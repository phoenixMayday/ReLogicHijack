using System;
using System.IO;
using System.Reflection;
using System.Threading;

class ServerObserver
{
    // Read player state without patching the server.
    static Assembly server;
    static string log;
    static string stop;

    [STAThread]
    static void Main(string[] args)
    {
        string exe = args[0];
        log = args[1];
        stop = args[2];

        server = Assembly.LoadFrom(exe);
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        var thread = new Thread(Observe);
        thread.IsBackground = true;
        thread.Start();

        string[] launch = new string[args.Length - 3];
        Array.Copy(args, 3, launch, 0, launch.Length);
        server.EntryPoint.Invoke(null, new object[] { launch });
    }

    static Assembly Resolve(object sender, ResolveEventArgs e)
    {
        string suffix = "." + new AssemblyName(e.Name).Name + ".dll";

        foreach (string name in server.GetManifestResourceNames())
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                using (var stream = server.GetManifestResourceStream(name))
                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    return Assembly.Load(buffer.ToArray());
                }
            }
        }

        return null;
    }

    static void Observe()
    {
        try
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var savePath = server.GetType("Terraria.Program").GetField("SavePath", flags);
            while (savePath.GetValue(null) == null) Thread.Sleep(100);
            Thread.Sleep(2000);

            var main = server.GetType("Terraria.Main");
            var players = main.GetField("player", flags);
            var playerType = server.GetType("Terraria.Player");
            var entityType = server.GetType("Terraria.Entity");
            var active = playerType.GetField("active");
            var position = entityType.GetField("position");
            var ghost = playerType.GetField("ghost");

            int count = 0;
            while (true)
            {
                if (File.Exists(stop)) Environment.Exit(0);

                var list = (Array)players.GetValue(null);
                foreach (object p in list)
                {
                    if (p == null || !(bool)active.GetValue(p)) continue;

                    object vector = position.GetValue(p);
                    var v = vector.GetType();
                    string x = Convert.ToSingle(v.GetField("X").GetValue(vector))
                        .ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                    string y = Convert.ToSingle(v.GetField("Y").GetValue(vector))
                        .ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                    File.AppendAllText(log, DateTime.UtcNow.ToString("o") +
                        " SERVER x=" + x + " y=" + y + " ghost=" + ghost.GetValue(p) + Environment.NewLine);
                }

                if (++count > 1800) Environment.Exit(0); // Lab has a three minute limit.
                Thread.Sleep(100);
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(log, "OBSERVER_ERROR " + ex + Environment.NewLine);
        }
    }
}
