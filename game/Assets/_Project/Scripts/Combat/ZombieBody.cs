using System.Collections.Generic;
using UnityEngine;
using SevenDays.Animation;
using SevenDays.Core;
using SevenDays.Procedural;

namespace SevenDays.Combat
{
    /// <summary>
    /// Cuerpo procedural del zombi: el mismo esqueleto y la misma animación por capas del soldado (ProceduralRig) con las proporciones del tipo, una sola malla con pesos
    /// (SkinnedMeshRenderer: piel y ropa, 2 llamadas de dibujo) en 3 niveles de detalle, hitboxes pegados a los huesos, decals de heridas y la capa de daño progresivo.
    /// Encima del rig añade lo propio del zombi: guardia de brazos por tipo, ataques con anticipación y seguimiento, cojera, cabeza que cuelga, reacciones por zona,
    /// aturdimiento, emerger del suelo y morir de espaldas, hacia delante o de rodillas (poses ajustadas al suelo con la malla real).
    /// </summary>
    public sealed class ZombieBody : IZombieRig
    {
        public Transform Visual { get; private set; }
        public Hitbox[] Hitboxes { get; private set; }
        public ProceduralRig Rig { get; private set; }
        public SkinnedMeshRenderer Renderer { get; private set; }
        public ZombieAnatomy Anatomy { get; private set; }
        public ZombieVariantData Variant { get; private set; }
        public ZombieIndividual Individual { get; private set; }
        public ZombieMeshBundle Bundle { get; private set; }
        public int Lod { get; private set; }
        public int Bucket { get; private set; }
        public string TypeId { get; private set; }
        public bool Emerging => _emerge < EmergeSeconds;
        public bool Dead => _dead;
        public int ActiveDecals { get { int n = 0; foreach (var d in _decals) if (d != null && d.enabled) n++; return n; } }

        public const float EmergeSeconds = 1.3f;

        private Transform _root;
        private Transform[] _bones;
        private Transform _belly;
        // jefes: mandíbula doble y núcleo con costillas del Devorador, punto débil, furia y vapor
        private Transform _jawL, _jawR, _core, _ribL, _ribR;
        private Hitbox _weak;
        private bool _weakOn;
        private float _weakOpen, _jaw, _lastEmissionOpen = -1f;
        private int _fury;
        private ParticleSystem _steam;
        private Health _owner;
        private Renderer[] _decals;                     // un MeshRenderer por herida posible (se enciende al revelarse)
        private bool[] _decalWanted;
        private WoundPatch[] _decalPatch;
        private MaterialPropertyBlock _skinBlock, _clothBlock;
        private Material _skinMat, _clothMat;
        private bool _flashing;
        private float _pendingDt, _time, _emerge = EmergeSeconds, _inflate = 1f, _glow, _shake, _impactUntil;
        private int _frameOffset;
        private bool _dead, _impactPending;
        private string _lastTypeId;
        private ZombieTypeData _type;
        private int _attackIndex = -1;
        private float _lastAttackT = -1f;
        private ZombieMotionFrame _frame;
        private ZombieAnimState _state;
        private readonly ZombieDamageLayer _damage = new ZombieDamageLayer();
        private readonly List<KeyValuePair<HitZone, int>> _revealed = new List<KeyValuePair<HitZone, int>>(4);
        // muelles de las reacciones (se les da un golpe y vuelven a cero)
        private CriticalSpring _fTorsoX, _fTorsoY, _fTorsoZ, _fHeadX, _fHeadY, _fHeadZ, _fPelvisY, _fPelvisRoll, _fArmL, _fArmR;
        // muerte
        private Quaternion[] _startRot; private Vector3 _startHips; private ZombiePose _target; private float _deathT, _deathDur; private ZombieDeathForm _form;
        private static Camera _cam; private static int _camFrame = -1;
        private V3 _armGuardL, _armGuardR;
        private float _armLen;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), EmissionId = Shader.PropertyToID("_EmissionColor");

        // ================================================================== construcción
        public static ZombieBody Build(Transform root, Health owner, ZombieTypeData type, int bucket)
        {
            var b = new ZombieBody { _root = root, _owner = owner, TypeId = type.id, Bucket = bucket, _type = type };
            b.Anatomy = ZombieAnatomies.For(type.id, bucket);
            ZombieMeshes.Prewarm(type.id, bucket);
            b.BuildRig();
            return b;
        }

