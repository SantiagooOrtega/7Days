using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public enum ColliderShape { None, Box, Capsule, Sphere }

    /// <summary>Qué tipo de objeto del mapa es sólido y con qué collider SIMPLE se representa (nunca MeshCollider).</summary>
    public class SolidSpec
    {
        public string Name;
        public bool Solid;
        public ColliderShape Shape;
        public string Reason;
        /// <summary>true = solo coincide con el nombre exacto (evita que "Ground" atrape a "GroundDetails" o "GroundFog").</summary>
        public bool Exact;
    }

    /// <summary>
    /// Fuente única de verdad de qué colisiona en el mapa. Los constructores de Unity la consultan para decidir los colliders, la auditoría de
    /// ColliderAudit la usa al jugar para detectar piezas sólidas sin collider, y una prueba EditMode recorre toda la lista y falla si un sólido
    /// no tiene collider simple o si algo no sólido lo tiene.
    /// </summary>
    public static class SolidCatalog
    {
        /// <summary>A partir de esta escala un matorral (variante 0) es un obstáculo (collider de esfera); más pequeño, no.</summary>
        public const float LargeBushScale = 1.1f;

        /// <summary>Forma del collider de una pieza de la distribución de props (None = sin collider: se atraviesa).</summary>
        public static ColliderShape ShapeFor(string propType, int variant, float scale)
        {
            switch (propType)
            {
                case PropTypes.TreeDead: return ColliderShape.Capsule;      // collider de tronco
                case PropTypes.CarWreck: return ColliderShape.Box;
                case PropTypes.PoleFallen: return ColliderShape.Capsule;
                case PropTypes.Bush: return variant == 0 && scale >= LargeBushScale ? ColliderShape.Sphere : ColliderShape.None;
                default: return ColliderShape.None; // hierba, neumáticos, bolsas, fogatas, señales (los postes finos no bloquean)
            }
        }

        public static bool IsSolid(string propType, int variant, float scale) => ShapeFor(propType, variant, scale) != ColliderShape.None;

        /// <summary>Piezas fijas del mapa por prefijo de nombre (así las identifica la auditoría en la escena).</summary>
        public static readonly IReadOnlyList<SolidSpec> MapPieces = new List<SolidSpec>
        {
            new SolidSpec { Name = "Ground", Exact = true, Solid = true, Shape = ColliderShape.Box, Reason = "el piso" },
            new SolidSpec { Name = "Wall", Solid = true, Shape = ColliderShape.Box, Reason = "muros del perímetro y de la casa" },
            new SolidSpec { Name = "Crumble", Solid = true, Shape = ColliderShape.Box, Reason = "bloques sobre los muros" },
            new SolidSpec { Name = "Barrier", Solid = true, Shape = ColliderShape.Box, Reason = "barreras de concreto" },
            new SolidSpec { Name = "Crate", Solid = true, Shape = ColliderShape.Box, Reason = "cajas" },
            new SolidSpec { Name = "Container", Solid = true, Shape = ColliderShape.Box, Reason = "contenedores" },
            new SolidSpec { Name = "Pillar", Solid = true, Shape = ColliderShape.Box, Reason = "pilares de la nave" },
            new SolidSpec { Name = "Back Wall", Solid = true, Shape = ColliderShape.Box, Reason = "muros de la nave" },
            new SolidSpec { Name = "Beam", Solid = true, Shape = ColliderShape.Box, Reason = "vigas" },
            new SolidSpec { Name = "Roof", Solid = true, Shape = ColliderShape.Box, Reason = "techos" },
            new SolidSpec { Name = "Floor", Solid = true, Shape = ColliderShape.Box, Reason = "piso de la casa" },
            new SolidSpec { Name = "Frame", Solid = true, Shape = ColliderShape.Box, Reason = "marcos de puertas y ventanas" },
            new SolidSpec { Name = "Furniture", Solid = true, Shape = ColliderShape.Box, Reason = "muebles" },
            new SolidSpec { Name = "Barricade", Solid = true, Shape = ColliderShape.Box, Reason = "tablones del director" },
            new SolidSpec { Name = "Barrel", Solid = true, Shape = ColliderShape.Capsule, Reason = "barriles" },
            new SolidSpec { Name = "Fire Barrel", Solid = true, Shape = ColliderShape.Capsule, Reason = "barriles con fuego" },
            new SolidSpec { Name = "Lamp Pole", Solid = true, Shape = ColliderShape.Capsule, Reason = "postes de farola" },
            new SolidSpec { Name = "Pipe", Solid = true, Shape = ColliderShape.Capsule, Reason = "tuberías" },
            new SolidSpec { Name = "Car Wreck", Solid = true, Shape = ColliderShape.Box, Reason = "coches" },
            new SolidSpec { Name = "Fallen Pole", Solid = true, Shape = ColliderShape.Capsule, Reason = "poste caído" },
            new SolidSpec { Name = "Dummy", Solid = true, Shape = ColliderShape.Capsule, Reason = "blancos de práctica" },
            // --- no sólidos
            new SolidSpec { Name = "Road", Exact = true, Solid = false, Shape = ColliderShape.None, Reason = "asfalto fino sobre el piso (el piso ya colisiona)" },
            new SolidSpec { Name = "Rubble", Solid = false, Shape = ColliderShape.None, Reason = "escombros pequeños: no deben frenar al jugador" },
            new SolidSpec { Name = "Stone", Solid = false, Shape = ColliderShape.None, Reason = "piedras de la fogata" },
            new SolidSpec { Name = "Log", Solid = false, Shape = ColliderShape.None, Reason = "troncos de la fogata" },
            new SolidSpec { Name = "Ash", Solid = false, Shape = ColliderShape.None, Reason = "ceniza" },
            new SolidSpec { Name = "Torch", Solid = false, Shape = ColliderShape.None, Reason = "antorcha pegada a la pared" },
            new SolidSpec { Name = "Building", Solid = false, Shape = ColliderShape.None, Reason = "skyline: fuera del mapa" },
            new SolidSpec { Name = "Ruin Top", Solid = false, Shape = ColliderShape.None, Reason = "skyline: fuera del mapa" },
            new SolidSpec { Name = "Walkable", Solid = false, Shape = ColliderShape.None, Reason = "volumen de NavMesh" },
        };

        /// <summary>Busca la especificación del prefijo más largo que encaje con el nombre ("Fire Barrel 1" -> "Fire Barrel", no "Barrel").</summary>
        public static SolidSpec MatchPiece(string objectName)
        {
            SolidSpec best = null;
            foreach (var s in MapPieces)
            {
                bool match = s.Exact ? objectName == s.Name : objectName.StartsWith(s.Name, StringComparison.Ordinal);
                if (match && (best == null || s.Name.Length > best.Name.Length)) best = s;
            }
            return best;
        }

        /// <summary>Tipos de caja de la casa: todos llevan collider de caja (los marcos no reducen el paso libre).</summary>
        public static ColliderShape ShapeForHouse(HouseBoxKind kind) => ColliderShape.Box;
    }
}
