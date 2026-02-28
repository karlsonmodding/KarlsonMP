using Riptide.Utils;
using Riptide;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using ServerKMP.GamemodeApi;
using System.Net;
using System.Security.Cryptography;

namespace ServerKMP
{
    public static class NetworkManager
    {
        public const ulong ProtocolVersion = 5;

        public static Server? server;
        public static int CurrentTick { get; private set; } = 0;
        public static HashSet<ushort> clientsAwaitingHandshake = new HashSet<ushort>(); // players that need to send handshake
        public static HashSet<ushort> registeredOnGamemode = new HashSet<ushort>(); // players that were sent to gamemode to be processed via C2S handshake
        public static Dictionary<ushort, string> usernameDatabase = new Dictionary<ushort, string>();
        public static Dictionary<ushort, RSACryptoServiceProvider> passwordEncryption = new Dictionary<ushort, RSACryptoServiceProvider>();
        public static Dictionary<ushort, bool> broadcastPosition = new Dictionary<ushort, bool>();
        public static string MOTD = "<MOTD>";

        public static void Start()
        {
            Console.WriteLine($"Starting server (port {Config.PORT}, {Config.MAX_PLAYERS} max clients)");
            RiptideLogger.Initialize(Console.WriteLine, false);
            server = new Server(new Riptide.Transports.Udp.UdpServer(Riptide.Transports.Udp.SocketMode.IPv4Only), "Riptide");
            //server = new Server("Riptide");
            server.Start(Config.PORT, Config.MAX_PLAYERS);
            server.ClientConnected += Server_ClientConnected;
            server.ClientDisconnected += Server_ClientDisconnected;
        }

        private static void Server_ClientConnected(object? sender, ServerConnectedEventArgs e)
        {
            Message handshake = Message.Create(MessageSendMode.Reliable, Packet_S2C.handshake);
            handshake.Add(MOTD).Add((ushort)registeredOnGamemode.Count).Add(server!.MaxClientCount).Add(ProtocolVersion);
            server!.Send(handshake, e.Client);
            clientsAwaitingHandshake.Add(e.Client.Id);
            KMP_TaskScheduler.Schedule(() =>
            {
                if (!e.Client.IsConnected) return; // client alr disconnected
                if (clientsAwaitingHandshake.Contains(e.Client.Id))
                    server.DisconnectClient(e.Client, Message.Create().Add("Didn't respond to handshake."));
            }, DateTime.Now.AddSeconds(30));
        }

        private static void Server_ClientDisconnected(object? sender, ServerDisconnectedEventArgs e)
        {
            if (clientsAwaitingHandshake.Contains(e.Client.Id))
                clientsAwaitingHandshake.Remove(e.Client.Id);
            if (TickManager.netObjects.ContainsKey(e.Client.Id))
                TickManager.netObjects.Remove(e.Client.Id);
            if (registeredOnGamemode.Contains(e.Client.Id))
            {
                registeredOnGamemode.Remove(e.Client.Id);
                GamemodeManager.SafeCall(() => GamemodeManager.currentGamemode!.OnPlayerDisconnect(e.Client.Id));
            }
        }

        public static void Update()
        {
            server!.Update();
        }