        private void BuildRig()
        {
            Visual = new GameObject("Visual").transform;
            Visual.SetParent(_root, false);
            Rig = Visual.gameObject.AddComponent<ProceduralRig>();
            Rig.ManualTick = true;
            Rig.GroundMask = ~((1 << 2) | HitboxLayer.Mask);
            Rig.Build(Anatomy.Proportions);
            Rig.Smoothing.handSpeed = 9f; Rig.Smoothing.hands = 0.07f; Rig.Smoothing.weights = 0.12f;
            Rig.BeforeArms += OnBeforeArms;
            // huesos extra (vientre del explosivo): se heredan de su hueso del esqueleto
            var first = ZombieMeshes.ForType(TypeId)[0];
            var bundle = ZombieMeshes.Get(TypeId, first.id, Bucket);
            int extras = bundle.Model.Extras.Count;
            _bones = new Transform[HumanoidRigSpec.BoneCount + extras];
            for (int i = 0; i < HumanoidRigSpec.BoneCount; i++) _bones[i] = Rig.Bone((BoneId)i);
            for (int e = 0; e < extras; e++)
            {
                var x = new GameObject(bundle.Model.Extras[e].Name).transform;
                x.SetParent(Rig.Bone(bundle.Model.Extras[e].Parent), false);
                var lp = bundle.Model.Extras[e].LocalPos;
                x.localPosition = new Vector3(lp.X, lp.Y, lp.Z);
                _bones[HumanoidRigSpec.BoneCount + e] = x;
                switch (bundle.Model.Extras[e].Name)
                {
                    case "Belly": _belly = x; break;
                    case ZombieBossBones.JawL: _jawL = x; break;
                    case ZombieBossBones.JawR: _jawR = x; break;
                    case ZombieBossBones.Core: _core = x; break;
                    case ZombieBossBones.RibL: _ribL = x; break;
                    case ZombieBossBones.RibR: _ribR = x; break;
                }
            }
            Renderer = Visual.gameObject.AddComponent<SkinnedMeshRenderer>();
            Renderer.bones = _bones; Renderer.rootBone = Visual;
            Renderer.sharedMesh = bundle.Lod[0];
            Renderer.localBounds = bundle.Bounds;
            Renderer.updateWhenOffscreen = false;
            Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            Renderer.gameObject.layer = 2;
            _skinMat = ZombieArt.Skin(TypeId); _clothMat = ZombieArt.Cloth;
            Renderer.sharedMaterials = new[] { _skinMat, _clothMat };
            _skinBlock = new MaterialPropertyBlock(); _clothBlock = new MaterialPropertyBlock();
            BuildHitboxes();
            BuildDecals(bundle);
            if (Anatomy.IsBoss)
            {
                float uc = Rig.Spec[(int)BoneId.UpperChest].Length;
                _steam = SevenDays.Look.Particles.CreateSteam(Rig.Bone(BoneId.UpperChest), new Vector3(0f, uc * 0.8f, -0.15f * Anatomy.Size), Anatomy.Size);
            }
            foreach (var t in Visual.GetComponentsInChildren<Transform>(true)) if (t.GetComponent<Hitbox>() == null) t.gameObject.layer = 2;
            _armLen = Rig.Spec[(int)BoneId.LeftUpperArm].Length + Rig.Spec[(int)BoneId.LeftLowerArm].Length;
            _frameOffset = Random.Range(0, 6);
        }

