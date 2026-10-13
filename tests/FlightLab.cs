using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace ReLogicHijack
{
    // Opt-in probe using the same flight methods as the keyboard controls.
    internal static class FlightLab
    {
        private static int phase;
        private static int samples;
        private static double elapsed;
        private static Vector2 origin;
        private static Vector2 hover;
        private static bool aboveGround;
        private static bool crossedSolid;
        private static bool refusedUnsafeExit;
        private static float hoverDrift;
        private static readonly float[] durations = { 2, 0.6f, 0.5f, 1, 0.4f, 1, 1 };
        private static readonly Vector2[] directions =
        {
            Vector2.Zero, -Vector2.UnitY, Vector2.Zero,
            Vector2.UnitY, Vector2.UnitX, -Vector2.UnitY, Vector2.Zero
        };

        public static bool Update(FlightController flight, double dt)
        {
            if (phase >= durations.Length) return false;
            if (Main.netMode != 1 || Main.gameMenu ||
                !Main.LocalPlayer.active || Main.LocalPlayer.dead) return false;

            var p = Main.LocalPlayer;
            elapsed += dt;

            if (phase == 0)
            {
                if (elapsed < durations[0]) return true;

                origin = p.position;
                if (!flight.SetFlight(true)) throw new InvalidOperationException("Lab could not enable flight");

                phase = 1;
                elapsed = 0;
                Bootstrap.Log("FLIGHT_LAB_BEGIN origin=" + origin);
            }

            float speed = phase == 3 || phase == 5 ? 360 : 240;
            flight.Move(directions[phase], speed, dt);
            FlightController.SyncPosition(p);

            bool solid = Collision.SolidCollision(p.position, p.width, p.height);
            if (p.position.Y < origin.Y - 100) aboveGround = true;
            if (phase == 4 && solid)
            {
                crossedSolid = true;
                if (!refusedUnsafeExit) refusedUnsafeExit = !flight.SetFlight(false) && flight.IsFlying;
            }

            if (phase == 2) hoverDrift = Math.Max(hoverDrift, Vector2.Distance(hover, p.position));
            if (++samples % 6 == 0)
            {
                Bootstrap.Log("FLIGHT_LAB_SAMPLE phase=" + phase +
                    " x=" + p.position.X.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                    " y=" + p.position.Y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                    " solid=" + solid + " ghost=" + p.ghost);
            }

            if (elapsed < durations[phase]) return true;

            phase++;
            elapsed = 0;
            if (phase == 2) hover = p.position;
            if (phase < durations.Length) return true;

            bool disabled = flight.SetFlight(false);
            Bootstrap.Log("FLIGHT_LAB_RESULT aboveGround=" + aboveGround + " crossedSolid=" + crossedSolid +
                " refusedUnsafeExit=" + refusedUnsafeExit + " hoverDrift=" + hoverDrift.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                " disabledInAir=" + disabled + " ghost=" + p.ghost + " netMode=" + Main.netMode);
            return true;
        }
    }
}
