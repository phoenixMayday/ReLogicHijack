using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;

namespace ReLogicHijack
{
    // Game components update after Terraria's player physics.
    public sealed class FlightController : GameComponent
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        public static FlightController Instance { get; private set; }
        public bool IsFlying { get; private set; }
        public string Notice { get; private set; }

        private Vector2 anchor;
        private Player currentPlayer;
        private int syncTicks;
        private bool toggleHeld;
        private double noticeSeconds;

        public FlightController(Game game) : base(game)
        {
            UpdateOrder = int.MaxValue;
            Notice = "";
            Instance = this;
        }

        private static bool Held(Keys key)
        {
            return (GetAsyncKeyState((int)key) & 0x8000) != 0;
        }

        private static bool TypingOrMenu()
        {
            return Main.blockInput || Main.drawingPlayerChat ||
                Main.editSign || Main.editChest || Main.inFancyUI;
        }

        public bool SetFlight(bool enabled)
        {
            var player = Main.LocalPlayer;

            if (enabled)
            {
                if (Main.gameMenu || !player.active || player.dead || Main.netMode == 2) return false;

                currentPlayer = player;
                anchor = player.position;
                IsFlying = true;
                player.velocity = Vector2.Zero;
                Notice = "";
            }
            else
            {
                if (IsFlying && player.active && !player.dead && !Main.gameMenu &&
                    Collision.SolidCollision(player.position, player.width, player.height))
                {
                    Notice = "Move into open space before disabling";
                    noticeSeconds = 4;
                    return false;
                }

                IsFlying = false;
                currentPlayer = null;
                Notice = "";
            }

            Bootstrap.Log("Noclip flight " + (IsFlying ? "ON" : "OFF") + ": " + player.position);
            SyncPosition(player);
            return true;
        }

        // Shared by keyboard controls and the isolated integration probe.
        public void Move(Vector2 direction, float pixelsPerSecond, double elapsedSeconds)
        {
            if (!IsFlying || currentPlayer == null) return;
            if (direction.LengthSquared() > 1) direction.Normalize();

            // Limit the movement step after a slow frame.
            float dt = (float)Math.Max(0, Math.Min(elapsedSeconds, 0.05));
            Vector2 next = anchor + direction * pixelsPerSecond * dt;
            next.X = MathHelper.Clamp(next.X, 160, Main.maxTilesX * 16 - 160 - currentPlayer.width);
            next.Y = MathHelper.Clamp(next.Y, 160, Main.maxTilesY * 16 - 160 - currentPlayer.height);

            if (Main.netMode == 1 && Main.sectionManager != null &&
                (!Main.sectionManager.TileLoaded((int)next.X / 16, (int)next.Y / 16) ||
                 !Main.sectionManager.TileLoaded((int)(next.X + currentPlayer.width) / 16,
                     (int)(next.Y + currentPlayer.height) / 16)))
            {
                Notice = "Waiting for world data";
                noticeSeconds = 1;
                next = anchor;
            }

            anchor = next;
            currentPlayer.position = anchor;
            currentPlayer.velocity = Vector2.Zero;
            currentPlayer.gfxOffY = 0;
            currentPlayer.netOffset = Vector2.Zero;
            currentPlayer.fallStart = currentPlayer.fallStart2 = (int)(anchor.Y / 16);
        }

        public override void Update(GameTime gameTime)
        {
            try
            {
                // Catch brief taps without toggling repeatedly while held.
                short toggle = GetAsyncKeyState((int)Keys.F6);
                bool held = (toggle & 0x8000) != 0;
                bool pressed = !toggleHeld && ((toggle & 1) != 0 || held);
                toggleHeld = held;

                if (Main.gameMenu || Main.netMode == 2 ||
                    Main.LocalPlayer.dead || !Main.LocalPlayer.active ||
                    (currentPlayer != null && currentPlayer != Main.LocalPlayer))
                {
                    if (IsFlying)
                    {
                        Bootstrap.Log("Noclip flight reset on death, disconnect, or player change.");
                    }

                    IsFlying = false;
                    currentPlayer = null;
                    return;
                }

                if (pressed && Main.instance.IsActive && !TypingOrMenu())
                {
                    SetFlight(!IsFlying);
                }

                double dt = gameTime.ElapsedGameTime.TotalSeconds;
                if (noticeSeconds > 0)
                {
                    noticeSeconds -= dt;
                    if (noticeSeconds <= 0) Notice = "";
                }

#if FLIGHT_LAB
                if (FlightLab.Update(this, dt)) return;
#endif
                if (!IsFlying || Main.gamePaused) return;

                Vector2 direction = Vector2.Zero;
                if (Main.instance.IsActive && !TypingOrMenu() && !Main.playerInventory)
                {
                    if (Held(Keys.A) || Held(Keys.Left)) direction.X--;
                    if (Held(Keys.D) || Held(Keys.Right)) direction.X++;
                    if (Held(Keys.W) || Held(Keys.Up)) direction.Y--;
                    if (Held(Keys.S) || Held(Keys.Down)) direction.Y++;
                }

                float speed = Held(Keys.LeftShift) || Held(Keys.RightShift) ? 720 : 240;
                Move(direction, speed, dt);

                if (++syncTicks >= 2)
                {
                    syncTicks = 0;
                    SyncPosition(Main.LocalPlayer);
                }
            }
            catch (Exception ex)
            {
                IsFlying = false;
                currentPlayer = null;
                Bootstrap.Log("Flight disabled after error: " + ex);
                Game.Components.Remove(this);
            }
        }

        public static void SyncPosition(Player player)
        {
            if (Main.netMode == 1 && !Main.gameMenu && player.active)
            {
                // Report movement using Terraria's existing packet.
                NetMessage.SendData(13, -1, -1, null, player.whoAmI);
            }
        }
    }
}
