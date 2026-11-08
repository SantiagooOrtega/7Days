using System;
using System.Threading.Tasks;

namespace SevenDays.Procedural
{
    public enum AtlasKind { Uniform, Gear, Helmet, Weapon }

    /// <summary>Mapas de un atlas: color (RGBA, se comprime a BC), normales (RGBA, sin comprimir) y máscara (R metal, G oclusión, A suavidad: URP Lit los lee así).</summary>
    public sealed class AtlasMaps
    {
        public int Size, NormalSize;   // las normales van a la mitad de resolución (sin comprimir ocupan 4 veces más que el color en BC)
        public byte[] Albedo, Normal, Mask;
    }

    /// <summary>Lo que pinta una celda en un punto: color, altura (para las normales), metal, suavidad y oclusión.</summary>
    public struct TexSample
    {
        public Rgb Color;
        public float Height, Metal, Smooth, Ao;
    }

    /// <summary>
    /// Texturas de personajes y armas (puras, deterministas, en hilos): cada atlas se divide en celdas (AtlasCell) y cada celda tiene su "pintor"
    /// (camuflaje woodland con trama, cordura con costuras y MOLLE, correas, velcro, cuero con barro, suela con tacos, rodilleras, metal rayado, radio,
    /// cubierta del casco, herrajes, placa calavera horneada desde su relieve, gorra, pañuelo, red; y para armas pavonado, fosfatado, polímero, madera,
    /// cinta, goma, número de serie, anodizado, lentes, rieles, óxido...). Las piezas proyectan sus UV en su celda (MeshBuilder).
    /// </summary>
    public static class CharacterTextures
    {
        // ---- celdas de cada atlas
        public const int UCamo = 0, UCamoMud = 1, UFold = 2, UKnit = 3;                         // uniforme: 2x2 (UFold: 4 subceldas, 0 codo, 1 rodilla)
        public const int GVest = 0, GPouch = 1, GWebbing = 2, GVelcro = 3, GGlove = 4, GKnuckle = 5, GBoot = 6, GSole = 7,
                         GLaces = 8, GPad = 9, GMetal = 10, GRadio = 11, GBelt = 12, GHolster = 13, GArmband = 14, GPatch = 15;   // equipo: 4x4
        public const int HCover = 0, HHardware = 1, HSkull = 2, HHeadwear = 3;                 // casco: 2x2 (las celdas 1 y 3 se dividen en 2x2)
        public const int HwRail = 0, HwPolymer = 1, HwRubber = 2, HwLens = 3;                  // subceldas de herrajes
        public const int HwCap = 0, HwBandana = 1, HwNet = 2, HwStrap = 3;                     // subceldas de prendas de cabeza
        public const int WBlued = 0, WPark = 1, WPolyBlack = 2, WPolyTan = 3, WWood = 4, WRubber = 5, WTape = 6, WSerial = 7,
                         WAnodized = 8, WLens = 9, WRail = 10, WRust = 11, WGripWrap = 12, WWoodLight = 13, WDot = 14, WBurnt = 15;   // armas: 4x4

        public static int Grid(AtlasKind k) => k == AtlasKind.Gear || k == AtlasKind.Weapon ? 4 : 2;
        public static AtlasCell Cell(AtlasKind k, int index) => AtlasCell.Grid(index, Grid(k), 0.03f);
        public static AtlasCell Sub(AtlasKind k, int index, int sub) => Cell(k, index).Sub(sub, 2, 0.03f);

        /// <summary>Tamaño por defecto: casco 2048 (la calavera de cerca), el resto 1024.</summary>
        public static int DefaultSize(AtlasKind k) => k == AtlasKind.Helmet ? 2048 : 1024;

