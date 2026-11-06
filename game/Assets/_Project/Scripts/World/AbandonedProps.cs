using UnityEngine;
using SevenDays.Core;
using SevenDays.Look;

namespace SevenDays.World
{
    /// <summary>Props de abandono hechos con primitivas: coche destrozado, poste caído y cartel roto.</summary>
    public static class AbandonedProps
    {
        private static readonly Color[] CarColors =
        {
            new Color(0.5f, 0.2f, 0.15f), new Color(0.22f, 0.32f, 0.42f), new Color(0.55f, 0.54f, 0.5f),
        };

        /// <summary>
        /// Coche destrozado (4.2 x 1.8 m): carrocería oxidada, cabina con cristales rotos, capó o maletero abierto y ruedas que
        /// faltan. Es cobertura (y se podrá saltar: ver Vaultable en la Fase 2.5/bloque 5). El NavMesh lo trata como obstáculo.
        /// </summary>
        public static GameObject Car(Transform parent, Vector3 pos, float yaw, int variant)
        {
            Color color = CarColors[Mathf.Abs(variant) % CarColors.Length];
            var root = new GameObject("Car Wreck " + variant);
            root.transform.SetParent(parent, false);
            // inclinado: le falta una rueda y está hundido
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(1.5f, yaw, variant == 1 ? 4f : -2f));

            var paint = Prim.GetTextured(color, SurfaceLibrary.Metal, Vector2.one, 0.3f, 0.3f, true, variant == 2 ? 1f : 0.9f);
            var dark = Prim.GetMaterial(new Color(0.05f, 0.05f, 0.06f));
            var glass = Prim.GetMaterial(new Color(0.08f, 0.1f, 0.12f));

            Prim.CreateBox("Body", new Vector3(0f, 0.55f, 0f), new Vector3(1.8f, 0.6f, 4.2f), paint, root.transform, 2f, default, false, 0.06f);
            Prim.CreateBox("Cabin", new Vector3(0f, 1.12f, -0.25f), new Vector3(1.6f, 0.55f, 2.0f), paint, root.transform, 2f, default, false, 0.06f);
            // cristales (parabrisas, luneta y laterales), algunos rotos = huecos oscuros
            Prim.CreateBox("Windshield", new Vector3(0f, 1.14f, 0.77f), new Vector3(1.45f, 0.42f, 0.04f), glass, root.transform, 1f, default, false, 0.01f);
            Prim.CreateBox("Rear Glass", new Vector3(0f, 1.14f, -1.27f), new Vector3(1.45f, 0.42f, 0.04f), glass, root.transform, 1f, default, false, 0.01f);
            Prim.CreateBox("Side Glass L", new Vector3(-0.82f, 1.14f, -0.25f), new Vector3(0.04f, 0.4f, 1.6f), glass, root.transform, 1f, default, false, 0.01f);
            Prim.CreateBox("Side Glass R", new Vector3(0.82f, 1.14f, -0.25f), new Vector3(0.04f, 0.4f, 1.6f), glass, root.transform, 1f, default, false, 0.01f);

            // capó levantado (variante 1) o maletero abierto (variante 2)
            if (variant == 1)
            {
                var hood = Prim.CreateBox("Hood", new Vector3(0f, 1.05f, 1.9f), new Vector3(1.7f, 0.08f, 1.3f), paint, root.transform, 2f, default, false, 0.02f);
                hood.transform.localRotation = Quaternion.Euler(-48f, 0f, 0f);
            }
            else if (variant == 2)
            {
                var lid = Prim.CreateBox("Trunk Lid", new Vector3(0f, 1.0f, -2.0f), new Vector3(1.6f, 0.08f, 0.9f), paint, root.transform, 2f, default, false, 0.02f);
                lid.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            }

            // ruedas: faltan una o dos
            for (int i = 0; i < 4; i++)
            {
                if ((variant + i) % 4 == 0 || (variant == 2 && i == 3)) continue;
                float x = (i % 2 == 0 ? -1f : 1f) * 0.88f, z = i < 2 ? 1.3f : -1.3f;
                var w = Prim.Create(PrimitiveType.Cylinder, "Wheel", new Vector3(x, 0.32f, z), new Vector3(0.64f, 0.12f, 0.64f), dark, root.transform, false);
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            // faros rotos
            var lamp = Prim.GetMaterial(new Color(0.35f, 0.33f, 0.28f));
            for (int s = -1; s <= 1; s += 2)
                Prim.CreateBox("Headlight", new Vector3(s * 0.6f, 0.62f, 2.12f), new Vector3(0.3f, 0.16f, 0.06f), lamp, root.transform, 1f, default, false, 0.01f);

            // collider único de toda la carrocería: cobertura y obstáculo del NavMesh
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(1.9f, 1.5f, 4.3f);
            col.center = new Vector3(0f, 0.75f, 0f);
            root.AddComponent<CoverObject>();
            Vegetation.MarkObstacle(root);
            return root;
        }

        /// <summary>Poste de luz caído con su brazo (obstáculo pequeño).</summary>
        public static GameObject FallenPole(Transform parent, Vector3 pos, float yaw, float scale)
        {
            var root = new GameObject("Fallen Pole");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var metal = Prim.GetTextured(new Color(0.36f, 0.38f, 0.4f), SurfaceLibrary.Metal, Vector2.one, 0.3f, 0.4f, true);
            float len = 6f * scale;

            var pole = Prim.Create(PrimitiveType.Cylinder, "Pole", new Vector3(0f, 0.16f, 0f), new Vector3(0.22f, len / 2f, 0.22f), metal, root.transform, true);
            pole.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim.CreateBox("Arm", new Vector3(len / 2f - 0.2f, 0.18f, 0.6f), new Vector3(0.1f, 0.1f, 1.3f), metal, root.transform, 1f, default, false, 0.01f);
            Prim.Create(PrimitiveType.Sphere, "Dead Bulb", new Vector3(len / 2f - 0.2f, 0.17f, 1.25f), Vector3.one * 0.25f, Prim.GetMaterial(new Color(0.3f, 0.3f, 0.28f)), root.transform, false);
            Vegetation.MarkObstacle(pole);
            return root;
        }

        /// <summary>Cartel genérico roto: poste inclinado y una placa colgando de un tornillo.</summary>
        public static GameObject BrokenSign(Transform parent, Vector3 pos, float yaw, int variant)
        {
            var root = new GameObject("Broken Sign");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var metal = Prim.GetTextured(new Color(0.34f, 0.36f, 0.38f), SurfaceLibrary.Metal, Vector2.one, 0.3f, 0.4f, true);
            var pole = Prim.Create(PrimitiveType.Cylinder, "Pole", new Vector3(0f, 1.05f, 0f), new Vector3(0.08f, 1.05f, 0.08f), metal, root.transform, false);
            pole.transform.localRotation = Quaternion.Euler(0f, 0f, 9f);
            var plateMat = Prim.GetTextured(new Color(0.25f, 0.4f, 0.48f), SurfaceLibrary.Metal, Vector2.one, 0.3f, 0.3f, true, 0.9f);
            var plate = Prim.CreateBox("Plate", new Vector3(0.12f, 1.95f, 0f), new Vector3(0.7f, 0.5f, 0.03f), plateMat, root.transform, 1f, default, false, 0.01f);
            plate.transform.localRotation = Quaternion.Euler(0f, 0f, -38f); // colgando de un tornillo
            return root;
        }
    }
}
