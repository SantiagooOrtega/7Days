using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Una pieza del modelo: hueso del que cuelga, nombre, material (ranura), malla en el espacio de su pivote, posición del pivote y si da sombra.</summary>
    public struct ModelPiece
    {
        public BoneId Bone;
        public string Name;
        public int Slot;
        public MeshData Mesh;
        public V3 Pivot;
        public bool Shadow;
    }

    /// <summary>
    /// Modelo del soldado (pura): uniforme con camuflaje (torso, brazos y piernas como tornos ovalados con el pivote en cada articulación), chaleco ligero
    /// (placas, cummerbund, hombreras de correa, tres portacargadores con solapa, radio con antena, panel de velcro, hebillas), cinturón con hebilla y bolsa,
    /// funda de pistola en el muslo derecho, hombreras finas, coderas, rodilleras, botas (caña, tobillo, empeine con cordones, suela) y la cabeza de cada variante:
    /// casco tipo FAST con rieles, montura de visión nocturna levantada, orejeras con micrófono, placa calavera con relieve y visor ahumado (Calavera); gorra con
    /// cascos, gafas y pañuelo (Explorador); casco con parches de cruz propia, gafas y pañuelo (Médico); capucha con red y velo (Francotirador).
    /// Materiales (ranuras): 0 uniforme, 1 equipo, 2 casco/cabeza, 3 visor. Presupuesto: ~15 000 triángulos.
    /// Medidas de piernas, rótula y bota iguales a las validadas por las pruebas de piernas (no hay vértices bajo el suelo).
    /// </summary>
    public static class SoldierModel
    {
        public const int SlotUniform = 0, SlotGear = 1, SlotHelmet = 2, SlotVisor = 3;
        public const float KneeJointRadius = 0.055f;
        public const int TriangleBudget = 15000;

        private static AtlasCell U(int c) => CharacterTextures.Cell(AtlasKind.Uniform, c);
        private static AtlasCell G(int c) => CharacterTextures.Cell(AtlasKind.Gear, c);
        private static AtlasCell H(int c) => CharacterTextures.Cell(AtlasKind.Helmet, c);
        private static AtlasCell HS(int c, int s) => CharacterTextures.Sub(AtlasKind.Helmet, c, s);

        public static List<ModelPiece> Build(BoneDef[] spec, RigProportions pr, CharacterVariantData v)
        {
            v = v ?? new CharacterVariantData();
            var list = new List<ModelPiece>();
            float k = pr.bulk, d = pr.torsoDepth, kt = pr.bulk * pr.limbThickness;
            float L(BoneId id) => spec[(int)id].Length;
            void Add(BoneId bone, string name, int slot, MeshBuilder mb, bool shadow = true, V3 pivot = default) =>
                list.Add(new ModelPiece { Bone = bone, Name = bone + " " + name, Slot = slot, Mesh = mb.Data, Pivot = pivot, Shadow = shadow });

            // ---------------------------------------------------------------- torso (uniforme) y chaleco (equipo)
            // V suave: cintura 0.150k, pecho 0.166k (k = grosor); pectoral hacia delante (cz), glúteo y espalda hacia atrás; trapecio y deltoides tapan la unión del hombro
            float sp = L(BoneId.Spine), ch = L(BoneId.Chest), uc = L(BoneId.UpperChest);
            float Zf(float rx, float zf) => rx * zf * d;   // radio Z: fondo del torso (torsoDepth)
            float[] Torso(params float[] y_w_zf_cz)   // grupos (y, ancho/k, fondo/ancho, cz/k) de abajo arriba
            {
                int n = y_w_zf_cz.Length / 4; var r = new float[n * MeshBuilder.LoftStride];
                for (int i = 0; i < n; i++)
                {
                    float rx = y_w_zf_cz[i * 4 + 1] * k;
                    r[i * 5] = y_w_zf_cz[i * 4]; r[i * 5 + 1] = rx; r[i * 5 + 2] = Zf(rx, y_w_zf_cz[i * 4 + 2]); r[i * 5 + 3] = 0f; r[i * 5 + 4] = y_w_zf_cz[i * 4 + 3] * k;
                }
                return r;
            }
            {
                var hips = new MeshBuilder();
                hips.Loft(new V3(0f, 0f, 0f), new V3(0, 0, 0), Torso(-0.13f, 0.130f, 0.66f, -0.004f, -0.05f, 0.158f, 0.66f, -0.012f, 0.05f, 0.158f, 0.66f, -0.006f, 0.12f, 0.158f, 0.66f, -0.002f), 16, true, true, U(CharacterTextures.UCamo), 0.45f);
                Add(BoneId.Hips, "pelvis", SlotUniform, hips);
                var belt = new MeshBuilder();
                float hipFront = 0.15f * k * 0.66f * d;
                belt.Lathe(new V3(0f, 0.07f, 0f), new V3(0, 0, 0), new[] { -0.025f, 0.166f * k, 0.025f, 0.166f * k }, 18, 0.66f * d, true, true, G(CharacterTextures.GBelt), 0.4f);
                belt.Box(new V3(0f, 0.07f, 0.166f * k * 0.66f * d + 0.006f), new V3(0.06f, 0.045f, 0.014f), new V3(0, 0, 0), 0.004f, G(CharacterTextures.GMetal), 0.08f);   // hebilla
                Add(BoneId.Hips, "cinturón", SlotGear, belt);
                var dump = new MeshBuilder();
                dump.Box(new V3(-0.166f * k, 0.0f, -0.04f), new V3(0.05f, 0.11f, 0.09f), new V3(0, 0, 8f), 0.012f, G(CharacterTextures.GPouch), 0.15f);   // bolsa en la cadera izquierda
                Add(BoneId.Hips, "bolsa", SlotGear, dump);

                var spine = new MeshBuilder();
                spine.Loft(new V3(0, 0, 0), new V3(0, 0, 0), Torso(-0.02f, 0.158f, 0.66f, 0f, sp * 0.5f, 0.159f, 0.66f, 0.002f, sp * 1.08f, 0.162f, 0.65f, 0.004f), 16, true, true, U(CharacterTextures.UCamo), 0.45f);
                Add(BoneId.Spine, "torso", SlotUniform, spine);
                var chest = new MeshBuilder();
                chest.Loft(new V3(0, 0, 0), new V3(0, 0, 0), Torso(-0.02f, 0.162f, 0.65f, 0.004f, ch * 0.5f, 0.165f, 0.68f, 0.008f, ch * 1.05f, 0.167f, 0.68f, 0.009f), 16, true, true, U(CharacterTextures.UCamo), 0.45f);
                Add(BoneId.Chest, "torso", SlotUniform, chest);
                var upper = new MeshBuilder();
                upper.Loft(new V3(0, 0, 0), new V3(0, 0, 0), Torso(-0.02f, 0.166f, 0.68f, 0.009f, uc * 0.35f, 0.167f, 0.66f, 0.008f, uc * 0.7f, 0.160f, 0.62f, 0.004f, uc * 0.92f, 0.125f, 0.6f, 0f, uc * 1.03f, 0.075f, 0.6f, 0f), 16, true, true, U(CharacterTextures.UCamo), 0.45f);
                Add(BoneId.UpperChest, "torso", SlotUniform, upper);
                // trapecio: elipsoide ancho y plano de la base del cuello a la punta del hombro (tapa el hueco sobre el hombro y marca la pendiente)
                var trap = new MeshBuilder();
                trap.Lathe(new V3(0f, uc * 0.9f, -0.012f), new V3(0, 0, 0), Ellipsoid(0.178f * k, 0.052f, 7), 18, 0.5f * d / 0.9f, true, true, U(CharacterTextures.UCamo), 0.4f);
                Add(BoneId.UpperChest, "trapecio", SlotUniform, trap);

                // chaleco: placas delantera y trasera, cummerbund, portacargadores, radio y velcro (siguen la superficie del pecho nuevo)
                float chestRx = 0.164f * k, front = Zf(chestRx, 0.68f) + 0.008f * k, back = Zf(chestRx, 0.68f) - 0.008f * k;
                var vest = new MeshBuilder();
                vest.Box(new V3(0f, ch * 0.62f, front + 0.012f), new V3(0.27f * k, 0.3f, 0.026f), new V3(-4f, 0, 0), 0.01f, G(CharacterTextures.GVest), 0.35f);
                vest.Box(new V3(0f, ch * 0.62f, -back - 0.012f), new V3(0.27f * k, 0.3f, 0.026f), new V3(4f, 0, 0), 0.01f, G(CharacterTextures.GVest), 0.35f);
                foreach (float sx in new[] { -1f, 1f })
                {
                    vest.Box(new V3(sx * 0.144f * k, ch * 0.45f, 0f), new V3(0.022f, 0.15f, 0.2f * d), new V3(0, 0, 0), 0.006f, G(CharacterTextures.GVest), 0.35f);   // cummerbund
                    vest.Box(new V3(sx * 0.1f, ch * 0.62f, front + 0.028f), new V3(0.03f, 0.022f, 0.012f), new V3(0, 0, 0), 0.003f, G(CharacterTextures.GMetal), 0.06f);   // hebillas
                }
                Add(BoneId.Chest, "chaleco", SlotGear, vest);
                var pouches = new MeshBuilder();
                for (int i = -1; i <= 1; i++)
                {
                    float x = i * 0.072f;
                    pouches.Box(new V3(x, ch * 0.42f, front + 0.04f), new V3(0.066f, 0.1f, 0.036f), new V3(-4f, 0, 0), 0.008f, G(CharacterTextures.GPouch), 0.15f);
                    pouches.Box(new V3(x, ch * 0.42f + 0.052f, front + 0.042f), new V3(0.068f, 0.02f, 0.04f), new V3(-4f, 0, 0), 0.005f, G(CharacterTextures.GPouch), 0.15f);   // solapa
                    pouches.Box(new V3(x, ch * 0.42f + 0.03f, front + 0.061f), new V3(0.014f, 0.03f, 0.004f), new V3(-4f, 0, 0), 0.0015f, G(CharacterTextures.GVelcro), 0.05f);  // lengüeta
                }
                Add(BoneId.Chest, "portacargadores", SlotGear, pouches, false);
                var radio = new MeshBuilder();
                radio.Box(new V3(-0.157f * k, ch * 0.75f, front * 0.4f), new V3(0.045f, 0.11f, 0.06f), new V3(0, 0, 0), 0.008f, G(CharacterTextures.GRadio), 0.15f);
                radio.Cylinder(new V3(-0.167f * k, ch * 0.75f + 0.15f, front * 0.4f - 0.01f), new V3(0, 0, -6f), 0.0035f, 0.002f, 0.2f, 6, G(CharacterTextures.GMetal), 0.3f);   // antena
                Add(BoneId.Chest, "radio", SlotGear, radio, false);

                var yoke = new MeshBuilder();   // parte alta del chaleco: correas de hombro, panel de velcro y parche
                float ucFront = Zf(0.166f * k, 0.68f) + 0.009f * k, ucBack = Zf(0.166f * k, 0.68f) - 0.009f * k;
                foreach (float sx in new[] { -1f, 1f })
                {
                    yoke.Box(new V3(sx * 0.1f, uc * 0.55f, ucFront + 0.008f), new V3(0.055f, uc * 0.75f, 0.018f), new V3(0, 0, 0), 0.006f, G(CharacterTextures.GWebbing), 0.3f);
                    yoke.Box(new V3(sx * 0.1f, uc * 0.55f, -ucBack - 0.008f), new V3(0.055f, uc * 0.75f, 0.018f), new V3(0, 0, 0), 0.006f, G(CharacterTextures.GWebbing), 0.3f);
                    yoke.Box(new V3(sx * 0.1f, uc * 0.93f, 0f), new V3(0.055f, 0.016f, (ucFront + ucBack) + 0.03f), new V3(0, 0, 0), 0.005f, G(CharacterTextures.GWebbing), 0.3f);   // sobre el hombro
                }
                yoke.Box(new V3(0f, uc * 0.42f, ucFront + 0.02f), new V3(0.14f, 0.07f, 0.008f), new V3(0, 0, 0), 0.003f, G(CharacterTextures.GVelcro), 0.15f);
                Add(BoneId.UpperChest, "correas", SlotGear, yoke, false);

                float neck = L(BoneId.Neck);
                var gaiter = new MeshBuilder();   // cuello con trapecio: se ensancha hacia la base
                gaiter.Lathe(new V3(0, 0, 0), new V3(0, 0, 0), new[] { -0.03f, 0.082f * k, 0.01f, 0.066f * k, neck + 0.02f, 0.058f * k }, 14, 1.05f, true, true, U(CharacterTextures.UKnit), 0.25f);
                Add(BoneId.Neck, "braga", SlotUniform, gaiter);
            }

            // ---------------------------------------------------------------- brazos (deltoides, bíceps y tríceps, antebrazo que se estrecha en la muñeca)
            // medidas literales para la Calavera (kt = 1.175) escaladas por f = kt / 1.175, así el grosor de F6 y de cada variante las mueve
            float f = kt / ReferenceKt;
            foreach (bool left in new[] { true, false })
            {
                float side = left ? -1f : 1f;
                BoneId ua = left ? BoneId.LeftUpperArm : BoneId.RightUpperArm, la = left ? BoneId.LeftLowerArm : BoneId.RightLowerArm;
                float lu = L(ua), ll = L(la);
                var upperArm = new MeshBuilder();
                upperArm.Loft(new V3(0, 0, 0), new V3(0, 0, 0), LoftRings(lu, f, 0, 0,
                    0.00f, 0.058f, 0.062f, 0f, 0f,
                    0.12f, 0.064f, 0.067f, 0f, 0f,       // inserción del deltoides
                    0.36f, 0.060f, 0.067f, 0f, 0.005f,   // bíceps hacia delante
                    0.60f, 0.053f, 0.058f, 0f, 0.001f,
                    0.85f, 0.046f, 0.048f, 0f, -0.001f,  // tríceps y codo
                    1.00f, 0.042f, 0.043f, 0f, 0f), 12, true, true, U(CharacterTextures.UCamo), 0.45f);
                Add(ua, "limb", SlotUniform, upperArm);
                var delt = new MeshBuilder();   // deltoides redondeado: tapa la unión del brazo con el torso y el trapecio
                delt.Lathe(new V3(side * 0.012f * f, 0.004f, 0f), new V3(0, 0, 0), Ellipsoid(0.068f * f, 0.062f * f, 6), 14, 1.04f, true, true, U(CharacterTextures.UCamo), 0.3f);
                Add(ua, "deltoides", SlotUniform, delt, false);
                var pad = new MeshBuilder();   // hombrera: media cúpula sobre el deltoides, pegada al cuerpo (su base en el eje del hombro)
                pad.Dome(new V3(side * 0.004f, 0.006f, 0f), new V3(0, 0, -90f * side), new V3(0.084f * f, 0.074f * f, 0.086f * f), lon => 0f, 0.006f, 14, 5, G(CharacterTextures.GPad), 0.15f);
                Add(ua, "hombrera", SlotGear, pad, false);
                if (v.armband && left)
                {
                    var band = new MeshBuilder();
                    band.Lathe(new V3(0, -lu * 0.4f, 0), new V3(0, 0, 0), new[] { -0.03f, 0.062f * f, 0.03f, 0.061f * f }, 12, 1.05f, true, true, G(CharacterTextures.GArmband), 0.2f);
                    Add(ua, "brazalete", SlotGear, band, false);
                }
                var lowerArm = new MeshBuilder();
                lowerArm.Loft(new V3(0, 0, 0), new V3(0, 0, 0), LoftRings(ll, f, 0, 0,
                    0.00f, 0.044f, 0.045f, 0f, 0f,
                    0.14f, 0.052f, 0.054f, 0f, 0.002f,   // músculos extensores
                    0.40f, 0.048f, 0.050f, 0f, 0.001f,
                    0.75f, 0.037f, 0.038f, 0f, 0f,
                    1.00f, 0.031f, 0.032f, 0f, 0f), 12, true, true, U(CharacterTextures.UCamo), 0.45f);   // se estrecha hacia la muñeca
                Add(la, "limb", SlotUniform, lowerArm);
                // codo: banda de pliegues (la tela se arruga en el codo: normal map propio) y codera hacia la punta del codo (+Z del hueso)
                var elbow = new MeshBuilder();
                elbow.Loft(new V3(0, 0, 0), new V3(0, 0, 0), Rings(-0.05f, 0.040f * f, 0.041f * f, -0.02f, 0.050f * f, 0.052f * f, 0.008f, 0.051f * f, 0.053f * f, 0.035f, 0.044f * f, 0.045f * f), 12, true, true, CharacterTextures.Sub(AtlasKind.Uniform, CharacterTextures.UFold, 0), 0.01f, fit: true);
                Add(la, "pliegues codo", SlotUniform, elbow, false);
                var codera = new MeshBuilder();
                codera.Dome(new V3(0f, -0.004f, 0.02f * f), new V3(90f, 0, 0), new V3(0.044f * f, 0.032f * f, 0.052f * f), lon => 0f, 0.006f, 10, 4, G(CharacterTextures.GPad), 0.12f);
                Add(la, "codera", SlotGear, codera, false);
                var cuff = new MeshBuilder();   // puño del guante sobre la muñeca
                cuff.Lathe(new V3(0, -ll * 0.88f, 0), new V3(0, 0, 0), new[] { -ll * 0.13f, 0.037f * f, 0f, 0.035f * f }, 10, 1.1f, true, true, G(CharacterTextures.GGlove), 0.15f);
                Add(la, "puño", SlotGear, cuff, false);
            }

            // ---------------------------------------------------------------- piernas (glúteo y cuádriceps, rodilla definida, gemelo, tobillo fino; el radio junto a la rodilla no pasa de la rótula)
            foreach (bool left in new[] { true, false })
            {
                float side = left ? -1f : 1f;
                BoneId ul = left ? BoneId.LeftUpperLeg : BoneId.RightUpperLeg, ll = left ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg;
                BoneId foot = left ? BoneId.LeftFoot : BoneId.RightFoot, toes = left ? BoneId.LeftToes : BoneId.RightToes;
                float thigh = spec[(int)ul].Length, shin = spec[(int)ll].Length, drop = spec[(int)foot].Length, ball = spec[(int)toes].LocalPosition.Z;
                float r = KneeJointRadius;
                var th = new MeshBuilder();
                th.Loft(new V3(0, 0, 0), new V3(0, 0, 0), LoftRings(thigh, f, 0, 2,
                    0.00f, 0.083f, 0.090f, 0f, -0.010f,   // cadera y glúteo (hacia atrás)
                    0.14f, 0.083f, 0.091f, 0f, -0.004f,
                    0.38f, 0.079f, 0.088f, 0f, 0.006f,    // cuádriceps (hacia delante)
                    0.62f, 0.070f, 0.076f, 0f, 0.008f,
                    0.86f, 0.056f, 0.058f, 0f, 0.002f,    // desde aquí, medidas absolutas: no pasan de la rótula
                    1.00f, 0.052f, 0.053f, 0f, 0f), 14, true, true, U(CharacterTextures.UCamo), 0.45f);
                Add(ul, "limb", SlotUniform, th);
                var pocket = new MeshBuilder();
                pocket.Box(new V3(side * 0.088f * f, -thigh * 0.45f, 0f), new V3(0.026f, 0.12f, 0.1f), new V3(0, 0, 0), 0.008f, G(CharacterTextures.GPouch), 0.15f);
                if (!left)
                {
                    // funda de pistola en el muslo derecho con su correa
                    pocket.Box(new V3(0.092f * f, -thigh * 0.28f, 0.01f), new V3(0.04f, 0.15f, 0.07f), new V3(0, 0, 4f), 0.01f, G(CharacterTextures.GHolster), 0.2f);
                    pocket.Lathe(new V3(0, -thigh * 0.36f, 0), new V3(0, 0, 0), new[] { -0.012f, 0.088f * f, 0.012f, 0.087f * f }, 12, 1.08f, true, true, G(CharacterTextures.GWebbing), 0.3f);
                }
                Add(ul, "bolsillo", SlotGear, pocket, false);
                // rodilla definida: rótula esférica (dentro de su radio) con pliegues detrás y rodillera hacia delante (+Z = hacia donde se dobla)
                var knee = new MeshBuilder();
                knee.Lathe(new V3(0, 0, 0), new V3(0, 0, 0), Sphere(r, 6), 12, 1f, true, true, CharacterTextures.Sub(AtlasKind.Uniform, CharacterTextures.UFold, 1), 0.01f, fit: true);
                Add(ll, "rótula", SlotUniform, knee, false);
                var kneePad = new MeshBuilder();
                kneePad.Dome(new V3(0f, -0.01f, 0.012f), new V3(90f, 0, 0), new V3(0.045f, 0.04f, 0.05f), lon => 0f, 0.008f, 12, 5, G(CharacterTextures.GPad), 0.12f);
                Add(ll, "rodillera", SlotGear, kneePad, false);
                var calf = new MeshBuilder();
                calf.Loft(new V3(0, 0, 0), new V3(0, 0, 0), LoftRings(shin, f, 1, 0,
                    0.00f, 0.052f, 0.053f, 0f, 0f,        // junto a la rodilla, medida absoluta
                    0.20f, 0.062f, 0.068f, 0f, -0.012f,   // gemelo (hacia atrás)
                    0.38f, 0.066f, 0.073f, 0f, -0.012f,
                    0.62f, 0.052f, 0.054f, 0f, -0.006f,
                    0.85f, 0.042f, 0.043f, 0f, 0f,
                    1.00f, 0.038f, 0.040f, 0f, 0f), 14, true, true, U(CharacterTextures.UCamoMud), 0.45f);   // tobillo fino
                Add(ll, "limb", SlotUniform, calf);
                var shaft = new MeshBuilder();   // caña de la bota sobre la parte baja de la pantorrilla
                shaft.Lathe(new V3(0, -shin * 0.6f, 0), new V3(0, 0, 0), Limb(shin * 0.42f, new[] { 0f, 0.057f, 1f, 0.055f }), 12, 1.05f, true, true, G(CharacterTextures.GBoot), 0.25f);
                Add(ll, "caña", SlotGear, shaft);
                var ankle = new MeshBuilder();
                ankle.Lathe(new V3(0, 0, 0), new V3(0, 0, 0), Sphere(0.048f, 5), 10, 1f, true, true, G(CharacterTextures.GBoot), 0.2f);
                Add(foot, "tobillo", SlotGear, ankle, false);
                // bota con el ORIGEN EN LA SUELA: suela de goma, empeine de cuero y cordones encima
                float heel = 0.07f, h = Math.Min(0.085f, drop + 0.01f), len = ball + heel;
                var boot = new MeshBuilder();
                boot.Box(new V3(0f, 0.009f, (ball - heel) * 0.5f), new V3(0.118f, 0.018f, len), new V3(0, 0, 0), 0.006f, G(CharacterTextures.GSole), 0.3f);
                boot.Box(new V3(0f, 0.018f + (h - 0.018f) * 0.5f, (ball - heel) * 0.5f - 0.004f), new V3(0.11f, h - 0.018f, len - 0.012f), new V3(0, 0, 0), 0.014f, G(CharacterTextures.GBoot), 0.3f);
                boot.Box(new V3(0f, h + 0.002f, ball * 0.45f), new V3(0.05f, 0.012f, ball * 0.8f), new V3(-12f, 0, 0), 0.004f, G(CharacterTextures.GLaces), 0.15f);   // lengüeta con cordones
                Add(foot, "bota", SlotGear, boot, true, new V3(0f, -drop, 0f));
                var toe = new MeshBuilder();
                toe.Box(new V3(0f, 0.009f, 0.0325f), new V3(0.112f, 0.018f, 0.085f), new V3(0, 0, 0), 0.006f, G(CharacterTextures.GSole), 0.2f);
                toe.Box(new V3(0f, 0.018f + 0.016f, 0.03f), new V3(0.104f, 0.032f, 0.078f), new V3(0, 0, 0), 0.012f, G(CharacterTextures.GBoot), 0.2f);
                Add(toes, "punta", SlotGear, toe);
            }

            // ---------------------------------------------------------------- cabeza
            BuildHead(list, spec, v, Add);
            return list;
        }

        // medidas de la cabeza (en el espacio del hueso Head, origen en la base del cráneo)
        public const float HeadRx = 0.1f, HeadRy = 0.112f, HeadRz = 0.11f, HeadCy = 0.106f, HeadCz = 0.01f;

        private static void BuildHead(List<ModelPiece> list, BoneDef[] spec, CharacterVariantData v, Action<BoneId, string, int, MeshBuilder, bool, V3> add)
        {
            var bc = new MeshBuilder();   // pasamontañas (no se ve piel en ninguna variante)
            bc.Lathe(new V3(0f, HeadCy, HeadCz), new V3(0, 0, 0), Ellipsoid(HeadRx, HeadRy, 7), 14, HeadRz / HeadRx, true, true, U(CharacterTextures.UKnit), 0.3f);
            add(BoneId.Head, "pasamontañas", SlotUniform, bc, true, default);

            bool helmet = v.headgear == Headgear.SkullHelmet || v.headgear == Headgear.MedicHelmet;
            if (helmet)
            {
                var shell = new MeshBuilder();
                shell.Dome(new V3(0f, 0.118f, -0.005f), new V3(0, 0, 0), new V3(0.118f, 0.112f, 0.132f), HelmetCut, 0.011f, 30, 9, H(CharacterTextures.HCover), 0.45f);
                add(BoneId.Head, "casco", SlotHelmet, shell, true, default);
                var hw = new MeshBuilder();
                foreach (float sx in new[] { -1f, 1f })
                {
                    // rieles laterales (dos tramos siguiendo la curva) y orejeras con almohadilla
                    hw.Box(new V3(sx * 0.122f, 0.165f, 0.035f), new V3(0.012f, 0.016f, 0.07f), new V3(0, sx * 18f, sx * -10f), 0.003f, HS(CharacterTextures.HHardware, CharacterTextures.HwRail), 0.1f);
                    hw.Box(new V3(sx * 0.124f, 0.162f, -0.035f), new V3(0.012f, 0.016f, 0.07f), new V3(0, sx * -18f, sx * -10f), 0.003f, HS(CharacterTextures.HHardware, CharacterTextures.HwRail), 0.1f);
                    hw.Cylinder(new V3(sx * 0.118f, 0.098f, 0.0f), new V3(0, 0, 90f), 0.044f, 0.04f, 0.03f, 16, HS(CharacterTextures.HHardware, CharacterTextures.HwPolymer), 0.1f);
                    hw.Cylinder(new V3(sx * 0.1f, 0.098f, 0.0f), new V3(0, 0, 90f), 0.042f, 0.042f, 0.014f, 14, HS(CharacterTextures.HHardware, CharacterTextures.HwRubber), 0.1f);
                    hw.Box(new V3(sx * 0.125f, 0.135f, 0.0f), new V3(0.01f, 0.05f, 0.018f), new V3(0, 0, 0), 0.003f, HS(CharacterTextures.HHardware, CharacterTextures.HwPolymer), 0.08f);   // brazo de la orejera al riel
                }
                // micrófono: brazo desde la orejera izquierda hacia la boca (pasa por fuera de la placa)
                hw.Cylinder(new V3(-0.112f, 0.062f, 0.065f), new V3(-8f, 50f, 90f), 0.004f, 0.004f, 0.11f, 6, HS(CharacterTextures.HHardware, CharacterTextures.HwRubber), 0.12f);
                hw.Box(new V3(-0.078f, 0.05f, 0.112f), new V3(0.018f, 0.014f, 0.022f), new V3(0, 40f, 0), 0.004f, HS(CharacterTextures.HHardware, CharacterTextures.HwRubber), 0.04f);
                // montura de visión nocturna con el visor LEVANTADO contra la frente del casco
                hw.Box(new V3(0f, 0.196f, 0.108f), new V3(0.05f, 0.04f, 0.018f), new V3(-35f, 0, 0), 0.005f, HS(CharacterTextures.HHardware, CharacterTextures.HwPolymer), 0.08f);
                hw.Box(new V3(0f, 0.218f, 0.11f), new V3(0.03f, 0.03f, 0.04f), new V3(-50f, 0, 0), 0.005f, HS(CharacterTextures.HHardware, CharacterTextures.HwRail), 0.08f);
                foreach (float sx in new[] { -1f, 1f })
                {
                    hw.Cylinder(new V3(sx * 0.024f, 0.236f, 0.094f), new V3(-35f, 0, 0), 0.016f, 0.015f, 0.07f, 12, HS(CharacterTextures.HHardware, CharacterTextures.HwPolymer), 0.1f);
                    hw.Cylinder(new V3(sx * 0.024f, 0.265f, 0.073f), new V3(-35f, 0, 0), 0.014f, 0.014f, 0.004f, 12, HS(CharacterTextures.HHardware, CharacterTextures.HwLens), 0.05f);
                }
                add(BoneId.Head, "herrajes", SlotHelmet, hw, false, default);
            }

            switch (v.headgear)
            {
                case Headgear.SkullHelmet:
                {
                    var plate = new MeshBuilder();
                    plate.Append(MeshBuilder.Sheet((s, t, side) => SkullFace.Surface(s, t, side, HeadRx, HeadRz, HeadCz), 28, 24, true),
                        new V3(0, 0, 0), Mat3.Identity, H(CharacterTextures.HSkull), 0f, exactUv: true);
                    add(BoneId.Head, "placa calavera", SlotHelmet, plate, true, default);
                    if (v.visor)
                    {
                        var visor = new MeshBuilder();
                        visor.Append(MeshBuilder.Sheet((s, t, side) => SkullFace.Point(0.17f + s * 0.66f, 0.58f + t * 0.3f, 0.01f, HeadRx, HeadRz, HeadCz), 20, 8, false),
                            new V3(0, 0, 0), Mat3.Identity, HS(CharacterTextures.HHardware, CharacterTextures.HwLens), 0f, exactUv: true);
                        add(BoneId.Head, "visor", SlotVisor, visor, false, default);
                    }
                    break;
                }
                case Headgear.CapGoggles:
                {
                    var cap = new MeshBuilder();
                    cap.Dome(new V3(0f, 0.135f, -0.002f), new V3(0, 0, 0), new V3(0.108f, 0.085f, 0.118f), lon => 0f, 0.008f, 20, 6, HS(CharacterTextures.HHeadwear, CharacterTextures.HwCap), 0.3f);
                    cap.Box(new V3(0f, 0.14f, 0.13f), new V3(0.16f, 0.008f, 0.08f), new V3(-8f, 0, 0), 0.003f, HS(CharacterTextures.HHeadwear, CharacterTextures.HwCap), 0.2f);   // visera
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        cap.Cylinder(new V3(sx * 0.108f, 0.098f, 0.0f), new V3(0, 0, 90f), 0.042f, 0.038f, 0.028f, 14, HS(CharacterTextures.HHardware, CharacterTextures.HwPolymer), 0.1f);   // cascos de radio
                        cap.Box(new V3(sx * 0.1f, 0.17f, 0.0f), new V3(0.012f, 0.1f, 0.02f), new V3(0, 0, sx * -25f), 0.003f, HS(CharacterTextures.HHardware, CharacterTextures.HwPolymer), 0.1f);
                    }
                    cap.Box(new V3(0f, 0.228f, 0f), new V3(0.1f, 0.012f, 0.022f), new V3(0, 0, 0), 0.004f, HS(CharacterTextures.HHardware, CharacterTextures.HwPolymer), 0.1f);   // diadema
                    add(BoneId.Head, "gorra", SlotHelmet, cap, true, default);
                    Goggles(add, 0f);
                    Bandana(add);
                    break;
                }
                case Headgear.MedicHelmet:
                {
                    Goggles(add, 0f);
                    Bandana(add);
                    var patch = new MeshBuilder();   // parches con la cruz propia a los lados y detrás del casco
                    foreach (float lon in new[] { -90f, 90f, 180f })
                    {
                        double a = lon * Math.PI / 180.0;
                        V3 at = new V3((float)Math.Sin(a) * 0.112f, 0.17f, -0.005f + (float)Math.Cos(a) * 0.126f);
                        patch.Box(at, new V3(0.06f, 0.06f, 0.006f), new V3(-25f, lon, 0), 0.002f, G(CharacterTextures.GArmband), 0.08f);
                    }
                    add(BoneId.Head, "parches", SlotGear, patch, false, default);
                    break;
                }
                default:   // capucha con red y velo
                {
                    var hood = new MeshBuilder();
                    hood.Dome(new V3(0f, 0.11f, -0.012f), new V3(0, 0, 0), new V3(0.13f, 0.13f, 0.142f), lon => HoodCut(lon), 0.008f, 26, 9, HS(CharacterTextures.HHeadwear, CharacterTextures.HwNet), 0.4f);
                    add(BoneId.Head, "capucha", SlotHelmet, hood, true, default);
                    var veil = new MeshBuilder();
                    veil.Append(MeshBuilder.Sheet((s, t, side) =>
                    {
                        // velo: cuelga del borde de la capucha sobre la cara, con ondas
                        float wave = 0.004f * (float)Math.Sin(s * 23f) * (1f - t);
                        return SkullFace.Point(0.06f + s * 0.88f, -0.05f + t * 1.05f, 0.022f + wave + (side > 0 ? 0f : -0.003f), HeadRx, HeadRz, HeadCz);
                    }, 18, 8, true), new V3(0, 0, 0), Mat3.Identity, HS(CharacterTextures.HHeadwear, CharacterTextures.HwNet), 0f, exactUv: true);
                    add(BoneId.Head, "velo", SlotHelmet, veil, false, default);
                    break;
                }
            }
        }

        private static void Goggles(Action<BoneId, string, int, MeshBuilder, bool, V3> add, float lift)
        {
            var g = new MeshBuilder();
            g.Lathe(new V3(0f, 0.128f + lift, HeadCz), new V3(0, 0, 0), new[] { -0.011f, HeadRx + 0.011f, 0.011f, HeadRx + 0.011f }, 18, HeadRz / HeadRx, true, true,
                HS(CharacterTextures.HHeadwear, CharacterTextures.HwStrap), 0.4f);
            g.Box(new V3(0f, 0.128f + lift, 0.118f), new V3(0.15f, 0.05f, 0.016f), new V3(0, 0, 0), 0.008f, HS(CharacterTextures.HHardware, CharacterTextures.HwPolymer), 0.15f);
            foreach (float sx in new[] { -1f, 1f })
                g.Box(new V3(sx * 0.036f, 0.128f + lift, 0.127f), new V3(0.058f, 0.036f, 0.006f), new V3(0, sx * 10f, 0), 0.003f, HS(CharacterTextures.HHardware, CharacterTextures.HwLens), 0.06f);
            add(BoneId.Head, "gafas", SlotHelmet, g, false, default);
        }

        private static void Bandana(Action<BoneId, string, int, MeshBuilder, bool, V3> add)
        {
            var b = new MeshBuilder();
            b.Append(MeshBuilder.Sheet((s, t, side) =>
            {
                // pañuelo: de la nariz al mentón, con la punta colgando por delante
                float tip = (1f - Math.Abs(s * 2f - 1f)) * 0.25f;
                return SkullFace.Point(0.04f + s * 0.92f, -tip + t * (0.55f + tip), side > 0 ? 0.01f : 0.006f, HeadRx, HeadRz, HeadCz);
            }, 16, 6, true), new V3(0, 0, 0), Mat3.Identity, HS(CharacterTextures.HHeadwear, CharacterTextures.HwBandana), 0f, exactUv: true);
            add(BoneId.Head, "pañuelo", SlotHelmet, b, false, default);
        }

        /// <summary>Corte del casco tipo FAST (latitud mínima según la longitud): sobre la ceja delante, muy alto sobre las orejas, bajo detrás.</summary>
        public static float HelmetCut(float lonDeg)
        {
            float a = Math.Abs(lonDeg);
            if (a < 55f) return Lerp(17f, 12f, a / 55f);
            if (a < 95f) return Lerp(12f, 24f, (a - 55f) / 40f);     // corte alto sobre la oreja
            if (a < 130f) return Lerp(24f, -8f, (a - 95f) / 35f);
            return Lerp(-8f, -18f, (a - 130f) / 50f);
        }

        /// <summary>Capucha: abierta en la cara, cae por los lados y cubre la nuca.</summary>
        public static float HoodCut(float lonDeg)
        {
            float a = Math.Abs(lonDeg);
            if (a < 50f) return 22f;
            if (a < 110f) return Lerp(22f, -25f, (a - 50f) / 60f);
            return Lerp(-25f, -45f, (a - 110f) / 70f);
        }

        /// <summary>kt de la Calavera con las proporciones por defecto (bulk 1.13 x 1.04 x limbThickness 1.0): las medidas literales de brazos y piernas son para ese valor.</summary>
        public const float ReferenceKt = 1.1752f;

        /// <summary>
        /// Anillos de un loft de extremidad con el pivote ARRIBA (en la articulación): grupos (t 0..1, radio X, radio Z, desplazamiento X, desplazamiento Z) de arriba a abajo,
        /// devueltos de abajo arriba con y = -t * largo. Las medidas se escalan por "scale", salvo los primeros "fixedFirst" y los últimos "fixedLast" anillos (junto a la rodilla: absolutos).
        /// </summary>
        public static float[] LoftRings(float length, float scale, int fixedFirst, int fixedLast, params float[] g)
        {
            int n = g.Length / 5; var r = new float[n * 5];
            for (int i = 0; i < n; i++)
            {
                bool abs = i >= n - fixedLast || i < fixedFirst;
                float sc = abs ? 1f : scale;
                int dst = (n - 1 - i) * 5;
                r[dst] = -g[i * 5] * length; r[dst + 1] = g[i * 5 + 1] * sc; r[dst + 2] = g[i * 5 + 2] * sc; r[dst + 3] = g[i * 5 + 3] * sc; r[dst + 4] = g[i * 5 + 4] * sc;
            }
            return r;
        }

        /// <summary>Anillos simples (y, radio X, radio Z) de un abultamiento: (alto, radio, y central, radio centro...) -> loft sin desplazamientos. Pares (y, rx, rz).</summary>
        public static float[] Rings(params float[] yRxRz)
        {
            int n = yRxRz.Length / 3; var r = new float[n * 5];
            for (int i = 0; i < n; i++) { r[i * 5] = yRxRz[i * 3]; r[i * 5 + 1] = yRxRz[i * 3 + 1]; r[i * 5 + 2] = yRxRz[i * 3 + 2]; }
            return r;
        }

        /// <summary>Perfil de un segmento de extremidad con el pivote ARRIBA (en la articulación) y que baja hasta "length": anillos (t, radio).</summary>
        public static float[] Limb(float length, float[] rings)
        {
            int n = rings.Length / 2;
            var p = new float[n * 2];
            for (int i = 0; i < n; i++) { int src = (n - 1 - i) * 2; p[i * 2] = -rings[src] * length; p[i * 2 + 1] = rings[src + 1]; }
            return p;
        }

        /// <summary>Perfil de una esfera (para tornos): "rings" anillos entre los polos.</summary>
        public static float[] Sphere(float r, int rings) => Ellipsoid(r, r, rings);

        public static float[] Ellipsoid(float rxz, float ry, int rings)
        {
            var p = new float[(rings + 1) * 2];
            for (int i = 0; i <= rings; i++)
            {
                double lat = -Math.PI / 2 + Math.PI * (0.04 + 0.92 * i / rings);
                p[i * 2] = (float)Math.Sin(lat) * ry; p[i * 2 + 1] = (float)Math.Cos(lat) * rxz;
            }
            return p;
        }

        public static int Triangles(List<ModelPiece> pieces) { int n = 0; foreach (var p in pieces) n += p.Mesh.Triangles.Count / 3; return n; }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0f, Math.Min(1f, t));
    }
}
