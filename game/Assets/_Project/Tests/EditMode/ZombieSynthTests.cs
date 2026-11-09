using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Síntesis de voces de zombi: todos los eventos suenan, sin saturar ni NaN, deterministas, variados por muestra y por timbre, y con el carácter de cada tipo.</summary>
    public class ZombieSynthTests
    {
        [Test]
        public void EveryTypeAndEvent_RendersCleanAudio()
        {
            foreach (string t in ZombieAnatomies.Types)
                foreach (var ev in ZombieSoundMap.EventsFor(t))
                    for (int v = 0; v < ZombieSoundMap.Variants(ev); v++)
                    {
                        var d = ZombieSynth.Render(t, ev, v, 0);
                        string id = t + " " + ev + " " + v;
                        Assert.Greater(d.Length, ZombieSynth.Rate / 10, id + ": demasiado corto");
                        Assert.Less(d.Length, ZombieSynth.Rate * 3, id + ": demasiado largo");
                        float peak = 0f; double energy = 0;
                        foreach (float x in d) { Assert.IsFalse(float.IsNaN(x) || float.IsInfinity(x), id); peak = Math.Max(peak, Math.Abs(x)); energy += x * x; }
                        Assert.LessOrEqual(peak, 0.95f, id + ": satura");
                        Assert.Greater(Math.Sqrt(energy / d.Length), 0.02, id + ": casi silencio");
                    }
        }

        [Test]
        public void Render_IsDeterministic_AndVariantsAndTimbresDiffer()
        {
            var a = ZombieSynth.Render(ZombieIds.Common, ZombieSound.Idle, 1, 0);
            var b = ZombieSynth.Render(ZombieIds.Common, ZombieSound.Idle, 1, 0);
            CollectionAssert.AreEqual(a, b);
            Assert.Greater(Difference(a, ZombieSynth.Render(ZombieIds.Common, ZombieSound.Idle, 2, 0)), 0.2f, "otra muestra del mismo evento");
            Assert.Greater(Difference(a, ZombieSynth.Render(ZombieIds.Common, ZombieSound.Idle, 1, 1)), 0.2f, "otra garganta");
        }

        [Test]
        public void Tank_IsDeeperThanCommon_WhichIsDeeperThanRunnerShriek()
        {
            float tank = Pitch(ZombieSynth.Render(ZombieIds.Tank, ZombieSound.Roar, 0, 0));
            float common = Pitch(ZombieSynth.Render(ZombieIds.Common, ZombieSound.Idle, 0, 0));
            float shriek = Pitch(ZombieSynth.Render(ZombieIds.Runner, ZombieSound.Shriek, 0, 0));
            Assert.Less(tank, common, "el tanque ruge más grave");
            Assert.Less(common, shriek, "el chillido del corredor es agudo");
            Assert.Greater(LowShare(ZombieSynth.Render(ZombieIds.Tank, ZombieSound.Idle, 0, 0)), LowShare(ZombieSynth.Render(ZombieIds.Runner, ZombieSound.Idle, 0, 0)) + 0.05f, "timbre más oscuro");
        }

        [Test]
        public void FuseLoop_HasNoSeam()
        {
            var d = ZombieSynth.Render(ZombieIds.Explosive, ZombieSound.Fuse, 0, 0);
            float jump = Math.Abs(d[d.Length - 1] - d[0]);
            float typical = 0f; for (int i = 1; i < d.Length; i++) typical += Math.Abs(d[i] - d[i - 1]); typical /= d.Length - 1;
            Assert.Less(jump, typical * 6f + 0.02f, "salto en la costura del bucle");
        }

        [Test]
        public void Headshot_IsShortAndDry_DeathIsLong()
        {
            var hs = ZombieSynth.Render(ZombieIds.Common, ZombieSound.Headshot, 0, 0);
            var death = ZombieSynth.Render(ZombieIds.Common, ZombieSound.Death, 0, 0);
            Assert.Less(hs.Length, ZombieSynth.Rate * 0.4f);
            Assert.Greater(death.Length, ZombieSynth.Rate * 1.2f);
        }

        [Test]
        public void EventMapping_ByTypeAndZone()
        {
            Assert.AreEqual(ZombieSound.Pant, ZombieSoundMap.Idle(ZombieIds.Runner));
            Assert.AreEqual(ZombieSound.Shriek, ZombieSoundMap.Alert(ZombieIds.Runner));
            Assert.AreEqual(ZombieSound.Roar, ZombieSoundMap.Alert(ZombieIds.Tank));
            Assert.AreEqual(ZombieSound.Gurgle, ZombieSoundMap.Idle(ZombieIds.Spitter));
            Assert.AreEqual(ZombieSound.PainHead, ZombieSoundMap.Pain(HitZone.Head));
            Assert.AreEqual(ZombieSound.PainLimb, ZombieSoundMap.Pain(HitZone.Legs));
            CollectionAssert.Contains(ZombieSoundMap.EventsFor(ZombieIds.Armored), ZombieSound.Metal);
            CollectionAssert.Contains(ZombieSoundMap.EventsFor(ZombieIds.Explosive), ZombieSound.Fuse);
            CollectionAssert.Contains(ZombieSoundMap.EventsFor(ZombieIds.Tank), ZombieSound.Step);
        }

        private static float Difference(float[] a, float[] b)
        {
            int n = Math.Min(a.Length, b.Length); double diff = 0, en = 0;
            for (int i = 0; i < n; i++) { diff += Math.Abs(a[i] - b[i]); en += Math.Abs(a[i]); }
            return (float)(diff / Math.Max(1e-6, en)) + (a.Length != b.Length ? 0.3f : 0f);
        }

        /// <summary>Tono fundamental (Hz) por autocorrelación en la parte central del sonido.</summary>
        private static float Pitch(float[] d)
        {
            int start = d.Length / 3, len = Math.Min(4096, d.Length / 3);
            int minLag = ZombieSynth.Rate / 600, maxLag = ZombieSynth.Rate / 35;
            double best = double.MinValue; int bestLag = minLag;
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                double s = 0; for (int i = 0; i < len; i++) s += d[start + i] * d[start + i + lag];
                if (s > best * 1.08) { best = s; bestLag = lag; }
            }
            return ZombieSynth.Rate / (float)bestLag;
        }

        /// <summary>Parte de la energía por debajo de ~400 Hz (filtro paso-bajo de un polo): más alta = timbre más oscuro.</summary>
        private static float LowShare(float[] d)
        {
            float k = 1f - (float)Math.Exp(-2 * Math.PI * 400 / ZombieSynth.Rate), lp = 0f; double low = 0, all = 0;
            foreach (float x in d) { lp += (x - lp) * k; low += lp * lp; all += x * x; }
            return (float)(low / Math.Max(1e-9, all));
        }
    }
}
