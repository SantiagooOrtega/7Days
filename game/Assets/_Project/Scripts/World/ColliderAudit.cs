using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Auditoría de colisiones que corre una vez al terminar de construir el mapa: recorre todos los objetos cuyo nombre figura como SÓLIDO en
    /// SolidCatalog y comprueba que tengan un collider activo (en sí mismos o en un ancestro, como el casco de un coche), que ninguno use
    /// MeshCollider (deben ser Box/Capsule/Sphere) y que lo marcado como no sólido no lleve collider. El resultado va a la Console.
    /// </summary>
    public static class ColliderAudit
    {
        public static void Run(Transform root)
        {
            int solid = 0, ok = 0;
            var missing = new List<string>();
            var wrongShape = new List<string>();
            var unexpected = new List<string>();

            foreach (var t in root.GetComponentsInChildren<Transform>(false)) // solo los activos (los tablones del director empiezan apagados)
            {
                var spec = SolidCatalog.MatchPiece(t.name);
                if (spec == null) continue;
                var own = t.GetComponent<Collider>();
                var any = own != null ? own : t.GetComponentInParent<Collider>();

                if (spec.Solid)
                {
                    solid++;
                    if (any == null || !any.enabled) { missing.Add(PathOf(t)); continue; }
                    ok++;
                    if (any is MeshCollider) wrongShape.Add(PathOf(t) + " (MeshCollider)");
                }
                else if (own != null && !own.isTrigger)
                {
                    unexpected.Add(PathOf(t));
                }
            }

            foreach (var mc in root.GetComponentsInChildren<MeshCollider>(false)) wrongShape.Add(PathOf(mc.transform) + " (MeshCollider)");

            string summary = "[7Days] Auditoría de colisiones: " + ok + "/" + solid + " sólidos con collider.";
            if (missing.Count == 0 && wrongShape.Count == 0 && unexpected.Count == 0)
            {
                Debug.Log(summary + " Todo en orden (colliders simples de Box/Capsule/Sphere).");
                return;
            }
            if (missing.Count > 0) Debug.LogError(summary + " SIN collider (se atraviesan y no frenan balas): " + string.Join(", ", missing));
            if (wrongShape.Count > 0) Debug.LogError("[7Days] Colliders no simples: " + string.Join(", ", wrongShape));
            if (unexpected.Count > 0) Debug.LogWarning("[7Days] Objetos que no deberían ser sólidos y tienen collider: " + string.Join(", ", unexpected));
        }

        private static string PathOf(Transform t)
        {
            string p = t.name;
            for (var a = t.parent; a != null && a.parent != null; a = a.parent) p = a.name + "/" + p;
            return p;
        }
    }
}
