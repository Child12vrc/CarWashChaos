using System.Collections.Generic;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>Pooled 3D one-shots by SFX id (TECH §2). Clips are registered by id (AudioLibrary in T6.3).</summary>
    public class AudioService : MonoBehaviour
    {
        [SerializeField] private int _poolSize = 24;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> _pool = new List<AudioSource>();
        int _next;

        void Awake()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject("sfx_" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.maxDistance = 30f;
                _pool.Add(src);
            }
        }

        public void Register(string id, AudioClip clip) => _clips[id] = clip;

        public void Play3D(string id, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            if (!_clips.TryGetValue(id, out var clip) || clip == null) return; // DESIGN-GAP: audio assets come in T6.3
            var src = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            src.transform.position = pos;
            src.pitch = pitch;
            var s = Services.Get<Settings>();
            src.PlayOneShot(clip, volume * (s != null ? s.SfxVolume : 1f)); // P2: SFX channel (master = AudioListener.volume)
        }
    }
}
