using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServerKMP.GamemodeApi;
using ServerKMP;
using System.IO.Ports;
using System.Drawing;

namespace FFA
{
    public class GamemodeEntry : Gamemode
    {
        public static Random rnd = new Random();
        private static Dictionary<ushort, Action<MessageClientToServer.MessageBase_C2S>> messageHandlers = new Dictionary<ushort, Action<MessageClientToServer.MessageBase_C2S>>
            {
                { Packet_C2S.handshake, MessageHandlers.Handshake },
                { Packet_C2S.position, MessageHandlers.PositionData },
                { Packet_C2S.requestScene, MessageHandlers.RequestScene },
                { Packet_C2S.shoot, MessageHandlers.Shoot },
                { Packet_C2S.damage, MessageHandlers.Damage },
                { Packet_C2S.chat, MessageHandlers.Chat },
                { Packet_C2S.pickup, MessageHandlers.Pickup },
                { Packet_C2S.password, MessageHandlers.Password },
            };
        public static Dictionary<ushort, Player> players = new Dictionary<ushort, Player>();

        public override void OnStart()
        {
            KMP_TaskScheduler.scheduledTasks.Clear();
            NetworkManager.MOTD = Config.MOTD + " / FFA | Map " + MapManager.currentMap!.name;
        }
        public override void OnStop()
        {
            players.Clear();
        }

        public override void ProcessMessage(MessageClientToServer.MessageBase_C2S message)
        {
            if (!messageHandlers.ContainsKey(message.RiptideId))
            {
                Console.WriteLine("[WARNING] Received known packet, but not registered in messageHandlers dictionary.");
                Console.WriteLine("[WARNING] Packet ID: " + message.RiptideId + " . Sent by client: " + message.fromId);
            }
            else
            {
                if (message.RiptideId == Packet_C2S.handshake || players.ContainsKey(message.fromId))
                    messageHandlers[message.RiptideId](message);
            }
        }

        public override void OnPlayerDisconnect(ushort id)
        {
            new MessageServerToClient.MessageKillFeed($"<color=red>({id}) {players[id].username} disconnected</color>").SendToAll();
            players[id].Destroy();
            players.Remove(id);
            // send player leave message
            new MessageServerToClient.MessagePlayerJoinLeave(id).SendToAll();

            // update scoreboard
            GamemodeEntry.UpdateScoreboard();
        }

        public override void OnMapChange()
        {
            if (MapManager.currentMap!.isDefault) // default map, just send scene name
                new MessageServerToClient.MessageMapChange(true, MapManager.currentMap.name, true).SendToAll();
            else
            { // here we need to use the file uploader.
                new MessageServerToClient.MessageMapChange(false, MapManager.currentMap.name, MapManager.currentMap.isLegacy).SendToAll();
                FileUploader.SendMapUploadRequest();
            }

            NetworkManager.MOTD = Config.MOTD + " / FFA | Map " + MapManager.currentMap!.name;
        }

        public static void UpdateScoreboard()
        {
            new MessageServerToClient.MessageUpdateScoreboard(GamemodeEntry.players.Select(x => (x.Key, x.Value.username, x.Value.kills, x.Value.deaths, x.Value.score)).ToList()).AddEntry(ushort.MaxValue, "<color=#00FF00>" + Config.MOTD + $" / FFA</color> <color=#777777>●</color> Map <color=yellow>{MapManager.currentMap!.name}</color>", int.MinValue, int.MinValue, int.MinValue).Compile().SendToAll();
        }
    }
}
