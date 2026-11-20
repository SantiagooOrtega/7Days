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
    }
}
