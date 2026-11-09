using System;

namespace SevenDays.Procedural
{
    /// <summary>Nombres de los huesos extra de los jefes (los anima ZombieBody): mitades de la mandíbula doble, núcleo y costillas del Devorador.</summary>
    public static class ZombieBossBones
    {
        public const string JawL = "JawL", JawR = "JawR", Core = "Core", RibL = "RibL", RibR = "RibR";
    }

    /// <summary>
    /// Piezas propias de los jefes, encima del cuerpo común:
    ///  - Bruto: armadura soldada (placas de chapa con cordones de soldadura), hombreras enormes con varillas, cadenas cruzadas y a la cintura, casco de obra
    ///    incrustado en el cráneo y puños-maza con placas y pernos.
    ///  - Lanzador Colosal: brazo gigante de lanzar con vendajes y neumáticos, bolsas en la espalda, un depósito de residuos atado con correas y pústulas verdosas.
    ///  - El Devorador: mandíbula doble (dos mitades en huesos propios que se abren), colmillos, columna expuesta con púas, bultos mutados, garras y el núcleo del pecho
    ///    (el punto débil) tras unas costillas que se abren como puertas (huesos JawL/JawR/Core/RibL/RibR).
    /// </summary>
    internal sealed class ZombieBossFeatures
    {
        private const string JawL = ZombieBossBones.JawL, JawR = ZombieBossBones.JawR, Core = ZombieBossBones.Core, RibL = ZombieBossBones.RibL, RibR = ZombieBossBones.RibR;

        private readonly ZombieModel.Builder _b;
        private readonly ZombieAnatomy _a;
        private readonly ZombieVariantData _v;
        private readonly int _lod;
        private readonly float _s, _k, _d, _tw, _h;
        private readonly Random _r;

        public ZombieBossFeatures(ZombieModel.Builder b, ZombieAnatomy a, ZombieVariantData v, int lod)
        {
            _b = b; _a = a; _v = v; _lod = lod; _s = b.S; _k = b.K; _d = b.D; _tw = b.Tw; _h = b.Hs;
            _r = new Random(v.seed * 37 + 3);
        }

        private float F() => (float)_r.NextDouble();
        private static AtlasCell SK(int id) => ZombieAtlas.Skin(id);
        private static AtlasCell CL(int id) => ZombieAtlas.Cloth(id);
        private int Sides(int n) => _b.SidesFor(n);

        /// <summary>Medida real del torso a una altura del hueso, leída de los mismos anillos que la malla: medio ancho, frente (z) y espalda (distancia hacia -z).</summary>
        private struct Section { public float Rx, Front, Back; }

        private static Section At(float[] rings, float y)
        {
            int n = rings.Length / 5, i = 0;
            while (i < n - 2 && rings[(i + 1) * 5] < y) i++;
            float y0 = rings[i * 5], y1 = rings[(i + 1) * 5], t = y1 - y0 > 1e-6f ? Math.Max(0f, Math.Min(1f, (y - y0) / (y1 - y0))) : 0f;
            float Lerp(int c) => rings[i * 5 + c] + (rings[(i + 1) * 5 + c] - rings[i * 5 + c]) * t;
            float rz = Lerp(2), cz = Lerp(4);
            return new Section { Rx = Lerp(1), Front = rz + cz, Back = rz - cz };
        }

        private Section Chest(float y) => At(_b.ChestR(0f), y);
        private Section Upper(float y) => At(_b.UpperR(0f), y);
        private Section SpineAt(float y) => At(_b.SpineR(0f), y);

        public void Build()
        {
            switch (_a.Boss)
            {
                case ZombieBossKind.Brute: Brute(); break;
                case ZombieBossKind.Launcher: Launcher(); break;
                case ZombieBossKind.Devourer: Devourer(); break;
            }
        }

        private static V3 AlignY(V3 n)
        {
            n = n.Normalized;
            double x = Math.Acos(Math.Max(-1.0, Math.Min(1.0, n.Y))), y = Math.Atan2(n.X, n.Z);
            return new V3((float)(x * 180.0 / Math.PI), (float)(y * 180.0 / Math.PI), 0f);
        }

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

