using UnityEngine;
using SevenDays.Procedural;
using SevenDays.Weapons;

namespace SevenDays.Animation
{
    /// <summary>Estado del manejo del arma en un instante (pesos ya suavizados por quien llama). Lo rellenan WeaponHandling/PlayerRigDriver y el escenario de pruebas.</summary>
    public struct GripState
    {
        public float Aim, Sprint, Cover, Draw, Reload;   // pesos 0..1
        public float AimPitch;                           // grados (+ abajo)
        public bool Aiming, InCover, TriggerOn, Sprinting;
        public ReloadState ReloadInfo;                   // puede ser null
        public float ActionCycle;                        // 0..1 bomba / cerrojo ciclando
        public float SinceShot;                          // segundos desde el último disparo
        public float MeleeT;                             // segundos desde que empezó el golpe (-1 sin golpe)
        public bool MeleeHeavy, MeleeCharging;
        public float FirstPerson;                        // 0..1 (mejilla solo en tercera persona)
    }

    /// <summary>Capas que se suman a la pose base: retroceso, sway, recarga, golpe, balanceo al correr y seguimiento del pecho.</summary>
    public struct PoseExtras
    {
        public Vector3 KickPos, ReloadRot, ChestOffset;
        public float KickPitch, Side;
        public Vector2 Sway;
        public V3 MeleeRot;
    }

    /// <summary>Inercia del arma (estado del muelle): el arma sigue a su anclaje en el cuerpo con un retraso elástico que crece con su peso.</summary>
    public sealed class GripInertia
    {
        public CriticalSpring3 Pos;
        public Quaternion Rot = Quaternion.identity;
        public bool Ready;
    }

