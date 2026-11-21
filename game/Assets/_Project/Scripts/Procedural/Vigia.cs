using System;
using System.Collections.Generic;

namespace SevenDays.Procedural
{
    public enum VigiaPriority { Informativa = 0, Importante = 1, Critica = 2 }
    /// <summary>Completa: todo. Reducida: solo importantes y críticas. Solo texto: subtítulos sin voz. Apagada: nada.</summary>
    public enum VigiaMode { Completa, Reducida, SoloTexto, Apagada }

    public class VigiaLine
    {
        public string Id, Category;
        public VigiaPriority Priority;
        public string[] Variants;
    }

    /// <summary>Colocación de los subtítulos de VIGÍA (pura, en el lienzo de 1080 de alto): centrados, abajo, con el borde inferior a "heightFrac" de la pantalla.</summary>
    public static class SubtitleLayout
    {
        public const float MaxWidth = 860f, Padding = 12f;

        /// <summary>x, y, ancho, alto del recuadro (y desde arriba, como IMGUI).</summary>
        public static float[] Rect(float canvasWidth, float textHeight, float heightFrac)
        {
            float w = Math.Min(MaxWidth, canvasWidth - 80f), h = textHeight + Padding;
            float bottom = 1080f * (1f - Math.Max(0.04f, Math.Min(0.3f, heightFrac)));
            return new[] { (canvasWidth - w) / 2f, bottom - h, w, h };
        }
    }

    /// <summary>Catálogo cerrado de frases (vigia_frases.txt: id TAB categoría TAB prioridad TAB variantes separadas por " | ").</summary>
    public static class VigiaCatalog
    {
        public static readonly string[] Categories = { "partida", "progreso", "combo", "recurso", "amenaza", "consejo" };

        public static Dictionary<string, VigiaLine> Parse(string text, List<string> errors = null)
        {
            var d = new Dictionary<string, VigiaLine>();
            foreach (var raw in text.Split('\n'))
            {
                var l = raw.TrimEnd('\r');
                if (l.Trim().Length == 0 || l.StartsWith("#")) continue;
                var c = l.Split('\t');
                if (c.Length != 4) { errors?.Add("línea con " + c.Length + " columnas: " + l); continue; }
                VigiaPriority p;
                switch (c[2]) { case "critica": p = VigiaPriority.Critica; break; case "importante": p = VigiaPriority.Importante; break; case "informativa": p = VigiaPriority.Informativa; break; default: errors?.Add(c[0] + ": prioridad " + c[2]); continue; }
                if (Array.IndexOf(Categories, c[1]) < 0) errors?.Add(c[0] + ": categoría " + c[1]);
                var vs = c[3].Split(new[] { " | " }, StringSplitOptions.None);
                for (int i = 0; i < vs.Length; i++) vs[i] = vs[i].Trim();
                if (d.ContainsKey(c[0])) errors?.Add("id repetido " + c[0]);
                d[c[0]] = new VigiaLine { Id = c[0], Category = c[1], Priority = p, Variants = vs };
            }
            return d;
        }

        public static string ClipName(string id, int variant) => "vigia_" + id + "_" + (variant + 1);
    }

    /// <summary>Lo que se va a decir: frase, variante elegida y cuándo caduca.</summary>
    public class VigiaItem
    {
        public VigiaLine Line;
        public int Variant;
        public double RequestedAt, ExpiresAt;
        public string Text => Line.Variants[Variant];
    }

    /// <summary>
    /// Cola de VIGÍA (pura): prioridad (crítica > importante > informativa), sin solapar, descarta lo caducado, enfriamiento por categoría y por frase, y en combate
    /// intenso solo deja pasar lo importante. Una crítica interrumpe lo que suena si no es crítico.
    /// </summary>
    public class VigiaQueue
    {
        public const int MaxQueued = 6;
        public const double SameLineCooldown = 8.0;
        public static double Ttl(VigiaPriority p) => p == VigiaPriority.Critica ? 15.0 : (p == VigiaPriority.Importante ? 8.0 : 4.0);
        public static double CategoryCooldown(string category)
        {
            switch (category) { case "combo": return 2.0; case "progreso": return 3.0; case "recurso": return 5.0; case "amenaza": return 4.0; case "consejo": return 45.0; default: return 0.0; }
        }

