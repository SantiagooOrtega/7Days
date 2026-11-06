using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Una pieza colocada en el mapa (árbol, arbusto, coche...). Es el formato JSON que podrá traer un mapa de la
    /// base de datos: {"items":[{"type":"tree_dead","x":-31.2,"z":14,"yaw":80,"scale":1.1,"variant":2,"state":""}]}.
    /// Los campos son públicos y simples para que JsonUtility los lea.
    /// </summary>
    [Serializable]
    public class PropEntry
    {
        public string type;
        public float x;
        public float z;
        public float yaw;     // grados
        public float scale = 1f;
        public int variant;
        public string state;  // reservado (señales: erguida, inclinada, caída...)
    }

    [Serializable]
    public class PropLayoutData
    {
        public PropEntry[] items = new PropEntry[0];
    }

    public static class PropTypes
    {
        public const string TreeDead = "tree_dead";
        public const string Bush = "bush";
        public const string Grass = "grass";
        public const string CarWreck = "car_wreck";
        public const string PoleFallen = "pole_fallen";
        public const string Tire = "tire";
        public const string Bag = "bag";
        public const string SignBroken = "sign_broken";
        public const string SignStop = "sign_stop";
        public const string SignYield = "sign_yield";
        public const string SignSpeed = "sign_speed";
        public const string SignNoTurn = "sign_no_turn";
        public const string SignHazard = "sign_hazard";
        public const string SignGeneric = "sign_generic";

        public static readonly string[] SignTypes = { SignStop, SignYield, SignSpeed, SignNoTurn, SignHazard, SignGeneric };
        public static readonly string[] SignStates = { "", "upright", "tilted", "bent", "fallen", "pole_only", "hanging" };

        public static bool IsSign(string type) => Array.IndexOf(SignTypes, type) >= 0;

        public static readonly string[] All = { TreeDead, Bush, Grass, CarWreck, PoleFallen, Tire, Bag, SignBroken, SignStop, SignYield, SignSpeed, SignNoTurn, SignHazard, SignGeneric };

        /// <summary>Piezas grandes: bloquean el paso del jugador y del NavMesh. La hierba, bolsas y neumáticos no.</summary>
        public static bool IsObstacle(string type) => type == TreeDead || type == CarWreck || type == PoleFallen; // (una señal caída es un obstáculo pequeño: se decide por su estado)

        public static int VariantCount(string type)
        {
            switch (type)
            {
                case TreeDead: return FoliageGeometry.TreeVariants;
                case Bush: return FoliageGeometry.BushVariants;
                case Grass: return 2;   // verde / seca
                case CarWreck: return 3;
                case Bag: return 3;
                case SignStop: case SignYield: case SignSpeed: case SignNoTurn: case SignHazard: case SignGeneric: return SignGenerator.Variants;
                default: return 1;
            }
        }
    }

    public class ValidationResult
    {
        public readonly List<PropEntry> Valid = new List<PropEntry>();
        public readonly List<string> Errors = new List<string>();
    }

    /// <summary>
    /// Valida y limpia una lista de piezas venida de fuera (JSON de la BD): tipo conocido, números finitos,
    /// posición dentro del mapa, escala razonable y topes por tipo. Lo inválido se descarta y se informa.
    /// </summary>
    public static class PropLayoutValidator
    {
        public const float MinScale = 0.25f, MaxScale = 4f, MaxAbsCoordinate = 60f;
        public const int MaxTotal = 600;

        public static ValidationResult Validate(PropLayoutData layout, IDictionary<string, int> caps = null)
        {
            var res = new ValidationResult();
            if (layout?.items == null) { res.Errors.Add("layout vacío"); return res; }
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < layout.items.Length; i++)
            {
                var e = layout.items[i];
                string why = Check(e);
                if (why == null && res.Valid.Count >= MaxTotal) why = "se superó el máximo total de " + MaxTotal;
                if (why == null)
                {
                    counts.TryGetValue(e.type, out int c);
                    if (caps != null && caps.TryGetValue(e.type, out int cap) && c >= cap) why = "tope de '" + e.type + "' (" + cap + ")";
                    else counts[e.type] = c + 1;
                }
                if (why != null) res.Errors.Add("item " + i + ": " + why);
                else res.Valid.Add(e);
            }
            return res;
        }

        private static string Check(PropEntry e)
        {
            if (e == null) return "nulo";
            if (Array.IndexOf(PropTypes.All, e.type) < 0) return "tipo desconocido '" + e.type + "'";
            if (!Finite(e.x) || !Finite(e.z) || !Finite(e.yaw) || !Finite(e.scale)) return "número no válido";
            if (Math.Abs(e.x) > MaxAbsCoordinate || Math.Abs(e.z) > MaxAbsCoordinate) return "fuera del mapa";
            if (e.scale < MinScale || e.scale > MaxScale) return "escala fuera de rango";
            if (e.variant < 0 || e.variant >= PropTypes.VariantCount(e.type)) return "variante fuera de rango";
            if (PropTypes.IsSign(e.type) && Array.IndexOf(PropTypes.SignStates, e.state ?? "") < 0) return "estado de señal desconocido '" + e.state + "'";
            return null;
        }

        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
