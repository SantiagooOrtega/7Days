using UnityEngine;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>Marcas viales gastadas sobre el asfalto (calcomanías fusionadas): línea amarilla discontinua, pasos de cebra y flechas casi borradas.</summary>
    public static class RoadMarkings
    {
        public static void Build(Transform root, LookSettings look)
        {
            if (!look.Signs.RoadMarkings) return;
            var parent = new GameObject("RoadMarkings").transform;
            parent.SetParent(root, false);
            const float y = 0.05f; // sobre el asfalto (cuya superficie está en y=0.03)

            var dash = new MeshBatch();
            for (int i = -6; i <= 6; i++) dash.AddFlatQuad(new Vector3(0f, y, i * 4.4f), 0f, 0.5f, 2.6f);
            Spawn(dash, "Center Line", parent, DecalKind.DashLine);

            var zebra = new MeshBatch();
            zebra.AddFlatQuad(new Vector3(0f, y + 0.002f, -22f), 0f, 7.6f, 3.2f);
            zebra.AddFlatQuad(new Vector3(0f, y + 0.002f, -1.5f), 0f, 7.6f, 3.2f);
            Spawn(zebra, "Crosswalks", parent, DecalKind.Zebra);

            var arrows = new MeshBatch();
            arrows.AddFlatQuad(new Vector3(-2f, y + 0.004f, -18f), 0f, 1.6f, 3.6f);
            arrows.AddFlatQuad(new Vector3(2f, y + 0.004f, 21f), 180f, 1.6f, 3.6f);
            arrows.AddFlatQuad(new Vector3(-2f, y + 0.004f, 11.5f), 4f, 1.6f, 3.6f);
            Spawn(arrows, "Arrows", parent, DecalKind.Arrow);
        }

        private static void Spawn(MeshBatch b, string name, Transform parent, DecalKind kind)
        {
            var go = b.Spawn(name, parent, Prim.GetDecal(DecalLibrary.Get(kind), Color.white, 0.12f, kind.ToString()));
            go.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;
            AtmosphereBudget.RegisterGroup(go, 4); // las marcas viales nunca se recortan
        }
    }
}
