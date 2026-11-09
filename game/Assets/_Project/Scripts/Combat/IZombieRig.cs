using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Combat
{
    /// <summary>Lo que el controlador le cuenta al cuerpo cada frame (el cuerpo decide cómo animarlo).</summary>
    public struct ZombieAnimState
    {
        public float Dt;
        public float WalkPhase, MoveAmount, Time, Lunge;     // para el cuerpo antiguo de primitivas (jefes)
        public bool Chasing, Stunned, Dead;
        public float AttackT;                                // -1 sin ataque; 0..1 a lo largo del ataque (el daño cae en 0.5)
        public ZombieAttackKind AttackKind;
        public float DistanceToCamera;
        public BossPose BossPose;                            // habilidad de jefe en curso (None = ninguna)
        public float BossT;                                  // 0..1 a lo largo de la habilidad (el golpe cae en 0.5, al final del telegraph)
    }

    /// <summary>
    /// Cuerpo visual del zombi. Lo implementan el cuerpo procedural (ZombieBody: esqueleto del ProceduralRig, malla con pesos, decals de heridas) y el antiguo de primitivas
    /// (ZombieRig, de respaldo para tipos sin anatomía). El controlador solo habla con esta interfaz.
    /// </summary>
    public interface IZombieRig
    {
        Transform Visual { get; }
        Hitbox[] Hitboxes { get; }
        /// <summary>Al aparecer: elige variante, tonos y heridas del individuo según la semilla, reinicia la pose y empieza a emerger.</summary>
        void Setup(ZombieTypeData type, int seed, bool emerge);
        void SetColor(Color c);
        void SetFlash(bool on, Material flash);
        void SetWeakPoint(bool on);
        void SetHitboxesEnabled(bool on);
        /// <summary>Pose antigua por parámetros (jefes): fase del paso, intensidad, ataque, aturdimiento, tiempo, embestida.</summary>
        void Pose(float walkPhase, float amount, float attack, float stagger, float time, float lunge = 0f);
        void Animate(in ZombieAnimState s);
        void OnHit(HitZone zone, Vector3 dirWorld, float strength, float healthFraction);
        void OnDeath(HitZone lastZone, Vector3 dirWorld, bool headshot);
        /// <summary>Explosivo: inflado del vientre (1..1.35), brillo de las venas (0..1) y temblor (0..1).</summary>
        void SetFuse(float inflate, float glow, float shake);
        bool Emerging { get; }
        /// <summary>true cuando acaba de ocurrir el instante del golpe de un ataque pesado (golpe de suelo): se consume al leerlo.</summary>
        bool ConsumeImpact();
        /// <summary>Fase de furia de un jefe (0, 1 al 50 %, 2 al 25 %): ojos y venas más brillantes, pose agresiva y vapor.</summary>
        void SetFury(int phase);
    }
}
