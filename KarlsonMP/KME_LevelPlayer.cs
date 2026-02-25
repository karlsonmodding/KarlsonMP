using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine;
using System.Xml.Linq;

namespace KarlsonMP
{
    public static class KME_LevelPlayer
    {
        public static Texture2D[] gameTex { get; private set; } = null;


        public static void InitGameTex()
        {
            List<Texture2D> temp = new List<Texture2D>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                switch (t.name)
                {
                    default: break;
                    case "GridBox_Default":
                    case "prototype_512x512_grey3":
                    case "prototype_512x512_white":
                    case "prototype_512x512_yellow":
                    case "Floor":
                    case "Blue":
                    case "Red":
                    case "Barrel":
                    case "Orange":
                    case "Yellow":
                    case "UnityWhite":
                    case "UnityNormalMap":
                    case "Sunny_01B_down":
                        if (temp.Count(x => x.name == t.name) == 0)
                            temp.Add(t);
                        break;
                }
            }
            gameTex = temp.ToArray();
            if (gameTex.Length != 13) KMP_Console.Log("<color=red>Invalid game texture array. Expected 13 items, got " + gameTex.Length + "</color>");
            foreach (var t in gameTex)
            {
                KMP_Console.Log(t.name);
            }
            KarlsonMapEditor.LevelLoader.Main.Init(new PrefabProvider(), log => KMP_Console.Log("[KME] " + log), gameTex);
        }

        public static void LoadLevel(string name, byte[] data, bool legacy)
        {
            PropManager.ClearProps();
            try
            {
                if (legacy)
                    KarlsonMapEditor.LevelLoader.LevelPlayer.LoadLevel(name, data, compressed: false, post_load: () => ClientSend.RequestScene());
                else
                {
                    try
                    {
                        KarlsonMapEditor.LevelLoader.LevelPlayer.LoadLevel(name, data, post_load: () => ClientSend.RequestScene());
                    }
                    catch (Exception ex2)
                    {
                        KillFeedGUI.AddText("Failed to load map.\nTrying legacy format.");
                        KMP_Console.Log(ex2.ToString());
                        KarlsonMapEditor.LevelLoader.LevelPlayer.LoadLevel(name, data, compressed: false, post_load: () => ClientSend.RequestScene());
                    }
                }
            }
            catch (Exception ex)
            {
                KillFeedGUI.AddText("Failed to load map.\nReturning to browser.");
                KMP_Console.Log(ex.ToString());
                PlaytimeLogic.DisconnectToBrowser();
            }
        }

        class PrefabProvider : KarlsonMapEditor.LevelLoader.IPrefabProvider
        {
            public override GameObject NewGlass()
            {
                return KMP_PrefabManager.NewGlass();
            }

            public override PhysicMaterial BounceMaterial()
            {
                return KMP_PrefabManager.BounceMaterial();
            }
        }
    }
}
