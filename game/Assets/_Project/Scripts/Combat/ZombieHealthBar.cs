using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Combat
{
    /// <summary>
    /// Barra de vida en el mundo sobre la cabeza del zombi (dos quads que miran a la cámara). Se crea una sola vez por zombi del pool. Solo se ve si recibió
    /// daño hace poco o el jugador está cerca; el relleno se suaviza y pasa de verde a amarillo y rojo (9 materiales compartidos, sin gastar uno por barra).
    /// </summary>
    public class ZombieHealthBar : MonoBehaviour
    {
        private const float Width = 0.9f, Height = 0.09f;
        private static readonly Material[] FillMats = new Material[9];
        private static Material _back;

        private Transform _fill, _root;
        private Renderer _fillR;
        private Health _health;
        private float _shown = 1f, _lastDamage = -100f;
        private float _height;
        private bool _visible;
        public bool Suppressed;   // los jefes usan la barra grande del HUD

        public static ZombieHealthBar Create(Transform zombie, Health health)
        {
            var go = new GameObject("HealthBar");
            go.transform.SetParent(zombie, false);
            var bar = go.AddComponent<ZombieHealthBar>();
            bar.Build(health);
            return bar;
        }

        private void Build(Health health)
        {
            _health = health;
            _root = transform;
            if (_back == null) _back = Prim.GetEmissive(new Color(0.05f, 0.05f, 0.05f), 0.6f);
            var back = Prim.Create(PrimitiveType.Quad, "back", Vector3.zero, new Vector3(Width + 0.04f, Height + 0.04f, 1f), _back, _root, false);
            var fill = Prim.Create(PrimitiveType.Quad, "fill", new Vector3(0f, 0f, -0.003f), new Vector3(Width, Height, 1f), FillMaterial(1f), _root, false);
            back.layer = 2; fill.layer = 2;
            foreach (var r in new[] { back.GetComponent<Renderer>(), fill.GetComponent<Renderer>() }) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _fill = fill.transform; _fillR = fill.GetComponent<Renderer>();
            _health.Damaged += _ => _lastDamage = Time.time;
            _root.gameObject.SetActive(false);
        }

        public void ResetBar(float height)
        {
            _height = height;
            _shown = 1f; _lastDamage = -100f; _visible = false;
            _root.gameObject.SetActive(false);
        }

        private static Material FillMaterial(float fraction)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(fraction * 8f), 0, 8);
            if (FillMats[i] == null)
            {
                FeedbackMath.BarColor(i / 8f, out float r, out float g, out float b);
                FillMats[i] = Prim.GetEmissive(new Color(r, g, b), 1.4f);
            }
            return FillMats[i];
        }

        /// <summary>Se llama desde el zombi cada frame (solo mientras está en uso); cuesta casi nada cuando la barra está oculta.</summary>
        public void Tick(float distanceToPlayer, Camera cam)
        {
            bool want = !Suppressed && FeedbackMath.BarVisible(Time.time - _lastDamage, distanceToPlayer, _health.IsDead, _health.Fraction >= 0.999f);
            if (want != _visible) { _visible = want; _root.gameObject.SetActive(want); }
            if (!want || cam == null) return;

            _shown = FeedbackMath.Smooth(_shown, _health.Fraction, Time.deltaTime);
            _fill.localScale = new Vector3(Mathf.Max(0.0001f, Width * _shown), Height, 1f);
            _fill.localPosition = new Vector3(-(Width - Width * _shown) * 0.5f, 0f, -0.003f);
            _fillR.sharedMaterial = FillMaterial(_shown);
            _root.position = transform.parent.position + Vector3.up * _height;
            _root.rotation = cam.transform.rotation;
        }
    }
}
