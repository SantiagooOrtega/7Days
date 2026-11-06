using System;
using System.Linq;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public class SignLayoutParams
    {
        public int Seed = 11;
        public float Density = 1f;
        public int MaxSigns = 24;
        public int BaseSigns = 18;
        public float HalfSize = 30f;
        public float StreetHalfWidth = 4.3f;
    }

    /// <summary>
    /// Señales desgastadas a lo largo de la calle, en las esquinas y en los extremos de la calle: tipo, estado, variante de desgaste,
    /// escala y giro. Algunas quedan semitapadas por maleza y escombros (hierba, bolsas o neumáticos alrededor).
    /// Salida en el mismo formato de props (PropLayoutData), así que también se puede definir desde un mapa en JSON.
    /// </summary>
    public static class SignLayout
    {
        private static readonly string[] Types = PropTypes.SignTypes;
        // pesos: pare, ceda, velocidad, prohibido girar, peligro/obras, genérico
        private static readonly float[] TypeWeights = { 0.18f, 0.14f, 0.22f, 0.14f, 0.18f, 0.14f };
        // pesos: erguida, inclinada, doblada, caída, solo poste, colgando
        private static readonly string[] States = { "upright", "tilted", "bent", "fallen", "pole_only", "hanging" };
        private static readonly float[] StateWeights = { 0.30f, 0.22f, 0.14f, 0.12f, 0.10f, 0.12f };

        public static PropLayoutData Generate(SignLayoutParams p, IList<Zone> exclusions)
        {
            exclusions = exclusions ?? new List<Zone>();
            var rng = new Random(p.Seed);
            float F() => (float)rng.NextDouble();
            int target = Math.Min(p.MaxSigns, (int)Math.Round(p.BaseSigns * Math.Max(0f, p.Density)));
            var items = new List<PropEntry>();
            if (target <= 0) return new PropLayoutData { items = items.ToArray() };

            // Candidatos: borde de la calle, esquinas del mapa y extremos de la calle
            var spots = new List<(float x, float z, bool street)>();
            for (float z = -26f; z <= 26f; z += 6.5f)
            {
                float zz = z + (F() - 0.5f) * 3f;
                float side = F() < 0.5f ? -1f : 1f;
                spots.Add((side * (p.StreetHalfWidth + 0.35f + F() * 0.5f), zz, true));
                if (F() < 0.6f) spots.Add((-side * (p.StreetHalfWidth + 0.35f + F() * 0.5f), zz + 2.5f, true));
            }
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    spots.Add((sx * (p.HalfSize - 4.5f), sz * (p.HalfSize - 4.5f), false));          // esquinas
                    spots.Add((sx * (p.StreetHalfWidth + 0.4f), sz * (p.HalfSize - 2.5f), true));     // extremos de la calle
                }
            // barajar de forma determinista
            for (int i = spots.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = spots[i]; spots[i] = spots[j]; spots[j] = t; }

            foreach (var s in spots)
            {
                if (items.Count(IsSign) >= target) break;
                if (Math.Abs(s.x) > p.HalfSize - 0.5f || Math.Abs(s.z) > p.HalfSize - 0.5f) continue;
                if (Blocked(s.x, s.z, exclusions)) continue;

                string type = Pick(Types, TypeWeights, F());
                string state = Pick(States, StateWeights, F());
                float yaw = s.street && Math.Abs(s.x) < 8f ? (s.x > 0 ? 270f : 90f) + (F() - 0.5f) * 36f : F() * 360f; // mira a la calle
                items.Add(new PropEntry { type = type, x = R(s.x), z = R(s.z), yaw = R(yaw), scale = R(0.92f + F() * 0.16f), variant = rng.Next(SignGenerator.Variants), state = state });

                // semitapada por maleza y escombros
                if (F() < 0.35f)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        double a = F() * Math.PI * 2; float r = 0.2f + F() * 0.4f;
                        items.Add(new PropEntry { type = PropTypes.Grass, x = R(s.x + (float)Math.Cos(a) * r), z = R(s.z + (float)Math.Sin(a) * r), yaw = R(F() * 360f), scale = R(1f + F() * 0.6f), variant = rng.Next(2), state = "" });
                    }
                    bool bag = F() < 0.5f;
                    items.Add(new PropEntry { type = bag ? PropTypes.Bag : PropTypes.Tire, x = R(s.x + (F() - 0.5f) * 0.9f), z = R(s.z + (F() - 0.5f) * 0.9f), yaw = R(F() * 360f), scale = 1f, variant = bag ? rng.Next(3) : 0, state = "" });
                }
            }
            return new PropLayoutData { items = items.ToArray() };
        }

        private static bool IsSign(PropEntry e) => PropTypes.IsSign(e.type);
        private static float R(float v) => (float)Math.Round(v, 2);

        private static bool Blocked(float x, float z, IList<Zone> ex)
        {
            foreach (var zone in ex) if (zone.Contains(x, z, 0.5f)) return true;
            return false;
        }

        private static string Pick(string[] values, float[] weights, float roll)
        {
            float sum = 0f; foreach (var w in weights) sum += w;
            float t = roll * sum, acc = 0f;
            for (int i = 0; i < values.Length; i++) { acc += weights[i]; if (t <= acc) return values[i]; }
            return values[values.Length - 1];
        }
    }
}
