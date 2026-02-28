using Riptide;
using Riptide.Transports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ServerKMP.GamemodeApi
{
    public static class MessageComponents
    {
        public class Optional_Vector2 : IMessageSerializable
        {
            private static readonly Optional_Vector2 _none = new Optional_Vector2 { value = false };
            public static Optional_Vector2 none => _none;
            private bool value = false;
            private Vector2 V;
            public Optional_Vector2(Vector2 v) { V = v; value = true; }
            public Optional_Vector2() { }
            public bool HasValue() => value;
            public Vector2 GetValue()
            {
                if (value)
                    return V;
                return Vector2.zero;
            }
            public void Serialize(Message message)
            {
                message.Add(value);
                if (value)
                    message.Add(V);
            }
            public void Deserialize(Message message)
            {
                value = message.GetBool();
                if (value)
                    V = message.GetVector2();
            }
            public static implicit operator Optional_Vector2(Vector2? value)
            {
                if (!value.HasValue)
                    return none;
                return new Optional_Vector2(value.Value);
            }
        }
        public class Optional_Vector3 : IMessageSerializable
        {
            private static readonly Optional_Vector3 _none = new Optional_Vector3 { value = false };
            public static Optional_Vector3 none => _none;
            private bool value = false;
            private Vector3 V;
            public Optional_Vector3(Vector3 v) { V = v; value = true; }
            public Optional_Vector3() { }
            public bool HasValue() => value;
            public Vector3 GetValue()
            {
                if (value)
                    return V;
                return Vector3.zero;
            }
            public void Serialize(Message message)
            {
                message.Add(value);
                if (value)
                    message.Add(V);
            }
            public void Deserialize(Message message)
            {
                value = message.GetBool();
                if (value)
                    V = message.GetVector3();
            }
            public static implicit operator Optional_Vector3(Vector3? value)
            {
                if (!value.HasValue)
                    return none;
                return new Optional_Vector3(value.Value);
            }
        }
    }

    public static class MessageServerToClient
    {
        public abstract class MessageBase_S2C
        {
            public Message? RiptideMessage;
            public MessageBase_S2C Send(ushort client)
            {
                NetworkManager.server!.Send(RiptideMessage, client);
                return this;
            }
            public MessageBase_S2C SendToAll()
            {
                // send only to those who passed handshake
                foreach (ushort id in NetworkManager.registeredOnGamemode)
                    NetworkManager.server!.Send(RiptideMessage, id);
                return this;
            }
            public MessageBase_S2C SendToAll(ushort client)
            {
                // send only to those who passed handshake
                foreach (ushort id in NetworkManager.registeredOnGamemode)
                {
                    if (id == client) continue;
                    NetworkManager.server!.Send(RiptideMessage, id);
                }
                return this;
            }
            public MessageBase_S2C SendToList(List<ushort> clients)
            {
                foreach (ushort id in clients)
                    NetworkManager.server!.Send(RiptideMessage, id);
                return this;
            }
        }

        public class MessageInitialPlayerList : MessageBase_S2C
        {
            private List<(ushort, string)> pushList;
            private bool compiled;

            /// <summary>
            /// Construct a packet with the players in the list
            /// </summary>
            /// <param name="players">list of players with (id, username)</param>
            public MessageInitialPlayerList(List<(ushort, string)> players)
            {
                pushList = players;
                Compile();
            }
            /// <summary>
            /// Construct a dynamic packet
            /// Add players with .AddPlayer(id, username)
            /// Before sending, use .Compile()
            /// </summary>
            public MessageInitialPlayerList()
            {
                pushList = new List<(ushort, string)>();
                compiled = false;
            }
            public MessageInitialPlayerList AddPlayer(ushort id, string name)
            {
                pushList.Add((id, name));
                return this;
            }
            public MessageInitialPlayerList Compile()
            {
                if (compiled) return this;
                compiled = true;
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.initialPlayerList);
                RiptideMessage.Add(pushList.Count);
                foreach (var p in pushList)
                    RiptideMessage.Add(p.Item1).Add(p.Item2);
                return this;
            }
        }
        public class MessagePlayerJoinLeave : MessageBase_S2C
        {
            /// <summary>
            /// Player join
            /// </summary>
            /// <param name="id">Player ID</param>
            /// <param name="username">Player Username</param>
            public MessagePlayerJoinLeave(ushort id, string username)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.addPlayer);
                RiptideMessage.Add(true).Add(id).Add(username);
            }
            /// <summary>
            /// Player leave
            /// </summary>
            /// <param name="id">Player ID</param>
            public MessagePlayerJoinLeave(ushort id)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.addPlayer);
                RiptideMessage.Add(false).Add(id);
            }
        }
        public class MessageTickData : MessageBase_S2C
        {
            List<(ushort, KObject)> players;
            public MessageTickData()
            {
                players = new List<(ushort, KObject)>();
            }
            public void AddPlayer(ushort id, KObject obj)
            {
                players.Add((id, obj));
            }
            public MessageTickData Compile(ulong Tick)
            {
                RiptideMessage = Message.Create(MessageSendMode.Unreliable, Packet_S2C.playerData);
                RiptideMessage.Add(Tick).Add((ushort)players.Count);
                foreach (var p in players)
                    RiptideMessage.Add(p.Item1).Add(p.Item2.pos).Add(p.Item2.rot);
                return this;
            }
        }
        public class MessageAnimationData : MessageBase_S2C
        {
            List<(ushort, KPlayer)> players;
            public MessageAnimationData()
            {
                players = new List<(ushort, KPlayer)>();
            }
            public void AddPlayer(ushort id, KPlayer player)
            {
                players.Add((id, player));
            }
            public MessageAnimationData Compile()
            {
                RiptideMessage = Message.Create(MessageSendMode.Unreliable, Packet_S2C.animationData);
                RiptideMessage.Add((ushort)players.Count);
                foreach (var p in players)
                    RiptideMessage.Add(p.Item1).Add(p.Item2.crouching).Add(p.Item2.moving).Add(p.Item2.grounded);
                return this;
            }
        }
        public class MessageSendBullet : MessageBase_S2C
        {
            public MessageSendBullet(Vector3 from, Vector3 to, Vector3 bulletColor, bool hitEffect = true)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.bullet);
                RiptideMessage.Add(from).Add(to).Add(bulletColor).Add(hitEffect);
            }
        }
        public class MessageKillFeed : MessageBase_S2C
        {
            public MessageKillFeed(string message)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.killFeed);
                RiptideMessage.Add(message);
            }
        }
        public class MessageTeleport : MessageBase_S2C
        {
            public MessageTeleport(MessageComponents.Optional_Vector3 position, MessageComponents.Optional_Vector2 rotation, MessageComponents.Optional_Vector3 velocity)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.teleport);
                RiptideMessage.Add(position).Add(rotation).Add(velocity);
            }
        }
        public class MessageMapChange : MessageBase_S2C
        {
            /// <summary>
            /// Tell client to change map
            /// </summary>
            /// <param name="vanillaMap">true if map is vanilla (1Sandbox0)</param>
            /// <param name="sceneName">scene name</param>
            /// <param name="legacy">true if map is .kme_raw (legacy format)</param>
            public MessageMapChange(bool vanillaMap, string sceneName, bool legacy)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.map);
                RiptideMessage.Add(vanillaMap).Add(sceneName).Add(legacy);
            }
        }
        public class MessageSetHP : MessageBase_S2C
        {
            public MessageSetHP(int hp)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.hp);
                RiptideMessage.Add(hp);
            }
        }
        public class MessageConfirmKill : MessageBase_S2C
        {
            public MessageConfirmKill(ushort victim)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.kill);
                RiptideMessage.Add(victim);
            }
        }
        public class MessageDied : MessageBase_S2C
        {
            public MessageDied(ushort killer)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.death);
                RiptideMessage.Add(killer);
            }
        }
        public class MessageRespawn : MessageBase_S2C
        {
            public MessageRespawn(Vector3 position)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.respawn);
                RiptideMessage.Add(position);
            }
        }
        public class MessageChatMessage : MessageBase_S2C
        {
            public MessageChatMessage(string message)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.chat);
                RiptideMessage.Add(message);
            }
        }
        public class MessageUpdateScoreboard : MessageBase_S2C
        {
            private List<(ushort, string, int, int, int)> pushList;
            private bool compiled;

            public MessageUpdateScoreboard(List<(ushort, string, int, int, int)> entries)
            {
                pushList = entries;
                compiled = false;
            }
            public MessageUpdateScoreboard()
            {
                pushList = new List<(ushort, string, int, int, int)>();
                compiled = false;
            }
            public MessageUpdateScoreboard AddEntry(ushort id, string name, int kills, int deaths, int score)
            {
                pushList.Add((id, name, kills, deaths, score));
                return this;
            }
            public MessageUpdateScoreboard Compile()
            {
                if (compiled) return this;
                compiled = true;
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.scoreboard);
                RiptideMessage.Add(pushList.Count);
                foreach (var p in pushList)
                    RiptideMessage.Add(p.Item1).Add(p.Item2).Add(p.Item3).Add(p.Item4).Add(p.Item5);
                return this;
            }
        }
        public class MessageColorPlayer : MessageBase_S2C
        {
            public MessageColorPlayer(ushort who, string color)
            {
                if (color != "yellow" && color != "red" && color != "blue")
                    color = "yellow";
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.colorPlayer);
                RiptideMessage.Add(who).Add(color);
            }
        }
        public class MessageSpectate : MessageBase_S2C
        {
            /// <summary>
            /// Start spectating
            /// </summary>
            /// <param name="target">Player ID of target we want to spectate</param>
            public MessageSpectate(ushort target)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.spectate);
                RiptideMessage.Add(true).Add(target);
            }
            /// <summary>
            /// Stop spectating
            /// </summary>
            public MessageSpectate()
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.spectate);
                RiptideMessage.Add(false);
            }
        }
        public class MessageHUDMessage : MessageBase_S2C
        {
            public enum ScreenPos
            {
                TopCenter = 0,
                AboveCrosshair,
                Subtitle,
                BottomLeft
            };

            public MessageHUDMessage(ScreenPos position, string text)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.hudMessage);
                RiptideMessage.Add((int)position).Add(text);
            }
        }
        public class MessageSelfBulletColor : MessageBase_S2C
        {
            public MessageSelfBulletColor(Vector3 color)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.selfBulletColor);
                RiptideMessage.Add(color);
            }
        }
        public class MessageShowNametags : MessageBase_S2C
        {
            public MessageShowNametags(bool show)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.showNametags);
                RiptideMessage.Add(show).Add(false);
            }
            public MessageShowNametags(bool show, List<ushort> players)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.showNametags);
                RiptideMessage.Add(show).Add(true).Add(players.Count);
                foreach (var u in players)
                    RiptideMessage.Add(u);
            }
        }
        public class MessageGiveTakeWeapon : MessageBase_S2C
        {
            /// <summary>
            /// Remove weapon from player
            /// </summary>
            /// <param name="slot">inventory slot id</param>
            public MessageGiveTakeWeapon(int slot)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.weapons);
                RiptideMessage.Add(true);
                RiptideMessage.Add(slot);
            }
            /// <summary>
            /// Give weapon to player
            /// </summary>
            /// <param name="model">model name</param>
            /// <param name="localScale">scale</param>
            /// <param name="meshRotation">mesh rotation</param>
            /// <param name="gunTip">gun tip</param>
            /// <param name="viewOffset">view offset</param>
            /// <param name="soundName">shooting sound</param>
            /// <param name="recoil">recoil</param>
            /// <param name="attackSpeed">attack speed</param>
            /// <param name="magazine">max bullets in mag</param>
            /// <param name="bulletCount">bullets per shot</param>
            /// <param name="spread">bullet spread</param>
            /// <param name="cooldown">shooting cooldown</param>
            /// <param name="boostRecoil">shotgun recoil</param>
            /// <param name="maxDamage">[DMG] Maximum damage</param>
            /// <param name="damageDropoff">[DMG] Distance after which damage is 0</param>
            /// <param name="damageScaleByDist">[DMG] Scailing factor for damage by distance</param>
            public MessageGiveTakeWeapon(string model, Vector3 localScale, Vector3 meshRotation, Vector3 gunTip, Vector3 viewOffset, string soundName, float recoil, float attackSpeed, int magazine, int bulletCount, float spread, float cooldown, float boostRecoil, float maxDamage, float damageDropoff, float damageScaleByDist)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.weapons);
                RiptideMessage.Add(false)
                    .Add(model).Add(localScale).Add(meshRotation).Add(gunTip).Add(viewOffset).Add(soundName).Add(recoil).Add(attackSpeed).Add(magazine).Add(bulletCount).Add(spread).Add(cooldown).Add(boostRecoil).Add(maxDamage).Add(damageDropoff).Add(damageScaleByDist);
            }
        }
        public class MessageCollisions : MessageBase_S2C
        {
            public MessageCollisions(bool disable_collisions)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.collisions);
                RiptideMessage.Add(disable_collisions);
            }
        }
        public class MessageCreateDestroyProp : MessageBase_S2C
        {
            /// <summary>
            /// Destroy prop
            /// </summary>
            /// <param name="id">prop id</param>
            public MessageCreateDestroyProp(int id)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.levelprop);
                RiptideMessage.Add(true);
                RiptideMessage.Add(id);
            }

            /// <summary>
            /// Create Prop
            /// </summary>
            /// <param name="id">prop id</param>
            /// <param name="pos">world pos</param>
            /// <param name="rot">rotation</param>
            /// <param name="scale">scale</param>
            /// <param name="prefabId">prefab id</param>
            /// <param name="annouce_pickup">announce to server when player picks it up</param>
            public MessageCreateDestroyProp(int id, Vector3 pos, Vector3 rot, Vector3 scale, int prefabId, bool annouce_pickup)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.levelprop);
                RiptideMessage.Add(false)
                    .Add(id).Add(pos).Add(rot).Add(scale).Add(prefabId).Add(annouce_pickup);
            }
        }

        public class MessageLinkPropToPlayer : MessageBase_S2C
        {
            /// <summary>
            /// Link prop to player (aka attach it to them)
            /// </summary>
            /// <param name="id">prop id</param>
            /// <param name="player">player id</param>
            /// <param name="posOffset">pos offset from player</param>
            /// <param name="rotOffset">rot offset from player</param>
            public MessageLinkPropToPlayer(int id, ushort player, Vector3 posOffset, Vector3 rotOffset)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.linkprop);
                RiptideMessage.Add(id).Add(player).Add(posOffset).Add(rotOffset);
            }
        }

        public class MessagePassword : MessageBase_S2C
        {
            /// <summary>
            /// Used internally by KMP. To ask for password use <see cref="NetManager.RequestPassword(ushort, string)"/>
            /// </summary>
            /// <param name="prompt"></param>
            /// <param name="cspBlob"></param>
            public MessagePassword(string prompt, byte[] cspBlob)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.password);
                RiptideMessage.Add(prompt).Add(cspBlob);
            }
        }

        public class MessageFilePart : MessageBase_S2C
        {
            /// <summary>
            /// Used internally by KMP. Future implementations for gamemodes will come.
            /// </summary>
            /// <param name="data">Data to send to the user</param>
            public MessageFilePart(byte[] data)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.file_data);
                RiptideMessage.Add(data);
            }
        }

        public class MessageFileRequest : MessageBase_S2C
        {
            /// <summary>
            /// Used internally by KMP. Sends a request to the client to upload a file.
            /// </summary>
            /// <param name="fileName">Name of the file to upload</param>
            /// <param name="fileSize">Total size (in bytes) of the file</param>
            /// <param name="hash">Hash of the file to check for integrity</param>
            public MessageFileRequest(string fileName, uint fileSize, byte[] hash)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.file_dl);
                RiptideMessage.Add(fileName).Add(fileSize).Add(hash);
            }
        }

        public class MessageSync : MessageBase_S2C
        {
            /// <summary>
            /// Used internally by KMP. Syncs server tick to clients
            /// </summary>
            /// <param name="tick">Current tick</param>
            public MessageSync(ulong tick)
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.sync);
                RiptideMessage.Add(tick);
            }
        }

        public class MessageGamerules : MessageBase_S2C
        {
            public static class Rules
            {
                /// <summary>
                /// Enable or disable Crouch Fixes (mainly double crouch exploit)
                /// </summary>
                public const string CrouchFixes = "CrouchFixes";
                /// <summary>
                /// Set distance from which nametags should be visible
                /// </summary>
                public const string NametagDistance = "NametagDistance";
                /// <summary>
                /// Enable damage logging on the client
                /// </summary>
                public const string ShowDamage = "ShowDamage";
            }

            List<(string, string)> rules;
            public MessageGamerules()
            {
                rules = new List<(string, string)>();
            }

            public MessageGamerules AddRule(string key, string value)
            {
                rules.Add((key, value));
                return this;
            }

            public MessageGamerules Compile()
            {
                RiptideMessage = Message.Create(MessageSendMode.Reliable, Packet_S2C.gamerules);
                RiptideMessage.Add(rules.Count);
                foreach (var rule in rules)
                    RiptideMessage.Add(rule.Item1).Add(rule.Item2);
                return this;
            }
        }
    }

    public static class MessageClientToServer
    {
        public abstract class MessageBase_C2S
        {
            public ushort fromId;
            public ushort RiptideId;
            public MessageBase_C2S(ushort fromId, ushort riptideId)
            {
                this.fromId = fromId;
                RiptideId = riptideId;
            }
        }

        public class MessageHandshake : MessageBase_C2S
        {
            public string username;
            public MessageHandshake(ushort fromId, string username) : base(fromId, Packet_C2S.handshake)
            {
                this.username = username;
            }
        }
        public class MessagePositionData : MessageBase_C2S
        {
            public Vector3 position;
            public Vector2 rotation;
            public bool crouching, moving, grounded;
            public MessagePositionData(ushort fromId, Message riptideMessage) : base(fromId, Packet_C2S.position)
            {
                position = riptideMessage.GetVector3();
                rotation = riptideMessage.GetVector2();
                crouching = riptideMessage.GetBool();
                moving = riptideMessage.GetBool();
                grounded = riptideMessage.GetBool();
            }
        }
        public class MessageRequestScene : MessageBase_C2S
        {
            public MessageRequestScene(ushort fromId, Message riptideMessage) : base(fromId, Packet_C2S.requestScene) { }
        }
        public class MessageShoot : MessageBase_C2S
        {
            public Vector3 origin;
            public Vector3 hitPoint;

            public MessageShoot(ushort fromId, Message riptideMessage) : base(fromId, Packet_C2S.shoot)
            {
                origin = riptideMessage.GetVector3();
                hitPoint = riptideMessage.GetVector3();
            }
        }
        public class MessageDamage : MessageBase_C2S
        {
            public ushort victim;
            public int damage;

            public MessageDamage(ushort fromId, Message riptideMessage) : base(fromId, Packet_C2S.damage)
            {
                victim = riptideMessage.GetUShort();
                damage = riptideMessage.GetInt();
            }
        }
        public class MessageChat : MessageBase_C2S
        {
            public string message;

            public MessageChat(ushort fromId, Message riptideMessage) : base(fromId, Packet_C2S.chat)
            {
                message = riptideMessage.GetString();
            }
        }
        public class MessagePickup : MessageBase_C2S
        {
            public int propid;
            public MessagePickup(ushort fromId, Message riptideMessage) : base(fromId, Packet_C2S.pickup)
            {
                propid = riptideMessage.GetInt();
            }
        }
        public class MessagePassword : MessageBase_C2S
        {
            public string password;
            public MessagePassword(ushort fromId, string pw) : base(fromId, Packet_C2S.password)
            {
                password = pw;
            }
        }
    }

    public static class NetManager
    {
        public static void KickClient(ushort client, string reason)
        {
            NetworkManager.server!.DisconnectClient(client, Message.Create().Add(reason));
        }

        public static void RequestPassword(ushort client, string prompt = "")
        {
            var rsa = new RSACryptoServiceProvider(1024)
            {
                PersistKeyInCsp = false
            };
            if (NetworkManager.passwordEncryption.ContainsKey(client))
                NetworkManager.passwordEncryption.Remove(client);
            NetworkManager.passwordEncryption.Add(client, rsa);
            new MessageServerToClient.MessagePassword(prompt, rsa.ExportCspBlob(false)).Send(client);
        }
    }
}
