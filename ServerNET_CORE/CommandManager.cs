using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ServerKMP
{
    public static class CommandManager
    {
        private static Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>>();

        public static void Init()
        {
            commands.Add("exit", (_) =>
            {
                Console.WriteLine("Quitting server..");
                Program.ExitServer();
            });
            commands.Add("map", (args) =>
            {
                if (args.Length != 2) Console.WriteLine("map [mapname] - change map to [mapname]");
                else MapManager.LoadMap(args[1]);
            });
            commands.Add("maps", (args) =>
            {
                Console.WriteLine("Default maps:");
                foreach (var x in MapManager.defaultMaps)
                    Console.WriteLine(x.name);
                Console.WriteLine("Custom maps:");
                foreach(var x in Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "Maps")))
                    if(x.EndsWith(".kme_raw") && File.Exists(x.Replace(".kme_raw", ".kme_data")))
                        Console.WriteLine(Path.GetFileNameWithoutExtension(x));
            });
            commands.Add("gamemode", (args) =>
            {
                if (args.Length != 2) Console.WriteLine("gamemode [mode] - change gamemode to [mode]");
                else GamemodeManager.LoadGamemode(args[1]);
            });

            commands.Add("cmds", (_) =>
            {
                Console.WriteLine("List of commands:");
                foreach (var x in commands)
                    Console.WriteLine(x.Key);
            });
        }

        public static void Execute(string label, string[] args)
        {
            if(!commands.ContainsKey(label))
                Console.WriteLine($"Unknown command {label}. Run 'cmds' for a list of commands");
            else
                commands[label](args);
        }
    }
}