        /// <summary>Cadena de eslabones de "a" a "b" (eslabones alternos girados 90 grados).</summary>
        private void Chain(MeshBuilder mb, V3 a, V3 b, float link)
        {
            V3 d = b - a; float len = (float)Math.Sqrt(V3.Dot(d, d));
            int n = Math.Max(2, (int)(len / (link * 1.6f)));
            V3 e = AlignY(d);
            for (int i = 0; i < n; i++)
            {
                V3 p = a + d * ((i + 0.5f) / n);
                var rot = Mat3.Euler(e) * Mat3.Euler(new V3(0f, i % 2 == 0 ? 0f : 90f, 90f));
                mb.Append(Torus(link * 0.55f, link * 0.16f, _lod == 0 ? 8 : 6, 4), p, rot, CL(ZombieAtlas.ClMetal), 0.1f);
            }
        }

        // ================================================================ Bruto
        private void Brute()
        {
            float s = _s, ch = _b.Len(BoneId.Chest), uc = _b.Len(BoneId.UpperChest), sp = _b.Len(BoneId.Spine);
            var mid = Chest(ch * 0.62f); float rx = mid.Rx;
            // placas de chapa soldadas: pecho, vientre y espalda, algo torcidas y de tamaños distintos, apoyadas en la superficie real del pecho
            var plates = new MeshBuilder();
            plates.Box(new V3(-0.02f * s, ch * 0.62f, mid.Front + 0.017f * s), new V3(rx * 1.2f, ch * 0.72f, 0.034f * s), new V3(-7f, -4f, 3f), 0.008f * s, CL(ZombieAtlas.ClMetal), 0.5f);
            var low = Chest(ch * 0.2f);
            plates.Box(new V3(rx * 0.38f, ch * 0.2f, low.Front + 0.014f * s), new V3(rx * 0.62f, ch * 0.36f, 0.026f * s), new V3(-2f, 8f, -6f), 0.006f * s, CL(ZombieAtlas.ClDoor), 0.4f);
            plates.Box(new V3(0f, ch * 0.55f, -Chest(ch * 0.55f).Back - 0.017f * s), new V3(rx * 1.25f, ch * 0.8f, 0.03f * s), new V3(5f, 0f, -2f), 0.008f * s, CL(ZombieAtlas.ClMetal), 0.5f);
            if (_lod == 0)
                for (int i = 0; i < 7; i++)   // cordones de soldadura en el borde de arriba y abajo de la placa del pecho
                {
                    float x = (i / 6f - 0.5f) * rx * 1.1f, y = ch * (i % 2 == 0 ? 0.97f : 0.27f);
                    plates.Lathe(new V3(x - 0.02f * s, y, Chest(y).Front + 0.03f * s), new V3(0f, 0f, 90f), SoldierModel.Ellipsoid(0.012f * s, 0.022f * s, 2), 5, 0.8f, true, true, CL(ZombieAtlas.ClMetal), 0.05f);
                }
            _b.AddPiece(BoneId.Chest, "armadura soldada", ZombieModel.SlotCloth, plates, 2, false, true, default, -1);
            var belly = new MeshBuilder();
            belly.Box(new V3(-0.03f * s, sp * 0.55f, SpineAt(sp * 0.55f).Front + 0.015f * s), new V3(0.22f * _k * s, sp * 0.65f, 0.026f * s), new V3(-3f, -6f, 5f), 0.006f * s, CL(ZombieAtlas.ClMetal), 0.4f);
            _b.AddPiece(BoneId.Spine, "placa vientre", ZombieModel.SlotCloth, belly, 1, false, true, default, -1);

            // hombreras enormes de chapa con varillas de obra clavadas
            float f = _b.F;
            foreach (bool left in new[] { true, false })
            {
                float side = left ? -1f : 1f;
                var pad = new MeshBuilder();
                pad.Dome(new V3(side * 0.012f * f, 0.012f * f, 0f), new V3(0f, 0f, -40f * side), new V3(0.074f * f, 0.06f * f, 0.08f * f), lon => 0f, 0.012f * s, Sides(12), _lod == 0 ? 5 : 3, CL(ZombieAtlas.ClMetal), 0.3f);
                for (int i = 0; i < (left ? 3 : 2); i++)   // también en el LOD 2: el ajuste al suelo de la muerte usa esa malla
                        pad.Cylinder(new V3(side * (0.02f + 0.025f * i) * f, 0.075f * f + 0.04f * s, (i - 1) * 0.04f * f), new V3((F() - 0.5f) * 30f, 0f, side * (20f + 15f * i)), 0.008f * s, 0.006f * s, 0.18f * s, 5, CL(ZombieAtlas.ClMetal), 0.1f);
                _b.AddPiece(left ? BoneId.LeftUpperArm : BoneId.RightUpperArm, "hombrera", ZombieModel.SlotCloth, pad, 2, false, true, default, -1);
            }

            // cadenas: en bandolera del hombro izquierdo a la cadera derecha, y alrededor de la cintura
            if (_lod <= 1)
            {
                var chain = new MeshBuilder();
                Chain(chain, new V3(-rx * 0.8f, ch * 1.0f, Chest(ch).Front * 0.75f), new V3(rx * 0.7f, ch * 0.05f, Chest(ch * 0.05f).Front + 0.025f * s), 0.06f * s);
                _b.AddPiece(BoneId.Chest, "cadena", ZombieModel.SlotCloth, chain, 1, false, false, default, -1);
                var waist = new MeshBuilder();
                float hipR = 0.165f * _k * s * ((_a.Proportions.hipWidth / s) / 0.27f);
                for (int i = 0; i < 12; i++)
                {
                    double a = i / 12.0 * Math.PI * 2.0;
                    var p = new V3((float)Math.Sin(a) * hipR * 1.04f, 0.08f * s + 0.015f * s * (float)Math.Sin(a * 3), (float)Math.Cos(a) * hipR * 0.7f * _d);
                    waist.Append(Torus(0.033f * s, 0.009f * s, 6, 4), p, Mat3.Euler(new V3(0f, (float)(a * 180.0 / Math.PI), i % 2 == 0 ? 90f : 0f)), CL(ZombieAtlas.ClMetal), 0.1f);
                }
                _b.AddPiece(BoneId.Hips, "cadena cintura", ZombieModel.SlotCloth, waist, 1, false, false, default, -1);
            }

            // casco de obra incrustado y agrietado, ladeado sobre el cráneo
            float h = _h;
            var helmet = new MeshBuilder();
            helmet.Dome(new V3(0.008f * h, 0.15f * h, -0.004f * h), new V3(-8f, 0f, 9f), new V3(0.112f * h, 0.082f * h, 0.124f * h), lon => 0f, 0.007f * h, Sides(14), _lod == 0 ? 5 : 3, SK(ZombieAtlas.SkFeatureB), 0.25f);
            helmet.Lathe(new V3(0.008f * h, 0.152f * h, 0.012f * h), new V3(-8f, 0f, 9f), new[] { -0.003f * h, 0.13f * h, 0.003f * h, 0.128f * h }, Sides(14), 1.12f, true, true, SK(ZombieAtlas.SkFeatureB), 0.25f);
            _b.AddPiece(BoneId.Head, "casco de obra", ZombieModel.SlotSkin, helmet, 2, false, true, default, -1);

            // puños-maza: placa en el dorso, pernos en los nudillos y vendas en el antebrazo
            foreach (bool left in new[] { true, false })
            {
                float ff = _b.F * _a.ForearmScale;
                float kh = _b.HandScale(ff);
                float pl = HandWrap.PalmLength * kh * 0.95f, pw = HandWrap.PalmWidth * kh;
                var fist = new MeshBuilder();
                fist.Box(new V3(HandWrap.PalmThickness * kh * 0.55f, -pl * 0.55f, 0f), new V3(0.02f * s, pl * 0.9f, pw * 1.05f), new V3(0f, 0f, 4f), 0.004f * s, CL(ZombieAtlas.ClMetal), 0.15f);
                if (_lod == 0)
                    for (int i = 0; i < 3; i++)
                        fist.Cylinder(new V3(HandWrap.PalmThickness * kh * 0.6f + 0.02f * s, -pl * 0.95f, (i - 1) * pw * 0.32f), new V3(0f, 0f, -80f), 0.01f * s, 0.002f * s, 0.05f * s, 5, CL(ZombieAtlas.ClMetal), 0.05f);
                _b.AddPiece(left ? BoneId.LeftHand : BoneId.RightHand, "puño maza", ZombieModel.SlotCloth, fist, 1, false, false, default, -1);
                BoneId la = left ? BoneId.LeftLowerArm : BoneId.RightLowerArm;
                float ll = _b.Len(la);
                var wrap = new MeshBuilder();
                foreach (float t in new[] { 0.25f, 0.45f, 0.7f })
                {
                    float rr = 0.05f * ff * (1.05f - 0.25f * t) + 0.004f * s;
                    wrap.Lathe(new V3(0f, -ll * t, 0f), new V3((F() - 0.5f) * 14f, 0f, (F() - 0.5f) * 14f), new[] { -0.018f * s, rr, 0.018f * s, rr * 0.98f }, Sides(10), 1.02f, true, true, CL(ZombieAtlas.ClBandage), 0.2f);
                }
                _b.AddPiece(la, "vendas", ZombieModel.SlotCloth, wrap, 1, false, false, default, -1);
            }
        }

