using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;

namespace KarlsonMP
{
    public static class ServerBrowser
    {
        static GameObject go;

        public static void Start()
        {
            go = new GameObject("ServerBrowser");
            go.AddComponent<ServerBrowserBehaviour>();
        }
        public static void Destroy()
        {
            UnityEngine.Object.Destroy(go);
            if (ServerBrowserBehaviour.QueryClient != null)
            {
                ServerBrowserBehaviour.QueryClient.Disconnect();
                ServerBrowserBehaviour.QueryClient = null;
            }
        }
    }

    public class ServerBrowserBehaviour : MonoBehaviour
    {
        public void Start()
        {
            grayTx = new Texture2D(1, 1);
            grayTx.SetPixel(0, 0, new Color(35f / 255f, 31f / 255f, 32f / 255f));
            grayTx.Apply();
            blackTx = new Texture2D(1, 1);
            blackTx.SetPixel(0, 0, new Color(0, 0, 0));
            blackTx.Apply();
            listTx = new Texture2D(1, 1);
            listTx.SetPixel(0, 0, new Color(25f / 255f, 21f / 255f, 22f / 255f));
            listTx.Apply();
            listAlt = new Texture2D(1, 1);
            listAlt.SetPixel(0, 0, new Color(15f / 255f, 11f / 255f, 12f / 255f));
            listAlt.Apply();

            // load user prefs
            if (File.Exists(Path.Combine(Loader.KMP_ROOT, "prefs")))
            {
                using(BinaryReader br = new BinaryReader(File.OpenRead(Path.Combine(Loader.KMP_ROOT, "prefs"))))
                {
                    userName = br.ReadString();
                    ushort x = br.ReadUInt16();
                    while (x-- > 0)
                        favorites.Add(br.ReadString());
                    x = br.ReadUInt16();
                    while (x-- > 0)
                        recent.Add(br.ReadString());
                }
            }
        }

        void SavePrefs()
        {
            using (BinaryWriter bw = new BinaryWriter(File.OpenWrite(Path.Combine(Loader.KMP_ROOT, "prefs"))))
            {
                bw.Write(userName);
                bw.Write((ushort)favorites.Count);
                foreach (var x in favorites)
                    bw.Write(x);
                bw.Write((ushort)recent.Count);
                foreach (var x in recent)
                    bw.Write(x);
            }
        }

        string userName;
        List<string> favorites = new List<string>(), recent = new List<string>();
        Texture2D grayTx, blackTx, listTx, listAlt;
        GUIStyle vAlign = null, listBtn;

        int tab = 0;
        string[] toolbar = new string[] { "Favorites", "Recent", "Internet" };
        string selectedServer = "";
        Vector2 scrollPos = new Vector2(0, 0);
        bool addServer = false;
        string sbAddr = "";

        Dictionary<string, (string, ushort, ushort, ulong)> ServerQueryCache = new Dictionary<string, (string, ushort, ushort, ulong)>();
        public static Riptide.Client QueryClient;
        string QueryServerAddr;
        void QueryServer(string addr)
        {
            if (QueryClient != null) return; // already querying a server
            QueryServerAddr = addr;
            QueryClient = new Riptide.Client("QueryClient");
            QueryClient.Connect(Loader.ToIPAddress(addr.Split(':')[0]).ToString() + ':' + addr.Split(':')[1]);
            QueryClient.MessageReceived += QueryClient_MessageReceived;
            QueryClient.ConnectionFailed += QueryClient_ConnectionFailed;
        }

        private void QueryClient_ConnectionFailed(object sender, Riptide.ConnectionFailedEventArgs e)
        {
            ServerQueryCache.Add(QueryServerAddr, (QueryServerAddr + " | <color=red>Failed to ping server</color>", 0, 0, ulong.MaxValue));
            QueryClient = null;
        }

        static string EscapeMOTD(string str)
        {
            str = str.Trim();
            // we allow at most one '\n' and all tags except <size>
            if(str.Contains("\n"))
            {
                int idx = str.IndexOf("\n");
                if(str.Substring(idx + 1).Contains("\n"))
                    str = str.Substring(0, idx) + "\n" + str.Substring(idx + 1).Replace("\n", " ");
            }
            return str.Replace("<size", "<<i></i>size").Replace("</size", "<<i></i>/size");
        }

        private void QueryClient_MessageReceived(object sender, Riptide.MessageReceivedEventArgs e)
        {
            try
            {
                ServerQueryCache.Add(QueryServerAddr, (EscapeMOTD(e.Message.GetString()), e.Message.GetUShort(), e.Message.GetUShort(), e.Message.GetULong()));
            }
            catch
            {
                try
                {
                    ServerQueryCache.Add(QueryServerAddr, (EscapeMOTD(e.Message.GetString()), e.Message.GetUShort(), e.Message.GetUShort(), 0));
                }
                catch
                {
                    try
                    {
                        ServerQueryCache.Add(QueryServerAddr, (EscapeMOTD(e.Message.GetString()), 0, 0, 0));
                    }
                    catch
                    {
                        ServerQueryCache.Add(QueryServerAddr, (QueryServerAddr + " | <color=red>Query error</color>", 0, 0, ulong.MaxValue));
                    }
                }
            }
            QueryClient.Disconnect();
            QueryClient = null;
        }

