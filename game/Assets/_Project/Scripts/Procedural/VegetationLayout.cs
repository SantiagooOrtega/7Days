using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Zona donde no se debe colocar nada grande (círculo o rectángulo).</summary>
    public readonly struct Zone
    {
        public readonly bool IsRect;
        public readonly float A, B, C, D; // círculo: x, z, radio | rect: x0, z0, x1, z1

        private Zone(bool rect, float a, float b, float c, float d) { IsRect = rect; A = a; B = b; C = c; D = d; }
        public static Zone Circle(float x, float z, float radius) => new Zone(false, x, z, radius, 0f);
        public static Zone Rect(float x0, float z0, float x1, float z1) => new Zone(true, Math.Min(x0, x1), Math.Min(z0, z1), Math.Max(x0, x1), Math.Max(z0, z1));

        public bool Contains(float x, float z, float margin)
        {
            if (IsRect) return x >= A - margin && x <= C + margin && z >= B - margin && z <= D + margin;
            float dx = x - A, dz = z - B, r = C + margin;
            return dx * dx + dz * dz <= r * r;
        }
    }

    public class VegetationParams
    {
        public int Seed = 3;
        /// <summary>Multiplicador de cantidad (0 = nada; 1 = normal; los topes Max* siempre se respetan).</summary>
        public float Density = 1f;
        public float HalfSize = 30f;            // mitad del mapa jugable (los muros están en ~30.5)
        public float StreetHalfWidth = 4.3f;    // la calle central queda libre de piezas grandes
        public float RingInner = 32f, RingOuter = 41f; // anillo de árboles que tapa el borde del mapa

        public int MaxTrees = 90, MaxBushes = 100, MaxGrass = 420, MaxProps = 48;
        public int BaseTrees = 64, BaseBushes = 54, BaseGrass = 280;
        /// <summary>Rectángulo de la casa (x0, z0, x1, z1) alrededor del cual se agrupan arbustos y hierba extra. Null = sin casa.</summary>
        public float[] HouseRect;
        public int HouseBushes = 12, HouseGrass = 50;
        public int BaseCars = 4, BasePoles = 3, BaseTires = 12, BaseBags = 14, BaseSigns = 3;
        /// <summary>Fogatas entre escombros y coches incendiados (de los coches que haya). Tope duro MaxFires.</summary>
        public int BaseCampfires = 5, BaseBurningCars = 2, MaxFires = 12;
    }

    /// <summary>
    /// Distribución por defecto de vegetación y relleno, determinista por semilla: anillo denso de árboles muertos que tapa
    /// el borde, arbustos y hierba en la base de los muros y los bordes de la calle, y props de abandono (coches, postes caídos,
    /// neumáticos, bolsas, carteles rotos). Respeta zonas excluidas y la calle libre. El resultado es el mismo formato que un
    /// JSON de la BD (PropLayoutData).
    /// </summary>
    public static class VegetationLayout
    {
        public static PropLayoutData Generate(VegetationParams p, IList<Zone> exclusions)
        {
            var items = new List<PropEntry>();
            var rng = new Random(p.Seed);
            float F() => (float)rng.NextDouble();
            exclusions = exclusions ?? new List<Zone>();
            float d = Math.Max(0f, p.Density);

            int trees = Math.Min(p.MaxTrees, (int)Math.Round(p.BaseTrees * d));
            int bushes = Math.Min(p.MaxBushes, (int)Math.Round(p.BaseBushes * d));
            int grass = Math.Min(p.MaxGrass, (int)Math.Round(p.BaseGrass * d));
            int cars = (int)Math.Round(p.BaseCars * d), poles = (int)Math.Round(p.BasePoles * d), tires = (int)Math.Round(p.BaseTires * d);
            int bags = (int)Math.Round(p.BaseBags * d), signs = (int)Math.Round(p.BaseSigns * d);
            // tope total de props de abandono: se recortan por orden de importancia visual
            int propBudget = p.MaxProps;
            cars = Math.Min(cars, propBudget); propBudget -= cars;
            poles = Math.Min(poles, propBudget); propBudget -= poles;
            signs = Math.Min(signs, propBudget); propBudget -= signs;
            tires = Math.Min(tires, propBudget); propBudget -= tires;
            bags = Math.Min(bags, propBudget);

            var trunks = new List<(float x, float z)>();

            // ---- Árboles: 85% en el anillo exterior, 15% en las esquinas del mapa
            for (int i = 0; i < trees; i++)
            {
                bool ring = i < Math.Round(trees * 0.85);
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    float x, z;
                    if (ring)
                    {
                        float along = (F() * 2f - 1f) * p.RingOuter, depth = p.RingInner + F() * (p.RingOuter - p.RingInner);
                        int side = rng.Next(4);
                        x = side < 2 ? along : (side == 2 ? depth : -depth);
                        z = side < 2 ? (side == 0 ? depth : -depth) : along;
                    }
                    else
                    {
                        x = (F() < 0.5f ? -1f : 1f) * (10f + F() * 16f);
                        z = (F() * 2f - 1f) * (p.HalfSize - 3f);
                    }
                    if (!Free(x, z, 1.2f, exclusions) || TooClose(trunks, x, z, ring ? 2.2f : 4f)) continue;
                    trunks.Add((x, z));
                    items.Add(Make(PropTypes.TreeDead, x, z, F() * 360f, 0.8f + F() * 0.6f, rng.Next(FoliageGeometry.TreeVariants)));
                    break;
                }
            }

            // ---- Arbustos: base de los muros (dentro) y anillo (fuera)
            for (int i = 0; i < bushes; i++)
            {
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    bool inside = F() < 0.6f;
                    Edge(F, rng, inside ? p.HalfSize - 3.5f : p.RingInner, inside ? p.HalfSize - 0.6f : p.RingInner + 6f, out float x, out float z);
                    if (!Free(x, z, 0.8f, exclusions) || InStreet(p, x, z)) continue;
                    items.Add(Make(PropTypes.Bush, x, z, F() * 360f, 0.7f + F() * 0.8f, rng.Next(FoliageGeometry.BushVariants)));
                    break;
                }
            }

            // ---- Hierba: bordes de la calle, base de los muros y alrededor de las coberturas
            var circles = new List<Zone>();
            foreach (var z in exclusions) if (!z.IsRect) circles.Add(z);
            for (int i = 0; i < grass; i++)
            {
                float roll = F(), x = 0f, z = 0f;
                if (roll < 0.4f)
                {
                    x = (F() < 0.5f ? -1f : 1f) * (p.StreetHalfWidth + 0.1f + F() * 1.2f);
                    z = (F() * 2f - 1f) * (p.HalfSize - 2f);
                }
                else if (roll < 0.65f || circles.Count == 0)
                {
                    Edge(F, rng, p.HalfSize - 3.2f, p.HalfSize - 0.5f, out x, out z);
                }
                else
                {
                    var c = circles[rng.Next(circles.Count)];
                    double a = F() * Math.PI * 2, r = c.C + 0.25f + F() * 0.9f;
                    x = c.A + (float)Math.Cos(a) * (float)r;
                    z = c.B + (float)Math.Sin(a) * (float)r;
                }
                if (Math.Abs(x) > p.HalfSize || Math.Abs(z) > p.HalfSize) continue;
                if (InSolid(x, z, exclusions)) continue;
                items.Add(Make(PropTypes.Grass, x, z, F() * 360f, 0.8f + F() * 0.7f, rng.Next(2)));
            }

            // ---- Alrededor de la casa: arbustos y hierba seca para que no se vea desnuda
            if (p.HouseRect != null && p.HouseRect.Length == 4)
            {
                float hx0 = Math.Min(p.HouseRect[0], p.HouseRect[2]), hz0 = Math.Min(p.HouseRect[1], p.HouseRect[3]);
                float hx1 = Math.Max(p.HouseRect[0], p.HouseRect[2]), hz1 = Math.Max(p.HouseRect[1], p.HouseRect[3]);
                int hb = Math.Max(0, (int)Math.Round(p.HouseBushes * d)), hg = Math.Max(0, (int)Math.Round(p.HouseGrass * d));
                int placedB = 0, placedG = 0, bushCap = p.MaxBushes - Count(items, PropTypes.Bush), grassCap = p.MaxGrass - Count(items, PropTypes.Grass);
                for (int attempt = 0; attempt < (hb + hg) * 8 && (placedB < hb || placedG < hg); attempt++)
                {
                    // anillo a 1.6-4.5 m del rectángulo de la casa (el borde interior queda libre para las entradas)
                    float ring = 1.6f + F() * 2.9f;
                    float t = F() * 2f * ((hx1 - hx0) + (hz1 - hz0)), x, z;
                    float w = hx1 - hx0, hh = hz1 - hz0;
                    if (t < w) { x = hx0 + t; z = hz0 - ring; }
                    else if (t < w + hh) { x = hx1 + ring; z = hz0 + (t - w); }
                    else if (t < 2 * w + hh) { x = hx0 + (t - w - hh); z = hz1 + ring; }
                    else { x = hx0 - ring; z = hz0 + (t - 2 * w - hh); }
                    if (Math.Abs(x) > p.HalfSize - 0.5f || Math.Abs(z) > p.HalfSize - 0.5f || InStreet(p, x, z)) continue;
                    if (InSolid(x, z, exclusions)) continue;
                    bool bush = placedB < hb && (placedG >= hg || F() < 0.4f);
                    if (bush)
                    {
                        if (placedB >= bushCap || !Free(x, z, 0.5f, exclusions)) continue;
                        items.Add(Make(PropTypes.Bush, x, z, F() * 360f, 0.8f + F() * 0.8f, rng.Next(FoliageGeometry.BushVariants)));
                        placedB++;
                    }
                    else if (placedG < hg && placedG < grassCap)
                    {
                        items.Add(Make(PropTypes.Grass, x, z, F() * 360f, 0.9f + F() * 0.7f, rng.Next(2)));
                        placedG++;
                    }
                }
            }

            // ---- Coches destrozados a los lados de la calle
            var carSpots = new List<(float x, float z)>();
            for (int i = 0; i < cars; i++)
            {
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    float x = (F() < 0.5f ? -1f : 1f) * (p.StreetHalfWidth + 2.6f + F() * 2.2f);
                    float z = (F() * 2f - 1f) * (p.HalfSize - 6f);
                    if (!Free(x, z, 2.6f, exclusions) || TooClose(carSpots, x, z, 7f)) continue;
                    carSpots.Add((x, z));
                    float yaw = (F() < 0.5f ? 0f : 180f) + (F() - 0.5f) * 50f; // más o menos paralelos a la calle
                    items.Add(Make(PropTypes.CarWreck, x, z, yaw, 1f, rng.Next(3)));
                    break;
                }
            }

            // ---- Coches incendiados: los primeros coches arden
            int burning = Math.Min((int)Math.Round(p.BaseBurningCars * d), p.MaxFires);
            foreach (var e in items) { if (burning <= 0) break; if (e.type == PropTypes.CarWreck) { e.state = "burning"; burning--; } }

            // ---- Fogatas entre los escombros: junto a coberturas y contenedores (borde de las zonas ocupadas), lejos de la calle y entre sí
            var camps = new List<(float x, float z)>();
            int campTarget = Math.Max(0, Math.Min((int)Math.Round(p.BaseCampfires * d), p.MaxFires));
            for (int i = 0; i < campTarget && circles.Count > 0; i++)
            {
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    var c = circles[rng.Next(circles.Count)];
                    double ang = F() * Math.PI * 2; float r = c.C + 1.3f + F() * 1.0f;
                    float x = c.A + (float)Math.Cos(ang) * r, z = c.B + (float)Math.Sin(ang) * r;
                    if (Math.Abs(x) > p.HalfSize - 2f || Math.Abs(z) > p.HalfSize - 2f || InStreet(p, x, z)) continue;
                    if (!Free(x, z, 0.6f, exclusions) || TooClose(camps, x, z, 7f)) continue;
                    camps.Add((x, z));
                    items.Add(Make(PropTypes.Campfire, x, z, F() * 360f, 1f, 0));
                    break;
                }
            }

            // ---- Postes caídos, carteles rotos, neumáticos y bolsas
            for (int i = 0; i < poles; i++) TryPlace(items, rng, F, p, exclusions, PropTypes.PoleFallen, 2.4f, false, 1f, 1f, 1);
            for (int i = 0; i < signs; i++)
            {
                float x = (F() < 0.5f ? -1f : 1f) * (p.StreetHalfWidth + 0.5f + F() * 0.6f), z = (F() * 2f - 1f) * (p.HalfSize - 4f);
                if (Free(x, z, 0.8f, exclusions)) items.Add(Make(PropTypes.SignBroken, x, z, F() * 360f, 1f, 0));
            }
            for (int i = 0; i < tires; i++) TryPlace(items, rng, F, p, exclusions, PropTypes.Tire, 0.6f, true, 0.9f, 1.15f, 1);
            for (int i = 0; i < bags; i++) TryPlace(items, rng, F, p, exclusions, PropTypes.Bag, 0.4f, true, 0.8f, 1.3f, 3);

            return new PropLayoutData { items = items.ToArray() };
        }

        private static void TryPlace(List<PropEntry> items, Random rng, Func<float> F, VegetationParams p, IList<Zone> ex, string type,
            float clearance, bool allowStreet, float sMin, float sMax, int variants)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                float x = (F() * 2f - 1f) * (p.HalfSize - 2f), z = (F() * 2f - 1f) * (p.HalfSize - 2f);
                if (!Free(x, z, clearance, ex)) continue;
                if (!allowStreet && InStreet(p, x, z)) continue;
                items.Add(Make(type, x, z, F() * 360f, sMin + F() * (sMax - sMin), rng.Next(variants)));
                return;
            }
        }

        private static int Count(List<PropEntry> items, string type) { int n = 0; foreach (var e in items) if (e.type == type) n++; return n; }

        private static PropEntry Make(string type, float x, float z, float yaw, float scale, int variant)
            => new PropEntry { type = type, x = (float)Math.Round(x, 2), z = (float)Math.Round(z, 2), yaw = (float)Math.Round(yaw, 1), scale = (float)Math.Round(scale, 2), variant = variant, state = "" };

        private static bool InStreet(VegetationParams p, float x, float z) => Math.Abs(x) < p.StreetHalfWidth + 0.8f && Math.Abs(z) <= p.HalfSize;

        private static bool Free(float x, float z, float margin, IList<Zone> zones)
        {
            foreach (var zone in zones) if (zone.Contains(x, z, margin)) return false;
            return true;
        }

        private static bool InSolid(float x, float z, IList<Zone> zones)
        {
            foreach (var zone in zones) if (!zone.IsRect ? zone.Contains(x, z, 0f) : zone.Contains(x, z, 0f)) return true;
            return false;
        }

        private static bool TooClose(List<(float x, float z)> list, float x, float z, float min)
        {
            foreach (var (px, pz) in list) { float dx = px - x, dz = pz - z; if (dx * dx + dz * dz < min * min) return true; }
            return false;
        }

        /// <summary>Punto en la franja entre dos "radios cuadrados" (distancia de Chebyshev) del centro: borde del mapa.</summary>
        private static void Edge(Func<float> F, Random rng, float inner, float outer, out float x, out float z)
        {
            float along = (F() * 2f - 1f) * outer, depth = inner + F() * (outer - inner);
            int side = rng.Next(4);
            x = side < 2 ? along : (side == 2 ? depth : -depth);
            z = side < 2 ? (side == 0 ? depth : -depth) : along;
        }
    }
}