    /// <summary>
    /// Agarre del arma compartido por el juego y por las pruebas visuales (así se prueba exactamente lo que se ve):
    ///  - POSE: el arma va anclada al CUERPO del rig (culata en el hueco del hombro; pistola y cuerpo a cuerpo respecto al pecho) en cadera, corriendo (low
    ///    ready), cobertura, apuntando y al sacarla, con inercia según su peso. Antes colgaba de un soporte fijo y flotaba delante del cuerpo.
    ///  - MANOS: cada mano se coloca por el EJE del mango que agarra (empuñadura, guardamano, bomba, cargador, culata, mango del arma cuerpo a cuerpo): la palma a un
    ///    radio del eje, el dorso hacia fuera y los dedos envolviéndolo (HandWrap). Antes la muñeca iba al punto y la palma quedaba a 6.6 cm.
    /// Se llama DESPUÉS de colocar el torso del rig y ANTES del IK de los brazos (ProceduralRig.BeforeArms): el arma sigue al pecho de este frame.
    /// </summary>
    public static class WeaponGripSolver
    {
        private static Vector3 V(V3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <summary>Al recargar el arma se inclina y gira HACIA la mano de apoyo (boca a la izquierda, ventana del cargador a la vista).</summary>
        public static readonly Vector3 ReloadTilt = new Vector3(-10f, -14f, -28f);   // giro -z: la parte de abajo (bomba, cargador) va a la izquierda

        private struct Hold { public Vector3 Anchor, Euler; }

        /// <summary>Pose base del arma en el espacio del padre de "pose" (posición y giro), anclada al cuerpo y con inercia.</summary>
        public static void BasePose(WeaponData w, ProceduralRig rig, Transform pose, in GripState s, GripInertia inertia, float dt, out Vector3 localPos, out Quaternion localRot)
        {
            Transform root = rig.transform;
            Vector3 fwd = Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized;
            Quaternion yaw = Quaternion.LookRotation(fwd.sqrMagnitude > 0.01f ? fwd : Vector3.forward, Vector3.up);
            var g = w.grip ?? WeaponGrip.For(w.kind, w.length, w.thickness);
            bool shouldered = g.shouldered && !w.IsMelee;
            Vector3 anchorOnWeapon = shouldered ? V(g.StockPos) : (w.IsMelee ? new Vector3(0f, 0f, MeleeGrips.For(w.id, w.length).Low) : V(g.GripPos));

            Transform chest = rig.Bone(BoneId.UpperChest);
            Vector3 cp = chest.position, cr = chest.right, cu = chest.up, cf = chest.forward;
            Vector3 At(Vector3 basePoint, float x, float y, float z) => basePoint + cr * x + cu * y + cf * z;
            Hold hip, sprint, cover, aim;
            if (shouldered)
            {
                // hueco del hombro derecho: un poco hacia dentro, abajo y delante de la articulación
                // hueco del hombro: delante de la articulación, en la superficie del chaleco (la culata apoya, no se hunde en el torso)
                Vector3 pocket = At(rig.Bone(BoneId.RightUpperArm).position, -0.06f, -0.04f, 0.1f + 0.11f * (rig.Proportions.torsoDepth * rig.Proportions.bulk - 0.9f));
                bool smg = w.kind == WeaponKind.Smg;
                aim = new Hold { Anchor = pocket, Euler = Vector3.zero };
                hip = smg ? new Hold { Anchor = At(pocket, -0.03f, -0.07f, 0.02f), Euler = new Vector3(8f, -6f, 0f) }       // subfusil al pecho
                          : new Hold { Anchor = At(pocket, 0.01f, -0.03f, 0f), Euler = new Vector3(10f, -3f, 0f) };         // culata en el hombro, boca algo abajo
                sprint = new Hold { Anchor = At(pocket, -0.02f, -0.06f, smg ? 0.06f : 0.05f), Euler = new Vector3(38f, -35f, 12f) };   // low ready: cruzada y abajo
                cover = new Hold { Anchor = At(pocket, -0.03f, -0.06f, 0.02f), Euler = new Vector3(14f, -8f, 0f) };         // pegada al pecho
            }
            else if (w.IsMelee)
            {
                hip = new Hold { Anchor = At(cp, 0.16f, -0.22f, 0.28f), Euler = new Vector3(-50f, -30f, 0f) };     // en diagonal delante del cuerpo, a dos manos
                sprint = new Hold { Anchor = At(cp, 0.18f, -0.26f, 0.2f), Euler = new Vector3(-30f, -20f, 0f) };
                cover = new Hold { Anchor = At(cp, 0.06f, -0.12f, 0.24f), Euler = new Vector3(-62f, -22f, 0f) };
                aim = hip;
            }
            else
            {
                // pistola y revólver (empuñadura): a una mano a la cadera (low ready); apuntando, brazos al frente a dos manos; en cobertura, pegada al pecho
                hip = new Hold { Anchor = At(cp, 0.17f, -0.2f, 0.3f), Euler = new Vector3(28f, -8f, 0f) };
                sprint = new Hold { Anchor = At(cp, 0.23f, -0.32f, 0.2f), Euler = new Vector3(65f, 0f, 0f) };
                cover = new Hold { Anchor = At(cp, 0.02f, -0.04f, 0.27f), Euler = new Vector3(18f, -4f, 0f) };
                aim = new Hold { Anchor = At(cp, 0.02f, 0.1f, 0.4f), Euler = Vector3.zero };
            }
            // recargando: el arma viene al centro del pecho, inclinada hacia la mano de apoyo (a una mano, la izquierda no llegaría al costado derecho)
            Hold reload = shouldered ? new Hold { Anchor = At(hip.Anchor, -0.02f, -0.03f, 0.02f), Euler = hip.Euler } : new Hold { Anchor = At(cp, 0.04f, -0.1f, 0.3f), Euler = new Vector3(22f, -18f, 8f) };
            hip = new Hold { Anchor = Vector3.Lerp(hip.Anchor, reload.Anchor, s.Reload), Euler = Vector3.Lerp(hip.Euler, reload.Euler, s.Reload) };
            Vector3 a = Vector3.Lerp(hip.Anchor, sprint.Anchor, s.Sprint), e = Vector3.Lerp(hip.Euler, sprint.Euler, s.Sprint);
            a = Vector3.Lerp(a, cover.Anchor, s.Cover); e = Vector3.Lerp(e, cover.Euler, s.Cover);
            a = Vector3.Lerp(a, aim.Anchor, s.Aim); e = Vector3.Lerp(e, aim.Euler, s.Aim);
            // guardada (al cambiar de arma): baja a la cadera derecha y sube desde ahí
            Vector3 holster = rig.Bone(BoneId.Hips).position + root.right * 0.22f - Vector3.up * 0.12f;
            a = Vector3.Lerp(holster, a, s.Draw); e = Vector3.Lerp(new Vector3(80f, 10f, 0f), e, s.Draw);
            e.x += s.AimPitch;

            Quaternion R = yaw * Quaternion.Euler(e);
            Vector3 origin = a - R * anchorOnWeapon;
            if (inertia != null && dt > 0f)
            {
                // inercia en el espacio del cuerpo: acompaña al torso con un retraso elástico (crítico: sin vibrar), mayor cuanto más pesa el arma
                Vector3 lo = root.InverseTransformPoint(origin);
                Quaternion lr = Quaternion.Inverse(root.rotation) * R;
                float lag = 0.012f + 0.045f * WeaponWeights.For(w.kind, w.id);
                if (!inertia.Ready) { inertia.Pos.Reset(new V3(lo.x, lo.y, lo.z)); inertia.Rot = lr; inertia.Ready = true; }
                V3 sp = inertia.Pos.Step(new V3(lo.x, lo.y, lo.z), lag, dt);
                inertia.Rot = Quaternion.Slerp(inertia.Rot, lr, 1f - Mathf.Exp(-dt / lag));
                origin = root.TransformPoint(new Vector3(sp.X, sp.Y, sp.Z));
                R = root.rotation * inertia.Rot;
            }
            Transform parent = pose.parent;
            localPos = parent != null ? parent.InverseTransformPoint(origin) : origin;
            localRot = parent != null ? Quaternion.Inverse(parent.rotation) * R : R;
        }

        /// <summary>Pose final del arma (antes de lean, disparo a ciegas y muerte, que añade WeaponHandling): base + capas (retroceso, sway, recarga, golpe).</summary>
        public static void ApplyPose(Transform pose, WeaponData w, ProceduralRig rig, in GripState s, in PoseExtras e, GripInertia inertia = null, float dt = 0f)
        {
            BasePose(w, rig, pose, s, inertia, dt, out Vector3 pos, out Quaternion rot);
            pos += new Vector3(-0.04f, 0.03f, 0.03f) * s.Reload + new Vector3(0f, 0f, e.Side * 0.07f) + e.ChestOffset;
            // con la culata al hombro el retroceso lo absorbe el torso (ProceduralRig.Recoil): el arma retrocede la mitad y no se hunde en el pecho
            var gr = w.grip ?? WeaponGrip.For(w.kind, w.length, w.thickness);
            pose.localPosition = pos + rot * e.KickPos * (gr.shouldered ? Mathf.Lerp(1f, 0.5f, s.Aim) : 1f);
            Quaternion extra = Quaternion.Euler(e.KickPitch + e.Sway.y + e.ReloadRot.x + e.MeleeRot.X + e.Side * 12f, e.Sway.x + e.ReloadRot.y + e.MeleeRot.Y,
                -e.Sway.x * 0.5f + e.ReloadRot.z + e.MeleeRot.Z);
            if (w.IsMelee)
            {
                // a dos manos el golpe gira entre las manos (no alrededor de la de abajo): la mano alta no sale despedida
                var mg = MeleeGrips.For(w.id, w.length);
                Vector3 pivot = new Vector3(0f, 0f, mg.TwoHanded ? (mg.Low + mg.High) * 0.5f : mg.Low);
                pose.localPosition += rot * (pivot - extra * pivot);
            }
            pose.localRotation = rot * extra;
        }

        /// <summary>Piezas móviles: corredera al disparar, bomba y cerrojo al ciclar, cargador y tambor durante la recarga (las mismas en el juego y en las pruebas).</summary>
        public static void AnimateParts(GunParts parts, WeaponData w, ReloadState rl, float actionCycle, float sinceShot)
        {
            if (parts == null) return;
            parts.SetSlide(1f - Mathf.Clamp01(sinceShot / 0.07f));
            parts.SetPump(actionCycle);
            float p = rl != null && rl.Active ? rl.Progress : -1f;
            float bolt = actionCycle;
            switch (rl != null && rl.Active ? rl.Style : ReloadStyle.None)
            {
                case ReloadStyle.Magazine: Mag(parts, p, 0.15f, 0.65f); break;
                case ReloadStyle.BoltMagazine: Mag(parts, p, 0.28f, 0.62f); bolt = Window(p, 0.12f, 0.85f); break;
                case ReloadStyle.Cylinder: parts.SetCylinder(Window(p, 0.12f, 0.85f)); break;
                default: parts.SetMag(0f, true); parts.SetCylinder(0f); break;
            }
            parts.SetBolt(bolt);
        }

        /// <summary>Cargador: baja y desaparece al sacarlo; el nuevo aparece por debajo y sube hasta encajar.</summary>
        private static void Mag(GunParts parts, float p, float outAt, float inAt)
        {
            if (p < outAt) parts.SetMag(0f, true);
            else if (p < outAt + 0.12f) parts.SetMag(Mathf.Lerp(0f, 0.25f, (p - outAt) / 0.12f), true);
            else if (p < inAt - 0.15f) parts.SetMag(0.25f, false);
            else if (p < inAt) parts.SetMag(Mathf.Lerp(0.2f, 0f, CameraMath.EaseInOut((p - (inAt - 0.15f)) / 0.15f)), true);
            else parts.SetMag(0f, true);
        }

        private static float Window(float p, float open, float close) => Mathf.Clamp01((p - open) / 0.06f) * Mathf.Clamp01((close - p) / 0.06f);

        /// <summary>Una mano en un mango: punto del eje, dirección del eje, hacia dónde queda el dorso, hacia dónde salen los dedos y radio.</summary>
        private struct Handle { public Vector3 Point, Axis, Back, Fingers; public float Radius; }

        /// <summary>Manos al arma: rellena las manos de "i" (muñeca, giro, peso y punto de agarre) y devuelve la pose de dedos de cada una.</summary>
        public static void Hands(WeaponData data, Transform wp, GunParts parts, ProceduralRig rig, in GripState s, ref RigInputs i, out HandPose rightPose, out HandPose leftPose)
        {
            var hold = WeaponHolds.For(data.kind);
            var g = data.grip ?? WeaponGrip.For(data.kind, data.length, data.thickness);
            Vector3 wf = wp.forward, wu = wp.up, wr = wp.right;
            float th = data.thickness;
            i.WeaponWeight = WeaponWeights.For(data.kind, data.id);
            i.HandSpace = wp;

            // ---- mano derecha (dominante): empuñadura; cuerpo a cuerpo, extremo del mango; francotirador al ciclar, el cerrojo
            Handle right;
            HandPose rightTemplate = HandPoses.For(HandGrip.Grip, s.TriggerOn);
            var melee = MeleeGrips.For(data.id, data.length);
            if (data.IsMelee)
            {
                right = new Handle { Point = wp.TransformPoint(0f, 0f, melee.Low), Axis = wf, Back = wr, Fingers = -wu, Radius = th * 0.75f };
                rightTemplate = HandPoses.For(HandGrip.Fist);
            }
            else
            {
                Vector3 gripAxis = wp.rotation * Quaternion.Euler(g.grip[3], 0f, 0f) * Vector3.up;
                // la mano algo baja en la empuñadura: la membrana del pulgar justo bajo el cuerpo del arma, el pulgar envuelve por el lado izquierdo
                right = new Handle { Point = wp.TransformPoint(V(g.GripPos)) - gripAxis * 0.015f, Axis = gripAxis, Back = wr, Fingers = wf, Radius = Mathf.Max(th * 0.425f, 0.0275f) };
                bool bolt = data.kind == WeaponKind.SniperRifle && parts != null && parts.Bolt != null &&
                            (s.ActionCycle > 0.05f || (s.ReloadInfo != null && s.ReloadInfo.Active && (s.ReloadInfo.LastEvent == ReloadEvent.BoltOpen || s.ReloadInfo.LastEvent == ReloadEvent.MagIn)));
                if (bolt) right = new Handle { Point = parts.Bolt.position + wr * 0.015f, Axis = wr, Back = wu, Fingers = -wf, Radius = 0.012f };   // ciclar el cerrojo
            }
            float kR = rig.RightHandMesh != null ? rig.RightHandMesh.Scale : 1f;
            Place(right, kR, out i.RightHand, out i.RightHandRot);
            i.RightHandGrip = right.Point; i.RightWeight = 1f;
            if (rig.RightHandMesh != null) rig.RightHandMesh.GripRadius = right.Radius;
            rightPose = HandWrap.Wrap(right.Radius, kR, rightTemplate);
            // pulgar en su sitio (medido en las mallas: así no entra en el arma): con arma de fuego, estirado por el lado; cuerpo a cuerpo, rodea el mango
            if (data.IsMelee) { rightPose.Thumb = new FingerCurl(10f, 10f); rightPose.ThumbOpposition = 10f; }
            else { rightPose.Thumb = new FingerCurl(0f, 0f); rightPose.ThumbOpposition = 0f; }
            if (!data.IsMelee && !s.TriggerOn) rightPose.Index = new FingerCurl(8f, 4f);   // índice fuera del gatillo, sobre el guardamonte, hasta que dispara
            if (data.kind == WeaponKind.Revolver && s.SinceShot > 0.03f && s.SinceShot < 0.35f) { rightPose.Thumb = new FingerCurl(0f, 0f); rightPose.ThumbOpposition = -25f; }   // pulgar atrás, sobre el martillo: arma

            // ---- mano izquierda (apoyo)
            Handle left;
            HandPose leftTemplate = HandPoses.For(HandGrip.Support);
            float leftW;
            if (data.IsMelee)
            {
                left = new Handle { Point = wp.TransformPoint(0f, 0f, melee.High), Axis = wf, Back = -wr, Fingers = -wu, Radius = th * 0.5f };
                leftTemplate = HandPoses.For(HandGrip.Fist);
                leftW = melee.TwoHanded && !MeleeGrips.Released(melee, s.MeleeT, s.MeleeHeavy) ? 1f : 0f;   // suelta la de apoyo en el golpe
            }
            else
            {
                bool twoHands = s.Aiming || s.InCover ? hold.TwoHandsAim : hold.TwoHandsHip;
                leftW = twoHands ? 1f : 0f;
                switch (g.support)
                {
                    case "pistol":   // agarre cruzado: envuelve la empuñadura por fuera de la mano derecha, pulgares adelante
                        left = new Handle { Point = right.Point, Axis = right.Axis, Back = -wr, Fingers = wf, Radius = right.Radius + 0.03f };
                        leftTemplate = new HandPose { Thumb = new FingerCurl(0f, 0f), ThumbOpposition = -30f };
                        break;
                    case "mag":
                        left = new Handle { Point = wp.TransformPoint(V(g.ForegripPos)), Axis = wp.rotation * Quaternion.Euler(g.foregrip[3], 0f, 0f) * Vector3.up, Back = -wr, Fingers = wf, Radius = Mathf.Max(th * 0.375f, 0.03f) };
                        leftTemplate = HandPoses.For(HandGrip.MagGrip);
                        break;
                    case "pump":     // la mano va EN la bomba: se mueve con ella al bombear
                        left = new Handle { Point = parts != null && parts.Pump != null ? parts.Pump.position : wp.TransformPoint(V(g.ForegripPos)), Axis = wf, Back = -wu, Fingers = wr, Radius = Mathf.Max(th * 0.575f, 0.0275f) };
                        leftTemplate = HandPoses.For(HandGrip.Pump);
                        break;
                    default:         // "C" bajo el guardamano; francotirador apuntando: bajo la culata
                        left = new Handle { Point = wp.TransformPoint(V(g.ForegripPos)), Axis = wf, Back = -wu, Fingers = wr, Radius = 0.05f };
                        if (data.kind == WeaponKind.SniperRifle && s.Aim > 0.5f) left.Point = wp.TransformPoint(0f, -0.02f, -0.12f);
                        break;
                }
            }

            // recarga: la mano de apoyo suelta el arma, va a la cintura y vuelve (con el cargador en la mano: mango fino)
            var rl = s.ReloadInfo;
            bool reloading = rl != null && rl.Active, transit = false;
            if (!data.IsMelee && (reloading || leftW > 0.01f))
            {
                Handle target = reloading ? ReloadHandle(rl, left, parts, rig, wp) : left;
                Vector3 now = rig.LeftHandMesh != null ? rig.LeftHandMesh.GripCenter : rig.Bone(BoneId.LeftHand).position;
                float travel = Vector3.Distance(now, target.Point);
                // al soltar el agarre, la mano conserva su orientación hasta separarse del arma (no gira atravesándola); al llegar, ya con la del destino
                float away = Mathf.Clamp01((Vector3.Distance(rig.Bone(BoneId.LeftHand).position, left.Point) - 0.08f) / 0.15f);
                float arrive = 1f - Mathf.Clamp01((travel - 0.03f) / 0.1f);
                // de un punto del arma a otro: primero se pone a su lado (misma altura y profundidad, 11 cm fuera) ya girada, y después entra recta.
                // Saliendo hacia lejos (cintura), primero se aparta por su lado. Nunca va en diagonal a través de una pieza ni gira junto a ella.
                bool sideStep = false, moving = travel > (reloading ? 0.03f : 0.05f) && parts != null;
                if (moving && Vector3.Distance(target.Point, wp.position) < data.length + 0.1f)
                {
                    Vector3 hl = wp.InverseTransformPoint(now), tl = wp.InverseTransformPoint(target.Point);
                    if (new Vector2(hl.y - tl.y, hl.z - tl.z).magnitude > 0.025f) { target.Point = wp.TransformPoint(new Vector3(Mathf.Min(hl.x, tl.x - 0.11f), tl.y, tl.z)); sideStep = true; }
                }
                else if (moving)
                {
                    Vector3 hl = wp.InverseTransformPoint(now);
                    if (hl.x > -0.09f && hl.z > -0.3f && hl.z < data.length) target.Point = wp.TransformPoint(new Vector3(hl.x - 0.12f, hl.y - 0.04f, hl.z));
                }
                if (Vector3.Distance(target.Point, left.Point) > 0.02f) target = Blend(left, target, sideStep ? 1f : Mathf.Max(Mathf.Max(away, arrive), 0.15f), target.Point);
                left = target;
                // en tránsito: junto al arma la mano se ABRE (los dedos salen del agarre sin clavarse); ya lejos, va recogida (sujetando el cargador)
                bool nearGun = away < 0.5f || Vector3.Distance(target.Point, wp.position) < data.length + 0.05f && travel < 0.15f;
                if (moving && travel > 0.025f) { leftTemplate = nearGun ? HandPoses.For(HandGrip.Open) : HandPoses.For(HandGrip.Fist); transit = true; }
                else if (reloading) leftTemplate = HandPoses.For(HandGrip.MagGrip);
                if (reloading) leftW = 1f;
            }
            if (s.Draw < 0.7f) leftW *= s.Draw;   // sacando el arma, la de apoyo se une al final

            float kL = rig.LeftHandMesh != null ? rig.LeftHandMesh.Scale : 1f;
            Place(left, kL, out i.LeftHand, out i.LeftHandRot);
            i.LeftHandGrip = left.Point; i.LeftWeight = leftW;
            if (rig.LeftHandMesh != null) rig.LeftHandMesh.GripRadius = left.Radius;
            if (g.support != "pistol" || data.IsMelee) { leftTemplate.Thumb = data.IsMelee ? new FingerCurl(10f, 10f) : new FingerCurl(0f, 0f); leftTemplate.ThumbOpposition = data.IsMelee ? 10f : 0f; }
            leftPose = leftW > 0.01f ? (transit ? leftTemplate : HandWrap.Wrap(left.Radius, kL, leftTemplate)) : HandPoses.For(HandGrip.Relaxed);
            if (rl != null && rl.Active && rl.LastEvent == ReloadEvent.MagOut) leftPose = HandPoses.For(HandGrip.Open);

            // postura de perfil con armas de culata: 15° a la cadera, 30° apuntando (el hombro izquierdo se adelanta hacia el guardamano)
            bool stocked = g.shouldered && !data.IsMelee;
            i.AimTwist = stocked ? Mathf.Lerp(GripLayout.ReadyTwist, GripLayout.ShoulderedTwist, s.Aim) * s.Draw : 0f;
            i.CheekWeld = stocked && s.Aiming && s.FirstPerson < 0.5f ? 1f : 0f;
        }

        /// <summary>Muñeca y giro de la mano para que el eje del mango quede justo delante de la palma (HandWrap.Anchor), dorso hacia fuera.</summary>
        private static void Place(Handle h, float k, out Vector3 wrist, out Quaternion rot)
        {
            Vector3 axis = h.Axis.sqrMagnitude > 1e-6f ? h.Axis.normalized : Vector3.forward;
            Vector3 back = Vector3.ProjectOnPlane(h.Back, axis), fingers = Vector3.ProjectOnPlane(h.Fingers, axis);
            if (fingers.sqrMagnitude < 1e-6f) fingers = Vector3.Cross(axis, back);
            rot = HandRot(back, fingers);
            V3 a = HandWrap.Anchor(h.Radius, k);
            wrist = h.Point - rot * new Vector3(a.X, a.Y, a.Z);
        }

        private static Handle Blend(Handle a, Handle b, float t, Vector3 point) => new Handle
        {
            Point = point, Axis = Vector3.Slerp(a.Axis, b.Axis, t), Back = Vector3.Slerp(a.Back, b.Back, t), Fingers = Vector3.Slerp(a.Fingers, b.Fingers, t), Radius = Mathf.Lerp(a.Radius, b.Radius, t),
        };

        /// <summary>
        /// Mano de apoyo en cada momento de la recarga: envuelve el cargador (al sacarlo y al meter el nuevo), va a la cintura (palma hacia el cuerpo), sujeta el
        /// tambor abierto del revólver, mete cartuchos por debajo de la escopeta y vuelve al agarre.
        /// </summary>
        private static Handle ReloadHandle(ReloadState rl, Handle support, GunParts parts, ProceduralRig rig, Transform wp)
        {
            Vector3 wf = wp.forward, wu = wp.up, wr = wp.right;
            Transform hips = rig.Bone(BoneId.Hips);
            var belt = new Handle { Point = hips.TransformPoint(-0.17f, 0.08f, 0.12f), Axis = hips.forward, Back = -hips.right, Fingers = -hips.up, Radius = 0.02f };
            Handle mag = support;
            if (parts != null && parts.Mag != null)
            {
                Vector3 up = parts.Mag.up;
                // cargador por fuera (fusil, subfusil, francotirador): la mano lo envuelve. Dentro de la empuñadura (pistola): lo empuja desde abajo, palma arriba
                bool inGrip = parts.Mag.localPosition.z < 0.08f;
                mag = inGrip
                    ? new Handle { Point = parts.MagWorldRest - up * (parts.Mag.lossyScale.y * 0.5f + 0.035f), Axis = wf, Back = -up, Fingers = wr, Radius = 0.02f }
                    : new Handle { Point = parts.MagWorldRest, Axis = up, Back = -wr, Fingers = wf, Radius = new Vector2(parts.Mag.lossyScale.x, parts.Mag.lossyScale.z).magnitude * 0.5f + 0.008f };   // cubre la diagonal de la caja (inclinada)
            }
            switch (rl.Style)
            {
                case ReloadStyle.Shells:
                {
                    if (rl.Elapsed < ReloadState.ShellLead) return support;
                    float local = ((rl.Elapsed - ReloadState.ShellLead) % rl.ShellTime) / rl.ShellTime;
                    var port = parts != null && parts.Pump != null
                        ? new Handle { Point = parts.Pump.position - wu * (parts.Pump.lossyScale.y * 0.5f + 0.03f), Axis = wf, Back = -wu, Fingers = wr, Radius = 0.02f }   // empuja el cartucho desde abajo
                        : support;
                    return local < 0.45f ? belt : port;
                }
                case ReloadStyle.Cylinder:
                {
                    var cyl = parts != null && parts.Cylinder != null
                        ? new Handle { Point = parts.Cylinder.position - wr * (parts.transform.Find("Body").lossyScale.x * 0.5f + 0.035f), Axis = wf, Back = -wr, Fingers = -wu, Radius = 0.02f }   // sujeta el tambor por el lado izquierdo
                        : support;
                    switch (rl.LastEvent)
                    {
                        case ReloadEvent.CylinderOpen: case ReloadEvent.MagIn: return cyl;
                        case ReloadEvent.HandToBelt: return belt;
                        default: return support;
                    }
                }
                default:
                    switch (rl.LastEvent)
                    {
                        case ReloadEvent.BoltOpen: return support;   // el cerrojo lo mueve la mano derecha
                        case ReloadEvent.MagOut:                     // agarra el cargador, lo acompaña al salir y lo lleva a la cintura
                            if (rl.SinceEvent < 0.3f && parts != null && parts.Mag != null) { var m = mag; m.Point = parts.Mag.position; return m; }
                            return belt;
                        case ReloadEvent.HandToBelt: return rl.SinceEvent < 0.25f ? belt : mag;   // coge uno nuevo y lo lleva a la boca del cargador
                        case ReloadEvent.MagIn: return mag;
                        default:
                            if (rl.Style == ReloadStyle.BoltMagazine) return support;   // con cerrojo: la de apoyo sigue en el guardamano hasta sacar el cargador
                            // primero suelta el guardamano bajando y apartándose (no roza el cargador, que está justo detrás), luego agarra el cargador
                            if (rl.Progress < 0.03f) return support;
                            if (rl.Progress < 0.1f) { var drop = support; drop.Point = support.Point - wu * 0.1f - wr * 0.07f; return drop; }
                            return mag;
                    }
            }
        }

        /// <summary>Rotación de una mano: "back" = hacia dónde mira el dorso (+X local; la palma mira al contrario), "fingers" = hacia dónde apuntan los dedos (-Y local).</summary>
        public static Quaternion HandRot(Vector3 back, Vector3 fingers)
        {
            Vector3 y = -fingers.normalized;
            Vector3 x = Vector3.ProjectOnPlane(back, y);
            if (x.sqrMagnitude < 1e-5f) x = Vector3.Cross(y, Vector3.forward);
            x.Normalize();
            Vector3 z = Vector3.Cross(x, y);
            return Quaternion.LookRotation(z, y);
        }
    }
}