        public void OnGUI()
        {
            if(vAlign == null)
            {
                vAlign = new GUIStyle(GUI.skin.label);
                vAlign.alignment = TextAnchor.MiddleLeft;

                listBtn = new GUIStyle(GUI.skin.button);
                listBtn.normal.background = Texture2D.blackTexture;
                listBtn.hover.background = Texture2D.blackTexture;
                listBtn.active.background = Texture2D.blackTexture;
            }
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, Screen.width, Screen.height), grayTx, new Rect(0, 0, 1, 1));
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, Screen.width, 30), blackTx, new Rect(0, 0, 1, 1));
            tab = GUI.Toolbar(new Rect(5, 5, 350, 20), tab, toolbar);
            GUI.Label(new Rect(360, 5, 100, 20), "Username");
            userName = GUI.TextField(new Rect(425, 5, 150, 20), userName);
            if (GUI.Button(new Rect(Screen.width - 75, 5, 70, 20), "Exit")) Application.Quit();
            if(tab == 0)
            {
                if (GUI.Button(new Rect(Screen.width - 230, 5, 150, 20), "Add Server"))
                {
                    addServer = true;
                    sbAddr = "";
                }
            }
            if (addServer)
                GUI.Window(2, new Rect(Screen.width / 2 - 150, Screen.height / 2 - 45, 300, 90), _ =>
                {
                    GUI.Label(new Rect(5, 20, 300, 20), "Enter server IP:");
                    sbAddr = GUI.TextArea(new Rect(5, 40, 290, 20), sbAddr);
                    if(sbAddr.Contains('\n') || GUI.Button(new Rect(10, 65, 135, 20), "Add Server"))
                    {
                        sbAddr = sbAddr.Replace("\n", "");
                        if (!sbAddr.Contains(':'))
                            sbAddr += ":11337";
                        if(favorites.Contains(sbAddr))
                            favorites.Remove(sbAddr); // move server to top of list
                        favorites.Insert(0, sbAddr);
                        addServer = false;
                        SavePrefs();
                    }
                    if(GUI.Button(new Rect(155, 65, 135, 20), "Cancel"))
                        addServer = false;
                }, "Add server to favorites");
            // draw server list
            int serverCount = 0;
            if (tab == 0)
                serverCount = favorites.Count;
            else if (tab == 1)
                serverCount = recent.Count;
            else if (tab == 2) // TODO: add server browser
                serverCount = 0;
            scrollPos = GUI.BeginScrollView(new Rect(0, 30, Screen.width, Screen.height - 150), scrollPos, new Rect(0, 0, Screen.width - 20, 50 * serverCount));
            List<string> serverList;
            if (tab == 0)
                serverList = favorites;
            else if (tab == 1)
                serverList = recent;
            else
                serverList = null;
            if (serverList == null)
                GUI.Label(new Rect(5, 5, 500, 20), "Public Server Browser not implemented yet");
            else
            {
                bool altTx = false;
                int i = 0;
                foreach(var x in serverList)
                {
                    GUI.DrawTextureWithTexCoords(new Rect(0, 50 * i, Screen.width, 50), altTx ? listAlt : listTx, new Rect(0, 0, 1, 1));
                    if(!ServerQueryCache.ContainsKey(x))
                    {
                        if(QueryServerAddr == x)
                            GUI.Label(new Rect(5, 50 * i, 350, 50), x + " | <color=yellow>Pinging server...</color>", vAlign);
                        else
                        {
                            GUI.Label(new Rect(5, 50 * i, 350, 50), x, vAlign);
                            QueryServer(x);
                        }
                    }
                    else
                    {
                        if (ServerQueryCache[x].Item4 < NetworkManager.ProtocolVersion)
                            GUI.Label(new Rect(5, 50 * i, 500, 50), "<color=red>[Old Server]</color> " + ServerQueryCache[x].Item1, vAlign);
                        else if (ServerQueryCache[x].Item4 != ulong.MaxValue && ServerQueryCache[x].Item4 > NetworkManager.ProtocolVersion)
                            GUI.Label(new Rect(5, 50 * i, 500, 50), "<color=red>[Old Client]</color> " + ServerQueryCache[x].Item1, vAlign);
                        else
                            GUI.Label(new Rect(5, 50 * i, 500, 50), ServerQueryCache[x].Item1, vAlign);
                        GUI.Label(new Rect(510, 50 * i, 150, 50), ServerQueryCache[x].Item2 + "/" + ServerQueryCache[x].Item3, vAlign);
                    }
                    if (GUI.Button(new Rect(0, 50 * i, Screen.width, 50), "", listBtn))
                        selectedServer = x;
                    ++i;
                    altTx = !altTx;
                }
            }
            GUI.EndScrollView();
            if(selectedServer != "")
            {
                if(ServerQueryCache.ContainsKey(selectedServer))
                    GUI.Label(new Rect(5, Screen.height - 30, 350, 20), ServerQueryCache[selectedServer].Item1);
                else
                    GUI.Label(new Rect(5, Screen.height - 30, 350, 20), selectedServer);
                if (GUI.Button(new Rect(300, Screen.height - 30, 100, 20), "Connect"))
                {
                    // add server to recent
                    if (recent.Contains(selectedServer))
                        recent.Remove(selectedServer);
                    recent.Insert(0, selectedServer);
                    SavePrefs();
                    // resolve server name
                    string hostname = selectedServer.Split(':')[0];
                    NetworkManager.Connect(Loader.ToIPAddress(hostname).ToString() + ':' + selectedServer.Split(':')[1], userName);
                    ServerBrowser.Destroy();
                }
                if(favorites.Contains(selectedServer) && GUI.Button(new Rect(405, Screen.height - 30, 175, 20), "Remove from Favorites"))
                {
                    favorites.Remove(selectedServer);
                    SavePrefs();
                }
                if(!favorites.Contains(selectedServer) && GUI.Button(new Rect(405, Screen.height - 30, 175, 20), "Add to Favorites"))
                {
                    favorites.Add(selectedServer);
                    SavePrefs();
                }
            }
        }

        public void Update()
        {
            QueryClient?.Update();
        }
    }
}