        private void BuildHitboxes()
        {
            var list = new List<Hitbox>();
            var spec = Rig.Spec; var pr = Anatomy.Proportions;
            float s = Anatomy.Size, k = pr.bulk, hs = pr.headSize * s;
            float L(BoneId id) => spec[(int)id].Length;
            Hitbox Add(Transform bone, HitZone zone, Collider c)
            {
                var hb = c.gameObject.AddComponent<Hitbox>();
                hb.Zone = zone; hb.Owner = _owner;
                c.gameObject.name = "Hitbox_" + zone;
                c.gameObject.layer = HitboxLayer.Index;
                c.isTrigger = false;
                list.Add(hb);
                return hb;
            }
            CapsuleCollider Capsule(Transform bone, Vector3 center, float radius, float height, int dir = 1)
            {
                var go = new GameObject("hb"); go.transform.SetParent(bone, false);
                var c = go.AddComponent<CapsuleCollider>(); c.center = center; c.radius = radius; c.height = Mathf.Max(height, radius * 2f); c.direction = dir;
                return c;
            }
            // cabeza: esfera sobre el cráneo
            var hgo = new GameObject("hb"); hgo.transform.SetParent(Rig.Bone(BoneId.Head), false);
            var sc = hgo.AddComponent<SphereCollider>(); sc.center = new Vector3(0f, 0.106f * hs, 0.01f * hs); sc.radius = 0.112f * hs;
            Add(Rig.Bone(BoneId.Head), HitZone.Head, sc);
            // torso: pecho y vientre en una cápsula sobre el pecho, y la pelvis aparte
            float tr = 0.155f * k * s * (1f - 0.1f * Anatomy.Emaciation) * (Anatomy.ScarPlates ? 1.15f : 1f) * (Anatomy.Boss == ZombieBossKind.Brute ? 1.12f : 1f);   // placas soldadas del Bruto
            float bottom = -L(BoneId.Spine) * 0.85f, top = L(BoneId.Chest) + L(BoneId.UpperChest) * 0.8f;
            Add(Rig.Bone(BoneId.Chest), HitZone.Torso, Capsule(Rig.Bone(BoneId.Chest), new Vector3(0f, (bottom + top) * 0.5f, 0.005f * s), tr, top - bottom));
            var pgo = new GameObject("hb"); pgo.transform.SetParent(Rig.Bone(BoneId.Hips), false);
            var ps = pgo.AddComponent<SphereCollider>(); ps.center = new Vector3(0f, 0.01f * s, 0f); ps.radius = 0.15f * k * s * (Anatomy.Boss == ZombieBossKind.Brute ? 1.12f : 1f);   // cadena de la cintura
            Add(Rig.Bone(BoneId.Hips), HitZone.Torso, ps);
            // cuello (cuenta como torso) y hombros: una cápsula horizontal sobre el pecho alto, del ancho de los hombros
            float neckLen = L(BoneId.Neck);
            Add(Rig.Bone(BoneId.Neck), HitZone.Torso, Capsule(Rig.Bone(BoneId.Neck), new Vector3(0f, neckLen * 0.5f, 0f), 0.058f * k * s, neckLen + 0.05f * s));
            float uc = L(BoneId.UpperChest);
            Add(Rig.Bone(BoneId.UpperChest), HitZone.Torso, Capsule(Rig.Bone(BoneId.UpperChest), new Vector3(0f, uc * 0.78f, 0f), 0.075f * s * Mathf.Max(0.9f, k), pr.shoulderWidth * 1.05f, 0));
            // depósito y bolsas de la espalda del Lanzador Colosal (cuentan como torso)
            if (Anatomy.Boss == ZombieBossKind.Launcher)
            {
                var bgo2 = new GameObject("hb"); bgo2.transform.SetParent(Rig.Bone(BoneId.UpperChest), false);
                var bb = bgo2.AddComponent<BoxCollider>(); bb.center = new Vector3(0f, uc * 0.45f, -(tr * 0.75f + 0.16f * s)); bb.size = new Vector3(0.55f * s, uc * 1.1f, 0.34f * s);
                Add(Rig.Bone(BoneId.UpperChest), HitZone.Torso, bb);
            }
            // vientre hinchado del explosivo: esfera sobre su hueso extra (crece con el inflado)
            if (_belly != null)
            {
                var bgo = new GameObject("hb"); bgo.transform.SetParent(_belly, false);
                var bs = bgo.AddComponent<SphereCollider>(); bs.radius = 0.215f * k * s * 1.05f;
                Add(_belly, HitZone.Torso, bs);
            }
            // brazos (más gruesos el grande del tanque) y piernas, cada segmento en su hueso
            float f = k * pr.limbThickness * s / SoldierModel.ReferenceKt * (1f - 0.2f * Anatomy.Emaciation);
            foreach (bool left in new[] { true, false })
            {
                float g = (Anatomy.BigArmScale > 1f && (Anatomy.BigArmSide < 0) == left) ? Anatomy.BigArmScale : 1f;
                BoneId ua = left ? BoneId.LeftUpperArm : BoneId.RightUpperArm, la = left ? BoneId.LeftLowerArm : BoneId.RightLowerArm;
                float lu = L(ua), ll = L(la), ex = (Anatomy.LongArmExtra > 0f && !left) ? Anatomy.LongArmExtra * s : 0f;
                // hombreras del Bruto y neumático y vendas del brazo grande del Lanzador: el brazo es más gordo por fuera
                float gear = Anatomy.Boss == ZombieBossKind.Brute ? 1.4f : (Anatomy.Boss == ZombieBossKind.Launcher && g > 1f ? 1.25f : 1f);
                Add(Rig.Bone(ua), HitZone.Arms, Capsule(Rig.Bone(ua), new Vector3(0f, -lu * 0.5f, 0f), 0.062f * f * g * gear, lu + 0.04f * f * g));
                float fs = Anatomy.ForearmScale;   // antebrazos-maza del Bruto
                Add(Rig.Bone(la), HitZone.Arms, Capsule(Rig.Bone(la), new Vector3(0f, -(ll + ex) * 0.5f - 0.01f * s, 0f), 0.05f * f * g * fs, ll + ex + 0.02f * s));
                // mano: esfera sobre el hueso de la mano (los dedos largos quedaban fuera del antebrazo)
                float hk = Mathf.Max(1f, g * 0.9f) * fs;
                var hgo2 = new GameObject("hb"); hgo2.transform.SetParent(Rig.Bone(left ? BoneId.LeftHand : BoneId.RightHand), false);
                var hs2 = hgo2.AddComponent<SphereCollider>(); hs2.center = new Vector3(0f, -(0.055f * s * hk + ex), 0f); hs2.radius = 0.07f * s * hk;
                Add(Rig.Bone(left ? BoneId.LeftHand : BoneId.RightHand), HitZone.Arms, hs2);
                BoneId ul = left ? BoneId.LeftUpperLeg : BoneId.RightUpperLeg, lo = left ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg;
                float th = L(ul), sh = L(lo) + L(left ? BoneId.LeftFoot : BoneId.RightFoot);
                Add(Rig.Bone(ul), HitZone.Legs, Capsule(Rig.Bone(ul), new Vector3(0f, -th * 0.5f, 0f), 0.085f * f, th + 0.02f));
                Add(Rig.Bone(lo), HitZone.Legs, Capsule(Rig.Bone(lo), new Vector3(0f, -L(lo) * 0.5f, 0f), 0.06f * f, L(lo) + 0.02f));
                // pie: caja sobre el hueso del pie (del talón a la punta)
                var fb = left ? BoneId.LeftFoot : BoneId.RightFoot; var tb = left ? BoneId.LeftToes : BoneId.RightToes;
                float drop = L(fb), ball = spec[(int)tb].LocalPosition.Z + 0.075f * s;
                var fgo = new GameObject("hb"); fgo.transform.SetParent(Rig.Bone(fb), false);
                var fbox = fgo.AddComponent<BoxCollider>(); fbox.center = new Vector3(0f, -drop * 0.45f, (ball - 0.07f * s) * 0.5f); fbox.size = new Vector3(0.12f * s * (0.8f + 0.2f * k), drop * 1.1f, ball + 0.07f * s);
                Add(Rig.Bone(fb), HitZone.Legs, fbox);
            }
            Hitboxes = list.ToArray();
            // punto débil del Devorador: esfera sobre el núcleo del pecho, del mismo tamaño que el del cuerpo antiguo (0.16 m x escala); solo existe mientras está expuesto
            if (_core != null)
            {
                var wgo = new GameObject("hb"); wgo.transform.SetParent(_core, false);
                var ws = wgo.AddComponent<SphereCollider>(); ws.radius = 0.16f * Anatomy.Size;
                _weak = wgo.AddComponent<Hitbox>(); _weak.Zone = HitZone.WeakPoint; _weak.Owner = _owner;
                wgo.name = "Hitbox_WeakPoint"; wgo.layer = HitboxLayer.Index; ws.isTrigger = false;
                wgo.SetActive(false);
            }
        }

