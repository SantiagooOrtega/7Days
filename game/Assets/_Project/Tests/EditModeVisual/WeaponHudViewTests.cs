using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests.Visual
{
    /// <summary>El HUD de armas de verdad (Canvas uGUI creado por código): iconos generados, ranura activa resaltada, cambio de arma, ranuras vacías, rueda y alocaciones.</summary>
    public class WeaponHudViewTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private readonly List<GameObject> _created = new List<GameObject>();
        private Component _view;
        private Type _vt;

        [SetUp]
        public void SetUp()
        {
            _vt = Game.GetType("SevenDays.UI.WeaponHudView", true);
            _view = (Component)_vt.GetMethod("Create").Invoke(null, new object[] { null });
            _created.Add(_view.gameObject);
        }

        [TearDown]
        public void TearDown() { foreach (var g in _created) if (g != null) UnityEngine.Object.DestroyImmediate(g); _created.Clear(); }

        private WeaponBarState Bar => (WeaponBarState)_vt.GetProperty("Bar").GetValue(_view);
        private void Slots(IList<WeaponData> w) => _vt.GetMethod("SetSlots").Invoke(_view, new object[] { w });
        private void Tick(float seconds) { for (int i = 0; i < Mathf.CeilToInt(seconds * 60f); i++) _vt.GetMethod("Tick").Invoke(_view, new object[] { 1f / 60f }); }
        private RectTransform SlotRect(int i) => (RectTransform)_vt.GetMethod("SlotRect").Invoke(_view, new object[] { i });
        private float Glow(int i) => (float)_vt.GetMethod("GlowAlpha").Invoke(_view, new object[] { i });
        private float IconAlpha(int i) => (float)_vt.GetMethod("IconAlpha").Invoke(_view, new object[] { i });
        private Texture Icon(int i) => (Texture)_vt.GetMethod("IconOf").Invoke(_view, new object[] { i });
        private T Prop<T>(string n) => (T)_vt.GetProperty(n).GetValue(_view);

        private static List<WeaponData> Four() => new List<WeaponData>(WeaponCatalog.Defaults().GetRange(0, 4));

        [Test]
        public void EveryWeapon_GetsItsIcon_256x128_Distinct_AndCached()
        {
            var cache = Game.GetType("SevenDays.UI.WeaponIconCache", true).GetMethod("Get");
            var seen = new HashSet<Texture>();
            foreach (var w in WeaponCatalog.Defaults())
            {
                var t = (Texture2D)cache.Invoke(null, new object[] { w });
                Assert.IsNotNull(t, w.id); Assert.AreEqual(256, t.width); Assert.AreEqual(128, t.height);
                Assert.IsTrue(seen.Add(t), w.id + ": textura repetida");
                Assert.AreSame(t, cache.Invoke(null, new object[] { w }), w.id + ": no se vuelve a generar");
            }
            Debug.Log("[HUD] " + seen.Count + " iconos, " + (long)Game.GetType("SevenDays.UI.WeaponIconCache").GetProperty("Bytes").GetValue(null) / 1024 + " KB");
        }

        [Test]
        public void ActiveSlot_IsBigger_Glows_WithRarityColor_OthersFaded_EmptyHasFaintIcon()
        {
            Slots(Four());
            Bar.Set(4, 2, instant: true);
            Tick(1f);
            float wActive = SlotRect(2).sizeDelta.x, wOther = SlotRect(1).sizeDelta.x;
            Assert.Greater(wActive, wOther * 1.3f, "la activa se agranda (~1.4x)");
            Assert.Greater(SlotRect(2).sizeDelta.y, SlotRect(3).sizeDelta.y * 1.3f);
            Assert.AreEqual(72f, wOther, 0.5f); Assert.AreEqual(36f, SlotRect(1).sizeDelta.y, 0.5f);
            Assert.AreEqual(100f, wActive, 1f); Assert.AreEqual(50f, SlotRect(2).sizeDelta.y, 1f);
            Assert.Less(SlotRect(2).anchoredPosition.x, SlotRect(1).anchoredPosition.x - 10f, "la activa se desplaza a la izquierda");
            Assert.Greater(Glow(2), 0.2f, "brillo en la activa"); Assert.AreEqual(0f, Glow(1), 0.01f); Assert.AreEqual(0f, Glow(0), 0.01f);
            Assert.AreEqual(1f, IconAlpha(2), 0.01f); Assert.AreEqual(WeaponBarState.InactiveAlpha, IconAlpha(1), 0.02f, "las demás, semitransparentes");
            Assert.AreEqual(WeaponBarState.EmptyAlpha, IconAlpha(4), 0.02f, "ranura vacía tenue");
            var expected = WeaponCatalog.RarityColor(Four()[2].rarity);
            var rc = (Color)_vt.GetMethod("RarityOf").Invoke(_view, new object[] { 2 });
            Assert.AreEqual(expected[0], rc.r, 1e-4f); Assert.AreEqual(expected[2], rc.b, 1e-4f);
            Assert.AreNotSame(Icon(4), Icon(0), "el vacío usa su icono propio");
        }

        [Test]
        public void SwitchingWeapon_MovesTheHighlight_UpdatesNameAndAmmo_WithoutJumps()
        {
            Slots(Four());
            Bar.Set(4, 0, instant: true); Tick(0.5f);
            _vt.GetMethod("SetTexts").Invoke(_view, new object[] { "12 / 48", "SEMI", "Pistola" });
            Assert.AreEqual("12 / 48", Prop<string>("AmmoText")); Assert.AreEqual("Pistola", Prop<string>("NameText"));
            Assert.AreEqual(0f, Prop<float>("NameAlpha"), 0.01f, "sin cambio, el nombre no se ve");

            Bar.Set(4, 3);
            _vt.GetMethod("SetTexts").Invoke(_view, new object[] { "30 / 90", "AUTO  [X]", "Fusil de asalto" });
            float prev = SlotRect(3).sizeDelta.x, maxStep = 0f;
            for (int i = 0; i < 60; i++)
            {
                Tick(1f / 60f);
                float cur = SlotRect(3).sizeDelta.x; maxStep = Mathf.Max(maxStep, Mathf.Abs(cur - prev)); prev = cur;
            }
            Assert.Less(maxStep, 8f, "crece sin saltos (px de referencia por frame)");
            Assert.Greater(SlotRect(3).sizeDelta.x, SlotRect(0).sizeDelta.x * 1.3f, "ahora la grande es la 4");
            Assert.Greater(Glow(3), 0.2f); Assert.AreEqual(0f, Glow(0), 0.02f);
            Assert.AreEqual("30 / 90", Prop<string>("AmmoText")); Assert.AreEqual("Fusil de asalto", Prop<string>("NameText"));
            // el nombre se vio ~1.5 s y se fue
            Tick(0.2f); Assert.Greater(Prop<float>("NameAlpha"), 0.9f, "a ~1.2 s aún se ve entero");
            Tick(0.6f); Assert.AreEqual(0f, Prop<float>("NameAlpha"), 0.01f, "pasados 1.5 s se oculta");
        }

        private static Rect World(RectTransform r)
        {
            var c = new Vector3[4]; r.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        [Test]
        public void NoSlotOverlaps_AndAmmoModeAndNameStayClearOfTheColumn_EvenWhileSwitching()
        {
            Slots(WeaponCatalog.Defaults().GetRange(0, 5));
            Bar.Set(5, 0, instant: true); Tick(0.3f);
            _vt.GetMethod("SetTexts").Invoke(_view, new object[] { "30 / 120", "AUTO  [X]", "Fusil de asalto" });
            var ammo = (RectTransform)_vt.GetProperty("AmmoRect").GetValue(_view); var mode = (RectTransform)_vt.GetProperty("ModeRect").GetValue(_view); var name = (RectTransform)_vt.GetProperty("NameRect").GetValue(_view);
            foreach (int target in new[] { 4, 2, 0, 3 })
            {
                Bar.Set(5, target);
                for (int f = 0; f < 45; f++)
                {
                    Tick(1f / 60f);
                    for (int i = 0; i < 5; i++)
                    {
                        var a = World(SlotRect(i));
                        for (int j = i + 1; j < 5; j++) Assert.IsFalse(a.Overlaps(World(SlotRect(j))), "las ranuras " + (i + 1) + " y " + (j + 1) + " se solapan (cambiando a " + (target + 1) + ", frame " + f + ")");
                        Assert.IsFalse(a.Overlaps(World(ammo)), "la munición pisa la ranura " + (i + 1));
                        Assert.IsFalse(a.Overlaps(World(mode)), "el modo pisa la ranura " + (i + 1));
                        if (i != target) Assert.IsFalse(a.Overlaps(World(name)), "el nombre pisa la ranura " + (i + 1));
                    }
                    Assert.IsFalse(World(ammo).Overlaps(World(mode)), "munición y modo se solapan");
                }
            }
        }

        [Test]
        public void Wheel_OpensWithTheSlots_HighlightsTheSelected_AndHides()
        {
            Slots(Four());
            Bar.Set(4, 1, instant: true);
            _vt.GetMethod("SetWheel").Invoke(_view, new object[] { true, 2, 4 });
            Tick(0.6f);
            Assert.IsTrue(Prop<bool>("WheelVisible"), "la rueda se ve");
            Assert.AreEqual(2, Prop<int>("WheelSelected"));
            _vt.GetMethod("SetWheel").Invoke(_view, new object[] { false, -1, 4 });
            Tick(0.8f);
            Assert.IsFalse(Prop<bool>("WheelVisible"), "se cierra");
        }

        [Test]
        public void Canvas_ScalesWithScreenSize_AndTicksWithoutAllocating()
        {
            var canvas = _view.GetComponent<Canvas>();
            var scaler = _view.GetComponent<UnityEngine.UI.CanvasScaler>();
            Assert.AreEqual(UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Slots(Four()); Bar.Set(4, 1, instant: true);
            _vt.GetMethod("SetTexts").Invoke(_view, new object[] { "12 / 48", "SEMI", "Pistola" });
            Tick(1f);   // calentamiento (JIT)
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 600; i++)
            {
                _vt.GetMethod("SetTexts").Invoke(_view, new object[] { "12 / 48", "SEMI", "Pistola" });   // mismos textos: no se reasignan
                _vt.GetMethod("Tick").Invoke(_view, new object[] { 1f / 60f });
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Debug.Log("[HUD] alocaciones en 600 frames: " + bytes + " bytes (" + (bytes / 600f).ToString("0.0") + " por frame; incluye la reflexión de la prueba)");
            // la propia reflexión (Invoke con arrays de objetos) asigna ~60 B por llamada: 3 llamadas por frame -> tope generoso
            Assert.Less(bytes / 600f, 400f);
        }
    }
}
