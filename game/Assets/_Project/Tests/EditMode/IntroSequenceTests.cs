using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    public class IntroSequenceTests
    {
        private const string Script =
            "﻿# comentario\n" +
            "01 | NARRADOR | UNITY | Valle Hondo vivía de la Planta 7.\r\n" +
            "\n" +
            "02 | narrador | imagen | 23:47. La Planta 7 explotó.\n" +
            "04 | IBARRA | IMAGEN | Hora 6 | hora 20.\n" +
            "06 | VIGÍA | IMAGEN | Señal vital detectada.\n" +
            "07 | Vigia | Unity | Tienes seis balas. Que cuenten.\n";

        private static List<IntroPanel> Panels() => IntroScript.Parse(Script);

        /// <summary>Avanza el reloj en pasos pequeños, como harían los frames.</summary>
        private static void Run(IntroSequence s, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.02f) s.Tick(0.02f);
        }

        /// <summary>Pulsa avanzar hasta que cambie la viñeta (completar texto, terminar el fundido, salir y entrar la siguiente).</summary>
        private static void NextPanel(IntroSequence s)
        {
            int i = s.Index;
            for (int n = 0; n < 10 && s.Index == i && !s.IsDone; n++) s.Advance();
        }

        [Test]
        public void Parse_ReadsFields_IgnoresBlankCommentsAndBom_KeepsPipesInText()
        {
            var p = Panels();
            Assert.AreEqual(5, p.Count);
            Assert.AreEqual(1, p[0].Number);
            Assert.AreEqual(IntroSpeaker.Narrator, p[0].Speaker);
            Assert.AreEqual(IntroTextSource.Unity, p[0].Source);
            Assert.AreEqual("Valle Hondo vivía de la Planta 7.", p[0].Text);
            Assert.AreEqual(IntroTextSource.Image, p[1].Source);
            Assert.AreEqual(IntroSpeaker.Ibarra, p[2].Speaker);
            Assert.AreEqual("Hora 6 | hora 20.", p[2].Text);
            Assert.AreEqual(IntroSpeaker.Vigia, p[3].Speaker);
            Assert.AreEqual(IntroSpeaker.Vigia, p[4].Speaker);
            Assert.AreEqual("intro_01", p[0].ImageName);
            Assert.AreEqual("intro_07", p[4].ImageName);
        }

        [Test]
        public void Parse_SkipsBadLines_AndExplainsWhy()
        {
            var errors = new List<string>();
            var p = IntroScript.Parse("xx | NARRADOR | UNITY | a\n02 | PEPE | UNITY | b\n03 | VIGÍA | AQUÍ | c\n04 | VIGÍA | UNITY |   \n05 | VIGÍA | UNITY\n06 | VIGÍA | UNITY | ok", errors);
            Assert.AreEqual(1, p.Count);
            Assert.AreEqual(6, p[0].Number);
            Assert.AreEqual(5, errors.Count);
            Assert.IsEmpty(IntroScript.Parse(null));
        }

        [Test]
        public void Labels_PerSpeaker()
        {
            Assert.IsNull(IntroScript.Label(IntroSpeaker.Narrator));
            Assert.AreEqual("VIGÍA", IntroScript.Label(IntroSpeaker.Vigia));
            Assert.AreEqual("CORONEL IBARRA", IntroScript.Label(IntroSpeaker.Ibarra));
        }

        [Test]
        public void Story_DayHeadings()
        {
            Assert.AreEqual(7, Story.DayTitles.Length);
            Assert.AreEqual("Día 1: La ceniza", Story.DayHeading(1));
            Assert.AreEqual("Día 7: La floración", Story.DayHeading(7));
        }

        [Test]
        public void UnityText_TypesAfterFadeIn_ClickCompletesFirst_ThenMovesOn()
        {
            var s = new IntroSequence(Panels(), p => true);
            Assert.AreEqual(IntroStage.FadeIn, s.Stage);
            Assert.AreEqual(0f, s.Alpha, 1e-4f);
            Assert.IsTrue(s.ShowsText, "UNITY: Unity escribe el texto aunque haya imagen");
            Run(s, IntroSequence.FadeInSeconds + 0.2f);
            Assert.AreEqual(IntroStage.Typing, s.Stage);
            Assert.Greater(s.VisibleChars, 0);
            Assert.Less(s.VisibleChars, s.TextLength);

            s.Advance();   // primer clic: completa el texto, no cambia de viñeta
            Assert.AreEqual(0, s.Index);
            Assert.IsTrue(s.TextComplete);
            Assert.AreEqual(IntroStage.Hold, s.Stage);

            s.Advance();   // segundo clic: sale a negro y entra la siguiente
            Assert.AreEqual(IntroStage.FadeOut, s.Stage);
            Run(s, IntroSequence.FadeOutSeconds + 0.05f);
            Assert.AreEqual(1, s.Index);
            Assert.AreEqual(IntroStage.FadeIn, s.Stage);
        }

        [Test]
        public void ClickDuringFadeIn_CompletesTextButKeepsFading()
        {
            var s = new IntroSequence(Panels(), p => true);
            Run(s, 0.2f);
            s.Advance();
            Assert.AreEqual(IntroStage.FadeIn, s.Stage);
            Assert.IsTrue(s.TextComplete);
            Run(s, IntroSequence.FadeInSeconds);
            Assert.AreEqual(IntroStage.Hold, s.Stage, "con el texto ya completo no vuelve a escribirse");
            Assert.AreEqual(s.TextLength, s.VisibleChars);
        }

        [Test]
        public void ImagePanel_WithImage_NoText_WaitsForPlayer()
        {
            var s = new IntroSequence(Panels(), p => true);
            NextPanel(s);   // entra la 2 (IMAGEN)
            Assert.AreEqual(1, s.Index);
            Assert.IsFalse(s.ShowsText, "IMAGEN con imagen: el texto ya está dibujado");
            Run(s, 120f);
            Assert.AreEqual(1, s.Index, "no avanza sola");
            Assert.AreEqual(IntroStage.Hold, s.Stage);
            Assert.AreEqual(1f, s.Alpha);
        }

        [Test]
        public void ImagePanel_WithoutImage_ShowsItsText_AndStillWaits()
        {
            var s = new IntroSequence(Panels(), p => p.Number != 2);
            NextPanel(s);
            Assert.AreEqual(1, s.Index);
            Assert.IsFalse(s.HasImage);
            Assert.IsTrue(s.ShowsText);
            Run(s, 60f);
            Assert.IsTrue(s.TextComplete);
            Assert.AreEqual(1, s.Index);
        }

        [Test]
        public void UnityPanel_AdvancesByItselfAfterReading()
        {
            var s = new IntroSequence(Panels(), p => true);
            float read = IntroSequence.FadeInSeconds + s.Current.Text.Length / IntroSequence.CharsPerSecond + s.HoldSeconds + IntroSequence.FadeOutSeconds;
            Run(s, read + 0.2f);
            Assert.AreEqual(1, s.Index);
        }

        [Test]
        public void Back_GoesToPrevious_WithTextAlreadyWritten_AndDoesNothingOnFirst()
        {
            var s = new IntroSequence(Panels(), p => false);
            Run(s, 0.3f);
            s.Back();
            Assert.AreEqual(0, s.Index);
            Assert.AreEqual(IntroStage.FadeIn, s.Stage);
            Assert.AreEqual(0.3f, s.StageTime, 0.03f, "en la primera viñeta retroceder no reinicia nada");

            NextPanel(s);
            Assert.AreEqual(1, s.Index);
            s.Back();
            Assert.AreEqual(0, s.Index);
            Assert.IsTrue(s.TextComplete);
        }

        [Test]
        public void Skip_EndsAtOnce()
        {
            var s = new IntroSequence(Panels(), p => true);
            s.Skip();
            Assert.IsTrue(s.IsDone);
            Assert.IsTrue(s.WasSkipped);
            Assert.AreEqual(0f, s.Alpha);
            Assert.AreEqual(1f, s.Progress);
            s.Advance(); s.Back(); s.Tick(1f);
            Assert.IsTrue(s.IsDone);
        }

        [Test]
        public void ClickingThrough_ReachesTitle_ThenTitleClosesByItself()
        {
            var s = new IntroSequence(Panels(), p => true);
            float last = 0f;
            for (int i = 0; i < 40 && !s.OnTitle; i++)
            {
                s.Advance();
                Assert.GreaterOrEqual(s.Progress, last);
                last = s.Progress;
            }
            Assert.IsTrue(s.OnTitle);
            Assert.AreEqual(5, s.Index);
            Assert.IsNull(s.Current);
            Assert.IsFalse(s.ShowsText);
            Assert.AreEqual(1f, s.Progress);
            Assert.IsFalse(s.WaitsForPlayer);
            Run(s, IntroSequence.FadeInSeconds + IntroSequence.TitleHoldSeconds + IntroSequence.FadeOutSeconds + 0.1f);
            Assert.IsTrue(s.IsDone);
            Assert.IsFalse(s.WasSkipped);
        }

        [Test]
        public void Alpha_FadesInAndOut()
        {
            var s = new IntroSequence(Panels(), p => true);
            Run(s, IntroSequence.FadeInSeconds * 0.5f);
            Assert.AreEqual(0.5f, s.Alpha, 0.05f);
            s.Advance(); s.Advance();   // completa el texto y termina el fundido de entrada
            Assert.AreEqual(1f, s.Alpha);
            s.Advance();                // sale a negro
            Run(s, IntroSequence.FadeOutSeconds * 0.5f);
            Assert.AreEqual(0.5f, s.Alpha, 0.06f);
        }

        [Test]
        public void HugeFrame_DoesNotSkipPanelsThatWait()
        {
            var s = new IntroSequence(Panels(), p => true);
            s.Tick(500f);   // un tirón enorme
            Assert.AreEqual(1, s.Index, "la viñeta UNITY pasa sola, pero la IMAGEN siguiente la para");
        }

        [Test]
        public void NoPanels_OnlyTitle()
        {
            var s = new IntroSequence(new List<IntroPanel>());
            Assert.IsTrue(s.OnTitle);
            Run(s, 10f);
            Assert.IsTrue(s.IsDone);
        }

        [Test]
        public void Fit_NeverCropsTheImage_AndZoomsAtMost5Percent()
        {
            var screens = new[] { (1920f, 1080f), (1440f, 900f), (2560f, 1080f), (1024f, 768f) };
            var images = new[] { (1920f, 1080f), (1344f, 768f), (2048f, 1152f) };
            foreach (var (sw, sh) in screens)
                foreach (var (iw, ih) in images)
                    for (float t = 0f; t <= 1f; t += 0.25f)
                    {
                        var r = IntroLayout.Fit(sw, sh, iw, ih, t);
                        Assert.GreaterOrEqual(r.X, -0.01f, sw + "x" + sh);
                        Assert.GreaterOrEqual(r.Y, -0.01f);
                        Assert.LessOrEqual(r.X + r.W, sw + 0.01f);
                        Assert.LessOrEqual(r.Y + r.H, sh + 0.01f);
                        Assert.AreEqual(iw / ih, r.W / r.H, 1e-3f, "sin deformar");
                        var start = IntroLayout.Fit(sw, sh, iw, ih, 0f);
                        Assert.LessOrEqual(r.W / start.W, 1f + IntroLayout.ImageZoom + 1e-4f);
                    }
            var end = IntroLayout.Fit(1920f, 1080f, 1920f, 1080f, 1f);
            Assert.AreEqual(1920f, end.W, 0.01f, "al final del zoom ocupa justo la pantalla");
        }

        [Test]
        public void Cover_AlwaysFillsTheScreen_WhilePanning()
        {
            foreach (float pan in new[] { -1f, -0.4f, 0f, 0.7f, 1f })
                for (float t = 0f; t <= 1f; t += 0.25f)
                {
                    var r = IntroLayout.Cover(1440f, 900f, 1920f, 1080f, t, IntroLayout.UnityZoom, pan, -pan);
                    Assert.LessOrEqual(r.X, 0.01f);
                    Assert.LessOrEqual(r.Y, 0.01f);
                    Assert.GreaterOrEqual(r.X + r.W, 1440f - 0.01f);
                    Assert.GreaterOrEqual(r.Y + r.H, 900f - 0.01f);
                }
        }

        [Test]
        public void Ease_StartsAtZero_GrowsTowardsOne()
        {
            Assert.AreEqual(0f, IntroLayout.Ease(0f), 1e-5f);
            Assert.Greater(IntroLayout.Ease(4f), IntroLayout.Ease(1f));
            Assert.Less(IntroLayout.Ease(1000f), 1.0001f);
        }
    }
}