        private void BuildDecals(ZombieMeshBundle bundle)
        {
            var spots = ZombieWounds.All;
            _decals = new Renderer[spots.Count]; _decalWanted = new bool[spots.Count]; _decalPatch = new WoundPatch[spots.Count];
            var mat = ZombieArt.Wounds;
            for (int i = 0; i < spots.Count; i++)
            {
                var go = new GameObject("Herida " + spots[i].Id);
                go.transform.SetParent(Rig.Bone(spots[i].Bone), false);
                go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = true;
                r.enabled = false;
                _decals[i] = r;
            }
        }

        // ================================================================== aparecer
        public void Setup(ZombieTypeData type, int seed, bool emerge)
        {
            _type = type; _dead = false; _flashing = false; _damage.Reset();
            var variants = ZombieMeshes.ForType(TypeId);
            var tentative = ZombieIndividual.Roll(seed, variants.Count, null);
            Variant = variants[Mathf.Clamp(tentative.VariantIndex, 0, variants.Count - 1)];
            Individual = ZombieIndividual.Roll(seed, variants.Count, Variant);
            Bundle = ZombieMeshes.Get(TypeId, Variant.id, Bucket);
            Lod = 0; Renderer.sharedMesh = Bundle.Lod[0];
            Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            Renderer.sharedMaterials = new[] { _skinMat, _clothMat };
            ApplyLook();
            // pose en reposo: huesos al sitio, cabeza y vientre sin deformar
            var spec = Rig.Spec;
            for (int i = 0; i < HumanoidRigSpec.BoneCount; i++)
            {
                _bones[i].localScale = Vector3.one; _bones[i].localRotation = Quaternion.identity;
                var lp = spec[i].LocalPosition; _bones[i].localPosition = new Vector3(lp.X, lp.Y, lp.Z);
            }
            if (_belly != null) _belly.localScale = Vector3.one;
            foreach (var x in new[] { _jawL, _jawR, _core, _ribL, _ribR }) if (x != null) { x.localRotation = Quaternion.identity; x.localScale = Vector3.one; }
            _fury = 0; _weakOn = false; _weakOpen = 0f; _jaw = 0f; _lastEmissionOpen = -1f;
            if (_weak != null) _weak.gameObject.SetActive(false);
            if (_steam != null) { var em = _steam.emission; em.rateOverTime = 0f; _steam.Clear(); }
            Visual.localPosition = Vector3.zero; Visual.localRotation = Quaternion.identity; Visual.localScale = Vector3.one;
            Rig.SetFootShape(Bundle.Foot);
            Rig.Inputs = default; Rig.Inputs.Grounded = true;
            Rig.ResetPose();
            // heridas: las del individuo desde el principio, las de daño apagadas
            var spots = ZombieWounds.All;
            for (int i = 0; i < spots.Count; i++)
            {
                _decalWanted[i] = false; _decals[i].enabled = false;
                var w = Bundle.WoundBySpot[i];
                if (w.patch != null)
                {
                    _decalPatch[i] = w.patch; _decals[i].GetComponent<MeshFilter>().sharedMesh = w.mesh;
                    if (spots[i].Stage == 0 && Individual.HasWound(spots[i].Slot)) _decalWanted[i] = true;
                }
                else _decalPatch[i] = null;
            }
            _fTorsoX = _fTorsoY = _fTorsoZ = _fHeadX = _fHeadY = _fHeadZ = _fPelvisY = _fPelvisRoll = _fArmL = _fArmR = default;
            _time = Random.value * 10f; _pendingDt = 0f; _inflate = 1f; _glow = 0f; _shake = 0f; _impactPending = false; _attackIndex = -1; _lastAttackT = -1f;
            _emerge = emerge ? 0f : EmergeSeconds;
            SetHitboxesEnabled(true);
            ApplyDecalVisibility();
        }

