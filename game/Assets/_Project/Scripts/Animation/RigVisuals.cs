using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>Paleta de un personaje (uniforme, armadura, piel, acento). Diseño propio: tonos apagados y sucios con un acento naranja o rojo gastado.</summary>
    public struct RigPalette
    {
        public Color Cloth, Armor, Skin, Accent, Boots;

        public static RigPalette Soldier => new RigPalette
        {
            Cloth = new Color(0.33f, 0.35f, 0.3f), Armor = new Color(0.27f, 0.28f, 0.29f), Skin = new Color(0.62f, 0.48f, 0.38f),
            Accent = new Color(0.72f, 0.36f, 0.12f), Boots = new Color(0.16f, 0.14f, 0.12f),
        };
    }

    /// <summary>
    /// Cuelga mallas de cada hueso del rig para que se vea un soldado corpulento (casco, hombreras, chaleco, guantes y botas). Cada pieza es hija de su hueso, así que
    /// sigue la animación; en codos, rodillas y hombros hay rótulas que tapan la unión: las articulaciones no se "rompen" aunque se doblen mucho.
    /// Es la versión provisional (primitivas con materiales compartidos); las mallas y texturas detalladas llegan en el Bloque 4.
    /// </summary>
    public static class RigVisuals
    {
        /// <summary>Piezas creadas en total (para el informe de rendimiento).</summary>
        public static int PartCount;

        public static void BuildSoldier(ProceduralRig rig, RigPalette pal)
        {
            var spec = rig.Spec; var pr = rig.Proportions; float k = pr.bulk, d = pr.torsoDepth, t = pr.limbThickness;
            Material cloth = Prim.GetMaterial(pal.Cloth), armor = Prim.GetMaterial(pal.Armor), skin = Prim.GetMaterial(pal.Skin), accent = Prim.GetMaterial(pal.Accent), boots = Prim.GetMaterial(pal.Boots);
            float L(BoneId id) => spec[(int)id].Length;

            // pelvis y columna (sin mochila ni bultos en la espalda; chaleco táctico ligero que no ensancha la silueta)
            Part(rig, BoneId.Hips, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(pr.hipWidth * 1.3f * k, 0.2f * k, 0.22f * k * d), cloth);
            Part(rig, BoneId.Hips, PrimitiveType.Cube, new Vector3(0f, 0.07f, 0f), new Vector3(pr.hipWidth * 1.38f * k, 0.045f * k, 0.24f * k * d), boots);            // cinturón
            Part(rig, BoneId.Spine, PrimitiveType.Cube, new Vector3(0f, L(BoneId.Spine) * 0.5f, 0f), new Vector3(0.3f * k, L(BoneId.Spine) * 1.15f, 0.21f * k * d), cloth);
            Part(rig, BoneId.Chest, PrimitiveType.Cube, new Vector3(0f, L(BoneId.Chest) * 0.5f, 0.005f), new Vector3(0.34f * k, L(BoneId.Chest) * 1.2f, 0.24f * k * d), armor);   // chaleco ligero
            Part(rig, BoneId.UpperChest, PrimitiveType.Cube, new Vector3(0f, L(BoneId.UpperChest) * 0.45f, 0.005f), new Vector3(pr.shoulderWidth * 0.82f * k, L(BoneId.UpperChest) * 1.05f, 0.24f * k * d), armor);
            Part(rig, BoneId.UpperChest, PrimitiveType.Cube, new Vector3(0f, L(BoneId.UpperChest) * 0.25f, 0.125f * k * d), new Vector3(0.24f * k, 0.045f, 0.02f), accent);       // franja de acento
            // cuello, cabeza y casco
            Part(rig, BoneId.Neck, PrimitiveType.Capsule, new Vector3(0f, L(BoneId.Neck) * 0.5f, 0f), new Vector3(0.11f * k, L(BoneId.Neck) * 0.7f, 0.11f * k), skin);
            float head = L(BoneId.Head);
            Part(rig, BoneId.Head, PrimitiveType.Sphere, new Vector3(0f, head * 0.45f, 0.01f), new Vector3(0.2f, head * 0.95f, 0.22f), skin);
            Part(rig, BoneId.Head, PrimitiveType.Sphere, new Vector3(0f, head * 0.62f, -0.005f), new Vector3(0.25f, head * 0.78f, 0.27f), armor);                 // casco
            Part(rig, BoneId.Head, PrimitiveType.Cube, new Vector3(0f, head * 0.45f, 0.11f), new Vector3(0.17f, 0.045f, 0.05f), boots);                            // visera

            foreach (bool left in new[] { true, false })
            {
                float side = left ? -1f : 1f;
                BoneId sh = left ? BoneId.LeftShoulder : BoneId.RightShoulder, ua = left ? BoneId.LeftUpperArm : BoneId.RightUpperArm, la = left ? BoneId.LeftLowerArm : BoneId.RightLowerArm, hand = left ? BoneId.LeftHand : BoneId.RightHand;
                BoneId ul = left ? BoneId.LeftUpperLeg : BoneId.RightUpperLeg, ll = left ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg, foot = left ? BoneId.LeftFoot : BoneId.RightFoot;

                // brazo: hombrera, brazo, codo, antebrazo, guante
                // brazo con pivote en cada articulación: hombro, brazo, codo y antebrazo (hombrera pequeña: no ensancha la silueta)
                Part(rig, ua, PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), Vector3.one * 0.12f * k * t, cloth);                                                     // rótula del hombro
                Part(rig, ua, PrimitiveType.Cube, new Vector3(side * 0.01f, 0.02f, 0f), new Vector3(0.12f, 0.05f, 0.14f) * k, armor, side * 12f);                  // hombrera
                Limb(rig, ua, ua + " limb", L(ua), new[] { 0f, 0.05f * k * t, 0.45f, 0.048f * k * t, 1f, 0.04f * k * t }, 1f, cloth);
                Part(rig, la, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.08f * k * t, cloth);                                                              // codo
                Limb(rig, la, la + " limb", L(la), new[] { 0f, 0.04f * k * t, 0.35f, 0.042f * k * t, 1f, 0.03f * k * t }, 1f, cloth);
                var guard = Limb(rig, la, la + " protector", L(la) * 0.45f, new[] { 0f, 0.046f * k * t, 1f, 0.038f * k * t }, 1f, armor);                       // protector de antebrazo
                guard.transform.localPosition = new Vector3(0f, -L(la) * 0.4f, 0f);
                // mano con dedos (pulgar + 4 dedos de 2 falanges): agarra el arma con poses (empuñadura, guardamano, bomba, puño)
                var rh = RigHand.Build(rig.Bone(hand), !left, boots, k);
                if (left) rig.LeftHandMesh = rh; else rig.RightHandMesh = rh;

                // pierna articulada: cada pieza cuelga de su hueso con el pivote EN LA ARTICULACIÓN (no en su centro), así gira donde debe y no asoma
                // por encima de ella. Muslo y pantorrilla se estrechan hacia la rodilla (radio <= el de la rótula) para que, arrodillado, nada atraviese el suelo.
                BuildLeg(rig, left, cloth, armor, boots, k * t);
            }
        }

        /// <summary>Reconstruye esqueleto y mallas con otra complexión (en Play, desde F6), conservando la capa del personaje.</summary>
        public static void Rebuild(ProceduralRig rig, RigProportions p, RigPalette pal)
        {
            rig.Rebuild(p);
            BuildSoldier(rig, pal);
            int layer = rig.gameObject.layer;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Mesh> LimbMeshes = new System.Collections.Generic.Dictionary<string, Mesh>();

        /// <summary>Radio de la rótula (m): todo lo que rodea la rodilla cabe en él. Arrodillado, la rodilla trasera queda a HalfKneel.RearKneeHeight del suelo.</summary>
        public const float KneeJointRadius = 0.055f;

        private static void BuildLeg(ProceduralRig rig, bool left, Material cloth, Material armor, Material boots, float k)
        {
            var spec = rig.Spec;
            float side = left ? -1f : 1f;
            BoneId ul = left ? BoneId.LeftUpperLeg : BoneId.RightUpperLeg, ll = left ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg;
            BoneId foot = left ? BoneId.LeftFoot : BoneId.RightFoot, toes = left ? BoneId.LeftToes : BoneId.RightToes;
            float thigh = spec[(int)ul].Length, shin = spec[(int)ll].Length, drop = spec[(int)foot].Length, ball = spec[(int)toes].LocalPosition.Z;
            float r = KneeJointRadius;

            // muslo: ancho en la cadera, se estrecha hasta la rodilla; bolsillo lateral
            var th = Limb(rig, ul, ul + " limb", thigh, new[] { 0f, 0.07f * k, 0.5f, 0.064f * k, 0.92f, r, 1f, r * 0.95f }, 1.08f, cloth);
            Prim.CreateBox(ul + " bolsillo", new Vector3(side * 0.075f * k, -thigh * 0.45f, 0f), new Vector3(0.03f, 0.12f, 0.1f) * k, armor, th.transform, 0.3f, default, false, 0.006f);
            // rodilla: rótula esférica + rodillera hacia delante (dentro del radio de la rótula)
            Part(rig, ll, PrimitiveType.Sphere, Vector3.zero, Vector3.one * r * 2f, cloth);
            Part(rig, ll, PrimitiveType.Sphere, new Vector3(0f, -0.01f, 0.02f), new Vector3(0.08f, 0.08f, 0.05f), armor);
            // pantorrilla con gemelo, y caña de la bota sobre la parte baja
            Limb(rig, ll, ll + " limb", shin, new[] { 0f, r * 0.95f, 0.3f, 0.062f, 0.65f, 0.05f, 1f, 0.045f }, 1.05f, cloth);
            var shaft = Limb(rig, ll, ll + " caña", shin * 0.42f, new[] { 0f, 0.057f, 1f, 0.055f }, 1.05f, boots);
            shaft.transform.localPosition = new Vector3(0f, -shin * 0.6f, 0f);
            // tobillo
            Part(rig, foot, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.096f, boots);
            // pie en dos segmentos con el ORIGEN EN LA SUELA: talón-bola (hueso del pie) y punta (hueso de la punta, que se dobla para quedar plana)
            var sole = new GameObject(foot + " suela").transform;
            sole.SetParent(rig.Bone(foot), false);
            sole.localPosition = new Vector3(0f, -drop, 0f);
            float heel = 0.07f, h = Mathf.Min(0.085f, drop + 0.01f);
            Prim.CreateBox(foot + " bota", new Vector3(0f, h * 0.5f, (ball - heel) * 0.5f), new Vector3(0.115f, h, ball + heel), boots, sole, 0.3f, default, false, 0.015f);
            Prim.CreateBox(toes + " punta", new Vector3(0f, 0.025f, 0.0325f), new Vector3(0.11f, 0.05f, 0.085f), boots, rig.Bone(toes), 0.3f, default, false, 0.015f);
            PartCount += 3;
            // medida REAL de la bota (no la supuesta): el rig la usa para que ningún vértice baje del suelo al inclinar el pie
            rig.SetFootShape(MeasureFoot(rig.Bone(foot), rig.Bone(toes)));
        }

        private static GameObject Limb(ProceduralRig rig, BoneId bone, string name, float length, float[] rings, float depth, Material m)
        {
            string key = length.ToString("0.###") + "|" + depth.ToString("0.##") + "|" + string.Join(",", System.Array.ConvertAll(rings, x => x.ToString("0.###")));
            if (!LimbMeshes.TryGetValue(key, out var mesh) || mesh == null)
            {
                mesh = BoxMesh.ToMesh(LimbGeometry.Segment(length, rings, 10, depth));
                mesh.name = "Limb " + key;
                LimbMeshes[key] = mesh;
            }
            var go = new GameObject(name);
            go.transform.SetParent(rig.Bone(bone), false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            PartCount++;
            return go;
        }

        /// <summary>Mide la bota en el espacio del pie (pose de reposo): caída del tobillo a la suela, talón, bola y largo de la punta.</summary>
        private static FootShape MeasureFoot(Transform foot, Transform toes)
        {
            float minY = 0f, minZ = 0f, maxToeZ = 0f;
            foreach (var mf in foot.GetComponentsInChildren<MeshFilter>())
                foreach (var v in mf.sharedMesh.vertices)
                {
                    Vector3 p = foot.InverseTransformPoint(mf.transform.TransformPoint(v));
                    minY = Mathf.Min(minY, p.y); minZ = Mathf.Min(minZ, p.z);
                    if (mf.transform.IsChildOf(toes)) maxToeZ = Mathf.Max(maxToeZ, p.z);
                }
            return new FootShape { Drop = -minY, Heel = -minZ, Ball = toes.localPosition.z, Toe = maxToeZ - toes.localPosition.z };
        }

        private static GameObject Part(ProceduralRig rig, BoneId bone, PrimitiveType type, Vector3 localPos, Vector3 scale, Material m, float roll = 0f)
        {
            var go = Prim.Create(type, bone + " mesh", localPos, scale, m, rig.Bone(bone), false);
            // las piezas pequeñas (rótulas, visera, cinturón, guantes, franjas) no proyectan sombra: no se nota y ahorra la mitad de las llamadas de sombra
            if (Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) < 0.16f || scale.y < 0.06f) go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PartCount++;
            if (roll != 0f) go.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            return go;
        }
    }
}
