using System.Collections.Generic;
using UnityEngine;

namespace SevenDays.World
{
    /// <summary>
    /// Auditoría de piezas flotantes (una vez al cargar el mapa, en el editor y en builds de desarrollo): avisa de todo renderer del nivel cuya base está a más
    /// de 0.5 m del suelo y que no tiene nada VISIBLE debajo (otra pieza dibujada justo bajo su base, o un collider con renderer activo; los muros del borde
    /// ocultos por la trinchera no cuentan). Excepciones intencionadas: luces (farolas, focos, linternas, balizas), árboles y follaje, la estructura de las casas
    /// (techos, vigas, balcones, dinteles), las ventanas iluminadas del fondo, el cielo y los objetos de botín.
    /// </summary>
    public static class FloatingAudit
    {
        public const float MinHeight = 0.5f, SupportGap = 0.5f;
        public static readonly string[] Intentional = { "Lamp", "Lantern", "Foco", "Lente", "Baliza", "Fachada", "Halo", "Torch", "Bulb", "Light", "Sign",
            "Tree", "Pine", "Canopy", "Crown", "Leaves", "Leaf", "Branch", "Hoja", "Foliage", "Night Sky", "Estrella", "Luna", "Loot", "Marcador", "Glint" };

        private struct Box { public Renderer R; public Transform T; public Bounds Local; public Bounds World; }

        public static List<string> Run(Transform level, bool log = true)
        {
            var boxes = new List<Box>();
            foreach (var r in level.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                var mf = r.GetComponent<MeshFilter>();
                var local = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(r.transform.InverseTransformPoint(r.bounds.center), r.bounds.size);
                boxes.Add(new Box { R = r, T = r.transform, Local = local, World = r.bounds });
            }
            var bad = new List<string>();
            var pts = new Vector3[5];
            foreach (var b in boxes)
            {
                if (b.World.min.y < MinHeight || b.World.size.y > 60f || IsIntentional(b.T, level)) continue;
                // cinco puntos de la base (centro y esquinas al 95 %) en los ejes de la propia pieza
                var l = b.Local; float ex = l.extents.x * 0.95f, ez = l.extents.z * 0.95f;
                pts[0] = b.T.TransformPoint(new Vector3(l.center.x, l.min.y, l.center.z));
                pts[1] = b.T.TransformPoint(new Vector3(l.center.x + ex, l.min.y, l.center.z + ez));
                pts[2] = b.T.TransformPoint(new Vector3(l.center.x - ex, l.min.y, l.center.z + ez));
                pts[3] = b.T.TransformPoint(new Vector3(l.center.x + ex, l.min.y, l.center.z - ez));
                pts[4] = b.T.TransformPoint(new Vector3(l.center.x - ex, l.min.y, l.center.z - ez));
                bool ok = false;
                for (int i = 0; i < pts.Length && !ok; i++) ok = Supported(pts[i], b, boxes);
                if (!ok) bad.Add(Path(b.T, level) + " (base a " + b.World.min.y.ToString("0.0") + " m en " + b.World.center.ToString("0.0") + ")");
            }
            if (log)
            {
                foreach (var s in bad) Debug.LogWarning("[7Days] Pieza flotante sin soporte: " + s);
                Debug.Log("[7Days] Auditoría de piezas flotantes: " + bad.Count + " sin soporte.");
            }
            return bad;
        }

        private static bool Supported(Vector3 p, Box self, List<Box> boxes)
        {
            // collider visible debajo (suelo, muros, cajas con su malla)
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 0.05f, Vector3.down, SupportGap + 0.05f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform == self.T) continue;
                if (h.collider is TerrainCollider) return true;
                var r = h.collider.GetComponent<Renderer>();
                if (r != null && r.enabled) return true;
            }
            // otra pieza dibujada justo debajo (caja orientada)
            for (float d = 0.03f; d <= SupportGap; d += 0.12f)
            {
                var q = p + Vector3.down * d;
                foreach (var o in boxes)
                {
                    if (o.T == self.T || !o.World.Contains(q)) continue;
                    var lq = o.T.InverseTransformPoint(q);
                    var e = o.Local; e.Expand(0.02f);
                    if (e.Contains(lq)) return true;
                }
            }
            return false;
        }

        public static bool IsIntentional(Transform t, Transform level)
        {
            for (var x = t; x != null && x != level; x = x.parent)
            {
                if (x.GetComponent<HouseController>() != null || x.name == "House" || x.name.StartsWith("House ")) return true;   // techos, vigas, balcones de las casas
                foreach (var w in Intentional) if (x.name.Contains(w)) return true;
            }
            return false;
        }

        private static string Path(Transform t, Transform level) { string s = t.name; for (var x = t.parent; x != null && x != level; x = x.parent) s = x.name + "/" + s; return s; }
    }
}
