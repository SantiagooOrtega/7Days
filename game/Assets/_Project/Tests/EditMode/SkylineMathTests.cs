using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>La azotea rota de cada edificio del fondo queda dentro de su planta (antes se desplazaba en ejes del mundo y colgaba en el aire).</summary>
    public class SkylineMathTests
    {
        [Test]
        public void RuinTop_AlwaysInsideTheBuildingFootprint()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 2000; i++)
            {
                float w = 8f + (float)rng.NextDouble() * 10f, d = 8f + (float)rng.NextDouble() * 8f;
                float sx = w * (0.3f + (float)rng.NextDouble() * 0.3f), sz = d * (0.3f + (float)rng.NextDouble() * 0.3f);
                SkylineMath.TopOffset(w, d, sx, sz, (float)rng.NextDouble(), (float)rng.NextDouble(), out float ox, out float oz);
                Assert.LessOrEqual(System.Math.Abs(ox) + sx / 2f, w / 2f - SkylineMath.Margin + 1e-4f);
                Assert.LessOrEqual(System.Math.Abs(oz) + sz / 2f, d / 2f - SkylineMath.Margin + 1e-4f);
            }
        }
    }
}
