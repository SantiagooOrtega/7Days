using UnityEngine;
using SevenDays.Core;

namespace SevenDays.Combat
{
    /// <summary>
    /// Voces de zombi: un pool fijo de 12 AudioSource 3D (rolloff logarítmico, 40 m como máximo) que siguen al zombi que habla. Si están todas ocupadas, un sonido
    /// nuevo solo entra quitándole la voz al más lejano del oyente, y solo si el nuevo está más cerca: siempre se oye lo que está encima. Cada voz lleva un filtro
    /// paso-bajo con el corte del individuo (timbre). Sin alocaciones por frame (arrays fijos).
    /// </summary>
    public class ZombieAudio : MonoBehaviour
    {
        public const int MaxVoices = 12;
        public const float MaxDistance = 40f;

        private static ZombieAudio _instance;
        private readonly AudioSource[] _src = new AudioSource[MaxVoices];
        private readonly AudioLowPassFilter[] _lp = new AudioLowPassFilter[MaxVoices];
        private readonly Transform[] _follow = new Transform[MaxVoices];
        private readonly Vector3[] _offset = new Vector3[MaxVoices];
        private readonly int[] _owner = new int[MaxVoices];
        private readonly float[] _until = new float[MaxVoices];
        private Transform _listener;
        private int _listenerFrame = -1;

        /// <summary>Voces sonando ahora mismo (panel F6).</summary>
        public static int ActiveVoices { get; private set; }
        /// <summary>Sonidos descartados por estar lejos o sin voz libre (panel F6).</summary>
        public static int Dropped { get; private set; }

        private static ZombieAudio Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("ZombieAudio");
                if (Application.isPlaying) DontDestroyOnLoad(go);
                _instance = go.AddComponent<ZombieAudio>();
                _instance.Build();
                return _instance;
            }
        }

        private void Build()
        {
            for (int i = 0; i < MaxVoices; i++)
            {
                var go = new GameObject("Voz " + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 1.5f; s.maxDistance = MaxDistance;
                s.dopplerLevel = 0f; s.priority = 100;
                _src[i] = s;
                _lp[i] = go.AddComponent<AudioLowPassFilter>();
                _lp[i].cutoffFrequency = 22000f; _lp[i].lowpassResonanceQ = 1.2f;
                _owner[i] = -1;
            }
        }

        private Vector3 ListenerPosition()
        {
            if (_listenerFrame != Time.frameCount)
            {
                _listenerFrame = Time.frameCount;
                var cam = Camera.main;
                _listener = cam != null ? cam.transform : null;
            }
            return _listener != null ? _listener.position : Vector3.zero;
        }

        /// <summary>
        /// Reproduce un sonido de zombi en 3D. owner: id del zombi (con interrupt, corta la voz que ya tuviera, p. ej. el gemido al morir); follow: a quién sigue
        /// la voz (null = queda en pos); nearRadius: distancia sin atenuación (más grande para los gigantes). Devuelve false si no entró.
        /// </summary>
        public static bool Play(int owner, Transform follow, Vector3 pos, AudioClip clip, float volume, float pitch, float cutoff, bool interrupt, float nearRadius = 1.5f)
        {
            if (clip == null) return false;
            var a = Instance;
            Vector3 lp = a.ListenerPosition();
            float dist = (pos - lp).magnitude;
            if (dist > MaxDistance) { Dropped++; return false; }
            float now = Time.unscaledTime;
            int slot = -1;
            if (interrupt && owner >= 0)
                for (int i = 0; i < MaxVoices; i++) if (_instance._owner[i] == owner && a._until[i] > now) { slot = i; break; }
            if (slot < 0) for (int i = 0; i < MaxVoices; i++) if (a._until[i] <= now) { slot = i; break; }
            if (slot < 0)
            {
                // todas ocupadas: se roba la del más lejano, si el nuevo está más cerca
                float far = dist;
                for (int i = 0; i < MaxVoices; i++)
                {
                    float d = (a._src[i].transform.position - lp).magnitude;
                    if (d > far) { far = d; slot = i; }
                }
                if (slot < 0) { Dropped++; return false; }
            }
            var s = a._src[slot];
            s.Stop();
            a._follow[slot] = follow;
            a._offset[slot] = follow != null ? pos - follow.position : Vector3.zero;
            s.transform.position = pos;
            s.clip = clip; s.pitch = pitch; s.volume = volume * AudioMix.Volume(AudioGroup.Zombies); s.minDistance = nearRadius;
            a._lp[slot].cutoffFrequency = cutoff;
            a._owner[slot] = owner;
            a._until[slot] = now + clip.length / Mathf.Max(0.1f, pitch) + 0.05f;
            s.Play();
            return true;
        }

        /// <summary>Corta todo lo que esté diciendo un zombi (al volver al pool).</summary>
        public static void StopOwner(int owner)
        {
            if (_instance == null) return;
            for (int i = 0; i < MaxVoices; i++)
                if (_instance._owner[i] == owner) { _instance._src[i].Stop(); _instance._until[i] = 0f; _instance._owner[i] = -1; _instance._follow[i] = null; }
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            int active = 0;
            for (int i = 0; i < MaxVoices; i++)
            {
                if (_until[i] <= now) { _follow[i] = null; continue; }
                active++;
                var f = _follow[i];
                if (f != null && f.gameObject.activeInHierarchy) _src[i].transform.position = f.position + _offset[i];
            }
            ActiveVoices = active;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; ActiveVoices = 0; Dropped = 0; }
    }
}