        private bool _lookReady;
        private float _nextLookCheck;

        private void ApplyLook()
        {
            var v = Variant; var ind = Individual;
            // mientras las texturas se generan en segundo plano, el tinte lleva el color base del tipo (si no, saldría blanco)
            _lookReady = ZombieArt.IsReady(_skinMat) && ZombieArt.IsReady(_clothMat);
            Color skin = new Color(v.skinTone[0], v.skinTone[1], v.skinTone[2]) * (1f + ind.SkinShift);
            if (!_lookReady) skin *= new Color(Anatomy.SkinBase[0], Anatomy.SkinBase[1], Anatomy.SkinBase[2]);
            skin.a = 1f;
            _skinBlock.SetColor(BaseColorId, skin);
            ApplyEmission(0f);
            Color cloth = new Color(v.clothTone[0], v.clothTone[1], v.clothTone[2]) * (1f - 0.35f * ind.Grime);
            if (!_lookReady) cloth *= new Color(0.4f, 0.38f, 0.34f);
            cloth.a = 1f;
            _clothBlock.SetColor(BaseColorId, cloth);
            Renderer.SetPropertyBlock(_skinBlock, 0); Renderer.SetPropertyBlock(_clothBlock, 1);
        }

        private void ApplyEmission(float fuseGlow)
        {
            var v = Variant; var tint = new Color(v.eyeTint[0], v.eyeTint[1], v.eyeTint[2]);
            // ojos con brillo tenue; el explosivo suma las venas y pústulas luminosas, que se encienden con la mecha
            float baseGlow = 0.35f + 0.9f * Individual.EyeGlow;
            Color e = tint * baseGlow;
            // jefes: la furia enciende ojos y venas (x2.8 y x4.6), y el núcleo expuesto del Devorador late
            if (Anatomy.IsBoss) e *= (1f + 1.8f * _fury) * (1f + 1.6f * _weakOpen);
            if (Anatomy.Belly) e += new Color(1f, 0.45f, 0.12f) * (0.25f + 3.2f * fuseGlow);
            _skinBlock.SetColor(EmissionId, e);
        }

        // ================================================================== interfaz con el controlador
        public void SetColor(Color c)
        {
            if (!_flashing) return;
            _flashing = false;
            Renderer.sharedMaterials = new[] { _skinMat, _clothMat };
            Renderer.SetPropertyBlock(_skinBlock, 0); Renderer.SetPropertyBlock(_clothBlock, 1);
        }

        public void SetFlash(bool on, Material flash)
        {
            if (!on) { SetColor(Color.white); return; }
            _flashing = true;
            Renderer.sharedMaterials = new[] { flash, flash };
            Renderer.SetPropertyBlock(null, 0); Renderer.SetPropertyBlock(null, 1);
        }

        public void SetWeakPoint(bool on)
        {
            _weakOn = on && !_dead;
            if (_weak != null) _weak.gameObject.SetActive(_weakOn);
        }

        public void SetFury(int phase)
        {
            if (phase == _fury) return;
            _fury = phase;
            if (!_flashing) { ApplyEmission(_glow); Renderer.SetPropertyBlock(_skinBlock, 0); }
            if (_steam != null) { var em = _steam.emission; em.rateOverTime = 10f * phase; }
        }

        public void SetHitboxesEnabled(bool on) { foreach (var h in Hitboxes) { var c = h.GetComponent<Collider>(); if (c != null) c.enabled = on; } }

        public void Pose(float walkPhase, float amount, float attack, float stagger, float time, float lunge = 0f) { }

        public bool ConsumeImpact() { bool p = _impactPending; _impactPending = false; return p; }

        public void SetFuse(float inflate, float glow, float shake)
        {
            _inflate = inflate; _shake = shake;
            if (_belly != null) _belly.localScale = Vector3.one * inflate;
            if (Mathf.Abs(glow - _glow) > 0.01f && !_flashing) { _glow = glow; ApplyEmission(glow); Renderer.SetPropertyBlock(_skinBlock, 0); }
        }

