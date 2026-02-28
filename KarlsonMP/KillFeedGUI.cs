using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMP
{
    public static class KillFeedGUI
    {
        public static void AddText(string text)
        {
            feed.Add(text);
        }

        public static void DamageReport(string text)
        {
            damageReport.Add(text);
        }

        public static _feedItem pin = null;
        private static readonly List<_feedItem> feed = new List<_feedItem>();
        private static readonly List<_feedItem> damageReport = new List<_feedItem>();

        private static bool init = false;
        private static GUIStyle center;

        public static void _draw()
        {
            if (!init)
            {
                init = true;
                center = new GUIStyle();
                center.alignment = TextAnchor.MiddleCenter;
                center.normal.textColor = Color.white;
            }
            int offY = 0;
            foreach (var item in pin != null ? feed.Prepend(pin) : feed)
            {
                if (item.state < 25) SetGUIOpacity(item.state / 25f);
                else if (item.state > 125) SetGUIOpacity((150 - item.state) / 25f);
                else SetGUIOpacity(1f);
                GUI.Box(new Rect(Screen.width - 205, 5 + offY, 200, 40), "");
                GUI.Label(new Rect(Screen.width - 205, 5 + offY, 200, 40), item.text, center);
                offY += 45;
            }
            offY = 0;
            foreach (var item in damageReport)
            {
                if (item.state > 50) SetGUIOpacity((70 - item.state) / 20f);
                else SetGUIOpacity(1f);
                GUI.Label(new Rect(Screen.width / 2 + 10f, Screen.height / 2 + offY, 100f, 45f), "<size=25>" + item.text + "</size>");
                offY += 30;
            }
            SetGUIOpacity(1f);
        }

        private static void SetGUIOpacity(float a) => GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, a);
        public static void _advance()
        {
            if (pin != null && pin.state++ >= 150)
                pin = null;
            for (int i = 0; i < feed.Count; i++)
            {
                feed[i].state++;
                if (feed[i].state == 150)
                {
                    feed.RemoveAt(i);
                    i--;
                }
            }
            for (int i = 0; i < damageReport.Count; i++)
            {
                damageReport[i].state++;
                if (damageReport[i].state == 70)
                {
                    damageReport.RemoveAt(i);
                    i--;
                }
            }
        }

        public class _feedItem
        {
            public _feedItem(string _text)
            {
                text = _text;
                state = 0;
            }

            public string text;
            public int state;

            public static implicit operator _feedItem(string _text) { return new _feedItem(_text); }
        }
    }
}
