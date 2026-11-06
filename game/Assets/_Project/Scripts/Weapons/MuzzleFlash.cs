using UnityEngine;
using SevenDays.Core;

namespace SevenDays.Weapons
{
    /// <summary>Destello del cañón: una luz puntual y una esfera brillante durante ~50 ms.</summary>
    public class MuzzleFlash : MonoBehaviour
    {
        private const float Duration = 0.05f;
        private GameObject _root;
        private float _until;

        private void Awake()
        {
            _root = new GameObject("MuzzleFlash");
            _root.transform.SetParent(transform, false);

            var light = _root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.45f);
            light.range = 7f;
            light.intensity = 5f;
            light.shadows = LightShadows.None;

            Prim.Create(PrimitiveType.Sphere, "Glow", Vector3.zero, Vector3.one * 0.16f,
                Prim.GetEmissive(new Color(1f, 0.8f, 0.4f), 8f), _root.transform, false)
                .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _root.SetActive(false);
        }

        public void Flash()
        {
            _until = Time.time + Duration;
            _root.SetActive(true);
        }

        private void Update()
        {
            if (_root.activeSelf && Time.time >= _until) _root.SetActive(false);
        }
    }
}