        // ================================================================ Lanzador Colosal
        private void Launcher()
        {
            float s = _s, uc = _b.Len(BoneId.UpperChest), ch = _b.Len(BoneId.Chest);
            BoneId ua = _a.BigArmSide > 0 ? BoneId.RightUpperArm : BoneId.LeftUpperArm, la = _a.BigArmSide > 0 ? BoneId.RightLowerArm : BoneId.LeftLowerArm;
            float fa = _b.F * _a.BigArmScale, lu = _b.Len(ua), ll = _b.Len(la) + _a.LongArmExtra * s;
            // brazo gigante: vendas a lo largo del brazo y del antebrazo
            var band = new MeshBuilder();
            foreach (float t in new[] { 0.3f, 0.5f, 0.72f })
            {
                float rr = 0.064f * fa * (1f - 0.12f * t) + 0.005f * s;
                band.Lathe(new V3(0f, -lu * t, 0f), new V3((F() - 0.5f) * 16f, 0f, (F() - 0.5f) * 16f), new[] { -0.024f * s, rr, 0.024f * s, rr * 0.97f }, Sides(12), 1.03f, true, true, CL(ZombieAtlas.ClBandage), 0.2f);
            }
            _b.AddPiece(ua, "vendas", ZombieModel.SlotCloth, band, 1, false, false, default, -1);
            var fore = new MeshBuilder();
            foreach (float t in new[] { 0.2f, 0.42f, 0.62f, 0.8f })
            {
                float rr = 0.05f * fa * (1.05f - 0.3f * t) + 0.005f * s;
                fore.Lathe(new V3(0f, -ll * t, 0f), new V3((F() - 0.5f) * 18f, 0f, (F() - 0.5f) * 18f), new[] { -0.022f * s, rr, 0.022f * s, rr * 0.97f }, Sides(12), 1.03f, true, true, CL(ZombieAtlas.ClBandage), 0.2f);
            }
            _b.AddPiece(la, "vendas", ZombieModel.SlotCloth, fore, 1, false, false, default, -1);
            // neumáticos: uno en el hombro del brazo grande y otro encajado en el antebrazo
            var tire = new MeshBuilder();
            tire.Append(Torus(0.068f * fa + 0.032f * s, 0.032f * s, Sides(18), _lod == 0 ? 8 : 6), new V3(0f, -lu * 0.08f, 0f), Mat3.Euler(new V3(0f, 0f, 16f)), CL(ZombieAtlas.ClRubber), 0.3f);
            _b.AddPiece(ua, "neumatico", ZombieModel.SlotCloth, tire, 2, false, true, default, -1);
            var tire2 = new MeshBuilder();
            tire2.Append(Torus(0.05f * fa + 0.028f * s, 0.028f * s, Sides(16), _lod == 0 ? 8 : 6), new V3(0f, -ll * 0.1f, 0f), Mat3.Euler(new V3(8f, 0f, -6f)), CL(ZombieAtlas.ClRubber), 0.3f);
            _b.AddPiece(la, "neumatico", ZombieModel.SlotCloth, tire2, 2, false, true, default, -1);

            // espalda: bolsas grandes y un depósito de residuos atado con correas
            float back = Upper(uc * 0.5f).Back;
            var bags = new MeshBuilder();
            (float x, float y, float rx, float ry)[] list = { (-0.12f, uc * 0.5f, 0.11f, 0.12f), (0.15f, uc * 0.15f, 0.09f, 0.1f), (-0.05f, uc * 0.92f, 0.08f, 0.08f), (0.12f, uc * 0.85f, 0.07f, 0.075f) };
            foreach (var g in list)
                bags.Lathe(new V3(g.x * s * _k, g.y, -back - g.rx * s * 0.5f), new V3(10f * (F() - 0.5f), 0f, 12f * (F() - 0.5f)), SoldierModel.Ellipsoid(g.rx * s, g.ry * s, 4), Sides(10), 0.9f, true, true, SK(ZombieAtlas.SkFeatureA), 0.3f);
            _b.AddPiece(BoneId.UpperChest, "bolsas", ZombieModel.SlotSkin, bags, 2, false, true, default, -1);
            var tank = new MeshBuilder();
            tank.Cylinder(new V3(0.02f * s, uc * 0.2f, -back - 0.17f * s), new V3(6f, 0f, -8f), 0.12f * s, 0.12f * s, 0.46f * s, Sides(14), CL(ZombieAtlas.ClMetal), 0.4f);
            tank.Lathe(new V3(0.02f * s, uc * 0.2f + 0.24f * s, -back - 0.17f * s), new V3(6f, 0f, -8f), SoldierModel.Ellipsoid(0.12f * s, 0.04f * s, 3), Sides(14), 1f, true, true, CL(ZombieAtlas.ClMetal), 0.2f);
            tank.Lathe(new V3(0.02f * s, uc * 0.2f + 0.02f * s, -back - 0.17f * s), new V3(6f, 0f, -8f), new[] { -0.03f * s, 0.124f * s, 0.03f * s, 0.124f * s }, Sides(14), 1f, false, false, CL(ZombieAtlas.ClOveralls), 0.2f);   // franja de aviso
            _b.AddPiece(BoneId.UpperChest, "deposito", ZombieModel.SlotCloth, tank, 2, false, true, default, -1);
            var straps = new MeshBuilder();
            foreach (float sx in new[] { -1f, 1f })
                straps.Lathe(new V3(sx * 0.13f * s * _k, uc * 0.55f, 0f), new V3(90f, 0f, sx * 8f), new[] { -0.02f * s, back * 1.04f, 0.02f * s, back * 1.04f }, Sides(14), 0.55f, false, false, CL(ZombieAtlas.ClWebbing), 0.3f);
            _b.AddPiece(BoneId.UpperChest, "correas", ZombieModel.SlotCloth, straps, 1, false, false, default, -1);

            // pústulas verdosas en el pecho, la espalda y el brazo grande
            var pus = new MeshBuilder();
            int n = _lod == 0 ? 10 : 5;
            for (int i = 0; i < n; i++)
            {
                double phi = (F() * 320.0 - 160.0) * Math.PI / 180.0;
                float y = ch * (0.15f + 0.8f * F()), rr = (0.02f + 0.02f * F()) * s;
                var sec = Chest(y); float c = (float)Math.Cos(phi);
                var at = new V3((float)Math.Sin(phi) * sec.Rx, y, c * (c > 0f ? sec.Front : sec.Back));
                pus.Lathe(at, AlignY(new V3((float)Math.Sin(phi), 0.1f, (float)Math.Cos(phi))), SoldierModel.Ellipsoid(rr, rr * 0.8f, 3), 6, 1f, true, true, SK(ZombieAtlas.SkFeatureB), 0.1f);
            }
            _b.AddPiece(BoneId.Chest, "pustulas", ZombieModel.SlotSkin, pus, 1, false, false, default, -1);
            var pusArm = new MeshBuilder();
            for (int i = 0; i < (_lod == 0 ? 5 : 2); i++)
            {
                double phi = F() * Math.PI * 2.0; float rr = (0.018f + 0.015f * F()) * s, ar = 0.066f * fa;
                pusArm.Lathe(new V3((float)Math.Sin(phi) * ar, -lu * (0.15f + 0.7f * F()), (float)Math.Cos(phi) * ar), AlignY(new V3((float)Math.Sin(phi), 0f, (float)Math.Cos(phi))), SoldierModel.Ellipsoid(rr, rr * 0.8f, 3), 6, 1f, true, true, SK(ZombieAtlas.SkFeatureB), 0.1f);
            }
            _b.AddPiece(ua, "pustulas", ZombieModel.SlotSkin, pusArm, 1, false, false, default, -1);
        }

