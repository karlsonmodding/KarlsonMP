using Riptide;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMP
{
    public class BulletRenderer
    {
        static uint bulletId = 0;
        static Dictionary<uint, (Coroutine, GameObject)> activeBullets = new Dictionary<uint, (Coroutine, GameObject)>();
        public static void DrawBullet(Vector3 from, Vector3 to, Color color, bool hitEffect = true)
        {
            // if not in level, don't do anything yet
            if (!ClientHandle.PlayerList) return;
            if (hitEffect)
            {
                var ps = UnityEngine.Object.Instantiate(PrefabManager.Instance.bulletDestroy, to, Quaternion.identity).GetComponent<ParticleSystem>();
                ps.transform.rotation = Quaternion.LookRotation((from - to) / (from - to).magnitude);
                var main = ps.main;
                main.startColor = color;
                UnityEngine.Object.Instantiate(PrefabManager.Instance.bulletHitAudio, to, Quaternion.identity);
            }
            GameObject br = new GameObject("Bullet Renderer");
            LineRenderer lr = br.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.widthMultiplier = 0.2f;
            lr.positionCount = 2;
            lr.startColor = color;
            lr.endColor = color;
            lr.SetPositions(new Vector3[] { from, to });
            uint myBulletId = bulletId;
            IEnumerator DestroyBr()
            {
                for (float m = 0.19f; m >= 0; m -= 0.01f)
                {
                    yield return new WaitForSeconds(0.02f);
                    lr.widthMultiplier = m;
                }
                UnityEngine.Object.Destroy(br);
                activeBullets.Remove(myBulletId);
            }
            activeBullets.Add(bulletId++, (Loader.monoHooks.StartCoroutine(DestroyBr()), br));
        }

        public static void DeleteBullets()
        {
            foreach (var b in activeBullets)
            {
                Loader.monoHooks.StopCoroutine(b.Value.Item1);
                UnityEngine.Object.Destroy(b.Value.Item2);
            }
            activeBullets.Clear();
        }
    }
}
