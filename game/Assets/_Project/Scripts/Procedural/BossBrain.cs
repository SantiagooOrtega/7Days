using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public enum BossEventType { None, PhaseChanged, TelegraphStarted, AbilityFired, WeakPointExposed, WeakPointHidden, WallStunStarted, WallStunEnded }

    public struct BossEvent
    {
        public BossEventType Type;
        public string AbilityId;
        public int Phase;
        public float Seconds;   // duración del aviso (TelegraphStarted)
        public override string ToString() => Type + (AbilityId != null ? ":" + AbilityId : "") + (Type == BossEventType.PhaseChanged ? ":" + Phase : "");
    }

    /// <summary>
    /// Cerebro del jefe (puro, sin Unity): fases de furia por vida (50 % y 25 %, no retroceden), elección ponderada de habilidades con cooldown por habilidad y global,
    /// aviso (telegraph) antes de cada ataque, aturdimiento contra un muro con ventana de daño extra, y puntos débiles que solo se exponen cada cierto tiempo.
    /// El controlador de Unity solo ejecuta lo que este cerebro decide.
    /// </summary>
    public class BossBrain
    {
        public readonly BossData Data;
        public int Phase { get; private set; }
        public BossAbilityData Telegraphing { get; private set; }
        public float TelegraphRemaining { get; private set; }
        public bool WeakPointExposed { get; private set; }
        public float StunRemaining { get; private set; }
        public bool WallStunned => StunRemaining > 0f;

        private readonly Random _rng;
        private readonly float[] _cooldowns;
        private float _globalCooldown;
        private float _weakTimer;
        private float _weakExposeLeft;
        private readonly List<BossEvent> _events = new List<BossEvent>(4);

        public BossBrain(BossData data, int seed = 1)
        {
            Data = data;
            _rng = new Random(seed);
            _cooldowns = new float[data.abilities.Length];
            for (int i = 0; i < _cooldowns.Length; i++) _cooldowns[i] = Math.Min(2.5f, data.abilities[i].cooldown * 0.3f); // no ataca nada más aparecer
            _globalCooldown = 1.5f;
            _weakTimer = data.weakPointPeriod;
        }

        public float SpeedMultiplier => Data.furySpeed[Phase];
        public float AttackRateMultiplier => Data.furyAttackRate[Phase];
        /// <summary>Daño que recibe de más mientras está aturdido contra un muro (1 = normal).</summary>
        public float VulnerabilityMultiplier => WallStunned ? Data.stunnedExtraDamage : 1f;
        public bool Busy => Telegraphing != null;

        public static int PhaseFor(BossData d, float fraction) => fraction <= d.phase3At ? 2 : (fraction <= d.phase2At ? 1 : 0);

        /// <summary>Un golpe en el punto débil solo cuenta como tal si está expuesto; si no, el impacto es de torso.</summary>
        public HitZone ResolveZone(HitZone z) => z == HitZone.WeakPoint && !WeakPointExposed ? HitZone.Torso : z;

        /// <summary>Choca contra un muro durante la carga: queda aturdido y vulnerable.</summary>
        public void WallStun()
        {
            StunRemaining = Data.wallStunSeconds;
            Telegraphing = null;
            _globalCooldown = Math.Max(_globalCooldown, Data.wallStunSeconds);
        }

        /// <summary>Avanza el cerebro. Devuelve los eventos de este paso (la lista se reutiliza: léela antes del siguiente Tick).</summary>
        public List<BossEvent> Tick(float dt, float healthFraction, float distanceToPlayer, bool alive = true)
        {
            _events.Clear();
            if (!alive) { Telegraphing = null; return _events; }

            int phase = PhaseFor(Data, healthFraction);
            if (phase > Phase)
            {
                Phase = phase;
                Telegraphing = null;                   // un cambio de fase interrumpe el ataque en preparación
                _globalCooldown = Math.Max(_globalCooldown, 1.2f);   // y se toma un momento para rugir
                _events.Add(new BossEvent { Type = BossEventType.PhaseChanged, Phase = Phase });
            }

            if (StunRemaining > 0f)
            {
                StunRemaining -= dt;
                if (StunRemaining <= 0f) { StunRemaining = 0f; _events.Add(new BossEvent { Type = BossEventType.WallStunEnded }); }
                TickWeak(dt);
                return _events;
            }

            float rate = AttackRateMultiplier;
            for (int i = 0; i < _cooldowns.Length; i++) _cooldowns[i] = Math.Max(0f, _cooldowns[i] - dt * rate);
            _globalCooldown = Math.Max(0f, _globalCooldown - dt);
            TickWeak(dt);

            if (Telegraphing != null)
            {
                TelegraphRemaining -= dt;
                if (TelegraphRemaining <= 0f)
                {
                    var a = Telegraphing;
                    Telegraphing = null;
                    int idx = Array.IndexOf(Data.abilities, a);
                    if (idx >= 0) _cooldowns[idx] = a.cooldown;
                    _globalCooldown = Data.globalCooldown;
                    _events.Add(new BossEvent { Type = BossEventType.AbilityFired, AbilityId = a.id, Phase = Phase });
                }
                return _events;
            }

            if (_globalCooldown > 0f) return _events;
            var pick = Pick(distanceToPlayer);
            if (pick != null)
            {
                Telegraphing = pick;
                // en furia los avisos son un poco más cortos, pero nunca menos de 0.5 s (el jugador necesita poder reaccionar)
                TelegraphRemaining = Math.Max(0.5f, pick.telegraph * (Phase == 0 ? 1f : Phase == 1 ? 0.9f : 0.8f));
                _events.Add(new BossEvent { Type = BossEventType.TelegraphStarted, AbilityId = pick.id, Phase = Phase, Seconds = TelegraphRemaining });
            }
            return _events;
        }

        private void TickWeak(float dt)
        {
            if (!Data.hasWeakPoint) return;
            if (WeakPointExposed)
            {
                _weakExposeLeft -= dt;
                if (_weakExposeLeft <= 0f) { WeakPointExposed = false; _weakTimer = Data.weakPointPeriod; _events.Add(new BossEvent { Type = BossEventType.WeakPointHidden }); }
            }
            else
            {
                _weakTimer -= dt;
                if (_weakTimer <= 0f) { WeakPointExposed = true; _weakExposeLeft = Data.weakPointExposeSeconds; _events.Add(new BossEvent { Type = BossEventType.WeakPointExposed }); }
            }
        }

        /// <summary>Expone el punto débil ya (p. ej. justo después de la onda expansiva).</summary>
        public void ExposeWeakPoint()
        {
            if (!Data.hasWeakPoint || WeakPointExposed) return;
            WeakPointExposed = true; _weakExposeLeft = Data.weakPointExposeSeconds;
        }

        private BossAbilityData Pick(float distance)
        {
            float total = 0f;
            var eligible = new List<int>(4);
            for (int i = 0; i < Data.abilities.Length; i++)
            {
                var a = Data.abilities[i];
                if (a.minPhase > Phase || _cooldowns[i] > 0f || distance < a.minRange || distance > a.maxRange) continue;
                eligible.Add(i); total += a.weight;
            }
            if (eligible.Count == 0) return null;
            float roll = (float)_rng.NextDouble() * total;
            foreach (int i in eligible) { roll -= Data.abilities[i].weight; if (roll <= 0f) return Data.abilities[i]; }
            return Data.abilities[eligible[eligible.Count - 1]];
        }
    }
}
