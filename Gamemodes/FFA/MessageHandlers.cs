using ServerKMP;
using ServerKMP.GamemodeApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FFA
{
    public static class MessageHandlers
    {
        public static void Handshake(MessageClientToServer.MessageBase_C2S _base)
            => Handshake((MessageClientToServer.MessageHandshake)_base);
        public static void Handshake(MessageClientToServer.MessageHandshake handshake)
        {
            // check for valid username
            if(!Regex.IsMatch(handshake.username, "^[a-zA-Z0-9_\\.]+$"))
            {
                NetManager.KickClient(handshake.fromId, "Invalid username. Please use only alphanumerical and '_', '.'.");
                return;
            }
            if(handshake.username.Length < 3 || handshake.username.Length > 32)
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
            // we don't do anything here lol
        }

        public static void RequestScene(MessageClientToServer.MessageBase_C2S _base)
        {
            // send initial player list (also filter by id, so client is not included in list)
            new MessageServerToClient.MessageInitialPlayerList(GamemodeEntry.players.Where(x => x.Key != _base.fromId).Select(x => (x.Key, NetworkManager.usernameDatabase[x.Key])).ToList()).Send(_base.fromId);

            GamemodeEntry.players[_base.fromId].RespawnPlayer();
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
            if (GamemodeEntry.players[damage.victim].invicibleUntil > DateTime.Now) return; // victim is invincible
            if (damage.damage < 0) return; // damage is negative. they are cheating
            // clamp hp to 0 (if damage is greater than health)
            GamemodeEntry.players[damage.victim].SetHP(Math.Max(0, GamemodeEntry.players[damage.victim].hp - damage.damage));
            if(GamemodeEntry.players[damage.victim].hp == 0)
            { // victim died
                if(damage.fromId != damage.victim)
                { // if not suicide
                    GamemodeEntry.players[damage.fromId].kills++;
                    GamemodeEntry.players[damage.fromId].score++;
                }
                GamemodeEntry.players[damage.victim].deaths++;
                GamemodeEntry.players[damage.victim].score--;
                GamemodeEntry.UpdateScoreboard();
                string kfMessage = $"{GamemodeEntry.players[damage.fromId].username} killed {GamemodeEntry.players[damage.victim].username}";
                if (damage.fromId == damage.victim)
                    kfMessage = $"{GamemodeEntry.players[damage.fromId].username} commited suicide";
                new MessageServerToClient.MessageKillFeed(kfMessage).SendToAll();
                new MessageServerToClient.MessageConfirmKill(damage.victim).Send(damage.fromId);
                new MessageServerToClient.MessageDied(damage.fromId).Send(damage.victim);
                ushort targetSpec = damage.fromId;
                if (GamemodeEntry.players[damage.fromId].spectating != 0)
                    // race condition, ping things
                    // the player that killed us is dead, so we spactate in-place
                    targetSpec = damage.victim;
                GamemodeEntry.players[damage.victim].EnterSpectate(targetSpec);
                GamemodeEntry.players[damage.victim].respawnTaskId = KMP_TaskScheduler.Schedule(() =>
                {
                    GamemodeEntry.players[damage.victim].ExitSpectate();
                    GamemodeEntry.players[damage.victim].RespawnPlayer();
                    GamemodeEntry.players[damage.victim].respawnTaskActive = false;
                }, DateTime.Now.AddSeconds(5));
                GamemodeEntry.players[damage.victim].respawnTaskActive = true;

                // check if there are any other people that were previously spectating our victim
                foreach(var x in GamemodeEntry.players)
                    if(x.Value.spectating == damage.victim)
                        x.Value.EnterSpectate(targetSpec == damage.victim ? x.Key : damage.fromId); // enter target spec (same story)
                // here we don't need to worry about setting timer, because the respawnTask is still active, hence we're spectating
            }
        }

        public static void Chat(MessageClientToServer.MessageBase_C2S _base)
            => Chat((MessageClientToServer.MessageChat)_base);
        public static void Chat(MessageClientToServer.MessageChat chat)
        {
            // here you can add commands, like this:
            //if(chat.message.StartsWith("!")) ...
            // you will need to write your own command processor, keep that in mind
            if(chat.message.StartsWith("!"))
            {
                var args = chat.message.Split(' ');
                if (args[0] == "!rs")
                {
                    GamemodeEntry.players[chat.fromId].kills = GamemodeEntry.players[chat.fromId].deaths = GamemodeEntry.players[chat.fromId].score = 0;
                    new MessageServerToClient.MessageChatMessage($"<color=yellow>* {GamemodeEntry.players[chat.fromId].username} reset their score</color>").SendToAll();
                    GamemodeEntry.UpdateScoreboard();
                }
                if (args[0] == "!admin")
                {
                    if(GamemodeEntry.players[chat.fromId].admin)
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
            new MessageServerToClient.MessageChatMessage($"{GamemodeEntry.players[chat.fromId].username} : {msg}").SendToAll();
        }

        public static void Pickup(MessageClientToServer.MessageBase_C2S _base)
            => Pickup((MessageClientToServer.MessagePickup)_base);
        public static void Pickup(MessageClientToServer.MessagePickup pickup)
        {
            new MessageServerToClient.MessageChatMessage($"you picked up {pickup.propid}").Send(pickup.fromId);
            new MessageServerToClient.MessageCreateDestroyProp(pickup.propid).SendToAll();
        }

        public static void Password(MessageClientToServer.MessageBase_C2S _base)
            => Password((MessageClientToServer.MessagePassword)_base);
        public static void Password(MessageClientToServer.MessagePassword password)
        {
            if(password.password != File.ReadAllText("adminpass"))
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
