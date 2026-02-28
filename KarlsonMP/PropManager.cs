using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMP
{
    public class PropManager
    {
        public static void SpawnProp(int id, Vector3 pos, Vector3 rot, Vector3 scale, int prefabId, bool annouce_pickup)
        {
            GameObject go = PrefabIdToGameObject(prefabId);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(rot);
            go.transform.localScale = scale;
            Prop.CreateProp(id, go, annouce_pickup);
        }

        public static void LinkPropToPlayer(int id, ushort playerId, Vector3 pos, Vector3 rot)
        {
            activeProps[id].playerid = playerId;
            activeProps[id].posOff = pos;
            activeProps[id].rotOff = rot;
        }

        public static void DestroyProp(int id)
        {
            UnityEngine.Object.Destroy(activeProps[id].go);
            activeProps.Remove(id);
        }

        public static void ClearProps()
        {
            foreach (var x in activeProps)
            {
                if (x.Value.go)
                    UnityEngine.Object.Destroy(x.Value.go);
            }
            activeProps.Clear();
        }

        public static void _onupdate()
        {
            foreach (var prop in activeProps)
            {
                if (prop.Value.playerid == 0) continue; // prop not linked to player
                var target = PlaytimeLogic.players.FirstOrDefault(x => x.id == prop.Value.playerid)?.player ?? null;
                if (prop.Value.playerid == NetworkManager.client.Id) target = PlayerMovement.Instance.gameObject;
                if (target == null) continue; // null player
                prop.Value.go.transform.position = target.transform.position + prop.Value.posOff;
                prop.Value.go.transform.rotation = Quaternion.Euler(target.transform.rotation.eulerAngles + prop.Value.rotOff);
            }
        }

        private static GameObject PrefabIdToGameObject(int id)
        {
            if (id == 0) return KMP_PrefabManager.NewMilk();
            if (id == 1)
            {
                var barrel = KMP_PrefabManager.NewBarrel();
                UnityEngine.Object.Destroy(barrel.transform.GetChild(0).GetComponent<Barrel>());
                UnityEngine.Object.Destroy(barrel.transform.GetChild(0).GetComponent<Object>());
                UnityEngine.Object.Destroy(barrel.transform.GetChild(0).GetComponent<Rigidbody>());
                return barrel;
            }
            return new GameObject();
        }

        private static Dictionary<int, Prop> activeProps = new Dictionary<int, Prop>();
        public class Prop
        {
            public int id;
            public GameObject go;
            public ushort playerid;
            public Vector3 posOff;
            public Vector3 rotOff;

            private Prop(int id, GameObject go, bool annouce)
            {
                this.id = id;
                this.go = go;
                playerid = 0;
                var prop_data = go.AddComponent<KMP_PropData>();
                prop_data.id = id;
                prop_data.annouce = annouce;
            }

            public static void CreateProp(int id, GameObject go, bool annouce = false)
            {
                if (activeProps.ContainsKey(id))
                    activeProps[id] = new Prop(id, go, annouce);
                else
                    activeProps.Add(id, new Prop(id, go, annouce));
            }
        }
        public class KMP_PropData : MonoBehaviour
        {
            public int id;
            public bool annouce;
        }
    }
}
