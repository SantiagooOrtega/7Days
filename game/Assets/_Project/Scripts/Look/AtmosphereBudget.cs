using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Look
{
    /// <summary>
    /// Registro de los efectos atmosféricos para poder recortarlos si el FPS baja de 55: reduce la emisión de las
    /// partículas y desactiva grupos de detalles según el nivel (0 = todo, 3 = solo lo esencial).
    /// </summary>
    public static class AtmosphereBudget
    {
        private class Emitter
        {
            public ParticleSystem Ps;
            public float BaseRate;
            public int BaseMax;
            public int DisableAtLevel;
        }

        private class Group
        {
            public GameObject Go;
            public int DisableAtLevel;
        }

        private static readonly List<Emitter> Emitters = new List<Emitter>();
        private static readonly List<Group> Groups = new List<Group>();

        public static int Level { get; private set; }
        /// <summary>Se dispara al cambiar el nivel de recorte (el gestor de fuego reasigna sus luces).</summary>
        public static event System.Action<int> LevelChanged;

        /// <summary>Registra un sistema de partículas. disableAtLevel: nivel desde el que se apaga del todo (0-3; 4 = nunca).</summary>
        public static void Register(ParticleSystem ps, int disableAtLevel = 4)
        {
            if (ps == null) return;
            Emitters.Add(new Emitter { Ps = ps, BaseRate = ps.emission.rateOverTimeMultiplier, BaseMax = ps.main.maxParticles, DisableAtLevel = disableAtLevel });
            Apply(Emitters[Emitters.Count - 1]);
        }

        /// <summary>Registra un objeto de detalle (decals, maleza...) que se oculta desde cierto nivel.</summary>
        public static void RegisterGroup(GameObject go, int disableAtLevel)
        {
            if (go == null) return;
            Groups.Add(new Group { Go = go, DisableAtLevel = disableAtLevel });
            go.SetActive(Level < disableAtLevel);
        }

        /// <summary>Quita un sistema registrado (por ejemplo, un fuego que vuelve al pool).</summary>
        public static void Unregister(ParticleSystem ps) => Emitters.RemoveAll(e => e.Ps == ps);

        public static void SetLevel(int level)
        {
            Level = Mathf.Clamp(level, 0, 3);
            foreach (var e in Emitters) Apply(e);
            foreach (var g in Groups) if (g.Go != null) g.Go.SetActive(Level < g.DisableAtLevel);
            LevelChanged?.Invoke(Level);
        }

        private static void Apply(Emitter e)
        {
            if (e.Ps == null) return;
            bool off = Level >= e.DisableAtLevel;
            float k = off ? 0f : Mathf.Max(0.15f, QualityGovernor.DensityFactor(Level)); // lo esencial nunca baja de 15%
            var em = e.Ps.emission;
            em.rateOverTimeMultiplier = e.BaseRate * k;
            var main = e.Ps.main;
            main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(e.BaseMax * Mathf.Max(0.2f, k)));
            if (off && e.Ps.isPlaying) e.Ps.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Emitters.Clear();
            Groups.Clear();
            Level = 0;
            LevelChanged = null;
        }
    }

    /// <summary>Vigila el FPS: si baja de 55 sostenidamente recorta la densidad atmosférica. F3 cambia el nivel a mano.</summary>
    public class AtmosphereGovernor : MonoBehaviour
    {
        private readonly QualityGovernor _gov = new QualityGovernor();
        private float _fps = 60f;

        private void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.08f);
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f3Key.wasPressedThisFrame)
            {
                _gov.SetLevel((_gov.Level + 1) % (_gov.MaxLevel + 1));
                AtmosphereBudget.SetLevel(_gov.Level);
                return;
            }
            if (_gov.Update(Time.unscaledDeltaTime, _fps))
            {
                AtmosphereBudget.SetLevel(_gov.Level);
                Debug.Log("[7Days] FPS " + Mathf.RoundToInt(_fps) + " < 55: se recorta la atmósfera al nivel " + _gov.Level);
            }
        }
    }
}
