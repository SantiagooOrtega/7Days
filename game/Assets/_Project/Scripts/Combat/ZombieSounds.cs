using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Combat
{
    /// <summary>
    /// Lo que dice y hace sonar un zombi concreto. Con su semilla elige garganta (timbre), tono base (que también cambia la velocidad), corte del filtro y cada
    /// cuánto gime, así 20 zombis no suenan iguales; cada muestra lleva además ±4 % de tono y ±15 % de volumen. Los sonidos van por el pool de ZombieAudio
    /// (12 voces, prioridad por cercanía). Sin alocaciones por frame.
    /// </summary>
    public sealed class ZombieSounds
    {
        private static int _nextOwner = 1;
        private readonly int _owner = _nextOwner++;
        private readonly int[] _last = new int[32];
        private Transform _at;
        private string _type;
        private int _timbre;
        private bool _heavy;
        private float _pitch, _cutoff, _idleScale, _near, _nextIdle, _nextMetal, _nextPain, _stepHalf;

        public bool Enabled { get; private set; }
        public int Owner => _owner;
        public float BasePitch => _pitch;
        public int Timbre => _timbre;

        public void Begin(Transform at, string typeId, int seed, bool emerging, bool enabled = true)
        {
            Stop();
            _at = at; _type = typeId; Enabled = enabled;
            var rng = new System.Random(seed ^ 0x5bd1e995);
            _timbre = rng.Next(ZombieSynth.Timbres);
            _pitch = 0.88f + (float)rng.NextDouble() * 0.24f;
            _cutoff = 2600f + (float)rng.NextDouble() * 4400f;
            _idleScale = 0.75f + (float)rng.NextDouble() * 0.6f;
            _heavy = typeId == ZombieIds.Tank || ZombieAnatomies.IsBoss(typeId);
            _near = ZombieAnatomies.IsBoss(typeId) ? 5f : (typeId == ZombieIds.Tank ? 3.5f : 1.5f);
            for (int i = 0; i < _last.Length; i++) _last[i] = -1;
            _nextIdle = Time.time + 1.5f + (float)rng.NextDouble() * 5f;
            _nextMetal = Time.time + 1f; _nextPain = 0f; _stepHalf = -1f;
            if (!enabled) return;
            ZombieSoundBank.Prewarm(typeId);
            if (emerging) Play(ZombieSound.Emerge, 1f, true);
        }

        public void Stop() => ZombieAudio.StopOwner(_owner);

        /// <summary>Gemidos, jadeos y gorgoteos de fondo, pisadas del tanque y chirridos del blindado.</summary>
        public void Tick(float dist, bool chasing, float moveAmount, float walkPhase, SevenDays.Player.PlayerImpulse shake)
        {
            if (!Enabled) return;
            float now = Time.time;
            if (now >= _nextIdle)
            {
                bool runner = _type == ZombieIds.Runner;
                // el corredor jadea seguido al perseguir; los demás gimen de vez en cuando (algo más al perseguir)
                float gap = runner ? (chasing ? Random.Range(1.2f, 2.4f) : Random.Range(4f, 8f)) : (chasing ? Random.Range(4f, 9f) : Random.Range(6f, 14f));
                _nextIdle = now + gap * _idleScale;
                if (dist < 30f) Play(ZombieSoundMap.Idle(_type), 0.8f, false);
            }
            if (moveAmount > 0.15f)
            {
                if (_heavy)
                {
                    // una pisada por medio ciclo de la zancada, con temblor de cámara si está cerca
                    float half = Mathf.Floor(walkPhase / Mathf.PI);
                    if (_stepHalf >= 0f && half != _stepHalf)
                    {
                        Play(ZombieSound.Step, Mathf.Lerp(0.6f, 1f, moveAmount), false, true);
                        if (shake != null && dist < 14f) shake.Shake(0.12f * (1f - dist / 14f));
                    }
                    _stepHalf = half;
                }
                else if (_type == ZombieIds.Armored && now >= _nextMetal)
                {
                    _nextMetal = now + Random.Range(1.3f, 2.8f);
                    Play(ZombieSound.Metal, 0.7f, false, true);
                }
            }
        }

        public void Alert() => Play(ZombieSoundMap.Alert(_type), 1f, true);
        public void Attack(ZombieAttackKind kind) => Play(kind == ZombieAttackKind.Bite ? ZombieSound.Bite : ZombieSound.Attack, 1f, true);
        public void Throw() => Play(ZombieSound.Retch, 1f, true);

        /// <summary>Dolor por zona (el de la cabeza, agudo y corto); un headshot que no mata suena además con el golpe seco. Como mucho uno cada 0.35 s (perdigones).</summary>
        public void Pain(HitZone zone, bool headshot)
        {
            if (!Enabled || Time.time < _nextPain) return;
            _nextPain = Time.time + 0.35f;
            if (headshot) Play(ZombieSound.Headshot, 1f, false, true);
            Play(ZombieSoundMap.Pain(zone), 0.9f, true);
            if (_type == ZombieIds.Armored && zone == HitZone.Torso) Play(ZombieSound.Metal, 0.6f, false, true);
        }

        /// <summary>Muerte: estertor largo; si fue de un headshot, solo el golpe seco (ya no tiene cabeza que gima).</summary>
        public void Death(bool headshot)
        {
            if (!Enabled) return;
            if (headshot) Play(ZombieSound.Headshot, 1f, true, true);
            else Play(ZombieSound.Death, 1f, true);
        }

        /// <summary>Estallido sordo del explosivo (se queda donde explotó).</summary>
        public void Blast(Vector3 at)
        {
            if (!Enabled) return;
            var clip = ZombieSoundBank.Pick(_type, ZombieSound.Blast, 0, ref _last[(int)ZombieSound.Blast]);
            ZombieAudio.Play(-1, null, at, clip, 1f, Random.Range(0.92f, 1.04f), 22000f, false, 4f);
        }

        /// <summary>Clip del bucle de la mecha (lo reproduce la fuente propia del explosivo).</summary>
        public AudioClip FuseClip() => ZombieSoundBank.Pick(_type, ZombieSound.Fuse, 0, ref _last[(int)ZombieSound.Fuse]);

        /// <summary>Reproduce un evento con la voz del individuo. sfx: efectos (pisada, metal, golpe) que no cambian con el tono ni el filtro de la garganta.</summary>
        public bool Play(ZombieSound ev, float volume, bool interrupt, bool sfx = false)
        {
            if (!Enabled || _at == null) return false;
            var clip = ZombieSoundBank.Pick(_type, ev, _timbre, ref _last[(int)ev]);
            if (clip == null) return false;
            float pitch = (sfx ? 1f : _pitch) * Random.Range(0.96f, 1.04f);
            float vol = volume * Random.Range(0.85f, 1f);
            Vector3 pos = _at.position + Vector3.up * (sfx && ev == ZombieSound.Step ? 0.1f : 1.5f);
            return ZombieAudio.Play(_owner, _at, pos, clip, vol, pitch, sfx ? 22000f : _cutoff, interrupt && !sfx, _near);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _nextOwner = 1;
    }
}
