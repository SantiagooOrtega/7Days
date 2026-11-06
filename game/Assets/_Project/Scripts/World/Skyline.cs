using UnityEngine;
using UnityEngine.Rendering;
using SevenDays.Core;
using SevenDays.Look;

namespace SevenDays.World
{
    /// <summary>
    /// Siluetas de edificios en ruinas fuera del mapa (sin colisión ni sombras): rompen el horizonte vacío y la
    /// niebla las funde con el cielo. Alturas desiguales y azoteas rotas.
    /// </summary>
    public static class Skyline
    {
        public static void Build(Transform root, LookSettings look)
        {
            int n = look.Atmosphere.Scaled(look.Atmosphere.SkylineBuildings);
            if (n <= 0) return;
            var parent = new GameObject("Skyline").transform;
            parent.SetParent(root, false);
            var rng = new System.Random(7);
            float R() => (float)rng.NextDouble();

            for (int i = 0; i < n; i++)
            {
                float ang = (i + R() * 0.7f) / n * Mathf.PI * 2f;
                float dist = 50f + R() * 28f;
                Vector3 c = new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                float w = 8f + R() * 10f, d = 8f + R() * 8f, h = 12f + R() * 28f;
                float tone = Mathf.Round((0.9f + R() * 0.25f) * 20f) / 20f;
                var mat = Prim.GetTextured(new Color(0.34f, 0.35f, 0.38f), SurfaceLibrary.Concrete, Vector2.one, 0.05f, 0f, true, tone);
                float yaw = Mathf.Atan2(c.x, c.z) * Mathf.Rad2Deg + (R() - 0.5f) * 40f;

                // cuerpo + azotea rota con 1-2 bloques más bajos o desplazados
                AddBox(parent, "Building " + i, c + Vector3.up * (h / 2f), new Vector3(w, h, d), yaw, mat);
                int extra = 1 + (int)(R() * 2f);
                for (int k = 0; k < extra; k++)
                {
                    float eh = 2f + R() * 7f;
                    var off = new Vector3((R() - 0.5f) * w * 0.7f, 0f, (R() - 0.5f) * d * 0.7f);
                    var size = new Vector3(w * (0.3f + R() * 0.3f), eh, d * (0.3f + R() * 0.3f));
                    AddBox(parent, "Ruin Top " + i + "_" + k, c + off + Vector3.up * (h + eh / 2f - 0.5f), size, yaw + (R() - 0.5f) * 25f, mat);
                }
            }
        }

        private static void AddBox(Transform parent, string name, Vector3 pos, Vector3 size, float yaw, Material mat)
        {
            var go = Prim.CreateBox(name, pos, size, mat, parent, 6f, new Vector2(Random.value, Random.value), false, 0.2f);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; // fuera de las sombras: ahorra GPU
        }
    }
}
