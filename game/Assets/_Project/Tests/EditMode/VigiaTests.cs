using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class VigiaTests
    {
        private static string Folder => Path.Combine(Application.dataPath, "_Project/Audio/Vigia/Resources");
        private static Dictionary<string, VigiaLine> Catalog()
        {
            var errors = new List<string>();
            var c = VigiaCatalog.Parse(File.ReadAllText(Path.Combine(Folder, "vigia_frases.txt")), errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
            return c;
        }
        private static VigiaLine L(string id, string cat, VigiaPriority p) => new VigiaLine { Id = id, Category = cat, Priority = p, Variants = new[] { id + " a", id + " b" } };

        [Test]
        public void EveryLine_HasSubtitles_ClipsAndDocRow()
        {
            var c = Catalog();
            Assert.Greater(c.Count, 50);
            string doc = File.ReadAllText(Path.Combine(Application.dataPath, "../../docs/VIGIA_FRASES.md"));
            foreach (var l in c.Values)
            {
                Assert.That(l.Variants.Length, Is.InRange(1, 3), l.Id);
                foreach (var v in l.Variants) Assert.IsFalse(string.IsNullOrWhiteSpace(v), l.Id + " sin subtítulo");
                for (int i = 0; i < l.Variants.Length; i++) Assert.IsTrue(File.Exists(Path.Combine(Folder, VigiaCatalog.ClipName(l.Id, i) + ".wav")), "falta el clip " + VigiaCatalog.ClipName(l.Id, i));
                // las narraciones de las láminas (intro y epílogo) no son frases de partida: no van en la tabla del documento (que no se toca)
                if (l.Id.StartsWith("comic_") || l.Id.StartsWith("epilogo")) continue;
                StringAssert.Contains("`" + l.Id + "`", doc, "docs/VIGIA_FRASES.md sin " + l.Id);
            }
        }

        [Test]
        public void EveryIdTheGameUses_IsInTheCatalog()
        {
            var c = Catalog();
            var used = new List<string> { "bienvenida", "preparacion", "cuenta_10", "cuenta_5", "oleada", "oleada_superada", "victoria", "derrota", "masacre", "racha_imparable", "triple_cabeza", "tiro_cabeza",
                "muerte_doble", "muerte_triple", "muerte_cuadruple", "queda_uno", "quedan_cinco", "restantes_25", "restantes_50", "restantes_75", "cargador_vacio", "recarga", "sin_municion", "municion_baja",
                "suministro_cercano", "botiquin_usado", "vida_baja", "estado_critico", "inventario_lleno", "jefe_bruto", "jefe_lanzador", "jefe_final", "jefe_furia", "explosivo_cerca", "lanzador_cerca", "tanque",
                "flanqueo", "rodeado", "consejo_precision", "consejo_cobertura", "consejo_recarga", "primera_sangre", "baja_distancia", "cuerpo_a_cuerpo", "explosion_cadena" };
            for (int d = 1; d <= 7; d++) used.Add("dia_" + d);
            for (int i = 1; i <= 5; i++) used.Add("combo_" + i);
            foreach (int m in VigiaProgress.KillMilestones) used.Add("bajas_" + m);
            foreach (var id in used) Assert.IsTrue(c.ContainsKey(id), "falta " + id);
        }

        [Test]
        public void Priority_CriticalFirst_AndInterrupts()
        {
            var q = new VigiaQueue();
            var info = L("i", "progreso", VigiaPriority.Informativa);
            var crit = L("c", "amenaza", VigiaPriority.Critica);
            Assert.IsTrue(q.Request(info, 0));
            var a = q.Next(0, _ => 3, out bool intr);
            Assert.AreEqual("i", a.Line.Id); Assert.IsFalse(intr);
            q.Request(L("imp", "recurso", VigiaPriority.Importante), 0.5);
            Assert.IsNull(q.Next(0.5, _ => 3, out _), "no se solapa");
            q.Request(crit, 1);
            var b = q.Next(1, _ => 3, out intr);
            Assert.AreEqual("c", b.Line.Id); Assert.IsTrue(intr, "la crítica corta a la informativa");
            Assert.IsNull(q.Next(2, _ => 3, out _), "una crítica no se interrumpe");
            Assert.AreEqual("imp", q.Next(4.1, _ => 3, out _).Line.Id);
        }

        [Test]
        public void Expired_AreDropped()
        {
            var q = new VigiaQueue();
            q.Request(L("i", "progreso", VigiaPriority.Informativa), 0);
            Assert.IsNull(q.Next(VigiaQueue.Ttl(VigiaPriority.Informativa) + 0.1, _ => 1, out _));
            Assert.AreEqual(0, q.Pending.Count);
        }

        [Test]
        public void Cooldowns_ByCategoryAndLine()
        {
            var q = new VigiaQueue();
            var a = L("a", "consejo", VigiaPriority.Informativa);
            var b = L("b", "consejo", VigiaPriority.Informativa);
            q.Request(a, 0); q.Next(0, _ => 1, out _);
            Assert.IsFalse(q.Request(b, 10), "misma categoría en enfriamiento (consejo 45 s)");
            Assert.IsTrue(q.Request(b, 46));
            q.Next(46, _ => 1, out _);
            var c = L("c", "combo", VigiaPriority.Informativa);
            q.Request(c, 50); q.Next(50, _ => 1, out _);
            Assert.IsFalse(q.Request(c, 53), "la misma frase espera 8 s");
            Assert.IsTrue(q.Request(c, 58.1));
        }

        [Test]
        public void IntenseCombat_OnlyImportant_AndModes()
        {
            var q = new VigiaQueue();
            Assert.IsFalse(q.Request(L("i", "progreso", VigiaPriority.Informativa), 0, intense: true));
            Assert.IsTrue(q.Request(L("m", "amenaza", VigiaPriority.Importante), 0, intense: true));
            q.Mode = VigiaMode.Reducida;
            Assert.IsFalse(q.Request(L("i2", "progreso", VigiaPriority.Informativa), 0));
            q.Mode = VigiaMode.Apagada;
            Assert.IsFalse(q.Request(L("c", "amenaza", VigiaPriority.Critica), 0));
            q.Mode = VigiaMode.SoloTexto;   // en solo texto la cola funciona igual (sin audio) y dura lo que dura leer
            Assert.IsTrue(q.Request(L("t", "partida", VigiaPriority.Importante), 0));
            var it = q.Next(0, x => VigiaQueue.TextSeconds(x.Text), out _);
            Assert.AreEqual(VigiaQueue.TextSeconds(it.Text), q.CurrentEnds, 1e-9);
        }

        [Test]
        public void Variants_NeverRepeatTwiceInARow()
        {
            var q = new VigiaQueue(3);
            var l = L("v", "partida", VigiaPriority.Importante);
            int last = -1;
            for (int i = 0; i < 20; i++)
            {
                q.Request(l, i * 20); var it = q.Next(i * 20, _ => 1, out _);
                Assert.AreNotEqual(last, it.Variant); last = it.Variant;
            }
        }

        [Test]
        public void Combo_Thresholds()
        {
            var c = new ComboTracker();
            Assert.IsNull(c.OnKill(0, false));
            Assert.IsNull(c.OnKill(3, false));          // racha 2
            Assert.AreEqual("combo_1", c.OnKill(6, false));
            Assert.AreEqual("combo_2", c.OnKill(9, false));
            Assert.IsNull(c.OnKill(20, false), "pasada la ventana de 4 s la racha empieza de nuevo");
            Assert.AreEqual("muerte_doble", c.OnKill(21, false));
            Assert.AreEqual("muerte_triple", c.OnKill(22, false));
            Assert.AreEqual("muerte_cuadruple", c.OnKill(23, false));
            var h = new ComboTracker();
            Assert.AreEqual("tiro_cabeza", h.OnKill(0, true));
            Assert.AreEqual("tiro_cabeza", h.OnKill(10, true));
            Assert.AreEqual("triple_cabeza", h.OnKill(20, true));
            var r = new ComboTracker();
            string s = null;
            for (int i = 0; i < ComboTracker.ImparableAt; i++) s = r.OnKill(i * 2.0, false);
            Assert.AreEqual("racha_imparable", s);
            for (int i = ComboTracker.ImparableAt; i < ComboTracker.MasacreAt; i++) s = r.OnKill(i * 2.0, false);
            Assert.AreEqual("masacre", s);
        }

        [Test]
        public void Progress_MilestonesAndRemaining_OncePerWave()
        {
            var p = new VigiaProgress();
            Assert.AreEqual("bajas_10", p.OnTotalKills(10));
            Assert.IsNull(p.OnTotalKills(10));
            Assert.AreEqual("restantes_75", p.OnRemaining(30, 40));
            Assert.AreEqual("restantes_50", p.OnRemaining(20, 40));
            Assert.IsNull(p.OnRemaining(19, 40));
            Assert.AreEqual("restantes_25", p.OnRemaining(10, 40));
            Assert.AreEqual("quedan_cinco", p.OnRemaining(5, 40));
            Assert.AreEqual("queda_uno", p.OnRemaining(1, 40));
            p.NewWave();
            Assert.AreEqual("queda_uno", p.OnRemaining(1, 40));
            Assert.IsNull(p.OnTotalKills(10), "los hitos de bajas no se repiten");
        }

        [Test]
        public void RadioFx_AddsBeeps_StaysInRange()
        {
            int rate = 44100;
            var x = new float[rate];
            for (int i = 0; i < x.Length; i++) x[i] = 0.3f * Mathf.Sin(2 * Mathf.PI * 800 * i / rate) + 0.3f * Mathf.Sin(2 * Mathf.PI * 60 * i / rate);
            var y = RadioFx.Process(x, rate);
            Assert.AreEqual(RadioFx.ProcessedSeconds(1.0), y.Length / (double)rate, 0.002);
            foreach (var v in y) Assert.LessOrEqual(Mathf.Abs(v), 1f);
            Assert.Greater(Mathf.Abs(y[(int)(0.03f * rate)]), 0.05f, "bip de apertura");
        }

        [Test]
        public void RadioFx_IsGentle_WideBand_SubtleEcho_SoftBeep_KeepsDynamics()
        {
            Assert.LessOrEqual(RadioFx.LowCut, 200f); Assert.GreaterOrEqual(RadioFx.HighCut, 6000f, "paso banda ancho");
            Assert.LessOrEqual(RadioFx.EchoGain, 0.1f); Assert.LessOrEqual(RadioFx.EchoSeconds, 0.05f, "eco muy corto y sutil");
            Assert.LessOrEqual(RadioFx.BeepGain, 0.2f, "bip suave");
            Assert.LessOrEqual(RadioFx.SaturationMix, 0.3f, "poca distorsión");
            // un tono de 1 kHz conserva su forma: factor de cresta ~1.41 (una compresión fuerte lo aplasta hacia 1.0)
            int rate = 44100; var x = new float[rate];
            for (int i = 0; i < x.Length; i++) x[i] = 0.5f * Mathf.Sin(2 * Mathf.PI * 1000 * i / rate);
            var y = RadioFx.Process(x, rate);
            int s0 = (int)((RadioFx.BeepSeconds + RadioFx.Gap) * rate) + rate / 4, s1 = s0 + rate / 2;
            double sum = 0; float peak = 0f;
            for (int i = s0; i < s1; i++) { sum += y[i] * y[i]; peak = Mathf.Max(peak, Mathf.Abs(y[i])); }
            float crest = peak / (float)System.Math.Sqrt(sum / (s1 - s0));
            Assert.Greater(crest, 1.33f, "la voz no queda aplastada (cresta " + crest.ToString("0.00") + ")");
            Assert.LessOrEqual(peak, 0.81f, "sin saturar: pico a -2 dBFS");
        }
    
        [Test]
        public void ComicIntro_EightPanels_ImagesPresent_VigiaOnlyOn6To8WithClips()
        {
            var c = Catalog();
            Assert.AreEqual(8, IntroText.ComicPanels);
            for (int i = 1; i <= 8; i++)
                Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "_Project/Intro/Resources/intro_" + i.ToString("00") + ".png")), "lámina " + i);
            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "_Project/Intro/Resources/final_01.png")), "epílogo");
            Assert.IsFalse(File.Exists(Path.Combine(Application.dataPath, "_Project/Intro/Resources/intro.txt")), "sin el texto provisional");
            var expected = new[] { "Señal vital detectada.", "Soy VIGÍA, la seguridad de la Planta 7.", "Puedo mantenerte con vida siete días... si haces lo que te diga.", "Tienes seis balas. Que cuenten.", "Sal. Yo te cubro." };
            var said = new System.Collections.Generic.List<string>();
            for (int p = 0; p < 8; p++)
            {
                var ids = IntroText.ComicNarration(p);
                if (p < 5) Assert.AreEqual(0, ids.Length, "lámina " + (p + 1) + " sin voz (el texto va dibujado)");
                foreach (var id in ids)
                {
                    Assert.IsTrue(c.ContainsKey(id), id);
                    said.Add(c[id].Variants[0]);
                    Assert.IsTrue(File.Exists(Path.Combine(Folder, VigiaCatalog.ClipName(id, 0) + ".wav")), "voz de " + id);
                }
            }
            CollectionAssert.AreEqual(expected, said, "solo sus frases, en orden (6, 7 y 8)");
            Assert.AreEqual(3, IntroText.ComicNarration(5).Length); Assert.AreEqual(1, IntroText.ComicNarration(6).Length); Assert.AreEqual(1, IntroText.ComicNarration(7).Length);
        }

        [Test]
        public void Epilogue_OnlyOnVictory_12s_ThreePanelTour_VigiaLine()
        {
            Assert.IsTrue(IntroText.PlaysEpilogue(true)); Assert.IsFalse(IntroText.PlaysEpilogue(false), "no se muestra en la derrota");
            Assert.AreEqual(12f, IntroText.EpilogueSeconds, 0.5f);
            Assert.Greater(IntroText.EpilogueFade, IntroText.FadeSeconds, "fundido lento");
            var t = IntroText.EpilogueTour; Assert.AreEqual(12, t.Length, "tres viñetas");
            for (int i = 0; i < 3; i++) { Assert.GreaterOrEqual(t[i * 4], 0f); Assert.GreaterOrEqual(t[i * 4 + 1], 0f); Assert.LessOrEqual(t[i * 4] + t[i * 4 + 2], 1f); Assert.LessOrEqual(t[i * 4 + 1] + t[i * 4 + 3], 1f); }
            var c = Catalog();
            Assert.AreEqual("Transmisión enviada. Todo el mundo sabrá qué hicieron.", c[IntroText.EpilogueNarration[0]].Variants[0]);
            Assert.IsTrue(File.Exists(Path.Combine(Folder, VigiaCatalog.ClipName(IntroText.EpilogueNarration[0], 0) + ".wav")));
        }

        [Test]
        public void ComicIntro_TimingFadesMotionAndLetterbox()
        {
            Assert.AreEqual(7f, IntroText.HoldSeconds(0f), "paso automático a los ~7 s");
            Assert.Greater(IntroText.HoldSeconds(9f), 9f, "espera a que VIGÍA termine");
            Assert.AreEqual(0f, IntroText.Alpha(0f, -1f)); Assert.AreEqual(1f, IntroText.Alpha(2f, -1f), "fundido de entrada");
            Assert.AreEqual(0f, IntroText.Alpha(5f, IntroText.LeaveSeconds), 1e-5f, "fundido de salida");
            for (int p = 0; p < 8; p++)
            {
                var a = IntroText.KenBurns(p, 0f); var b = IntroText.KenBurns(p, 1f);
                Assert.AreEqual(1f, a[2], 1e-5f, "empieza sin zoom");
                Assert.Less(b[2], 0.95f, "zoom lento");
                foreach (var r in new[] { a, b, IntroText.KenBurns(p, 0.5f) }) { Assert.GreaterOrEqual(r[0], 0f); Assert.GreaterOrEqual(r[1], 0f); Assert.LessOrEqual(r[0] + r[2], 1f + 1e-5f); Assert.LessOrEqual(r[1] + r[3], 1f + 1e-5f); }
            }
            // letterbox: 16:9 en 4:3 (bandas arriba y abajo) y en 21:9 (a los lados), sin deformar
            var f = IntroText.Fit(1280, 720, 1024, 768); Assert.AreEqual(1024f, f[2], 1e-3f); Assert.Greater(f[1], 0f); Assert.AreEqual(1280f / 720f, f[2] / f[3], 1e-3f);
            f = IntroText.Fit(1280, 720, 2560, 1080); Assert.AreEqual(1080f, f[3], 1e-3f); Assert.Greater(f[0], 0f); Assert.AreEqual(1280f / 720f, f[2] / f[3], 1e-3f);
            // recorrido por tramos: empieza en el primero y termina en el último
            var rects = new[] { new[] { 0f, 0f, 0.4f, 0.4f }, new[] { 0.3f, 0.3f, 0.4f, 0.4f }, new[] { 0.6f, 0.6f, 0.4f, 0.4f } };
            CollectionAssert.AreEqual(rects[0], IntroText.Tour(rects, 0f)); CollectionAssert.AreEqual(rects[2], IntroText.Tour(rects, 1f));
        }

        [Test]
        public void Intro_Typewriter()
        {
            Assert.AreEqual(0, IntroText.Visible("hola", 0f));
            Assert.AreEqual(4, IntroText.Visible("hola", 10f));
            Assert.AreEqual(16, IntroText.Visible(new string('x', 40), 0.5f));
            Assert.GreaterOrEqual(IntroText.PanelSeconds("abc", 6f), 6.8f, "espera a que acabe la narración");
            Assert.AreEqual(2, IntroText.Parse("uno\n\n# nota\ndos\n").Count);
        }

        [Test]
        public void Subtitles_AtTheBottom_Compact_AwayFromTheCrosshair()
        {
            var r = SubtitleLayout.Rect(1920f, 70f, 0.09f);
            Assert.AreEqual(1080f * 0.91f, r[1] + r[3], 0.5f, "borde inferior al 9 % desde abajo");
            Assert.Greater(r[1], 540f + 120f, "lejos de la mira (centro)");
            Assert.LessOrEqual(r[2], SubtitleLayout.MaxWidth); Assert.AreEqual(1920f / 2f, r[0] + r[2] / 2f, 0.5f, "centrados");
            var low = SubtitleLayout.Rect(1920f, 70f, 0.01f); Assert.AreEqual(1080f * 0.96f, low[1] + low[3], 0.5f, "límite inferior 4 %");
            var narrow = SubtitleLayout.Rect(800f, 40f, 0.09f); Assert.LessOrEqual(narrow[2], 720f);
        }
    }
}
