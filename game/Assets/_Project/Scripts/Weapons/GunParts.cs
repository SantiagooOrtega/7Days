using UnityEngine;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.Weapons
{
    /// <summary>
    /// Arma armada por piezas separadas (cuerpo, empuñadura, cargador, corredera o cerrojo, bomba, tambor) para poder animarlas: la corredera retrocede al disparar,
    /// la bomba y el cerrojo ciclan, el cargador sale y entra al recargar y el tambor se abre. Puntos de referencia: Tip (boca/punta), CasingPoint (expulsión).
    /// Provisional con primitivas; las mallas detalladas y texturas llegan en el Bloque 4 con los mismos nombres de piezas.
    /// </summary>
    public class GunParts : MonoBehaviour
    {
        public Transform Body, Mag, Slide, Pump, Cylinder, Bolt, Tip, CasingPoint;
        public TrailRenderer Trail;
        private Vector3 _magRest, _slideRest, _pumpRest, _boltRest;
        private Quaternion _cylRest, _boltRotRest;
        private Renderer _magRenderer;

        public static GunParts Build(WeaponData d, Transform parent)
        {
            var root = new GameObject("Gun");
            root.transform.SetParent(parent, false);
            var g = root.AddComponent<GunParts>();
            var c = d.color;
            Color main = new Color(c[0], c[1], c[2]), dark = main * 0.55f, light = Color.Lerp(main, Color.white, 0.25f);
            dark.a = light.a = 1f;
            float len = d.length, th = d.thickness;

            if (d.IsMelee)
            {
                g.Body = P(root.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 0f, len * 0.5f), new Vector3(th, th, len), main);
                P(root.transform, PrimitiveType.Cube, "Grip", new Vector3(0f, 0f, 0.08f), new Vector3(th * 1.5f, th * 1.5f, 0.16f), new Color(0.15f, 0.12f, 0.1f));
            }
            else
            {
                float h = 0.1f;
                g.Body = P(root.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 0f, len * 0.5f), new Vector3(th, h, len), main);
                P(root.transform, PrimitiveType.Cube, "Grip", new Vector3(0f, -0.09f, 0.04f), new Vector3(th * 0.85f, 0.12f, 0.055f), dark, 15f);
                switch (d.kind)
                {
                    case WeaponKind.Pistol:
                        g.Slide = P(root.transform, PrimitiveType.Cube, "Slide", new Vector3(0f, 0.055f, len * 0.5f), new Vector3(th * 1.05f, 0.04f, len * 0.98f), light);
                        g.Mag = P(root.transform, PrimitiveType.Cube, "Mag", new Vector3(0f, -0.12f, 0.045f), new Vector3(th * 0.7f, 0.08f, 0.04f), dark, 15f);
                        break;
                    case WeaponKind.Revolver:
                        g.Cylinder = P(root.transform, PrimitiveType.Cylinder, "Cylinder", new Vector3(0f, 0.01f, len * 0.32f), new Vector3(0.075f, 0.04f, 0.075f), dark);
                        g.Cylinder.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        P(root.transform, PrimitiveType.Cylinder, "Barrel", new Vector3(0f, 0.025f, len * 0.75f), new Vector3(0.03f, len * 0.25f, 0.03f), main).localRotation = Quaternion.Euler(90f, 0f, 0f);
                        break;
                    case WeaponKind.Shotgun:
                        g.Pump = P(root.transform, PrimitiveType.Cube, "Pump", new Vector3(0f, -0.065f, len * 0.42f), new Vector3(th * 1.15f, 0.055f, len * 0.24f), new Color(0.32f, 0.22f, 0.14f));
                        P(root.transform, PrimitiveType.Cube, "Stock", new Vector3(0f, -0.02f, -0.1f), new Vector3(th * 0.9f, 0.11f, 0.22f), new Color(0.32f, 0.22f, 0.14f));
                        break;
                    case WeaponKind.SniperRifle:
                        g.Mag = P(root.transform, PrimitiveType.Cube, "Mag", new Vector3(0f, -0.09f, len * 0.36f), new Vector3(th * 0.8f, 0.07f, 0.09f), dark);
                        g.Bolt = P(root.transform, PrimitiveType.Cube, "Bolt", new Vector3(th * 0.75f, 0.03f, len * 0.3f), new Vector3(0.05f, 0.02f, 0.02f), light);
                        P(root.transform, PrimitiveType.Cube, "Stock", new Vector3(0f, -0.02f, -0.12f), new Vector3(th * 0.9f, 0.11f, 0.24f), dark);
                        break;
                    default:   // subfusil y fusil de asalto
                        g.Mag = P(root.transform, PrimitiveType.Cube, "Mag", new Vector3(0f, -0.13f, len * 0.42f), new Vector3(th * 0.75f, 0.16f, 0.06f), dark, -12f);
                        g.Slide = P(root.transform, PrimitiveType.Cube, "ChargingHandle", new Vector3(th * 0.6f, 0.03f, len * 0.38f), new Vector3(0.04f, 0.02f, 0.03f), light);
                        if (d.kind == WeaponKind.AssaultRifle) P(root.transform, PrimitiveType.Cube, "Stock", new Vector3(0f, -0.02f, -0.11f), new Vector3(th * 0.85f, 0.1f, 0.2f), dark);
                        break;
                }
            }
            g.Tip = new GameObject("Tip").transform; g.Tip.SetParent(root.transform, false); g.Tip.localPosition = new Vector3(0f, 0f, len);
            g.CasingPoint = new GameObject("CasingPoint").transform; g.CasingPoint.SetParent(root.transform, false); g.CasingPoint.localPosition = new Vector3(th * 0.6f, 0.04f, len * 0.35f);
            if (d.IsMelee)
            {
                g.Trail = g.Tip.gameObject.AddComponent<TrailRenderer>();
                g.Trail.time = 0.12f; g.Trail.startWidth = 0.08f; g.Trail.endWidth = 0f; g.Trail.minVertexDistance = 0.03f;
                g.Trail.sharedMaterial = ParticleMaterials.SoftAdditive;
                g.Trail.startColor = new Color(1f, 0.85f, 0.6f, 0.5f); g.Trail.endColor = new Color(1f, 0.6f, 0.3f, 0f);
                g.Trail.emitting = false;
                g.Trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (g.Mag != null) { g._magRest = g.Mag.localPosition; g._magRenderer = g.Mag.GetComponent<Renderer>(); }
            if (g.Slide != null) g._slideRest = g.Slide.localPosition;
            if (g.Pump != null) g._pumpRest = g.Pump.localPosition;
            if (g.Bolt != null) { g._boltRest = g.Bolt.localPosition; g._boltRotRest = g.Bolt.localRotation; }
            if (g.Cylinder != null) g._cylRest = g.Cylinder.localRotation;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            return g;
        }

        private static Transform P(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color, float pitch = 0f)
        {
            var go = Prim.Create(type, name, pos, scale, Prim.GetMaterial(color), parent, false);
            if (pitch != 0f) go.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            go.GetComponent<Renderer>().shadowCastingMode = scale.magnitude < 0.12f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            return go.transform;
        }

        /// <summary>Corredera / palanca de carga: 0 en su sitio, 1 atrás del todo.</summary>
        public void SetSlide(float t) { if (Slide != null) Slide.localPosition = _slideRest + Vector3.back * 0.035f * Mathf.Clamp01(t); }
        /// <summary>Bomba de la escopeta: 0 delante, 1 atrás.</summary>
        public void SetPump(float t) { if (Pump != null) Pump.localPosition = _pumpRest + Vector3.back * 0.09f * Mathf.Clamp01(t); }
        /// <summary>Cerrojo: 0 cerrado, 1 abierto (gira hacia arriba y retrocede).</summary>
        public void SetBolt(float t)
        {
            if (Bolt == null) return;
            float u = Mathf.Clamp01(t);
            Bolt.localRotation = _boltRotRest * Quaternion.Euler(0f, 0f, 80f * Mathf.Clamp01(u * 2f));
            Bolt.localPosition = _boltRest + Vector3.back * 0.07f * Mathf.Clamp01(u * 2f - 1f);
        }
        /// <summary>Tambor del revólver: 0 cerrado, 1 abierto a un lado.</summary>
        public void SetCylinder(float t) { if (Cylinder != null) Cylinder.localRotation = Quaternion.Euler(0f, 0f, -70f * Mathf.Clamp01(t)) * _cylRest; }
        /// <summary>Cargador: desplazamiento hacia abajo (m) y si se ve.</summary>
        public void SetMag(float down, bool visible)
        {
            if (Mag == null) return;
            Mag.localPosition = _magRest + Vector3.down * down;
            if (_magRenderer != null) _magRenderer.enabled = visible;
        }
        public Vector3 MagWorldRest => Mag != null ? transform.TransformPoint(_magRest) : transform.position;
        public void SetTrail(bool on) { if (Trail != null) Trail.emitting = on; }
    }
}
