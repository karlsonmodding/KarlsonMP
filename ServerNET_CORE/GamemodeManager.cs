using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ServerKMP
{
    public class GamemodeManager
    {
        public static GamemodeApi.Gamemode? currentGamemode;
        public static void SafeCall(Action call)
        {
            try
            {
                call();
            }
            catch (Exception e)
            {
                Console.WriteLine("[GAMEMODE ERROR] " + e.ToString());
            }
        }

        public static void Init()
        {
            if(!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Gamemodes", $"{Config.GAMEMODE}.dll")))
            {
                Console.WriteLine($"[ERROR] Couldn't find gamemode {Config.GAMEMODE}.dll");
                return;
            }
            var asm = AppDomain.CurrentDomain.Load(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "Gamemodes", $"{Config.GAMEMODE}.dll")));
            var type = asm.GetTypes().Where(x => x.BaseType == typeof(GamemodeApi.Gamemode)).FirstOrDefault();
            if(type == null)
            {
                Console.WriteLine("[ERROR] Couldn't find gamemode entrypoint");
                return;
            }
            currentGamemode = (GamemodeApi.Gamemode)Activator.CreateInstance(type, null)!;
            SafeCall(currentGamemode!.OnStart);
        }

        public static void LoadGamemode(string modeName)
        {
            SafeCall(currentGamemode!.OnStop);
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Gamemodes", $"{modeName}.dll")))
            {
                Console.WriteLine($"[ERROR] Couldn't find gamemode {modeName}.dll");
                return;
            }
            var asm = AppDomain.CurrentDomain.Load(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "Gamemodes", $"{modeName}.dll")));
            var type = asm.GetTypes().Where(x => x.BaseType == typeof(GamemodeApi.Gamemode)).FirstOrDefault();
            if (type == null)
            {
                Console.WriteLine("[ERROR] Couldn't find gamemode entrypoint");
                return;
            }
            currentGamemode = (GamemodeApi.Gamemode)Activator.CreateInstance(type, null)!;
            SafeCall(currentGamemode!.OnStart);
            // re-handshake all players to gamemode
            foreach (var i in NetworkManager.registeredOnGamemode)
                SafeCall(() => currentGamemode!.ProcessMessage(new GamemodeApi.MessageClientToServer.MessageHandshake(i, NetworkManager.usernameDatabase[i])));
        }
    }
}

