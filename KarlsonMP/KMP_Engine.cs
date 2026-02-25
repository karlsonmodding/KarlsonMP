using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMP
{
    public class KObject
    {
        public readonly ushort entityId;
        public readonly Vector3 position;
        public readonly Vector2 rotation;

        public KObject(ushort _eid, Vector3 _position, Vector2 _rotation)
        {
            entityId = _eid;
            position = _position;
            rotation = _rotation;
            // we want x rotation to be in [-90,90] range
            if (rotation.x > 180)
                rotation.x -= 360;
        }

        private static float ClockModular(float a, float mod = 360)
        {
            // clock modular means after mod we loop around to 0, just like a clock loops from 12 to 0
            if (a >= mod)
                a -= mod;
            return a;
        }

        public static KObject Interpolate(KObject objPrev, KObject objNow, float tickTime)
        {
            if(objPrev == null || objNow == null) return null;
            // interpolate between objPrev and objNow, where tickTime is 0..1 and represents the between-tick time
            // for extrapolation tickTime can be greater than 1, and the formula still works
            /*
             * Linear interpolation:
             * Let x1 and x2 be the values we want to interpolate between
             * Let t be the progress (0..1) that we want to get between x1 and x2
             * Then,
             * x = x1 + (x2 - x1) * t
             */
            Vector3 interpolatePos = objPrev.position + (objNow.position - objPrev.position) * tickTime;
            Vector2 interpolateRot;
            if (Math.Abs(objNow.rotation.y - objPrev.rotation.y) <= 180)
                interpolateRot = objPrev.rotation + (objNow.rotation - objPrev.rotation) * tickTime;
            else
            {
                float minAlpha = Math.Min(objPrev.rotation.y, objNow.rotation.y), maxAlpha = Math.Max(objPrev.rotation.y, objNow.rotation.y);
                // we will interpolate separate on axis
                interpolateRot = new Vector2(
                    // interpolation on x remains the same
                    objPrev.rotation.x + (objNow.rotation.x - objPrev.rotation.x) * tickTime,
                    // for 360->0 loop around, we will shift the value closer to 0 with +360
                    // then we can apply linear interpolation and get clock-modular with 360
                    ClockModular(maxAlpha + (minAlpha + 360 - maxAlpha) * tickTime)
                    );
            }
            KObject interpolated = new KObject(objPrev.entityId, interpolatePos, interpolateRot);
            return interpolated;
        }
    }

    public class KTick
    {
        public readonly ulong Tick;
        public readonly List<KObject> objects;
        public KObject GetObject(ushort _eid) => (from x in objects where x.entityId == _eid select x).FirstOrDefault();
        public KTick(ulong _tick, List<KObject> _objects)
        {
            Tick = _tick;
            objects = _objects;
        }
    }

    public class KSnapshot
    {
        private readonly bool interporable;
        public KTick tick1, tick2;
        public float tickInterpolation;

        public KSnapshot(KTick tick1, KTick tick2, float tickInterpolation)
        {
            this.tick1 = tick1;
            this.tick2 = tick2;
            this.tickInterpolation = tickInterpolation;
            interporable = true;
        }

        public KSnapshot(KTick tick)
        {
            this.tick1 = tick;
            interporable = false;
        }

        public KObject GetObject(ushort _eid)
        {
            // non interporable, only one tick in snapshot
            if (!interporable) return tick1.GetObject(_eid);

            // find object in both ticks
            KObject obj1 = tick1.GetObject(_eid);
            KObject obj2 = tick2.GetObject(_eid);
            return KObject.Interpolate(obj1, obj2, tickInterpolation);
        }
    }

    public static class KTickManager
    {
        private static readonly List<KTick> ticks = new List<KTick>();
        public static ulong ServerTick { get; private set; } = 0;
        public static ulong RenderTick => ServerTick - KEngine.cl_interp;
        public static bool Synchronized = false;

        public static float timeInTick { get; private set; } = 0;

        public static void AdvanceTick()
        {
            timeInTick -= Time.fixedDeltaTime;
            ServerTick++;
        }
        public static void AdvanceSubtick()
        {
            timeInTick += Time.deltaTime;
            while (timeInTick >= Time.fixedDeltaTime)
                AdvanceTick();
        }
        public static void SyncTick(ulong serverTick)
        {
            ServerTick = serverTick;
            Synchronized = true;
        }

        public static void Reset()
        {
            Synchronized = false;
            ServerTick = 0;
            ticks.Clear();
        }

        public static KTick GetCurrentTick()
        {
            var candidate = (from x in ticks where x.Tick <= RenderTick select x);
            if (candidate == null || candidate.Count() == 0) return null;
            return candidate.OrderByDescending(x => x.Tick).FirstOrDefault();
        }

        public static KTick GetNextTick()
        {
            var candidate = (from x in ticks where x.Tick > RenderTick select x);
            // check if we have a tick available
            if (candidate == null || candidate.Count() == 0) return null;

            return candidate.OrderBy(x => x.Tick).FirstOrDefault();
        }

        public static KTick GetExtrapolationTick(ulong lastFrameTick)
        {
            return (from x in ticks where x.Tick < lastFrameTick select x).OrderByDescending(x => x.Tick).FirstOrDefault();
        }

        private static float interpRatio; // here for debug purposes
        public static KSnapshot GetSnapshot()
        {
            if (!Synchronized) return null;
            if (ticks.Count == 0) return null; // no ticks
            if (ticks.Count == 1) return new KSnapshot(ticks[0]); // send non-interpolable snapshot
            KTick renderTick = GetCurrentTick(),
                nextTick = GetNextTick();
            if (renderTick == null && nextTick != null) return new KSnapshot(nextTick); // we are looking too far behind (took me WAY too long to find this edge case lol)
            if (renderTick == null && nextTick == null) return new KSnapshot(ticks[0]); // both ticks are null, but we have a tick in the list, so we send it as non-interpolable
            // check if we need to extrapolate
            if (nextTick == null)
            {
                nextTick = renderTick;
                renderTick = GetExtrapolationTick(nextTick.Tick);
                if (nextTick.Tick - RenderTick > KEngine.cl_extrapolate)
                {
                    // warn player that we exceeded cl_extrapolate threshold
                    KillFeedGUI.AddText($"<color=red>Bad network.\nExtrapolating more than {KEngine.cl_extrapolate} ticks.</color>");
                }
            }
            // nice math btw
            interpRatio = (RenderTick + timeInTick / Time.fixedDeltaTime - renderTick.Tick) / (nextTick.Tick - renderTick.Tick);
            // if you want to know more about these, stay in school
            // also i wrote these without googling or researching, just a pen and paper and my giga-chad brain
            // but if you actually want to know why
            /*
             * Calculating the interpolation ratio:
             * Let t1 and t2 be two consecutive ticks (in memory),
             * That follow the rule: t1 <= T < t2
             * Where T is the tick we currently want to render (which is: ServerTick - KEngine.cl_interp)
             * Simply calculating where T resides between t1 and t2 to a scale 0..1
             * Can be done using the following:
             * (T - t1) / (t2 - t1)
             * t2 - t1 is the difference between ticks
             * T - t1 is the difference between T and t1
             * What we're actually calculating is how far T is from t1 relative to how far t2 is from t1
             * (We're translating T and t2 from t1 to 0, and calculating their ratio (or normalize T))
             * In order to account for the subtick value (which is timeInTick / prevTickTime, again diving to get a 0..1 scale (aka normalize timeInTick))
             * We just add it to T, which leads us to:
             * (T + timeInTick / prevTickTime - t1) / (t2 - t1)
             * 
             * Thanks for attending my TedTalk
             * For more math / competitve programming lessons contant me on discord @devilexe3
             */
            return new KSnapshot(renderTick, nextTick, interpRatio);
        }

        public static void RegisterFrame(KTick ktick)
        {
            ticks.Add(ktick);
            if (ticks.Count > 200)
                ticks.RemoveRange(0, ticks.Count - 200); // only keep 200 snapshots in memory
        }

        // these are here to render KarlsonMP debug
        public static ulong debug_ServerTick => ServerTick;
        public static ulong debug_RenderTick => RenderTick;
        public static float debug_subtick => timeInTick / Time.fixedDeltaTime;
        public static float debug_interpRatio => interpRatio;
    }

    public static class KEngine
    {
        public static CV_ushort cl_interp = new CV_ushort(2); // we will render 2 ticks behind (40ms of delay)
        public static CV_ushort cl_extrapolate = new CV_ushort(2); // we will allow at most 2 ticks to be extrapolated

        public static void RenderFrame()
        {
            if (!KTickManager.Synchronized) return;
            KTickManager.AdvanceSubtick();
            // get snapshot
            KSnapshot snapshot = KTickManager.GetSnapshot();
            if (snapshot == null) return; // no ticks
            if (snapshot.tick1 == null) return; // also no ticks but different

            // go through all players in list
            foreach (var player in PlaytimeLogic.players)
            {
                // get object (this is already interpolated by KSnapshot)
                KObject obj = snapshot.GetObject(player.id);
                if (obj == null) continue;
                player.Move(obj.position, obj.rotation);
            }
        }
    }
}
