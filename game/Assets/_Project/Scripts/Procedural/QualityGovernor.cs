using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Decide el nivel de calidad (0 = Alta .. 3 = Mínima) según el FPS: si se mantiene por debajo del objetivo unos segundos (pasado un calentamiento) baja un
    /// nivel y espera; si se mantiene holgado por encima durante RecoverSeconds (y hay margen), SUBE un nivel. Histéresis: el umbral de subida (58) está por encima
    /// del de bajada (55) y las dos esperas son distintas, así que no oscila. C# puro para poder probarlo sin Unity.
    /// </summary>
    public class QualityGovernor
    {
        public float TargetFps = 55f;
        public float RecoverFps = 58f;
        public float WarmupSeconds = 6f;
        public float SustainSeconds = 2.5f;
        public float RecoverSeconds = 4f;
        public float CooldownSeconds = 5f;
        public int MaxLevel = 3;

        public int Level { get; private set; }

        private float _time, _lowTime, _highTime, _cooldown;

        /// <summary>
        /// Avanza el reloj. "canDegrade": la resolución dinámica ya está en su mínimo (primero se baja la resolución, luego la calidad).
        /// "headroom": la resolución dinámica está en su máximo (solo entonces se recupera calidad). Devuelve true si el nivel cambió.
        /// </summary>
        public bool Update(float dt, float fps, bool canDegrade = true, bool headroom = true)
        {
            _time += dt;
            if (_time < WarmupSeconds) return false;
            if (_cooldown > 0f) { _cooldown -= dt; _lowTime = _highTime = 0f; return false; }
            if (fps < TargetFps && canDegrade)
            {
                _highTime = 0f;
                _lowTime += dt;
                if (_lowTime < SustainSeconds || Level >= MaxLevel) return false;
                Level++;
                _lowTime = 0f; _cooldown = CooldownSeconds;
                return true;
            }
            _lowTime = 0f;
            if (fps >= RecoverFps && headroom && Level > 0)
            {
                _highTime += dt;
                if (_highTime < RecoverSeconds) return false;
                Level--;
                _highTime = 0f; _cooldown = CooldownSeconds;
                return true;
            }
            _highTime = 0f;
            return false;
        }

        /// <summary>Fija el nivel a mano (tecla F3 o menú) y reinicia las esperas.</summary>
        public void SetLevel(int level)
        {
            Level = level < 0 ? 0 : (level > MaxLevel ? MaxLevel : level);
            _lowTime = _highTime = 0f;
            _cooldown = CooldownSeconds;
        }

        /// <summary>Factor de densidad atmosférica (1 = completo) para un nivel: 1, 0.6, 0.3, 0.</summary>
        public static float DensityFactor(int level) => level <= 0 ? 1f : (level == 1 ? 0.6f : (level == 2 ? 0.3f : 0f));
    }

    /// <summary>
    /// Resolución dinámica por tiempo de GPU (pura): render scale entre Min y Max. Si la GPU pasa de DownMs durante DownSeconds baja un paso (rápido: evita tirones);
    /// si se queda por debajo de UpMs durante UpSeconds sube medio paso (despacio). Entre UpMs y DownMs no cambia: histéresis, sin parpadeo.
    /// Sin dato de GPU (0) usa el tiempo de frame.
    /// </summary>
    public class DynamicResolution
    {
        public float Min = 0.6f, Max = 1f;
        public float DownMs = 15f, UpMs = 11.5f;          // presupuesto de 60 FPS = 16.7 ms
        public float DownSeconds = 0.35f, UpSeconds = 3f;
        public float DownStep = 0.1f, UpStep = 0.05f;
        public float Scale { get; private set; } = 1f;

        private float _over, _under;

        public bool AtMin => Scale <= Min + 1e-3f;
        public bool AtMax => Scale >= Max - 1e-3f;

        public void SetRange(float min, float max)
        {
            Min = Clamp(min, 0.5f, 1f); Max = Clamp(max, Min, 1f);
            Scale = Clamp(Scale, Min, Max);
        }

        /// <summary>Avanza; devuelve true si cambió la escala.</summary>
        /// <summary>Sin dato de GPU, el tiempo de frame con el límite de 60 FPS siempre ronda 16.7 ms: solo se baja si se pierden frames (&lt; ~55 FPS) y se sube más despacio.</summary>
        public float DownFrameMs = 18.2f, UpFrameMs = 17.3f;

        public bool Update(float dt, float gpuMs, float frameMs)
        {
            bool gpu = gpuMs > 0.1f;
            float ms = gpu ? gpuMs : frameMs, down = gpu ? DownMs : DownFrameMs, up = gpu ? UpMs : UpFrameMs, upAfter = gpu ? UpSeconds : UpSeconds * 2f;
            if (ms > down) { _over += dt; _under = 0f; }
            else if (ms < up) { _under += dt; _over = 0f; }
            else { _over = 0f; _under = 0f; }
            float before = Scale;
            if (_over >= DownSeconds) { Scale = Clamp(Scale - DownStep, Min, Max); _over = 0f; }
            else if (_under >= upAfter) { Scale = Clamp(Scale + UpStep, Min, Max); _under = 0f; }
            return Math.Abs(Scale - before) > 1e-4f;
        }

        public void Reset(float scale) { Scale = Clamp(scale, Min, Max); _over = _under = 0f; }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    public enum ShadowSoftness { Hard, Low, Medium }

    /// <summary>Lo que cambia cada nivel de calidad (documentado en docs/UNITY_SETUP.md).</summary>
    public struct QualityProfile
    {
        public string Name;
        public float ShadowDistance;
        public int Cascades;
        public ShadowSoftness Softness;
        public bool Ssao, SsaoHalf;
        public bool Bloom, BloomQuarter;
        public bool Grain;
        public float MinScale, MaxScale;   // rango de la resolución dinámica
        public int Atmosphere;             // nivel de recorte de AtmosphereBudget (0 todo .. 3 lo esencial)

        public static readonly string[] Names = { "Alta", "Media", "Baja", "Mínima" };

        public static QualityProfile For(int level)
        {
            switch (level <= 0 ? 0 : (level >= 3 ? 3 : level))
            {
                case 0: return new QualityProfile { Name = Names[0], ShadowDistance = 50f, Cascades = 2, Softness = ShadowSoftness.Medium, Ssao = true, SsaoHalf = true, Bloom = true, BloomQuarter = false, Grain = true, MinScale = 0.75f, MaxScale = 1f, Atmosphere = 0 };
                case 1: return new QualityProfile { Name = Names[1], ShadowDistance = 42f, Cascades = 2, Softness = ShadowSoftness.Low, Ssao = true, SsaoHalf = true, Bloom = true, BloomQuarter = true, Grain = true, MinScale = 0.67f, MaxScale = 1f, Atmosphere = 1 };
                case 2: return new QualityProfile { Name = Names[2], ShadowDistance = 35f, Cascades = 1, Softness = ShadowSoftness.Hard, Ssao = false, SsaoHalf = true, Bloom = true, BloomQuarter = true, Grain = false, MinScale = 0.6f, MaxScale = 0.9f, Atmosphere = 2 };
                default: return new QualityProfile { Name = Names[3], ShadowDistance = 30f, Cascades = 1, Softness = ShadowSoftness.Hard, Ssao = false, SsaoHalf = true, Bloom = false, BloomQuarter = true, Grain = false, MinScale = 0.6f, MaxScale = 0.8f, Atmosphere = 3 };
            }
        }
    }
}
