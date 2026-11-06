using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World.Loot
{
    /// <summary>
    /// Coloca los puntos de loot en el mapa: objetos sueltos en el suelo y contenedores buscables en muebles de la casa, maleteros de coches, montones de
    /// escombros, escondites entre arbustos y cajas de suministros en las zonas expuestas.
    /// </summary>
    public static class LootBuilder
    {
        public static void Build(Transform root, LootLayoutData layout, LootManager manager)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            var parent = new GameObject("Loot Points").transform;
            parent.SetParent(root, false);
            int n = 0;

            foreach (var p in layout.points)
            {
                Vector3 pos = new Vector3(p.x, 0f, p.z);
                switch (p.container)
                {
                    case "ground":
                        manager.SpawnGroundPoint(p);
                        break;
                    case "furniture":
                    {
                        var t = Nearest(all, "Furniture furniture:", pos, 1.6f);
                        if (t != null) SearchableContainer.Attach(t.gameObject, p, manager, 2.2f);
                        else Debug.LogWarning("[7Days] Punto de loot '" + p.id + "': no hay mueble cerca; se omite.");
                        break;
                    }
                    case "car":
                    {
                        var t = Nearest(all, "Car Wreck", pos, 3.5f);
                        if (t != null) SearchableContainer.Attach(t.gameObject, p, manager, 3.2f);
                        else Debug.LogWarning("[7Days] Punto de loot '" + p.id + "': no hay coche cerca; se omite.");
                        break;
                    }
                    case "rubble":
                        SearchableContainer.Attach(RubblePile(parent, pos, ++n), p, manager);
                        break;
                    case "bush":
                        SearchableContainer.Attach(Stash(parent, pos, ++n), p, manager, 1.9f); // escondido entre el follaje: solo una luz tenue lo delata
                        break;
                    default: // crate
                        SearchableContainer.Attach(SupplyCrate(parent, pos, ++n), p, manager);
                        break;
                }
            }
        }

        private static Transform Nearest(Transform[] all, string prefix, Vector3 pos, float maxDist)
        {
            Transform best = null; float bestD = maxDist;
            foreach (var t in all)
            {
                if (!t.name.StartsWith(prefix)) continue;
                var tp = t.position;
                float d = Vector2.Distance(new Vector2(tp.x, tp.z), new Vector2(pos.x, pos.z));
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        /// <summary>Caja de suministros con la textura de madera (sólida: lleva collider de caja).</summary>
        private static GameObject SupplyCrate(Transform parent, Vector3 pos, int i)
        {
            var mat = Prim.GetTextured(new Color(0.34f, 0.3f, 0.2f), SurfaceLibrary.CrateWood, Vector2.one, 0.1f, 0f, false, 0.95f);
            var go = Prim.CreateBox("Crate Supply " + i, pos + Vector3.up * 0.35f, Vector3.one * 0.7f, mat, parent, 0.7f, default, true, 0.02f);
            go.transform.rotation = Quaternion.Euler(0f, i * 37f, 0f);
            go.AddComponent<CoverObject>();
            return go;
        }

        /// <summary>Montón de escombros: losas y bloques de concreto planos, sin collider (se pisa).</summary>
        private static GameObject RubblePile(Transform parent, Vector3 pos, int i)
        {
            var root = new GameObject("Rubble Pile " + i);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            var mat = Prim.GetTextured(new Color(0.42f, 0.41f, 0.39f), SurfaceLibrary.Concrete, Vector2.one, 0.1f, 0f, true);
            for (int k = 0; k < 5; k++)
            {
                var piece = Prim.CreateBox("Rubble Part", new Vector3((k % 3 - 1) * 0.35f, 0.1f + k * 0.04f, (k / 3 - 0.5f) * 0.4f), new Vector3(0.55f - k * 0.04f, 0.2f, 0.4f), mat, root.transform, 1f, default, false, 0.02f);
                piece.transform.localRotation = Quaternion.Euler(k * 11f, k * 53f, k * 7f);
            }
            return root;
        }

        /// <summary>Escondite entre arbustos: un punto invisible; solo lo delata la luz tenue (hay que fijarse).</summary>
        private static GameObject Stash(Transform parent, Vector3 pos, int i)
        {
            var go = new GameObject("Stash " + i);
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 0.15f;
            return go;
        }
    }
}