        public static AtlasMaps Generate(AtlasKind kind, CharacterVariantData v, int size)
        {
            v = v ?? new CharacterVariantData();
            int n = size, grid = Grid(kind);
            var albedo = new byte[n * n * 4];
            var mask = new byte[n * n * 4];
            var height = new float[n * n];
            Parallel.For(0, n, y =>
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, vv = (y + 0.5f) / n;
                    int cx = Math.Min(grid - 1, (int)(u * grid)), cy = Math.Min(grid - 1, (int)(vv * grid));
                    int cell = cy * grid + cx;
                    float lu = u * grid - cx, lv = vv * grid - cy;
                    var o = new TexSample { Color = new Rgb(0.5f, 0.5f, 0.5f), Smooth = 0.2f, Ao = 1f };
                    switch (kind)
                    {
                        case AtlasKind.Uniform: Uniform(cell, lu, lv, v, ref o); break;
                        case AtlasKind.Gear: Gear(cell, lu, lv, v, ref o); break;
                        case AtlasKind.Helmet: Helmet(cell, lu, lv, v, ref o); break;
                        default: Weapon(cell, lu, lv, ref o); break;
                    }
                    int i = y * n + x;
                    height[i] = o.Height;
                    albedo[i * 4] = B(o.Color.R * o.Ao); albedo[i * 4 + 1] = B(o.Color.G * o.Ao); albedo[i * 4 + 2] = B(o.Color.B * o.Ao); albedo[i * 4 + 3] = 255;
                    mask[i * 4] = B(o.Metal); mask[i * 4 + 1] = B(o.Ao); mask[i * 4 + 2] = 0; mask[i * 4 + 3] = B(o.Smooth);
                }
            });
            // normales a media resolución (media de 2x2 alturas): mismo relieve, la cuarta parte de memoria
            int nn = n > 256 ? n / 2 : n;
            float[] h2 = height;
            if (nn != n)
            {
                h2 = new float[nn * nn];
                for (int y = 0; y < nn; y++)
                    for (int x = 0; x < nn; x++)
                        h2[y * nn + x] = (height[(2 * y) * n + 2 * x] + height[(2 * y) * n + 2 * x + 1] + height[(2 * y + 1) * n + 2 * x] + height[(2 * y + 1) * n + 2 * x + 1]) * 0.25f;
            }
            return new AtlasMaps { Size = n, NormalSize = nn, Albedo = albedo, Mask = mask, Normal = Normals(h2, nn, 3f * nn / 1024f) };
        }

        /// <summary>Normales desde la altura (bordes sin envolver: las celdas no se repiten). strength escala la pendiente.</summary>
        public static byte[] Normals(float[] h, int n, float strength)
        {
            var normal = new byte[n * n * 4];
            Parallel.For(0, n, y =>
            {
                int ym = Math.Max(0, y - 1), yp = Math.Min(n - 1, y + 1);
                for (int x = 0; x < n; x++)
                {
                    int xm = Math.Max(0, x - 1), xp = Math.Min(n - 1, x + 1);
                    float dx = (h[y * n + xp] - h[y * n + xm]) * 0.5f * strength;
                    float dy = (h[yp * n + x] - h[ym * n + x]) * 0.5f * strength;
                    float inv = 1f / (float)Math.Sqrt(dx * dx + dy * dy + 1f);
                    int i = (y * n + x) * 4;
                    normal[i] = B(-dx * inv * 0.5f + 0.5f); normal[i + 1] = B(-dy * inv * 0.5f + 0.5f); normal[i + 2] = B(inv * 0.5f + 0.5f); normal[i + 3] = 255;
                }
            });
            return normal;
        }

        // ------------------------------------------------------------------ uniforme
        private static void Uniform(int cell, float u, float v, CharacterVariantData p, ref TexSample o)
        {
            int seed = p.seed * 101 + cell * 7;
            if (cell == UKnit)
            {
                // tejido de punto (pasamontañas, braga de cuello): canalé vertical
                float rib = (float)Math.Abs(Math.Sin(u * 90f * Math.PI));
                float knit = Fbm(u, v, 24, 2, seed);
                o.Color = Rgb.Lerp(new Rgb(0.06f, 0.06f, 0.06f), new Rgb(0.13f, 0.13f, 0.12f), 0.3f + 0.4f * rib + 0.3f * knit);
                o.Height = 0.35f * rib + 0.1f * knit; o.Smooth = 0.12f; o.Ao = 0.85f + 0.15f * rib;
                return;
            }
            if (cell == UFold) { Fold(u, v, p, seed, ref o); return; }
            Rgb camo = Camo(u, v, p, seed);
            // trama de la tela (sarga): diagonales finas
            float twill = (float)Math.Sin((u * 1.0f + v) * 260f * Math.PI) * 0.5f + 0.5f;
            float wrinkle = Fbm(u * 0.8f, v * 1.6f, 4, 3, seed + 3);
            float dust = Fbm(u, v, 6, 3, seed + 9);
            Rgb c = camo.Scale(0.92f + 0.12f * twill);
            c = Rgb.Lerp(c, new Rgb(0.42f, 0.39f, 0.33f), Smooth(0.55f, 0.85f, dust) * 0.22f);   // polvo
            // costuras dobles en los bordes de la celda (la pieza queda "cosida")
            float seam = Math.Min(Math.Min(u, 1f - u), Math.Min(v, 1f - v));
            float stitch = seam < 0.035f ? ((float)Math.Sin((u + v) * 600f) > 0.2f ? 1f : 0f) * Smooth(0.035f, 0.02f, seam) : 0f;
            c = Rgb.Lerp(c, c.Scale(0.6f), stitch * 0.6f);
            float ao = 1f - 0.25f * Smooth(0.6f, 0.2f, wrinkle);
            if (cell == UCamoMud)
            {
                // espinillas: barro abajo (v pequeña = tobillo) y rodilla gastada arriba
                float mudNoise = Fbm(u, v, 8, 4, seed + 11);
                float mud = Smooth(0.35f, 0.05f, v + (mudNoise - 0.5f) * 0.25f);
                float knee = Smooth(0.78f, 0.95f, v) * Smooth(0.4f, 0.75f, mudNoise);
                c = Rgb.Lerp(c, new Rgb(0.2f, 0.16f, 0.11f), mud * 0.85f);
                c = Rgb.Lerp(c, new Rgb(0.38f, 0.35f, 0.29f), knee * 0.5f);
                o.Smooth = 0.18f + mud * 0.25f;
            }
            else o.Smooth = 0.2f;
            // sangre seca, leve y rara
            float blood = Smooth(0.82f, 0.9f, Fbm(u + 0.3f, v, 5, 4, seed + 41));
            c = Rgb.Lerp(c, new Rgb(0.18f, 0.06f, 0.04f), blood * 0.55f);
            o.Color = c; o.Height = 0.12f * twill + 0.55f * wrinkle - 0.4f * stitch; o.Ao = ao;
        }

        /// <summary>
        /// Pliegues de la tela en codos (subcelda 0) y rodillas (subcelda 1): el camuflaje con crestas horizontales onduladas que se concentran en el pliegue interior de la
        /// articulación (la espalda de la pieza, -Z: u cerca de 0 y 1) y se alisan en la punta (codo, rótula), con surcos oscuros y relieve fuerte en el mapa de normales.
        /// Coordenadas de la banda: u alrededor (0.5 = la punta), v a lo largo (0.5 = la articulación).
        /// </summary>
        private static void Fold(float u, float v, CharacterVariantData p, int seed, ref TexSample o)
        {
            int sub = (u < 0.5f ? 0 : 1) + (v < 0.5f ? 0 : 2);
            float su = Frac(u * 2f), sv = Frac(v * 2f);
            int creases = sub == 1 ? 7 : 6; float amp = sub == 1 ? 1.35f : 1f;
            Rgb camo = Camo(su, sv, p, seed + sub * 7);
            float du = Math.Min(su, 1f - su);                 // 0 en el pliegue interior, 0.5 en la punta
            float crook = Smooth(0.42f, 0.08f, du);
            float ridge = 0f;
            for (int k = 0; k < creases; k++)
            {
                float c = (k + 0.5f) / creases + (TileableNoise.Hash01(k, sub, seed) - 0.5f) * 0.06f;
                float curve = c + 0.035f * (float)Math.Sin((su * 2f + k * 0.37f) * Math.PI * 2.0) + (0.5f - du) * 0.05f;   // onduladas y arqueadas hacia el pliegue
                ridge = Math.Max(ridge, Smooth(0.055f, 0.012f, Math.Abs(sv - curve)));
            }
            float strength = 0.3f + 0.7f * crook;
            float groove = (1f - ridge) * strength;
            Rgb c2 = camo.Scale(0.92f + 0.14f * ridge * strength);
            c2 = Rgb.Lerp(c2, c2.Scale(0.62f), groove * 0.55f);   // surcos más oscuros
            float dust = Fbm(su, sv, 6, 3, seed + 9);
            c2 = Rgb.Lerp(c2, new Rgb(0.42f, 0.39f, 0.33f), Smooth(0.55f, 0.85f, dust) * 0.2f);
            o.Color = c2; o.Height = ridge * strength * 2.6f * amp; o.Smooth = 0.2f; o.Ao = 1f - 0.3f * groove;
        }

        /// <summary>Woodland oscuro: base, manchas medianas, manchas oscuras y trazos claros, con bordes deformados.</summary>
        private static Rgb Camo(float u, float v, CharacterVariantData p, int seed)
        {
            float k = p.camoScale;
            float wu = u + (Fbm(u, v, 3, 2, seed + 1) - 0.5f) * 0.18f, wv = v + (Fbm(u, v, 3, 2, seed + 2) - 0.5f) * 0.18f;
            Rgb c = CharacterVariantData.C(p.camoBase);
            if (Fbm(wu * k, wv * 0.6f * k, 3, 3, seed + 5) > 0.54f) c = CharacterVariantData.C(p.camoMid);
            if (Fbm(wu * 1.3f * k, wv * 0.8f * k, 3, 3, seed + 6) > 0.6f) c = CharacterVariantData.C(p.camoDark);
            if (Fbm(wu * 2f * k, wv * 1.1f * k, 4, 2, seed + 7) > 0.67f) c = CharacterVariantData.C(p.camoLight);
            return c;
        }

        // ------------------------------------------------------------------ equipo
        private static void Gear(int cell, float u, float v, CharacterVariantData p, ref TexSample o)
        {
            int seed = p.seed * 131 + cell * 13;
            Rgb gear = CharacterVariantData.C(p.gear), dark = CharacterVariantData.C(p.gearDark);
            float grime = Fbm(u, v, 5, 4, seed);
            float edge = Math.Min(Math.Min(u, 1f - u), Math.Min(v, 1f - v));
            float edgeWear = Smooth(0.06f, 0.0f, edge) * Smooth(0.45f, 0.75f, grime);
            switch (cell)
            {
                case GVest:
                case GPouch:
                {
                    // cordura: trama cruzada fina; chaleco con filas de MOLLE; portacargador con solapa
                    float weave = (float)(Math.Sin(u * 220f * Math.PI) * Math.Sin(v * 220f * Math.PI)) * 0.5f + 0.5f;
                    Rgb c = gear.Scale(0.9f + 0.15f * weave);
                    float h = 0.1f * weave;
                    if (cell == GVest)
                    {
                        float row = Frac(v * 6f);
                        float molle = Smooth(0.08f, 0.14f, row) * Smooth(0.5f, 0.44f, row);   // cinta horizontal
                        float slot = Frac(u * 7f) < 0.08f ? 1f : 0f;                          // cortes de la cinta
                        c = Rgb.Lerp(c, gear.Scale(0.78f), molle * 0.6f);
                        h += molle * 0.6f - slot * molle * 0.6f;
                    }
                    else
                    {
                        float flap = Smooth(0.71f, 0.69f, v);                                // la solapa arriba
                        float flapEdge = Smooth(0.03f, 0f, Math.Abs(v - 0.7f));
                        c = Rgb.Lerp(c, c.Scale(0.75f), flapEdge);
                        h += (1f - flap) * 0.3f - flapEdge * 0.4f;
                    }
                    float st = Stitch(u, v, 0.05f);
                    c = Rgb.Lerp(c, c.Scale(0.65f), st * 0.7f);
                    c = Rgb.Lerp(c, new Rgb(0.3f, 0.27f, 0.22f), Smooth(0.6f, 0.9f, grime) * 0.3f);   // polvo
                    c = Rgb.Lerp(c, c.Scale(1.4f), edgeWear * 0.4f);
                    o.Color = c; o.Height = h - st * 0.3f; o.Smooth = 0.22f; o.Ao = 0.85f + 0.15f * weave;
                    return;
                }
                case GWebbing:
                case GBelt:
                {
                    // cinta: costillas a lo largo (u) y costura en zigzag
                    float rib = (float)Math.Abs(Math.Sin(v * 70f * Math.PI));
                    Rgb baseC = cell == GBelt ? dark.Scale(0.8f) : dark;
                    float zig = Smooth(0.02f, 0f, Math.Abs(Frac(u * 12f) - (0.5f + 0.3f * (float)Math.Sin(v * 40f))) * 0.2f);
                    o.Color = Rgb.Lerp(baseC.Scale(0.85f + 0.2f * rib), baseC.Scale(0.6f), zig * 0.5f);
                    o.Height = 0.35f * rib; o.Smooth = 0.2f; o.Ao = 0.85f + 0.15f * rib;
                    return;
                }
                case GVelcro:
                {
                    float hook = Fbm(u, v, 64, 2, seed);
                    o.Color = dark.Scale(0.75f + 0.3f * hook);
                    o.Height = 0.5f * hook; o.Smooth = 0.08f; o.Ao = 0.8f + 0.2f * hook;
                    // cinta de nombre lisa (en blanco: nada escrito)
                    if (v > 0.62f && v < 0.82f && u > 0.12f && u < 0.88f) { o.Color = Rgb.Lerp(dark, gear, 0.6f); o.Height = 0.25f; o.Smooth = 0.25f; }
                    return;
                }
                case GGlove:
                {
                    // cuero sintético con poros y zona de agarre punteada
                    float pores = Fbm(u, v, 48, 2, seed);
                    float dots = (Frac(u * 18f) - 0.5f) * (Frac(u * 18f) - 0.5f) + (Frac(v * 18f) - 0.5f) * (Frac(v * 18f) - 0.5f) < 0.06f ? 1f : 0f;
                    var lea = CharacterVariantData.C(p.leather);
                    o.Color = lea.Scale(0.85f + 0.3f * pores + 0.1f * dots * (v < 0.5f ? 1f : 0f));
                    o.Height = 0.2f * pores + 0.4f * dots * (v < 0.5f ? 1f : 0f); o.Smooth = 0.32f; o.Ao = 0.9f;
                    o.Color = Rgb.Lerp(o.Color, o.Color.Scale(1.5f), edgeWear * 0.5f);
                    return;
                }
                case GKnuckle:
                case GPad:
                {
                    // polímero rígido: liso con rayones; rodilleras con barro
                    var pad = CharacterVariantData.C(p.pad);
                    float ridge = cell == GKnuckle ? (float)Math.Abs(Math.Sin(u * 5f * Math.PI)) : 0f;
                    float scratch = Scratches(u, v, seed);
                    Rgb c = pad.Scale(0.95f + 0.1f * grime);
                    c = Rgb.Lerp(c, c.Scale(1.8f), scratch * 0.5f);
                    float mud = cell == GPad ? Smooth(0.55f, 0.8f, Fbm(u, v, 6, 4, seed + 5)) : 0f;
                    c = Rgb.Lerp(c, new Rgb(0.22f, 0.17f, 0.12f), mud * 0.7f);
                    o.Color = c; o.Height = 0.5f * ridge - 0.3f * scratch; o.Smooth = 0.45f - 0.25f * mud; o.Ao = 0.9f + 0.1f * ridge;
                    return;
                }
                case GBoot:
                {
                    // cuero de bota: arrugas, rozaduras y barro que sube desde la suela (v pequeña)
                    var boot = CharacterVariantData.C(p.boot);
                    float crease = Fbm(u * 2f, v, 6, 4, seed);
                    float mudN = Fbm(u, v, 9, 4, seed + 3);
                    float mud = Smooth(0.45f, 0.05f, v + (mudN - 0.5f) * 0.3f);
                    Rgb c = boot.Scale(0.8f + 0.35f * crease);
                    c = Rgb.Lerp(c, c.Scale(1.5f), edgeWear * 0.5f);
                    c = Rgb.Lerp(c, new Rgb(0.23f, 0.18f, 0.12f), mud * 0.8f);
                    o.Color = c; o.Height = 0.6f * crease; o.Smooth = 0.35f - 0.2f * mud + 0.1f * crease; o.Ao = 0.85f + 0.15f * crease;
                    return;
                }
                case GSole:
                {
                    // goma con tacos (rejilla de bloques) y barro en los huecos
                    float bu = Frac(u * 6f), bv = Frac(v * 9f);
                    float lug = Smooth(0.12f, 0.2f, Math.Min(Math.Min(bu, 1f - bu), Math.Min(bv, 1f - bv)));
                    Rgb c = Rgb.Lerp(new Rgb(0.05f, 0.05f, 0.05f), new Rgb(0.09f, 0.08f, 0.07f), lug);
                    c = Rgb.Lerp(c, new Rgb(0.22f, 0.17f, 0.12f), (1f - lug) * 0.7f);
                    o.Color = c; o.Height = lug; o.Smooth = 0.15f; o.Ao = 0.6f + 0.4f * lug;
                    return;
                }
                case GLaces:
                {
                    // cordones cruzados sobre la lengüeta
                    float a = Frac(u * 4f + v * 4f), b = Frac(u * 4f - v * 4f);
                    float lace = Math.Max(Smooth(0.18f, 0.08f, Math.Abs(a - 0.5f)), Smooth(0.18f, 0.08f, Math.Abs(b - 0.5f)));
                    var boot = CharacterVariantData.C(p.boot);
                    o.Color = Rgb.Lerp(boot.Scale(0.7f), new Rgb(0.14f, 0.13f, 0.12f), lace);
                    o.Height = lace; o.Smooth = 0.2f; o.Ao = 0.7f + 0.3f * lace;
                    return;
                }
                case GMetal:
                {
                    // herrajes: metal pavonado con rayones brillantes y bordes gastados
                    var metal = CharacterVariantData.C(p.metal);
                    float scratch = Scratches(u, v, seed);
                    o.Color = Rgb.Lerp(metal.Scale(0.9f + 0.2f * grime), new Rgb(0.55f, 0.55f, 0.55f), Math.Min(1f, scratch * 0.6f + edgeWear * 0.5f));
                    o.Metal = 0.85f; o.Smooth = 0.5f + 0.2f * scratch; o.Height = -0.3f * scratch; o.Ao = 1f;
                    return;
                }
                case GRadio:
                {
                    // radio: polímero con rejilla del altavoz y teclas
                    float gx = Frac(u * 16f) - 0.5f, gy = Frac(v * 16f) - 0.5f;
                    float hole = v > 0.55f && gx * gx + gy * gy < 0.06f ? 1f : 0f;
                    o.Color = new Rgb(0.08f, 0.08f, 0.08f).Scale(1f + 0.3f * grime - 0.6f * hole);
                    o.Height = -hole + (v < 0.3f && Frac(u * 4f) < 0.7f ? 0.4f : 0f); o.Smooth = 0.4f; o.Ao = 1f - 0.5f * hole;
                    return;
                }
                case GHolster:
                {
                    o.Color = new Rgb(0.07f, 0.07f, 0.07f).Scale(0.9f + 0.3f * grime);
                    o.Smooth = 0.5f; o.Height = 0.1f * grime;
                    float rivet = Rivets(u, v);
                    o.Color = Rgb.Lerp(o.Color, new Rgb(0.4f, 0.4f, 0.42f), rivet); o.Metal = rivet; o.Height += rivet * 0.6f;
                    return;
                }
                case GArmband:
                {
                    // brazalete: tela clara con una cruz propia genérica (aspa redondeada, color de acento)
                    var cloth = new Rgb(0.7f, 0.68f, 0.62f).Scale(0.9f + 0.15f * grime);
                    float cx = Math.Abs(u - 0.5f), cy = Math.Abs(v - 0.5f);
                    bool cross = (cx < 0.08f && cy < 0.3f) || (cy < 0.08f && cx < 0.3f);
                    o.Color = cross ? CharacterVariantData.C(p.accent) : cloth;
                    o.Height = cross ? 0.3f : 0.05f * grime; o.Smooth = 0.2f;
                    return;
                }
                default:
                {
                    // parche de velcro con emblema propio abstracto: tres galones
                    float chev = Frac((v + Math.Abs(u - 0.5f) * 0.8f) * 3f);
                    bool mark = chev < 0.35f && v > 0.15f && v < 0.85f && Math.Abs(u - 0.5f) < 0.38f;
                    o.Color = mark ? CharacterVariantData.C(p.accent).Scale(0.85f) : dark.Scale(0.9f + 0.2f * grime);
                    o.Height = mark ? 0.4f : 0f; o.Smooth = 0.15f;
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ casco
        private static void Helmet(int cell, float u, float v, CharacterVariantData p, ref TexSample o)
        {
            int seed = p.seed * 151 + cell * 17;
            if (cell == HSkull) { Skull(u, v, p, seed, ref o); return; }
            if (cell == HCover)
            {
                // cubierta de tela del casco: manchas suaves, paneles de velcro y desgaste
                var baseC = CharacterVariantData.C(p.helmet);
                float blot = Fbm(u, v, 4, 4, seed);
                float blot2 = Fbm(u + 0.4f, v, 6, 3, seed + 1);
                Rgb c = baseC;
                if (blot > 0.58f) c = Rgb.Lerp(baseC, CharacterVariantData.C(p.camoMid), 0.7f);
                if (blot2 > 0.66f) c = Rgb.Lerp(c, CharacterVariantData.C(p.camoDark), 0.6f);
                float weave = (float)(Math.Sin(u * 400f * Math.PI) * Math.Sin(v * 400f * Math.PI)) * 0.5f + 0.5f;
                float panel = (Math.Abs(u - 0.5f) < 0.12f && v > 0.55f && v < 0.8f) || (Math.Abs(u - 0.15f) < 0.06f && v > 0.4f && v < 0.6f) ? 1f : 0f;
                c = Rgb.Lerp(c, CharacterVariantData.C(p.gearDark), panel * 0.8f);
                float scuff = Smooth(0.7f, 0.9f, Fbm(u, v, 9, 4, seed + 4));
                c = Rgb.Lerp(c, c.Scale(1.4f), scuff * 0.35f);
                o.Color = c.Scale(0.92f + 0.12f * weave); o.Height = 0.1f * weave + 0.4f * panel + 0.3f * blot; o.Smooth = 0.18f; o.Ao = 0.9f + 0.1f * weave;
                return;
            }
            // celdas con 4 subceldas
            int sub = (u < 0.5f ? 0 : 1) + (v < 0.5f ? 0 : 2);
            float su = Frac(u * 2f), sv = Frac(v * 2f);
            float grime = Fbm(su, sv, 5, 4, seed + sub);
            if (cell == HHardware)
            {
                switch (sub)
                {
                    case HwRail:
                    {
                        float slot = Frac(su * 8f) < 0.18f ? 1f : 0f;
                        float scratch = Scratches(su, sv, seed);
                        o.Color = Rgb.Lerp(CharacterVariantData.C(p.metal), new Rgb(0.5f, 0.5f, 0.5f), scratch * 0.6f).Scale(1f - 0.5f * slot);
                        o.Metal = 0.8f; o.Smooth = 0.5f; o.Height = -slot - 0.3f * scratch; o.Ao = 1f - 0.4f * slot;
                        return;
                    }
                    case HwPolymer:
                        o.Color = new Rgb(0.09f, 0.09f, 0.09f).Scale(0.9f + 0.3f * grime); o.Smooth = 0.42f; o.Height = 0.15f * grime; return;
                    case HwRubber:
                        o.Color = new Rgb(0.05f, 0.05f, 0.05f).Scale(0.9f + 0.4f * grime); o.Smooth = 0.25f; o.Height = 0.3f * Fbm(su, sv, 32, 2, seed); return;
                    default:   // lente: vidrio oscuro verdoso, muy liso
                        o.Color = new Rgb(0.03f, 0.06f, 0.06f); o.Smooth = 0.92f; o.Metal = 0.2f; o.Height = 0f; return;
                }
            }
            // prendas de cabeza (variantes)
            var fab = CharacterVariantData.C(p.fabric);
            switch (sub)
            {
                case HwCap:
                {
                    float weave = (float)Math.Abs(Math.Sin((su + sv) * 180f * Math.PI));
                    o.Color = Rgb.Lerp(fab.Scale(0.88f + 0.2f * weave), new Rgb(0.4f, 0.37f, 0.31f), Smooth(0.6f, 0.9f, grime) * 0.4f);
                    o.Height = 0.25f * weave; o.Smooth = 0.15f;
                    return;
                }
                case HwBandana:
                {
                    // pañuelo: estampado propio de puntos y rombos desvaído
                    float dx = Frac(su * 10f) - 0.5f, dy = Frac(sv * 10f) - 0.5f;
                    bool dot = Math.Abs(dx) + Math.Abs(dy) < 0.22f;
                    o.Color = dot ? CharacterVariantData.C(p.accent).Scale(0.7f) : fab.Scale(0.7f + 0.2f * grime);
                    o.Height = 0.1f * grime; o.Smooth = 0.15f;
                    return;
                }
                case HwNet:
                {
                    // red de camuflaje: nudos en rombo sobre tela, con jirones
                    float a = Frac((su + sv) * 14f), b = Frac((su - sv) * 14f);
                    float cord = Math.Max(Smooth(0.15f, 0.05f, Math.Abs(a - 0.5f)), Smooth(0.15f, 0.05f, Math.Abs(b - 0.5f)));
                    Rgb under = Rgb.Lerp(fab, CharacterVariantData.C(p.camoMid), Smooth(0.45f, 0.6f, grime));
                    o.Color = Rgb.Lerp(under.Scale(0.6f), fab.Scale(1.1f), cord);
                    o.Height = cord; o.Smooth = 0.1f; o.Ao = 0.6f + 0.4f * cord;
                    return;
                }
                default:   // cinta elástica de las gafas
                {
                    float rib = (float)Math.Abs(Math.Sin(sv * 40f * Math.PI));
                    o.Color = new Rgb(0.08f, 0.08f, 0.08f).Scale(0.9f + 0.3f * rib); o.Height = 0.3f * rib; o.Smooth = 0.2f;
                    return;
                }
            }
        }

        /// <summary>Placa calavera: pintura blanca gastada sobre metal oscuro, cuencas y surcos sin pintar, desconchones, rayones y suciedad en los huecos.</summary>
        private static void Skull(float u, float v, CharacterVariantData p, int seed, ref TexSample o)
        {
            float s = u * 2f - 1f, t = v;
            float relief = SkullFace.Relief(s, t), detail = SkullFace.Detail(s, t);
            float cover = SkullFace.PaintCover(s, t);
            // desconchones: la pintura salta en relieves altos, bordes y al azar
            float chipN = Fbm(u, v, 14, 4, seed);
            float chip = Smooth(0.62f, 0.7f, chipN + Math.Max(0f, relief) * 40f + Smooth(0.05f, 0f, Math.Min(Math.Min(1f - Math.Abs(s), t), 1f - t)) * 0.3f);
            float paint = cover * (1f - chip);
            float scratch = Scratches(u, v, seed + 3);
            float dirt = Smooth(0f, -0.01f, relief) * 0.7f + Smooth(0.6f, 0.85f, Fbm(u, v, 6, 3, seed + 5)) * 0.4f;
            Rgb metal = Rgb.Lerp(new Rgb(0.07f, 0.07f, 0.075f), new Rgb(0.32f, 0.31f, 0.3f), scratch * 0.7f);
            Rgb white = CharacterVariantData.C(p.paint).Scale(0.92f + 0.12f * Fbm(u, v, 30, 2, seed + 7));
            white = Rgb.Lerp(white, white.Scale(0.55f), scratch * 0.5f);
            Rgb c = Rgb.Lerp(metal, white, paint);
            c = Rgb.Lerp(c, new Rgb(0.16f, 0.13f, 0.1f), Math.Min(1f, dirt) * 0.55f);
            o.Color = c;
            o.Metal = (1f - paint) * 0.75f;
            o.Smooth = paint > 0.5f ? 0.38f - 0.15f * dirt : 0.48f + 0.2f * scratch;
            o.Ao = 1f - 0.45f * Smooth(0f, -0.012f, relief);
            // normales: solo el relieve fino (el grande ya está en la malla), más el escalón de la pintura y los rayones; en unidades de "píxeles" del atlas
            o.Height = detail / 0.00024f / 3f + paint * 0.6f - scratch * 0.5f;
        }

        // ------------------------------------------------------------------ armas
        private static void Weapon(int cell, float u, float v, ref TexSample o)
        {
            int seed = 977 + cell * 19;
            float grime = Fbm(u, v, 5, 4, seed);
            float scratch = Scratches(u, v, seed);
            float edge = Math.Min(Math.Min(u, 1f - u), Math.Min(v, 1f - v));
            float edgeWear = Smooth(0.08f, 0f, edge) * Smooth(0.4f, 0.7f, grime);
            switch (cell)
            {
                case WBlued:
                    o.Color = Rgb.Lerp(new Rgb(0.05f, 0.055f, 0.07f), new Rgb(0.45f, 0.45f, 0.46f), Math.Min(1f, scratch * 0.5f + edgeWear * 0.8f));
                    o.Metal = 0.92f; o.Smooth = 0.58f + 0.15f * scratch; o.Height = -0.3f * scratch; return;
                case WPark:
                    o.Color = Rgb.Lerp(new Rgb(0.1f, 0.1f, 0.1f).Scale(0.9f + 0.2f * Fbm(u, v, 40, 2, seed)), new Rgb(0.42f, 0.42f, 0.42f), edgeWear * 0.7f + scratch * 0.3f);
                    o.Metal = 0.8f; o.Smooth = 0.3f; o.Height = 0.15f * Fbm(u, v, 60, 2, seed + 1); return;
                case WPolyBlack:
                case WPolyTan:
                {
                    float stipple = Fbm(u, v, 80, 2, seed);
                    Rgb baseC = cell == WPolyBlack ? new Rgb(0.07f, 0.07f, 0.07f) : new Rgb(0.4f, 0.35f, 0.27f);
                    o.Color = Rgb.Lerp(baseC.Scale(0.9f + 0.2f * stipple), baseC.Scale(1.5f), edgeWear * 0.5f);
                    o.Smooth = 0.28f; o.Height = 0.45f * stipple; o.Ao = 0.9f + 0.1f * stipple; return;
                }
                case WWood:
                case WWoodLight:
                {
                    // madera con veta: anillos deformados a lo largo de u, nudos y barniz gastado
                    float ring = Frac(v * 18f + Fbm(u, v, 3, 3, seed) * 3f + (float)Math.Sin(u * 6f) * 0.2f);
                    float knot = Smooth(0.07f, 0.02f, Dist(u, v, 0.63f, 0.42f));
                    Rgb a = cell == WWood ? new Rgb(0.22f, 0.12f, 0.06f) : new Rgb(0.38f, 0.24f, 0.13f);
                    Rgb c = Rgb.Lerp(a, a.Scale(0.6f), Smooth(0.6f, 0.9f, ring) + knot);
                    c = Rgb.Lerp(c, c.Scale(1.3f), edgeWear * 0.5f);
                    o.Color = c; o.Smooth = 0.45f - 0.2f * edgeWear; o.Height = 0.3f * Smooth(0.6f, 0.9f, ring); return;
                }
                case WRubber:
                    o.Color = new Rgb(0.05f, 0.05f, 0.05f).Scale(0.9f + 0.3f * grime);
                    o.Smooth = 0.2f; o.Height = 0.6f * (float)Math.Abs(Math.Sin(v * 12f * Math.PI)); return;
                case WTape:
                case WGripWrap:
                {
                    // cinta adhesiva enrollada en espiral (bandas inclinadas solapadas)
                    float band = Frac(u * 5f + v * 1.5f);
                    float lip = Smooth(0.06f, 0f, band);
                    Rgb tape = cell == WTape ? new Rgb(0.16f, 0.16f, 0.15f) : new Rgb(0.2f, 0.12f, 0.08f);
                    o.Color = Rgb.Lerp(tape.Scale(0.9f + 0.25f * Fbm(u, v, 30, 2, seed)), tape.Scale(0.6f), lip);
                    o.Smooth = 0.35f; o.Height = 0.4f * band - 0.6f * lip; return;
                }
                case WSerial:
                {
                    // placa con número de serie genérico: bloques de glifos grabados (ilegibles)
                    float row = Frac(v * 4f), col = Frac(u * 14f);
                    int gx = (int)(u * 14f), gy = (int)(v * 4f);
                    bool glyph = row > 0.3f && row < 0.7f && col > 0.15f && col < 0.85f && TileableNoise.Hash01(gx, gy, seed) > 0.25f &&
                                 TileableNoise.Hash01((int)(col * 4f) + gx * 7, (int)(row * 5f) + gy * 5, seed + 3) > 0.45f;
                    o.Color = glyph ? new Rgb(0.32f, 0.32f, 0.32f) : new Rgb(0.07f, 0.07f, 0.08f);
                    o.Metal = 0.85f; o.Smooth = 0.5f; o.Height = glyph ? -1f : 0f; return;
                }
                case WAnodized:
                case WDot:
                    o.Color = Rgb.Lerp(new Rgb(0.06f, 0.06f, 0.065f), new Rgb(0.4f, 0.4f, 0.42f), Math.Min(1f, edgeWear + scratch * 0.4f));
                    o.Metal = 0.7f; o.Smooth = 0.55f; o.Height = -0.2f * scratch;
                    if (cell == WDot && Math.Abs(u - 0.5f) < 0.03f && v > 0.2f && v < 0.8f) o.Color = new Rgb(0.6f, 0.58f, 0.5f);   // marca de ajuste
                    return;
                case WLens:
                    o.Color = Rgb.Lerp(new Rgb(0.02f, 0.05f, 0.07f), new Rgb(0.1f, 0.2f, 0.22f), Smooth(0.5f, 0f, Dist(u, v, 0.35f, 0.65f)));
                    o.Metal = 0.3f; o.Smooth = 0.95f; return;
                case WRail:
                {
                    float slot = Frac(u * 10f) < 0.4f ? 1f : 0f;
                    o.Color = Rgb.Lerp(new Rgb(0.08f, 0.08f, 0.085f), new Rgb(0.45f, 0.45f, 0.45f), edgeWear + scratch * 0.3f).Scale(1f - 0.4f * slot);
                    o.Metal = 0.75f; o.Smooth = 0.45f; o.Height = slot; o.Ao = 1f - 0.3f * slot; return;
                }
                case WRust:
                {
                    // acero con óxido (barra): manchas anaranjadas apagadas y picaduras
                    float rust = Smooth(0.45f, 0.7f, Fbm(u, v, 6, 5, seed));
                    float pit = Smooth(0.75f, 0.85f, Fbm(u, v, 40, 2, seed + 2));
                    o.Color = Rgb.Lerp(new Rgb(0.3f, 0.3f, 0.31f), new Rgb(0.36f, 0.17f, 0.07f), rust).Scale(1f - 0.3f * pit);
                    o.Metal = 0.85f - 0.6f * rust; o.Smooth = 0.4f - 0.25f * rust; o.Height = 0.3f * rust - 0.5f * pit; return;
                }
                default:   // bocacha quemada
                    o.Color = Rgb.Lerp(new Rgb(0.05f, 0.045f, 0.04f), new Rgb(0.2f, 0.16f, 0.12f), Smooth(0.5f, 0.8f, grime));
                    o.Metal = 0.7f; o.Smooth = 0.3f; o.Height = 0.2f * grime; return;
            }
        }

        // ------------------------------------------------------------------ utilidades
        /// <summary>Rayones finos en direcciones al azar (0..1).</summary>
        public static float Scratches(float u, float v, int seed)
        {
            float s = 0f;
            for (int i = 0; i < 3; i++)
            {
                float ang = TileableNoise.Hash01(i, seed, 5) * 3.1416f;
                float cu = (float)Math.Cos(ang), cv = (float)Math.Sin(ang);
                float along = u * cu + v * cv, across = -u * cv + v * cu;
                float line = Frac(across * (9f + i * 5f) + Fbm(u, v, 4, 2, seed + i) * 0.6f);
                float mask = Smooth(0.6f, 0.8f, Fbm(along, across, 3, 2, seed + 10 + i));
                s = Math.Max(s, Smooth(0.03f, 0.005f, Math.Abs(line - 0.5f)) * mask);
            }
            return s;
        }

        private static float Stitch(float u, float v, float margin)
        {
            float e = Math.Min(Math.Min(u, 1f - u), Math.Min(v, 1f - v));
            if (Math.Abs(e - margin) > 0.01f) return 0f;
            float along = Math.Min(u, 1f - u) < Math.Min(v, 1f - v) ? v : u;
            return Frac(along * 40f) < 0.6f ? 1f : 0f;
        }

        private static float Rivets(float u, float v)
        {
            float best = 9f;
            foreach (var (x, y) in new[] { (0.12f, 0.12f), (0.88f, 0.12f), (0.12f, 0.88f), (0.88f, 0.88f) }) best = Math.Min(best, Dist(u, v, x, y));
            return Smooth(0.05f, 0.03f, best);
        }

        private static float Fbm(float u, float v, int f, int o, int s) => TileableNoise.Fbm(u, v, f, o, s);   // Perlin periódico: continuo para cualquier u, v (sin cortes al escalar)
        private static float Smooth(float e0, float e1, float x) { float k = Math.Max(0f, Math.Min(1f, (x - e0) / (e1 - e0))); return k * k * (3f - 2f * k); }
        private static float Frac(float x) => x - (float)Math.Floor(x);
        private static float Dist(float x, float y, float cx, float cy) => (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        private static byte B(float v) => (byte)(Math.Max(0f, Math.Min(1f, v)) * 255f + 0.5f);
    }
}
