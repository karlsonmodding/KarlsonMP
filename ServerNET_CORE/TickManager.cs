using ServerKMP.GamemodeApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ServerKMP
{
    public class KObject
    {
        public Vector3 pos;
        public Vector2 rot;

        public KObject()
        {
            pos = Vector3.zero;
            rot = Vector2.zero;
        }
    }

    public class KPlayer : KObject
    {
        public bool crouching, moving, grounded;
        public KPlayer() : base()
        {
            crouching = false;
            moving = false;
            grounded = false;
        }

        public static KPlayer dontBroadcast => _dontBroadcast;
        private static readonly KPlayer _dontBroadcast = new KPlayer { pos = new Vector3(666666, 666666, 666666) };
    }

    public static class TickManager
    {
        public static Dictionary<ushort, KPlayer> netObjects = new Dictionary<ushort, KPlayer>();
        public static ulong CurrentTick = 0;

        public static void NextTick()
        {
            if (CurrentTick % 200 == 0)
                new MessageServerToClient.MessageSync(CurrentTick).SendToAll();
            CurrentTick++;
            // broadcast tick
            var tickMessage = new MessageServerToClient.MessageTickData();
            foreach (var player in NetworkManager.registeredOnGamemode)
                if (NetworkManager.broadcastPosition[player])
                    tickMessage.AddPlayer(player, netObjects[player]);
                else
                    tickMessage.AddPlayer(player, KPlayer.dontBroadcast);
            tickMessage.Compile(CurrentTick).SendToAll();
            // send animations
            var animations = new MessageServerToClient.MessageAnimationData();
            foreach (var player in NetworkManager.registeredOnGamemode)
                if (NetworkManager.broadcastPosition[player])
                    animations.AddPlayer(player, netObjects[player]);
                else
                    animations.AddPlayer(player, KPlayer.dontBroadcast);
            animations.Compile().SendToAll();
        }
    }
}
