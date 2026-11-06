using UnityEngine;

namespace SevenDays.Look
{
    /// <summary>Parpadeo de fuego/lámpara con ruido Perlin (barata: solo cambia intensidad y rango).</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        public float Amount = 0.35f;
        public float Speed = 9f;

        private Light _light;
        private float _baseIntensity;
        private float _baseRange;
        private float _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _baseIntensity = _light.intensity;
            _baseRange = _light.range;
            _seed = Random.value * 100f;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * Speed);
            float k = 1f - Amount + n * Amount * 2f;
            _light.intensity = _baseIntensity * k;
            _light.range = _baseRange * (0.95f + 0.1f * n);
        }
    }
}