        public void OnHit(HitZone zone, Vector3 dirWorld, float strength, float healthFraction)
        {
            if (_dead) return;
            // dirección hacia donde miraba el disparo, en el espacio del cuerpo: z positiva = viene de frente
            Vector3 local = -Visual.InverseTransformDirection(dirWorld);
            if (Anatomy.IsBoss && _type != null) strength *= 1f - 0.75f * _type.stunResistance;   // los jefes apenas se inmutan
            var f = ZombieMotion.FlinchFor(zone, new V3(local.x, local.y, local.z), strength);
            const float k = 4f;   // el muelle crítico pasa por un máximo de velocidad / 30
            _fTorsoX.Kick(f.Torso.X * k); _fTorsoY.Kick(f.Torso.Y * k); _fTorsoZ.Kick(f.Torso.Z * k);
            _fHeadX.Kick(f.Head.X * k); _fHeadY.Kick(f.Head.Y * k); _fHeadZ.Kick(f.Head.Z * k);
            _fPelvisY.Kick(f.PelvisDrop * 6f); _fPelvisRoll.Kick(f.PelvisRoll * k); _fArmL.Kick(f.LeftArm * 8f); _fArmR.Kick(f.RightArm * 8f);
            _damage.Update(healthFraction, zone, _revealed);
            for (int i = 0; i < _revealed.Count; i++) RevealWound(_revealed[i].Key, _revealed[i].Value);
        }

        private void RevealWound(HitZone zone, int stage)
        {
            var spots = ZombieWounds.All;
            for (int i = 0; i < spots.Count; i++)
                if (spots[i].Stage == stage && spots[i].Zone == zone && _decalPatch[i] != null) { _decalWanted[i] = true; break; }
            ApplyDecalVisibility();
        }

        private void ApplyDecalVisibility()
        {
            bool show = Lod < 2 && !_flashing;
            for (int i = 0; i < _decals.Length; i++) _decals[i].enabled = show && _decalWanted[i];
        }

        public void OnDeath(HitZone zone, Vector3 dirWorld, bool headshot)
        {
            if (_dead) return;
            Vector3 local = -Visual.InverseTransformDirection(dirWorld);
            _form = ZombieMotion.DeathFor(zone, new V3(local.x, local.y, local.z), headshot, Individual.Seed);
            if (Anatomy.Boss == ZombieBossKind.Launcher && _form == ZombieDeathForm.FallBack) _form = ZombieDeathForm.FallForward;   // con el depósito a la espalda no cae de espaldas
            _target = Bundle.Lying[(int)_form];
            _startRot = new Quaternion[HumanoidRigSpec.BoneCount];
            for (int i = 0; i < _startRot.Length; i++) _startRot[i] = _bones[i].localRotation;
            _startHips = _bones[(int)BoneId.Hips].localPosition;
            _deathT = 0f; _deathDur = _form == ZombieDeathForm.FallBack ? 0.95f : (_form == ZombieDeathForm.FallForward ? 0.85f : 1.1f);
            _dead = true; _emerge = EmergeSeconds;
            if (headshot) _bones[(int)BoneId.Head].localScale = Vector3.zero;   // golpe seco: la cabeza desaparece y queda el muñón del cuello
            if (_belly != null) _belly.localScale = Vector3.one;
            SetWeakPoint(false);
            if (_steam != null) { var em = _steam.emission; em.rateOverTime = 0f; }
        }

        // ================================================================== cada frame
        /// <summary>CPU (ms) que gastaron todos los cuerpos de zombi en Animate durante el último frame completo (panel F6).</summary>
        public static float AnimateMsLastFrame { get; private set; }
        private static float _accumMs; private static int _accumFrame = -1;

