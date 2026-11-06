using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using SevenDays.Core;
using SevenDays.Look;
using SevenDays.Procedural;

namespace SevenDays.World
{
    /// <summary>
    /// Señales de tráfico desgastadas con GPU instancing: poste metálico oxidado + placa con la textura procedural del tipo
    /// y de la variante de desgaste. Estados: erguida, inclinada, doblada, caída, solo poste y placa colgando de un tornillo.
    /// Los postes finos no tienen collider (no bloquean el NavMesh); una señal caída lleva un collider pequeño y se marca
    /// "no caminable". Placas y postes no proyectan sombras. Un par de señales llevan una luz roja parpadeante.
    /// </summary>
    public static class TrafficSigns
    {
        private const float PoleRadius = 0.045f;
        private const float PlateFront = PoleRadius + SignGeometry.Thickness * 0.5f + 0.003f; // la placa va pegada al frente del poste
        private static Mesh _poleMesh;
        private static readonly Dictionary<SignType, Mesh> PlateMeshes = new Dictionary<SignType, Mesh>();

        public static SignType ToType(string propType)
        {
            switch (propType)
            {
                case PropTypes.SignStop: return SignType.Stop;
                case PropTypes.SignYield: return SignType.Yield;
                case PropTypes.SignSpeed: return SignType.Speed;
                case PropTypes.SignNoTurn: return SignType.NoTurn;
                case PropTypes.SignHazard: return SignType.Hazard;
                default: return SignType.Generic;
            }
        }

        /// <summary>Qué texturas hacen falta (tipo + variante) para un conjunto de piezas.</summary>
        public static HashSet<(SignType, int)> Needed(IEnumerable<PropEntry> entries)
        {
            var set = new HashSet<(SignType, int)>();
            foreach (var e in entries) if (PropTypes.IsSign(e.type)) set.Add((ToType(e.type), e.variant));
            return set;
        }

        public static void Build(Transform root, LookSettings look, IList<PropEntry> signs)
        {
            if (signs == null || signs.Count == 0) return;
            var parent = new GameObject("TrafficSigns").transform;
            parent.SetParent(root, false);
            var rng = new System.Random(look.Signs.Seed + 5);
            float R() => (float)rng.NextDouble();

            var poleMatrices = new List<Matrix4x4>();
            var plateMatrices = new Dictionary<(SignType, int), List<Matrix4x4>>();
            int redLights = look.Signs.RedLightSigns;

            foreach (var e in signs)
            {
                SignType type = ToType(e.type);
                var (pw, ph) = SignGeometry.Dimensions(type);
                var rootM = Matrix4x4.TRS(new Vector3(e.x, 0f, e.z), Quaternion.Euler(0f, e.yaw, 0f), Vector3.one * e.scale);
                string state = string.IsNullOrEmpty(e.state) ? "upright" : e.state;

                bool hasPlate = state != "pole_only";
                Matrix4x4 plateLocal = Matrix4x4.identity;
                float lightHeight = 2.4f;

                switch (state)
                {
                    case "tilted":
                    {
                        float tx = (R() < 0.5f ? -1f : 1f) * (8f + R() * 7f), tz = (R() < 0.5f ? -1f : 1f) * (4f + R() * 7f);
                        rootM = rootM * Matrix4x4.Rotate(Quaternion.Euler(tx, 0f, tz));
                        AddPole(poleMatrices, rootM, Vector3.zero, Quaternion.identity, 2.4f);
                        plateLocal = Matrix4x4.TRS(new Vector3(0f, 2.05f, PlateFront), Quaternion.identity, Vector3.one);
                        break;
                    }
                    case "bent":
                    {
                        float bend = 28f + R() * 22f;
                        var upper = Quaternion.Euler(bend, 0f, (R() - 0.5f) * 30f);
                        var lowerTop = new Vector3(0f, 1.3f, 0f);
                        AddPole(poleMatrices, rootM, Vector3.zero, Quaternion.identity, 1.3f);
                        AddPole(poleMatrices, rootM, lowerTop, upper, 1.1f);
                        Vector3 center = lowerTop + upper * new Vector3(0f, 0.72f, PlateFront);
                        plateLocal = Matrix4x4.TRS(center, upper, Vector3.one);
                        lightHeight = 2.2f;
                        break;
                    }
                    case "fallen":
                    {
                        // el poste queda tendido y la placa mira hacia arriba
                        var lay = Quaternion.Euler(-88f + (R() - 0.5f) * 8f, 0f, (R() - 0.5f) * 30f);
                        rootM = rootM * Matrix4x4.TRS(new Vector3(0f, 0.07f, 0f), lay, Vector3.one);
                        AddPole(poleMatrices, rootM, Vector3.zero, Quaternion.identity, 2.4f);
                        plateLocal = Matrix4x4.TRS(new Vector3(0f, 2.05f, PlateFront), Quaternion.identity, Vector3.one);
                        AddFallenCollider(parent, rootM);
                        break;
                    }
                    case "pole_only":
                    {
                        float h = 1.7f + R() * 0.6f;
                        rootM = rootM * Matrix4x4.Rotate(Quaternion.Euler((R() - 0.5f) * 10f, 0f, (R() - 0.5f) * 10f));
                        AddPole(poleMatrices, rootM, Vector3.zero, Quaternion.identity, h);
                        break;
                    }
                    case "hanging":
                    {
                        AddPole(poleMatrices, rootM, Vector3.zero, Quaternion.identity, 2.4f);
                        // cuelga de un tornillo en su esquina superior: el centro queda justo debajo del punto de apoyo
                        var pivot = new Vector3(0f, 2.3f, PlateFront);
                        Vector2 c0 = new Vector2(pw / 2f, -ph / 2f);
                        float theta = -90f - Mathf.Atan2(c0.y, c0.x) * Mathf.Rad2Deg;
                        var q = Quaternion.Euler(0f, (R() - 0.5f) * 30f, theta);
                        plateLocal = Matrix4x4.TRS(pivot + q * new Vector3(c0.x, c0.y, 0f), q, Vector3.one);
                        break;
                    }
                    default: // upright
                        AddPole(poleMatrices, rootM, Vector3.zero, Quaternion.identity, 2.4f);
                        plateLocal = Matrix4x4.TRS(new Vector3(0f, 2.05f, PlateFront), Quaternion.identity, Vector3.one);
                        break;
                }

                if (hasPlate)
                {
                    var key = (type, e.variant);
                    if (!plateMatrices.TryGetValue(key, out var list)) plateMatrices[key] = list = new List<Matrix4x4>();
                    list.Add(rootM * plateLocal);
                }

                // luz roja parpadeante en las primeras señales erguidas/inclinadas/dobladas
                if (redLights > 0 && hasPlate && (state == "upright" || state == "tilted" || state == "bent"))
                {
                    AddRedLight(parent, rootM.MultiplyPoint3x4(new Vector3(0f, lightHeight + 0.55f, 0.05f)));
                    redLights--;
                }
            }

            // Postes: un solo grupo instanciado con el material de metal oxidado
            var poleGo = new GameObject("Instanced Poles");
            poleGo.transform.SetParent(parent, false);
            poleGo.AddComponent<InstancedBatch>().Init(PoleMesh(), Prim.GetTextured(new Color(0.33f, 0.34f, 0.35f), SurfaceLibrary.Metal, Vector2.one, 0.3f, 0.4f, true), poleMatrices.ToArray(), false);

            // Placas: un grupo por tipo y variante (submesh 0 = frente con la textura de la señal, 1 = dorso metálico)
            foreach (var kv in plateMatrices)
            {
                var (type, variant) = kv.Key;
                if (!SignLibrary.TryGet(type, variant, out var diff, out var nor)) continue;
                float tone = 0.9f + 0.05f * (((int)type + variant * 2) % 3); // pequeña variación de tono por grupo
                var front = Prim.GetWithTextures(diff, nor, new Color(tone, tone, tone, 1f), 0.4f, 0.2f, "sign|" + type + "|" + variant);
                var back = Prim.GetTextured(new Color(0.4f, 0.41f, 0.42f), SurfaceLibrary.Metal, Vector2.one, 0.3f, 0.4f, true);
                var go = new GameObject("Instanced Signs " + type + " v" + variant);
                go.transform.SetParent(parent, false);
                go.AddComponent<InstancedBatch>().Init(PlateMesh(type), new[] { front, back }, kv.Value.ToArray(), false);
            }
        }

