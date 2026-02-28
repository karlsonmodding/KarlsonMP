using Riptide;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMP
{
    public class Scoreboard
    {
        private static List<ScoreboardEntry> entries = new List<ScoreboardEntry>();
        public static void UpdateScoreboard(Message message)
        {
            entries.Clear();
            int count = message.GetInt();
            while (count-- > 0)
                entries.Add(new ScoreboardEntry(message.GetUShort(), message.GetString(), message.GetInt(), message.GetInt(), message.GetInt()));
            //entries = entries.OrderByDescending(x => x.score).ToList();
        }

        private static Texture2D _grayTx, _blackTx;
        private static GUIStyle hAlign = null;

        public static void GuiCtor()
        {
            _grayTx = new Texture2D(1, 1);
            _grayTx.SetPixel(0, 0, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            _grayTx.Apply();
            _blackTx = new Texture2D(1, 1);
            _blackTx.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.5f));
            _blackTx.Apply();
        }

        private static bool scoreboardOpened = false;
        public static void Update()
        {
            scoreboardOpened = Input.GetKey(KeyCode.Tab);
        }

        public static void OnGUI()
        {
            if (!scoreboardOpened) return;
            if (hAlign == null)
            {
                hAlign = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft
                };
            }
            // scoreboard header
            float width = 500f, basex = (Screen.width - width) / 2f, basey = 100f;
            GUI.DrawTexture(new Rect(basex, basey, width, 30f), _blackTx);
            GUI.Label(new Rect(basex + 5f, basey, 50f, 30f), "ID", hAlign);
            GUI.Label(new Rect(basex + 60f, basey, 300f, 30f), "Username", hAlign);
            GUI.Label(new Rect(basex + 350f, basey, 100f, 30f), "Kills", hAlign);
            GUI.Label(new Rect(basex + 400f, basey, 100f, 30f), "Deaths", hAlign);
            GUI.Label(new Rect(basex + 450f, basey, 100f, 30f), "Score", hAlign);
            float yOff = 30f;
            bool alt = true;
            foreach (var score in entries)
            {
                if (alt)
                    GUI.DrawTexture(new Rect(basex, basey + yOff, width, 30f), _grayTx);
                else
                    GUI.DrawTexture(new Rect(basex, basey + yOff, width, 30f), _blackTx);
                alt = !alt;
                if (score.id != ushort.MaxValue)
                {
                    GUI.Label(new Rect(basex + 5f, basey + yOff, 50f, 30f), score.id.ToString(), hAlign);
                    if (score.kills != int.MinValue)
                        GUI.Label(new Rect(basex + 350f, basey + yOff, 100f, 30f), score.kills.ToString(), hAlign);
                    if (score.deaths != int.MinValue)
                        GUI.Label(new Rect(basex + 400f, basey + yOff, 100f, 30f), score.deaths.ToString(), hAlign);
                    if (score.score != int.MinValue)
                        GUI.Label(new Rect(basex + 450f, basey + yOff, 100f, 30f), score.score.ToString(), hAlign);
                }
                GUI.Label(new Rect(basex + 60f, basey + yOff, 300f, 30f), score.name, hAlign);
                yOff += 30f;
            }
        }

        public class ScoreboardEntry
        {
            public int id;
            public string name;
            public int kills;
            public int deaths;
            public int score;
            public ScoreboardEntry(int id, string name, int kills, int deaths, int score)
            {
                this.id = id;
                this.name = name;
                this.kills = kills;
                this.deaths = deaths;
                this.score = score;
            }
        }
    }
}
