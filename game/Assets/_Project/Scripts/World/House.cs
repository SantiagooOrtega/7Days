using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Construye la casa a partir del plano de HousePlanner, con las mismas mallas y texturas del mapa: muros con bisel y UV en metros,
    /// techo con grosor real (sin backface culling), marcos, muebles que son CoverObject y Vaultable, colliders de caja en todo. Marca el
    /// NavMesh: muros y marcos "no caminables", techo ignorado, puertas como volúmenes transitables y la ventana grande con un enlace.
    /// </summary>
    public static class House
    {
        public static HouseController Build(Transform root, LookSettings look, HouseLayoutData layout)
        {
            var valid = HouseValidator.Validate(layout);
            if (!valid.Ok)
            {
                Debug.LogWarning("[7Days] Casa inválida, se usa la de por defecto: " + string.Join("; ", valid.Errors));
                layout = HouseLayoutData.Default();
            }
            var plan = HousePlanner.Plan(layout);
            var rng = new System.Random(layout.roofSeed + 17);
            float R() => (float)rng.NextDouble();

            var houseGo = new GameObject("House");
            houseGo.transform.SetParent(root, false);
            houseGo.transform.SetPositionAndRotation(new Vector3(layout.x, 0f, layout.z), Quaternion.Euler(0f, layout.yaw, 0f));
            var controller = houseGo.AddComponent<HouseController>();

            foreach (var b in plan.Boxes)
            {
                var size = new Vector3(b.Size.X, b.Size.Y, b.Size.Z);
                var go = Prim.CreateBox(Name(b), new Vector3(b.Center.X, b.Center.Y, b.Center.Z), size, MaterialFor(b, size, R), houseGo.transform,
                    TileFor(b, size), b.Kind == HouseBoxKind.Furniture && b.Tag == "furniture:crate" ? default : new Vector2(R(), R()), b.Collider, b.Kind == HouseBoxKind.Roof ? 0.03f : 0.02f);
                go.transform.localRotation = Quaternion.Euler(b.TiltX, b.Yaw, b.TiltZ);

                if (b.Cover) go.AddComponent<CoverObject>(); // trae su Vaultable (la altura decide si se puede saltar)
                if (b.Nav == NavFlag.NotWalkable) Vegetation.MarkObstacle(go);
                else if (b.Nav == NavFlag.Ignore) go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                if (b.Kind == HouseBoxKind.Barricade) controller.RegisterBarricade(b.OpeningId, go);
            }

            foreach (var info in plan.Openings) AddNavigation(houseGo.transform, info, look, controller);
            foreach (var t in plan.TorchPositions)
            {
                var local = new Vector3(t.X, t.Y, t.Z);
                var world = houseGo.transform.TransformPoint(local);
                controller.TorchPoints.Add(world);
                // soporte de la antorcha (palo oscuro inclinado) y su fuego, que sale del pool
                var stick = Prim.Create(PrimitiveType.Cylinder, "Torch", local + Vector3.down * 0.15f, new Vector3(0.05f, 0.22f, 0.05f), Prim.GetMaterial(new Color(0.2f, 0.14f, 0.1f)), houseGo.transform, false);
                stick.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
                FireManager.Instance?.Spawn(FireKind.Torch, world + Vector3.up * 0.18f);
            }
            controller.Initialize(layout, plan);
            controller.SetDay(1);
            return controller;
        }

        // ---------------------------------------------------------------- materiales
        private static string Name(HouseBox b) => b.Kind + (string.IsNullOrEmpty(b.Tag) ? "" : " " + b.Tag);

        private static float TileFor(HouseBox b, Vector3 size)
        {
            if (b.Kind == HouseBoxKind.Furniture && b.Tag == "furniture:crate") return size.x;
            switch (b.Kind)
            {
                case HouseBoxKind.Wall: return 2.2f;
                case HouseBoxKind.Roof: return 3f;
                case HouseBoxKind.Floor: return 2f;
                default: return 1.2f;
            }
        }

        private static Material MaterialFor(HouseBox b, Vector3 size, System.Func<float> r)
        {
            float tone = Mathf.Round((0.92f + r() * 0.16f) * 20f) / 20f;
            switch (b.Kind)
            {
                case HouseBoxKind.Wall:
                    return Prim.GetTextured(new Color(0.46f, 0.34f, 0.28f), SurfaceLibrary.Wall, Vector2.one, 0.1f, 0f, false, tone);
                case HouseBoxKind.Frame:
                    return Prim.GetTextured(new Color(0.22f, 0.16f, 0.12f), SurfaceLibrary.Bark, Vector2.one, 0.1f, 0f, true, tone);
                case HouseBoxKind.Floor:
                    return Prim.GetTextured(new Color(0.4f, 0.38f, 0.35f), SurfaceLibrary.Concrete, Vector2.one, 0.12f, 0f, true, 1f);
                case HouseBoxKind.Roof:
                    return Prim.GetTextured(new Color(0.34f, 0.33f, 0.32f), SurfaceLibrary.Concrete, Vector2.one, 0.1f, 0f, true, tone);
                case HouseBoxKind.Beam:
                    return Prim.GetTextured(new Color(0.3f, 0.22f, 0.16f), SurfaceLibrary.Bark, Vector2.one, 0.1f, 0f, true, tone);
                case HouseBoxKind.Barricade:
                    return Prim.GetTextured(new Color(0.42f, 0.3f, 0.18f), SurfaceLibrary.CrateWood, Vector2.one, 0.1f, 0f, false, tone);
                default:
                    switch (b.Tag)
                    {
                        case "furniture:crate":
                            return Prim.GetTextured(new Color(0.42f, 0.3f, 0.18f), SurfaceLibrary.CrateWood, Vector2.one, 0.1f, 0f, false, tone);
                        case "furniture:sofa":
                            return Prim.GetMaterial(new Color(0.3f * tone, 0.2f * tone, 0.18f * tone));
                        case "furniture:bed":
                            return Prim.GetMaterial(new Color(0.36f * tone, 0.36f * tone, 0.34f * tone));
                        default:
                            return Prim.GetTextured(new Color(0.34f, 0.24f, 0.17f), SurfaceLibrary.Bark, Vector2.one, 0.1f, 0f, true, tone);
                    }
            }
        }

        // ---------------------------------------------------------------- NavMesh
        private static void AddNavigation(Transform house, OpeningInfo info, LookSettings look, HouseController controller)
        {
            var o = info.Opening;
            var center = new Vector3(info.Center.X, info.Center.Y, info.Center.Z);
            var normal = new Vector3(info.Normal.X, info.Normal.Y, info.Normal.Z);

            if (o.kind == "door")
            {
                // puerta: volumen "Walkable" que tiene prioridad sobre el marco y los muros "Not Walkable" vecinos
                var go = new GameObject("Walkable " + o.id);
                go.transform.SetParent(house, false);
                go.transform.localPosition = new Vector3(center.x, 1.2f, center.z);
                var vol = go.AddComponent<NavMeshModifierVolume>();
                bool alongX = Mathf.Abs(normal.z) > 0.5f;
                vol.size = alongX ? new Vector3(o.width, 2.4f, info.Thickness + 1.0f) : new Vector3(info.Thickness + 1.0f, 2.4f, o.width);
                vol.center = Vector3.zero;
                vol.area = 0; // Walkable
                return;
            }

            if (o.kind == "window_large" && look.House.WindowsWalkable)
            {
                // ventana grande: el antepecho es un obstáculo para el NavMesh, así que se tiende un enlace de adentro hacia afuera
                var go = new GameObject("Link " + o.id);
                go.transform.SetParent(house, false);
                go.transform.localPosition = new Vector3(center.x, 0f, center.z);
                var link = go.AddComponent<NavMeshLink>();
                var inward = -normal * 0.9f;
                var outward = normal * 0.9f;
                link.startPoint = new Vector3(inward.x, HousePlanner.FloorThickness, inward.z);
                link.endPoint = new Vector3(outward.x, 0f, outward.z);
                link.width = o.width;
                link.bidirectional = true;
                link.area = 0;
                link.autoUpdate = false;
                controller.RegisterLink(o.id, link);
            }
        }
    }
}
