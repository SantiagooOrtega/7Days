using System;
using System.Collections.Generic;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Suministros más visibles: cada tipo tiene su modelo e icono, el marcador respeta la distancia y la línea de visión, y la escasez no cambia.</summary>
    public class SupplyTests
    {
        public static IEnumerable<SupplyKind> Kinds() { foreach (SupplyKind k in Enum.GetValues(typeof(SupplyKind))) if (k != SupplyKind.Weapon) yield return k; }

        [TestCaseSource(nameof(Kinds))]
        public void EveryKind_HasItsOwnClosedModel_WithBaseOnTheGround(SupplyKind kind)
        {
            var m = SupplyModel.Build(kind);
            Assert.Greater(m.Triangles, 60, kind + ": modelo propio, no un cubo");
            Assert.Greater(m.Slots[SupplyModelData.SlotAccent].Triangles.Count, 0, kind + ": acento (franja de calibre o cruz)");
            Assert.AreEqual(0f, m.Min.Y, 0.006f, kind + ": apoyado en y = 0");
            Assert.Less(m.Max.Y, 0.45f); Assert.Greater(m.Max.Y, 0.05f);
            foreach (var slot in m.Slots) if (slot.Triangles.Count > 0) Assert.Greater(Winding.SignedVolume(slot), 0f, kind + ": caras hacia fuera");
            var o = SupplyModel.Outline(m.Slots[0], 0.01f);
            Assert.Less(Winding.SignedVolume(o), 0f, "el contorno es la malla hinchada con las caras al revés");
        }

        [Test]
        public void EveryLootItem_MapsToAModel_AndEachCaliberLooksDifferent()
        {
            var layout = LootDefaults.Create();
            var kinds = new HashSet<SupplyKind>();
            foreach (var t in layout.tables) foreach (var e in t.entries) kinds.Add(SupplyModel.KindOf(e.ToItem()));
            foreach (var k in kinds) if (k != SupplyKind.Weapon) Assert.Greater(SupplyModel.Build(k).Triangles, 0, k.ToString());
            CollectionAssert.IsSupersetOf(kinds, new[] { SupplyKind.AmmoPistol, SupplyKind.AmmoShotgun, SupplyKind.AmmoRifle, SupplyKind.Bandage, SupplyKind.MedkitSmall, SupplyKind.MedkitMedium, SupplyKind.MedBag, SupplyKind.Weapon });
            var colors = new HashSet<string>();
            foreach (var k in new[] { SupplyKind.AmmoPistol, SupplyKind.AmmoShotgun, SupplyKind.AmmoRifle }) colors.Add(string.Join(",", SupplyModel.Build(k).Colors[0]) + "|" + string.Join(",", SupplyModel.TypeColor(k)));
            Assert.AreEqual(3, colors.Count, "una caja de color distinto por calibre");
            Assert.AreEqual(SupplyKind.MedBag, SupplyModel.KindOf(LootItem.Medkit(MedkitKind.Large)));
        }

        [TestCaseSource(nameof(Kinds))]
        public void Icons_AreReadable(SupplyKind kind)
        {
            var px = SupplyModel.Icon(kind);
            Assert.AreEqual(SupplyModel.IconSize * SupplyModel.IconSize * 4, px.Length);
            int solid = 0, clear = 0;
            for (int i = 3; i < px.Length; i += 4) { if (px[i] > 230) solid++; if (px[i] == 0) clear++; }
            Assert.Greater(solid, 300, "silueta");
            Assert.Greater(clear, 200, "fondo transparente en las esquinas");
        }

        [Test]
        public void Marker_RespectsDistance_AndLineOfSight()
        {
            Assert.IsTrue(SupplyVisibility.ShowMarker(10f, true));
            Assert.IsFalse(SupplyVisibility.ShowMarker(16f, true), "a más de 15 m no hay marcador");
            Assert.IsFalse(SupplyVisibility.ShowMarker(10f, false), "tapado de lejos: oculto");
            Assert.IsTrue(SupplyVisibility.ShowMarker(5f, false), "muy cerca se ve aunque algo lo tape");
            Assert.Greater(SupplyVisibility.MarkerSize(15f), SupplyVisibility.MarkerSize(2f));
            Assert.IsTrue(SupplyVisibility.LookedAt(3f, 5f)); Assert.IsFalse(SupplyVisibility.LookedAt(6f, 2f)); Assert.IsFalse(SupplyVisibility.LookedAt(2f, 20f));
        }

        [Test]
        public void EdgeIndicator_StaysOnTheBorder_AndPointsDownWhenBehind()
        {
            foreach (var d in new[] { new V2(1f, 0f), new V2(-1f, 0.3f), new V2(0.2f, 1f), new V2(-0.7f, -0.7f) })
            {
                var p = SupplyVisibility.EdgePoint(d, false);
                float m = 0.06f;
                bool onBorder = Math.Abs(p.X - m) < 1e-4f || Math.Abs(p.X - (1f - m)) < 1e-4f || Math.Abs(p.Y - m) < 1e-4f || Math.Abs(p.Y - (1f - m)) < 1e-4f;
                Assert.IsTrue(onBorder, "en el borde: " + p.X + "," + p.Y);
                Assert.AreEqual(Math.Sign(d.X), Math.Sign(p.X - 0.5f), "del lado correcto");
            }
            Assert.Less(SupplyVisibility.EdgePoint(new V2(0.1f, 0.5f), true).Y, 0.5f, "detrás: se señala por abajo");
            Assert.IsTrue(SupplyVisibility.OnScreen(0.5f, 0.5f, 3f)); Assert.IsFalse(SupplyVisibility.OnScreen(0.5f, 0.5f, -1f));
        }

        [Test]
        public void Pulse_AndDropBeam_AreBounded()
        {
            for (float t = 0f; t < 5f; t += 0.05f) Assert.That(SupplyVisibility.Pulse(t, 0.3f), Is.InRange(0f, 1f));
            Assert.AreEqual(0f, SupplyVisibility.Beam(-0.1f)); Assert.AreEqual(1f, SupplyVisibility.Beam(1f), 1e-4f); Assert.AreEqual(0f, SupplyVisibility.Beam(SupplyVisibility.BeamSeconds + 0.1f));
        }

        [Test]
        public void Scarcity_IsUnchanged()
        {
            // la visibilidad no toca la cantidad: mismos topes y mismas tablas que en BALANCE.md / MAPA_SPAWN_LOOT.md
            var l = LootDefaults.Create();
            Assert.AreEqual(24, l.maxActivePickups); Assert.AreEqual(8, l.maxDropsPerDay); Assert.AreEqual(0.25f, l.respawnFractionPerDay, 1e-6f);
            Assert.AreEqual(6, l.startMagazine); Assert.AreEqual(6, l.startReserve);
            float common = 0f; foreach (var d in l.drops) if (d.zombie == "common") common = d.chance;
            Assert.AreEqual(0.12f, common, 1e-6f);
            var a = LootRoller.RollDrop(l, "tank", new Random(7)); var b = LootRoller.RollDrop(LootDefaults.Create(), "tank", new Random(7));
            Assert.AreEqual(a?.id, b?.id, "mismas tiradas con la misma semilla");
        }
    }
}
