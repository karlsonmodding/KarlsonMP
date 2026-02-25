using ServerKMP;
using ServerKMP.GamemodeApi;

namespace Race
{
    public class GamemodeEntry : Gamemode
    {
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


        void DrawZones()
        {
            DrawBox(RaceData.startZone, new Vector3(0, 1, 0));
            //foreach (var z in RaceData.teleports)
            //    DrawBox(z.Item1, new Vector3(1, 1, 0));
            KMP_TaskScheduler.Schedule(DrawZones, DateTime.Now.AddMilliseconds(250));
        }

        public override void OnStart()
        {
            KMP_TaskScheduler.scheduledTasks.Clear();
            DrawZones();
            if (!MapManager.currentMap!.isDefault)
                ProcessMapData();
            NetworkManager.MOTD = Config.MOTD + " / Race | Map " + MapManager.currentMap!.name;
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
            // check for all players that were spectating him
            foreach (var p in GamemodeEntry.players)
            {
                if (p.Value.spectating == id)
                {
                    p.Value.spectating = 0;
                    p.Value.RespawnPlayer();
                }
            }
            new MessageServerToClient.MessageKillFeed($"<color=red>({id}) {players[id].username} disconnected</color>").SendToAll();
            players[id].Destroy();
            players.Remove(id);
            // send player leave message
            new MessageServerToClient.MessagePlayerJoinLeave(id).SendToAll();

            // update scoreboard
            GamemodeEntry.UpdateScoreboard();
        }

        public class Zone
        {
            public Vector3 from, to;
            public bool Inside(Vector3 v)
            {
                if (v.x < Math.Min(from.x, to.x) || v.y < Math.Min(from.y, to.y) || v.z < Math.Min(from.z, to.z))
                    return false;
                if (v.x > Math.Max(from.x, to.x) || v.y > Math.Max(from.y, to.y) || v.z > Math.Max(from.z, to.z))
                    return false;
                return true;
            }
        }

        public static class RaceData
        {
            public static Zone startZone = new Zone();
            public static Vector3 milk, milk_size;
            public static List<(Zone, Vector3)> teleports = new List<(Zone, Vector3)>();
        }
        void ProcessMapData()
        {
            // process map zones
            // start zone
            var sz = MapManager.currentMap!.map_data["zones"].Where(x => x.Item1 == "start").First();
            RaceData.startZone.from = sz.Item2 - sz.Item4 / 2;
            RaceData.startZone.to = sz.Item2 + sz.Item4 / 2;
            // milk
            var milk = MapManager.currentMap.map_data["zones"].Where(x => x.Item1 == "milk").First();
            RaceData.milk = milk.Item2;
            RaceData.milk_size = milk.Item4;
            // tp zones
            RaceData.teleports.Clear();
            if (MapManager.currentMap.map_data.ContainsKey("tp"))
                foreach (var tpzone in MapManager.currentMap.map_data["tp"])
                {
                    RaceData.teleports.Add((new Zone
                    {
                        from = tpzone.Item2 - tpzone.Item4 / 2,
                        to = tpzone.Item2 + tpzone.Item4 / 2,
                    }, Vector3.Parse(tpzone.Item1)));
                }
        }
        public override void OnMapChange()
        {
            if (MapManager.currentMap!.isDefault) // default map, just send scene name
                new MessageServerToClient.MessageMapChange(true, MapManager.currentMap.name, true).SendToAll();
            else
            {
                // here we need to use the pre-implemented http server
                new MessageServerToClient.MessageMapChange(false, MapManager.currentMap.name, MapManager.currentMap.isLegacy).SendToAll();
                FileUploader.SendMapUploadRequest();
                ProcessMapData();
            }
            NetworkManager.MOTD = Config.MOTD + " / Race | Map " + MapManager.currentMap.name;
        }

        public static void UpdateScoreboard()
        {
            new MessageServerToClient.MessageUpdateScoreboard(players.Select(x => (x.Key, x.Value.username + x.Value.GetPbTime() + (x.Value.spectating == 0 ? "" : " <color=#00ffff>[spectating " + players[x.Value.spectating].username + "]</color>"), int.MinValue, int.MinValue, x.Value.score)).ToList()).AddEntry(ushort.MaxValue, "<color=#00FF00>" + Config.MOTD + $" / Race</color> <color=#777777>●</color> Map <color=yellow>{MapManager.currentMap!.name}</color>", int.MinValue, int.MinValue, int.MinValue).Compile().SendToAll();
        }

