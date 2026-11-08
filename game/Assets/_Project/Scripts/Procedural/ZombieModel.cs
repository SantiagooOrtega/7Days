using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Una pieza del cuerpo del zombi: malla en el espacio de su pivote, hueso (o hueso extra) al que se ata, material (piel o ropa) y si se mezcla con el hueso vecino en las articulaciones.</summary>
    public struct ZombiePiece
    {
        public BoneId Bone;
        public int Extra;          // >= 0: índice de un hueso extra (vientre, bolsas) en lugar de Bone
        public string Name;
        public int Slot;           // ZombieModel.SlotSkin | SlotCloth
        public MeshData Mesh;
        public V3 Pivot;
        public bool Blend;         // piel y ropa de torso y extremidades: se doblan suave en codos, rodillas y columna
        public bool Shadow;
    }

    /// <summary>Hueso extra que no mueve el ProceduralRig (se hereda de un hueso del esqueleto): lo usa el vientre del explosivo para inflarse.</summary>
    public struct ZombieExtraBone
    {
        public string Name;
        public BoneId Parent;
        public V3 LocalPos;
    }

    public class ZombieModelResult
    {
        public BoneDef[] Spec;
        public readonly List<ZombiePiece> Pieces = new List<ZombiePiece>();
        public readonly List<ZombieExtraBone> Extras = new List<ZombieExtraBone>();
        public FootShape Foot;

        public int Triangles(int slot = -1)
        {
            int n = 0;
            foreach (var p in Pieces) if (slot < 0 || p.Slot == slot) n += p.Mesh.Triangles.Count / 3;
            return n;
        }
    }

    /// <summary>
    /// Modelo del zombi (puro): cuerpo anatómico con el mismo esqueleto del soldado (HumanoidRigSpec) y las proporciones de cada tipo. Piel: torso con caja torácica y
    /// músculos, cuello con tendones, cráneo con pómulos y cuenca, mandíbula (entera, rota o expuesta), dientes, ojos con velo, orejas, pelo (calvo, mechones o apelmazado),
    /// brazos, manos con dedos largos y uñas rotas, piernas y pies descalzos o con botas rotas. Ropa: camisa, uniforme, bata, traje, mono o harapos (con agujeros por alfa
    /// en la textura). Los rasgos propios de cada tipo (placas, bolsas, vientre, armadura) se añaden aparte. Tres niveles de detalle (LOD 0 completo, 1 medio, 2 lejano).
    /// Presupuesto de triángulos: ver <see cref="Budget"/>.
    /// </summary>
    public static class ZombieModel
    {
        public const int SlotSkin = 0, SlotCloth = 1;
        public const int CommonBudget = 6000, HeavyBudget = 12000;

        public static int Budget(string typeId) => typeId == ZombieIds.Tank ? HeavyBudget : CommonBudget;

        /// <summary>Índice global de un hueso extra (después de los 22 del esqueleto).</summary>
        public static int ExtraBoneIndex(int extra) => HumanoidRigSpec.BoneCount + extra;

        public static ZombieModelResult Build(ZombieAnatomy a, ZombieVariantData v, int lod)
        {
            var b = new Builder(a, v, lod);
            b.Run();
            return b.Result;
        }

        internal sealed class Builder
        {
            private readonly ZombieAnatomy _a;
            private readonly ZombieVariantData _v;
            private readonly int _lod;
            private readonly RigProportions _pr;
            private readonly BoneDef[] _spec;
            private readonly float _s, _k, _d, _em, _f, _tw, _hs;
            public readonly ZombieModelResult Result = new ZombieModelResult();

            public Builder(ZombieAnatomy a, ZombieVariantData v, int lod)
            {
                _a = a; _v = v; _lod = Math.Max(0, Math.Min(2, lod));
                _pr = a.Proportions;
                _spec = HumanoidRigSpec.Build(_pr);
                Result.Spec = _spec;
                _s = a.Size; _k = _pr.bulk; _d = _pr.torsoDepth; _em = a.Emaciation;
                float kt = _k * _pr.limbThickness;
                _f = kt * _s / SoldierModel.ReferenceKt * (1f - 0.2f * _em);   // radios de brazos y piernas (las tablas son del soldado)
                _tw = 1f - 0.1f * _em;                                          // anchura del torso
                _hs = _pr.headSize * _s;                                        // tamaño de la cabeza
            }


            // loft con anillos reducidos en el LOD 2 (cada dos anillos interiores) y los lados del nivel de detalle
            private void Lf(MeshBuilder mb, V3 center, V3 euler, float[] rings, int sides, AtlasCell cell, float cellMeters, bool fit = false)
            {
                if (_lod == 2 && rings.Length / 5 >= 5)
                {
                    var o = new List<float>(); int n = rings.Length / 5;
                    for (int i = 0; i < n; i++) if (i == 0 || i == n - 1 || i % 2 == 0) for (int c = 0; c < 5; c++) o.Add(rings[i * 5 + c]);
                    rings = o.ToArray();
                }
                mb.Loft(center, euler, rings, Sides(sides), true, true, cell, cellMeters, fit);
            }

            private float L(BoneId id) => _spec[(int)id].Length;
            private int Sides(int n) => _lod == 0 ? n : (_lod == 1 ? Math.Max(6, (int)(n * 0.6f)) : Math.Max(5, n / 2));
            private static AtlasCell SK(int id) => ZombieAtlas.Skin(id);
            private static AtlasCell CL(int id) => ZombieAtlas.Cloth(id);

            private void Add(BoneId bone, string name, int slot, MeshBuilder mb, int maxLod = 2, bool blend = false, bool shadow = true, V3 pivot = default, int extra = -1)
            {
                if (_lod > maxLod || mb.Data.Triangles.Count == 0) return;
                Result.Pieces.Add(new ZombiePiece { Bone = bone, Extra = extra, Name = bone + " " + name, Slot = slot, Mesh = mb.Data, Pivot = pivot, Blend = blend, Shadow = shadow });
            }

            // anillos del torso: grupos (y, ancho/k, fondo/ancho, cz/k) con las medidas del soldado; grow engorda (ropa)
            private float[] Torso(float grow, params float[] g)
            {
                int n = g.Length / 4; var r = new float[n * MeshBuilder.LoftStride];
                for (int i = 0; i < n; i++)
                {
                    float rx = g[i * 4 + 1] * _k * _s * _tw + grow;
                    r[i * 5] = g[i * 4]; r[i * 5 + 1] = rx; r[i * 5 + 2] = rx * g[i * 4 + 2] * _d; r[i * 5 + 3] = 0f; r[i * 5 + 4] = g[i * 4 + 3] * _k * _s;
                }
                return r;
            }

            /// <summary>Recorta un loft entre dos alturas interpolando los extremos (para mangas cortas, camisas que no llegan a la cintura...).</summary>
            private static float[] Slice(float[] rings, float y0, float y1)
            {
                var o = new List<float>();
                int n = rings.Length / 5;
                void Interp(float y)
                {
                    for (int i = 0; i < n - 1; i++)
                    {
                        float ya = rings[i * 5], yb = rings[(i + 1) * 5];
                        if (y >= ya && y <= yb)
                        {
                            float t = yb - ya > 1e-6f ? (y - ya) / (yb - ya) : 0f;
                            o.Add(y);
                            for (int c = 1; c < 5; c++) o.Add(rings[i * 5 + c] + (rings[(i + 1) * 5 + c] - rings[i * 5 + c]) * t);
                            return;
                        }
                    }
                    int e = y < rings[0] ? 0 : n - 1;
                    o.Add(y); for (int c = 1; c < 5; c++) o.Add(rings[e * 5 + c]);
                }
                Interp(y0);
                for (int i = 0; i < n; i++) if (rings[i * 5] > y0 + 1e-5f && rings[i * 5] < y1 - 1e-5f) for (int c = 0; c < 5; c++) o.Add(rings[i * 5 + c]);
                Interp(y1);
                return o.ToArray();
            }

            private static float[] Grow(float[] rings, float add, float mul = 1f)
            {
                var r = (float[])rings.Clone();
                for (int i = 0; i < r.Length / 5; i++) { r[i * 5 + 1] = r[i * 5 + 1] * mul + add; r[i * 5 + 2] = r[i * 5 + 2] * mul + add; }
                return r;
            }

            public void Run()
            {
                Torso();
                Arms();
                Legs();
                Head();
                Clothing();
                new ZombieTypeFeatures(this, _a, _v, _lod).Build();
            }

            // ================================================================ torso
            private void Torso()
            {
                float s = _s, k = _k;
                float sp = L(BoneId.Spine), ch = L(BoneId.Chest), uc = L(BoneId.UpperChest);
                float hipK = (_pr.hipWidth / s) / 0.27f;
                var pelvis = new MeshBuilder();
                Lf(pelvis, default, default, PelvisRings(0f, hipK), 12, SK(ZombieAtlas.SkBody), 0.5f);
                Add(BoneId.Hips, "pelvis", SlotSkin, pelvis, 2, true);

                var spine = new MeshBuilder();
                Lf(spine, default, default, Torso(0f, -0.02f * s, 0.158f, 0.66f, 0f, sp * 0.5f, 0.150f, 0.66f, 0.002f, sp * 1.08f, 0.156f, 0.65f, 0.004f), 12, SK(ZombieAtlas.SkBody), 0.5f);
                Add(BoneId.Spine, "torso", SlotSkin, spine, 2, true);

                var chest = new MeshBuilder();
                Lf(chest, default, default, ChestRings(0f), 12, SK(ZombieAtlas.SkBody), 0.5f);
                Add(BoneId.Chest, "torso", SlotSkin, chest, 2, true);

                var upper = new MeshBuilder();
                Lf(upper, default, default, UpperRings(0f), 12, SK(ZombieAtlas.SkBody), 0.5f);
                Add(BoneId.UpperChest, "torso", SlotSkin, upper, 2, true);
                // trapecio caído: elipsoide ancho y plano de la base del cuello al hombro
                var trap = new MeshBuilder();
                trap.Lathe(new V3(0f, uc * 0.88f, -0.012f * s), default, SoldierModel.Ellipsoid(0.16f * k * s * _tw, 0.05f * s, 4), Sides(12), 0.5f * _d / 0.9f, true, true, SK(ZombieAtlas.SkBody), 0.4f);
                Add(BoneId.UpperChest, "trapecio", SlotSkin, trap, 1);

                // cuello con tendones (esternocleidomastoideos) y la nuez
                float neck = L(BoneId.Neck);
                float nr = 0.052f * k * s * (1f - 0.15f * _em);
                var nk = new MeshBuilder();
                Lf(nk, default, default, new[] { -0.03f * s, nr * 1.5f, nr * 1.45f, 0f, 0f, 0.0f, nr * 1.15f, nr * 1.1f, 0f, 0.002f * s, neck * 0.5f, nr, nr * 0.98f, 0f, 0.004f * s, neck + 0.02f * s, nr * 0.95f, nr * 0.93f, 0f, 0.004f * s },
                    10, SK(ZombieAtlas.SkBody), 0.3f);
                Add(BoneId.Neck, "cuello", SlotSkin, nk, 2, true);
                var tendons = new MeshBuilder();
                foreach (float sx in new[] { -1f, 1f })
                    tendons.Loft(new V3(sx * nr * 0.55f, 0f, nr * 0.62f), new V3(0f, 0f, sx * -14f), new[] { 0.0f, 0.010f * s, 0.008f * s, 0f, 0f, neck * 0.5f, 0.012f * s, 0.009f * s, 0f, 0f, neck * 1.05f, 0.007f * s, 0.006f * s, 0f, 0f },
                        5, true, true, SK(ZombieAtlas.SkBody), 0.1f);
                Add(BoneId.Neck, "tendones", SlotSkin, tendons, 0);

                // columna marcada en los flacos: nudillos de vértebras a lo largo de la espalda
                if (_a.VisibleRibs && _lod == 0)
                {
                    var bones = new MeshBuilder();
                    for (int i = 0; i < 4; i++)
                        bones.Lathe(new V3(0f, (0.02f + i * 0.04f) * s, -(0.15f * k * s * _tw * 0.66f * _d) * 0.98f), default, SoldierModel.Ellipsoid(0.013f * s, 0.011f * s, 3), 6, 1.2f, true, true, SK(ZombieAtlas.SkBody), 0.05f);
                    Add(BoneId.Chest, "columna", SlotSkin, bones, 0);
                }
            }

            private float[] PelvisRings(float grow, float hipK)
            {
                float s = _s, k = _k;
                var g = new[] { -0.13f * s, 0.130f, 0.66f, -0.004f, -0.05f * s, 0.158f, 0.66f, -0.012f, 0.05f * s, 0.158f, 0.66f, -0.006f, 0.12f * s, 0.152f, 0.66f, -0.002f };
                var r = new float[(g.Length / 4) * 5];
                for (int i = 0; i < g.Length / 4; i++)
                {
                    float rx = g[i * 4 + 1] * k * s * hipK + grow;
                    r[i * 5] = g[i * 4]; r[i * 5 + 1] = rx; r[i * 5 + 2] = rx * g[i * 4 + 2] * _d; r[i * 5 + 4] = g[i * 4 + 3] * k * s;
                }
                return r;
            }

            private float[] ChestRings(float grow)
            {
                float s = _s, ch = L(BoneId.Chest);
                if (_a.VisibleRibs && _lod == 0)
                {
                    // caja torácica: costillas como relieve (rizado del radio cada ~3 cm) y vientre hundido por debajo
                    var o = new List<float>();
                    int n = 9;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)(n - 1);
                        float rib = 1f + 0.05f * _em * (float)Math.Sin(t * Math.PI * 4.0 + 0.5);
                        float w = (0.150f + 0.016f * t) * rib;
                        float y = -0.02f * s + (ch * 1.07f + 0.02f * s) * t;
                        float rx = w * _k * _s * _tw + grow;
                        o.Add(y); o.Add(rx); o.Add(rx * (0.66f + 0.02f * t) * _d); o.Add(0f); o.Add((0.003f + 0.006f * t) * _k * _s);
                    }
                    return o.ToArray();
                }
                return Torso(grow, -0.02f * s, 0.152f, 0.65f, 0.004f, ch * 0.5f, 0.158f, 0.68f, 0.008f, ch * 1.05f, 0.162f, 0.68f, 0.009f);
            }

            private float[] UpperRings(float grow)
            {
                float s = _s, uc = L(BoneId.UpperChest);
                return Torso(grow, -0.02f * s, 0.162f, 0.68f, 0.009f, uc * 0.35f, 0.163f, 0.66f, 0.008f, uc * 0.7f, 0.155f, 0.62f, 0.004f, uc * 0.92f, 0.118f, 0.6f, 0f, uc * 1.03f, 0.07f, 0.6f, 0f);
            }

            // ================================================================ brazos
            private float[] UpperArmRings(float len, float f) => SoldierModel.LoftRings(len, f, 0, 0,
                0.00f, 0.058f, 0.062f, 0f, 0f, 0.12f, 0.064f, 0.067f, 0f, 0f, 0.36f, 0.060f, 0.067f, 0f, 0.005f, 0.60f, 0.053f, 0.058f, 0f, 0.001f, 0.85f, 0.046f, 0.048f, 0f, -0.001f, 1.00f, 0.042f, 0.043f, 0f, 0f);

            private float[] LowerArmRings(float len, float f) => SoldierModel.LoftRings(len, f, 0, 0,
                0.00f, 0.044f, 0.045f, 0f, 0f, 0.14f, 0.052f, 0.054f, 0f, 0.002f, 0.40f, 0.048f, 0.050f, 0f, 0.001f, 0.75f, 0.037f, 0.038f, 0f, 0f, 1.00f, 0.031f, 0.032f, 0f, 0f);

            private float ArmScale(bool left) => (_a.BigArmScale > 1f && (_a.BigArmSide < 0) == left) ? _a.BigArmScale : 1f;
            private float ArmExtra(bool left) => (_a.LongArmExtra > 0f && !left) ? _a.LongArmExtra * _s : 0f;

            private void Arms()
            {
                foreach (bool left in new[] { true, false })
                {
                    float side = left ? -1f : 1f, g = ArmScale(left), f = _f * g, ex = ArmExtra(left);
                    BoneId ua = left ? BoneId.LeftUpperArm : BoneId.RightUpperArm, la = left ? BoneId.LeftLowerArm : BoneId.RightLowerArm, hand = left ? BoneId.LeftHand : BoneId.RightHand;
                    float lu = L(ua), ll = L(la);
                    var up = new MeshBuilder();
                    Lf(up, default, default, UpperArmRings(lu, f), 10, SK(ZombieAtlas.SkBody), 0.5f);
                    Add(ua, "brazo", SlotSkin, up, 2, true);
                    var delt = new MeshBuilder();
                    delt.Lathe(new V3(side * 0.012f * f, 0.004f, 0f), default, SoldierModel.Ellipsoid(0.066f * f, 0.06f * f, 4), Sides(10), 1.04f, true, true, SK(ZombieAtlas.SkBody), 0.3f);
                    Add(ua, "deltoides", SlotSkin, delt, 0, false, false);
                    var lo = new MeshBuilder();
                    Lf(lo, default, default, LowerArmRings(ll + ex, f), 10, SK(ZombieAtlas.SkBody), 0.5f);
                    Add(la, "antebrazo", SlotSkin, lo, 2, true);
                    // codo huesudo
                    var elbow = new MeshBuilder();
                    elbow.Lathe(new V3(0f, -0.004f * f, -0.028f * f), default, SoldierModel.Ellipsoid(0.026f * f, 0.024f * f, 3), 6, 1f, true, true, SK(ZombieAtlas.SkBody), 0.1f);
                    Add(la, "codo", SlotSkin, elbow, 0, false, false);
                    Hand(hand, left, f, ex);
                }
            }

            // mano con dedos largos y uñas rotas: palma plana, cuatro dedos con la punta curvada hacia la palma, pulgar
            private void Hand(BoneId bone, bool left, float f, float extra)
            {
                float k = Math.Max(0.7f, Math.Min(1.7f, f / 0.8f)) * _s / Math.Max(0.8f, _s);   // el tamaño de la mano sigue al del brazo
                k = Math.Max(0.7f, Math.Min(2.4f, k));
                float thumb = left ? -1f : 1f;
                float pl = HandWrap.PalmLength * k * 0.95f, pw = HandWrap.PalmWidth * k * 0.98f, pt = HandWrap.PalmThickness * k * 0.85f;
                var h = new MeshBuilder();
                // palma: loft corto de arriba abajo (la palma mira a -X)
                Lf(h, new V3(0f, -extra, 0f), default, new[] { -pl, pt * 0.42f, pw * 0.5f, 0f, 0f, -pl * 0.55f, pt * 0.5f, pw * 0.53f, 0f, 0f, 0f, pt * 0.42f, pw * 0.45f, 0f, 0f }, 8, SK(ZombieAtlas.SkHand), 0.12f);
                Add(bone, "mano", SlotSkin, h, 2, false, false);
                if (_lod >= 2) return;
                var fingers = new MeshBuilder();
                float[] z = HandWrap.Z, l1 = HandWrap.L1, l2 = HandWrap.L2;
                for (int i = 0; i < 4; i++)
                {
                    float len = (l1[i] + l2[i] + 0.016f) * k * 1.12f;   // dedos largos y finos
                    float r = (i == 3 ? 0.0075f : 0.0088f) * k;
                    // loft de la punta (arriba) a la base: y negativa hacia las puntas; se curvan hacia la palma (-X)
                    float[] rings = { -len, r * 0.55f, r * 0.55f, -0.026f * k, 0f, -len * 0.45f, r * 0.85f, r * 0.85f, -0.012f * k, 0f, 0f, r, r, 0f, 0f };
                    fingers.Loft(new V3(0f, -pl - extra, z[i] * thumb * k), default, rings, 5, true, true, SK(ZombieAtlas.SkHand), 0.05f);
                    if (_lod == 0 && i % 2 == 0)
                    {
                        // uña rota: una esquirla oscura en la punta
                        fingers.Box(new V3(-0.027f * k - r * 0.4f, -pl - extra - len * 0.97f, z[i] * thumb * k), new V3(0.003f * k, 0.011f * k, r * 1.1f), new V3(0f, 0f, 14f * (i % 2 == 0 ? 1 : -1)), 0f, SK(ZombieAtlas.SkBone), 0.04f);
                    }
                }
                // pulgar
                fingers.Loft(new V3(-0.006f * k, -0.024f * k - extra, 0.04f * thumb * k), new V3(0f, 0f, 0f), new[] { -0.058f * k, 0.0065f * k, 0.0065f * k, -0.016f * k, 0.014f * k * thumb, -0.03f * k, 0.008f * k, 0.008f * k, -0.008f * k, 0.008f * k * thumb, 0f, 0.0095f * k, 0.0095f * k, 0f, 0f }, 5, true, true, SK(ZombieAtlas.SkHand), 0.05f);
                Add(bone, "dedos", SlotSkin, fingers, 1, false, false);
            }

            // ================================================================ piernas
            private float[] ThighRings(float len, float f) => SoldierModel.LoftRings(len, f, 0, 2,
                0.00f, 0.083f, 0.090f, 0f, -0.010f, 0.14f, 0.083f, 0.091f, 0f, -0.004f, 0.38f, 0.079f, 0.088f, 0f, 0.006f, 0.62f, 0.070f, 0.076f, 0f, 0.008f, 0.86f, 0.056f * 0.9f, 0.058f * 0.9f, 0f, 0.002f, 1.00f, 0.052f * 0.9f, 0.053f * 0.9f, 0f, 0f);

            private float[] CalfRings(float len, float f) => SoldierModel.LoftRings(len, f, 1, 0,
                0.00f, 0.052f * 0.9f, 0.053f * 0.9f, 0f, 0f, 0.20f, 0.062f, 0.068f, 0f, -0.012f, 0.38f, 0.066f, 0.073f, 0f, -0.012f, 0.62f, 0.052f, 0.054f, 0f, -0.006f, 0.85f, 0.040f, 0.041f, 0f, 0f, 1.00f, 0.036f, 0.038f, 0f, 0f);

            private void Legs()
            {
                foreach (bool left in new[] { true, false })
                {
                    float side = left ? -1f : 1f;
                    BoneId ul = left ? BoneId.LeftUpperLeg : BoneId.RightUpperLeg, ll = left ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg;
                    BoneId foot = left ? BoneId.LeftFoot : BoneId.RightFoot, toes = left ? BoneId.LeftToes : BoneId.RightToes;
                    float thigh = L(ul), shin = L(ll), drop = L(foot), ball = _spec[(int)toes].LocalPosition.Z;
                    var th = new MeshBuilder();
                    Lf(th, default, default, ThighRings(thigh, _f), 10, SK(ZombieAtlas.SkBody), 0.5f);
                    Add(ul, "muslo", SlotSkin, th, 2, true);
                    var calf = new MeshBuilder();
                    Lf(calf, default, default, CalfRings(shin, _f), 10, SK(ZombieAtlas.SkBody), 0.5f);
                    Add(ll, "pantorrilla", SlotSkin, calf, 2, true);
                    // rótula huesuda
                    var knee = new MeshBuilder();
                    knee.Lathe(new V3(0f, 0f, 0.012f * _s), default, SoldierModel.Ellipsoid(0.04f * _s * (1f - 0.1f * _em), 0.04f * _s, 3), Sides(8), 1f, true, true, SK(ZombieAtlas.SkBody), 0.1f);
                    Add(ll, "rotula", SlotSkin, knee, 0, false, false);
                    Foot(foot, toes, left, drop, ball);
                }
            }

            // pie descalzo (o bota rota, que añade la ropa): empeine, talón y dedos; el origen del hueso del pie es el tobillo
            private void Foot(BoneId foot, BoneId toes, bool left, float drop, float ball)
            {
                float s = _s, w = 0.052f * s * (0.8f + 0.2f * _k);
                float heel = 0.065f * s, h = drop * 0.9f;
                var f = new MeshBuilder();
                // loft a lo largo de +Z (giro de 90 grados sobre X: el eje Y del loft pasa a +Z y el Z a -Y): talón detrás, bola delante; la suela queda en el origen + (-drop)
                Lf(f, new V3(0f, -drop, 0f), new V3(90f, 0f, 0f), new[] { -heel, w * 0.5f, h * 0.4f, 0f, -h * 0.4f, -heel * 0.45f, w * 0.78f, h * 0.5f, 0f, -h * 0.5f, ball * 0.15f, w * 0.92f, h * 0.5f, 0f, -h * 0.5f, ball, w, h * 0.32f, 0f, -h * 0.32f },
                    10, SK(ZombieAtlas.SkFoot), 0.2f);
                Add(foot, "pie", SlotSkin, f, 2, false, true);
                var t = new MeshBuilder();
                float toeLen = 0.075f * s;
                Lf(t, default, new V3(90f, 0f, 0f), new[] { -0.005f * s, w, 0.02f * s, 0f, -0.02f * s, toeLen * 0.55f, w * 0.9f, 0.016f * s, 0f, -0.016f * s, toeLen, w * 0.7f, 0.012f * s, 0f, -0.012f * s }, 8, SK(ZombieAtlas.SkFoot), 0.15f);
                Add(toes, "dedos", SlotSkin, t, 2, false, false);
            }

            // ================================================================ cabeza
            public const float HeadRx = SoldierModel.HeadRx, HeadRy = SoldierModel.HeadRy, HeadRz = SoldierModel.HeadRz;

            private void Head()
            {
                float h = _hs;
                var style = _v.head;
                // cráneo: pómulos, cuenca y mejillas hundidas según el estilo
                bool sunken = style == ZombieHeadStyle.Sunken;
                float cheek = sunken ? 0.84f : 1f;
                float[] sk = {
                    0.012f * h, 0.046f * h * cheek, 0.052f * h, 0f, 0.02f * h,
                    0.035f * h, 0.074f * h * cheek, 0.08f * h, 0f, 0.02f * h,
                    0.060f * h, 0.085f * h * cheek, 0.095f * h, 0f, 0.015f * h,
                    0.090f * h, 0.095f * h, 0.105f * h, 0f, 0.010f * h,
                    0.120f * h, 0.100f * h, 0.110f * h, 0f, 0.008f * h,
                    0.150f * h, 0.100f * h, 0.112f * h, 0f, 0.000f * h,
                    0.180f * h, 0.088f * h, 0.100f * h, 0f, -0.004f * h,
                    0.205f * h, 0.055f * h, 0.068f * h, 0f, -0.006f * h,
                    0.218f * h, 0.020f * h, 0.030f * h, 0f, -0.006f * h };
                var skull = new MeshBuilder();
                Lf(skull, default, default, sk, 16, SK(ZombieAtlas.SkHead), 0f, true);
                Add(BoneId.Head, "craneo", SlotSkin, skull, 2);
                // ojos con velo lechoso en las cuencas
                var eyes = new MeshBuilder();
                foreach (float sx in new[] { -1f, 1f })
                    eyes.Lathe(new V3(sx * 0.036f * h, 0.118f * h, 0.082f * h), new V3(90f, 0f, 0f), SoldierModel.Ellipsoid(0.014f * h, 0.012f * h, 3), Sides(7), 1f, true, true, SK(ZombieAtlas.SkEye), 0f, true);
                Add(BoneId.Head, "ojos", SlotSkin, eyes, 2, false, false);
                // ceja marcada y nariz hundida
                var brow = new MeshBuilder();
                brow.Loft(new V3(0f, 0.138f * h, 0.098f * h), new V3(0f, 0f, 90f), new[] { -0.07f * h, 0.012f * h, 0.012f * h, 0f, 0f, 0f, 0.016f * h, 0.015f * h, 0f, 0.002f * h, 0.07f * h, 0.012f * h, 0.012f * h, 0f, 0f }, 6, true, true, SK(ZombieAtlas.SkBody), 0.1f);
                Add(BoneId.Head, "ceja", SlotSkin, brow, 1, false, false);
                var nose = new MeshBuilder();
                nose.Loft(new V3(0f, 0.07f * h, 0.098f * h), new V3(-24f, 0f, 0f), new[] { -0.02f * h, 0.014f * h, 0.012f * h, 0f, 0f, 0.0f, 0.011f * h, 0.012f * h, 0f, 0.002f * h, 0.03f * h, 0.006f * h, 0.006f * h, 0f, 0.004f * h }, 6, true, true, SK(ZombieAtlas.SkBody), 0.1f);
                Add(BoneId.Head, "nariz", SlotSkin, nose, 0, false, false);
                var ears = new MeshBuilder();
                foreach (float sx in new[] { -1f, 1f })
                    ears.Lathe(new V3(sx * 0.1f * h, 0.098f * h, -0.004f * h), new V3(0f, 0f, 90f), SoldierModel.Ellipsoid(0.026f * h, 0.012f * h, 2), 5, 0.6f, true, true, SK(ZombieAtlas.SkBody), 0.08f);
                Add(BoneId.Head, "orejas", SlotSkin, ears, 0, false, false);
                Jaw(style, h);
                Hair(h);
            }

            // mandíbula: entera con la boca entreabierta, rota (colgando a un lado) o expuesta (hueso y dientes a la vista, sin mejilla)
            private void Jaw(ZombieHeadStyle style, float h)
            {
                bool broken = style == ZombieHeadStyle.JawBroken, exposed = style == ZombieHeadStyle.JawExposed;
                float open = broken ? 24f : (exposed ? 14f : 6f);
                float yaw = broken ? 11f : (exposed ? -6f : 0f);
                float skinR = exposed ? 0f : 1f;
                // la mandíbula gira desde la bisagra cerca de las orejas (y=0.075, z=0.0)
                float[] ring = { -0.050f * h, 0.036f * h, 0.030f * h, 0f, 0.058f * h, -0.020f * h, 0.064f * h, 0.050f * h, 0f, 0.034f * h, 0.010f * h, 0.074f * h, 0.075f * h, 0f, 0.012f * h, 0.036f * h, 0.070f * h, 0.082f * h, 0f, 0.006f * h };
                var jaw = new MeshBuilder();
                int cell = exposed ? ZombieAtlas.SkBone : ZombieAtlas.SkBody;
                Lf(jaw, new V3(0f, 0.012f * h, 0.004f * h), new V3(open, yaw, 0f), ring, 12, SK(cell), exposed ? 0.1f : 0.35f);
                Add(BoneId.Head, "mandibula", SlotSkin, jaw, 2, false, true);
                if (_lod > 0) return;
                // dientes: fila superior fija y fila inferior pegada a la mandíbula (cajas finas)
                var teeth = new MeshBuilder();
                for (int i = -2; i <= 2; i++)
                {
                    float x = i * 0.0125f * h, z = 0.082f * h - Math.Abs(i) * 0.0022f * h;
                    teeth.Box(new V3(x, 0.066f * h, z), new V3(0.0105f * h, 0.013f * h - Math.Abs(i) * 0.0008f * h, 0.006f * h), new V3(0f, i * 6f, i % 2 == 0 ? 2f : -3f), 0f, SK(ZombieAtlas.SkBone), 0.03f);
                }
                Add(BoneId.Head, "dientes", SlotSkin, teeth, 0, false, false);
                var lowerTeeth = new MeshBuilder();
                for (int i = -2; i <= 2; i++)
                {
                    float x = i * 0.0125f * h, z = 0.081f * h - Math.Abs(i) * 0.0022f * h;
                    if (broken && i == 1) continue;     // dientes que faltan
                    lowerTeeth.Box(new V3(x, 0.032f * h, z), new V3(0.01f * h, 0.011f * h, 0.0055f * h), new V3(0f, i * 6f, 0f), 0f, SK(ZombieAtlas.SkBone), 0.03f);
                }
                                // los dientes de abajo siguen a la mandíbula: se giran con ella alrededor de la bisagra
                Result.Pieces.Add(new ZombiePiece { Bone = BoneId.Head, Extra = -1, Name = "Head dientes inferiores", Slot = SlotSkin, Mesh = Rotated(lowerTeeth.Data, new V3(0f, 0.012f * h, 0.004f * h), new V3(open, yaw, 0f)), Pivot = default, Blend = false, Shadow = false });
            }

            private static MeshData Rotated(MeshData m, V3 pos, V3 euler)
            {
                var r = Mat3.Euler(euler); var o = new MeshData();
                for (int i = 0; i < m.Positions.Count; i++)
                {
                    o.Positions.Add(pos + r.Mul(m.Positions[i])); o.Normals.Add(r.Mul(m.Normals[i]).Normalized); o.Tangents.Add(r.Mul(m.Tangents[i]).Normalized);
                    o.TangentW.Add(m.WOf(i)); o.Uvs.Add(m.Uvs[i]);
                }
                foreach (int t in m.Triangles) o.Triangles.Add(t);
                return o;
            }

            private void Hair(float h)
            {
                if (_v.hair == ZombieHairStyle.Bald) return;
                var hair = new MeshBuilder();
                var r = new Random(_v.seed * 31 + 7);
                float F() => (float)r.NextDouble();
                if (_v.hair == ZombieHairStyle.Tufts)
                {
                    // mechones sueltos sobre un cuero cabelludo con calvas
                    int n = _lod == 0 ? 9 : 5;
                    for (int i = 0; i < n; i++)
                    {
                        double lon = (F() * 300.0 - 150.0) * Math.PI / 180.0;
                        float lat = 35f + F() * 50f;
                        double la = lat * Math.PI / 180.0;
                        V3 at = new V3((float)(Math.Cos(la) * Math.Sin(lon)) * 0.098f * h, 0.106f * h + (float)Math.Sin(la) * 0.108f * h, (float)(Math.Cos(la) * Math.Cos(lon)) * 0.108f * h - 0.004f * h);
                        hair.Lathe(at, new V3(-(float)(lat * 0.6), (float)(lon * 180.0 / Math.PI), 0f), SoldierModel.Ellipsoid(0.014f * h, (0.03f + 0.03f * F()) * h, 3), 5, 0.7f, true, true, SK(ZombieAtlas.SkHair), 0.06f);
                    }
                }
                else
                {
                    // pelo largo apelmazado: casquete sobre cráneo y nuca y mechones que cuelgan hasta los hombros
                    hair.Dome(new V3(0f, 0.112f * h, -0.006f * h), default, new V3(0.108f * h, 0.112f * h, 0.118f * h), lon => 18f + 6f * (float)Math.Cos(lon * Math.PI / 180.0 * 2.0), 0.008f * h, Sides(12), Sides(4), SK(ZombieAtlas.SkHair), 0.3f);
                    int n = _lod == 0 ? 5 : (_lod == 1 ? 3 : 0);
                    for (int i = 0; i < n; i++)
                    {
                        double lon = (110.0 + i * (140.0 / Math.Max(1, n - 1))) * (i % 2 == 0 ? 1 : -1) * Math.PI / 180.0;
                        float len = (0.1f + 0.1f * F()) * h;
                        V3 at = new V3((float)Math.Sin(lon) * 0.092f * h, 0.08f * h, (float)Math.Cos(lon) * 0.1f * h - 0.004f * h);
                        hair.Loft(at, default, new[] { -len, 0.004f * h, 0.006f * h, (F() - 0.5f) * 0.016f * h, (F() - 0.5f) * 0.01f * h, -len * 0.5f, 0.009f * h, 0.012f * h, 0f, 0f, 0.0f, 0.012f * h, 0.016f * h, 0f, 0f }, 5, true, true, SK(ZombieAtlas.SkHair), 0.08f);
                    }
                }
                Add(BoneId.Head, "pelo", SlotSkin, hair, 2, false, false);
            }

            // ================================================================ ropa
            private void Clothing()
            {
                var c = _v.clothing;
                int top = ZombieAtlas.TopCell(c), bottom = ZombieAtlas.BottomCell(c);
                float s = _s, sp = L(BoneId.Spine), ch = L(BoneId.Chest), uc = L(BoneId.UpperChest);
                float hipK = (_pr.hipWidth / s) / 0.27f;
                bool rags = c == ZombieClothing.Rags, longSleeves = c == ZombieClothing.Military || c == ZombieClothing.LabCoat || c == ZombieClothing.Suit || c == ZombieClothing.Workwear;
                float grow = 0.006f * s;

                // torso: camisa, chaqueta o mono; los harapos solo cubren el pecho y cuelgan rotos
                var chestCloth = new MeshBuilder(); var upperCloth = new MeshBuilder(); var spineCloth = new MeshBuilder(); var pelvisCloth = new MeshBuilder();
                float[] spR = Torso(grow, -0.02f * s, 0.158f, 0.66f, 0f, sp * 0.5f, 0.150f, 0.66f, 0.002f, sp * 1.08f, 0.156f, 0.65f, 0.004f);
                if (rags) spR = Slice(spR, sp * 0.4f, sp * 1.08f);
                Lf(spineCloth, default, default, spR, 12, CL(top), 0.45f);
                Add(BoneId.Spine, "prenda", SlotCloth, spineCloth, 2, true);
                Lf(chestCloth, default, default, ChestRings(grow), 12, CL(top), 0.45f);
                Add(BoneId.Chest, "prenda", SlotCloth, chestCloth, 2, true);
                Lf(upperCloth, default, default, UpperRings(grow), 12, CL(top), 0.45f);
                Add(BoneId.UpperChest, "prenda", SlotCloth, upperCloth, 2, true);
                if (!rags)
                {
                    Lf(pelvisCloth, default, default, PelvisRings(grow, hipK), 12, CL(bottom), 0.45f);
                    Add(BoneId.Hips, "pantalon", SlotCloth, pelvisCloth, 2, true);
                }

                foreach (bool left in new[] { true, false })
                {
                    float g = ArmScale(left), f = _f * g;
                    BoneId ua = left ? BoneId.LeftUpperArm : BoneId.RightUpperArm, la = left ? BoneId.LeftLowerArm : BoneId.RightLowerArm;
                    float lu = L(ua), ll = L(la);
                    float[] uaR = Grow(UpperArmRings(lu, f), grow * 1.1f, 1.04f);
                    if (rags && !left) goto legs;                          // harapos: solo la manga izquierda, rota
                    if (!longSleeves) uaR = Slice(uaR, -lu * 0.5f, 0f);    // manga corta hasta mitad del brazo
                    else if (rags) uaR = Slice(uaR, -lu * 0.65f, 0f);
                    var sleeve = new MeshBuilder();
                    Lf(sleeve, default, default, uaR, 10, CL(top), 0.4f);
                    Add(ua, "manga", SlotCloth, sleeve, 2, true);
                    if (longSleeves)
                    {
                        float[] laR = Slice(Grow(LowerArmRings(ll + ArmExtra(left), f), grow, 1.04f), -ll * 0.97f, 0f);
                        var fore = new MeshBuilder();
                        Lf(fore, default, default, laR, 10, CL(top), 0.4f);
                        Add(la, "manga", SlotCloth, fore, 2, true);
                    }
                legs:;
                }

                foreach (bool left in new[] { true, false })
                {
                    BoneId ul = left ? BoneId.LeftUpperLeg : BoneId.RightUpperLeg, ll = left ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg;
                    float thigh = L(ul), shin = L(ll);
                    float[] tR = Grow(ThighRings(thigh, _f), grow * 1.2f, 1.03f), cR = Grow(CalfRings(shin, _f), grow, 1.03f);
                    if (rags) { if (left) { tR = Slice(tR, -thigh * 0.5f, 0f); cR = null; } else { cR = Slice(cR, -shin * 0.45f, 0f); } }
                    else if (c == ZombieClothing.Workwear && !left) cR = Slice(cR, -shin * 0.7f, 0f);   // pernera rota por debajo de la rodilla
                    else cR = Slice(cR, -shin * 0.96f, 0f);
                    var leg = new MeshBuilder();
                    Lf(leg, default, default, tR, 10, CL(bottom), 0.45f);
                    Add(ul, "pernera", SlotCloth, leg, 2, true);
                    if (cR != null)
                    {
                        var shinC = new MeshBuilder();
                        Lf(shinC, default, default, cR, 10, CL(bottom), 0.45f);
                        Add(ll, "pernera", SlotCloth, shinC, 2, true);
                    }
                    // bata: faldón suelto por delante y detrás del muslo, atado al muslo (no atraviesa las piernas al caminar)
                    if (c == ZombieClothing.LabCoat)
                    {
                        var skirt = new MeshBuilder();
                        float[] sk = Slice(Grow(ThighRings(thigh, _f), grow * 3f, 1.22f), -thigh * 0.62f, 0.02f);
                        Lf(skirt, default, default, sk, 10, CL(ZombieAtlas.ClLab), 0.45f);
                        Add(ul, "faldon", SlotCloth, skirt, 1, true);
                    }
                    FootCloth(left);
                }
                if (_lod == 0 && c != ZombieClothing.Rags)
                {
                    var belt = new MeshBuilder();
                    belt.Lathe(new V3(0f, 0.06f * s, 0f), default, new[] { -0.02f * s, 0.158f * _k * s * hipK + grow * 1.5f, 0.02f * s, 0.158f * _k * s * hipK + grow * 1.5f }, Sides(14), 0.68f * _d, true, true, CL(ZombieAtlas.ClLeather), 0.3f);
                    Add(BoneId.Hips, "cinturon", SlotCloth, belt, 0);
                }
            }

            // bota rota: caña en la pantorrilla y suela; descalzo no añade nada
            private void FootCloth(bool left)
            {
                if (_v.footwear != ZombieFootwear.BrokenBoots) return;
                BoneId ll = left ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg, foot = left ? BoneId.LeftFoot : BoneId.RightFoot, toes = left ? BoneId.LeftToes : BoneId.RightToes;
                float shin = L(ll), drop = L(foot), ball = _spec[(int)toes].LocalPosition.Z, s = _s;
                var shaft = new MeshBuilder();
                shaft.Lathe(new V3(0f, -shin * 0.62f, 0f), default, SoldierModel.Limb(shin * 0.4f, new[] { 0f, 0.052f * s * 1.02f, 1f, 0.05f * s }), Sides(12), 1.05f, true, true, CL(ZombieAtlas.ClLeather), 0.25f);
                Add(ll, "bota", SlotCloth, shaft, 1);
                float heel = 0.07f * s, h = Math.Min(0.085f * s, drop + 0.01f), len = ball + heel;
                var boot = new MeshBuilder();
                boot.Box(new V3(0f, 0.009f * s, (ball - heel) * 0.5f), new V3(0.108f * s, 0.018f * s, len), default, 0.006f, CL(ZombieAtlas.ClRubber), 0.3f);
                boot.Box(new V3(0f, 0.018f * s + (h - 0.018f * s) * 0.5f, (ball - heel) * 0.5f - 0.004f), new V3(0.1f * s, h - 0.018f * s, len - 0.012f), default, 0.014f, CL(ZombieAtlas.ClLeather), 0.3f);
                Add(foot, "bota", SlotCloth, boot, 2, false, true, new V3(0f, -drop, 0f));
                var toe = new MeshBuilder();
                toe.Box(new V3(0f, 0.027f * s, 0.032f * s), new V3(0.098f * s, 0.04f * s, 0.07f * s), default, 0.012f, CL(ZombieAtlas.ClLeather), 0.2f);
                Add(toes, "puntera", SlotCloth, toe, 2);
            }

            // ---- acceso para las características de cada tipo
            public void AddPiece(BoneId bone, string name, int slot, MeshBuilder mb, int maxLod, bool blend, bool shadow, V3 pivot, int extra) => Add(bone, name, slot, mb, maxLod, blend, shadow, pivot, extra);
            public int AddExtraBone(string name, BoneId parent, V3 localPos)
            {
                Result.Extras.Add(new ZombieExtraBone { Name = name, Parent = parent, LocalPos = localPos });
                return Result.Extras.Count - 1;
            }
            public float S => _s; public float K => _k; public float D => _d; public float Em => _em; public float F => _f; public float Tw => _tw; public float Hs => _hs;
            public float Len(BoneId id) => L(id);
            public int SidesFor(int n) => Sides(n);
            public BoneDef[] Spec => _spec;
            public float[] ChestR(float grow) => ChestRings(grow);
            public float[] UpperR(float grow) => UpperRings(grow);
            public float[] UpperArmR(float len, float f) => UpperArmRings(len, f);
        }
    }
}
