using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Graphics.Light;

namespace ReLogicHijack
{
    // Uses the existing draw event and lighting interface; no method detours.
    public sealed class FullbrightController : GameComponent
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        public static FullbrightController Instance { get; private set; }
        public bool Fullbright { get; private set; }

        private readonly FieldInfo engineField = typeof(Lighting).GetField(
            "_activeEngine", BindingFlags.NonPublic | BindingFlags.Static);
        private bool lightHeld;
        private Player owner;

        public FullbrightController(Game game) : base(game)
        {
            UpdateOrder = int.MaxValue;
            Instance = this;

            if (engineField == null) throw new MissingFieldException("Lighting._activeEngine");

            Main.OnPreDraw += BeforeDraw;
            game.Exiting += delegate { Reset(); };
            Bootstrap.Log("Fullbright lighting wrapper registered.");
        }

        public void SetFullbright(bool enabled)
        {
            Fullbright = enabled;
            owner = enabled ? Main.LocalPlayer : null;

            RefreshLighting();
            Main.renderCount = 0;
            Main.renderNow = false;

            Bootstrap.Log("Fullbright " + (enabled ? "ON" : "OFF"));
        }

        private void Reset()
        {
            Fullbright = false;
            owner = null;
            RefreshLighting();
        }

        private void RefreshLighting()
        {
            var active = (ILightingEngine)engineField.GetValue(null);
            var wrapper = active as FullbrightEngine;

            // Keep the original engine updating, then unwrap it when disabled.
            if (Fullbright && !Main.gameMenu && active != null && wrapper == null)
            {
                engineField.SetValue(null, new FullbrightEngine(active));
            }
            else if ((!Fullbright || Main.gameMenu) && wrapper != null)
            {
                engineField.SetValue(null, wrapper.Inner);
            }
        }

        private void BeforeDraw(GameTime time)
        {
            RefreshLighting();
        }

        private bool Pressed()
        {
            short state = GetAsyncKeyState((int)Keys.F4);
            bool held = (state & 0x8000) != 0;
            bool pressed = !lightHeld && ((state & 1) != 0 || held);
            lightHeld = held;
            return pressed;
        }

        public override void Update(GameTime gameTime)
        {
            try
            {
                bool lightPress = Pressed();

                if (Main.gameMenu || Main.netMode == 2 ||
                    !Main.LocalPlayer.active || Main.LocalPlayer.dead ||
                    (owner != null && owner != Main.LocalPlayer))
                {
                    if (Fullbright) Reset();
                    return;
                }

                if (Main.instance.IsActive && !Main.blockInput && !Main.drawingPlayerChat &&
                    !Main.editSign && !Main.editChest && !Main.inFancyUI && lightPress)
                {
                    SetFullbright(!Fullbright);
                }

                RefreshLighting();

#if FULLBRIGHT_LAB
                FullbrightLab.Update(this, gameTime.ElapsedGameTime.TotalSeconds);
#endif
            }
            catch (Exception ex)
            {
                Reset();
                Bootstrap.Log("Fullbright disabled after error: " + ex);
                Main.OnPreDraw -= BeforeDraw;
                Game.Components.Remove(this);
            }
        }

        private sealed class FullbrightEngine : ILightingEngine
        {
            public readonly ILightingEngine Inner;

            public FullbrightEngine(ILightingEngine inner)
            {
                Inner = inner;
            }

            public Vector3 GetColor(int x, int y) { return Vector3.One; }

            public void AddLight(int x, int y, Vector3 color) { Inner.AddLight(x, y, color); }

            public void ProcessArea(Rectangle area) { Inner.ProcessArea(area); }

            public void Rebuild() { Inner.Rebuild(); }

            public void Clear() { Inner.Clear(); }
        }
    }
}
