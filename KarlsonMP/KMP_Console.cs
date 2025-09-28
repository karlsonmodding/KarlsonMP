using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMP
{
    public abstract class ConVar
    {
        public void Exec(string[] args)
        {
            if (args.Length == 1)
                KMP_Console.Log($"{args[0]} - '{GetValue()}'", true);
            else
                SetValue(args[1]);
        }

        public virtual void SetValue(string input) { }
        public virtual string GetValue() { return ""; }
        public override string ToString() => GetValue();
    }

    public class CV_ushort : ConVar
    {
        public ushort Value;
        public CV_ushort(ushort value)
        {
            Value = value;
        }
        public override string GetValue()
        {
            return Value.ToString();
        }
        public override void SetValue(string input)
        {
            if (ushort.TryParse(input, out ushort newValue))
                Value = newValue;
            else throw new Exception($"Error parsing '{input}'");
        }
        public static implicit operator ushort(CV_ushort c) => c.Value;
    }

    public static class KMP_Console
    {
        public static void Log(string message) => Log(message, false);
        public static void Log(string message, bool consoleOnly)
        {
            content += message + '\n';
            while (content.Split('\n').Length > 100) content = content.Substring(content.IndexOf("\n") + 1);
            if (!consoleOnly)
                File.AppendAllText(Path.Combine(Loader.KMP_ROOT, $"KarlsonMP.log"), message + "\r\n");
        }

        public static string Content => content;
        private static string content = "KarlsonMP reborn\n    made by devilExE\n    licensed under MIT License\n    karlsonmodding/KarlsonMP @ github.com\n\n";
        public static void ClearScreen() => content = "";

        public static Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>>();
        public static Dictionary<string, ConVar> convars = new Dictionary<string, ConVar>();
        public static Dictionary<string, string> binds = new Dictionary<string, string>();
        public static void _processCommand(string commandString)
        {
            Log(commandString, true);
            string[] args = commandString.Split(' ');
            string label = args[0];
            if(commands.ContainsKey(label))
                commands[label](args);
            else if(convars.ContainsKey(label))
                convars[label].Exec(args);
            else
                Log("<color=red>Unknwon command</color> " + label + ". Try running <color=yellow>cmds</color> for a list of commands.", true);
        }

        public static void OnUpdate()
        {
            foreach(var key in binds.Keys)
                if (Input.GetKeyDown(key))
                    _processCommand(binds[key]);
        }

        static bool cfg_ran = false;
        public static void Autoexec()
        {
            if (cfg_ran) return;
            cfg_ran = true;
            if (!File.Exists(Path.Combine(Loader.KMP_ROOT, "autoexec.txt"))) return;
            Log("Running 'autoexec.txt'", true);
            foreach (var line in File.ReadAllLines(Path.Combine(Loader.KMP_ROOT, "autoexec.txt")))
                _processCommand(line.Trim());
        }
    }
}
