using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Rasgos propios de cada tipo, añadidos al cuerpo común: placas de tejido cicatrizado del tanque, armadura improvisada del blindado (chaleco, puerta, neumáticos o
    /// chatarra), bolsas y bultos del lanzador y vientre hinchado con pústulas del explosivo (en un hueso extra para poder inflarse durante la mecha).
    /// </summary>
    internal sealed class ZombieTypeFeatures
    {
        private readonly ZombieModel.Builder _b;
        private readonly ZombieAnatomy _a;
        private readonly ZombieVariantData _v;
        private readonly int _lod;
        private readonly float _s, _k, _d, _tw;

        public ZombieTypeFeatures(ZombieModel.Builder b, ZombieAnatomy a, ZombieVariantData v, int lod)
        {
            _b = b; _a = a; _v = v; _lod = lod; _s = b.S; _k = b.K; _d = b.D; _tw = b.Tw;
        }

        private static AtlasCell SK(int id) => ZombieAtlas.Skin(id);
        private static AtlasCell CL(int id) => ZombieAtlas.Cloth(id);
        private float ChestRx => 0.158f * _k * _s * _tw;
        private float ChestRz => ChestRx * 0.68f * _d;
        private float UpperRx => 0.16f * _k * _s * _tw;
        private float UpperRz => UpperRx * 0.66f * _d;

        public void Build()
        {
            switch (_a.TypeId)
            {
                case ZombieIds.Tank: Tank(); break;
                case ZombieIds.Armored: Armored(); break;
                case ZombieIds.Spitter: Spitter(); break;
                case ZombieIds.Explosive: Explosive(); break;
            }
        }

        /// <summary>Euler (x, y) que lleva el eje +Y local de una pieza hasta la dirección n.</summary>
        private static V3 AlignY(V3 n)
        {
            n = n.Normalized;
            double x = Math.Acos(Math.Max(-1.0, Math.Min(1.0, n.Y))), y = Math.Atan2(n.X, n.Z);
            return new V3((float)(x * 180.0 / Math.PI), (float)(y * 180.0 / Math.PI), 0f);
        }

        /// <summary>Toro (neumático, anillo) alrededor del eje Y: radio mayor R, radio del tubo r.</summary>
        private static MeshData Torus(float R, float r, int sides, int tube)
        {
            var prof = new float[(tube + 1) * 2];
            for (int i = 0; i <= tube; i++)
            {
                double t = i * 2.0 * Math.PI / tube;
                prof[i * 2] = (float)Math.Sin(t) * r; prof[i * 2 + 1] = R + (float)Math.Cos(t) * r;
            }
            return MeshBuilder.LatheData(prof, sides, 1f, false, false);
        }

        // ================================================================ tanque: placas de tejido cicatrizado
        private void Tank()
        {
            var r = new Random(_v.seed * 13 + 5);
            float F() => (float)r.NextDouble();
            // placas planas y abultadas, repartidas por pecho, espalda, hombros y el brazo grande
            void Plates(BoneId bone, float y0, float y1, float rx, float rz, float cz, int n, float size, bool backOnly = false)
            {
                var mb = new MeshBuilder();
                for (int i = 0; i < n; i++)
                {
                    double phi = (backOnly ? 180.0 + (F() - 0.5) * 150.0 : (F() * 340.0 - 170.0)) * Math.PI / 180.0;
                    float y = y0 + (y1 - y0) * F();
                    var at = new V3((float)Math.Sin(phi) * rx, y, cz + (float)Math.Cos(phi) * rz);
                    var n3 = new V3((float)Math.Sin(phi) / Math.Max(1e-3f, rx), 0.15f, (float)Math.Cos(phi) / Math.Max(1e-3f, rz));
                    float sz = size * (0.7f + 0.7f * F());
                    mb.Lathe(at, AlignY(n3), SoldierModel.Ellipsoid(sz, sz * 0.32f, 3), Math.Max(6, _b.SidesFor(8)), 0.7f + 0.5f * F(), true, true, SK(ZombieAtlas.SkFeatureA), 0.22f);
                }
                _b.AddPiece(bone, "placas", ZombieModel.SlotSkin, mb, 1, false, true, default, -1);
            }
            float ch = _b.Len(BoneId.Chest), uc = _b.Len(BoneId.UpperChest), sp = _b.Len(BoneId.Spine);
            Plates(BoneId.UpperChest, uc * 0.1f, uc * 0.75f, UpperRx * 1.02f, UpperRz * 1.02f, 0.008f * _k * _s, 6, 0.06f * _s * _k);
            Plates(BoneId.Chest, 0f, ch, ChestRx * 1.02f, ChestRz * 1.02f, 0.008f * _k * _s, 5, 0.065f * _s * _k);
            Plates(BoneId.Spine, 0f, sp, 0.152f * _k * _s * _tw * 1.02f, 0.152f * _k * _s * _tw * 0.66f * _d * 1.02f, 0.004f * _k * _s, 4, 0.06f * _s * _k, true);
            // el brazo grande lleva su coraza de cicatrices
            BoneId big = _a.BigArmSide > 0 ? BoneId.RightUpperArm : BoneId.LeftUpperArm;
            float lu = _b.Len(big), fa = _b.F * _a.BigArmScale;
            var arm = new MeshBuilder();
            for (int i = 0; i < 4; i++)
            {
                float y = -lu * (0.2f + 0.2f * i), ang = (F() - 0.5f) * 220f * (float)Math.PI / 180f;
                arm.Lathe(new V3((float)Math.Sin(ang) * 0.07f * fa, y, (float)Math.Cos(ang) * 0.072f * fa), AlignY(new V3((float)Math.Sin(ang), 0.1f, (float)Math.Cos(ang))),
                    SoldierModel.Ellipsoid(0.04f * fa, 0.014f * fa, 3), 7, 0.8f, true, true, SK(ZombieAtlas.SkFeatureA), 0.15f);
            }
            _b.AddPiece(big, "placas", ZombieModel.SlotSkin, arm, 1, false, true, default, -1);
        }

        // ================================================================ blindado
        private void Armored()
        {
            float ch = _b.Len(BoneId.Chest), uc = _b.Len(BoneId.UpperChest), sp = _b.Len(BoneId.Spine);
            float s = _s, front = ChestRz + 0.008f * _k * s, back = ChestRz - 0.008f * _k * s;
            switch (_v.armor)
            {
                case ZombieArmor.VestPlates:
                {
                    var m = new MeshBuilder();
                    m.Box(new V3(0f, ch * 0.62f, front + 0.016f * s), new V3(0.27f * _k * s, 0.3f * s, 0.028f * s), new V3(-4f, 0f, 0f), 0.01f * s, CL(ZombieAtlas.ClMetal), 0.35f);
                    m.Box(new V3(0f, ch * 0.62f, -back - 0.016f * s), new V3(0.27f * _k * s, 0.3f * s, 0.028f * s), new V3(4f, 0f, 0f), 0.01f * s, CL(ZombieAtlas.ClMetal), 0.35f);
                    foreach (float sx in new[] { -1f, 1f })
                        m.Box(new V3(sx * 0.15f * _k * s, ch * 0.4f, 0f), new V3(0.03f * s, 0.16f * s, 0.2f * _d * s), default, 0.006f * s, CL(ZombieAtlas.ClWebbing), 0.3f);
                    _b.AddPiece(BoneId.Chest, "placas chaleco", ZombieModel.SlotCloth, m, 2, false, true, default, -1);
                    var straps = new MeshBuilder();
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        straps.Box(new V3(sx * 0.1f * s, uc * 0.55f, UpperRz + 0.012f * s), new V3(0.05f * s, uc * 0.75f, 0.016f * s), default, 0.005f * s, CL(ZombieAtlas.ClWebbing), 0.3f);
                        straps.Box(new V3(sx * 0.1f * s, uc * 0.55f, -UpperRz - 0.012f * s), new V3(0.05f * s, uc * 0.75f, 0.016f * s), default, 0.005f * s, CL(ZombieAtlas.ClWebbing), 0.3f);
                    }
                    _b.AddPiece(BoneId.UpperChest, "correas", ZombieModel.SlotCloth, straps, 1, false, false, default, -1);
                    Pauldrons(CL(ZombieAtlas.ClMetal));
                    Bracers(CL(ZombieAtlas.ClMetal));
                    break;
                }
                case ZombieArmor.DoorShield:
                {
                    // puerta de metal pintada atada al pecho con cuerdas: ancha, alta y delante del torso (la cabeza queda libre)
                    var m = new MeshBuilder();
                    float dw = 0.52f * s * Math.Max(0.9f, _k), dh = 0.92f * s;
                    m.Box(new V3(0f, ch * 0.1f, front + 0.05f * s), new V3(dw, dh, 0.034f * s), new V3(-6f, 0f, 3f), 0.008f * s, CL(ZombieAtlas.ClDoor), 0.5f);
                    m.Cylinder(new V3(dw * 0.3f, ch * 0.1f, front + 0.09f * s), new V3(90f, 0f, 90f), 0.012f * s, 0.012f * s, 0.07f * s, 8, CL(ZombieAtlas.ClMetal), 0.1f);   // picaporte
                    _b.AddPiece(BoneId.Chest, "puerta", ZombieModel.SlotCloth, m, 2, false, true, default, -1);
                    var rope = new MeshBuilder();
                    foreach (float y in new[] { 0.2f, 0.62f })
                        rope.Lathe(new V3(0f, ch * y, 0f), default, new[] { -0.012f * s, ChestRx * 1.05f + 0.01f * s, 0.012f * s, ChestRx * 1.05f + 0.01f * s }, _b.SidesFor(14), 0.7f * _d, true, true, CL(ZombieAtlas.ClWebbing), 0.3f);
                    _b.AddPiece(BoneId.Chest, "cuerdas", ZombieModel.SlotCloth, rope, 1, false, false, default, -1);
                    break;
                }
                case ZombieArmor.TireRings:
                {
                    var bandolier = new MeshBuilder();
                    // neumático en bandolera alrededor del torso (inclinado) y otro de hombrera
                    bandolier.Append(Torus(0.255f * s * Math.Max(0.9f, _k), 0.062f * s, _b.SidesFor(18), 8), new V3(0f, ch * 0.5f, 0f), Mat3.Euler(new V3(18f, 0f, 14f)), CL(ZombieAtlas.ClRubber), 0.3f);
                    _b.AddPiece(BoneId.Chest, "neumatico", ZombieModel.SlotCloth, bandolier, 2, false, true, default, -1);
                    var pauldron = new MeshBuilder();
                    pauldron.Append(Torus(0.12f * s, 0.045f * s, _b.SidesFor(14), 7), new V3(0.01f * s, -0.02f * s, 0f), Mat3.Euler(new V3(0f, 0f, 22f)), CL(ZombieAtlas.ClRubber), 0.25f);
                    _b.AddPiece(BoneId.LeftUpperArm, "neumatico", ZombieModel.SlotCloth, pauldron, 2, false, true, default, -1);
                    break;
                }
                default:   // chatarra: chapa sobre el pecho, matrículas en los hombros, neumático cortado en la rodilla y tablones en los antebrazos
                {
                    var m = new MeshBuilder();
                    m.Box(new V3(0.02f * s, ch * 0.55f, front + 0.02f * s), new V3(0.3f * _k * s, 0.34f * s, 0.016f * s), new V3(-3f, 8f, 5f), 0.004f * s, CL(ZombieAtlas.ClMetal), 0.4f);
                    m.Box(new V3(-0.06f * s, ch * 0.25f, front + 0.03f * s), new V3(0.16f * s, 0.12f * s, 0.012f * s), new V3(-3f, -10f, -8f), 0.004f * s, CL(ZombieAtlas.ClDoor), 0.2f);
                    m.Box(new V3(0f, ch * 0.62f, -back - 0.014f * s), new V3(0.28f * _k * s, 0.3f * s, 0.016f * s), new V3(4f, 0f, 0f), 0.004f * s, CL(ZombieAtlas.ClMetal), 0.35f);
                    _b.AddPiece(BoneId.Chest, "chapa", ZombieModel.SlotCloth, m, 2, false, true, default, -1);
                    Pauldrons(CL(ZombieAtlas.ClDoor));
                    var knee = new MeshBuilder();
                    knee.Dome(new V3(0f, -0.01f * s, 0.028f * s), new V3(90f, 0f, 0f), new V3(0.062f * s, 0.05f * s, 0.07f * s), lon => 0f, 0.012f * s, 12, 4, CL(ZombieAtlas.ClRubber), 0.2f);
                    _b.AddPiece(BoneId.LeftLowerLeg, "rodillera", ZombieModel.SlotCloth, knee, 1, false, false, default, -1);
                    Bracers(CL(ZombieAtlas.ClRubber));
                    break;
                }
            }
        }

        private void Pauldrons(AtlasCell cell)
        {
            float s = _s, f = _b.F;
            foreach (bool left in new[] { true, false })
            {
                float side = left ? -1f : 1f;
                var pad = new MeshBuilder();
                pad.Dome(new V3(side * 0.006f * s, 0.01f * s, 0f), new V3(0f, 0f, -90f * side), new V3(0.092f * f, 0.08f * f, 0.094f * f), lon => 0f, 0.008f * s, _b.SidesFor(10), 4, cell, 0.2f);
                _b.AddPiece(left ? BoneId.LeftUpperArm : BoneId.RightUpperArm, "hombrera", ZombieModel.SlotCloth, pad, 2, false, true, default, -1);
            }
        }

        private void Bracers(AtlasCell cell)
        {
            float f = _b.F;
            foreach (bool left in new[] { true, false })
            {
                BoneId la = left ? BoneId.LeftLowerArm : BoneId.RightLowerArm;
                float ll = _b.Len(la);
                var g = new MeshBuilder();
                g.Box(new V3(0f, -ll * 0.4f, 0.04f * f), new V3(0.05f * f, ll * 0.5f, 0.012f * f), default, 0.003f, cell, 0.2f);
                g.Lathe(new V3(0f, -ll * 0.25f, 0f), default, new[] { -0.012f, 0.052f * f, 0.012f, 0.052f * f }, 8, 1.05f, true, true, CL(ZombieAtlas.ClWebbing), 0.2f);
                _b.AddPiece(la, "protector", ZombieModel.SlotCloth, g, 0, false, false, default, -1);
            }
        }

        // ================================================================ lanzador: bultos y bolsas
        private void Spitter()
        {
            var r = new Random(_v.seed * 17 + 3);
            float F() => (float)r.NextDouble();
            float uc = _b.Len(BoneId.UpperChest), ch = _b.Len(BoneId.Chest), s = _s;
            var back = new MeshBuilder();
            // tres bolsas grandes en la espalda alta y una joroba en el cuello
            (float x, float y, float rx, float ry)[] bags = { (-0.07f, uc * 0.45f, 0.085f, 0.09f), (0.075f, uc * 0.25f, 0.07f, 0.08f), (0f, uc * 0.75f, 0.06f, 0.07f) };
            foreach (var bag in bags)
                back.Lathe(new V3(bag.x * s * _k, bag.y, -UpperRz - bag.rx * s * 0.55f), new V3(8f * (F() - 0.5f), 0f, 10f * (F() - 0.5f)), SoldierModel.Ellipsoid(bag.rx * s, bag.ry * s, 4), _b.SidesFor(9), 0.95f, true, true, SK(ZombieAtlas.SkFeatureA), 0.25f);
            _b.AddPiece(BoneId.UpperChest, "bolsas espalda", ZombieModel.SlotSkin, back, 1, false, true, default, -1);
            var shoulders = new MeshBuilder();
            foreach (float sx in new[] { -1f, 1f })
                shoulders.Lathe(new V3(sx * UpperRx * 0.82f, uc * 0.88f, -0.01f * s), new V3(0f, 0f, sx * -20f), SoldierModel.Ellipsoid(0.055f * s * (sx > 0 ? 1.2f : 0.9f), 0.05f * s, 4), _b.SidesFor(10), 0.9f, true, true, SK(ZombieAtlas.SkFeatureA), 0.2f);
            _b.AddPiece(BoneId.UpperChest, "bultos hombros", ZombieModel.SlotSkin, shoulders, 1, false, true, default, -1);
            // pústulas verdosas en pecho y espalda
            var pustules = new MeshBuilder();
            int n = _lod == 0 ? 5 : 3;
            for (int i = 0; i < n; i++)
            {
                double phi = (F() * 300.0 - 150.0) * Math.PI / 180.0;
                float y = ch * (0.1f + 0.8f * F());
                float rr = (0.014f + 0.012f * F()) * s;
                var at = new V3((float)Math.Sin(phi) * ChestRx, y, 0.008f * _k * s + (float)Math.Cos(phi) * ChestRz);
                pustules.Lathe(at, AlignY(new V3((float)Math.Sin(phi), 0.1f, (float)Math.Cos(phi))), SoldierModel.Ellipsoid(rr, rr * 0.8f, 3), 5, 1f, true, true, SK(ZombieAtlas.SkFeatureB), 0.1f);
            }
            _b.AddPiece(BoneId.Chest, "pustulas", ZombieModel.SlotSkin, pustules, 1, false, false, default, -1);
        }

        // ================================================================ explosivo: vientre hinchado (hueso extra) con pústulas y venas luminosas
        private void Explosive()
        {
            float s = _s, sp = _b.Len(BoneId.Spine);
            int belly = _b.AddExtraBone("Belly", BoneId.Spine, new V3(0f, sp * 0.62f, 0.04f * s * _k));
            var m = new MeshBuilder();
            float rx = 0.19f * _k * s, ry = 0.16f * s, rz = 0.2f * _d * s;
            m.Lathe(default, default, SoldierModel.Ellipsoid(rx, ry, 6), _b.SidesFor(16), rz / rx, true, true, SK(ZombieAtlas.SkFeatureA), 0.5f, true);
            var r = new Random(_v.seed * 19 + 11);
            float F() => (float)r.NextDouble();
            int n = _lod == 0 ? 9 : (_lod == 1 ? 4 : 0);
            for (int i = 0; i < n; i++)
            {
                double phi = (F() * 280.0 - 140.0) * Math.PI / 180.0, lat = (F() * 70.0 - 35.0) * Math.PI / 180.0;
                var p = new V3((float)(Math.Cos(lat) * Math.Sin(phi)) * rx, (float)Math.Sin(lat) * ry, (float)(Math.Cos(lat) * Math.Cos(phi)) * rz);
                float rr = (0.02f + 0.022f * F()) * s;
                m.Lathe(p, AlignY(new V3(p.X / rx, p.Y / ry, p.Z / rz)), SoldierModel.Ellipsoid(rr, rr * 0.85f, 3), 6, 1f, true, true, SK(ZombieAtlas.SkFeatureB), 0.1f);
            }
            _b.AddPiece(BoneId.Spine, "vientre", ZombieModel.SlotSkin, m, 2, false, true, default, belly);
        }
    }
}
