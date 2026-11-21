using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Medidas de la pantalla de selección: todo dentro de su panel, sin solaparse, letras grandes y la misma composición a 720p, 1080p y 1440p.</summary>
    public class SelectLayoutTests
    {
        [Test]
        public void Preview_TitleImageStatsDescription_InsideAndNotOverlapping()
        {
            var panel = new[] { 0f, 0f, SelectContent.PreviewPanel[2] - 20f, SelectContent.PreviewPanel[3] - 20f };   // con margen del marco
            var parts = new[] { SelectContent.PreviewTitle, SelectContent.PreviewImage, SelectContent.PreviewStats, SelectContent.PreviewDesc };
            foreach (var p in parts) Assert.IsTrue(SelectContent.Inside(p, panel), "dentro del panel");
            for (int i = 0; i < parts.Length; i++)
                for (int j = i + 1; j < parts.Length; j++) Assert.IsFalse(SelectContent.Overlap(parts[i], parts[j]), i + " y " + j + " se pisan");
            Assert.Greater(SelectContent.PreviewTitle[1], SelectContent.PreviewImage[1], "título arriba");
            Assert.Less(SelectContent.PreviewDesc[1], SelectContent.PreviewImage[1], "descripción debajo");
            Assert.Less(SelectContent.PreviewImage[0], SelectContent.PreviewStats[0], "imagen a un lado");
        }

        [Test]
        public void Panels_InsideTheScreen_NotOverlappingEachOtherNorTitleNorFooter()
        {
            var screen = new[] { 0f, 0f, 1920f, 1080f };
            var title = new[] { -560f, 396f, 900f, 196f };      // "7DAYS" (y 325-495) y el subtítulo (y 298-342), arriba a la izquierda
            var footer = new[] { 0f, -504f, 1920f, 72f };
            foreach (var p in new[] { SelectContent.ListPanel, SelectContent.PreviewPanel })
            {
                Assert.IsTrue(SelectContent.Inside(p, screen));
                Assert.IsFalse(SelectContent.Overlap(p, title)); Assert.IsFalse(SelectContent.Overlap(p, footer));
            }
            Assert.IsFalse(SelectContent.Overlap(SelectContent.ListPanel, SelectContent.PreviewPanel));
        }

        [Test]
        public void FontSizes_AndScaleAt720p1080p1440p()
        {
            Assert.That(SelectContent.TitleSize, Is.InRange(48, 60)); Assert.That(SelectContent.OptionSize, Is.InRange(32, 36));
            Assert.That(SelectContent.DescSize, Is.InRange(24, 26)); Assert.GreaterOrEqual(SelectContent.RowHeight, SelectContent.OptionSize * 2f, "filas altas");
            Assert.AreEqual(2f / 3f, SelectContent.CanvasScale(1280, 720), 1e-4f);
            Assert.AreEqual(1f, SelectContent.CanvasScale(1920, 1080), 1e-4f);
            Assert.AreEqual(4f / 3f, SelectContent.CanvasScale(2560, 1440), 1e-4f, "16:9: la misma composición, escalada");
            Assert.GreaterOrEqual(SelectContent.DescSize * SelectContent.CanvasScale(1280, 720), 16f, "legible a 720p");
        }

        [Test]
        public void Content_EveryCharacterAndMapHasTextAndBars()
        {
            foreach (var v in CharacterVariants.Defaults())
            {
                StringAssert.DoesNotContain("Superviviente de Valle Hondo.", SelectContent.CharacterDescription(v), v.id + " con descripción propia");
                foreach (var b in SelectContent.Bars(v)) Assert.That(b, Is.InRange(0f, 1f));
            }
            Assert.AreNotEqual(SelectContent.MapDescription(MapLibrary.ForestId), SelectContent.MapDescription("calle_en_ruinas"));
            Assert.AreEqual("P\\u2009R\\u2009E", SelectContent.Tracked("PRE").Replace(" ", "\\u2009"));
        }
    }
}
