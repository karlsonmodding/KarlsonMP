using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerKMP
{
    public static class MapManager
    {
        public static List<Map> defaultMaps = new List<Map>();

        public static void Init()
        {
            defaultMaps.Add(new Map("1Sandbox0", new Dictionary<string, List<(string, Vector3, Vector3, Vector3)>> {
                { "spawn", new List<(string, Vector3, Vector3, Vector3)>() { ("spawn", new Vector3(0f, 0f, -50f), Vector3.zero, Vector3.zero) }
                } }, true, true));
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Maps"));
            currentMap = defaultMaps[0];
        }

        public static Map? currentMap = null;

        public static void LoadMap(string mapName)
        {
            if (defaultMaps.Count(x => x.name == mapName) == 1)
            {
                currentMap = (from x in defaultMaps where x.name == mapName select x).First();
                Console.WriteLine("Switched map to default map " + mapName);
            }
            else
            {
                if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Maps", mapName + ".kme_data")))
                    Console.WriteLine($"File '{mapName}.kme_data' doesn't exist!");
                else
                {
                    // load metadata
                    Dictionary<string, List<(string, Vector3, Vector3, Vector3)>> map_data = new Dictionary<string, List<(string, Vector3, Vector3, Vector3)>>();
                    using (FileStream fs = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), "Maps", mapName + ".kme_data")))
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        int len = br.ReadInt32();
                        while (len-- > 0)
                        {
                            string keyName = br.ReadString();
                            int keyCount = br.ReadInt32();
                            List<(string, Vector3, Vector3, Vector3)> values = new List<(string, Vector3, Vector3, Vector3)>();
                            while (keyCount-- > 0)
                                values.Add((br.ReadString(), br.ReadVector3(), br.ReadVector3(), br.ReadVector3()));
                            map_data.Add(keyName, values);
                        }
                    }
                    if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Maps", mapName + ".kme")))
                    {
                        Map map = new Map(mapName, map_data, false, false);
                        currentMap = map;
                        Console.WriteLine("Switched map to custom map " + mapName);
                        FileUploader.mapData = File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "Maps", mapName + ".kme"));
                    }
                    else if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Maps", mapName + ".kme_raw")))
                    {
                        Map map = new Map(mapName, map_data, false, true);
                        currentMap = map;
                        Console.WriteLine("Switched map to custom legacy map " + mapName);
                        FileUploader.mapData = File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "Maps", mapName + ".kme_raw"));
                    }
                    else
                    {
                        Console.WriteLine($"File '{mapName}.kme' or '{mapName}.kme_raw' doesn't exist!");
                    }
                }
            }
            GamemodeManager.SafeCall(GamemodeManager.currentGamemode!.OnMapChange);
        }

        public class Map
        {
            public string name;
            public Dictionary<string, List<(string, Vector3, Vector3, Vector3)>> map_data;
            public bool isDefault = false;
            public bool isLegacy = true;
            public Map(string name, Dictionary<string, List<(string, Vector3, Vector3, Vector3)>> map_data, bool isDefault, bool isLegacy)
            {
                this.name = name;
                this.map_data = map_data;
                this.isDefault = isDefault;
                this.isLegacy = isLegacy;
            }
        }
    }
}
