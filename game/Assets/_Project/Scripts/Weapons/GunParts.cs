using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.Weapons
{
    /// <summary>
    /// Arma armada por piezas separadas con mallas detalladas (WeaponModel: cuerpo, cañón, mira, cargador, empuñadura, guardamano, culata, guardamonte,
    /// serie y acento de rareza) sobre un atlas compartido de materiales (CharacterArt). Las piezas que se animan conservan nombre y reposo: la corredera
    /// retrocede al disparar, la bomba y el cerrojo ciclan, el cargador sale y entra al recargar y el tambor se abre. Puntos: Tip (boca), CasingPoint
    /// (expulsión) y Sight (punto de mira real: centro de la óptica o línea de alza y guion).
    /// </summary>
    public class GunParts : MonoBehaviour
    {
        public Transform Body, Mag, Slide, Pump, Cylinder, Bolt, Tip, CasingPoint, Sight;
        public TrailRenderer Trail;
        public bool HasOptic;
        public int Triangles;
        private Vector3 _magRest, _slideRest, _pumpRest, _boltRest;
        private Quaternion _cylRest, _boltRotRest;
        private Renderer[] _magRenderers;

        private static readonly Dictionary<string, WeaponModelData> Models = new Dictionary<string, WeaponModelData>();
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        public static GunParts Build(WeaponData d, Transform parent)
        {
            var root = new GameObject("Gun");
            root.transform.SetParent(parent, false);
            var g = root.AddComponent<GunParts>();
            string key = d.id + "|" + d.kind + "|" + d.length.ToString("0.###") + "|" + d.thickness.ToString("0.###");
            if (!Models.TryGetValue(key, out var model)) { model = WeaponModel.Build(d); Models[key] = model; }
            Material atlas = SevenDays.Animation.CharacterArt.WeaponMaterial, accent = SevenDays.Animation.CharacterArt.Accent(d.rarity);
            Material dot = Prim.GetEmissive(new Color(1f, 0.12f, 0.05f), 6f);
            var made = new Dictionary<string, Transform>();
            foreach (var part in model.Parts)
            {
                var go = new GameObject(part.Name);
                Transform parentT = part.Parent != null && made.TryGetValue(part.Parent, out var pt) ? pt : root.transform;
                go.transform.SetParent(parentT, false);
                go.transform.localPosition = new Vector3(part.Position.X, part.Position.Y, part.Position.Z);
                go.transform.localRotation = Quaternion.Euler(part.Euler.X, part.Euler.Y, part.Euler.Z);
                string mk = key + "|" + part.Name;
                if (!Meshes.TryGetValue(mk, out var mesh) || mesh == null) { mesh = BoxMesh.ToMesh(part.Mesh); mesh.name = d.id + " " + part.Name; Meshes[mk] = mesh; }
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = part.Slot == WeaponModel.SlotAccent ? accent : part.Slot == WeaponModel.SlotDot ? dot : atlas;
                r.shadowCastingMode = mesh.bounds.size.magnitude < 0.12f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                made[part.Name] = go.transform;
            }
            made.TryGetValue("Body", out g.Body); made.TryGetValue("Mag", out g.Mag); made.TryGetValue("Slide", out g.Slide);
            made.TryGetValue("Pump", out g.Pump); made.TryGetValue("Cylinder", out g.Cylinder); made.TryGetValue("Bolt", out g.Bolt);
            g.Triangles = model.Triangles;
            g.HasOptic = model.HasOptic;
            float len = d.length, th = d.thickness;
            g.Tip = new GameObject("Tip").transform; g.Tip.SetParent(root.transform, false); g.Tip.localPosition = new Vector3(0f, 0f, len);
            g.CasingPoint = new GameObject("CasingPoint").transform; g.CasingPoint.SetParent(root.transform, false); g.CasingPoint.localPosition = new Vector3(th * 0.6f, 0.04f, len * 0.35f);
            if (!d.IsMelee)
            {
                g.Sight = new GameObject("Sight").transform; g.Sight.SetParent(root.transform, false);
                g.Sight.localPosition = new Vector3(model.Sight.X, model.Sight.Y, model.Sight.Z);
            }
            else
            {
                g.Trail = g.Tip.gameObject.AddComponent<TrailRenderer>();
                g.Trail.time = 0.12f; g.Trail.startWidth = 0.08f; g.Trail.endWidth = 0f; g.Trail.minVertexDistance = 0.03f;
                g.Trail.sharedMaterial = ParticleMaterials.SoftAdditive;
                g.Trail.startColor = new Color(1f, 0.85f, 0.6f, 0.5f); g.Trail.endColor = new Color(1f, 0.6f, 0.3f, 0f);
                g.Trail.emitting = false;
                g.Trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (g.Mag != null) { g._magRest = g.Mag.localPosition; g._magRenderers = g.Mag.GetComponentsInChildren<Renderer>(); }
            if (g.Slide != null) g._slideRest = g.Slide.localPosition;
            if (g.Pump != null) g._pumpRest = g.Pump.localPosition;
            if (g.Bolt != null) { g._boltRest = g.Bolt.localPosition; g._boltRotRest = g.Bolt.localRotation; }
            if (g.Cylinder != null) g._cylRest = g.Cylinder.localRotation;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            return g;
        }

        /// <summary>Tamaño real de una pieza en el mundo (caja de su malla por su escala): las piezas ya no son cubos escalados.</summary>
        public static Vector3 SizeOf(Transform t)
        {
            var mf = t != null ? t.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) return t != null ? t.lossyScale : Vector3.zero;
            return Vector3.Scale(mf.sharedMesh.bounds.size, t.lossyScale);
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
            if (_magRenderers != null) foreach (var r in _magRenderers) if (r != null) r.enabled = visible;
        }
        public Vector3 MagWorldRest => Mag != null ? transform.TransformPoint(_magRest) : transform.position;
        public void SetTrail(bool on) { if (Trail != null) Trail.emitting = on; }
    }
}