        static void DrawBox(Zone zone, Vector3 color)
        {
            new MessageServerToClient.MessageSendBullet(new Vector3(zone.from.x, zone.from.y, zone.from.z), new Vector3(zone.to.x, zone.from.y, zone.from.z), color, false).SendToAll();
            new MessageServerToClient.MessageSendBullet(new Vector3(zone.from.x, zone.from.y, zone.from.z), new Vector3(zone.from.x, zone.to.y, zone.from.z), color, false).SendToAll();
            new MessageServerToClient.MessageSendBullet(new Vector3(zone.to.x, zone.from.y, zone.from.z), new Vector3(zone.to.x, zone.to.y, zone.from.z), color, false).SendToAll();
            new MessageServerToClient.MessageSendBullet(new Vector3(zone.from.x, zone.to.y, zone.from.z), new Vector3(zone.to.x, zone.to.y, zone.from.z), color, false).SendToAll();

            new MessageServerToClient.MessageSendBullet(new Vector3(zone.from.x, zone.from.y, zone.to.z), new Vector3(zone.to.x, zone.from.y, zone.to.z), color, false).SendToAll();
            new MessageServerToClient.MessageSendBullet(new Vector3(zone.from.x, zone.from.y, zone.to.z), new Vector3(zone.from.x, zone.to.y, zone.to.z), color, false).SendToAll();
            new MessageServerToClient.MessageSendBullet(new Vector3(zone.to.x, zone.from.y, zone.to.z), new Vector3(zone.to.x, zone.to.y, zone.to.z), color, false).SendToAll();
            new MessageServerToClient.MessageSendBullet(new Vector3(zone.from.x, zone.to.y, zone.to.z), new Vector3(zone.to.x, zone.to.y, zone.to.z), color, false).SendToAll();

            new MessageServerToClient.MessageSendBullet(new Vector3(zone.from.x, zone.from.y, zone.from.z), new Vector3(zone.from.x, zone.from.y, zone.to.z), color, false).SendToAll();
            new MessageServerToClient.MessageSendBullet(new Vector3(zone.to.x, zone.from.y, zone.from.z), new Vector3(zone.to.x, zone.from.y, zone.to.z), color, false).SendToAll();
            new MessageServerToClient.MessageSendBullet(new Vector3(zone.from.x, zone.to.y, zone.from.z), new Vector3(zone.from.x, zone.to.y, zone.to.z), color, false).SendToAll();
            new MessageServerToClient.MessageSendBullet(new Vector3(zone.to.x, zone.to.y, zone.from.z), new Vector3(zone.to.x, zone.to.y, zone.to.z), color, false).SendToAll();
        }

        public override void ServerTick()
        {
            foreach (var player in players)
            {
                if (player.Value.spectating != 0) continue;
                // if in start zone, update player's last zone time
                if (RaceData.startZone.Inside(TickManager.netObjects[player.Key].pos))
                {
                    player.Value.lastTimeInZone = DateTime.Now;
                    new MessageServerToClient.MessageHUDMessage(MessageServerToClient.MessageHUDMessage.ScreenPos.TopCenter, "<size=25><color=gray>In starting zone</color></size>").Send(player.Key);
                    if (!player.Value.in_zone)
                    {
                        player.Value.in_zone = true;
                    }
                }
                else
                {
                    if (player.Value.in_zone)
                    {
                        player.Value.in_zone = false;
                    }
                    int ms = (int)(DateTime.Now - player.Value.lastTimeInZone).TotalMilliseconds;
                    new MessageServerToClient.MessageHUDMessage(MessageServerToClient.MessageHUDMessage.ScreenPos.TopCenter, $"<size=35>{ms / 60000:D2}:{ms / 1000 % 60:D2}</size><size=22px>.{ms % 1000:D3}</size>").Send(player.Key);
                }
                // check for tp zones
                foreach (var z in RaceData.teleports)
                {
                    if (z.Item1.Inside(TickManager.netObjects[player.Key].pos))
                        new MessageServerToClient.MessageTeleport(z.Item2, MessageComponents.Optional_Vector2.none, Vector3.zero).Send(player.Key);
                }
            }
        }
    }
}
