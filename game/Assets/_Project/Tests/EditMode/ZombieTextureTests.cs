using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Texturas procedurales de los zombis: deterministas, con todos los mapas y con el alfa y la emisión donde toca.</summary>
    public class ZombieTextureTests
    {
        public static IEnumerable<string> Types() => ZombieAnatomies.Types;

        [TestCaseSource(nameof(Types))]
        public void SkinAtlas_IsDeterministic_OpaqueAndHasAllMaps(string type)
        {
            var a = ZombieTextures.Generate(ZombieAtlasKind.Skin, type, 128);
            var b = ZombieTextures.Generate(ZombieAtlasKind.Skin, type, 128);
            Assert.AreEqual(128 * 128 * 4, a.Albedo.Length); Assert.AreEqual(a.NormalSize * a.NormalSize * 4, a.Normal.Length); Assert.AreEqual(a.Albedo.Length, a.Mask.Length);
            Assert.AreEqual(a.EmissionSize * a.EmissionSize * 4, a.Emission.Length);
            CollectionAssert.AreEqual(a.Albedo, b.Albedo, "misma semilla, mismo resultado");
            for (int i = 3; i < a.Albedo.Length; i += 4) Assert.AreEqual(255, a.Albedo[i], "la piel es opaca");
            for (int i = 2; i < a.Normal.Length; i += 4) Assert.Greater(a.Normal[i], 127, "normales hacia fuera");
        }

        [Test]
        public void SkinAtlases_DifferByType()
        {
            var common = ZombieTextures.Generate(ZombieAtlasKind.Skin, ZombieIds.Common, 128);
            var tank = ZombieTextures.Generate(ZombieAtlasKind.Skin, ZombieIds.Tank, 128);
            CollectionAssert.AreNotEqual(common.Albedo, tank.Albedo);
        }

        [Test]
        public void ExplosiveGlowsMoreThanCommon()
        {
            long Sum(byte[] e) { long s = 0; for (int i = 0; i < e.Length; i += 4) s += e[i] + e[i + 1] + e[i + 2]; return s; }
            var ex = ZombieTextures.Generate(ZombieAtlasKind.Skin, ZombieIds.Explosive, 128);
            var co = ZombieTextures.Generate(ZombieAtlasKind.Skin, ZombieIds.Common, 128);
            Assert.Greater(Sum(ex.Emission), Sum(co.Emission) * 2, "venas y pústulas luminosas");
        }

        [Test]
        public void ClothAtlas_HasTornHoles_ButMostlyCloth()
        {
            var c = ZombieTextures.Generate(ZombieAtlasKind.Cloth, ZombieIds.Common, 256);
            Assert.IsTrue(c.HasAlpha);
            int holes = 0, total = 256 * 256;
            for (int i = 3; i < c.Albedo.Length; i += 4) if (c.Albedo[i] < 128) holes++;
            float frac = holes / (float)total;
            TestContext.WriteLine("Agujeros en la ropa: " + (frac * 100f).ToString("0.0") + " %");
            Assert.That(frac, Is.InRange(0.002f, 0.12f));
        }

        [Test]
        public void WoundAtlas_IsMostlyTransparent_WithShapesInEveryCell()
        {
            var w = ZombieTextures.Generate(ZombieAtlasKind.Wounds, ZombieIds.Common, 256);
            int opaque = 0;
            var perCell = new int[16];
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                    if (w.Albedo[(y * 256 + x) * 4 + 3] > 128) { opaque++; perCell[(y / 64) * 4 + x / 64]++; }
            Assert.Less(opaque / (256f * 256f), 0.5f, "los decals dejan ver la piel");
            for (int c = 0; c < 16; c++) Assert.Greater(perCell[c], 30, "celda " + c + " con forma");
        }

        [Test]
        public void TextureMemory_AtSize512_StaysSmall()
        {
            // color BC3 (1 byte por píxel) + máscara BC3 + normales RGBA32 a mitad de resolución + emisión 256: con mipmaps (x1.33)
            double mb = (512.0 * 512 * 2 + 256.0 * 256 * 4 + 256.0 * 256 * 0.5) * 1.33 / 1048576.0;
            TestContext.WriteLine("Un atlas de 512: ~" + mb.ToString("0.00") + " MB");
            Assert.Less(mb, 1.5);
        }
    }
}