        // ================================================================ El Devorador
        private void Devourer()
        {
            float s = _s, h = _h, ch = _b.Len(BoneId.Chest), uc = _b.Len(BoneId.UpperChest), sp = _b.Len(BoneId.Spine);
            // mandíbula doble: cada mitad cuelga de su bisagra (detrás de la mejilla) y se abre hacia fuera y abajo al rugir
            foreach (bool left in new[] { true, false })
            {
                float side = left ? -1f : 1f;
                int bone = _b.AddExtraBone(left ? JawL : JawR, BoneId.Head, new V3(side * 0.052f * h, 0.07f * h, 0.0f));
                var jaw = new MeshBuilder();
                // loft hacia delante (+Z) y algo hacia el centro; cz baja la mitad de la mandíbula
                // mandíbula de insecto: gruesa en la bisagra, curvada hacia el centro y acabada en punta
                jaw.Loft(new V3(0f, 0f, 0f), new V3(90f, 0f, 0f), new[] {
                    -0.012f * h, 0.016f * h, 0.022f * h, 0f, 0.008f * h,
                    0.03f * h, 0.018f * h, 0.024f * h, -side * 0.006f * h, 0.026f * h,
                    0.075f * h, 0.015f * h, 0.019f * h, -side * 0.018f * h, 0.036f * h,
                    0.12f * h, 0.01f * h, 0.012f * h, -side * 0.032f * h, 0.036f * h,
                    0.15f * h, 0.003f * h, 0.004f * h, -side * 0.044f * h, 0.03f * h }, Sides(8), true, true, SK(ZombieAtlas.SkTissue), 0.12f);
                // colmillos de la mitad inferior, hacia arriba
                int teeth = _lod == 0 ? 4 : (_lod == 1 ? 2 : 0);
                for (int i = 0; i < teeth; i++)
                {
                    float z = (0.035f + 0.025f * i) * h, len = (0.03f - 0.004f * i) * h;
                    jaw.Cylinder(new V3(-side * (0.008f + 0.008f * i) * h, -0.02f * h - 0.004f * h * i + len * 0.5f, z), new V3(0f, 0f, side * 10f), 0.0055f * h, 0.0012f * h, len, 5, SK(ZombieAtlas.SkBone), 0.04f);
                }
                _b.AddPiece(BoneId.Head, left ? "mandibula izquierda" : "mandibula derecha", ZombieModel.SlotSkin, jaw, 2, false, true, default, bone);
            }
            // colmillos superiores fijos en el cráneo
            var upper = new MeshBuilder();
            for (int i = -2; i <= 2; i++)
            {
                if (_lod > 0 && i % 2 != 0) continue;
                float len = (0.034f - Math.Abs(i) * 0.005f) * h;
                upper.Cylinder(new V3(i * 0.016f * h, 0.064f * h - len * 0.5f, 0.098f * h - Math.Abs(i) * 0.006f * h), new V3(180f, 0f, i * 5f), 0.007f * h, 0.0015f * h, len, 5, SK(ZombieAtlas.SkBone), 0.04f);
            }
            _b.AddPiece(BoneId.Head, "colmillos", ZombieModel.SlotSkin, upper, 1, false, false, default, -1);

            // columna expuesta: vértebras con púas a lo largo de la espalda
            void Spine(BoneId bone, float len, float backZ, int count)
            {
                var mb = new MeshBuilder();
                for (int i = 0; i < count; i++)
                {
                    float y = len * (i + 0.5f) / count;
                    mb.Lathe(new V3(0f, y, -backZ), new V3(90f, 0f, 0f), SoldierModel.Ellipsoid(0.03f * s, 0.028f * s, 3), Sides(7), 1f, true, true, SK(ZombieAtlas.SkBone), 0.06f);
                    mb.Cylinder(new V3(0f, y + 0.02f * s, -backZ - 0.07f * s), new V3(-60f, 0f, 0f), 0.022f * s, 0.002f * s, 0.14f * s * (0.8f + 0.4f * F()), 5, SK(ZombieAtlas.SkBone), 0.06f);
                }
                _b.AddPiece(bone, "columna", ZombieModel.SlotSkin, mb, 2, false, true, default, -1);
            }
            Spine(BoneId.Spine, sp, SpineAt(sp * 0.5f).Back * 0.95f, _lod == 0 ? 3 : 2);
            Spine(BoneId.Chest, ch, Chest(ch * 0.5f).Back * 0.95f, _lod == 0 ? 3 : 2);
            Spine(BoneId.UpperChest, uc * 0.9f, Upper(uc * 0.45f).Back * 0.95f, 2);

            // núcleo (punto débil) en el pecho, tras dos "puertas" de costillas que se abren al exponerse
            var sec = Chest(ch * 0.62f); float front = sec.Front, rx = sec.Rx;
            int core = _b.AddExtraBone(Core, BoneId.Chest, new V3(0f, ch * 0.62f, front - 0.05f * s));
            var c = new MeshBuilder();
            c.Lathe(default, default, SoldierModel.Ellipsoid(0.12f * s, 0.12f * s, 5), Sides(12), 0.8f, true, true, SK(ZombieAtlas.SkFeatureB), 0.3f, true);
            _b.AddPiece(BoneId.Chest, "nucleo", ZombieModel.SlotSkin, c, 2, false, false, default, core);
            foreach (bool left in new[] { true, false })
            {
                float side = left ? -1f : 1f;
                // bisagra en el costado, pegada a la piel; las costillas llegan hasta el centro (el esternón) y tapan el núcleo
                float hx = rx * 0.62f;
                int rib = _b.AddExtraBone(left ? RibL : RibR, BoneId.Chest, new V3(side * hx, ch * 0.62f, front - 0.025f * s));
                var mb = new MeshBuilder();
                for (int i = 0; i < 4; i++)
                {
                    float y = (i - 1.5f) * 0.062f * s, len = hx + 0.01f * s;
                    mb.Loft(new V3(0f, y, 0f), new V3(0f, 0f, side * 90f), new[] { 0f, 0.013f * s, 0.012f * s, 0f, 0f, len * 0.5f, 0.012f * s, 0.011f * s, 0f, 0.022f * s, len, 0.009f * s, 0.009f * s, 0f, 0.018f * s }, Sides(6), true, true, SK(ZombieAtlas.SkBone), 0.1f);
                }
                _b.AddPiece(BoneId.Chest, left ? "costillas izquierdas" : "costillas derechas", ZombieModel.SlotSkin, mb, 2, false, true, default, rib);
            }

            // bultos mutados en el hombro izquierdo y el cuello, púas óseas en los antebrazos y garras
            var tumor = new MeshBuilder();
            for (int i = 0; i < (_lod == 0 ? 6 : 3); i++)
            {
                float rr = (0.05f + 0.05f * F()) * s;
                tumor.Lathe(new V3(-rx * (0.7f + 0.25f * F()), uc * (0.5f + 0.5f * F()), (F() - 0.5f) * 0.2f * s), new V3((F() - 0.5f) * 40f, 0f, (F() - 0.5f) * 40f), SoldierModel.Ellipsoid(rr, rr * 0.85f, 3), Sides(8), 1f, true, true, SK(ZombieAtlas.SkFeatureA), 0.2f);
            }
            _b.AddPiece(BoneId.UpperChest, "bultos", ZombieModel.SlotSkin, tumor, 2, false, true, default, -1);
            foreach (bool left in new[] { true, false })
            {
                BoneId la = left ? BoneId.LeftLowerArm : BoneId.RightLowerArm, hand = left ? BoneId.LeftHand : BoneId.RightHand;
                float ll = _b.Len(la), f = _b.F;
                var spikes = new MeshBuilder();
                for (int i = 0; i < 2; i++)
                    spikes.Cylinder(new V3(0f, -ll * (0.3f + 0.3f * i), -0.05f * f - 0.04f * s), new V3(-70f, 0f, 0f), 0.016f * s, 0.002f * s, 0.11f * s, 5, SK(ZombieAtlas.SkBone), 0.05f);
                _b.AddPiece(la, "pua", ZombieModel.SlotSkin, spikes, 1, false, false, default, -1);
                float kh = _b.HandScale(f);
                float pl = HandWrap.PalmLength * kh * 0.95f, pw = HandWrap.PalmWidth * kh;
                var claws = new MeshBuilder();
                for (int i = 0; i < 4; i++)
                    claws.Cylinder(new V3(-0.02f * kh, -pl - 0.13f * kh, (i - 1.5f) * pw * 0.28f * (left ? -1f : 1f)), new V3(0f, 0f, -12f), 0.006f * kh, 0.0012f * kh, 0.07f * kh, 5, SK(ZombieAtlas.SkBone), 0.04f);
                _b.AddPiece(hand, "garras", ZombieModel.SlotSkin, claws, 1, false, false, default, -1);
            }
        }
    }
}