        public static void Exit()
        {
            server!.Stop();
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

    public static class ServerHandle
    {
        [MessageHandler(Packet_C2S.handshake)]
        public static void Handshake(ushort from, Message message)
        {
            string username = message.GetString();
            try
            {
                ulong protocolVersion = message.GetULong();
                if (protocolVersion != NetworkManager.ProtocolVersion)
                {
                    if (protocolVersion < NetworkManager.ProtocolVersion)
                        NetManager.KickClient(from, "Your client is too old. Update KMP!");
                    else
                        NetManager.KickClient(from, "Server is too old! You need to wait for it to be updated.");
                    Console.WriteLine($"Rejected ({from}) {username} because protocol doesn't match.");
                    return;
                }
                Console.WriteLine($"Client {from} logged in as {username}");
                NetworkManager.clientsAwaitingHandshake.Remove(from);
                NetworkManager.registeredOnGamemode.Add(from);
                NetworkManager.usernameDatabase[from] = username;
                NetworkManager.broadcastPosition[from] = false;
                new MessageServerToClient.MessageSync(TickManager.CurrentTick).Send(from);
                TickManager.netObjects.Add(from, new KPlayer());
                GamemodeManager.SafeCall(() => GamemodeManager.currentGamemode!.ProcessMessage(new MessageClientToServer.MessageHandshake(from, username)));
            }
            catch
            {
                NetManager.KickClient(from, "Your client is too old. Update KMP!");
            }
        }
        [MessageHandler(Packet_C2S.position)]
        public static void PositionData(ushort from, Message message)
        {
            if (!TickManager.netObjects.TryGetValue(from, out KPlayer? value))
            {
                NetManager.KickClient(from, "Bad packet");
                return;
            }
            var msg = new MessageClientToServer.MessagePositionData(from, message);
            value.pos = msg.position;
            value.rot = msg.rotation;
            value.crouching = msg.crouching;
            value.grounded = msg.grounded;
            value.moving = msg.moving;
            GamemodeManager.SafeCall(() => GamemodeManager.currentGamemode!.ProcessMessage(msg));
        }
        [MessageHandler(Packet_C2S.requestScene)]
        public static void RequestScene(ushort from, Message message)
            => GamemodeManager.SafeCall(() => GamemodeManager.currentGamemode!.ProcessMessage(new MessageClientToServer.MessageRequestScene(from, message)));
        [MessageHandler(Packet_C2S.shoot)]
        public static void Shoot(ushort from, Message message)
            => GamemodeManager.SafeCall(() => GamemodeManager.currentGamemode!.ProcessMessage(new MessageClientToServer.MessageShoot(from, message)));
        [MessageHandler(Packet_C2S.damage)]
        public static void Damage(ushort from, Message message)
            => GamemodeManager.SafeCall(() => GamemodeManager.currentGamemode!.ProcessMessage(new MessageClientToServer.MessageDamage(from, message)));
        [MessageHandler(Packet_C2S.chat)]
        public static void Chat(ushort from, Message message)
            => GamemodeManager.SafeCall(() => GamemodeManager.currentGamemode!.ProcessMessage(new MessageClientToServer.MessageChat(from, message)));
        [MessageHandler(Packet_C2S.pickup)]
        public static void Pickup(ushort from, Message message)
            => GamemodeManager.SafeCall(() => GamemodeManager.currentGamemode!.ProcessMessage(new MessageClientToServer.MessagePickup(from, message)));
        [MessageHandler(Packet_C2S.password)]
        public static void Password(ushort from, Message message)
        {
            if (!NetworkManager.passwordEncryption.TryGetValue(from, out RSACryptoServiceProvider? value))
            {
                NetManager.KickClient(from, "Bad packet");
                return;
            }
            byte[] pw_enc = message.GetBytes();
            if (pw_enc.Length == 0)
            {
                value.Dispose();
                NetworkManager.passwordEncryption.Remove(from);
                GamemodeManager.SafeCall(() => GamemodeManager.currentGamemode!.ProcessMessage(new MessageClientToServer.MessagePassword(from, "")));
                return;
            }
            // decrypt password
            var rsa = NetworkManager.passwordEncryption[from];
            var password = Encoding.UTF8.GetString(rsa.Decrypt(pw_enc, false));
            rsa.Dispose();
            NetworkManager.passwordEncryption.Remove(from);
            GamemodeManager.SafeCall(() => GamemodeManager.currentGamemode!.ProcessMessage(new MessageClientToServer.MessagePassword(from, password)));
        }
        [MessageHandler(Packet_C2S.file_data)]
        public static void FileData(ushort from, Message message)
        {
            string fileName = message.GetString();
            // rn we only allow downloading of the map
            if (fileName != MapManager.currentMap!.name)
            {
                new MessageServerToClient.MessageFilePart([]).Send(from);
                return;
            }
            new MessageServerToClient.MessageFilePart(FileUploader.GetMapPart(message.GetUShort())).Send(from);
        }
    }
}
