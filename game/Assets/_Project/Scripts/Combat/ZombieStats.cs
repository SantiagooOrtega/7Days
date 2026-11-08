using UnityEngine;
using SevenDays.Animation;

namespace SevenDays.Combat
{
    /// <summary>Resumen de los zombis vivos para el panel F6: cuántos hay, reparto de LOD, CPU de su animación, triángulos dibujados, heridas visibles y memoria de texturas y mallas.</summary>
    public struct ZombieStatsSnapshot
    {
        public int Active, Lod0, Lod1, Lod2, Legacy, Decals, Frozen;
        public long Triangles;
        public float AnimCpuMs, RigMs, TextureMb, MeshMb;
    }

    public static class ZombieStats
    {
        public static ZombieStatsSnapshot Collect()
        {
            var s = new ZombieStatsSnapshot();
            var mgr = ZombieManager.Instance;
            if (mgr == null) return s;
            foreach (var z in mgr.All)
            {
                if (!z.InUse) continue;
                s.Active++;
                if (!(z.Rig is ZombieBody b)) { s.Legacy++; continue; }
                if (b.Lod == 0) s.Lod0++; else if (b.Lod == 1) s.Lod1++; else s.Lod2++;
                s.Decals += b.ActiveDecals;
                var r = b.Renderer;
                if (r != null && r.enabled && r.sharedMesh != null) s.Triangles += (r.sharedMesh.GetIndexCount(0) + r.sharedMesh.GetIndexCount(1)) / 3;
                if (b.Rig.UpdateInterval > 1) s.Frozen++;
            }
            foreach (var rig in ProceduralRig.Active) s.RigMs += rig.AverageCostMs / Mathf.Max(1, rig.UpdateInterval);
            s.AnimCpuMs = ZombieBody.AnimateMsLastFrame;
            s.TextureMb = ZombieArt.TextureBytes / 1048576f; s.MeshMb = ZombieMeshes.MeshBytes / 1048576f;
            return s;
        }

        public static string Describe(in ZombieStatsSnapshot s)
            => s.Active == 0 ? "sin zombis" : s.Active + " activos (LOD 0/1/2: " + s.Lod0 + "/" + s.Lod1 + "/" + s.Lod2 + (s.Legacy > 0 ? ", jefes " + s.Legacy : "") + ")  animación " + s.AnimCpuMs.ToString("0.00") + " ms CPU (rigs " + s.RigMs.ToString("0.00") + ")  "
               + s.Triangles / 1000 + "k tri  heridas " + s.Decals + "  texturas " + s.TextureMb.ToString("0.0") + " MB  mallas " + s.MeshMb.ToString("0.0") + " MB";
    }
}