        public void Animate(in ZombieAnimState s)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            AnimateInner(s);
            float ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000f / System.Diagnostics.Stopwatch.Frequency;
            if (_accumFrame != Time.frameCount) { AnimateMsLastFrame = _accumMs; _accumMs = 0f; _accumFrame = Time.frameCount; }
            _accumMs += ms;
        }

        private void AnimateInner(in ZombieAnimState s)
        {
            _state = s;
            if (!_lookReady && Time.unscaledTime >= _nextLookCheck) { _nextLookCheck = Time.unscaledTime + 0.5f; if (ZombieArt.IsReady(_skinMat) && ZombieArt.IsReady(_clothMat)) ApplyLook(); }
            if (_camFrame != Time.frameCount) { _camFrame = Time.frameCount; _cam = Camera.main; }
            float dist = s.DistanceToCamera;
            UpdateLod(dist);
            if (_dead) { UpdateDeath(s.Dt); return; }
            _pendingDt += s.Dt;
            _time += s.Dt;
            // animación lejana o fuera de pantalla: se congela (lo que se salta se acumula y no se nota al volver)
            int interval = RigLod.Interval(dist);
            bool offscreen = !Renderer.isVisible && dist > 14f;
            if (dist > 90f || offscreen) return;
            if (interval > 1 && (Time.frameCount + _frameOffset) % interval != 0) return;
            float dt = Mathf.Min(_pendingDt, 0.1f); _pendingDt = 0f;
            Step(dt, s);
        }

        private void UpdateLod(float dist)
        {
            int want = Lod == 0 ? (dist > 22f ? 1 : 0) : (Lod == 1 ? (dist < 18f ? 0 : (dist > 42f ? 2 : 1)) : (dist < 38f ? 1 : 2));
            if (want != Lod && Bundle != null)
            {
                Lod = want;
                Renderer.sharedMesh = Bundle.Lod[Lod];
                Renderer.shadowCastingMode = Lod == 0 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                ApplyDecalVisibility();
            }
            Rig.ApplyLod(dist);
            bool vis = dist < 110f;
            if (Renderer.enabled != vis) Renderer.enabled = vis;
        }

        private void Step(float dt, in ZombieAnimState s)
        {
            var ind = Individual;
            _armGuardL = ZombieMotion.Guard(TypeId, -1, ind.Aggression); _armGuardR = ZombieMotion.Guard(TypeId, 1, ind.Aggression);
            // qué hace el cuerpo: emerger > aturdido > ataque > andar/quedarse
            ZombieMotionFrame f;
            bool attacking = s.AttackT >= 0f && !s.Stunned && !Emerging;
            if (Emerging)
            {
                _emerge += dt; f = ZombieMotion.Emerge(_emerge / EmergeSeconds);
            }
            else if (s.Stunned) f = ZombieMotion.Stunned(_time, ind.Phase);
            else if (s.BossPose != BossPose.None)
            {
                f = ZombieMotion.Boss(s.BossPose, s.BossT, _time, _fury, _armGuardL, _armGuardR);
                attacking = true; _lastAttackT = -1f;
            }
            else if (attacking)
            {
                if (_lastAttackT < 0f || s.AttackT < _lastAttackT - 0.2f) _attackIndex++;
                _lastAttackT = s.AttackT;
                f = ZombieMotion.Attack(s.AttackKind, s.AttackT, _armGuardL, _armGuardR);
                if (f.Impact > 0.9f && !_impactPending && s.AttackKind == ZombieAttackKind.Smash) { _impactPending = true; }
            }
            else
            {
                _lastAttackT = -1f;
                f = default;
            }
            // guardia de brazos del tipo y del individuo (en idle y andando)
            if (!attacking && !Emerging && !s.Stunned)
            {
                bool engaged = s.Chasing;
                float w = engaged ? 1f : 0.4f + 0.2f * ind.Aggression;
                f.LeftHand = _armGuardL; f.RightHand = _armGuardR; f.LeftWeight = w; f.RightWeight = w;
                // cabeza que cuelga y balanceo lento
                float breath = Mathf.Sin(_time * 0.9f + ind.Phase * 6.28f);
                f.Head = new V3(12f + 5f * breath + (engaged ? -8f : 0f), 4f * Mathf.Sin(_time * 0.55f + ind.Phase * 3f), ind.HeadTilt);
                f.Torso = new V3(0f, 3f * Mathf.Sin(_time * 0.7f + ind.Phase) + (ind.Phase - 0.5f) * 14f, 2.5f * Mathf.Sin(_time * 0.6f + ind.Phase * 2f) + (ind.ShoulderDrop - 0.5f) * 10f);   // hombros desiguales y torso algo girado
                // carga con la cabeza baja (tanque) y torso adelantado (corredor)
                if (engaged && TypeId == ZombieIds.Tank) { f.Head = new V3(f.Head.X + 26f, f.Head.Y, f.Head.Z); f.Torso = new V3(14f, f.Torso.Y, f.Torso.Z); }
                if (engaged && TypeId == ZombieIds.Runner) f.Torso = new V3(f.Torso.X + 8f, f.Torso.Y, f.Torso.Z);
            }
            else if (attacking && s.BossPose == BossPose.None && s.AttackKind != ZombieAttackKind.Smash) { f.Head = new V3(f.Head.X + 10f, f.Head.Y, f.Head.Z + ind.HeadTilt * 0.5f); }
            if (_fury > 0 && !attacking && !s.Stunned) { f.Torso = new V3(f.Torso.X + 6f * _fury, f.Torso.Y, f.Torso.Z); f.Head = new V3(f.Head.X - 4f * _fury, f.Head.Y, f.Head.Z); }   // furia: encorvado y mirando al frente
            if (Anatomy.IsBoss) BossExtras(dt, f.JawOpen);
            // cojera
            ZombieMotion.Limp(ind.Limp, ind.LimpSide, Rig.GaitPhase, out float limpRoll, out float limpTorso);
            // reacciones a impactos (muelles que vuelven a cero)
            float tx = _fTorsoX.Step(0f, 0.18f, dt), ty = _fTorsoY.Step(0f, 0.18f, dt), tz = _fTorsoZ.Step(0f, 0.18f, dt);
            float hx = _fHeadX.Step(0f, 0.16f, dt), hy = _fHeadY.Step(0f, 0.16f, dt), hz = _fHeadZ.Step(0f, 0.16f, dt);
            float pd = _fPelvisY.Step(0f, 0.2f, dt), pr = _fPelvisRoll.Step(0f, 0.2f, dt);
            _fArmL.Step(0f, 0.2f, dt); _fArmR.Step(0f, 0.2f, dt);
            Vector3 jitter = _shake > 0f ? new Vector3(Mathf.PerlinNoise(_time * 31f, 1f) - 0.5f, Mathf.PerlinNoise(_time * 27f, 5f) - 0.5f, Mathf.PerlinNoise(_time * 23f, 9f) - 0.5f) * (10f * _shake) : Vector3.zero;

            var I = Rig.Inputs;
            I.Grounded = true; I.Dead = false;
            I.Hunch = (s.Chasing && TypeId == ZombieIds.Runner ? 0.4f : (s.Chasing ? 0.1f : 0f)) + 0.12f * _fury;
            I.Fatigue = 0.15f + 0.5f * ind.Limp;
            I.PelvisRoll = f.PelvisRoll + limpRoll + pr;
            I.PelvisOffset = new Vector3(f.PelvisOffset.X, f.PelvisOffset.Y, f.PelvisOffset.Z) * Anatomy.Proportions.height + new Vector3(0f, pd, 0f);
            I.TorsoExtra = new Vector3(f.Torso.X + tx + jitter.x, f.Torso.Y + ty + jitter.y, f.Torso.Z + tz + limpTorso + jitter.z);
            I.HeadExtra = new Vector3(f.Head.X + hx + jitter.x * 0.6f, f.Head.Y + hy, f.Head.Z + hz);
            I.LeftWeight = f.LeftWeight; I.RightWeight = f.RightWeight;
            I.ArmSwing = 1f;
            _frame = f;
            Rig.Inputs = I;
            Rig.Tick(dt);
        }

        // mandíbula doble que se abre (rugidos, onda), costillas que se abren sobre el núcleo expuesto y su latido
        private void BossExtras(float dt, float jawOpen)
        {
            float idle = 0.08f + 0.06f * Mathf.Sin(_time * 1.7f) + 0.12f * _fury;
            _jaw = Mathf.MoveTowards(_jaw, Mathf.Max(idle, jawOpen), dt * 4f);
            if (_jawL != null) _jawL.localRotation = Quaternion.Euler(_jaw * 34f, -_jaw * 26f, -_jaw * 12f);
            if (_jawR != null) _jawR.localRotation = Quaternion.Euler(_jaw * 34f, _jaw * 26f, _jaw * 12f);
            _weakOpen = Mathf.MoveTowards(_weakOpen, _weakOn ? 1f : 0f, dt * 3f);
            if (_ribL != null) _ribL.localRotation = Quaternion.Euler(0f, -72f * _weakOpen, 0f);
            if (_ribR != null) _ribR.localRotation = Quaternion.Euler(0f, 72f * _weakOpen, 0f);
            if (_core != null) _core.localScale = Vector3.one * (0.85f + 0.15f * _weakOpen + 0.06f * _weakOpen * Mathf.Sin(_time * 7f));
            if (_core != null && Mathf.Abs(_weakOpen - _lastEmissionOpen) > 0.04f && !_flashing)
            {
                _lastEmissionOpen = _weakOpen;
                ApplyEmission(_glow); Renderer.SetPropertyBlock(_skinBlock, 0);
            }
        }

        // las manos se colocan con el pecho de este frame (después del torso y antes del IK de los brazos)
        private void OnBeforeArms(ProceduralRig rig, float dt)
        {
            var I = rig.Inputs;
            Vector3 right = Visual.right, up = Visual.up, fwd = Visual.forward;
            float swing = Mathf.Sin(rig.GaitPhase * Mathf.PI * 2f);
            float flail = TypeId == ZombieIds.Runner && _state.Chasing ? 0.18f * (0.5f + Individual.Aggression) : 0.05f;
            for (int a = 0; a < 2; a++)
            {
                bool left = a == 0;
                V3 o = left ? _frame.LeftHand : _frame.RightHand;
                float armLen = _armLen * (left ? 1f : 1f);
                var shoulder = rig.Bone(left ? BoneId.LeftUpperArm : BoneId.RightUpperArm).position;
                float sw = swing * (left ? 1f : -1f) * flail;
                float kick = (left ? _fArmL.Value : _fArmR.Value);
                Vector3 target = shoulder + (right * o.X + up * (o.Y + sw * 0.5f - kick * 0.12f) + fwd * (o.Z + sw)) * armLen;
                if (left) I.LeftHand = target; else I.RightHand = target;
            }
            Quaternion rot = Quaternion.LookRotation(-right, -fwd);   // dedos hacia delante, palma hacia abajo
            I.LeftHandRot = rot; I.RightHandRot = rot;
            rig.Inputs = I;
        }

        private void UpdateDeath(float dt)
        {
            _deathT += dt;
            float e = Mathf.Clamp01(_deathT / _deathDur);
            for (int i = 0; i < HumanoidRigSpec.BoneCount; i++)
            {
                var id = (BoneId)i;
                float delay = id == BoneId.Hips || id == BoneId.Spine ? 0f : (id == BoneId.Head || id == BoneId.Neck ? 0.2f : 0.1f);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((e - delay) / (1f - delay)));
                var t = _target.Euler[i];
                _bones[i].localRotation = Quaternion.Slerp(_startRot[i], Quaternion.Euler(t.X, t.Y, t.Z), k);
            }
            float kh = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / 0.9f));
            _bones[(int)BoneId.Hips].localPosition = Vector3.Lerp(_startHips, new Vector3(_startHips.x * (1f - kh), _target.HipsPos.Y, 0f), kh);
        }
    }
}
