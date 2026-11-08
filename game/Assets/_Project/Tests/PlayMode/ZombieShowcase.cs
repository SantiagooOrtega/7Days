using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SevenDays.Procedural;

namespace SevenDays.Tests.PlayMode
{
    /// <summary>
    /// Capturas de los zombis reales renderizados con URP (no es una prueba de aprobado/suspenso): un zombi de cada tipo de frente y de lado, tres variantes del común y la capa de
    /// daño al 100/50/25 %. Guarda PNG en la carpeta de la variable de entorno ZOMBIE_SHOTS. Se ejecuta a mano (es [Explicit]).
    /// </summary>
    [Explicit, Category("Rendimiento")]
    public class ZombieShowcase
    {
        private static readonly Assembly Game = Assembly.Load("Assembly-CSharp");
        private static Type T(string n) => Game.GetType(n, true);
        private static string Dir => Environment.GetEnvironmentVariable("ZOMBIE_SHOTS") ?? Path.Combine(Application.dataPath, "..", "shots");

        private GameObject _stage; private Camera _cam; private RenderTexture _rt;

        private void Stage()
        {
            _stage = new GameObject("Showcase");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(_stage.transform); ground.transform.position = new Vector3(0, -0.5f, 0); ground.transform.localScale = new Vector3(60, 1, 60);
            ground.GetComponent<Renderer>().sharedMaterial.color = new Color(0.32f, 0.31f, 0.3f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(_stage.transform); wall.transform.position = new Vector3(0, 3, -4); wall.transform.localScale = new Vector3(30, 8, 0.5f);
            wall.GetComponent<Renderer>().sharedMaterial.color = new Color(0.2f, 0.2f, 0.21f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.15f; sun.color = new Color(1f, 0.95f, 0.88f); sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, 145f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.56f);
            var camGo = new GameObject("ShotCam"); _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color(0.16f, 0.17f, 0.19f); _cam.fieldOfView = 28f; _cam.nearClipPlane = 0.1f;
            _rt = new RenderTexture(900, 1100, 24, RenderTextureFormat.ARGB32); _cam.targetTexture = _rt;
            Directory.CreateDirectory(Dir);
        }

        private object Make(string typeId, int seed, int bucket, Vector3 pos, float yaw, out GameObject root)
        {
            root = new GameObject("Zombie " + typeId); root.transform.SetParent(_stage.transform);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var health = root.AddComponent(T("SevenDays.Core.Health"));
            var type = ZombieCatalog.Get(typeId);
            var bt = T("SevenDays.Combat.ZombieBody");
            var obj = bt.GetMethod("Build").Invoke(null, new object[] { root.transform, health, type, bucket });
            bt.GetMethod("Setup").Invoke(obj, new object[] { type, seed, false });
            return obj;
        }

        private static object State(float dt, bool chasing, bool stunned, float attackT, ZombieAttackKind kind)
        {
            var st = Activator.CreateInstance(T("SevenDays.Combat.ZombieAnimState"));
            void Set(string f, object v) => st.GetType().GetField(f).SetValue(st, v);
            Set("Dt", dt); Set("Chasing", chasing); Set("Stunned", stunned); Set("AttackT", attackT); Set("AttackKind", kind); Set("DistanceToCamera", 4f); Set("Time", Time.time);
            return st;
        }

        private static void Animate(object body, object st) => body.GetType().GetMethod("Animate").Invoke(body, new[] { st });

        private IEnumerator Shoot(string name, Vector3 target, Vector3 from)
        {
            yield return null; yield return null;   // el skinning de las mallas se calcula una vez por frame: sin esto la captura sale con la pose anterior
            _cam.transform.position = from; _cam.transform.LookAt(target);
            _cam.Render();
            RenderTexture.active = _rt;
            var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null; UnityEngine.Object.Destroy(tex);
        }

        private IEnumerator Settle(object body, int frames, bool chasing = false, float attackT = -1f, ZombieAttackKind kind = ZombieAttackKind.Swipe)
        {
            for (int i = 0; i < frames; i++) { Animate(body, State(Time.deltaTime > 0 ? Time.deltaTime : 1f / 60f, chasing, false, attackT, kind)); yield return null; }
        }

        [UnityTest]
        public IEnumerator Types_FrontAndSide()
        {
            Stage();
            T("SevenDays.Animation.ZombieArt").GetMethod("PrewarmAll").Invoke(null, null); T("SevenDays.Animation.ZombieArt").GetMethod("WaitAll").Invoke(null, null);
            foreach (var id in ZombieAnatomies.Types)
            {
                var body = Make(id, 4, 1, Vector3.zero, 0f, out var root);
                yield return Settle(body, 45);
                float h = ZombieAnatomies.For(id).Proportions.height;
                float dist = h * 3.1f;
                var lookAt = new Vector3(0, h * 0.5f, 0);
                yield return Shoot(id + "_front", lookAt, new Vector3(0, h * 0.55f, dist));
                yield return Shoot(id + "_side", lookAt, new Vector3(dist, h * 0.55f, 0));
                yield return Shoot(id + "_34", lookAt, new Vector3(dist * 0.7f, h * 0.6f, dist * 0.7f));
                UnityEngine.Object.Destroy(root);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Types_Chasing()
        {
            Stage();
            T("SevenDays.Animation.ZombieArt").GetMethod("PrewarmAll").Invoke(null, null); T("SevenDays.Animation.ZombieArt").GetMethod("WaitAll").Invoke(null, null);
            foreach (var id in ZombieAnatomies.Types)
            {
                var body = Make(id, 4, 1, new Vector3(0, 0, -6f), 0f, out var root);
                float speed = id == ZombieIds.Runner ? 5.2f : (id == ZombieIds.Tank ? 1.5f : 2.4f);
                for (int i = 0; i < 100; i++)
                {
                    root.transform.position += root.transform.forward * speed / 60f;
                    var st = State(1f / 60f, true, false, -1f, ZombieAttackKind.Swipe); st.GetType().GetField("MoveAmount").SetValue(st, Mathf.Clamp01(speed / 5.2f));
                    Animate(body, st); yield return null;
                }
                float h = ZombieAnatomies.For(id).Proportions.height;
                var p = root.transform.position;
                yield return Shoot(id + "_chase", p + new Vector3(0, h * 0.5f, 0), p + new Vector3(h * 1.1f, h * 0.65f, h * 1.5f));
                UnityEngine.Object.Destroy(root);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Common_ThreeVariants_AndDamageLayer()
        {
            Stage();
            T("SevenDays.Animation.ZombieArt").GetMethod("PrewarmAll").Invoke(null, null); T("SevenDays.Animation.ZombieArt").GetMethod("WaitAll").Invoke(null, null);
            var variants = ZombieVariants.ForType(ZombieVariants.Defaults(), ZombieIds.Common);
            for (int seed = 1; seed <= 12 && seed < 60; seed++)
            {
                var body = Make(ZombieIds.Common, seed, 1, Vector3.zero, 0f, out var root);
                var v = (ZombieVariantData)body.GetType().GetProperty("Variant").GetValue(body);
                yield return Settle(body, 30);
                yield return Shoot("variant_" + seed + "_" + v.id, new Vector3(0, 0.9f, 0), new Vector3(0, 1.0f, 5.4f));
                UnityEngine.Object.Destroy(root);
                yield return null;
            }
            // capa de daño: 100 / 50 / 25 %
            var z = Make(ZombieIds.Common, 3, 1, Vector3.zero, 0f, out var zr);
            yield return Settle(z, 30);
            var hit = z.GetType().GetMethod("OnHit");
            yield return Shoot("damage_100", new Vector3(0, 0.9f, 0), new Vector3(0, 1.0f, 5.4f));
            hit.Invoke(z, new object[] { HitZone.Torso, -zr.transform.forward, 1f, 0.74f }); hit.Invoke(z, new object[] { HitZone.Head, -zr.transform.forward, 1f, 0.49f });
            yield return Settle(z, 40);
            yield return Shoot("damage_50", new Vector3(0, 0.9f, 0), new Vector3(0, 1.0f, 5.4f));
            hit.Invoke(z, new object[] { HitZone.Arms, -zr.transform.forward, 1f, 0.24f });
            yield return Settle(z, 40);
            yield return Shoot("damage_25", new Vector3(0, 0.9f, 0), new Vector3(0, 1.0f, 5.4f));
        }

        [UnityTest]
        public IEnumerator Attacks_AndDeath()
        {
            Stage();
            T("SevenDays.Animation.ZombieArt").GetMethod("PrewarmAll").Invoke(null, null); T("SevenDays.Animation.ZombieArt").GetMethod("WaitAll").Invoke(null, null);
            var body = Make(ZombieIds.Common, 4, 1, Vector3.zero, 0f, out var root);
            yield return Settle(body, 30, true);
            int n = 0;
            foreach (ZombieAttackKind k in Enum.GetValues(typeof(ZombieAttackKind)))
            {
                yield return Settle(body, 20, true);
                var marks = new[] { 0.3f, 0.5f, 0.75f }; int mi = 0;
                for (int f = 0; f <= 42 && mi < marks.Length; f++)
                {
                    float t = f / 42f;
                    Animate(body, State(1f / 60f, true, false, t, k));
                    if (t >= marks[mi]) { yield return Shoot("attack_" + k + "_" + (int)(marks[mi] * 100), new Vector3(0, 0.9f, 0.3f), new Vector3(3.8f, 1.2f, 3.8f)); mi++; }
                    else yield return null;
                }
            }
            foreach (var zone in new[] { HitZone.Head, HitZone.Torso, HitZone.Legs })
            {
                var b2 = Make(ZombieIds.Common, 8, 1, Vector3.zero, 0f, out var r2);
                yield return Settle(b2, 20, true);
                b2.GetType().GetMethod("OnDeath").Invoke(b2, new object[] { zone, zone == HitZone.Torso ? r2.transform.forward : -r2.transform.forward, zone == HitZone.Head });
                var dead = State(1f / 60f, false, false, -1f, ZombieAttackKind.Swipe); dead.GetType().GetField("Dead").SetValue(dead, true);
                for (int i = 0; i < 120; i++) Animate(b2, dead);
                yield return Shoot("death_" + zone, new Vector3(0, 0.3f, 0), new Vector3(3.5f, 2.4f, 3.5f));
                UnityEngine.Object.Destroy(r2);
                yield return null;
            }
        }
    }
}
