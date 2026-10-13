using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace ReLogicHijack
{
    // Draws status and registers the flight and fullbright demonstrations.
    public static class Overlay
    {
        private static SpriteBatch batch;
        private static Texture2D pixel;
        private static bool installed;
        private static bool firstFrame;
        private static int frames;
        private static int fps;
        private static int lastMode = -1;
        private static bool lastMenu;

        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static double nextSample = 1;

        public static void Install()
        {
            try
            {
                if (installed) return;

                installed = true;
                Main.instance.Components.Add(new FullbrightController(Main.instance));
                Main.instance.Components.Add(new FlightController(Main.instance));
                Main.OnPostDraw += Draw;
                Main.instance.Exiting += delegate { Dispose(); };

                Bootstrap.Log("Subscribed to Terraria.Main.OnPostDraw. Game " + Main.versionNumber);
            }
            catch (Exception ex)
            {
                Bootstrap.Log("Drawing event registration failed: " + ex);
            }
        }

        private static void Draw(GameTime gameTime)
        {
            bool began = false;

            try
            {
                frames++;
                if (clock.Elapsed.TotalSeconds >= nextSample)
                {
                    fps = frames;
                    frames = 0;
                    nextSample = clock.Elapsed.TotalSeconds + 1;
                }

                if (Main.netMode != lastMode || Main.gameMenu != lastMenu)
                {
                    lastMode = Main.netMode;
                    lastMenu = Main.gameMenu;
                    Bootstrap.Log("State: netMode=" + lastMode + ", gameMenu=" + lastMenu);
                }

                if (FontAssets.MouseText == null || !FontAssets.MouseText.IsLoaded) return;

                var device = Main.instance.GraphicsDevice;

                // Device recreation can occur after a display-mode change.
                if (batch == null || batch.IsDisposed || batch.GraphicsDevice != device ||
                    pixel == null || pixel.IsDisposed)
                {
                    Dispose();
                    batch = new SpriteBatch(device);
                    pixel = new Texture2D(device, 1, 1);
                    pixel.SetData(new Color[] { Color.White });
                }

                string mode = Main.gameMenu ? "Menu" :
                    Main.netMode == 1 ? "Multiplayer client" : "Single player";
                var flight = FlightController.Instance;
                var lighting = FullbrightController.Instance;

                string text = "ReLogicHijack / " + Main.versionNumber + "\n" + mode + "  |  " + fps + " FPS\n" +
                    "F4 Fullbright: " + (lighting != null && lighting.Fullbright ? "ON" : "OFF") +
                    "\nF6 Flight: " + (flight != null && flight.IsFlying ? "ON" : "OFF") +
                    "  |  WASD / arrows  |  Shift: fast";
                if (flight != null && !string.IsNullOrEmpty(flight.Notice))
                {
                    text += "\n" + flight.Notice;
                }

                var font = FontAssets.MouseText.Value;
                var size = font.MeasureString(text);

                // Bottom-left, in viewport pixels, independent of world/UI zoom.
                var viewport = device.Viewport;
                int x = 18;
                int y = Main.gameMenu ? 18 : Math.Max(18, viewport.Height - (int)size.Y - 42);
                var box = new Rectangle(x, y, (int)size.X + 28, (int)size.Y + 22);

                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone);
                began = true;

                batch.Draw(pixel, box, new Color(9, 16, 27, 220));
                batch.Draw(pixel, new Rectangle(x, y, 3, box.Height), new Color(85, 224, 185));
                batch.DrawString(font, text, new Vector2(x + 14, y + 11), new Color(232, 242, 250));

                batch.End();
                began = false;

                if (!firstFrame)
                {
                    firstFrame = true;
                    Bootstrap.Log("First overlay frame rendered.");
                }
            }
            catch (Exception ex)
            {
                if (began)
                {
                    try { batch.End(); }
                    catch { }
                }

                Main.OnPostDraw -= Draw;
                Bootstrap.Log("Overlay disabled after drawing error: " + ex);
                Dispose();
            }
        }

        private static void Dispose()
        {
            if (batch != null)
            {
                batch.Dispose();
                batch = null;
            }

            if (pixel != null)
            {
                pixel.Dispose();
                pixel = null;
            }
        }
    }
}
