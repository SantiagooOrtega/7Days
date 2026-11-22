using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SevenDays.Tests
{
    /// <summary>
    /// La build no sale en magenta: cada shader que el código pide con Shader.Find (o Prim.LitShader) entra en la build, por Always Included Shaders o por la
    /// colección Resources/Look/ShadersUsados (Graphics > Preloaded Shaders), con las variantes que usa el juego; la niebla y el post-procesado no se recortan.
    /// Y todo OnGUI del juego respeta la puerta del HUD (nada encima de la intro o el epílogo).
    /// </summary>
    public class BuildShadersTests
    {
        private static readonly System.Type PrimT = System.Reflection.Assembly.Load("Assembly-CSharp").GetType("SevenDays.Core.Prim", true);
        private static string LitShader => (string)PrimT.GetField("LitShader").GetValue(null);
        private static string Scripts => Path.Combine(Application.dataPath, "_Project/Scripts");

        private static HashSet<string> ShadersInBuild(out ShaderVariantCollection svc)
        {
            var names = new HashSet<string>();
            var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var always = gs.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < always.arraySize; i++) { var s = always.GetArrayElementAtIndex(i).objectReferenceValue as Shader; if (s != null) names.Add(s.name); }
            var pre = gs.FindProperty("m_PreloadedShaders"); svc = null;
            for (int i = 0; i < pre.arraySize; i++)
            {
                var c = pre.GetArrayElementAtIndex(i).objectReferenceValue as ShaderVariantCollection; if (c == null) continue; svc = c;
                var so = new SerializedObject(c).FindProperty("m_Shaders");
                for (int j = 0; j < so.arraySize; j++) { var s = so.GetArrayElementAtIndex(j).FindPropertyRelative("first").objectReferenceValue as Shader; if (s != null) names.Add(s.name); }
            }
            return names;
        }

        [Test]
        public void EveryShaderFoundByCode_IsInTheBuild()
        {
            var inBuild = ShadersInBuild(out var svc);
            Assert.IsNotNull(svc, "colección de variantes en Graphics > Preloaded Shaders");
            Assert.AreEqual("Assets/_Project/Resources/Look/ShadersUsados.shadervariants", AssetDatabase.GetAssetPath(svc));
            var wanted = new HashSet<string> { LitShader, "SevenDays/UI Additive" };
            foreach (var f in Directory.GetFiles(Scripts, "*.cs", SearchOption.AllDirectories))
                foreach (Match m in Regex.Matches(File.ReadAllText(f), "Shader\\.Find\\(\"([^\"]+)\"\\)")) wanted.Add(m.Groups[1].Value);
            CollectionAssert.IsNotEmpty(wanted);
            foreach (var w in wanted)
            {
                Assert.IsNotNull(Shader.Find(w), "existe: " + w);
                Assert.IsTrue(inBuild.Contains(w), "'" + w + "' no entra en la build (ni Always Included ni en ShadersUsados)");
            }
            // las variantes que usa el juego (relieve, emisión, transparencia, recorte)
            var lit = Shader.Find(LitShader);
            foreach (var kw in new[] { new string[0], new[] { "_EMISSION" }, new[] { "_NORMALMAP" }, new[] { "_SURFACE_TYPE_TRANSPARENT" }, new[] { "_ALPHATEST_ON" },
                                       new[] { "_EMISSION", "_METALLICSPECGLOSSMAP", "_NORMALMAP", "_OCCLUSIONMAP" } })
                Assert.IsTrue(svc.Contains(new ShaderVariantCollection.ShaderVariant(lit, UnityEngine.Rendering.PassType.ScriptableRenderPipeline, kw)), "variante URP Lit " + string.Join(" ", kw));
        }

        [Test]
        public void FogAndPostProcessing_AreNotStripped()
        {
            var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            Assert.AreEqual(1, gs.FindProperty("m_FogStripping").intValue, "niebla: modo personalizado (el juego la enciende por código; en automático se recortaba)");
            Assert.IsTrue(gs.FindProperty("m_FogKeepExp2").boolValue, "se conserva la niebla exponencial al cuadrado (la que usa el juego)");
            string urp = File.ReadAllText(Path.Combine(Application.dataPath, "Settings/UniversalRenderPipelineGlobalSettings.asset"));
            StringAssert.DoesNotContain("m_StripUnusedPostProcessingVariants: 1", urp, "el post-procesado se crea por código: no se recortan sus variantes");
        }

        [Test]
        public void PrimTemplate_IsUrpLit_NotTheBuiltinDefault()
        {
            string prim = File.ReadAllText(Path.Combine(Scripts, "Core/Prim.cs"));
            StringAssert.DoesNotContain("probe.GetComponent<Renderer>().sharedMaterial", prim, "en la build el material de un primitivo es el Standard (magenta en URP)");
            var m = (Material)PrimT.GetMethod("GetMaterial", new[] { typeof(Color), typeof(bool) }).Invoke(null, new object[] { Color.red, false });
            Assert.AreEqual(LitShader, m.shader.name);
        }

        [Test]
        public void EveryGameOnGui_RespectsTheHudGate()
        {
            var bad = new List<string>();
            foreach (var f in Directory.GetFiles(Scripts, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(f), src = File.ReadAllText(f);
                if (name == "IntroPlayer.cs" || name == "LoginScreen.cs" || name == "FrontEnd.cs") continue;   // la intro se dibuja a sí misma; login y menú no están en partida
                var m = Regex.Match(src, "void OnGUI\\(\\)\\s*\\{\\s*if \\(SevenDays\\.Core\\.HudGate\\.Hidden\\) return;");
                if (src.Contains("void OnGUI()") && !m.Success) bad.Add(name);
            }
            CollectionAssert.IsEmpty(bad, "OnGUI que se dibujaría encima de la intro");
        }
    }
}
