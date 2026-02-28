using ServerKMP.GamemodeApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerKMP
{
    internal class Program
    {
        static Thread? mainThread;
        static bool serverRunning = true;
        public static bool IsServerRunning => serverRunning;
        public static void ExitServer() => serverRunning = false;

        static void Main(string[] _)
        {
            Config.LoadConfig();
            mainThread = new Thread(MainThread);
            mainThread.Start();

            CommandManager.Init();
            MapManager.Init();
            while (serverRunning)
            {
                string command = Console.ReadLine()!;
                string[] args = command.Split(' ');
                string label = args[0];
                CommandManager.Execute(label, args);
            }
        }

        static void MainThread()
        {
            Console.WriteLine($"Main Thread started, running at {Config.TPS} TPS (1 tick every {Config.MSPT} ms)");

            // start networking
            NetworkManager.Start();

            // add asm resolve
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            // load gamemode
            GamemodeManager.Init();

            DateTime _nextLoop = DateTime.Now;
            while (serverRunning)
            {
                while (_nextLoop < DateTime.Now)
                {
                    Update();
                    GamemodeManager.SafeCall(GamemodeManager.currentGamemode!.ServerTick);

                    // run scheduled tasks
                    foreach (var x in KMP_TaskScheduler.scheduledTasks)
                    {
                        if (x.Time < DateTime.Now)
                        {
                            x.Task();
                            x.ran = true;
                        }
                    }
                    KMP_TaskScheduler.ClearAndAddTasks();

                    _nextLoop = _nextLoop.AddMilliseconds(Config.MSPT);
                    if (_nextLoop > DateTime.Now)
                        Thread.Sleep(_nextLoop - DateTime.Now); // don't overload server
                }
            }

            Console.WriteLine("Shutting down Main Thread");
            GamemodeManager.SafeCall(GamemodeManager.currentGamemode!.OnStop);

            // shutdown server
            NetworkManager.Exit();
        }

        private static System.Reflection.Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
        {
            var dll = new AssemblyName(args.Name).Name + ".dll";
            if (File.Exists(dll))
                return Assembly.LoadFile(Path.Combine(Directory.GetCurrentDirectory(), dll));
            return null;
        }

        static void Update()
        {
            NetworkManager.Update();
            TickManager.NextTick();
        }
    }
}