        public VigiaMode Mode = VigiaMode.Completa;
        public readonly List<VigiaItem> Pending = new List<VigiaItem>();
        public VigiaItem Current { get; private set; }
        public double CurrentEnds { get; private set; }
        private readonly Dictionary<string, double> _catReady = new Dictionary<string, double>();
        private readonly Dictionary<string, double> _lineReady = new Dictionary<string, double>();
        private readonly Dictionary<string, int> _lastVariant = new Dictionary<string, int>();
        private readonly Random _rng;

        public VigiaQueue(int seed = 1) { _rng = new Random(seed); }

        public bool Allowed(VigiaLine l, bool intense)
        {
            if (Mode == VigiaMode.Apagada) return false;
            if (Mode == VigiaMode.Reducida && l.Priority == VigiaPriority.Informativa) return false;
            if (intense && l.Priority == VigiaPriority.Informativa) return false;
            return true;
        }

        /// <summary>Pide decir una frase. false si no se admite (modo, combate intenso, enfriamiento o ya en cola).</summary>
        public bool Request(VigiaLine l, double now, bool intense = false)
        {
            if (l == null || !Allowed(l, intense)) return false;
            if (_lineReady.TryGetValue(l.Id, out double lr) && now < lr && l.Category != "partida") return false;
            if (_catReady.TryGetValue(l.Category, out double cr) && now < cr && l.Priority != VigiaPriority.Critica) return false;
            foreach (var p in Pending) if (p.Line.Id == l.Id) return false;
            if (Current != null && Current.Line.Id == l.Id && now < CurrentEnds) return false;
            Pending.Add(new VigiaItem { Line = l, Variant = PickVariant(l), RequestedAt = now, ExpiresAt = now + Ttl(l.Priority) });
            if (Pending.Count > MaxQueued)
            {
                // sobra uno: fuera el menos importante y más viejo
                int worst = 0;
                for (int i = 1; i < Pending.Count; i++) if (Pending[i].Line.Priority < Pending[worst].Line.Priority || (Pending[i].Line.Priority == Pending[worst].Line.Priority && Pending[i].RequestedAt < Pending[worst].RequestedAt)) worst = i;
                Pending.RemoveAt(worst);
            }
            return true;
        }

        private int PickVariant(VigiaLine l)
        {
            int n = l.Variants.Length;
            if (n <= 1) return 0;
            int last = _lastVariant.TryGetValue(l.Id, out int v) ? v : -1;
            int pick = _rng.Next(n - (last >= 0 ? 1 : 0));
            if (last >= 0 && pick >= last) pick++;   // nunca la misma variante dos veces seguidas
            _lastVariant[l.Id] = pick;
            return pick;
        }

        /// <summary>
        /// Avanza la cola. Devuelve la frase que debe empezar ahora (o null). duration(item) da cuánto dura (audio o texto). interrupted = true si cortó la anterior.
        /// </summary>
        public VigiaItem Next(double now, Func<VigiaItem, double> duration, out bool interrupted)
        {
            interrupted = false;
            for (int i = Pending.Count - 1; i >= 0; i--) if (now > Pending[i].ExpiresAt) Pending.RemoveAt(i);   // sin cierre: se llama cada frame
            if (Pending.Count == 0) return null;
            int best = 0;
            for (int i = 1; i < Pending.Count; i++)
                if (Pending[i].Line.Priority > Pending[best].Line.Priority || (Pending[i].Line.Priority == Pending[best].Line.Priority && Pending[i].RequestedAt < Pending[best].RequestedAt)) best = i;
            var item = Pending[best];
            bool busy = Current != null && now < CurrentEnds;
            if (busy)
            {
                if (item.Line.Priority == VigiaPriority.Critica && Current.Line.Priority != VigiaPriority.Critica) interrupted = true;
                else return null;
            }
            Pending.RemoveAt(best);
            Current = item;
            CurrentEnds = now + duration(item);
            _catReady[item.Line.Category] = now + CategoryCooldown(item.Line.Category);
            _lineReady[item.Line.Id] = now + SameLineCooldown;
            return item;
        }