        private static void AddPole(List<Matrix4x4> list, Matrix4x4 rootM, Vector3 pos, Quaternion rot, float length)
            => list.Add(rootM * Matrix4x4.TRS(pos, rot, new Vector3(1f, length, 1f)));

        private static Mesh PoleMesh()
        {
            if (_poleMesh != null) return _poleMesh;
            var m = new MeshData();
            FoliageGeometry.Tube(m, new List<V3> { new V3(0, 0, 0), new V3(0, 0.5f, 0), new V3(0, 1f, 0) }, new List<float> { PoleRadius, PoleRadius, PoleRadius }, 8, 1f, true);
            _poleMesh = BoxMesh.ToMesh(m);
            _poleMesh.name = "Sign Pole";
            return _poleMesh;
        }

        private static Mesh PlateMesh(SignType type)
        {
            if (PlateMeshes.TryGetValue(type, out var m) && m != null) return m;
            m = BoxMesh.ToMesh(SignGeometry.Plate(type));
            m.name = "Sign Plate " + type;
            PlateMeshes[type] = m;
            return m;
        }

        /// <summary>Una señal caída estorba un poco: collider pequeño y área "Not Walkable" para el NavMesh.</summary>
        private static void AddFallenCollider(Transform parent, Matrix4x4 rootM)
        {
            var go = new GameObject("Fallen Sign Collider");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(rootM.MultiplyPoint3x4(new Vector3(0f, 1.2f, 0f)), rootM.rotation);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.12f;
            col.height = 2.4f;
            Vegetation.MarkObstacle(go);
        }

        private static void AddRedLight(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Sign Red Light");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var bulb = Prim.Create(PrimitiveType.Sphere, "Lamp", Vector3.zero, Vector3.one * 0.14f, Prim.GetEmissive(new Color(1f, 0.1f, 0.06f), 5f), go.transform, false);
            bulb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.12f, 0.08f);
            l.range = 7f;
            l.intensity = 2.4f;
            l.shadows = LightShadows.None;
            var f = go.AddComponent<FlickerLight>();
            f.Amount = 0.05f;
            f.BlinkHz = 0.9f;
            f.Bulb = bulb.GetComponent<Renderer>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _poleMesh = null;
            PlateMeshes.Clear();
        }
    }
}
