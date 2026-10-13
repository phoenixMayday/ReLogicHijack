using System;
using System.Reflection;
using Terraria;
using Terraria.Graphics.Light;

namespace ReLogicHijack
{
    // Optional lighting-only probe. Does not move or damage the player.
    internal static class FullbrightLab
    {
        private static bool completed;
        private static double elapsed;

        public static void Update(FullbrightController controller, double dt)
        {
            if (completed || Main.gameMenu || Main.LocalPlayer.dead) return;

            elapsed += dt;
            if (elapsed < 2) return;

            var field = typeof(Lighting).GetField(
                "_activeEngine", BindingFlags.NonPublic | BindingFlags.Static);
            var originalMode = Lighting.Mode;
            bool passed = true;

            try
            {
                foreach (LightMode mode in Enum.GetValues(typeof(LightMode)))
                {
                    controller.SetFullbright(false);
                    Lighting.Mode = mode;
                    object original = field.GetValue(null);

                    controller.SetFullbright(true);
                    var color = Lighting.GetColor(100, Main.maxTilesY - 100);
                    bool white = color.R == 255 && color.G == 255 && color.B == 255;

                    controller.SetFullbright(false);
                    bool restored = object.ReferenceEquals(original, field.GetValue(null));
                    passed &= white && restored;

                    Bootstrap.Log("FULLBRIGHT_LIGHT mode=" + mode + " white=" + white +
                        " originalEngineRestored=" + restored);
                }
            }
            finally
            {
                controller.SetFullbright(false);
                Lighting.Mode = originalMode;
                completed = true;
            }

            Bootstrap.Log("FULLBRIGHT_RESULT allModes=" + passed + " netMode=" + Main.netMode);
        }
    }
}
