using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>Pieza de un arma: nombre, pieza padre (null = raíz), posición y giro de su pivote, malla y material (0 atlas, 1 acento de rareza, 2 punto rojo).</summary>
    public struct WeaponPart
    {
        public string Name, Parent;
        public V3 Position, Euler;
        public MeshData Mesh;
        public int Slot;
    }

    /// <summary>Modelo de un arma: piezas, punto de mira (SightPoint) y la línea de mira (dos puntos por los que pasa: alza y guion, o el eje de la óptica).</summary>
    public sealed class WeaponModelData
    {
        public readonly List<WeaponPart> Parts = new List<WeaponPart>();
        public V3 Sight, SightLineA, SightLineB;
        public bool HasOptic;
        public int Triangles { get { int n = 0; foreach (var p in Parts) n += p.Mesh.Triangles.Count / 3; return n; } }
    }

    /// <summary>
    /// Armas detalladas por código (puras): cuerpo, cañón, mira (alza y guion, punto rojo o telescópica), cargador, empuñadura, guardamano y culata como piezas
    /// separadas; corredera, bomba, cerrojo, cargador y tambor conservan su nombre, su posición de reposo y su caja de antes, así siguen moviéndose con las
    /// animaciones y el agarre ya calibrado (WeaponGripSolver) sigue valiendo. Material: un atlas compartido (pavonado, fosfatado, polímero, madera, cinta,
    /// goma, serie, anodizado, lentes, rieles, óxido) y un acento por rareza. Presupuesto: 3 000 triángulos por arma.
    /// </summary>
    public static class WeaponModel
    {
        public const int TriangleBudget = 3000;
        public const int SlotAtlas = 0, SlotAccent = 1, SlotDot = 2;

        private static AtlasCell W(int c) => CharacterTextures.Cell(AtlasKind.Weapon, c);

        private sealed class Builder
        {
            public readonly WeaponModelData Data = new WeaponModelData();
            public MeshBuilder Part(string name, V3 pos, V3 euler = default, string parent = null, int slot = SlotAtlas)
            {
                var mb = new MeshBuilder();
                _pending.Add((name, parent, pos, euler, slot, mb));
                return mb;
            }
            private readonly List<(string, string, V3, V3, int, MeshBuilder)> _pending = new List<(string, string, V3, V3, int, MeshBuilder)>();
            public WeaponModelData Done()
            {
                foreach (var (n, p, pos, e, s, mb) in _pending) if (mb.TriangleCount > 0) Data.Parts.Add(new WeaponPart { Name = n, Parent = p, Position = pos, Euler = e, Mesh = mb.Data, Slot = s });
                return Data;
            }
        }

        private static readonly V3 Z0 = new V3(0, 0, 0);
        private static readonly V3 AlongZ = new V3(90f, 0, 0);   // los tornos van por su eje Y: girados 90° quedan a lo largo del cañón (+Z)

        public static WeaponModelData Build(WeaponData d)
        {
            var b = new Builder();
            float len = d.length, th = d.thickness;
            switch (d.kind)
            {
                case WeaponKind.Pistol: Pistol(b, len, th); break;
                case WeaponKind.Revolver: Revolver(b, len, th); break;
                case WeaponKind.Shotgun: Shotgun(b, len, th); break;
                case WeaponKind.SniperRifle: Sniper(b, len, th); break;
                case WeaponKind.Smg: Rifle(b, len, th, true); break;
                case WeaponKind.Melee: Melee(b, d.id, len, th); break;
                default: Rifle(b, len, th, false); break;
            }
            return b.Done();
        }

        // ------------------------------------------------------------------ piezas comunes
        private static void Grip(Builder b, float th, int cell, bool tape)
        {
            // empuñadura (misma caja que antes: el agarre la usa) con cinta enrollada o punteado y un talón
            var g = b.Part("Grip", new V3(0f, -0.09f, 0.04f), new V3(15f, 0, 0));
            g.Box(Z0, new V3(th * 0.85f, 0.12f, 0.055f), Z0, 0.012f, W(tape ? CharacterTextures.WTape : cell), 0.15f);
            g.Box(new V3(0f, -0.058f, -0.004f), new V3(th * 0.9f, 0.012f, 0.062f), Z0, 0.004f, W(CharacterTextures.WPolyBlack), 0.08f);
        }

        private static void TriggerGuard(Builder b, float th, float z)
        {
            var t = b.Part("Guardamonte", new V3(0f, -0.055f, z));
            t.Box(new V3(0f, -0.012f, 0f), new V3(th * 0.32f, 0.007f, 0.055f), Z0, 0.002f, W(CharacterTextures.WPark), 0.08f);
            t.Box(new V3(0f, 0.004f, 0.026f), new V3(th * 0.32f, 0.03f, 0.007f), Z0, 0.002f, W(CharacterTextures.WPark), 0.08f);
            t.Box(new V3(0f, 0.012f, 0.002f), new V3(0.006f, 0.022f, 0.007f), new V3(12f, 0, 0), 0.0015f, W(CharacterTextures.WBlued), 0.04f);   // gatillo
        }

        private static void Serial(Builder b, float th, V3 at)
        {
            var s = b.Part("Serie", at);
            s.Box(Z0, new V3(0.003f, 0.012f, 0.045f), Z0, 0.0008f, W(CharacterTextures.WSerial), 0.06f);
            s.Box(new V3(-th - 0.003f, 0f, 0f), new V3(0.003f, 0.012f, 0.045f), Z0, 0.0008f, W(CharacterTextures.WSerial), 0.06f);
        }

        private static void IronSights(Builder b, float rearZ, float frontZ, float y)
        {
            // alza (dos orejetas con muesca) y guion: la línea de mira pasa por sus puntas
            var r = b.Part("Alza", new V3(0f, y, rearZ));
            r.Box(new V3(-0.007f, 0f, 0f), new V3(0.006f, 0.012f, 0.008f), Z0, 0.0015f, W(CharacterTextures.WBlued), 0.04f);
            r.Box(new V3(0.007f, 0f, 0f), new V3(0.006f, 0.012f, 0.008f), Z0, 0.0015f, W(CharacterTextures.WBlued), 0.04f);
            r.Box(new V3(0f, -0.005f, 0f), new V3(0.022f, 0.004f, 0.009f), Z0, 0.001f, W(CharacterTextures.WBlued), 0.04f);
            var f = b.Part("Guion", new V3(0f, y, frontZ));
            f.Box(Z0, new V3(0.004f, 0.012f, 0.008f), Z0, 0.001f, W(CharacterTextures.WBlued), 0.04f);
            b.Data.SightLineA = new V3(0f, y + 0.006f, rearZ); b.Data.SightLineB = new V3(0f, y + 0.006f, frontZ);
            b.Data.Sight = new V3(0f, y + 0.006f, rearZ - 0.03f);
        }

        private static void RedDot(Builder b, float y, float z)
        {
            // punto rojo cerrado: cuerpo, ventana con lente y el punto (emisivo) en el eje
            var r = b.Part("Mira", new V3(0f, y, z));
            r.Box(new V3(0f, -0.022f, 0f), new V3(0.032f, 0.01f, 0.05f), Z0, 0.003f, W(CharacterTextures.WAnodized), 0.08f);   // base al riel
            r.Box(new V3(-0.017f, 0f, 0f), new V3(0.004f, 0.04f, 0.05f), Z0, 0.0015f, W(CharacterTextures.WDot), 0.08f);
            r.Box(new V3(0.017f, 0f, 0f), new V3(0.004f, 0.04f, 0.05f), Z0, 0.0015f, W(CharacterTextures.WDot), 0.08f);
            r.Box(new V3(0f, 0.022f, 0f), new V3(0.038f, 0.006f, 0.05f), Z0, 0.002f, W(CharacterTextures.WDot), 0.08f);
            r.Box(new V3(0.022f, 0.008f, 0.0f), new V3(0.006f, 0.01f, 0.012f), Z0, 0.002f, W(CharacterTextures.WAnodized), 0.04f);   // botón de brillo
            var lens = b.Part("Lente", new V3(0f, y, z + 0.022f));
            lens.Box(Z0, new V3(0.03f, 0.036f, 0.002f), Z0, 0.0005f, W(CharacterTextures.WLens), 0.04f);
            var dot = b.Part("Punto", new V3(0f, y, z + 0.0225f), default, null, SlotDot);
            dot.Box(Z0, new V3(0.0025f, 0.0025f, 0.002f), Z0, 0.0004f, W(CharacterTextures.WLens), 0.01f);
            b.Data.HasOptic = true;
            b.Data.SightLineA = new V3(0f, y, z - 0.025f); b.Data.SightLineB = new V3(0f, y, z + 0.025f);
            b.Data.Sight = new V3(0f, y, z - 0.05f);
        }

        private static void Rail(Builder b, float y, float z0, float z1, float width)
        {
            var r = b.Part("Riel", new V3(0f, y, (z0 + z1) * 0.5f));
            r.Box(Z0, new V3(width, 0.008f, z1 - z0), Z0, 0.002f, W(CharacterTextures.WRail), 0.25f);
            int teeth = Math.Max(2, (int)((z1 - z0) / 0.01f));
            for (int i = 0; i < teeth; i += 2)
                r.Box(new V3(0f, 0.0055f, -(z1 - z0) * 0.5f + (i + 0.5f) * (z1 - z0) / teeth), new V3(width * 0.9f, 0.003f, (z1 - z0) / teeth * 0.6f), Z0, 0.0006f, W(CharacterTextures.WRail), 0.25f);
        }

        // ------------------------------------------------------------------ pistola
        private static void Pistol(Builder b, float len, float th)
        {
            var body = b.Part("Body", new V3(0f, 0f, len * 0.5f));   // armazón (polímero)
            body.Box(new V3(0f, -0.012f, -len * 0.02f), new V3(th * 0.86f, 0.05f, len * 0.92f), Z0, 0.008f, W(CharacterTextures.WPolyBlack), 0.3f);
            body.Box(new V3(0f, -0.032f, len * 0.32f), new V3(th * 0.6f, 0.012f, len * 0.2f), Z0, 0.003f, W(CharacterTextures.WRail), 0.1f);   // riel bajo el cañón
            var slide = b.Part("Slide", new V3(0f, 0.055f, len * 0.5f));
            slide.Box(Z0, new V3(th * 1.02f, 0.042f, len * 0.98f), Z0, 0.006f, W(CharacterTextures.WBlued), 0.3f);
            for (int i = 0; i < 6; i++)   // estrías traseras para tirar de la corredera
                slide.Box(new V3(th * 0.515f, 0f, -len * 0.42f + i * 0.008f), new V3(0.002f, 0.03f, 0.003f), Z0, 0.0006f, W(CharacterTextures.WPark), 0.03f);
            slide.Box(new V3(th * 0.3f, 0.02f, len * 0.05f), new V3(th * 0.32f, 0.004f, len * 0.22f), Z0, 0.001f, W(CharacterTextures.WBurnt), 0.08f);   // ventana de expulsión
            var barrel = b.Part("Barrel", new V3(0f, 0.052f, len * 0.995f));
            barrel.Cylinder(Z0, AlongZ, 0.011f, 0.011f, 0.012f, 12, W(CharacterTextures.WBurnt), 0.05f);
            Grip(b, th, CharacterTextures.WPolyBlack, false);
            TriggerGuard(b, th, 0.085f);
            var mag = b.Part("Mag", new V3(0f, -0.12f, 0.045f), new V3(15f, 0, 0));
            mag.Box(Z0, new V3(th * 0.7f, 0.08f, 0.04f), Z0, 0.004f, W(CharacterTextures.WBlued), 0.1f);
            var accent = b.Part("Acento", new V3(0f, -0.044f, 0f), default, "Mag", SlotAccent);   // tapa del cargador con el color de la rareza
            accent.Box(Z0, new V3(th * 0.75f, 0.008f, 0.046f), Z0, 0.002f, W(CharacterTextures.WPolyBlack), 0.05f);
            Serial(b, th * 0.43f, new V3(th * 0.43f, -0.02f, len * 0.4f));
            IronSights(b, len * 0.06f, len * 0.94f, 0.082f);
        }

        // ------------------------------------------------------------------ revólver
        private static void Revolver(Builder b, float len, float th)
        {
            var body = b.Part("Body", new V3(0f, 0f, len * 0.5f));
            body.Box(new V3(0f, 0.0f, -len * 0.34f), new V3(th * 0.8f, 0.07f, len * 0.32f), Z0, 0.008f, W(CharacterTextures.WBlued), 0.3f);   // armazón
            body.Box(new V3(0f, 0.04f, -len * 0.18f), new V3(th * 0.5f, 0.012f, len * 0.5f), Z0, 0.004f, W(CharacterTextures.WBlued), 0.3f);   // puente superior
            body.Cylinder(new V3(0f, 0.025f, len * 0.25f), AlongZ, 0.015f, 0.014f, len * 0.5f, 14, W(CharacterTextures.WBlued), 0.3f);     // cañón
            body.Box(new V3(0f, 0.044f, len * 0.25f), new V3(0.008f, 0.01f, len * 0.5f), Z0, 0.002f, W(CharacterTextures.WBlued), 0.3f);     // costilla
            body.Box(new V3(0f, 0.008f, len * 0.12f), new V3(0.016f, 0.012f, len * 0.26f), Z0, 0.003f, W(CharacterTextures.WBlued), 0.2f);   // varilla extractora
            body.Box(new V3(0f, 0.05f, -len * 0.47f), new V3(0.012f, 0.03f, 0.022f), new V3(-25f, 0, 0), 0.003f, W(CharacterTextures.WPark), 0.04f);   // martillo
            var cyl = b.Part("Cylinder", new V3(0f, 0.01f, len * 0.32f));
            cyl.Cylinder(Z0, AlongZ, 0.038f, 0.038f, 0.075f, 12, W(CharacterTextures.WBlued), 0.15f);
            for (int i = 0; i < 6; i++)   // estrías del tambor
            {
                double a = i * Math.PI / 3.0;
                cyl.Box(new V3((float)Math.Sin(a) * 0.036f, (float)Math.Cos(a) * 0.036f, 0f), new V3(0.008f, 0.008f, 0.05f), new V3(0, 0, (float)(-a * 180.0 / Math.PI)), 0.002f, W(CharacterTextures.WPark), 0.05f);
            }
            Grip(b, th, CharacterTextures.WWood, false);
            TriggerGuard(b, th, 0.075f);
            var accent = b.Part("Acento", new V3(0f, -0.15f, 0.03f), new V3(15f, 0, 0), null, SlotAccent);   // medallón de la empuñadura
            accent.Box(Z0, new V3(th * 0.88f, 0.012f, 0.03f), Z0, 0.003f, W(CharacterTextures.WWood), 0.04f);
            IronSights(b, len * 0.04f, len * 0.97f, 0.058f);
        }

        // ------------------------------------------------------------------ escopeta
        private static void Shotgun(Builder b, float len, float th)
        {
            var body = b.Part("Body", new V3(0f, 0f, len * 0.5f));
            body.Box(new V3(0f, 0f, -len * 0.35f), new V3(th * 0.85f, 0.09f, len * 0.3f), Z0, 0.01f, W(CharacterTextures.WPark), 0.4f);   // cajón de mecanismos
            body.Box(new V3(th * 0.43f, 0.012f, -len * 0.33f), new V3(0.003f, 0.03f, 0.07f), Z0, 0.001f, W(CharacterTextures.WBurnt), 0.08f);   // ventana de carga
            var barrel = b.Part("Barrel", new V3(0f, 0.022f, len * 0.6f));
            barrel.Cylinder(Z0, AlongZ, 0.017f, 0.016f, len * 0.8f, 14, W(CharacterTextures.WBlued), 0.5f);
            barrel.Cylinder(new V3(0f, -0.042f, -len * 0.06f), AlongZ, 0.015f, 0.015f, len * 0.62f, 12, W(CharacterTextures.WBlued), 0.5f);   // depósito tubular
            barrel.Box(new V3(0f, -0.025f, len * 0.3f), new V3(0.01f, 0.03f, 0.016f), Z0, 0.002f, W(CharacterTextures.WBlued), 0.05f);           // abrazadera
            var pump = b.Part("Pump", new V3(0f, -0.065f, len * WeaponGrip.PumpAt));
            pump.Box(Z0, new V3(th * 1.15f, 0.055f, len * 0.24f), Z0, 0.012f, W(CharacterTextures.WWoodLight), 0.25f);
            var stock = b.Part("Stock", new V3(0f, -0.02f, -0.1f));
            stock.Box(Z0, new V3(th * 0.9f, 0.11f, 0.22f), new V3(-4f, 0, 0), 0.016f, W(CharacterTextures.WWood), 0.3f);
            stock.Box(new V3(0f, -0.004f, -0.104f), new V3(th * 0.9f, 0.1f, 0.012f), Z0, 0.004f, W(CharacterTextures.WRubber), 0.15f);   // cantonera (dentro del largo: no empuja la culata hacia el pecho)
            var accent = b.Part("Acento", new V3(0f, -0.02f, -0.06f), default, null, SlotAccent);
            accent.Box(Z0, new V3(th * 0.92f, 0.012f, 0.02f), Z0, 0.003f, W(CharacterTextures.WWood), 0.05f);
            Grip(b, th, CharacterTextures.WWood, false);
            TriggerGuard(b, th, 0.075f);
            Serial(b, th * 0.43f, new V3(th * 0.43f, -0.01f, len * 0.08f));
            IronSights(b, len * 0.02f, len * 0.995f, 0.046f);
        }

        // ------------------------------------------------------------------ francotirador
        private static void Sniper(Builder b, float len, float th)
        {
            var body = b.Part("Body", new V3(0f, 0f, len * 0.5f));
            body.Lathe(new V3(0f, 0.008f, -len * 0.38f), AlongZ, new[] { -0.13f, 0.024f, 0.13f, 0.024f }, 12, 1f, true, true, W(CharacterTextures.WBlued), 0.35f);   // cajón redondo
            body.Box(new V3(0f, -0.022f, -len * 0.25f), new V3(th, 0.06f, len * 0.42f), Z0, 0.012f, W(CharacterTextures.WPolyTan), 0.45f);   // guardamano del chasis
            var barrel = b.Part("Barrel", new V3(0f, 0.008f, len * 0.68f));
            barrel.Cylinder(Z0, AlongZ, 0.016f, 0.012f, len * 0.62f, 12, W(CharacterTextures.WBlued), 0.6f);
            barrel.Box(new V3(0f, 0f, len * 0.31f + 0.03f), new V3(0.04f, 0.03f, 0.06f), Z0, 0.006f, W(CharacterTextures.WBurnt), 0.08f);   // freno de boca
            var bolt = b.Part("Bolt", new V3(th * 0.75f, 0.03f, 0.1f));
            bolt.Box(Z0, new V3(0.05f, 0.012f, 0.012f), Z0, 0.003f, W(CharacterTextures.WBlued), 0.05f);
            bolt.Lathe(new V3(0.028f, 0f, 0f), Z0, SoldierModel.Sphere(0.011f, 4), 8, 1f, true, true, W(CharacterTextures.WPolyBlack), 0.03f);   // bola de la palanca
            var mag = b.Part("Mag", new V3(0f, -0.09f, 0.17f));
            mag.Box(Z0, new V3(th * 0.8f, 0.07f, 0.09f), Z0, 0.006f, W(CharacterTextures.WPolyBlack), 0.1f);
            var stock = b.Part("Stock", new V3(0f, -0.02f, -0.12f));
            stock.Box(Z0, new V3(th * 0.9f, 0.11f, 0.24f), Z0, 0.016f, W(CharacterTextures.WPolyTan), 0.3f);
            stock.Box(new V3(0f, 0.062f, 0.02f), new V3(th * 0.7f, 0.016f, 0.12f), Z0, 0.006f, W(CharacterTextures.WPolyTan), 0.15f);   // carrillera
            stock.Box(new V3(0f, -0.004f, -0.113f), new V3(th * 0.88f, 0.1f, 0.014f), Z0, 0.004f, W(CharacterTextures.WRubber), 0.15f);
            var accent = b.Part("Acento", new V3(0f, -0.02f, -0.02f), default, null, SlotAccent);
            accent.Box(Z0, new V3(th * 0.92f, 0.1f, 0.014f), Z0, 0.003f, W(CharacterTextures.WPolyTan), 0.1f);
            Grip(b, th, CharacterTextures.WPolyTan, false);
            TriggerGuard(b, th, 0.075f);
            // visor telescópico: tubo, campana del objetivo, ocular, torretas y anillas
            float sy = 0.075f, z0 = 0.0f, z1 = 0.36f;
            var scope = b.Part("Mira", new V3(0f, sy, (z0 + z1) * 0.5f));
            scope.Cylinder(Z0, AlongZ, 0.016f, 0.016f, (z1 - z0) * 0.6f, 14, W(CharacterTextures.WAnodized), 0.35f);
            scope.Cylinder(new V3(0f, 0f, (z1 - z0) * 0.38f), AlongZ, 0.017f, 0.026f, (z1 - z0) * 0.16f, 14, W(CharacterTextures.WAnodized), 0.15f);
            scope.Cylinder(new V3(0f, 0f, -(z1 - z0) * 0.38f), AlongZ, 0.021f, 0.017f, (z1 - z0) * 0.16f, 14, W(CharacterTextures.WAnodized), 0.15f);
            scope.Cylinder(new V3(0f, 0.022f, 0f), Z0, 0.009f, 0.009f, 0.018f, 10, W(CharacterTextures.WDot), 0.05f);
            scope.Cylinder(new V3(0.022f, 0f, 0f), new V3(0, 0, 90f), 0.009f, 0.009f, 0.018f, 10, W(CharacterTextures.WDot), 0.05f);
            foreach (float rz in new[] { -0.07f, 0.08f })
                scope.Box(new V3(0f, -0.028f, rz), new V3(0.024f, 0.032f, 0.016f), Z0, 0.004f, W(CharacterTextures.WAnodized), 0.06f);
            var lens = b.Part("Lente", new V3(0f, sy, z1 + 0.001f));
            lens.Cylinder(Z0, AlongZ, 0.024f, 0.024f, 0.003f, 14, W(CharacterTextures.WLens), 0.06f);
            var eye = b.Part("Ocular", new V3(0f, sy, z0 - 0.001f));
            eye.Cylinder(Z0, AlongZ, 0.019f, 0.019f, 0.003f, 14, W(CharacterTextures.WLens), 0.06f);
            b.Data.HasOptic = true;
            b.Data.SightLineA = new V3(0f, sy, z0); b.Data.SightLineB = new V3(0f, sy, z1);
            b.Data.Sight = new V3(0f, sy, z0 - 0.04f);
            Rail(b, 0.05f, 0.0f, 0.3f, 0.022f);
        }

        // ------------------------------------------------------------------ fusil de asalto y subfusil
        private static void Rifle(Builder b, float len, float th, bool smg)
        {
            float recv = smg ? 0.62f : 0.36f;   // largo del cajón (fracción)
            var body = b.Part("Body", new V3(0f, 0f, len * 0.5f));
            body.Box(new V3(0f, 0.012f, -len * 0.5f + len * recv * 0.5f), new V3(th * 0.92f, 0.066f, len * recv), Z0, 0.008f, W(CharacterTextures.WPark), 0.4f);    // cajón superior
            body.Box(new V3(0f, -0.032f, -len * 0.5f + len * recv * 0.42f), new V3(th * 0.86f, 0.036f, len * recv * 0.84f), Z0, 0.006f, W(CharacterTextures.WAnodized), 0.4f);   // cajón inferior y brocal
            body.Box(new V3(th * 0.47f, 0.016f, -len * 0.5f + len * recv * 0.55f), new V3(0.003f, 0.022f, 0.06f), Z0, 0.001f, W(CharacterTextures.WBurnt), 0.08f);   // ventana de expulsión
            var hand = b.Part("Guardamano", new V3(0f, 0.004f, len * (recv + (0.9f - recv) * 0.5f)));
            hand.Box(Z0, new V3(th * 0.98f, 0.088f, len * (0.9f - recv)), Z0, 0.012f, W(smg ? CharacterTextures.WPolyBlack : CharacterTextures.WAnodized), 0.4f);
            int slots = smg ? 2 : 4;
            for (int i = 0; i < slots; i++)   // ranuras laterales del guardamano
                foreach (float sx in new[] { -1f, 1f })
                    hand.Box(new V3(sx * th * 0.49f, 0f, -len * (0.9f - recv) * 0.35f + i * len * (0.9f - recv) * 0.7f / Math.Max(1, slots - 1)), new V3(0.003f, 0.03f, 0.03f), Z0, 0.001f, W(CharacterTextures.WBurnt), 0.04f);
            var barrel = b.Part("Barrel", new V3(0f, 0.008f, len * 0.95f));
            barrel.Cylinder(Z0, AlongZ, 0.011f, 0.011f, len * 0.1f, 10, W(CharacterTextures.WBlued), 0.15f);
            barrel.Cylinder(new V3(0f, 0f, len * 0.05f + 0.025f), AlongZ, 0.016f, 0.016f, 0.05f, 10, W(CharacterTextures.WBurnt), 0.08f);   // bocacha
            var ch = b.Part("Slide", new V3(th * 0.6f, 0.03f, len * 0.38f));   // palanca de carga (se mueve al disparar)
            ch.Box(Z0, new V3(0.04f, 0.018f, 0.026f), Z0, 0.004f, W(CharacterTextures.WAnodized), 0.05f);
            var mag = b.Part("Mag", new V3(0f, -0.13f, len * (smg ? 0.42f : WeaponGrip.RifleMagAt)), new V3(-12f, 0, 0));
            mag.Box(Z0, new V3(th * 0.75f, 0.16f, 0.06f), Z0, 0.006f, W(smg ? CharacterTextures.WBlued : CharacterTextures.WPolyBlack), 0.2f);
            for (int i = 0; i < 4; i++) mag.Box(new V3(th * 0.375f, 0.05f - i * 0.03f, 0f), new V3(0.003f, 0.006f, 0.05f), Z0, 0.001f, W(CharacterTextures.WPark), 0.04f);   // nervios
            var accent = b.Part("Acento", new V3(0f, -0.083f, 0f), default, "Mag", SlotAccent);   // tapa del cargador con el color de la rareza
            accent.Box(Z0, new V3(th * 0.8f, 0.01f, 0.066f), Z0, 0.003f, W(CharacterTextures.WPolyBlack), 0.05f);
            Grip(b, th, CharacterTextures.WPolyBlack, true);
            TriggerGuard(b, th, 0.08f);
            if (smg)
            {
                // culata plegable de alambre (corta: la culata apoya a 6 cm)
                var stock = b.Part("Stock", new V3(0f, -0.01f, -0.035f));
                foreach (float sx in new[] { -1f, 1f }) stock.Box(new V3(sx * 0.018f, 0f, 0.005f), new V3(0.006f, 0.008f, 0.06f), Z0, 0.002f, W(CharacterTextures.WBlued), 0.06f);
                stock.Box(new V3(0f, -0.005f, -0.024f), new V3(0.05f, 0.075f, 0.012f), Z0, 0.004f, W(CharacterTextures.WRubber), 0.08f);
            }
            else
            {
                var stock = b.Part("Stock", new V3(0f, -0.02f, -0.11f));
                stock.Box(Z0, new V3(th * 0.85f, 0.1f, 0.2f), Z0, 0.014f, W(CharacterTextures.WPolyBlack), 0.25f);
                stock.Cylinder(new V3(0f, 0.025f, 0.11f), AlongZ, 0.016f, 0.016f, 0.05f, 10, W(CharacterTextures.WAnodized), 0.06f);   // tubo
                stock.Box(new V3(0f, -0.003f, -0.094f), new V3(th * 0.83f, 0.092f, 0.012f), Z0, 0.004f, W(CharacterTextures.WRubber), 0.1f);
            }
            Serial(b, th * 0.43f, new V3(th * 0.43f, -0.032f, -len * 0.5f + len * recv * 0.3f + len * 0.5f));
            Rail(b, 0.049f, len * 0.02f, len * (smg ? 0.55f : 0.34f), 0.022f);
            RedDot(b, 0.083f, len * (smg ? 0.25f : 0.18f));
        }

        // ------------------------------------------------------------------ cuerpo a cuerpo
        private static void Melee(Builder b, string id, float len, float th)
        {
            // el mango es "Body" (donde agarran las manos); la cabeza (bate, hoja, pala, uña) va en su propia pieza
            var body = b.Part("Body", new V3(0f, 0f, len * 0.5f));
            switch (id)
            {
                case "bat":
                {
                    body.Lathe(Z0, AlongZ, new[] { -len * 0.5f, th * 0.45f, -len * 0.05f, th * 0.55f }, 14, 1f, true, true, W(CharacterTextures.WWoodLight), 0.6f);
                    var head = b.Part("Cabeza", new V3(0f, 0f, len * 0.725f));
                    head.Lathe(Z0, AlongZ, new[] { -len * 0.275f, th * 0.55f, len * 0.1f, th * 1.15f, len * 0.275f, th * 1.1f }, 14, 1f, true, true, W(CharacterTextures.WWoodLight), 0.6f);
                    break;
                }
                case "axe":
                {
                    body.Box(Z0, new V3(th, th * 1.2f, len), Z0, 0.01f, W(CharacterTextures.WWood), 0.6f);
                    var head = b.Part("Hoja", new V3(0f, th * 1.6f, len * 0.93f));
                    head.Box(Z0, new V3(0.012f, th * 3.5f, 0.11f), Z0, 0.003f, W(CharacterTextures.WRust), 0.2f);
                    break;
                }
                case "shovel":
                {
                    body.Box(Z0, new V3(th, th, len), Z0, 0.01f, W(CharacterTextures.WWood), 0.6f);
                    var head = b.Part("Pala", new V3(0f, 0f, len + 0.1f));
                    head.Box(Z0, new V3(0.17f, 0.012f, 0.22f), new V3(-6f, 0, 0), 0.004f, W(CharacterTextures.WRust), 0.3f);
                    break;
                }
                default:   // barra: hexagonal oxidada con el extremo curvo de uña
                {
                    body.Lathe(Z0, AlongZ, new[] { -len * 0.5f, th * 0.5f, len * 0.44f, th * 0.5f }, 6, 1f, true, true, W(CharacterTextures.WRust), 0.6f);
                    var claw = b.Part("Uña", new V3(0f, 0f, len));
                    claw.Box(new V3(0f, th * 0.9f, 0f), new V3(th * 0.9f, th * 2.2f, th * 0.9f), new V3(-35f, 0, 0), 0.006f, W(CharacterTextures.WRust), 0.15f);
                    claw.Box(new V3(0f, th * 1.9f, th * 0.6f), new V3(th * 1.2f, th * 0.4f, th * 1.4f), new V3(-70f, 0, 0), 0.004f, W(CharacterTextures.WRust), 0.1f);
                    break;
                }
            }
            var grip = b.Part("Grip", new V3(0f, 0f, 0.08f));
            grip.Lathe(Z0, AlongZ, new[] { -0.08f, th * 0.75f, 0.08f, th * 0.72f }, 10, 1f, true, true, W(CharacterTextures.WGripWrap), 0.2f);
            var accent = b.Part("Acento", new V3(0f, 0f, 0.17f), default, null, SlotAccent);   // cinta de color de la rareza
            accent.Lathe(Z0, AlongZ, new[] { -0.012f, th * 0.62f, 0.012f, th * 0.62f }, 10, 1f, true, true, W(CharacterTextures.WTape), 0.05f);
        }
    }
}