        public bool Speaking(double now) => Current != null && now < CurrentEnds;

        public void Clear() { Pending.Clear(); Current = null; CurrentEnds = 0; }

        /// <summary>Duración del subtítulo sin audio: ~16 caracteres por segundo, mínimo 1.6 s.</summary>
        public static double TextSeconds(string text) => Math.Max(1.6, 0.9 + text.Length / 16.0);
    }

    /// <summary>
    /// Combos y rachas (puro). Una baja a menos de ChainWindow (4 s) de la anterior alarga la racha. Bajas a menos de MultiWindow (1.5 s) entre sí son multi-baja:
    /// 2 doble, 3 triple, 4+ cuádruple (tiene preferencia). Racha 3 -> "combo uno", 4 -> "combo dos" ... 7 -> "combo cinco", 8 -> racha imparable, 12 -> masacre.
    /// Tres headshots seguidos -> "triple cabeza"; si no, cada headshot puede decir "tiro a la cabeza". Devuelve como mucho una frase por baja.
    /// </summary>
    public class ComboTracker
    {
        public const double ChainWindow = 4.0, MultiWindow = 1.5;
        public const int ImparableAt = 8, MasacreAt = 12;
        public int Chain { get; private set; }
        public int Multi { get; private set; }
        public int HeadStreak { get; private set; }
        private double _last = double.NegativeInfinity;

        public string OnKill(double now, bool headshot)
        {
            Chain = now - _last <= ChainWindow ? Chain + 1 : 1;
            Multi = now - _last <= MultiWindow ? Multi + 1 : 1;
            _last = now;
            HeadStreak = headshot ? HeadStreak + 1 : 0;

            if (Multi >= 4) return "muerte_cuadruple";
            if (Multi == 3) return "muerte_triple";
            if (Multi == 2) return "muerte_doble";
            if (Chain == MasacreAt) return "masacre";
            if (Chain == ImparableAt) return "racha_imparable";
            if (Chain >= 3 && Chain <= 7) return "combo_" + (Chain - 2);
            if (HeadStreak == 3) return "triple_cabeza";
            if (headshot) return "tiro_cabeza";
            return null;
        }

        public void Reset() { Chain = Multi = HeadStreak = 0; _last = double.NegativeInfinity; }
    }

    /// <summary>Umbrales de progreso (puros): hitos de bajas totales y zombis restantes de la oleada. Cada umbral se anuncia una vez.</summary>
    public class VigiaProgress
    {
        public static readonly int[] KillMilestones = { 10, 25, 50, 100 };
        private readonly HashSet<string> _said = new HashSet<string>();

        public string OnTotalKills(int total)
        {
            foreach (int m in KillMilestones) if (total == m && _said.Add("bajas_" + m)) return "bajas_" + m;
            return null;
        }

        /// <summary>Nueva oleada: los umbrales de restantes vuelven a valer.</summary>
        public void NewWave() { _said.RemoveWhere(s => !s.StartsWith("bajas_")); }

        public string OnRemaining(int remaining, int total)
        {
            if (total <= 0) return null;
            string id = null;
            if (remaining == 1) id = "queda_uno";
            else if (remaining == 5 && total > 6) id = "quedan_cinco";
            else if (remaining * 4 <= total && total >= 12) id = "restantes_25";
            else if (remaining * 2 <= total && total >= 8) id = "restantes_50";
            else if (remaining * 4 <= total * 3 && total >= 12) id = "restantes_75";
            return id != null && _said.Add(id) ? id : null;
        }
    }
}
