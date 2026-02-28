using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMP
{
    public class MonoHooks : MonoBehaviour
    {
        public static GameObject playerPrefab;
        public static GameObject bulletPrefab;
        public static Texture2D _grayTx = new Texture2D(1, 1);
        public static GUIStyle _watermarkFont = new GUIStyle();

        public void Start()
        {
            // load prefabs
            AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(Loader.KMP_ROOT, "karlsonmp.bundle"));
            playerPrefab = (GameObject)bundle.LoadAsset("assets/karlsonmp/playerprefab.prefab");
            Inventory.GuiCtor(bundle);
            Inventory.LoadAssets(bundle);
            KMP_AudioManager.Initialize(bundle);

            KarlsonMapEditor.LevelLoader.Main.Skybox.Default = RenderSettings.skybox;
            KarlsonMapEditor.LevelLoader.Main.Skybox.Procedural = bundle.LoadAsset<Material>("assets/kme/skyboxprocedural.mat");
            KarlsonMapEditor.LevelLoader.Main.Skybox.SixSided = bundle.LoadAsset<Material>("assets/kme/skyboxsixsided.mat");
            KarlsonMapEditor.LevelLoader.MaterialManager.defaultShader = bundle.LoadAsset<Shader>("assets/kme/standardvariants.shader");

            KMP_Console.Log("[Bootstrap] monohooks loaded assets");

            GuiCtor();
            PlaytimeLogic.Start();
            Scoreboard.GuiCtor();
            HUDMessages.GuiCtor();
        }

        public void FixedUpdate()
        {
            if (NetworkManager.client != null)
                NetworkManager.client.Update();

            PlaytimeLogic.FixedUpdate();
            KillFeedGUI._advance();
        }

        public void Update()
        {
            Time.timeScale = 1f;
            PlaytimeLogic.Update();
            Inventory.Update();
            Scoreboard.Update();
            KMP_Console.OnUpdate();
        }

        public void OnGUI()
        {
            KillFeedGUI._draw();
            PlaytimeLogic.OnGUI();
            Inventory.OnGUI();
            Scoreboard.OnGUI();
            HUDMessages.OnGUI();

            if (dialogData.show)
            {
                dialogData.dialog_ = GUI.Window(71, dialogData.dialog_, (int id) =>
                {
                    GUI.Label(new Rect(5f, 20f, 496f, 150f), dialogData.content);
                    if (dialogData.yes == "")
                        dialogData.yes = "Yes";
                    bool btnYes = false, btnNo = false;
                    if (dialogData.no == "")
                    {
                        float w = Math.Max(GUI.skin.button.CalcSize(new GUIContent(dialogData.yes)).x, 90f);
                        btnYes = GUI.Button(new Rect((490f - w) / 2, 175f, w + 10f, 20f), dialogData.yes);
                    }
                    else
                    {
                        float w = Math.Max(GUI.skin.button.CalcSize(new GUIContent(dialogData.yes)).x, 90f);
                        float w2 = Math.Max(GUI.skin.button.CalcSize(new GUIContent(dialogData.no)).x, 90f);
                        btnYes = GUI.Button(new Rect((240f - w) / 2, 175f, w + 10f, 20f), dialogData.yes);
                        btnNo = GUI.Button(new Rect(250f + (240f - w2) / 2, 175f, w2 + 10f, 20f), dialogData.no);
                    }
                    if (btnYes)
                    {
                        dialogData.onYes();
                        dialogData.show = false;
                    }
                    if (btnNo)
                    {
                        dialogData.onNo();
                        dialogData.show = false;
                    }
                    GUI.DragWindow();
                }, dialogData.title);
            }

            GUI.Label(new Rect(0f, 0f, Screen.width - 5, Screen.height - 2), "KarlsonMP", _watermarkFont);
        }

        private void GuiCtor()
        {
            _grayTx.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
            _grayTx.Apply();

            _watermarkFont.fontSize = 15;
            _watermarkFont.normal.textColor = Color.white;
            _watermarkFont.alignment = TextAnchor.LowerRight;
        }
        static class dialogData
        {
            public static string title;
            public static string content;
            public static string yes;
            public static string no;
            public static bool show;
            public static Rect dialog_ = new Rect((Screen.width - 500f) / 2, (Screen.height - 200f) / 2, 500f, 200f);
            public static Action onYes;
            public static Action onNo;
        };
        public static void ShowDialog(string _title, string _content, string _yes = "Yes", string _no = "No", Action onYes = null, Action onNo = null)
        {
            dialogData.title = _title;
            dialogData.content = _content;
            dialogData.yes = _yes;
            dialogData.no = _no;
            dialogData.show = true;
            dialogData.dialog_ = new Rect((Screen.width - 500f) / 2, (Screen.height - 200f) / 2, 500f, 200f);
            dialogData.onYes = onYes;
            dialogData.onNo = onNo;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            PlaytimeLogic.ForcePause(true);
        }

        public void OnApplicationQuit()
        {
            NetworkManager.Quit();
            Process.GetCurrentProcess().Kill();
        }

        public void OnApplicationFocus(bool focus)
        {
            if (!focus)
            {
                PlaytimeLogic.ForcePause(true);
            }
        }
    }
}
