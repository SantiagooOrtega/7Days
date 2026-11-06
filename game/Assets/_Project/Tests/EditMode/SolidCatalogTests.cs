using System;
using System.Linq;
using NUnit.Framework;
using SevenDays.Procedural;

namespace SevenDays.Tests
{
    /// <summary>Recorre la lista de props sólidos y falla si alguno no tiene collider (o si no es un collider simple).</summary>
    public class SolidCatalogTests
    {
        [Test]
        public void EveryPropType_IsClassified_SolidOnesHaveACollider_NonSolidOnesDont()
        {
            foreach (var type in PropTypes.All)
            {
                for (int variant = 0; variant < PropTypes.VariantCount(type); variant++)
                {
                    foreach (float scale in new[] { 0.5f, 1.0f, 1.2f, 2.5f })
                    {
                        var shape = SolidCatalog.ShapeFor(type, variant, scale);
                        bool solid = SolidCatalog.IsSolid(type, variant, scale);
                        Assert.AreEqual(solid, shape != ColliderShape.None, type + " v" + variant + " x" + scale + ": sólido y collider deben coincidir");
                        Assert.That(shape, Is.AnyOf(ColliderShape.None, ColliderShape.Box, ColliderShape.Capsule, ColliderShape.Sphere), "solo colliders simples");
                    }
                }
            }
        }

        [Test]
        public void MandatorySolidProps_AlwaysHaveACollider_AtAnyVariantAndScale()
        {
            // árboles (tronco), coches y postes caídos: nunca se atraviesan
            foreach (var type in new[] { PropTypes.TreeDead, PropTypes.CarWreck, PropTypes.PoleFallen })
                for (int v = 0; v < PropTypes.VariantCount(type); v++)
                    foreach (float s in new[] { 0.25f, 0.8f, 1f, 1.4f, 4f })
                        Assert.AreNotEqual(ColliderShape.None, SolidCatalog.ShapeFor(type, v, s), type + " v" + v + " x" + s + " sin collider");
            Assert.AreEqual(ColliderShape.Capsule, SolidCatalog.ShapeFor(PropTypes.TreeDead, 0, 1f), "árbol = collider de tronco");
        }

        [Test]
        public void Grass_SmallBushes_Tires_Bags_AreNotSolid()
        {
            Assert.AreEqual(ColliderShape.None, SolidCatalog.ShapeFor(PropTypes.Grass, 0, 1f));
            Assert.AreEqual(ColliderShape.None, SolidCatalog.ShapeFor(PropTypes.Tire, 0, 1f));
            Assert.AreEqual(ColliderShape.None, SolidCatalog.ShapeFor(PropTypes.Bag, 0, 1f));
            Assert.AreEqual(ColliderShape.None, SolidCatalog.ShapeFor(PropTypes.Bush, 0, SolidCatalog.LargeBushScale - 0.01f), "arbusto pequeño");
            Assert.AreEqual(ColliderShape.None, SolidCatalog.ShapeFor(PropTypes.Bush, 1, 2f), "el arbusto de ramitas no bloquea");
            Assert.AreEqual(ColliderShape.Sphere, SolidCatalog.ShapeFor(PropTypes.Bush, 0, SolidCatalog.LargeBushScale), "arbusto grande");
        }

        [Test]
        public void Signs_ThinPolesDontBlock()
        {
            foreach (var t in PropTypes.SignTypes) Assert.AreEqual(ColliderShape.None, SolidCatalog.ShapeFor(t, 0, 1f), t);
        }

        [Test]
        public void MapPieces_EverySolidOneHasASimpleCollider_AndNoNonSolidOneHasOne()
        {
            Assert.GreaterOrEqual(SolidCatalog.MapPieces.Count(p => p.Solid), 15);
            foreach (var p in SolidCatalog.MapPieces)
            {
                if (p.Solid) Assert.That(p.Shape, Is.AnyOf(ColliderShape.Box, ColliderShape.Capsule, ColliderShape.Sphere), p.Name + ": todo lo sólido debe tener un collider simple");
                else Assert.AreEqual(ColliderShape.None, p.Shape, p.Name + ": lo no sólido no lleva collider");
                Assert.IsFalse(string.IsNullOrEmpty(p.Reason), p.Name + " sin motivo documentado");
            }
        }

        [Test]
        public void MapPieces_KeyObjectsAreListed()
        {
            foreach (var name in new[] { "Wall N", "Barrier 1", "Crate 3", "Container A", "Pillar 0", "Barrel 2", "Fire Barrel 0", "Lamp Pole 1", "Pipe 0", "Car Wreck 1", "Back Wall A", "Beam -1", "Roof 0", "Crumble 4" })
            {
                var s = SolidCatalog.MatchPiece(name);
                Assert.IsNotNull(s, name + " no está en el catálogo");
                Assert.IsTrue(s.Solid, name + " debe ser sólido");
            }
            foreach (var name in new[] { "Rubble 3", "Road", "Stone", "Log", "Building 5" })
                Assert.IsFalse(SolidCatalog.MatchPiece(name).Solid, name);
        }

        [Test]
        public void MatchPiece_PrefersTheLongestPrefix()
        {
            Assert.AreEqual("Fire Barrel", SolidCatalog.MatchPiece("Fire Barrel 1").Name);
            Assert.AreEqual("Back Wall", SolidCatalog.MatchPiece("Back Wall B").Name);
            Assert.IsNull(SolidCatalog.MatchPiece("Zzz"));
        }

        [Test]
        public void ExactNames_DoNotCatchLookalikeContainers()
        {
            Assert.IsNull(SolidCatalog.MatchPiece("GroundDetails"), "el contenedor de maleza y manchas no es el piso");
            Assert.IsNull(SolidCatalog.MatchPiece("GroundFog 0"), "ni la niebla");
            Assert.IsTrue(SolidCatalog.MatchPiece("Ground").Solid);
            Assert.IsFalse(SolidCatalog.MatchPiece("Walkable front_door").Solid, "los volúmenes de NavMesh no son sólidos");
        }

        [Test]
        public void EveryHouseBoxKind_IsSolidWithABoxCollider()
        {
            foreach (HouseBoxKind k in Enum.GetValues(typeof(HouseBoxKind))) Assert.AreEqual(ColliderShape.Box, SolidCatalog.ShapeForHouse(k), k.ToString());
            var plan = HousePlanner.Plan(HouseLayoutData.Default());
            Assert.IsTrue(plan.Boxes.All(b => b.Collider));
        }
    }
}
