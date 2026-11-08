using System;
using System.Threading.Tasks;

namespace SevenDays.Procedural
{
    public enum ZombieAtlasKind { Skin, Cloth, Wounds }

    /// <summary>
    /// Mapas de un atlas de zombi: color (el alfa recorta agujeros de la ropa y la forma de las heridas), normales (media resolución), máscara (R metal, G oclusión, A suavidad)
    /// y emisión (mitad de resolución: ojos con velo, venas y pústulas luminosas; negro donde no brilla).
    /// </summary>
    public sealed class ZombieMaps
    {
        public int Size, NormalSize, EmissionSize;
        public byte[] Albedo, Normal, Mask, Emission;
        public bool HasAlpha;
    }

    public struct ZSample
    {
        public Rgb Color;
        public float Alpha, Height, Metal, Smooth, Ao;
        public Rgb Emit;
    }

    /// <summary>
    /// Texturas procedurales de los zombis (puras, deterministas, en hilos). Piel (un atlas por tipo): cuerpo con venas, moratones, livor, costras, heridas y sangre seca;
    /// cabeza con cuencas, nariz, labios agrietados y sangre en la boca; manos, pies, hueso y dientes, ojo con velo lechoso y brillo tenue, pelo, tejido; y rasgos del tipo
    /// (placas cicatrizadas, vientre con venas luminosas, bolsas y pústulas). Ropa (común): camisa, camiseta, uniforme, bata, traje, pantalón, vaquero, mono, harapos, cuero,
    /// metal oxidado, goma, cinta, puerta, venda y saco, con agujeros, barro, quemaduras y desgarros. Heridas: decals recortados por alfa. Diseños propios.
    /// </summary>
    public static class ZombieTextures
    {
        public static ZombieMaps Generate(ZombieAtlasKind kind, string typeId, int size)
        {
            int n = size;
            var albedo = new byte[n * n * 4];
            var mask = new byte[n * n * 4];
            var height = new float[n * n];
            int en = Math.Max(16, n / 2);
            var emis = new float[en * en * 3];
            int typeSeed = Math.Abs((typeId ?? "").GetHashCode() % 1000) + 7;
            var anat = ZombieAnatomies.For(typeId ?? ZombieIds.Common);
            Parallel.For(0, n, y =>
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    var o = new ZSample { Color = new Rgb(0.5f, 0.5f, 0.5f), Alpha = 1f, Smooth = 0.25f, Ao = 1f };
                    switch (kind)
                    {
                        case ZombieAtlasKind.Skin: PaintSkin(u, v, typeId, typeSeed, anat, ref o); break;
                        case ZombieAtlasKind.Cloth: PaintCloth(u, v, ref o); break;
                        default: PaintWound(u, v, ref o); break;
                    }
                    int i = y * n + x;
                    height[i] = o.Height;
                    albedo[i * 4] = B(o.Color.R); albedo[i * 4 + 1] = B(o.Color.G); albedo[i * 4 + 2] = B(o.Color.B); albedo[i * 4 + 3] = B(o.Alpha);
                    mask[i * 4] = B(o.Metal); mask[i * 4 + 1] = B(o.Ao); mask[i * 4 + 2] = 0; mask[i * 4 + 3] = B(o.Smooth);
                    if ((x & 1) == 0 && (y & 1) == 0 && x / 2 < en && y / 2 < en)
                    {
                        int e = ((y / 2) * en + x / 2) * 3;
                        emis[e] = o.Emit.R; emis[e + 1] = o.Emit.G; emis[e + 2] = o.Emit.B;
                    }
                }
            });
            int nn = n > 256 ? n / 2 : n;
            float[] h2 = height;
            if (nn != n)
            {
                h2 = new float[nn * nn];
                for (int y = 0; y < nn; y++)
                    for (int x = 0; x < nn; x++)
                        h2[y * nn + x] = (height[(2 * y) * n + 2 * x] + height[(2 * y) * n + 2 * x + 1] + height[(2 * y + 1) * n + 2 * x] + height[(2 * y + 1) * n + 2 * x + 1]) * 0.25f;
            }
            var emission = new byte[en * en * 4];
            for (int i = 0; i < en * en; i++) { emission[i * 4] = B(emis[i * 3]); emission[i * 4 + 1] = B(emis[i * 3 + 1]); emission[i * 4 + 2] = B(emis[i * 3 + 2]); emission[i * 4 + 3] = 255; }
            return new ZombieMaps { Size = n, NormalSize = nn, EmissionSize = en, Albedo = albedo, Mask = mask, Emission = emission, HasAlpha = kind != ZombieAtlasKind.Skin, Normal = CharacterTextures.Normals(h2, nn, 3.4f * nn / 1024f * (kind == ZombieAtlasKind.Cloth ? 1.4f : 1f)) };
        }

        // ================================================================== piel
        private static void PaintSkin(float u, float v, string typeId, int ts, ZombieAnatomy a, ref ZSample o)
        {
            float lu, lv; int id;
            if (v < 0.5f && u < 0.5f) { id = ZombieAtlas.SkBody; lu = u * 2f; lv = v * 2f; }
            else if (v < 0.5f) { id = ZombieAtlas.SkHead; lu = (u - 0.5f) * 2f; lv = v * 2f; }
            else { int cx = Math.Min(3, (int)(u * 4f)), cy = Math.Min(1, (int)((v - 0.5f) * 4f)); id = ZombieAtlas.SkHand + cy * 4 + cx; lu = u * 4f - cx; lv = (v - 0.5f) * 4f - cy; }
            var baseSkin = new Rgb(a.SkinBase[0], a.SkinBase[1], a.SkinBase[2]);
            switch (id)
            {
                case ZombieAtlas.SkBody: Body(lu, lv, ts, a, baseSkin, ref o); break;
                case ZombieAtlas.SkHead: Head(lu, lv, ts, a, baseSkin, ref o); break;
                case ZombieAtlas.SkHand: Hand(lu, lv, ts, baseSkin, ref o); break;
                case ZombieAtlas.SkFoot: Foot(lu, lv, ts, baseSkin, ref o); break;
                case ZombieAtlas.SkBone: Bone(lu, lv, ts, ref o); break;
                case ZombieAtlas.SkEye: Eye(lu, lv, ts, ref o); break;
                case ZombieAtlas.SkHair: Hair(lu, lv, ts, ref o); break;
                case ZombieAtlas.SkTissue: Tissue(lu, lv, ts, ref o); break;
                case ZombieAtlas.SkFeatureA: FeatureA(typeId, lu, lv, ts, baseSkin, ref o); break;
                default: FeatureB(typeId, lu, lv, ts, baseSkin, ref o); break;
            }
        }

        private static float Ridged(float u, float v, int f, int oct, int s) => 1f - Math.Abs(2f * Fbm(u, v, f, oct, s) - 1f);

        /// <summary>Piel pálida grisácea con pelusilla de poros, red de venas verdosas, livor violáceo, moratones, costras, llagas y sangre seca. Sirve para torso, brazos y piernas.</summary>
        private static void Body(float u, float v, int ts, ZombieAnatomy a, Rgb baseSkin, ref ZSample o)
        {
            int seed = ts * 3 + 100;
            float mott = Fbm(u, v, 5, 4, seed);
            Rgb c = Rgb.Lerp(baseSkin.Scale(0.82f), baseSkin.Scale(1.08f), mott);
            // livor mortis: manchas violáceas donde se acumula la sangre
            float livor = Smooth(0.55f, 0.8f, Fbm(u, v, 3, 3, seed + 1));
            c = Rgb.Lerp(c, new Rgb(0.42f, 0.32f, 0.4f), livor * 0.5f);
            // venas: dos capas de ruido en cresta, verde azulado oscuro
            float vein = Math.Max(Smooth(0.935f, 0.985f, Ridged(u, v, 4, 3, seed + 2)), 0.7f * Smooth(0.95f, 0.99f, Ridged(u, v, 7, 2, seed + 3)));
            c = Rgb.Lerp(c, new Rgb(0.28f, 0.4f, 0.38f), vein * (0.3f + 0.3f * a.SkinTension));
            // moratones amarillo-verdosos y morados
            float bruise = Smooth(0.66f, 0.82f, Fbm(u, v, 3, 3, seed + 4));
            Rgb bc = Rgb.Lerp(new Rgb(0.4f, 0.27f, 0.38f), new Rgb(0.5f, 0.5f, 0.28f), Fbm(u, v, 6, 2, seed + 5));
            c = Rgb.Lerp(c, bc, bruise * 0.65f);
            // costras marrones y llagas abiertas
            float scab = Smooth(0.74f, 0.84f, Fbm(u, v, 9, 3, seed + 6));
            c = Rgb.Lerp(c, new Rgb(0.28f, 0.17f, 0.11f), scab * 0.85f);
            float sore = Smooth(0.82f, 0.9f, Fbm(u + 0.37f, v, 7, 3, seed + 7));
            c = Rgb.Lerp(c, new Rgb(0.42f, 0.09f, 0.07f), sore * 0.9f);
            // sangre seca en chorreras verticales
            float streak = Smooth(0.66f, 0.8f, Fbm(u, v * 0.4f, 11, 3, seed + 8)) * Smooth(0.35f, 0.9f, Fbm(u, v, 3, 2, seed + 9));
            c = Rgb.Lerp(c, new Rgb(0.2f, 0.05f, 0.04f), streak * 0.7f);
            // mugre
            c = Rgb.Lerp(c, new Rgb(0.2f, 0.19f, 0.16f), Smooth(0.6f, 0.9f, Fbm(u, v, 6, 4, seed + 10)) * 0.4f);
            float pores = Fbm(u, v, 60, 2, seed + 11);
            o.Color = c.Scale(0.95f + 0.1f * pores);
            o.Height = 0.25f * pores + 0.35f * vein * (a.SkinTension) - 0.6f * sore + 0.3f * scab + 0.2f * (1f - Smooth(0.2f, 0.7f, Fbm(u, v, 14, 2, seed + 12)));
            o.Smooth = 0.32f - 0.12f * scab + 0.25f * sore; o.Ao = 1f - 0.15f * livor - 0.2f * sore;
        }

        /// <summary>Cabeza: u=0.5 es la cara de frente, v=0 el mentón y v=1 la coronilla. Cuencas hundidas, nariz, labios agrietados, sangre en la boca, pómulos y cuero cabelludo con costras.</summary>
        private static void Head(float u, float v, int ts, ZombieAnatomy a, Rgb baseSkin, ref ZSample o)
        {
            int seed = ts * 5 + 200;
            Body(u, v, ts + 9, a, baseSkin, ref o);
            float du = u - 0.5f;   // 0 en el centro de la cara
            // cuencas: elipses oscuras con ojeras violáceas
            float sockets = 0f;
            foreach (float sx in new[] { -0.058f, 0.058f })
            {
                float d = (float)Math.Sqrt(((du - sx) / 0.05f) * ((du - sx) / 0.05f) + ((v - 0.463f) / 0.09f) * ((v - 0.463f) / 0.09f));   // v del loft del cráneo = longitud de arco: los ojos (y = 0.118) caen en 0.463
                sockets = Math.Max(sockets, Smooth(1.5f, 0.6f, d));
            }
            o.Color = Rgb.Lerp(o.Color, new Rgb(0.22f, 0.13f, 0.17f), sockets * 0.75f);
            o.Ao *= 1f - 0.5f * sockets; o.Height -= sockets * 0.8f;
            // nariz: sombra vertical y fosas
            float nose = Smooth(0.03f, 0f, Math.Abs(du)) * Smooth(0.2f, 0.4f, v) * Smooth(0.55f, 0.4f, v);
            o.Color = Rgb.Lerp(o.Color, o.Color.Scale(0.75f), nose * 0.5f);
            float nostril = Math.Max(Smooth(0.014f, 0f, Dist(du, v * 0.5f, -0.014f, 0.15f)), Smooth(0.014f, 0f, Dist(du, v * 0.5f, 0.014f, 0.15f)));
            o.Color = Rgb.Lerp(o.Color, new Rgb(0.1f, 0.05f, 0.05f), nostril);
            // boca: labios oscuros y agrietados, comisura con sangre
            float mouth = Smooth(0.07f, 0.02f, Math.Abs(v - 0.235f)) * Smooth(0.14f, 0.07f, Math.Abs(du));
            o.Color = Rgb.Lerp(o.Color, new Rgb(0.3f, 0.12f, 0.12f), mouth * 0.9f);
            float blood = Smooth(0.3f, 0.12f, v) * Smooth(0.5f, 0.1f, Math.Abs(du) * 2f) * Smooth(0.35f, 0.6f, Fbm(u, v, 8, 3, seed));
            o.Color = Rgb.Lerp(o.Color, Rgb.Lerp(new Rgb(0.34f, 0.04f, 0.04f), new Rgb(0.16f, 0.04f, 0.03f), Fbm(u, v, 5, 2, seed + 1)), Math.Min(1f, blood * 1.3f));
            o.Smooth = Math.Max(o.Smooth, 0.3f + 0.4f * blood * 0.6f);
            // cuero cabelludo (v alto): piel más oscura con costras
            float scalp = Smooth(0.8f, 0.95f, v);
            o.Color = Rgb.Lerp(o.Color, o.Color.Scale(0.6f), scalp * 0.5f);
            // pómulos prominentes y mejillas hundidas
            float cheek = Smooth(0.2f, 0.34f, Math.Abs(du)) * Smooth(0.5f, 0.38f, Math.Abs(v - 0.4f));
            o.Color = Rgb.Lerp(o.Color, o.Color.Scale(0.78f), cheek * 0.45f);
            // arrugas de la frente
            float fore = Smooth(0.014f, 0f, Math.Abs(Frac(v * 38f) - 0.5f) - 0.4f) * Smooth(0.62f, 0.7f, v) * Smooth(0.82f, 0.74f, v) * 0.5f;
            o.Height += fore * 0.4f; o.Color = Rgb.Lerp(o.Color, o.Color.Scale(0.8f), fore);
        }

        private static void Hand(float u, float v, int ts, Rgb baseSkin, ref ZSample o)
        {
            int seed = ts * 7 + 300;
            float mott = Fbm(u, v, 6, 3, seed);
            Rgb c = Rgb.Lerp(baseSkin.Scale(0.75f), baseSkin.Scale(1f), mott);
            float dirt = Smooth(0.45f, 0.8f, Fbm(u, v, 7, 4, seed + 1));
            c = Rgb.Lerp(c, new Rgb(0.18f, 0.14f, 0.1f), dirt * 0.55f);
            float blood = Smooth(0.55f, 0.8f, Fbm(u, v, 5, 3, seed + 2));
            c = Rgb.Lerp(c, new Rgb(0.25f, 0.04f, 0.04f), blood * 0.8f);
            float nail = Smooth(0.82f, 0.9f, v) * Smooth(0.2f, 0.45f, Math.Abs(u - 0.5f) * -1f + 0.5f);   // uñas oscuras rotas hacia las puntas
            c = Rgb.Lerp(c, new Rgb(0.12f, 0.09f, 0.07f), nail * 0.8f);
            o.Color = c; o.Height = 0.3f * Fbm(u, v, 40, 2, seed + 3); o.Smooth = 0.28f; o.Ao = 1f - 0.15f * dirt;
        }

        private static void Foot(float u, float v, int ts, Rgb baseSkin, ref ZSample o)
        {
            int seed = ts * 9 + 400;
            Rgb c = Rgb.Lerp(baseSkin.Scale(0.7f), new Rgb(0.2f, 0.17f, 0.13f), Smooth(0.35f, 0.8f, Fbm(u, v, 6, 4, seed)) * 0.8f);
            float scratch = CharacterTextures.Scratches(u, v, seed);
            c = Rgb.Lerp(c, new Rgb(0.38f, 0.07f, 0.06f), scratch * 0.6f);
            o.Color = c; o.Height = 0.35f * Fbm(u, v, 30, 3, seed + 1) - 0.3f * scratch; o.Smooth = 0.22f; o.Ao = 0.95f;
        }

        private static void Bone(float u, float v, int ts, ref ZSample o)
        {
            int seed = ts * 11 + 500;
            float grain = Fbm(u, v, 12, 3, seed);
            Rgb c = Rgb.Lerp(new Rgb(0.62f, 0.56f, 0.42f), new Rgb(0.82f, 0.77f, 0.62f), grain);
            c = Rgb.Lerp(c, new Rgb(0.34f, 0.24f, 0.12f), Smooth(0.6f, 0.9f, Fbm(u, v, 8, 3, seed + 1)) * 0.6f);   // sarro y manchas
            float crack = Smooth(0.03f, 0f, Math.Abs(Frac(u * 8f + Fbm(u, v, 4, 2, seed + 2) * 0.4f) - 0.5f) - 0.47f);
            o.Color = Rgb.Lerp(c, c.Scale(0.4f), crack * 0.7f); o.Height = 0.3f * grain - 0.4f * crack; o.Smooth = 0.4f; o.Ao = 1f - 0.4f * crack;
        }

        /// <summary>Ojo (esfera con el polo mirando al frente: v=1 es el centro): esclerótica amarillenta con venas, iris gris cubierto por un velo lechoso y pupila apagada. Brilla tenue.</summary>
        private static void Eye(float u, float v, int ts, ref ZSample o)
        {
            int seed = ts * 13 + 600;
            float r = (1f - v) * 2f;   // 0 en el polo (centro del ojo), 1 en el ecuador
            Rgb sclera = Rgb.Lerp(new Rgb(0.7f, 0.66f, 0.5f), new Rgb(0.78f, 0.74f, 0.62f), Fbm(u, v, 8, 3, seed));
            float vein = Smooth(0.9f, 0.96f, Ridged(u, v, 6, 2, seed + 1)) * Smooth(0.35f, 0.75f, r);
            sclera = Rgb.Lerp(sclera, new Rgb(0.6f, 0.15f, 0.14f), vein * 0.8f);
            float iris = Smooth(0.5f, 0.42f, r);
            Rgb ic = Rgb.Lerp(new Rgb(0.55f, 0.6f, 0.58f), new Rgb(0.4f, 0.45f, 0.42f), Fbm(u, v, 14, 2, seed + 2));
            float pupil = Smooth(0.2f, 0.14f, r);
            Rgb c = Rgb.Lerp(sclera, ic, iris);
            c = Rgb.Lerp(c, new Rgb(0.08f, 0.08f, 0.07f), pupil * 0.85f);
            c = Rgb.Lerp(c, new Rgb(0.78f, 0.8f, 0.74f), Smooth(0.5f, 0.1f, r) * 0.45f);   // velo lechoso
            o.Color = c; o.Smooth = 0.8f - 0.3f * Smooth(0.5f, 0.1f, r); o.Ao = 1f;
            // brillo propio: el iris y el velo (el tinte lo pone el material por zombi)
            float glow = iris * (1f - pupil * 0.8f);
            o.Emit = new Rgb(glow, glow, glow);
        }

        private static void Hair(float u, float v, int ts, ref ZSample o)
        {
            int seed = ts * 17 + 700;
            float strand = 0.5f + 0.5f * (float)Math.Sin(u * 160f + Fbm(u, v, 6, 2, seed) * 6f);
            Rgb c = Rgb.Lerp(new Rgb(0.06f, 0.05f, 0.04f), new Rgb(0.2f, 0.17f, 0.13f), strand * Fbm(u, v, 10, 3, seed + 1));
            c = Rgb.Lerp(c, new Rgb(0.22f, 0.04f, 0.04f), Smooth(0.7f, 0.9f, Fbm(u, v, 5, 3, seed + 2)) * 0.5f);   // apelmazado con sangre seca
            o.Color = c; o.Height = 0.5f * strand; o.Smooth = 0.28f; o.Ao = 0.8f + 0.2f * strand;
        }

        /// <summary>Tejido al descubierto (muñón del cuello, heridas profundas): rojo oscuro húmedo con vetas claras.</summary>
        private static void Tissue(float u, float v, int ts, ref ZSample o)
        {
            int seed = ts * 19 + 800;
            float f = Fbm(u, v, 8, 4, seed);
            Rgb c = Rgb.Lerp(new Rgb(0.22f, 0.03f, 0.03f), new Rgb(0.48f, 0.12f, 0.1f), f);
            float fat = Smooth(0.62f, 0.78f, Fbm(u, v, 6, 3, seed + 1));
            c = Rgb.Lerp(c, new Rgb(0.6f, 0.45f, 0.3f), fat * 0.7f);
            float d = Dist(u, v, 0.5f, 0.5f);
            c = Rgb.Lerp(c, new Rgb(0.74f, 0.68f, 0.56f), Smooth(0.14f, 0.1f, d) * 0.9f);   // hueso en el centro (corte del cuello)
            o.Color = c; o.Height = 0.4f * f; o.Smooth = 0.65f; o.Ao = 0.9f;
        }

        private static void FeatureA(string typeId, float u, float v, int ts, Rgb baseSkin, ref ZSample o)
        {
            int seed = ts * 23 + 900;
            switch (typeId)
            {
                case ZombieIds.Tank:
                {
                    // placas de tejido cicatrizado: celdas tipo grietas con bordes elevados, cuero grisáceo
                    float cell = Voronoi(u, v, 5, seed);
                    float edge = Smooth(0.06f, 0.0f, cell);
                    Rgb c = Rgb.Lerp(new Rgb(0.4f, 0.38f, 0.33f), new Rgb(0.55f, 0.5f, 0.42f), Fbm(u, v, 8, 3, seed));
                    c = Rgb.Lerp(c, new Rgb(0.2f, 0.07f, 0.06f), edge * 0.8f);   // fisuras oscuras con sangre
                    c = Rgb.Lerp(c, c.Scale(0.65f), Smooth(0.6f, 0.9f, Fbm(u, v, 5, 3, seed + 1)) * 0.5f);
                    o.Color = c; o.Height = 0.9f * (1f - edge) + 0.3f * Fbm(u, v, 30, 2, seed + 2); o.Smooth = 0.2f; o.Ao = 1f - 0.5f * edge;
                    return;
                }
                case ZombieIds.Explosive:
                {
                    // vientre tenso y translúcido, amarillento, con venas naranjas que brillan y estrías
                    Rgb c = Rgb.Lerp(new Rgb(0.74f, 0.58f, 0.4f), new Rgb(0.82f, 0.66f, 0.46f), Fbm(u, v, 6, 3, seed));
                    float vein = Math.Max(Smooth(0.88f, 0.96f, Ridged(u, v, 3, 3, seed + 1)), Smooth(0.9f, 0.97f, Ridged(u, v, 6, 2, seed + 2)) * 0.8f);
                    float stretch = Smooth(0.012f, 0f, Math.Abs(Frac(v * 26f + Fbm(u, v, 4, 2, seed + 3) * 1.5f) - 0.5f) - 0.46f) * 0.6f;
                    c = Rgb.Lerp(c, new Rgb(0.7f, 0.25f, 0.12f), vein * 0.85f);
                    c = Rgb.Lerp(c, c.Scale(0.8f), stretch);
                    o.Color = c; o.Height = 0.3f * vein + 0.2f * Fbm(u, v, 20, 2, seed + 4) - 0.4f * stretch; o.Smooth = 0.5f; o.Ao = 1f;
                    o.Emit = new Rgb(vein * 1f, vein * 0.38f, vein * 0.08f);
                    return;
                }
                case ZombieIds.Spitter:
                {
                    // bolsas verdosas translúcidas con venas oscuras
                    Rgb c = Rgb.Lerp(new Rgb(0.46f, 0.52f, 0.3f), new Rgb(0.58f, 0.64f, 0.34f), Fbm(u, v, 6, 3, seed));
                    float vein = Smooth(0.88f, 0.96f, Ridged(u, v, 3, 3, seed + 1));
                    c = Rgb.Lerp(c, new Rgb(0.2f, 0.3f, 0.16f), vein * 0.8f);
                    c = Rgb.Lerp(c, new Rgb(0.3f, 0.34f, 0.12f), Smooth(0.65f, 0.85f, Fbm(u, v, 4, 3, seed + 2)) * 0.5f);
                    o.Color = c; o.Height = 0.3f * vein + 0.2f * Fbm(u, v, 22, 2, seed + 3); o.Smooth = 0.55f; o.Ao = 1f;
                    return;
                }
                default:
                {
                    // piel desgarrada: costado abierto con carne y costras
                    float f = Fbm(u, v, 5, 4, seed);
                    Rgb c = Rgb.Lerp(baseSkin.Scale(0.8f), new Rgb(0.4f, 0.1f, 0.09f), Smooth(0.5f, 0.75f, f));
                    o.Color = c; o.Height = 0.4f * f; o.Smooth = 0.4f; o.Ao = 0.9f;
                    return;
                }
            }
        }

        private static void FeatureB(string typeId, float u, float v, int ts, Rgb baseSkin, ref ZSample o)
        {
            int seed = ts * 29 + 1000;
            float d = Dist(u, v, 0.5f, 0.5f);
            switch (typeId)
            {
                case ZombieIds.Explosive:
                {
                    // pústula naranja con el centro encendido
                    Rgb c = Rgb.Lerp(new Rgb(0.78f, 0.5f, 0.2f), new Rgb(0.9f, 0.7f, 0.3f), Smooth(0.5f, 0.1f, d));
                    c = Rgb.Lerp(c, new Rgb(0.45f, 0.18f, 0.08f), Smooth(0.35f, 0.5f, d) * 0.7f);
                    float core = Smooth(0.28f, 0.02f, d);
                    o.Color = c; o.Height = 0.5f * core; o.Smooth = 0.7f; o.Ao = 1f;
                    o.Emit = new Rgb(core, core * 0.45f, core * 0.1f);
                    return;
                }
                case ZombieIds.Spitter:
                {
                    Rgb c = Rgb.Lerp(new Rgb(0.4f, 0.52f, 0.2f), new Rgb(0.7f, 0.78f, 0.3f), Smooth(0.5f, 0.05f, d));
                    o.Color = c; o.Height = 0.5f * Smooth(0.4f, 0.05f, d); o.Smooth = 0.75f; o.Ao = 1f;
                    float core = Smooth(0.2f, 0.02f, d) * 0.4f;
                    o.Emit = new Rgb(core * 0.5f, core, core * 0.2f);
                    return;
                }
                default:
                {
                    Rgb c = Rgb.Lerp(baseSkin.Scale(0.7f), new Rgb(0.3f, 0.06f, 0.05f), Smooth(0.5f, 0.75f, Fbm(u, v, 6, 3, seed)));
                    o.Color = c; o.Height = 0.3f * Fbm(u, v, 12, 3, seed + 1); o.Smooth = 0.3f; o.Ao = 0.95f;
                    return;
                }
            }
        }

        // ================================================================== ropa (común)
        private static void PaintCloth(float u, float v, ref ZSample o)
        {
            int cx = Math.Min(3, (int)(u * 4f)), cy = Math.Min(3, (int)(v * 4f));
            int id = cy * 4 + cx;
            float lu = u * 4f - cx, lv = v * 4f - cy;
            int seed = id * 31 + 2000;
            float grime = Fbm(lu, lv, 5, 4, seed);
            float weave = 0.5f + 0.5f * Fbm(lu, lv, 90, 1, seed + 13);   // trama como grano fino (sin patrón regular: no hace muaré)
            Rgb c; o.Smooth = 0.2f; o.Alpha = 1f; o.Ao = 1f;
            switch (id)
            {
                case ZombieAtlas.ClShirt:   // franela de cuadros desteñida
                {
                    float chk = Math.Max(Smooth(0.46f, 0.5f, Math.Abs(Frac(lu * 6f) - 0.5f) * -1f + 0.5f), 0f);
                    float a1 = Frac(lu * 6f) < 0.5f ? 1f : 0f, a2 = Frac(lv * 6f) < 0.5f ? 1f : 0f;
                    c = a1 + a2 == 1f ? new Rgb(0.34f, 0.18f, 0.14f) : (a1 + a2 == 2f ? new Rgb(0.2f, 0.12f, 0.1f) : new Rgb(0.44f, 0.38f, 0.3f));
                    c = c.Scale(0.88f + 0.2f * weave); o.Height = 0.2f * weave;
                    break;
                }
                case ZombieAtlas.ClTee:     // punto de algodón gris
                {
                    float rib = (float)Math.Abs(Math.Sin(lu * 100f * Math.PI));
                    c = new Rgb(0.42f, 0.42f, 0.4f).Scale(0.85f + 0.2f * rib); o.Height = 0.3f * rib; break;
                }
                case ZombieAtlas.ClMilitary:   // camuflaje oscuro
                {
                    c = new Rgb(0.2f, 0.22f, 0.15f);
                    if (Fbm(lu, lv, 3, 3, seed + 1) > 0.54f) c = new Rgb(0.24f, 0.2f, 0.13f);
                    if (Fbm(lu * 1.3f, lv, 3, 3, seed + 2) > 0.6f) c = new Rgb(0.08f, 0.09f, 0.07f);
                    c = c.Scale(0.9f + 0.18f * weave); o.Height = 0.25f * weave; break;
                }
                case ZombieAtlas.ClLab:     // bata blanca amarillenta con manchas
                {
                    c = Rgb.Lerp(new Rgb(0.62f, 0.6f, 0.52f), new Rgb(0.7f, 0.68f, 0.6f), grime).Scale(0.92f + 0.12f * weave);
                    c = Rgb.Lerp(c, new Rgb(0.45f, 0.36f, 0.2f), Smooth(0.6f, 0.85f, Fbm(lu, lv, 4, 3, seed + 3)) * 0.6f);
                    c = Rgb.Lerp(c, new Rgb(0.28f, 0.05f, 0.05f), Smooth(0.72f, 0.9f, Fbm(lu, lv, 6, 3, seed + 4)) * 0.8f);
                    o.Height = 0.2f * weave; break;
                }
                case ZombieAtlas.ClSuit:    // traje de lana oscuro con raya diplomática
                {
                    float stripe = Smooth(0.05f, 0f, Math.Abs(Frac(lu * 12f) - 0.5f) - 0.47f);
                    c = new Rgb(0.13f, 0.13f, 0.15f).Scale(0.9f + 0.2f * weave); c = Rgb.Lerp(c, new Rgb(0.3f, 0.3f, 0.32f), stripe * 0.5f); o.Height = 0.2f * weave; break;
                }
                case ZombieAtlas.ClTrousers: c = new Rgb(0.16f, 0.15f, 0.14f).Scale(0.88f + 0.22f * weave); o.Height = 0.25f * weave; break;
                case ZombieAtlas.ClJeans:   // vaquero con sarga diagonal y desteñido
                {
                    float twill = (float)Math.Sin((lu + lv) * 220f * Math.PI) * 0.5f + 0.5f;
                    c = Rgb.Lerp(new Rgb(0.14f, 0.18f, 0.27f), new Rgb(0.3f, 0.36f, 0.45f), Smooth(0.4f, 0.8f, Fbm(lu, lv, 4, 3, seed + 5)) * 0.6f).Scale(0.85f + 0.25f * twill);
                    o.Height = 0.3f * twill; break;
                }
                case ZombieAtlas.ClOveralls:   // mono de lona anaranjado desteñido con grasa
                {
                    c = Rgb.Lerp(new Rgb(0.5f, 0.28f, 0.1f), new Rgb(0.4f, 0.36f, 0.16f), Fbm(lu, lv, 3, 2, seed + 6)).Scale(0.88f + 0.2f * weave);
                    c = Rgb.Lerp(c, new Rgb(0.07f, 0.06f, 0.05f), Smooth(0.6f, 0.85f, Fbm(lu, lv, 5, 3, seed + 7)) * 0.7f); o.Height = 0.25f * weave; break;
                }
                case ZombieAtlas.ClRags:
                case ZombieAtlas.ClSack:   // arpillera sucia
                {
                    float thread = (float)(Math.Sin(lu * 70f * Math.PI) + Math.Sin(lv * 74f * Math.PI)) * 0.25f + 0.5f;
                    c = Rgb.Lerp(new Rgb(0.34f, 0.27f, 0.17f), new Rgb(0.2f, 0.17f, 0.12f), grime).Scale(0.8f + 0.35f * thread);
                    o.Height = 0.5f * thread; break;
                }
                case ZombieAtlas.ClLeather:
                {
                    float crack = Smooth(0.03f, 0f, Math.Abs(Voronoi(lu, lv, 9, seed) - 0.05f));
                    c = Rgb.Lerp(new Rgb(0.12f, 0.08f, 0.06f), new Rgb(0.26f, 0.18f, 0.12f), grime).Scale(1f - 0.3f * crack);
                    o.Height = 0.3f * grime - 0.4f * crack; o.Smooth = 0.35f; break;
                }
                case ZombieAtlas.ClMetal:
                {
                    float rust = Smooth(0.45f, 0.7f, Fbm(lu, lv, 6, 5, seed));
                    float sc = CharacterTextures.Scratches(lu, lv, seed);
                    c = Rgb.Lerp(new Rgb(0.3f, 0.3f, 0.31f), new Rgb(0.38f, 0.17f, 0.07f), rust).Scale(1f - 0.2f * sc);
                    float rivet = Smooth(0.03f, 0.015f, Math.Min(Dist(lu, lv, 0.1f, 0.1f), Math.Min(Dist(lu, lv, 0.9f, 0.1f), Math.Min(Dist(lu, lv, 0.1f, 0.9f), Dist(lu, lv, 0.9f, 0.9f)))));
                    o.Metal = 0.85f - 0.6f * rust; o.Smooth = 0.4f - 0.25f * rust; o.Height = 0.3f * rust + 0.8f * rivet - 0.3f * sc;
                    o.Color = c; break;
                }
                case ZombieAtlas.ClRubber:
                {
                    float tread = Smooth(0.45f, 0.55f, (float)Math.Sin(lv * 40f * Math.PI + (float)Math.Sin(lu * 6f * Math.PI) * 1.5f) * 0.5f + 0.5f);
                    c = new Rgb(0.07f, 0.07f, 0.07f).Scale(0.8f + 0.5f * tread);
                    c = Rgb.Lerp(c, new Rgb(0.2f, 0.17f, 0.12f), Smooth(0.6f, 0.9f, grime) * 0.5f);
                    o.Height = tread; o.Smooth = 0.25f; break;
                }
                case ZombieAtlas.ClWebbing:
                {
                    float rib = (float)Math.Abs(Math.Sin(lv * 60f * Math.PI));
                    c = new Rgb(0.2f, 0.21f, 0.15f).Scale(0.85f + 0.25f * rib); o.Height = 0.35f * rib; break;
                }
                case ZombieAtlas.ClDoor:   // pintura verde azulada desconchada sobre óxido
                {
                    float peel = Smooth(0.5f, 0.62f, Fbm(lu, lv, 5, 4, seed));
                    c = Rgb.Lerp(new Rgb(0.34f, 0.44f, 0.4f), new Rgb(0.4f, 0.2f, 0.09f), peel);
                    c = Rgb.Lerp(c, new Rgb(0.12f, 0.1f, 0.08f), Smooth(0.7f, 0.9f, grime) * 0.3f);
                    o.Metal = 0.6f * peel; o.Smooth = 0.3f; o.Height = 0.4f * peel; break;
                }
                default:   // venda
                {
                    float f = (float)Math.Abs(Math.Sin((lu + lv * 0.2f) * 40f * Math.PI));
                    c = Rgb.Lerp(new Rgb(0.62f, 0.56f, 0.44f), new Rgb(0.45f, 0.38f, 0.28f), grime).Scale(0.9f + 0.15f * f);
                    c = Rgb.Lerp(c, new Rgb(0.32f, 0.05f, 0.05f), Smooth(0.6f, 0.82f, Fbm(lu, lv, 5, 3, seed + 8)) * 0.85f);
                    o.Height = 0.3f * f; break;
                }
            }
            // pliegues y arrugas de la tela (correr alrededor de la pieza) y manchas de tono: rompen la superficie lisa
            bool soft = id != ZombieAtlas.ClMetal && id != ZombieAtlas.ClRubber && id != ZombieAtlas.ClDoor && id != ZombieAtlas.ClLeather;
            if (soft)
            {
                float fold = Fbm(lu * 0.4f, lv * 3.4f, 3, 3, seed + 20);
                float tone = Fbm(lu, lv, 2, 3, seed + 21);
                c = c.Scale(0.82f + 0.36f * tone);
                o.Height += 1.1f * fold + 0.5f * (1f - Smooth(0.25f, 0.5f, Math.Abs(fold - 0.5f)));
                c = c.Scale(0.9f + 0.2f * fold);
            }
            if (id != ZombieAtlas.ClMetal) o.Color = c;
            // suciedad, barro hacia el borde inferior (v pequeña = dobladillo), quemaduras y sangre
            Rgb col = o.Color;
            col = Rgb.Lerp(col, new Rgb(0.2f, 0.17f, 0.12f), Smooth(0.55f, 0.9f, grime) * 0.35f);
            float mud = Smooth(0.6f, 0.9f, Fbm(lu, lv, 3, 4, seed + 12));   // barro a manchas (sin depender del borde de la celda: cada pieza usa su propio v)
            col = Rgb.Lerp(col, new Rgb(0.18f, 0.13f, 0.09f), mud * 0.35f);
            float burn = Smooth(0.72f, 0.88f, Fbm(lu, lv, 4, 3, seed + 9));
            col = Rgb.Lerp(col, new Rgb(0.04f, 0.035f, 0.03f), burn * 0.8f);
            float blood = Smooth(0.7f, 0.86f, Fbm(lu + 0.2f, lv, 5, 3, seed + 10));
            if (id != ZombieAtlas.ClMetal && id != ZombieAtlas.ClRubber && id != ZombieAtlas.ClDoor) col = Rgb.Lerp(col, new Rgb(0.22f, 0.04f, 0.04f), blood * 0.75f);
            o.Color = col; o.Ao = 0.85f + 0.15f * weave - 0.1f * mud;
            // agujeros y desgarros (solo ropa blanda): manchas recortadas por el alfa, más frecuentes cerca del dobladillo y en los harapos
            if (id != ZombieAtlas.ClMetal && id != ZombieAtlas.ClRubber && id != ZombieAtlas.ClDoor && id != ZombieAtlas.ClLeather && id != ZombieAtlas.ClWebbing)
            {
                float bias = id == ZombieAtlas.ClRags ? 0.12f : 0f;
                float holes = Fbm(lu, lv, 4, 3, seed + 11) - bias;
                float rim = Smooth(0.78f, 0.84f, holes + 0.05f);
                o.Alpha = holes > 0.76f ? 0f : 1f;
                float edge = Smooth(0.7f, 0.76f, holes);
                o.Color = Rgb.Lerp(o.Color, new Rgb(0.07f, 0.04f, 0.03f), edge * 0.8f * rim);   // borde chamuscado del desgarro
                o.Height -= edge * 0.5f;
            }
        }

        // ================================================================== heridas (decals con alfa)
        private static void PaintWound(float u, float v, ref ZSample o)
        {
            int cx = Math.Min(3, (int)(u * 4f)), cy = Math.Min(3, (int)(v * 4f));
            int id = cy * 4 + cx;
            float lu = u * 4f - cx, lv = v * 4f - cy;
            int seed = id * 37 + 3000;
            float n = Fbm(lu, lv, 7, 3, seed);
            float d = Dist(lu, lv, 0.5f, 0.5f);
            o.Alpha = 0f; o.Smooth = 0.6f; o.Ao = 1f;
            Rgb blood = Rgb.Lerp(new Rgb(0.33f, 0.04f, 0.04f), new Rgb(0.16f, 0.03f, 0.03f), Fbm(lu, lv, 5, 2, seed + 1));
            switch (id)
            {
                case ZombieAtlas.WdBite:
                case ZombieAtlas.WdBiteB:
                {
                    // mordida: arco de punciones alrededor de una zona magullada
                    float ring = Smooth(0.07f, 0.02f, Math.Abs(d - 0.28f)) * (lv > 0.3f ? 1f : 0f);
                    float punct = 0f;
                    for (int k = 0; k < 9; k++)
                    {
                        float ang = 3.14159f * (0.1f + 0.8f * k / 8f);
                        punct = Math.Max(punct, Smooth(0.045f, 0.015f, Dist(lu, lv, 0.5f + 0.28f * (float)Math.Cos(ang), 0.5f + 0.28f * (float)Math.Sin(ang) * (id == ZombieAtlas.WdBiteB ? -1f : 1f) )));
                    }
                    float bruise = Smooth(0.42f, 0.2f, d + (n - 0.5f) * 0.18f);
                    o.Color = Rgb.Lerp(new Rgb(0.34f, 0.2f, 0.3f), new Rgb(0.2f, 0.03f, 0.03f), Math.Max(punct, ring * 0.4f));
                    o.Alpha = Math.Max(bruise * 0.9f, punct) > 0.4f ? 1f : 0f; o.Height = -punct; break;
                }
                case ZombieAtlas.WdGash:
                case ZombieAtlas.WdGashB:
                {
                    // tajo irregular con carne y bordes oscuros
                    float line = lv - (0.5f + 0.1f * (float)Math.Sin(lu * 9f + seed) + (n - 0.5f) * 0.12f);
                    float w = 0.05f + 0.04f * (float)Math.Sin(lu * 3.14159f);
                    float inside = Smooth(w, w * 0.4f, Math.Abs(line)) * Smooth(0.06f, 0.2f, Math.Min(lu, 1f - lu));
                    o.Color = Rgb.Lerp(new Rgb(0.2f, 0.02f, 0.02f), new Rgb(0.55f, 0.15f, 0.12f), Smooth(w * 0.3f, 0f, Math.Abs(line)));
                    float drip = 0f;
                    for (int k = 0; k < 6; k++)
                    {
                        float xk = (k + 0.5f) / 6f + (TileableNoise.Hash01(k, seed, 4) - 0.5f) * 0.06f, lenK = 0.08f + 0.3f * TileableNoise.Hash01(k, seed, 5);
                        float yLine = 0.5f + 0.1f * (float)Math.Sin(xk * 9f + seed);
                        if (xk > 0.22f && xk < 0.78f && lv < yLine && lv > yLine - lenK) drip = Math.Max(drip, Smooth(0.013f, 0.005f, Math.Abs(lu - xk)));
                    }
                    o.Alpha = (inside > 0.25f || drip > 0.5f) ? 1f : 0f; o.Height = -inside; if (drip > 0.5f && inside <= 0.25f) o.Color = blood; break;
                }
                case ZombieAtlas.WdBullet:
                {
                    float hole = Smooth(0.07f, 0.05f, d);
                    float ring = Smooth(0.2f, 0.07f, d + (n - 0.5f) * 0.1f);
                    o.Color = Rgb.Lerp(blood, new Rgb(0.03f, 0.01f, 0.01f), hole);
                    o.Alpha = ring > 0.3f ? 1f : 0f; o.Height = -hole; break;
                }
                case ZombieAtlas.WdStain:
                case ZombieAtlas.WdStainB:
                {
                    float blob = Smooth(0.46f, 0.26f, d + (n - 0.5f) * 0.4f);
                    o.Color = blood; o.Alpha = blob > 0.35f ? 1f : 0f; o.Smooth = 0.75f; break;
                }
                case ZombieAtlas.WdBruise:
                {
                    float blob = Smooth(0.46f, 0.2f, d + (n - 0.5f) * 0.3f);
                    o.Color = Rgb.Lerp(new Rgb(0.34f, 0.2f, 0.36f), new Rgb(0.42f, 0.46f, 0.2f), Smooth(0.2f, 0.45f, d + (n - 0.5f) * 0.2f));
                    o.Alpha = blob > 0.4f ? 1f : 0f; o.Smooth = 0.35f; break;
                }
                case ZombieAtlas.WdScab:
                {
                    float blob = Smooth(0.42f, 0.22f, d + (n - 0.5f) * 0.25f);
                    o.Color = Rgb.Lerp(new Rgb(0.2f, 0.1f, 0.07f), new Rgb(0.34f, 0.2f, 0.12f), Fbm(lu, lv, 12, 2, seed + 2));
                    o.Alpha = blob > 0.4f ? 1f : 0f; o.Height = 0.6f * blob; o.Smooth = 0.2f; break;
                }
                case ZombieAtlas.WdDrip:
                {
                    float streams = Smooth(0.07f, 0.02f, Math.Abs(Frac(lu * 5f + Fbm(lu, lv, 3, 2, seed) * 0.6f) - 0.5f)) ;
                    float len = Smooth(0.0f, 0.9f, lv) * (0.35f + 0.65f * Fbm(Frac(lu * 5f) * 0.5f + 0.2f, 0.3f, 3, 2, seed + 3));
                    float run = streams * Smooth(0.15f, 0.45f, len + (1f - lv) * 0.25f);
                    float top = Smooth(0.75f, 0.9f, lv) * Smooth(0.5f, 0.3f, Math.Abs(lu - 0.5f));
                    o.Color = blood; o.Alpha = (run > 0.35f || top > 0.4f) ? 1f : 0f; o.Smooth = 0.8f; break;
                }
                case ZombieAtlas.WdTear:
                case ZombieAtlas.WdTearB:
                {
                    // desgarro de la ropa: borde de tela rasgado con carne y sangre detrás
                    float rim = d + (n - 0.5f) * 0.3f;
                    float inner = Smooth(0.3f, 0.2f, rim), outer = Smooth(0.42f, 0.34f, rim);
                    o.Color = Rgb.Lerp(Rgb.Lerp(new Rgb(0.12f, 0.1f, 0.08f), new Rgb(0.2f, 0.03f, 0.03f), Smooth(0.2f, 0.32f, rim)), new Rgb(0.4f, 0.1f, 0.08f), inner * 0.6f);
                    o.Alpha = outer > 0.4f ? 1f : 0f; o.Height = -inner; break;
                }
                case ZombieAtlas.WdHead:
                {
                    float blob = Smooth(0.38f, 0.16f, d + (n - 0.5f) * 0.25f);
                    float bone = Smooth(0.14f, 0.08f, d);
                    o.Color = Rgb.Lerp(Rgb.Lerp(new Rgb(0.26f, 0.03f, 0.03f), new Rgb(0.5f, 0.14f, 0.1f), Smooth(0.3f, 0.15f, d)), new Rgb(0.74f, 0.68f, 0.58f), bone);
                    o.Alpha = blob > 0.35f ? 1f : 0f; o.Height = -blob * 0.6f; break;
                }
                case ZombieAtlas.WdBurn:
                {
                    float blob = Smooth(0.44f, 0.22f, d + (n - 0.5f) * 0.35f);
                    o.Color = Rgb.Lerp(new Rgb(0.03f, 0.025f, 0.02f), new Rgb(0.28f, 0.1f, 0.06f), Smooth(0.25f, 0.42f, d));
                    o.Alpha = blob > 0.4f ? 1f : 0f; o.Smooth = 0.15f; o.Height = 0.2f * n; break;
                }
                case ZombieAtlas.WdBone:
                {
                    float blob = Smooth(0.34f, 0.2f, d + (n - 0.5f) * 0.2f);
                    o.Color = Rgb.Lerp(new Rgb(0.45f, 0.06f, 0.05f), new Rgb(0.78f, 0.72f, 0.6f), Smooth(0.28f, 0.14f, d));
                    o.Alpha = blob > 0.4f ? 1f : 0f; o.Height = 0.3f * blob; break;
                }
                default:   // salpicadura de gotas
                {
                    float drops = 0f;
                    for (int k = 0; k < 14; k++)
                    {
                        float ang = (float)(TileableNoise.Hash01(k, seed, 1) * 6.283), rad = 0.1f + 0.38f * TileableNoise.Hash01(k, seed, 2);
                        float sz = 0.015f + 0.05f * TileableNoise.Hash01(k, seed, 3) * (1f - rad);
                        drops = Math.Max(drops, Smooth(sz, sz * 0.5f, Dist(lu, lv, 0.5f + rad * (float)Math.Cos(ang), 0.5f + rad * (float)Math.Sin(ang))));
                    }
                    o.Color = blood; o.Alpha = drops > 0.4f ? 1f : 0f; o.Smooth = 0.8f; break;
                }
            }
            // sin sangre en los bordes de la celda (los mipmaps no mezclan con la vecina)
            float edge = Math.Min(Math.Min(lu, 1f - lu), Math.Min(lv, 1f - lv));
            if (edge < 0.03f) o.Alpha = 0f;
        }

        // ================================================================== utilidades
        /// <summary>Distancia normalizada al borde de la celda de Voronoi más cercana (0 en el borde): grietas y placas.</summary>
        private static float Voronoi(float u, float v, int cells, int seed)
        {
            float best = 9f, second = 9f;
            float x = u * cells, y = v * cells;
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = ix + dx, cy = iy + dy;
                    int wx = TileableNoise.Mod(cx, cells), wy = TileableNoise.Mod(cy, cells);
                    float px = cx + TileableNoise.Hash01(wx, wy, seed), py = cy + TileableNoise.Hash01(wx, wy, seed + 1);
                    float d = (px - x) * (px - x) + (py - y) * (py - y);
                    if (d < best) { second = best; best = d; } else if (d < second) second = d;
                }
            return (float)(Math.Sqrt(second) - Math.Sqrt(best));
        }

        private static float Fbm(float u, float v, int f, int o, int s) => TileableNoise.Fbm(u, v, f, o, s);
        private static float Smooth(float e0, float e1, float x) { float k = Math.Max(0f, Math.Min(1f, (x - e0) / (e1 - e0))); return k * k * (3f - 2f * k); }
        private static float Frac(float x) => x - (float)Math.Floor(x);
        private static float Dist(float x, float y, float cx, float cy) => (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        private static byte B(float v) => (byte)(Math.Max(0f, Math.Min(1f, v)) * 255f + 0.5f);
    }
}
