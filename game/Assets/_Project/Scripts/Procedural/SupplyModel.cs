using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Qué suministro es (para modelo, icono y color): munición por calibre, vendas, botiquín pequeño, mediano, mochila de médico (grande) o arma.</summary>
    public enum SupplyKind { AmmoPistol, AmmoShotgun, AmmoRifle, Bandage, MedkitSmall, MedkitMedium, MedBag, Weapon }

    /// <summary>Modelo de un suministro: tres partes (cuerpo, detalle y acento luminoso), cada una con su color. El acento es la cruz naranja o la franja del calibre.</summary>
    public sealed class SupplyModelData
    {
        public const int SlotBody = 0, SlotDetail = 1, SlotAccent = 2;
        public readonly MeshData[] Slots = { new MeshData(), new MeshData(), new MeshData() };
        public float[][] Colors = new float[3][];
        public int Triangles { get { int n = 0; foreach (var m in Slots) n += m.Triangles.Count / 3; return n; } }
        public V3 Min, Max;
    }

    /// <summary>
    /// Modelos propios de los suministros (puros, por código, diseños genéricos): caja de cartón de balas de pistola (amarilla, con puntas asomando), caja de cartuchos
    /// de escopeta (roja, con cartuchos de pie), lata metálica de munición de rifle (verde, con asa y cierre), rollo de vendas con el extremo suelto, estuche
    /// pequeño de primeros auxilios con cremallera, maletín mediano con asa y cierres, y mochila de médico con correas y bolsillo (el botiquín grande).
    /// La cruz es naranja y propia (no el emblema de la Cruz Roja). Las medidas son reales (metros) y la base queda en y = 0.
    /// </summary>
    public static class SupplyModel
    {
        public static readonly float[] Cardboard = { 0.78f, 0.6f, 0.22f }, Brass = { 0.86f, 0.66f, 0.28f }, ShellRed = { 0.72f, 0.12f, 0.1f }, CanGreen = { 0.26f, 0.33f, 0.2f };
        public static readonly float[] White = { 0.86f, 0.86f, 0.82f }, Grey = { 0.32f, 0.33f, 0.34f }, Orange = { 1f, 0.45f, 0.06f }, BagGreen = { 0.24f, 0.28f, 0.22f }, Dark = { 0.12f, 0.12f, 0.13f };

        public static SupplyKind KindOf(LootItem item)
        {
            if (item == null) return SupplyKind.AmmoPistol;
            if (item.kind == "weapon") return SupplyKind.Weapon;
            if (item.kind == "ammo") return item.AmmoTypeValue == AmmoType.Shotgun ? SupplyKind.AmmoShotgun : (item.AmmoTypeValue == AmmoType.Rifle ? SupplyKind.AmmoRifle : SupplyKind.AmmoPistol);
            switch (item.MedkitKindValue)
            {
                case MedkitKind.Small: return SupplyKind.MedkitSmall;
                case MedkitKind.Medium: return SupplyKind.MedkitMedium;
                case MedkitKind.Large: return SupplyKind.MedBag;
                default: return SupplyKind.Bandage;
            }
        }

        /// <summary>Color propio del tipo (contorno, icono, partículas): amarillo pistola, rojo escopeta, verde rifle, blanco vendas, naranja médico.</summary>
        public static float[] TypeColor(SupplyKind k)
        {
            switch (k)
            {
                case SupplyKind.AmmoPistol: return new[] { 1f, 0.82f, 0.25f };
                case SupplyKind.AmmoShotgun: return new[] { 1f, 0.32f, 0.2f };
                case SupplyKind.AmmoRifle: return new[] { 0.45f, 0.95f, 0.4f };
                case SupplyKind.Bandage: return new[] { 0.95f, 0.95f, 0.9f };
                case SupplyKind.Weapon: return new[] { 0.6f, 0.8f, 1f };
                default: return new[] { 1f, 0.5f, 0.12f };
            }
        }

        private static AtlasCell Cell => new AtlasCell(0f, 0f, 1f, 0f);

        public static SupplyModelData Build(SupplyKind kind)
        {
            var d = new SupplyModelData();
            var body = new MeshBuilder(); var detail = new MeshBuilder(); var accent = new MeshBuilder();
            switch (kind)
            {
                case SupplyKind.AmmoPistol:
                {
                    // caja de cartón con la tapa entreabierta y cinco puntas de bala asomando
                    body.Box(new V3(0f, 0.045f, 0f), new V3(0.16f, 0.09f, 0.11f), default, 0.004f, Cell, 0.2f);
                    body.Box(new V3(0f, 0.094f, -0.03f), new V3(0.16f, 0.006f, 0.06f), new V3(-18f, 0f, 0f), 0.002f, Cell, 0.2f);
                    for (int i = 0; i < 5; i++)
                    {
                        float x = -0.06f + i * 0.03f;
                        detail.Cylinder(new V3(x, 0.09f, 0.03f), default, 0.0055f, 0.0055f, 0.03f, 8, Cell, 0.1f);
                        detail.Lathe(new V3(x, 0.105f, 0.03f), default, new[] { 0f, 0.0055f, 0.008f, 0.004f, 0.014f, 0.0008f }, 8, 1f, false, true, Cell, 0.1f);
                    }
                    accent.Box(new V3(0f, 0.045f, 0.0555f), new V3(0.164f, 0.03f, 0.002f), default, 0f, Cell, 0.2f);   // franja del calibre
                    d.Colors = new[] { Cardboard, Brass, TypeColor(kind) };
                    break;
                }
                case SupplyKind.AmmoShotgun:
                {
                    // caja roja con cartuchos de pie (vaina roja y culote de latón)
                    body.Box(new V3(0f, 0.05f, 0f), new V3(0.2f, 0.1f, 0.13f), default, 0.004f, Cell, 0.2f);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -0.06f + i * 0.04f;
                        body.Cylinder(new V3(x, 0.115f, 0.02f), default, 0.0105f, 0.0105f, 0.05f, 10, Cell, 0.1f);
                        detail.Cylinder(new V3(x, 0.09f, 0.02f), default, 0.0112f, 0.0112f, 0.012f, 10, Cell, 0.1f);
                    }
                    accent.Box(new V3(0f, 0.05f, 0.0655f), new V3(0.204f, 0.026f, 0.002f), default, 0f, Cell, 0.2f);
                    d.Colors = new[] { ShellRed, Brass, TypeColor(kind) };
                    break;
                }
                case SupplyKind.AmmoRifle:
                {
                    // lata metálica de munición: cuerpo, tapa con reborde, asa plegada y cierre
                    body.Box(new V3(0f, 0.075f, 0f), new V3(0.26f, 0.15f, 0.11f), default, 0.006f, Cell, 0.2f);
                    body.Box(new V3(0f, 0.156f, 0f), new V3(0.27f, 0.016f, 0.12f), default, 0.004f, Cell, 0.2f);
                    detail.Box(new V3(0f, 0.168f, 0f), new V3(0.12f, 0.01f, 0.018f), default, 0.003f, Cell, 0.1f);
                    detail.Box(new V3(0.132f, 0.12f, 0f), new V3(0.012f, 0.05f, 0.04f), default, 0.002f, Cell, 0.1f);
                    accent.Box(new V3(0f, 0.08f, 0.0562f), new V3(0.2f, 0.024f, 0.002f), default, 0f, Cell, 0.2f);
                    d.Colors = new[] { CanGreen, Dark, TypeColor(kind) };
                    break;
                }
                case SupplyKind.Bandage:
                {
                    // rollo de venda tumbado con el extremo suelto y una franja naranja
                    body.Cylinder(new V3(0f, 0.05f, 0f), new V3(0f, 0f, 90f), 0.05f, 0.05f, 0.09f, 16, Cell, 0.2f);
                    detail.Cylinder(new V3(0f, 0.05f, 0f), new V3(0f, 0f, 90f), 0.016f, 0.016f, 0.092f, 10, Cell, 0.1f);
                    body.Box(new V3(0f, 0.004f, 0.09f), new V3(0.088f, 0.004f, 0.09f), new V3(4f, 6f, 0f), 0f, Cell, 0.2f);
                    accent.Cylinder(new V3(0.03f, 0.05f, 0f), new V3(0f, 0f, 90f), 0.0505f, 0.0505f, 0.012f, 16, Cell, 0.2f);
                    d.Colors = new[] { White, Grey, Orange };
                    break;
                }
                case SupplyKind.MedkitSmall:
                {
                    // estuche blando con cremallera y la cruz naranja en la tapa
                    body.Lathe(new V3(0f, 0.035f, 0f), new V3(0f, 0f, 0f), SoldierModel.Ellipsoid(0.09f, 0.035f, 5), 16, 0.72f, true, true, Cell, 0.2f);
                    detail.Lathe(new V3(0f, 0.036f, 0f), default, new[] { -0.004f, 0.0905f, 0.004f, 0.0905f }, 16, 0.72f, false, false, Cell, 0.1f);
                    Cross(accent, new V3(0f, 0.07f, 0f), 0.07f, 0.002f);
                    d.Colors = new[] { White, Dark, Orange };
                    break;
                }
                case SupplyKind.MedkitMedium:
                {
                    // maletín rígido con asa, dos cierres y la cruz
                    body.Box(new V3(0f, 0.06f, 0f), new V3(0.28f, 0.12f, 0.2f), default, 0.012f, Cell, 0.2f);
                    detail.Box(new V3(0f, 0.06f, 0f), new V3(0.284f, 0.008f, 0.204f), default, 0.002f, Cell, 0.1f);
                    detail.Box(new V3(0f, 0.135f, 0f), new V3(0.1f, 0.012f, 0.02f), default, 0.004f, Cell, 0.1f);
                    foreach (float x in new[] { -0.08f, 0.08f }) detail.Box(new V3(x, 0.07f, 0.103f), new V3(0.03f, 0.024f, 0.008f), default, 0.002f, Cell, 0.1f);
                    Cross(accent, new V3(0f, 0.121f, 0f), 0.11f, 0.002f);
                    d.Colors = new[] { White, Grey, Orange };
                    break;
                }
                case SupplyKind.MedBag:
                {
                    // mochila de médico: cuerpo redondeado, bolsillo, asa, dos correas y la cruz grande
                    body.Lathe(new V3(0f, 0.17f, 0f), default, new[] { -0.17f, 0.11f, -0.15f, 0.15f, 0.1f, 0.16f, 0.15f, 0.13f, 0.17f, 0.06f }, 16, 0.6f, true, true, Cell, 0.3f);
                    detail.Box(new V3(0f, 0.09f, 0.095f), new V3(0.2f, 0.12f, 0.03f), default, 0.012f, Cell, 0.2f);
                    detail.Box(new V3(0f, 0.34f, 0f), new V3(0.1f, 0.016f, 0.024f), default, 0.006f, Cell, 0.1f);
                    foreach (float x in new[] { -0.07f, 0.07f }) detail.Box(new V3(x, 0.17f, -0.1f), new V3(0.035f, 0.3f, 0.012f), default, 0.003f, Cell, 0.1f);
                    Cross(accent, new V3(0f, 0.22f, 0.092f), 0.12f, 0.003f, true);
                    d.Colors = new[] { BagGreen, Dark, Orange };
                    break;
                }
                default:
                    return d;   // el arma usa su modelo real (WeaponModel)
            }
            d.Slots[0] = body.Data; d.Slots[1] = detail.Data; d.Slots[2] = accent.Data;
            Bounds(d);
            return d;
        }

        /// <summary>Cruz propia (dos barras) acostada (en la tapa) o de frente (en la mochila).</summary>
        private static void Cross(MeshBuilder mb, V3 at, float size, float thick, bool front = false)
        {
            float w = size * 0.3f;
            if (!front) { mb.Box(at, new V3(size, thick, w), default, 0f, Cell, 0.1f); mb.Box(at, new V3(w, thick, size), default, 0f, Cell, 0.1f); }
            else { mb.Box(at, new V3(size, w, thick), default, 0f, Cell, 0.1f); mb.Box(at, new V3(w, size, thick), default, 0f, Cell, 0.1f); }
        }

        private static void Bounds(SupplyModelData d)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue, z1 = float.MinValue;
            foreach (var m in d.Slots) foreach (var p in m.Positions) { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z); }
            d.Min = new V3(x0, y0, z0); d.Max = new V3(x1, y1, z1);
        }

        /// <summary>Copia de una malla hinchada "d" metros por sus normales con las caras invertidas: el contorno brillante (se ve solo por fuera, detrás del objeto).</summary>
        public static MeshData Outline(MeshData m, float d)
        {
            var o = new MeshData();
            for (int i = 0; i < m.Positions.Count; i++)
            {
                o.Positions.Add(m.Positions[i] + m.Normals[i] * d); o.Normals.Add(-m.Normals[i]); o.Tangents.Add(m.Tangents[i]); o.TangentW.Add(m.WOf(i)); o.Uvs.Add(m.Uvs[i]);
            }
            for (int i = 0; i < m.Triangles.Count; i += 3) { o.Triangles.Add(m.Triangles[i]); o.Triangles.Add(m.Triangles[i + 2]); o.Triangles.Add(m.Triangles[i + 1]); }
            return o;
        }

        // ------------------------------------------------------------------ iconos (64 x 64, RGBA, fondo transparente)
        public const int IconSize = 64;

        /// <summary>Icono del marcador flotante: bala del color del calibre, cruz naranja, rollo de venda; con borde oscuro para leerse sobre cualquier fondo.</summary>
        public static byte[] Icon(SupplyKind kind)
        {
            int n = IconSize; var px = new byte[n * n * 4];
            var c = TypeColor(kind);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float sd = Shape(kind, u, v);   // distancia con signo: < 0 dentro
                    float fill = Clamp01(0.5f - sd * n * 0.5f), edge = Clamp01(0.5f - (sd - 0.09f) * n * 0.5f);
                    // disco de fondo oscuro translúcido
                    float disc = Clamp01(0.5f - ((float)Math.Sqrt(u * u + v * v) - 0.95f) * n * 0.5f);
                    float a = Math.Max(edge, disc * 0.55f);
                    int i = (y * n + x) * 4;
                    float r = Lerp(0.06f, c[0], fill), g = Lerp(0.06f, c[1], fill), b = Lerp(0.07f, c[2], fill);
                    px[i] = B(r); px[i + 1] = B(g); px[i + 2] = B(b); px[i + 3] = B(a);
                }
            return px;
        }

        private static float Shape(SupplyKind kind, float u, float v)
        {
            switch (kind)
            {
                case SupplyKind.AmmoPistol: case SupplyKind.AmmoShotgun: case SupplyKind.AmmoRifle:
                {
                    // tres balas de pie (vaina y punta ojival)
                    float best = 9f;
                    int count = kind == SupplyKind.AmmoRifle ? 3 : 2; float w = kind == SupplyKind.AmmoShotgun ? 0.2f : 0.14f;
                    for (int k = 0; k < count; k++)
                    {
                        float cx = (k - (count - 1) * 0.5f) * (w * 2.4f);
                        float lu = Math.Abs(u - cx);
                        float body = Math.Max(lu - w, Math.Max(-0.62f - v, v - 0.25f));
                        float tipH = kind == SupplyKind.AmmoShotgun ? 0.12f : 0.4f;
                        float tip = Math.Max(Math.Max(lu - w * (1f - Clamp01((v - 0.25f) / tipH)), 0.25f - v), v - 0.25f - tipH);
                        best = Math.Min(best, Math.Min(body, tip));
                    }
                    return best;
                }
                case SupplyKind.Bandage:
                {
                    float r = (float)Math.Sqrt(u * u + v * v);
                    float roll = Math.Max(r - 0.55f, 0.18f - r);
                    float tail = Math.Max(Math.Abs(v + 0.38f) - 0.13f, Math.Max(-u, u - 0.85f));
                    return Math.Min(roll, tail);
                }
                case SupplyKind.Weapon:
                {
                    // pistola: corredera, empuñadura y guardamonte
                    float slide = Math.Max(Math.Abs(u + 0.05f) - 0.7f, Math.Abs(v - 0.22f) - 0.15f);
                    float grip = Math.Max(Math.Abs((u + 0.45f) + (v + 0.2f) * 0.35f) - 0.17f, Math.Abs(v + 0.25f) - 0.33f);
                    return Math.Min(slide, grip);
                }
                default:
                    return Math.Min(Math.Max(Math.Abs(u) - 0.62f, Math.Abs(v) - 0.2f), Math.Max(Math.Abs(u) - 0.2f, Math.Abs(v) - 0.62f));   // cruz
            }
        }

        private static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static byte B(float v) => (byte)(Clamp01(v) * 255f + 0.5f);
    }

    /// <summary>
    /// Reglas de visibilidad de los suministros (puras): marcador flotante a menos de 15 m (oculto si algo tapa la línea de visión y está a más de 6 m), indicador en el
    /// borde de la pantalla y "ping" para los cercanos, texto al mirarlos de cerca, latido del contorno y la columna de luz breve de los drops de zombis.
    /// Solo cambian lo que se VE: no tocan cantidades, probabilidades ni topes (LootDirector y BALANCE.md siguen igual).
    /// </summary>
    public static class SupplyVisibility
    {
        public const float MarkerRange = 15f, MarkerAlwaysRange = 6f, CompassRange = 25f, PingRange = 15f, LookRange = 4.5f, LookAngle = 9f, BeamSeconds = 3.5f;

        public static bool ShowMarker(float distance, bool lineOfSight) => distance <= MarkerRange && (lineOfSight || distance <= MarkerAlwaysRange);

        /// <summary>Tamaño del marcador en metros: crece algo con la distancia para leerse igual de lejos.</summary>
        public static float MarkerSize(float distance) => 0.28f + 0.022f * Math.Max(0f, Math.Min(MarkerRange, distance));

        /// <summary>Latido del contorno 0..1 (cada objeto con su desfase).</summary>
        public static float Pulse(float time, float phase) => 0.5f + 0.5f * (float)Math.Sin(time * 3.2f + phase * 6.283f);

        /// <summary>Intensidad de la columna de luz de un drop: aparece, se mantiene y se apaga en BeamSeconds.</summary>
        public static float Beam(float age) => age < 0f || age > BeamSeconds ? 0f : Math.Min(1f, age / 0.25f) * Math.Min(1f, (BeamSeconds - age) / 1.2f);

        /// <summary>
        /// Punto en el borde de la pantalla (0..1) hacia el que señalar un objeto fuera de la vista. dir = dirección desde el centro de la pantalla (x derecha, y arriba);
        /// margin en fracción de pantalla. Si está detrás, el objeto se señala por abajo.
        /// </summary>
        public static V2 EdgePoint(V2 dir, bool behind, float margin = 0.06f)
        {
            if (behind) dir = new V2(dir.X, -Math.Abs(dir.Y) - 0.3f);
            float len = (float)Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
            if (len < 1e-5f) dir = new V2(0f, -1f); else dir = new V2(dir.X / len, dir.Y / len);
            float half = 0.5f - margin;
            float k = half / Math.Max(Math.Abs(dir.X), Math.Abs(dir.Y));
            return new V2(0.5f + dir.X * k, 0.5f + dir.Y * k);
        }

        /// <summary>true si el punto del visor (x, y en 0..1, z > 0 delante) está dentro de la pantalla.</summary>
        public static bool OnScreen(float x, float y, float z) => z > 0f && x >= 0f && x <= 1f && y >= 0f && y <= 1f;

        /// <summary>Se mira al objeto si está cerca y casi en el centro de la mira.</summary>
        public static bool LookedAt(float distance, float angleDeg) => distance <= LookRange && angleDeg <= LookAngle;
    }
}
