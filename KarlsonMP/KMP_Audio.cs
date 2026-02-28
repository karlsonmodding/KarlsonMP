using Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMP
{
    public class KMP_AudioManager
    {
        private static readonly string[] clips = new string[] { "hitself", "hitmarker", "kill", "pistol", "reload", "shotgun", "smg", "death" };

        public static List<AudioSource> sources = new List<AudioSource>();
        public static void Initialize(AssetBundle bundle)
        {
            foreach (var c in clips)
            {
                var source = AudioManager.Instance.gameObject.AddComponent<AudioSource>();
                var clip = bundle.LoadAsset<AudioClip>("assets/karlsonmp/sfx/" + c + ".mp3");
                source.clip = clip;
                source.loop = false;
                source.volume = 1f;
                source.pitch = 1f;
                source.ignoreListenerPause = true;
                source.ignoreListenerVolume = true;
                source.bypassEffects = true;
                source.bypassListenerEffects = true;
                source.bypassReverbZones = true;
                sources.Add(source);
            }
        }
        public static void PlaySound(string name, float pitchVar = 0f) => PlaySoundDelayed(name, 0f, pitchVar);
        public static void PlaySoundDelayed(string name, float delay, float pitchVar)
        {
            var clip = (from x in sources where x.clip.name == name select x).First();
            clip.volume = AudioListener.volume;
            clip.pitch = 1f + UnityEngine.Random.Range(-pitchVar, pitchVar);
            clip.PlayDelayed(delay);
        }
    }
}
