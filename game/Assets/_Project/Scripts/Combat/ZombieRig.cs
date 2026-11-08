using UnityEngine;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Combat
{
    /// <summary>
    /// Cuerpo del zombie armado con primitivas articuladas (pelvis, torso, cabeza, 2 brazos, 2 piernas). Cada parte cuelga de un pivote para poder
    /// animarla sin huesos, y cada zona de impacto es un collider simple en la capa Hitbox hijo de su pivote: sigue a la animación.
    /// Para cambiarlo por un modelo de Quaternius basta crear un rig con las mismas partes (ver docs/UNITY_SETUP.md); el resto del código solo usa esta clase.
    /// </summary>
    public class ZombieRig : IZombieRig
    {
        public Transform Visual { get; private set; }
        public Transform Spine, HeadPivot, ArmL, ArmR, LegL, LegR;
        public GameObject WeakPoint;   // núcleo brillante en el pecho de los jefes con punto débil; solo existe como hitbox mientras está expuesto
        public Renderer[] Renderers;
        public Hitbox[] Hitboxes { get; private set; }
        private Renderer[] _skin, _cloth;
        private Material _skinMat, _clothMat;
        private float _baseScale = 1f;

        public static ZombieRig Build(Transform root, Health owner)
        {
            var r = new ZombieRig();
            r.Visual = new GameObject("Visual").transform;
            r.Visual.SetParent(root, false);
            Material m0 = Prim.GetMaterial(new Color(0.45f, 0.55f, 0.4f));
            Material c0 = Prim.GetMaterial(new Color(0.25f, 0.25f, 0.28f));

            var skin = new System.Collections.Generic.List<Renderer>();
            var cloth = new System.Collections.Generic.List<Renderer>();
            var boxes = new System.Collections.Generic.List<Hitbox>();

            Transform Pivot(string n, Transform parent, Vector3 pos) { var t = new GameObject(n).transform; t.SetParent(parent, false); t.localPosition = pos; return t; }
            Renderer Part(PrimitiveType type, string n, Transform parent, Vector3 pos, Vector3 scale, Material m, System.Collections.Generic.List<Renderer> list)
            {
                var go = Prim.Create(type, n, pos, scale, m, parent, false);
                go.layer = 2;
                var rend = go.GetComponent<Renderer>();
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 24 zombies x 7 partes con sombra dinámica costarían demasiado
                list.Add(rend);
                return rend;
            }
            Hitbox Box(string n, Transform parent, HitZone zone, Collider c)
            {
                var hb = c.gameObject.AddComponent<Hitbox>();
                hb.Zone = zone; hb.Owner = owner;
                c.gameObject.name = n;
                c.gameObject.layer = HitboxLayer.Index;
                c.isTrigger = false;
                boxes.Add(hb);
                return hb;
            }
            Collider Capsule(Transform parent, Vector3 center, float radius, float height)
            {
                var go = new GameObject("hb"); go.transform.SetParent(parent, false);
                var c = go.AddComponent<CapsuleCollider>(); c.center = center; c.radius = radius; c.height = height; c.direction = 1;
                return c;
            }

            // piernas: pivote en la cadera
            r.LegL = Pivot("LegL", r.Visual, new Vector3(-0.13f, 0.9f, 0f));
            r.LegR = Pivot("LegR", r.Visual, new Vector3(0.13f, 0.9f, 0f));
            foreach (var leg in new[] { r.LegL, r.LegR })
            {
                Part(PrimitiveType.Capsule, "mesh", leg, new Vector3(0f, -0.43f, 0f), new Vector3(0.2f, 0.43f, 0.2f), c0, cloth);
                Box("Hitbox_Legs", leg, HitZone.Legs, Capsule(leg, new Vector3(0f, -0.45f, 0f), 0.15f, 0.95f));
            }
            Part(PrimitiveType.Cube, "pelvis", r.Visual, new Vector3(0f, 0.92f, 0f), new Vector3(0.38f, 0.2f, 0.24f), c0, cloth);

            // tronco, cabeza y brazos cuelgan de la columna (se inclina al animar)
            r.Spine = Pivot("Spine", r.Visual, new Vector3(0f, 0.95f, 0f));
            Part(PrimitiveType.Cube, "torso", r.Spine, new Vector3(0f, 0.32f, 0f), new Vector3(0.46f, 0.62f, 0.26f), c0, cloth);
            Box("Hitbox_Torso", r.Spine, HitZone.Torso, Capsule(r.Spine, new Vector3(0f, 0.32f, 0f), 0.27f, 0.68f));

            r.HeadPivot = Pivot("Head", r.Spine, new Vector3(0f, 0.68f, 0f));
            Part(PrimitiveType.Sphere, "mesh", r.HeadPivot, new Vector3(0f, 0.16f, 0f), new Vector3(0.28f, 0.3f, 0.28f), m0, skin);
            var hs = new GameObject("hb"); hs.transform.SetParent(r.HeadPivot, false);
            var sc = hs.AddComponent<SphereCollider>(); sc.center = new Vector3(0f, 0.16f, 0f); sc.radius = 0.19f;
            Box("Hitbox_Head", r.HeadPivot, HitZone.Head, sc);

            r.ArmL = Pivot("ArmL", r.Spine, new Vector3(-0.31f, 0.55f, 0f));
            r.ArmR = Pivot("ArmR", r.Spine, new Vector3(0.31f, 0.55f, 0f));
            foreach (var arm in new[] { r.ArmL, r.ArmR })
            {
                Part(PrimitiveType.Capsule, "mesh", arm, new Vector3(0f, -0.28f, 0f), new Vector3(0.14f, 0.28f, 0.14f), m0, skin);
                Box("Hitbox_Arms", arm, HitZone.Arms, Capsule(arm, new Vector3(0f, -0.28f, 0f), 0.11f, 0.62f));
            }

            var wp = Prim.Create(PrimitiveType.Sphere, "WeakPoint", new Vector3(0f, 0.38f, 0.17f), Vector3.one * 0.2f, Prim.GetEmissive(new Color(1f, 0.5f, 0.08f), 4f), r.Spine, false);
            wp.layer = HitboxLayer.Index;
            wp.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var wpc = wp.AddComponent<SphereCollider>(); wpc.radius = 0.8f;   // en espacio local: 0.8 x 0.2 = 0.16 m
            var wph = wp.AddComponent<Hitbox>(); wph.Zone = HitZone.WeakPoint; wph.Owner = owner;
            wp.SetActive(false);
            r.WeakPoint = wp;

            r._skin = skin.ToArray(); r._cloth = cloth.ToArray();
            var all = new System.Collections.Generic.List<Renderer>(skin); all.AddRange(cloth);
            r.Renderers = all.ToArray();
            r.Hitboxes = boxes.ToArray();
            return r;
        }

        /// <summary>Pinta la piel y la ropa con el color del tipo (los materiales se comparten por color: sigue habiendo pocos).</summary>
        public void SetColor(Color c)
        {
            _skinMat = Prim.GetMaterial(c);
            _clothMat = Prim.GetMaterial(Color.Lerp(c, new Color(0.18f, 0.18f, 0.22f), 0.7f));
            foreach (var r in _skin) r.sharedMaterial = _skinMat;
            foreach (var r in _cloth) r.sharedMaterial = _clothMat;
        }

        public void SetFlash(bool on, Material flash)
        {
            for (int i = 0; i < _skin.Length; i++) _skin[i].sharedMaterial = on ? flash : _skinMat;
            for (int i = 0; i < _cloth.Length; i++) _cloth[i].sharedMaterial = on ? flash : _clothMat;
        }

        // ---- IZombieRig (cuerpo antiguo de primitivas: lo usan los jefes hasta el bloque D)
        public void Setup(ZombieTypeData type, int seed, bool emerge)
        {
            _baseScale = type.scale;
            Visual.localScale = Vector3.one * type.scale; Visual.localPosition = Vector3.zero; Visual.localRotation = Quaternion.identity;
            HeadPivot.localScale = Vector3.one;
            SetColor(new Color(type.color[0], type.color[1], type.color[2]));
        }

        public void Animate(in ZombieAnimState s)
        {
            if (s.DistanceToCamera >= 70f) return;   // lejos la animación se salta: nadie la ve
            Pose(s.WalkPhase, Mathf.Clamp01(s.MoveAmount), Mathf.Max(0f, s.AttackT), s.Stunned ? 1f : 0f, s.Time, s.Chasing ? 1f : 0f);
        }

        public void OnHit(HitZone zone, Vector3 dirWorld, float strength, float healthFraction) { }
        public void OnDeath(HitZone lastZone, Vector3 dirWorld, bool headshot) { if (headshot) HeadPivot.localScale = Vector3.zero; }
        public void SetFuse(float inflate, float glow, float shake) { Visual.localScale = Vector3.one * _baseScale * inflate; }
        public bool Emerging => false;
        public bool ConsumeImpact() => false;

        public void SetWeakPoint(bool on) { if (WeakPoint != null) WeakPoint.SetActive(on); }

        public void SetHitboxesEnabled(bool on) { foreach (var h in Hitboxes) { var c = h.GetComponent<Collider>(); if (c != null) c.enabled = on; } }

        /// <summary>
        /// Pose procedural. walk: fase del paso; amount 0..1 intensidad; attack 0..1 (levanta y baja los brazos); stagger 0..1 (se echa atrás);
        /// idleSway da un balanceo lento al estar parado.
        /// </summary>
        public void Pose(float walkPhase, float amount, float attack, float stagger, float time, float lunge = 0f)
        {
            float swing = Mathf.Sin(walkPhase) * 38f * amount;
            LegL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            LegR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            float sway = Mathf.Sin(time * 1.3f) * 3f;
            // brazos hacia delante (postura de zombi) con un poco de vaivén; al atacar suben y golpean
            float reach = -78f + Mathf.Sin(walkPhase + 1f) * 10f * amount + sway;
            float hit = attack < 0.5f ? Mathf.Lerp(0f, -50f, attack * 2f) : Mathf.Lerp(-50f, 55f, (attack - 0.5f) * 2f);
            ArmL.localRotation = Quaternion.Euler(reach + hit, 0f, -4f);
            ArmR.localRotation = Quaternion.Euler(reach + hit * 0.9f, 0f, 4f);
            Spine.localRotation = Quaternion.Euler(8f * amount + 14f * lunge - 28f * stagger, Mathf.Sin(walkPhase) * 6f * amount, sway * 0.5f);
            HeadPivot.localRotation = Quaternion.Euler(Mathf.Sin(time * 0.9f) * 5f - 14f * stagger, 0f, Mathf.Sin(time * 0.7f) * 6f);
        }
    }
}
