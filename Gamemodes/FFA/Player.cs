using ServerKMP;
using ServerKMP.GamemodeApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFA
{
    public class Player
    {
        public ushort id;
        public string username;
        public int hp;
        public int kills, deaths, score;
        public DateTime invicibleUntil;
        public int spectating = 0;
        public uint respawnTaskId;
        public bool respawnTaskActive;
        public bool admin;

        public Player(ushort _id, string _username)
        {
            id = _id;
            username = _username;
            hp = 100;
            kills = 0;
            deaths = 0;
            score = 0;
            invicibleUntil = DateTime.Now;
            spectating = 0;
            respawnTaskActive = false;
            admin = false;
        }

        public void SetUsername(string _username)
        {
            username = _username;
        }

        public void SetHP(int _hp)
        {
            hp = _hp;
            new MessageServerToClient.MessageSetHP(hp).Send(id);
        }

        public void RespawnPlayer()
        {
            // reset hp
            SetHP(100);
            // pick random spawn location
            int count = MapManager.currentMap!.map_data["spawn"].Count;
            var pos = MapManager.currentMap.map_data["spawn"][new Random().Next(count)];
            invicibleUntil = DateTime.Now.AddSeconds(3);
            new MessageServerToClient.MessageRespawn(pos.Item2).Send(id);
            // send weapons
            new MessageServerToClient.MessageGiveTakeWeapon("ak47", new Vector3(50f, 50f, 2.5f), new Vector3(0f, 180f, 0f), new Vector3(-0.015f, -0f, 0f), Vector3.zero, "smg", 0.2f, 0.15f, 20, 1, 0.01f, 0.2f, 0, 40f, 0, 0.1f).Send(id);
            new MessageServerToClient.MessageGiveTakeWeapon("deagle", new Vector3(1.44f, 1.44f, 0.527f), new Vector3(-90f, 0f, 0f), new Vector3(-0.3f, -0.3f, 0f), new Vector3(0f, 0.2f, 0.2f), "pistol", 0.3f, 0.4f, 1, 1, 0, 0.7f, 0, 100f, 0, 0).Send(id);
            new MessageServerToClient.MessageGiveTakeWeapon("shotty", new Vector3(40f, 50f, 2.5f), new Vector3(0f, 180f, 0f), Vector3.zero, new Vector3(0f, 0.2f, 0.2f), "shotgun", 0.5f, 1, 8, 6, 0.075f, 0.5f, 7f, 40f, 50f, 1f).Send(id);

            NetworkManager.broadcastPosition[id] = true;
        }

        public void EnterSpectate(ushort target)
        {
            new MessageServerToClient.MessageSpectate(target).Send(id);
            spectating = target;

            // send fake position
            NetworkManager.broadcastPosition[id] = false;
        }
        public void ExitSpectate()
        {
            new MessageServerToClient.MessageSpectate().Send(id);
            spectating = 0;
            NetworkManager.broadcastPosition[id] = true;
        }

        public void EnableAdmin()
        {
            admin = true;
            username = "<color=#ee4444>(A)</color> <color=#ff9494>" + username + "</color>";
            new MessageServerToClient.MessageChatMessage($"{username} is now an admin").SendToAll();
        }

        public void Destroy()
        {
            if (respawnTaskActive)
                KMP_TaskScheduler.CancelTask(respawnTaskId);
            username = "";
        }
    }
}
