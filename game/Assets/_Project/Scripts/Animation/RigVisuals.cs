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
            var spec = rig.Spec; float k = rig.Proportions.bulk;
            Material cloth = Prim.GetMaterial(pal.Cloth), armor = Prim.GetMaterial(pal.Armor), skin = Prim.GetMaterial(pal.Skin), accent = Prim.GetMaterial(pal.Accent), boots = Prim.GetMaterial(pal.Boots);
            float L(BoneId id) => spec[(int)id].Length;

            // pelvis y columna
            Part(rig, BoneId.Hips, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(rig.Proportions.hipWidth * 1.35f, 0.2f, 0.24f) * k, cloth);
            Part(rig, BoneId.Hips, PrimitiveType.Cube, new Vector3(0f, 0.07f, 0f), new Vector3(rig.Proportions.hipWidth * 1.45f, 0.05f, 0.27f) * k, boots);            // cinturón
            Part(rig, BoneId.Spine, PrimitiveType.Cube, new Vector3(0f, L(BoneId.Spine) * 0.5f, 0f), new Vector3(0.36f, L(BoneId.Spine) * 1.15f, 0.24f) * k, cloth);
            Part(rig, BoneId.Chest, PrimitiveType.Cube, new Vector3(0f, L(BoneId.Chest) * 0.5f, 0.01f), new Vector3(0.44f, L(BoneId.Chest) * 1.2f, 0.3f) * k, armor);   // chaleco
            Part(rig, BoneId.UpperChest, PrimitiveType.Cube, new Vector3(0f, L(BoneId.UpperChest) * 0.45f, 0.01f), new Vector3(rig.Proportions.shoulderWidth * 0.95f, L(BoneId.UpperChest) * 1.05f, 0.32f) * k, armor);
            Part(rig, BoneId.UpperChest, PrimitiveType.Cube, new Vector3(0f, L(BoneId.UpperChest) * 0.25f, 0.17f * k), new Vector3(0.3f, 0.05f, 0.03f) * k, accent);       // franja de acento
            Part(rig, BoneId.UpperChest, PrimitiveType.Cube, new Vector3(0f, L(BoneId.UpperChest) * 0.3f, -0.2f * k), new Vector3(0.34f, L(BoneId.UpperChest) * 1.1f, 0.14f) * k, cloth);  // mochila
            // cuello, cabeza y casco
            Part(rig, BoneId.Neck, PrimitiveType.Capsule, new Vector3(0f, L(BoneId.Neck) * 0.5f, 0f), new Vector3(0.12f, L(BoneId.Neck) * 0.7f, 0.12f) * k, skin);
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
                Part(rig, ua, PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), Vector3.one * 0.17f * k, cloth);                                                        // rótula del hombro
                Part(rig, ua, PrimitiveType.Cube, new Vector3(side * 0.02f, 0.03f, 0f), new Vector3(0.2f, 0.09f, 0.2f) * k, armor, side * 12f);                    // hombrera
                Part(rig, ua, PrimitiveType.Capsule, new Vector3(0f, -L(ua) * 0.5f, 0f), new Vector3(0.13f, L(ua) * 0.55f, 0.13f) * k, cloth);
                Part(rig, la, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.11f * k, cloth);                                                                  // codo
                Part(rig, la, PrimitiveType.Capsule, new Vector3(0f, -L(la) * 0.5f, 0f), new Vector3(0.11f, L(la) * 0.55f, 0.11f) * k, cloth);
                Part(rig, la, PrimitiveType.Cube, new Vector3(0f, -L(la) * 0.62f, 0f), new Vector3(0.12f, L(la) * 0.45f, 0.12f) * k, armor);                         // protector de antebrazo
                // mano con dedos (pulgar + 4 dedos de 2 falanges): agarra el arma con poses (empuñadura, guardamano, bomba, puño)
                var rh = RigHand.Build(rig.Bone(hand), !left, boots, k);
                if (left) rig.LeftHandMesh = rh; else rig.RightHandMesh = rh;

                // pierna: muslo, rodillera, espinillera, bota
                Part(rig, ul, PrimitiveType.Capsule, new Vector3(0f, -L(ul) * 0.5f, 0f), new Vector3(0.18f, L(ul) * 0.56f, 0.18f) * k, cloth);
                Part(rig, ll, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.14f * k, armor);                                                                  // rodillera
                Part(rig, ll, PrimitiveType.Capsule, new Vector3(0f, -L(ll) * 0.5f, 0f), new Vector3(0.14f, L(ll) * 0.55f, 0.14f) * k, cloth);
                Part(rig, ll, PrimitiveType.Cube, new Vector3(0f, -L(ll) * 0.75f, 0.01f), new Vector3(0.15f, L(ll) * 0.45f, 0.15f) * k, boots);                      // caña de la bota
                Part(rig, foot, PrimitiveType.Cube, new Vector3(0f, -L(foot) * 0.5f, 0.05f), new Vector3(0.13f, L(foot) * 1.2f, 0.27f) * k, boots);                 // bota
            }
        }

        private static void Part(ProceduralRig rig, BoneId bone, PrimitiveType type, Vector3 localPos, Vector3 scale, Material m, float roll = 0f)
        {
            var go = Prim.Create(type, bone + " mesh", localPos, scale, m, rig.Bone(bone), false);
            // las piezas pequeñas (rótulas, visera, cinturón, guantes, franjas) no proyectan sombra: no se nota y ahorra la mitad de las llamadas de sombra
            if (Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) < 0.16f || scale.y < 0.06f) go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PartCount++;
            if (roll != 0f) go.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        }
    }
}
