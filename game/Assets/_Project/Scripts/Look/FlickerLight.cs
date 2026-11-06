using UnityEngine;

namespace SevenDays.Look
{
    /// <summary>
    /// Parpadeo de fuego/lámpara con ruido Perlin (barato: solo cambia intensidad, rango y color).
    /// RedMix tiñe la luz de rojo por ratos y Blackouts añade apagones breves (farolas defectuosas, ambiente tenso).
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        public float Amount = 0.35f;
        public float Speed = 9f;
        /// <summary>0..1: cuánto se tiñe de rojo por ratos.</summary>
        public float RedMix;
        public bool Blackouts;
        /// <summary>Si es mayor que 0, la luz parpadea encendida/apagada a esa frecuencia (luz de emergencia de una señal).</summary>
        public float BlinkHz;
        /// <summary>Bombilla emisiva que acompaña a la luz (opcional).</summary>
        public Renderer Bulb;

        private static readonly Color Red = new Color(1f, 0.12f, 0.08f);
        private Light _light;
        private float _baseIntensity, _baseRange, _seed;
        private Color _baseColor;
        private MaterialPropertyBlock _mpb;
        private float _blackoutUntil, _nextBlackout;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _baseIntensity = _light.intensity;
            _baseRange = _light.range;
            _baseColor = _light.color;
            _seed = Random.value * 100f;
            _nextBlackout = Time.time + Random.Range(4f, 12f);
            if (Bulb != null) _mpb = new MaterialPropertyBlock();
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * Speed);
            float k = 1f - Amount + n * Amount * 2f;

            Color c = _baseColor;
            if (RedMix > 0f)
            {
                float red = RedMix * Mathf.SmoothStep(0.35f, 0.8f, Mathf.PerlinNoise(_seed + 50f, Time.time * 0.6f));
                c = Color.Lerp(_baseColor, Red, red);
            }

            if (Blackouts)
            {
                if (Time.time >= _nextBlackout)
                {
                    _blackoutUntil = Time.time + Random.Range(0.06f, 0.22f);
                    _nextBlackout = Time.time + Random.Range(4f, 12f);
                }
                if (Time.time < _blackoutUntil) k *= 0.04f;
            }

            if (BlinkHz > 0f && Mathf.Repeat(Time.time * BlinkHz, 1f) > 0.4f) k *= 0.03f; // 40% encendida, 60% apagada

            _light.color = c;
            _light.intensity = _baseIntensity * k;
            _light.range = _baseRange * (0.95f + 0.1f * n);

            if (Bulb != null)
            {
                _mpb.SetColor("_EmissionColor", c * (5f * Mathf.Clamp01(k)));
                Bulb.SetPropertyBlock(_mpb);
            }
        }
    }
}
