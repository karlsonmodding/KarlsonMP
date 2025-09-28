using ServerKMP;
using ServerKMP.GamemodeApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Race
{
    public static class MessageHandlers
    {
        public static void Handshake(MessageClientToServer.MessageBase_C2S _base)
            => Handshake((MessageClientToServer.MessageHandshake)_base);
        public static void Handshake(MessageClientToServer.MessageHandshake handshake)
        {
            // check for valid username
            if (!Regex.IsMatch(handshake.username, "^[a-zA-Z0-9_\\.]+$"))
            {
                NetManager.KickClient(handshake.fromId, "Invalid username. Please use only alphanumerical and '_', '.'.");
                return;
            }
            if (handshake.username.Length < 3 || handshake.username.Length > 32)
            {
                NetManager.KickClient(handshake.fromId, "Invalid username. Username can only be 3-32 characters long.");
                return;
            }
            // check for username collision
            if (GamemodeEntry.players.Any(x => NetworkManager.usernameDatabase[x.Key].ToLower() == handshake.username.ToLower()))
            {
                NetManager.KickClient(handshake.fromId, "Someone else is already using that username.");
                return;
            }
            GamemodeEntry.players.Add(handshake.fromId, new Player(handshake.fromId, handshake.username));

            // send playerjoin to all except client
            new MessageServerToClient.MessagePlayerJoinLeave(handshake.fromId, handshake.username).SendToAll(handshake.fromId);
            new MessageServerToClient.MessageKillFeed($"<color=green>({handshake.fromId}) {handshake.username} connected</color>").SendToAll(handshake.fromId);

            // send gamerules
            new MessageServerToClient.MessageGamerules()
                .AddRule(MessageServerToClient.MessageGamerules.Rules.NametagDistance, "150")
                //.AddRule(MessageServerToClient.MessageGamerules.Rules.CrouchFixes, "1") - it's enabled by default
                .Compile().Send(handshake.fromId);

            // send player current map
            if (MapManager.currentMap!.isDefault) // default map, just send scene name
                new MessageServerToClient.MessageMapChange(true, MapManager.currentMap.name).Send(handshake.fromId);
            else
            {
                new MessageServerToClient.MessageMapChange(false, MapManager.currentMap.name).Send(handshake.fromId);
                FileUploader.SendMapUploadRequest(handshake.fromId);
            }

            // update scoreboard
            GamemodeEntry.UpdateScoreboard();
        }

        public static void PositionData(MessageClientToServer.MessageBase_C2S _base)
            => PositionData((MessageClientToServer.MessagePositionData)_base);
        public static void PositionData(MessageClientToServer.MessagePositionData positionData)
        {
            // don't do anything here lol
        }

        public static void RequestScene(MessageClientToServer.MessageBase_C2S _base)
        {
            // send initial player list (also filter by id, so client is not included in list)
            new MessageServerToClient.MessageInitialPlayerList(GamemodeEntry.players.Where(x => x.Key != _base.fromId).Select(x => (x.Key, NetworkManager.usernameDatabase[x.Key])).ToList()).Send(_base.fromId);

            GamemodeEntry.players[_base.fromId].RespawnPlayer();
            // send milk
            new MessageServerToClient.MessageCreateDestroyProp(1, GamemodeEntry.RaceData.milk, Vector3.zero, GamemodeEntry.RaceData.milk_size, 0, true).Send(_base.fromId);
            // disable collisions
            new MessageServerToClient.MessageCollisions(true).Send(_base.fromId);
        }

        public static void Shoot(MessageClientToServer.MessageBase_C2S _base)
            => Shoot((MessageClientToServer.MessageShoot)_base);
        public static void Shoot(MessageClientToServer.MessageShoot shoot)
        {
            // broadcast bullet to all players.
            // 1f, 0f, 0f is red color
            new MessageServerToClient.MessageSendBullet(shoot.origin, shoot.hitPoint, new Vector3(1f, 0f, 0f)).SendToAll(shoot.fromId);
        }

        public static void Damage(MessageClientToServer.MessageBase_C2S _base)
            => Damage((MessageClientToServer.MessageDamage)_base);
        public static void Damage(MessageClientToServer.MessageDamage damage)
        {
            if (damage.victim != damage.fromId) return; // only process suicides
            GamemodeEntry.players[damage.fromId].RespawnPlayer();
        }

        public static void Chat(MessageClientToServer.MessageBase_C2S _base)
            => Chat((MessageClientToServer.MessageChat)_base);
        public static void Chat(MessageClientToServer.MessageChat chat)
        {
            // here you can add commands, like this:
            //if(chat.message.StartsWith("!")) ...
            // you will need to write your own command processor, keep that in mind
            if (chat.message.StartsWith("!"))
            {
                var args = chat.message.Split(' ');
                if (args[0] == "!r")
                {
                    if (GamemodeEntry.players[chat.fromId].spectating != 0)
                    {
                        GamemodeEntry.players[chat.fromId].spectating = 0;
                        GamemodeEntry.UpdateScoreboard();
                    }
                    GamemodeEntry.players[chat.fromId].RespawnPlayer();
                }
                if (args[0] == "!w")
                {
                    GamemodeEntry.players[chat.fromId].weapons = !GamemodeEntry.players[chat.fromId].weapons;
                    if(GamemodeEntry.players[chat.fromId].weapons)
                    {
                        GamemodeEntry.players[chat.fromId].GiveWeapons();
                        new MessageServerToClient.MessageChatMessage("Weapons: <color=green>enabled</color>.").Send(chat.fromId);
                    }
                    else
                    {
                        GamemodeEntry.players[chat.fromId].TakeWeapons();
                        new MessageServerToClient.MessageChatMessage("Weapons: <color=red>disabled</color>.").Send(chat.fromId);
                    }
                }
                if (args[0] == "!hp")
                {
                    GamemodeEntry.players[chat.fromId].show_hp = !GamemodeEntry.players[chat.fromId].show_hp;
                    if (GamemodeEntry.players[chat.fromId].show_hp)
                    {
                        new MessageServerToClient.MessageSetHP(100).Send(chat.fromId);
                        new MessageServerToClient.MessageChatMessage("HP bar: <color=green>enabled</color>.").Send(chat.fromId);
                    }
                    else
                    {
                        new MessageServerToClient.MessageSetHP(0).Send(chat.fromId);
                        new MessageServerToClient.MessageChatMessage("HP bar: <color=red>disabled</color>.").Send(chat.fromId);
                    }
                }
                if (args[0] == "!s")
                {
                    GamemodeEntry.players[chat.fromId].sounds = !GamemodeEntry.players[chat.fromId].sounds;
                    if (GamemodeEntry.players[chat.fromId].sounds)
                    {
                        new MessageServerToClient.MessageChatMessage("Sounds: <color=green>enabled</color>.").Send(chat.fromId);
                    }
                    else
                    {
                        new MessageServerToClient.MessageChatMessage("Sounds: <color=red>disabled</color>.").Send(chat.fromId);
                    }
                }
                if (args[0] == "!spec")
                {
                    if(args.Length != 2)
                    {
                        new MessageServerToClient.MessageChatMessage("!spec <id>").Send(chat.fromId);
                        return;
                    }
                    ushort target;
                    if(!ushort.TryParse(args[1], out target) || target == chat.fromId)
                    {
                        new MessageServerToClient.MessageChatMessage("Invalid ID!").Send(chat.fromId);
                        return;
                    }
                    if (GamemodeEntry.players[target].spectating != 0)
                    {
                        new MessageServerToClient.MessageChatMessage($"{GamemodeEntry.players[target].username} is spectating {GamemodeEntry.players[GamemodeEntry.players[target].spectating].username} (ID {GamemodeEntry.players[target].spectating})").Send(chat.fromId);
                        target = GamemodeEntry.players[target].spectating;
                        return;
                    }
                    GamemodeEntry.players[chat.fromId].EnterSpectate(target);
                    // for all players that were spectating me, enter them spec on my new target
                    foreach (var p in GamemodeEntry.players)
                    {
                        if(p.Value.spectating == chat.fromId)
                        {
                            new MessageServerToClient.MessageChatMessage($"{GamemodeEntry.players[chat.fromId].username} is now spectating {GamemodeEntry.players[target].username} (ID {target})").Send(p.Key);
                            p.Value.EnterSpectate(target);
                        }
                    }
                    GamemodeEntry.UpdateScoreboard();
                }
                if (args[0] == "!admin")
                {
                    if (GamemodeEntry.players[chat.fromId].admin)
                    {
                        new MessageServerToClient.MessageChatMessage("You are already an admin!").Send(chat.fromId);
                        return;
                    }
                    NetManager.RequestPassword(chat.fromId, "Enter the admin password");
                }
                if (!GamemodeEntry.players[chat.fromId].admin) return; // not an admin, so no access to this commands
                if (args[0] == "!map")
                {
                    if (args.Length == 1)
                        new MessageServerToClient.MessageChatMessage("Usage: !map <map_name>").Send(chat.fromId);
                    else
                        MapManager.LoadMap(args[1]);
                }
                if (args[0] == "!gamemode") // maybe giving a bit too much power to admin here? unsure
                {
                    if (args.Length == 1)
                        new MessageServerToClient.MessageChatMessage("Usage: !gamemode <gamemode>").Send(chat.fromId);
                    else
                        GamemodeManager.LoadGamemode(args[1]);
                }
                if (args[0] == "!kick")
                {
                    if (args.Length != 2)
                        new MessageServerToClient.MessageChatMessage("Usage: !kick <id>").Send(chat.fromId);
                    else
                        NetManager.KickClient(ushort.Parse(args[1]), "Kicked by admin " + GamemodeEntry.players[chat.fromId].username);
                }
                if (args[0] == "!o")
                {
                    if (chat.message.Length < 3)
                        new MessageServerToClient.MessageChatMessage("Usage: !o <message>").Send(chat.fromId);
                    else
                        new MessageServerToClient.MessageChatMessage(GamemodeEntry.players[chat.fromId].username + " has an announcment:\n<size=25><color=red>(!)</color> " + chat.message.Substring(3) + "</size>").SendToAll();
                }
                return;
            }

            string msg = chat.message.Replace("<", "<<i></i>"); // sanitize against unwanted richtext
            Console.WriteLine($"[CHAT] {GamemodeEntry.players[chat.fromId].username} : {msg}");
            new MessageServerToClient.MessageChatMessage($"<color=#aaaaaa>{GamemodeEntry.players[chat.fromId].username}</color> {msg}").SendToAll();
        }

        public static string FormatTime(int ms)
        {
            return $"{ms / 60000:D2}:{ms / 1000 % 60:D2}.{ms % 1000:D3}";
        }

        public static void Pickup(MessageClientToServer.MessageBase_C2S _base)
            => Pickup((MessageClientToServer.MessagePickup)_base);
        public static void Pickup(MessageClientToServer.MessagePickup pickup)
        {
            if (GamemodeEntry.players[pickup.fromId].spectating != 0)
                return; // we are spectating
            int time = (int)(DateTime.Now - GamemodeEntry.players[pickup.fromId].lastTimeInZone).TotalMilliseconds;
            new MessageServerToClient.MessageChatMessage($"<color=yellow><b>»</b> <b>{GamemodeEntry.players[pickup.fromId].username}</b> finished the level in {FormatTime(time)}.</color>").SendToAll();
            // send soundfx to player
            if (GamemodeEntry.players[pickup.fromId].sounds)
                new MessageServerToClient.MessageConfirmKill(0).Send(pickup.fromId);
            GamemodeEntry.players[pickup.fromId].RespawnPlayer();
            GamemodeEntry.players[pickup.fromId].score++;
            if (time < GamemodeEntry.players[pickup.fromId].pb || GamemodeEntry.players[pickup.fromId].pb == 0)
            {
                GamemodeEntry.players[pickup.fromId].pb = time;
                new MessageServerToClient.MessageChatMessage($"<color=magenta><b>»</b> You set a new PB!</color>").Send(pickup.fromId);
            }
            GamemodeEntry.UpdateScoreboard();
        }

        public static void Password(MessageClientToServer.MessageBase_C2S _base)
            => Password((MessageClientToServer.MessagePassword)_base);
        public static void Password(MessageClientToServer.MessagePassword password)
        {
            if (password.password != File.ReadAllText("adminpass"))
            {
                NetManager.KickClient(password.fromId, "Invalid admin password.");
            }
            else
            {
                GamemodeEntry.players[password.fromId].EnableAdmin();
                GamemodeEntry.UpdateScoreboard();
            }
        }
    }
}
