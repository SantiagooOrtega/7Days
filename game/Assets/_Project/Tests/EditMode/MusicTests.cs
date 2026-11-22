using System.IO;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Música: volumen por fase, ducking con VIGÍA, fundidos de 1.5 s, cruce de fuentes para clips cortos y la pista importada.</summary>
    public class MusicTests
    {
        [Test]
        public void VolumeByPhase_HighInMenu_LowInGame_DuckWithVigia()
        {
            Assert.AreEqual(1f, MusicMix.Target(MusicPhase.Menu, false, false), "alto en carga y menú");
            Assert.AreEqual(1f, MusicMix.Target(MusicPhase.Intro, false, false));
            Assert.AreEqual(0.4f, MusicMix.Target(MusicPhase.Intro, true, false), 1e-5f, "~40 % mientras habla VIGÍA");
            Assert.AreEqual(0.4f, MusicMix.Target(MusicPhase.Menu, true, false), 1e-5f);
            float game = MusicMix.Target(MusicPhase.Game, false, false);
            Assert.That(game, Is.InRange(0.15f, 0.2f), "~15-20 % en partida");
            Assert.AreEqual(0.4f, MusicMix.Target(MusicPhase.Game, false, true), 1e-5f, "~40 % con jefe o combate intenso");
            Assert.Less(MusicMix.Target(MusicPhase.Game, true, false), game, "ducking suave en partida");
            Assert.Greater(MusicMix.Target(MusicPhase.Game, true, false), game * 0.4f, "suave: no se apaga");
            Assert.AreEqual(MusicMix.Target(MusicPhase.Menu, false, false), MusicMix.Target(MusicPhase.Dead, false, false), "al morir, la música a volumen de menú");
            Assert.AreEqual(MusicMix.Target(MusicPhase.Menu, false, false), MusicMix.Target(MusicPhase.Victory, false, false), "al ganar, también");
        }

        [Test]
        public void Fades_DayOneAndDeathIn1_5s_DuckFaster()
        {
            Assert.AreEqual(1.5f, MusicMix.TransitionSeconds(MusicPhase.Menu, MusicPhase.Game), "al entrar al Día 1");
            Assert.AreEqual(1.5f, MusicMix.TransitionSeconds(MusicPhase.Game, MusicPhase.Dead), "al morir");
            Assert.Less(MusicMix.TransitionSeconds(MusicPhase.Game, MusicPhase.Game), 1.5f);
            // simula el fundido del menú (1.0) a partida a 60 FPS: llega en ~1.5 s, ni antes ni de golpe
            float from = 1f, to = MusicMix.Target(MusicPhase.Game, false, false), rate = MusicMix.Rate(from, to, 1.5f), v = from;
            int frames = 0;
            while (v != to && frames < 1000) { float prev = v; v = MusicMix.Step(v, to, rate, 1f / 60f); Assert.LessOrEqual(prev - v, 0.02f, "sin saltos"); frames++; }
            Assert.AreEqual(1.5f, frames / 60f, 0.05f);
        }

        [Test]
        public void ShortClip_Crossfades_LongClip_Loops()
        {
            Assert.IsTrue(MusicMix.NeedsCrossfade(30f)); Assert.IsFalse(MusicMix.NeedsCrossfade(181f), "la pista del juego (3 min) usa el bucle simple");
            Assert.IsFalse(MusicMix.NeedsCrossfade(0f), "sin clip, nada");
            MusicMix.CrossfadeGains(0f, out float o0, out float i0); MusicMix.CrossfadeGains(1f, out float o1, out float i1); MusicMix.CrossfadeGains(0.5f, out float om, out float im);
            Assert.AreEqual(1f, o0, 1e-5f); Assert.AreEqual(0f, i0, 1e-5f); Assert.AreEqual(0f, o1, 1e-5f); Assert.AreEqual(1f, i1, 1e-5f);
            Assert.AreEqual(1f, om * om + im * im, 1e-4f, "potencia constante en el cruce");
        }

        [Test]
        public void MusicTrack_Imported_StreamingVorbis_AndMusicGroupInMenu()
        {
            string mp3 = Path.Combine(Application.dataPath, "_Project/Audio/Music/musica_juego.mp3");
            Assert.IsTrue(File.Exists(mp3), "musica_juego.mp3");
            var imp = (UnityEditor.AudioImporter)UnityEditor.AssetImporter.GetAtPath("Assets/_Project/Audio/Music/musica_juego.mp3");
            Assert.IsNotNull(imp);
            Assert.AreEqual(AudioClipLoadType.Streaming, imp.defaultSampleSettings.loadType);
            Assert.AreEqual(AudioCompressionFormat.Vorbis, imp.defaultSampleSettings.compressionFormat);
            Assert.AreEqual(0.7f, imp.defaultSampleSettings.quality, 0.01f);
            var game = System.Reflection.Assembly.Load("Assembly-CSharp");
            var names = (string[])game.GetType("SevenDays.Core.AudioMix", true).GetField("Names").GetValue(null);
            CollectionAssert.Contains(names, "Música", "grupo Música en el menú Esc");
            Assert.AreEqual(names.Length, System.Enum.GetValues(game.GetType("SevenDays.Core.AudioGroup", true)).Length);
        }
    }
}
