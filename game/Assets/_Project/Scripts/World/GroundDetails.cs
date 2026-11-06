using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Suciedad del suelo: grietas con maleza, charcos oscuros y manchas de sangre seca. Todo son calcomanías
    /// y hojas fusionadas en pocas mallas (sin colliders ni sombras), registradas en AtmosphereBudget para
    /// poder ocultarlas si el FPS baja de 55. La maleza no afecta al NavMesh.
    /// </summary>
    public static class GroundDetails
    {
        public static void Build(Transform root, LookSettings look, IList<Vector3> anchors)
        {
            var a = look.Atmosphere;
            var rng = new System.Random(99);
            float R() => (float)rng.NextDouble();
            var parent = new GameObject("GroundDetails").transform;
            parent.SetParent(root, false);

            var crackBatch = new MeshBatch();
            var puddleBatch = new MeshBatch();
            var bloodBatch = new MeshBatch();
            var weedGreen = new MeshBatch();
            var weedDry = new MeshBatch();

            // Grietas (y la maleza que sale de ellas)
            int cracks = a.Scaled(a.Cracks);
            var crackSpots = new List<(Vector3 pos, float yaw, float len)>();
            for (int i = 0; i < cracks; i++)
            {
                Vector3 p = RandomGroundPoint(R);
                float yaw = R() * 360f, len = 2.2f + R() * 2.2f;
                crackBatch.AddFlatQuad(Surface(p, 0.045f), yaw, len, len);
                crackSpots.Add((p, yaw, len));
            }

            // Charcos oscuros
            for (int i = 0; i < a.Scaled(a.Puddles); i++)
            {
                Vector3 p = RandomGroundPoint(R);
                float s = 1.3f + R() * 2.2f;
                puddleBatch.AddFlatQuad(Surface(p, 0.04f), R() * 360f, s * (0.8f + R() * 0.5f), s);
            }

            // Sangre seca: junto a barreras y cajas (donde "alguien se resguardó") y suelta por el mapa
            for (int i = 0; i < a.Scaled(a.BloodStains); i++)
            {
                Vector3 p = anchors != null && anchors.Count > 0 && R() < 0.7f
                    ? anchors[rng.Next(anchors.Count)] + new Vector3((R() - 0.5f) * 3f, 0f, (R() - 0.5f) * 3f)
                    : RandomGroundPoint(R);
                float s = 1.1f + R() * 1.6f;
                bloodBatch.AddFlatQuad(Surface(p, 0.05f), R() * 360f, s, s);
            }

            // Maleza: 60% saliendo de las grietas, el resto en los bordes de la calle y junto a coberturas/escombros
            int tufts = a.Scaled(a.WeedTufts);
            for (int i = 0; i < tufts; i++)
            {
                Vector3 p;
                if (crackSpots.Count > 0 && R() < 0.6f)
                {
                    var c = crackSpots[rng.Next(crackSpots.Count)];
                    float t = (R() - 0.5f) * c.len * 0.8f;
                    p = c.pos + Quaternion.Euler(0f, c.yaw, 0f) * new Vector3(t, 0f, (R() - 0.5f) * 0.3f);
                }
                else if (anchors != null && anchors.Count > 0 && R() < 0.6f)
                {
                    p = anchors[rng.Next(anchors.Count)] + new Vector3((R() - 0.5f) * 2.4f, 0f, (R() - 0.5f) * 2.4f);
                }
                else
                {
                    p = new Vector3((R() < 0.5f ? -1f : 1f) * (4.1f + R() * 0.4f), 0f, (R() - 0.5f) * 56f); // borde de la calle
                }
                AddTuft(R() < 0.65f ? weedGreen : weedDry, new Vector3(p.x, Mathf.Abs(p.x) < 4f ? 0.03f : 0f, p.z), R);
            }

            Spawn(crackBatch, "Cracks", parent, DecalKind.Crack, 0.1f, 4);
            Spawn(puddleBatch, "Puddles", parent, DecalKind.Puddle, 0.95f, 3);
            Spawn(bloodBatch, "Blood", parent, DecalKind.Blood, 0.2f, 3);
            SpawnWeeds(weedGreen, "Weeds Green", parent, new Color(0.30f, 0.36f, 0.16f), 3);
            SpawnWeeds(weedDry, "Weeds Dry", parent, new Color(0.45f, 0.38f, 0.2f), 3);
        }

        private static Vector3 RandomGroundPoint(System.Func<float> r) => new Vector3((r() - 0.5f) * 54f, 0f, (r() - 0.5f) * 54f);

        /// <summary>Altura de la calcomanía: sobre el asfalto (más alto) o el concreto.</summary>
        private static Vector3 Surface(Vector3 p, float onRoad) => new Vector3(p.x, Mathf.Abs(p.x) < 3.9f ? onRoad : onRoad - 0.02f, p.z);

        /// <summary>Mata de 5 hojas con distinta altura, inclinación y giro.</summary>
        private static void AddTuft(MeshBatch b, Vector3 pos, System.Func<float> r)
        {
            for (int k = 0; k < 5; k++)
            {
                float h = 0.18f + r() * 0.42f;
                b.AddBlade(pos + new Vector3((r() - 0.5f) * 0.14f, 0f, (r() - 0.5f) * 0.14f), r() * 360f, h, 0.035f + r() * 0.03f, (r() - 0.3f) * h * 0.7f);
            }
        }

        private static void Spawn(MeshBatch b, string name, Transform parent, DecalKind kind, float smoothness, int disableAtLevel)
        {
            if (b.VertexCount == 0) return;
            var tex = DecalLibrary.Get(kind);
            var go = b.Spawn(name, parent, Prim.GetDecal(tex, Color.white, smoothness, kind.ToString()));
            MarkNoNavMesh(go);
            AtmosphereBudget.RegisterGroup(go, disableAtLevel);
        }

        private static void SpawnWeeds(MeshBatch b, string name, Transform parent, Color color, int disableAtLevel)
        {
            if (b.VertexCount == 0) return;
            var go = b.Spawn(name, parent, Prim.GetMaterial(color, true)); // hojas de una capa: doble cara
            MarkNoNavMesh(go);
            AtmosphereBudget.RegisterGroup(go, disableAtLevel);
        }

        /// <summary>La maleza y las calcomanías no deben bloquear ni "hornearse" en el NavMesh (Fase 3).</summary>
        private static void MarkNoNavMesh(GameObject go)
        {
            var mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
            mod.ignoreFromBuild = true;
        }
    }
}
