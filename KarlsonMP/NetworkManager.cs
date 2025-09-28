using Riptide;
using Riptide.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KarlsonMP
{
    public class NetworkManager
    {
        public static Client client;
        public static string address { get; private set; }
        public static string username { get; private set; }
        public static void Connect(string _addr, string _username)
        {
            address = _addr;
            if (!address.Contains(':'))
                address += ":11337";
            username = _username;
            KillFeedGUI.AddText($"Connecting to {address}");
            RiptideLogger.Initialize(KMP_Console.Log, false);
            client = new Client("Riptide"); // logger name
            client.Connect(address);
            client.ConnectionFailed += FailedToConnect;
            client.Disconnected += DidDisconnect;
        }

        public static void Quit()
        {
            if (client == null) return;
            client.Disconnected -= DidDisconnect;
            if(!client.IsNotConnected)
                client.Disconnect();
        }

        private static void FailedToConnect(object sender, ConnectionFailedEventArgs e)
        {
            MonoHooks.ShowDialog("KarlsonMP reborn", "Failed to connect to the server", "Go to browser", "Exit game", () => { PlaytimeLogic.DisconnectToBrowser(); }, () => { Application.Quit(); });
        }

        private static void DidDisconnect(object sender, DisconnectedEventArgs e)
        {
            if(e.Message == null)
                MonoHooks.ShowDialog("KarlsonMP reborn", "Server closed the connection", "Go to browser", "Exit game", () => { PlaytimeLogic.DisconnectToBrowser(); }, () => { Application.Quit(); });
            else
                MonoHooks.ShowDialog("KarlsonMP reborn", "Server closed the connection:\n\n" + e.Message.GetString(), "Go to browser", "Exit game", () => { PlaytimeLogic.DisconnectToBrowser(); }, () => { Application.Quit(); });
        }
    }

    public static class Packet_S2C
    {
        public const ushort handshake = 1; // handshake, send MOTD
        public const ushort initialPlayerList = 2; // initial player list
        public const ushort addPlayer = 3; // player join/left
        public const ushort playerData = 4; // server snapshot of the world
        public const ushort bullet = 5; // bullet ray
        public const ushort killFeed = 6;
        public const ushort teleport = 7;
        public const ushort map = 8;
        public const ushort hp = 9;
        public const ushort kill = 10; // we killed someone
        public const ushort death = 11; // we died
        public const ushort respawn = 12; // server respawned us (teleport should come before/after)
        public const ushort chat = 13;
        public const ushort scoreboard = 14;
        public const ushort colorPlayer = 15; // color: 'yellow', 'red', 'blue'
        public const ushort spectate = 16; // spectate player / exit spectate
        public const ushort hudMessage = 17;
        public const ushort selfBulletColor = 18; // change own bullet color
        public const ushort showNametags = 19;
        public const ushort weapons = 20; // give/remove weapon
        public const ushort collisions = 21;
        public const ushort levelprop = 22; // create/destroy prop
        public const ushort linkprop = 23; // link prop to player
        public const ushort password = 24; // for requesting a password from player
        public const ushort file_dl = 25; // send file for download to the client (includes length and checksum)
        public const ushort file_data = 26; // sending file part in response to client request
        public const ushort sync = 27; // sync current tick to client
        public const ushort animationData = 28; // send animation data to clients (doesn't need to be interpolated)
        public const ushort gamerules = 29; // set of constants that may be expanded later
    }
    public static class Packet_C2S
    {
        public const ushort handshake = 1; // handshake, send discord bearer
        public const ushort position = 2; // position
        public const ushort requestScene = 3; // request sync and initialPlayerList
        public const ushort shoot = 4; // client shoot
        public const ushort damage = 5; // damage report after shoot
        public const ushort chat = 6;
        public const ushort pickup = 7; // announce prop pickup
        public const ushort password = 8; // for sending a password to the server
        public const ushort file_data = 9; // request file part for download
    }

    public class ClientSend
    {
        public static void Handshake(string username)
        {
            Message message = Message.Create(MessageSendMode.Reliable, Packet_C2S.handshake);
            message.Add(username);
            NetworkManager.client.Send(message);
        }

        public static void PositionData()
        {
            if (!ClientHandle.PlayerList) return;
            Message message = Message.Create(MessageSendMode.Unreliable, Packet_C2S.position);
            message
                .Add(PlayerMovement.Instance.transform.position)
                .Add(new Vector2(Camera.main.transform.rotation.eulerAngles.x, Camera.main.transform.rotation.eulerAngles.y))
                .Add(CrouchFixes.IsCrouching())
                .Add(PlayerMovement.Instance.rb.velocity.sqrMagnitude > 1f)
                .Add(PlayerMovement.Instance.grounded);
            NetworkManager.client.Send(message);
        }

        public static void RequestScene()
        {
            NetworkManager.client.Send(Message.Create(MessageSendMode.Reliable, Packet_C2S.requestScene));
            // run autoexec
            KMP_Console.Autoexec();
        }

        public static void Shoot(Vector3 from, Vector3 to)
        {
            Message message = Message.Create(MessageSendMode.Reliable, Packet_C2S.shoot);
            message
                .Add(from)
                .Add(to);
            NetworkManager.client.Send(message);
        }

        public static void Damage(ushort victim, int damage)
        {
            Message message = Message.Create(MessageSendMode.Reliable, Packet_C2S.damage);
            message
                .Add(victim)
                .Add(damage);
            NetworkManager.client.Send(message);
        }

        public static void ChatMessage(string msg)
        {
            Message message = Message.Create(MessageSendMode.Reliable, Packet_C2S.chat);
            message.Add(msg);
            NetworkManager.client.Send(message);
        }

        public static void Pickup(int propid)
        {
            Message message = Message.Create(MessageSendMode.Reliable, Packet_C2S.pickup);
            message.Add(propid);
            NetworkManager.client.Send(message);
        }

        public static void Password(byte[] pw)
        {
            Message message = Message.Create(MessageSendMode.Reliable, Packet_C2S.password);
            message.Add(pw);
            NetworkManager.client.Send(message);
        }

        public static void FileData(string filename, ushort part)
        {
            Message message = Message.Create(MessageSendMode.Reliable, Packet_C2S.file_data);
            message.Add(filename).Add(part);
            NetworkManager.client.Send(message);
        }
    }

    public class ClientHandle
    {
        public static bool PlayerList { get; private set; } = false;
        public static void ResetPlayerList() => PlayerList = false;

        [MessageHandler(Packet_S2C.handshake)]
        public static void Handshake(Message message)
        {
            if (NetworkManager.client == null || !NetworkManager.client.IsConnected)
                return; // we shouldn't handle this packet
            string motd = message.GetString();
            KillFeedGUI.AddText("Connected to\n" + motd);
            ClientSend.Handshake(NetworkManager.username);
        }

        [MessageHandler(Packet_S2C.initialPlayerList)]
        public static void InitialPlayerList(Message message)
        {
            int _length = message.GetInt();
            KillFeedGUI.AddText("There are " + _length + " other players online.");
            while (_length-- > 0)
            {
                ushort _id = message.GetUShort();
                string _username = message.GetString();
                if (PlaytimeLogic.players.Find(x => x.id == _id) != null) continue; // player already exists
                PlaytimeLogic.players.Add(new Player(_id, _username));
            }
            PlayerList = true;
        }

        [MessageHandler(Packet_S2C.addPlayer)]
        public static void AddPlayer(Message message)
        {
            bool join = message.GetBool();
            ushort id = message.GetUShort();
            if(!join)
            {
                Player _p = PlaytimeLogic.players.Find(p => p.id == id);
                if (_p == null) return; // player doesn't exist
                _p.Destroy();
                PlaytimeLogic.players.Remove(_p);
                return;
            }
            string username = message.GetString();

            if (PlaytimeLogic.players.Find(x => x.id == id) != null) return; // player already exists
            PlaytimeLogic.players.Add(new Player(id, username));
        }

        [MessageHandler(Packet_S2C.playerData)]
        public static void PlayerData(Message message)
        {
            if (!PlayerList) return; // no player list yet
            ulong Tick = message.GetULong();
            ushort count = message.GetUShort();
            List<KObject> objects = new List<KObject>();
            while(count-- > 0)
                objects.Add(new KObject(message.GetUShort(), message.GetVector3(), message.GetVector2()));
            KTickManager.RegisterFrame(new KTick(Tick, objects));
        }

        [MessageHandler(Packet_S2C.animationData)]
        public static void AnimationData(Message message)
        {
            if (!PlayerList) return;
            ushort count = message.GetUShort();
            while(count-- > 0)
            {
                var pid = message.GetUShort();
                var p = PlaytimeLogic.players.Where(x => x.id == pid).FirstOrDefault();
                bool crouching = message.GetBool(), moving = message.GetBool(), grounded = message.GetBool();
                if (p != null)
                    p.UpdateAnimations(crouching, moving, grounded);
            }
        }

        [MessageHandler(Packet_S2C.sync)]
        public static void Sync(Message message)
        {
            KTickManager.SyncTick(message.GetULong());
        }

        [MessageHandler(Packet_S2C.bullet)]
        public static void Bullet(Message message)
        {
            Vector3 from = message.GetVector3();
            Vector3 to = message.GetVector3();
            var c = message.GetVector3();
            bool hitEffect = message.GetBool();
            Color color = new Color(c.x, c.y, c.z);
            BulletRenderer.DrawBullet(from, to, color, hitEffect);
        }

        [MessageHandler(Packet_S2C.killFeed)]
        public static void KillFeed(Message message)
        {
            string msg = message.GetString();
            KillFeedGUI.AddText(msg);
        }

        [MessageHandler(Packet_S2C.teleport)]
        public static void Teleport(Message message)
        {
            var pos = message.GetSerializable<MessageExtensions.oVector3>();
            var rot = message.GetSerializable<MessageExtensions.oVector2>();
            var vel = message.GetSerializable<MessageExtensions.oVector3>();
            if (pos.HasValue())
                PlayerMovement.Instance.transform.position = pos.GetValue();
            if(rot.HasValue())
            {
                Camera.main.transform.rotation = Quaternion.Euler(rot.GetValue().x, rot.GetValue().y, 0f);
                PlayerMovement.Instance.orientation.transform.rotation = Quaternion.Euler(0f, rot.GetValue().y, 0f);
            }
            if(vel.HasValue())
                PlayerMovement.Instance.rb.velocity = vel.GetValue();
        }

        public static string RequestedMap { get; private set; } = "";
        [MessageHandler(Packet_S2C.map)]
        public static void MapChange(Message message)
        {
            bool isDefault = message.GetBool();
            RequestedMap = message.GetString();
            KillFeedGUI.AddText($"Loading map {RequestedMap}");
            // start with the player dead
            PlayerMovement.Instance.ReflectionSet("dead", true);
            if (isDefault)
            {
                PlaytimeLogic.PrepareMapChange();
                SceneManager.LoadScene(RequestedMap);
                Game.Instance.StartGame();
            }
            // don't download map here, wait for server to send download request
        }

        [MessageHandler(Packet_S2C.hp)]
        public static void SetHP(Message message)
        {
            int hp = message.GetInt();
            if (hp < PlaytimeLogic.hp) KMP_AudioManager.PlaySound("hitself", 0.15f);
            PlaytimeLogic.hp = hp;
        }

        [MessageHandler(Packet_S2C.kill)]
        public static void ConfirmKill(Message message)
        {
            ushort victim = message.GetUShort();
            KMP_AudioManager.PlaySound("kill", 0.05f);
        }

        [MessageHandler(Packet_S2C.death)]
        public static void WeDied(Message message)
        {
            ushort killer = message.GetUShort();
            // if killer = 0, natural cause
            KMP_AudioManager.PlaySound("death", 0.05f);
            PlayerMovement.Instance.ReflectionSet("dead", true);
        }

        [MessageHandler(Packet_S2C.respawn)]
        public static void Respawn(Message message)
        {
            if (PlaytimeLogic.spectatingId != 0)
                PlaytimeLogic.ExitSpectate();
            PlayerMovement.Instance.ReflectionSet("dead", false);
            PlaytimeLogic.suicided = false;
            // reset guns ammo, because we got respawned
            Inventory.ReloadAll();
            Vector3 position = message.GetVector3();
            PlayerMovement.Instance.transform.position = position;
            PlayerMovement.Instance.rb.velocity = Vector3.zero;
            PlayerMovement.Instance.ReflectionInvoke("StopCrouch");
        }

        [MessageHandler(Packet_S2C.chat)]
        public static void Chat(Message message)
        {
            string msg = message.GetString();
            PlaytimeLogic.AddChat(msg);
        }

        [MessageHandler(Packet_S2C.scoreboard)]
        public static void ScoreboardUpdate(Message message)
        {
            Scoreboard.UpdateScoreboard(message);
        }

        [MessageHandler(Packet_S2C.colorPlayer)]
        public static void ColorPlayer(Message message)
        {
            ushort who = message.GetUShort();
            if (who == NetworkManager.client.Id) return;
            string color = message.GetString();
            Texture2D tex;
            switch(color)
            {
                case "yellow":
                    tex = KME_LevelPlayer.gameTex[5];
                    break;
                case "blue":
                    tex = KME_LevelPlayer.gameTex[7];
                    break;
                case "red":
                    tex = KME_LevelPlayer.gameTex[9];
                    break;
                default:
                    return;
            }
            Player p = (from x in PlaytimeLogic.players where x.id == who select x).First();
            p.player.transform.GetChild(1).GetComponent<SkinnedMeshRenderer>().material.mainTexture = tex;
        }

        [MessageHandler(Packet_S2C.spectate)]
        public static void Spectate(Message message)
        {
            bool enterSpec = message.GetBool();
            if (!enterSpec)
                PlaytimeLogic.ExitSpectate();
            else
                PlaytimeLogic.StartSpectate(message.GetUShort());
        }

        [MessageHandler(Packet_S2C.hudMessage)]
        public static void HudMessage(Message message)
        {
            int position = message.GetInt();
            string text = message.GetString();
            if (position < 0 || position > 3) return;
            switch (position)
            {
                case 0:
                    HUDMessages.topCenter = text;
                    break;
                case 1:
                    HUDMessages.aboveCrosshair = text;
                    break;
                case 2:
                    HUDMessages.subtitle = text;
                    break;
                case 3:
                    HUDMessages.bottomLeft = text;
                    break;
            }
        }

        [MessageHandler(Packet_S2C.selfBulletColor)]
        public static void SelfBulletColor(Message message)
        {
            Vector3 c = message.GetVector3();
            Inventory.selfBulletColor = new Color(c.x, c.y, c.z);
        }

        [MessageHandler(Packet_S2C.showNametags)]
        public static void ShowNametags(Message message)
        {
            bool toggle = message.GetBool();
            if(message.UnreadBits > 0)
            {
                // player list toggle
                while(message.UnreadBits > 0)
                {
                    ushort pid = message.GetUShort();
                    var p = (from x in PlaytimeLogic.players where x.id == pid select x).FirstOrDefault();
                    if (p == null) continue;
                    p.nametagShown = toggle;
                }
            }
            else
            {
                PlaytimeLogic.showNametags = toggle;
                // global toggle
                foreach (var p in PlaytimeLogic.players)
                    p.nametagShown = PlaytimeLogic.showNametags;
            }
        }

        [MessageHandler(Packet_S2C.weapons)]
        public static void GiveWeapon(Message message)
        {
            bool remove = message.GetBool();
            if(remove)
            {
                Inventory.RemoveWeapon(message.GetInt());
                return;
            }
            Inventory.GiveWeapon(message.GetString(), message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetString(), message.GetFloat(), message.GetFloat(), message.GetInt(), message.GetInt(), message.GetFloat(), message.GetFloat(), message.GetFloat(), message.GetFloat(), message.GetFloat(), message.GetFloat());
        }

        [MessageHandler(Packet_S2C.collisions)]
        public static void Collisions(Message message)
        {
            bool ignore_coll = message.GetBool();
            Physics.IgnoreLayerCollision(8, 8, ignore_coll);
            Physics.IgnoreLayerCollision(8, 12, ignore_coll);
        }

        [MessageHandler(Packet_S2C.levelprop)]
        public static void LevelProp(Message message)
        {
            bool destroy = message.GetBool();
            if (!destroy)
                PropManager.SpawnProp(message.GetInt(), message.GetVector3(), message.GetVector3(), message.GetVector3(), message.GetInt(), message.GetBool());
            else
                PropManager.DestroyProp(message.GetInt());
        }

        [MessageHandler(Packet_S2C.linkprop)]
        public static void LinkProp(Message message)
        {
            PropManager.LinkPropToPlayer(message.GetInt(), message.GetUShort(), message.GetVector3(), message.GetVector3());
        }

        [MessageHandler(Packet_S2C.password)]
        public static void Password(Message message)
        {
            PlaytimeLogic.PasswordDialog.Prompt(message.GetString(), message.GetBytes());
        }

        [MessageHandler(Packet_S2C.file_dl)]
        public static void FileRequest(Message message)
        {
            FileHandler.HandleFileRequest(message.GetString(), message.GetUInt(), message.GetBytes());
        }

        [MessageHandler(Packet_S2C.file_data)]
        public static void FilePart(Message message)
        {
            FileHandler.HandleFilePart(message.GetBytes());
        }

        [MessageHandler(Packet_S2C.gamerules)]
        public static void Gamerules(Message message)
        {
            int count = message.GetInt();
            while(count-- > 0)
            {
                string key = message.GetString();
                string value = message.GetString();
                switch(key)
                {
                    case "CrouchFixes":
                        CrouchFixes.Enabled = value == "1";
                        break;
                    case "NametagDistance":
                        PlaytimeLogic.nametagDistance = float.Parse(value);
                        break;
                    case "ShowDamage":
                        Inventory.gamerule_ShowDamage = value == "1";
                        break;
                }
            }
        }
    }
}
