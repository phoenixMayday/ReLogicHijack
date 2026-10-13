using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace ReLogicHijack
{
    // Avoid Terraria/XNA type references here so Main cannot initialize early.
    public static class Bootstrap
    {
        private static int started;
        private static readonly object logLock = new object();

        public static readonly string LogPath = Path.Combine(
            Path.GetTempPath(),
            "ReLogicHijack-" + Process.GetCurrentProcess().Id + ".log");

        public static void Start()
        {
            try
            {
                if (Interlocked.Exchange(ref started, 1) != 0) return;
                if (Assembly.GetEntryAssembly() == null ||
                    Assembly.GetEntryAssembly().GetName().Name != "Terraria") return;

                Log("Dependency module initializer executed. Overlay: " + typeof(Bootstrap).Assembly.Location);

                var thread = new Thread(WaitForGame);
                thread.Name = "ReLogicHijack bootstrap";
                thread.IsBackground = true;
                thread.Start();
            }
            catch (Exception ex)
            {
                Log("Bootstrap failed: " + ex);
            }
        }

        private static void WaitForGame()
        {
            try
            {
                // LaunchGame sets SavePath before Main is ready.
                for (int attempt = 0; attempt < 600; attempt++)
                {
                    Thread.Sleep(100);

                    Assembly game = null;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.GetName().Name == "Terraria") game = assembly;
                    }

                    if (game == null) continue;

                    var program = game.GetType("Terraria.Program", true);
                    var savePath = program.GetField(
                        "SavePath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                    if (savePath == null || savePath.GetValue(null) == null) continue;

                    Thread.Sleep(1000);

                    // Register game components on Terraria's thread.
                    var main = game.GetType("Terraria.Main", true);
                    var install = typeof(Bootstrap).Assembly.GetType("ReLogicHijack.Overlay", true)
                        .GetMethod("Install", BindingFlags.Public | BindingFlags.Static);
                    var callback = (Action)Delegate.CreateDelegate(typeof(Action), install);
                    main.GetMethod("QueueMainThreadAction", new Type[] { typeof(Action) })
                        .Invoke(null, new object[] { callback });

                    Log("Overlay installation queued on the game thread.");
                    return;
                }

                Log("Game startup timed out; overlay not installed.");
            }
            catch (Exception ex)
            {
                Log("Overlay installation failed: " + ex);
            }
        }

        public static void Log(string message)
        {
            try
            {
                lock (logLock)
                {
                    File.AppendAllText(LogPath,
                        DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must not interrupt the game.
            }
        }
    }
}
