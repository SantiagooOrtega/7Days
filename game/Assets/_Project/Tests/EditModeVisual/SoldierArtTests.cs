using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Tests.Visual
{
    /// <summary>El soldado vestido de verdad (mallas y materiales reales) para cada variante: presupuesto, materiales, texturas generadas y comprimidas.</summary>
    public class SoldierArtTests
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown() { foreach (var g in _created) if (g != null) UnityEngine.Object.DestroyImmediate(g); _created.Clear(); }

        public static IEnumerable<string> Ids() { foreach (var v in CharacterVariants.Defaults()) yield return v.id; }

        private Component Soldier(string id)
        {
            var v = CharacterVariants.Defaults().Find(x => x.id == id);
            var go = new GameObject("Soldado " + id);
            _created.Add(go);
            var rig = (Component)go.AddComponent(T("SevenDays.Animation.ProceduralRig"));
            rig.GetType().GetMethod("Build").Invoke(rig, new object[] { RigProportions.Soldier() });
            T("SevenDays.Animation.RigVisuals").GetMethod("BuildSoldier", new[] { rig.GetType(), typeof(CharacterVariantData) }).Invoke(null, new object[] { rig, v });
            return rig;
        }

        [TestCaseSource(nameof(Ids))]
        public void Soldier_WithinBudget_FourMaterialsMax_NoBackpack(string id)
        {
            var rig = Soldier(id);
            int tris = 0;
            var mats = new HashSet<Material>();
            foreach (var mf in rig.GetComponentsInChildren<MeshFilter>())
            {
                tris += (int)mf.sharedMesh.GetIndexCount(0) / 3;
                mats.Add(mf.GetComponent<Renderer>().sharedMaterial);
                StringAssert.DoesNotContain("mochila", mf.name.ToLowerInvariant());
                // UV y tangentes válidas en la malla de Unity
                foreach (var t in mf.sharedMesh.tangents) Assert.IsTrue(Mathf.Abs(t.w) == 1f, mf.name + ": signo de tangente");
            }
            Debug.Log("[Soldado] " + id + ": " + tris + " triángulos, " + mats.Count + " materiales");
            Assert.LessOrEqual(tris, 15000, id + ": presupuesto del jugador");
            Assert.LessOrEqual(mats.Count, 4, id + ": 3-4 materiales");
        }

        [Test]
        public void Textures_AreGenerated_Compressed_AndReported()
        {
            var art = T("SevenDays.Animation.CharacterArt");
            var w = System.Diagnostics.Stopwatch.StartNew();
            var sb = new System.Text.StringBuilder("[Texturas de personaje]\n");
            foreach (var v in CharacterVariants.Defaults())
            {
                var set = art.GetMethod("For").Invoke(null, new object[] { v });
                art.GetMethod("WaitAll").Invoke(null, null);
                foreach (var slot in new[] { "Uniform", "Gear", "Helmet" })
                {
                    var m = (Material)set.GetType().GetField(slot).GetValue(set);
                    var albedo = m.GetTexture("_BaseMap") as Texture2D;
                    var normal = m.GetTexture("_BumpMap") as Texture2D;
                    Assert.IsNotNull(albedo, v.id + " " + slot + ": sin color");
                    Assert.IsNotNull(normal, v.id + " " + slot + ": sin normales");
                    Assert.IsNotNull(m.GetTexture("_MetallicGlossMap"), v.id + " " + slot + ": sin máscara");
                    Assert.Greater(albedo.mipmapCount, 1, "con mipmaps");
                    if (SystemInfo.SupportsTextureFormat(TextureFormat.DXT5)) Assert.AreEqual(TextureFormat.DXT5, albedo.format, "color comprimido a BC");
                    Assert.AreEqual(TextureFormat.RGBA32, normal.format, "normales sin comprimir");
                }
                sb.Append(v.id).Append(": ").Append(((long)set.GetType().GetField("TextureBytes").GetValue(set) / (1024f * 1024f)).ToString("0.0")).Append(" MB\n");
            }
            sb.Append("tiempo total (4 variantes, 12 atlas): ").Append(w.ElapsedMilliseconds).Append(" ms");
            Debug.Log(sb.ToString());
        }
    }
}
