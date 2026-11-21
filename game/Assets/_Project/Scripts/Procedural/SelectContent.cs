using System;

namespace SevenDays.Procedural
{
    /// <summary>
    /// Pantalla de selección de personaje y mapa (puro): medidas a la resolución de referencia (1920x1080; el Canvas escala igual a 1280x720 y 2560x1440),
    /// tamaños de letra, textos de cada personaje y mapa, y las barras de estadísticas. La UI (FrontEnd) solo coloca lo que dice esta clase.
    /// </summary>
    public static class SelectContent
    {
        // ---- tipografía (px a 1080p)
        public const int TitleSize = 54, OptionSize = 34, ValueSize = 32, DescSize = 25, LabelSize = 26, FooterSize = 26;
        public const float RowHeight = 76f;

        // ---- paneles (centro y tamaño, en coordenadas del Canvas de referencia, origen en el centro)
        public static readonly float[] ListPanel = { -500f, -40f, 700f, 560f };
        public static readonly float[] PreviewPanel = { 420f, -50f, 840f, 680f };

        // ---- dentro del panel de vista previa (origen en su centro): título arriba, imagen a la izquierda, datos a la derecha y descripción debajo
        public static readonly float[] PreviewTitle = { 0f, 280f, 780f, 70f };
        public static readonly float[] PreviewImage = { -215f, 45f, 350f, 380f };
        public static readonly float[] PreviewStats = { 205f, 45f, 410f, 380f };
        public static readonly float[] PreviewDesc = { 0f, -250f, 780f, 140f };

        /// <summary>Escala del Canvas (Scale With Screen Size, mezcla 0.5) para una resolución.</summary>
        public static float CanvasScale(float w, float h) => (float)Math.Pow(2.0, 0.5 * Math.Log(w / 1920.0, 2.0) + 0.5 * Math.Log(h / 1080.0, 2.0));

        /// <summary>Las tres barras del personaje (0-1): vida, velocidad y resistencia (curación y pulso con el arma).</summary>
        public static float[] Bars(CharacterVariantData v)
        {
            float life = Clamp01(v.maxHealth / 140f);
            float speed = Clamp01((v.moveSpeed - 0.7f) / 0.5f);
            float resist = Clamp01((v.healing * 0.5f + (2f - v.weaponSway) * 0.5f - 0.6f) / 0.7f);
            return new[] { life, speed, resist };
        }

        public static readonly string[] BarNames = { "VIDA", "VELOCIDAD", "RESISTENCIA" };

        public static string CharacterDescription(CharacterVariantData v)
        {
            switch (v.id)
            {
                case "calavera": return "Veterano con placa calavera en el casco. Aguanta más golpes que nadie y mantiene la línea cuando todo se viene abajo.";
                case "explorador": return "Ligero y rápido. Gorra, gafas y radio: llega antes al suministro y sale antes del cerco.";
                case "francotirador": return "Paciente y de pulso firme. Capucha con red y velo: el arma apenas se mueve al apuntar de lejos.";
                case "medico": return "Médico de campo con mochila de suministros. Sus vendas y botiquines curan más: mantiene al equipo en pie.";
                case "sam_duarte": return "Rescatista de Valle Hondo. Chaqueta naranja reflectante, capucha negra bajo la gorra. No vino a pelear: vino a buscar a los que faltan.";
                default: return "Superviviente de Valle Hondo.";
            }
        }

        public static string MapDescription(string id) => id == MapLibrary.ForestId
            ? "Noche cerrada entre pinos, niebla densa y una cabaña con tres entradas. Pocas coberturas duras: muévete y no te quedes quieto."
            : "Autopista y nave industrial en ruinas. Casa central con segundo piso y balcón, coberturas de hormigón y coches.";

        /// <summary>Dificultad del mapa (1-5) en texto y barra.</summary>
        public static string DifficultyName(int d) => d <= 1 ? "BAJA" : d == 2 ? "MEDIA" : d == 3 ? "ALTA" : d == 4 ? "MUY ALTA" : "EXTREMA";

        /// <summary>Espaciado de letras para titulares (la fuente del sistema no tiene "tracking"): un espacio fino entre letras.</summary>
        public static string Tracked(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var b = new System.Text.StringBuilder(s.Length * 2 + 8);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == ' ') { if (i > 0 && s[i - 1] != ' ') b.Append(WordGap); continue; }   // entre palabras: hueco triple (si no, se leen pegadas)
                b.Append(s[i]); if (i < s.Length - 1 && s[i + 1] != ' ') b.Append('\u2009');   // espacio fino entre letras
            }
            return b.ToString();
        }

        /// <summary>Hueco entre palabras del texto espaciado: tres espacios normales (las letras van separadas por un espacio fino).</summary>
        public const string WordGap = "   ";

        /// <summary>¿Se solapan dos rectángulos (centro x, y, ancho, alto)? Lo usan las pruebas de la pantalla.</summary>
        public static bool Overlap(float[] a, float[] b) => Math.Abs(a[0] - b[0]) * 2f < a[2] + b[2] && Math.Abs(a[1] - b[1]) * 2f < a[3] + b[3];
        /// <summary>¿Está "inner" dentro de "outer" (mismas coordenadas)?</summary>
        public static bool Inside(float[] inner, float[] outer) =>
            inner[0] - inner[2] / 2f >= outer[0] - outer[2] / 2f - 0.01f && inner[0] + inner[2] / 2f <= outer[0] + outer[2] / 2f + 0.01f
            && inner[1] - inner[3] / 2f >= outer[1] - outer[3] / 2f - 0.01f && inner[1] + inner[3] / 2f <= outer[1] + outer[3] / 2f + 0.01f;

        private static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
