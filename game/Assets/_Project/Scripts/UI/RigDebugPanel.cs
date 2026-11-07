using UnityEngine;
using SevenDays.Animation;
using SevenDays.Procedural;
using UnityEngine.InputSystem;

namespace SevenDays.UI
{
    /// <summary>
    /// Panel F6 de la animación procedural: fase y longitud de la zancada, apoyo, pesos de la mezcla, suavizados (editables), coste en ms (último, medio y total de
    /// todos los rigs), rayos al suelo, LOD, una prueba de carga con 20 rigs y una casilla para dibujar huesos y objetivos IK sobre la pantalla.
    /// </summary>
    public class RigDebugPanel : MonoBehaviour
    {
        private ProceduralRig _rig, _playerRig;
        private bool _open, _bones, _ground, _angles, _skeleton;
        private readonly float[] _kneeHistL = new float[150], _kneeHistR = new float[150];
        private int _histIndex;
        private SevenDays.Player.PlayerShooting _shooting;
        private SevenDays.Animation.PlayerRigDriver _driver;
        private GUIStyle _label, _head;
        private Vector2 _scroll;
        private RigStressTest _stress;
        private float _totalMs, _frameMs;

        public void Initialize(ProceduralRig rig)
        {
            _rig = _playerRig = rig;
            if (rig != null) { _shooting = rig.GetComponentInParent<SevenDays.Player.PlayerShooting>(); _driver = rig.GetComponentInParent<SevenDays.Animation.PlayerRigDriver>(); }
        }

        private void Update()
        {
            // con el ciclo F7 activo, el panel mide y dibuja sobre el soldado de la demostración
            _rig = RigDemoCycle.Current != null && RigDemoCycle.Current.Rig != null ? RigDemoCycle.Current.Rig : _playerRig;
            var k = Keyboard.current;
            if (k != null && k.f6Key.wasPressedThisFrame)
            {
                _open = !_open;
                Cursor.lockState = _open ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = _open;
            }
            float total = 0f;
            foreach (var r in ProceduralRig.Active) total += r.AverageCostMs / Mathf.Max(1, r.UpdateInterval);
            _totalMs = total;
            if (_rig != null) { _kneeHistL[_histIndex] = _rig.KneeFlexion[0]; _kneeHistR[_histIndex] = _rig.KneeFlexion[1]; _histIndex = (_histIndex + 1) % _kneeHistL.Length; }
            _frameMs = Mathf.Lerp(_frameMs, Time.unscaledDeltaTime * 1000f, 0.05f);
            // una línea por frame: pie (hueso, IK, malla) frente al suelo y rodilla (IK, hueso, malla) de cada pierna
            if (_skeleton && _rig != null && _rig.Bones != null) Debug.Log(RigProbe.LogLine(_rig));
        }

        private void OnGUI()
        {
            if (_rig == null) return;
            if (_bones && Event.current.type == EventType.Repaint) DrawBones();
            if (_ground && Event.current.type == EventType.Repaint) DrawGround();
            if (_angles && Event.current.type == EventType.Repaint) DrawAngles();
            if (_skeleton && Event.current.type == EventType.Repaint) DrawSkeletonVsMesh();
            if (!_open) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            _head ??= new GUIStyle(_label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.5f, 1f, 0.8f) } };
            var rect = new Rect(Screen.width - 440, 10, 430, Screen.height - 20);
            if (rect.Contains(Event.current.mousePosition)) SevenDays.Player.PlayerCamera.UiHoverUntil = Time.unscaledTime + 0.15f;
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12));
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("ANIMACIÓN PROCEDURAL (F6 cierra)", _head);
            GUILayout.Label("Coste del jugador: " + _rig.LastCostMs.ToString("0.000") + " ms (media " + _rig.AverageCostMs.ToString("0.000") + ")  rayos " + _rig.RaycastsLastUpdate + "  LOD cada " + _rig.UpdateInterval + " fr.", _label);
            GUILayout.Label("Todos los rigs (" + ProceduralRig.Active.Count + "): " + _totalMs.ToString("0.00") + " ms/frame  de " + _frameMs.ToString("0.0") + " ms totales (" + (1000f / Mathf.Max(1f, _frameMs)).ToString("0") + " FPS)", _label);
            GUILayout.Label("Zancada: fase " + _rig.GaitPhase.ToString("0.00") + "  ciclo " + _rig.CycleLength.ToString("0.00") + " m  apoyo " + (_rig.StanceFraction * 100f).ToString("0") + "%  velocidad " + _rig.MeasuredSpeed.ToString("0.00") + " m/s", _label);
            GUILayout.Label("Pies apoyados: izq " + _rig.FootPlanted[0] + "  der " + _rig.FootPlanted[1], _label);
            GUILayout.Label("Mezcla velocidad: idle " + P(_rig.SpeedWeights[0]) + " caminar " + P(_rig.SpeedWeights[1]) + " trotar " + P(_rig.SpeedWeights[2]) + " correr " + P(_rig.SpeedWeights[3]), _label);
            var d = _rig.DirectionWeights;
            GUILayout.Label("Dirección: N " + P(d[0]) + " NE " + P(d[1]) + " E " + P(d[2]) + " SE " + P(d[3]) + " S " + P(d[4]) + " SO " + P(d[5]) + " O " + P(d[6]) + " NO " + P(d[7]), _label);
            var i = _rig.Inputs;
            GUILayout.Label("Entradas: agachado " + i.Crouch.ToString("0.00") + "  apuntando " + i.Aiming + "  cobertura " + i.InCover + "  lean " + i.Lean.ToString("0.00") + "  manos " + i.RightWeight.ToString("0.0") + "/" + i.LeftWeight.ToString("0.0"), _label);

            _bones = GUILayout.Toggle(_bones, " Dibujar huesos y objetivos IK");
            _ground = GUILayout.Toggle(_ground, " Mostrar altura de suelo y mínimo de pies");
            _angles = GUILayout.Toggle(_angles, " Mostrar ángulos y objetivos de agarre");
            _skeleton = GUILayout.Toggle(_skeleton, " Esqueleto vs malla (y una línea por frame en la consola)");
            GUILayout.Label("Codos: izq " + _rig.ElbowFlexion[0].ToString("0") + "°  der " + _rig.ElbowFlexion[1].ToString("0") + "°", _label);
            GUILayout.Label("Flexión de rodilla (últimos 2.5 s; azul izq, naranja der, línea gris = 150°)", _label);
            Rect graph = GUILayoutUtility.GetRect(400f, 90f);
            if (Event.current.type == EventType.Repaint) DrawKneeGraph(graph);
            GUILayout.Label("Inclinación: total " + _rig.MoveLean.ToString("0.0") + "° (pelvis " + _rig.PelvisLean.ToString("0.0") + "°)  visible " + RigProbe.TorsoLean(_rig).ToString("0.0") + "°   pelvis " + ((_rig.Bone(BoneId.Hips).position.y - _rig.transform.position.y - _rig.StandPelvis) * 100f).ToString("+0.0;-0.0") + " cm vs de pie", _label);
            GUILayout.Label("Rodillas: izq " + _rig.KneeFlexion[0].ToString("0") + "°  der " + _rig.KneeFlexion[1].ToString("0") + "° (límite 0-" + LegSolver.MaxKneeFlexion.ToString("0") + "°)  arrodillado " + P(_rig.KneelWeight) + (_rig.PelvisLimited ? "  PELVIS LIMITADA" : ""), _label);
            _rig.DrawGizmos = _bones;

            GUILayout.Space(6);
            GUILayout.Label("Prueba de carga", _head);
            int target = _stress != null ? _stress.Count : 0;
            bool on = GUILayout.Toggle(_stress != null && _stress.isActiveAndEnabled && target > 0, " 20 soldados con rig caminando alrededor");
            if (on && (_stress == null || !_stress.isActiveAndEnabled))
            {
                if (_stress == null) _stress = RigStressTest.Create(_rig.transform);
                _stress.gameObject.SetActive(true);
                _stress.SetCount(20);
            }
            else if (!on && _stress != null && _stress.isActiveAndEnabled) _stress.gameObject.SetActive(false);

            GUILayout.Space(6);
            GUILayout.Label("Suavizados (s)", _head);
            var s = _rig.Smoothing;
            s.pelvis = Slider("pelvis", s.pelvis, 0f, 0.3f);
            s.footPlanted = Slider("pie apoyado", s.footPlanted, 0f, 0.1f);
            s.footSwing = Slider("pie en el aire", s.footSwing, 0f, 0.2f);
            s.torso = Slider("torso", s.torso, 0f, 0.4f);
            s.head = Slider("cabeza", s.head, 0f, 0.3f);
            s.hands = Slider("manos", s.hands, 0f, 0.2f);
            s.weights = Slider("pesos de agarre", s.weights, 0f, 0.5f);
            GUILayout.Space(6);
            GUILayout.Label("Pose de locomoción (LocomotionPoseSettings)", _head);
            var p = _rig.Pose;
            p.contactKnee = Slider("rodilla contacto", p.contactKnee, 0f, 40f);
            p.midStanceKnee = Slider("rodilla apoyo medio", p.midStanceKnee, 0f, 30f);
            p.toeOffKnee = Slider("rodilla despegue", p.toeOffKnee, 0f, 60f);
            p.walkSwingKnee = Slider("rodilla vuelo andando", p.walkSwingKnee, 20f, 150f);
            p.runSwingKnee = Slider("rodilla vuelo corriendo", p.runSwingKnee, 30f, 150f);
            p.walkThigh = Slider("muslo andando", p.walkThigh, 5f, 80f);
            p.runThigh = Slider("muslo corriendo", p.runThigh, 5f, 80f);
            p.heelStrikePitch = Slider("talón (punta arriba)", p.heelStrikePitch, 0f, 30f);
            p.toeOffPitch = Slider("punta al despegar", p.toeOffPitch, 0f, 45f);
            p.absorb = Slider("absorción pelvis (m)", p.absorb, 0f, 0.1f);
            p.runLean = Slider("Inclinación al correr", p.runLean, 0f, 15f);
            p.trotLean = Slider("inclinación al trotar", p.trotLean, 0f, 15f);
            p.walkLean = Slider("inclinación andando", p.walkLean, 0f, 15f);
            p.weaponLean = Slider("inclinación con arma", p.weaponLean, 0f, 15f);
            p.maxRunLean = Slider("inclinación máxima", p.maxRunLean, 0f, 15f);
            p.pelvisLeanShare = Slider("parte de la pelvis", p.pelvisLeanShare, 0f, 0.5f);
            p.standKnee = Slider("rodilla de pie", p.standKnee, 0f, 20f);
            p.runMidStanceKnee = Slider("rodilla apoyo corriendo", p.runMidStanceKnee, 0f, 60f);
            p.stanceDrop = Slider("caída pelvis al apoyar (m)", p.stanceDrop, 0.01f, 0.12f);
            p.kneelTorsoLean = Slider("torso media rodilla", p.kneelTorsoLean, 0f, 45f);
            p.Validate();

            // variante jugable: cambia ropa, cabeza, colores y estadísticas al momento
            if (_rig == _playerRig)
            {
                GUILayout.Space(6);
                GUILayout.Label("Variante (" + (_rig.Variant != null ? _rig.Variant.displayName : "-") + ")", _head);
                GUILayout.BeginHorizontal();
                foreach (var v in CharacterVariantLibrary.All)
                {
                    bool sel = _rig.Variant != null && _rig.Variant.id == v.id;
                    if (GUILayout.Toggle(sel, v.displayName, GUI.skin.button) && !sel)
                    {
                        CharacterVariantLibrary.SetCurrent(v.id);
                        RigVisuals.Rebuild(_rig, ProportionCopy(_rig.Proportions), v);
                        SevenDays.Player.PlayerVariantStats.Apply(_rig.GetComponentInParent<SevenDays.Player.PlayerMotor>()?.gameObject, v);
                    }
                }
                GUILayout.EndHorizontal();
                if (_rig.Variant != null)
                    GUILayout.Label("vida " + _rig.Variant.maxHealth.ToString("0") + "  velocidad x" + _rig.Variant.moveSpeed.ToString("0.00") + "  curación x" + _rig.Variant.healing.ToString("0.00") +
                                    "  balanceo x" + _rig.Variant.weaponSway.ToString("0.00") + "  triángulos " + RigVisuals.Triangles(_rig) + (CharacterArt.Busy ? "  (generando texturas...)" : ""), _label);
            }
            // complexión (ProportionSettings): se aplica reconstruyendo el esqueleto y las mallas del jugador
            if (_rig == _playerRig)
            {
                GUILayout.Space(6);
                GUILayout.Label("Complexión (ProportionSettings)", _head);
                _prop ??= ProportionCopy(_rig.Proportions);
                _prop.shoulderWidth = Slider("ancho de hombros", _prop.shoulderWidth, 0.3f, 0.7f);
                _prop.hipWidth = Slider("ancho de cadera", _prop.hipWidth, 0.18f, 0.4f);
                _prop.bulk = Slider("grosor general", _prop.bulk, 0.7f, 1.5f);
                _prop.torsoDepth = Slider("grosor del torso", _prop.torsoDepth, 0.6f, 1.4f);
                _prop.limbThickness = Slider("brazos y piernas", _prop.limbThickness, 0.6f, 1.4f);
                _prop.headSize = Slider("cabeza", _prop.headSize, 0.8f, 1.3f);
                _prop.armRatio = Slider("largo de brazos", _prop.armRatio, 0.85f, 1.15f);
                _prop.legRatio = Slider("largo de piernas", _prop.legRatio, 0.44f, 0.56f);
                if (GUILayout.Button("Aplicar complexión")) { _prop.Validate(); RigVisuals.Rebuild(_rig, ProportionCopy(_prop), _rig.Variant); }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawKneeGraph(Rect r)
        {
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f); GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            float y150 = r.yMax - r.height * (LegSolver.MaxKneeFlexion / 160f);
            GUI.DrawTexture(new Rect(r.x, y150, r.width, 1f), Texture2D.whiteTexture);
            GUI.color = prev;
            int n = _kneeHistL.Length;
            for (int k = 1; k < n; k++)
            {
                int i0 = (_histIndex + k - 1) % n, i1 = (_histIndex + k) % n;
                float x0 = r.x + r.width * (k - 1) / (n - 1), x1 = r.x + r.width * k / (n - 1);
                GraphLine(new Vector2(x0, r.yMax - r.height * _kneeHistL[i0] / 160f), new Vector2(x1, r.yMax - r.height * _kneeHistL[i1] / 160f), new Color(0.4f, 0.7f, 1f));
                GraphLine(new Vector2(x0, r.yMax - r.height * _kneeHistR[i0] / 160f), new Vector2(x1, r.yMax - r.height * _kneeHistR[i1] / 160f), new Color(1f, 0.6f, 0.2f));
            }
        }

        private static void GraphLine(Vector2 a, Vector2 b, Color c)
        {
            float len = (b - a).magnitude; if (len < 0.1f) return;
            var m = GUI.matrix; var prev = GUI.color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, a);
            GUI.color = c; GUI.DrawTexture(new Rect(a.x, a.y - 0.75f, len, 1.5f), Texture2D.whiteTexture);
            GUI.matrix = m; GUI.color = prev;
        }

        /// <summary>Ángulo de cada rodilla y codo junto a la articulación, puntos de agarre del arma (verde: empuñadura, azul: guardamano, magenta: culata) y manos (amarillo).</summary>
        private void DrawAngles()
        {
            var cam = Cam();
            if (cam == null || _rig.Bones == null) return;
            Label(cam, _rig.Bone(BoneId.LeftLowerLeg).position, _rig.KneeFlexion[0].ToString("0") + "°", _rig.KneeFlexion[0] > 145f ? Color.red : Color.green);
            Label(cam, _rig.Bone(BoneId.RightLowerLeg).position, _rig.KneeFlexion[1].ToString("0") + "°", _rig.KneeFlexion[1] > 145f ? Color.red : Color.green);
            Label(cam, _rig.Bone(BoneId.LeftLowerArm).position, _rig.ElbowFlexion[0].ToString("0") + "°", Color.cyan);
            Label(cam, _rig.Bone(BoneId.RightLowerArm).position, _rig.ElbowFlexion[1].ToString("0") + "°", Color.cyan);
            if (_shooting != null && _driver != null && _driver.WeaponPose != null)
            {
                var d = _shooting.Current.Data; var g = d.grip ?? WeaponGrip.For(d.kind, d.length, d.thickness);
                var pose = _driver.WeaponPose;
                Dot(cam, pose.TransformPoint(new Vector3(g.grip[0], g.grip[1], g.grip[2])), Color.green, 8f);
                Dot(cam, pose.TransformPoint(new Vector3(g.foregrip[0], g.foregrip[1], g.foregrip[2])), new Color(0.3f, 0.6f, 1f), 8f);
                if (g.shouldered) Dot(cam, pose.TransformPoint(new Vector3(g.stock[0], g.stock[1], g.stock[2])), Color.magenta, 8f);
            }
            for (int h = 0; h < 2; h++) Dot(cam, _rig.HandTargets[h], Color.yellow, 6f);
        }

        /// <summary>
        /// Esqueleto vs malla: huesos reales (cian), cadena que calculó el IK (magenta: cadera, rodilla y tobillo), eje de cada malla de la pierna (blanco) y, junto
        /// a la rodilla, la flexión según el IK, los huesos y las mallas (deben coincidir). Bajo el pie, cuánto queda el vértice más bajo sobre el suelo (rojo si está debajo).
        /// </summary>
        private void DrawSkeletonVsMesh()
        {
            var cam = Cam();
            if (cam == null || _rig.Bones == null) return;
            for (int b = 0; b < _rig.Bones.Length; b++)
            {
                int parent = _rig.Spec[b].Parent;
                if (parent >= 0) Line(cam, _rig.Bones[parent].position, _rig.Bones[b].position, new Color(0.3f, 1f, 1f));
            }
            for (int f = 0; f < 2; f++)
            {
                bool left = f == 0;
                Transform hip = _rig.Bone(left ? BoneId.LeftUpperLeg : BoneId.RightUpperLeg), knee = _rig.Bone(left ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg);
                Line(cam, hip.position, _rig.IkKnees[f], Color.magenta); Line(cam, _rig.IkKnees[f], _rig.IkAnkles[f], Color.magenta);
                var thigh = RigProbe.Limb(hip); var shin = RigProbe.Limb(knee);
                if (thigh != null) Line(cam, hip.position, hip.position + (RigProbe.Centroid(thigh) - hip.position) * 2f, Color.white);
                if (shin != null) Line(cam, knee.position, knee.position + (RigProbe.Centroid(shin) - knee.position) * 2f, Color.white);
                var r = RigProbe.Leg(_rig, f);
                bool match = Mathf.Abs(r.IkKnee - r.BoneKnee) < 3f && Mathf.Abs(r.IkKnee - r.MeshKnee) < 3f;
                Label(cam, knee.position, "IK " + r.IkKnee.ToString("0") + "° hueso " + r.BoneKnee.ToString("0") + "° malla " + r.MeshKnee.ToString("0") + "°", match ? Color.green : Color.red, 230f);
                float clearance = (r.LowestMeshY - r.GroundY) * 100f;
                if (r.GroundY > _rig.transform.position.y - 5f)
                    Label(cam, _rig.Bone(left ? BoneId.LeftFoot : BoneId.RightFoot).position, "pie " + clearance.ToString("+0.0;-0.0") + " cm", clearance < -0.2f ? Color.red : Color.green, 120f);
            }
        }

        private static void Label(Camera cam, Vector3 p, string text, Color c, float width = 60f)
        {
            Vector3 s = cam.WorldToScreenPoint(p);
            if (s.z <= 0f) return;
            var prev = GUI.color; GUI.color = c;
            GUI.Label(new Rect(s.x + 6f, Screen.height - s.y - 10f, width, 20f), text);
            GUI.color = prev;
        }

        private RigProportions _prop;
        private static RigProportions ProportionCopy(RigProportions p) => JsonUtility.FromJson<RigProportions>(JsonUtility.ToJson(p));

        private static Camera Cam() => RigDemoCycle.Current != null && RigDemoCycle.Current.SideCamera != null ? RigDemoCycle.Current.SideCamera : Camera.main;

        private static string P(float v) => (v * 100f).ToString("0") + "%";

        private float Slider(string name, float v, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, _label, GUILayout.Width(130));
            v = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(180));
            GUILayout.Label(v.ToString("0.000"), _label, GUILayout.Width(60));
            GUILayout.EndHorizontal();
            return v;
        }

        /// <summary>Huesos (cian), objetivos de los pies (verde: apoyado, rojo: en el aire) y de las manos (amarillo) proyectados en pantalla.</summary>
        private void DrawBones()
        {
            var cam = Cam();
            if (cam == null || _rig.Bones == null) return;
            for (int b = 0; b < _rig.Bones.Length; b++)
            {
                int parent = _rig.Spec[b].Parent;
                if (parent < 0) continue;
                Line(cam, _rig.Bones[parent].position, _rig.Bones[b].position, new Color(0.3f, 1f, 1f));
            }
            for (int f = 0; f < 2; f++) Dot(cam, _rig.FootTargets[f], _rig.FootPlanted[f] ? Color.green : Color.red, 8f);
            for (int h = 0; h < 2; h++) Dot(cam, _rig.HandTargets[h], Color.yellow, 7f);
        }

        /// <summary>
        /// Bajo cada pie: el plano del suelo (cruz blanca), el mínimo del tobillo (línea naranja: suelo + tobillo + suela) y el tobillo real (punto verde si está por
        /// encima, rojo si estuviera por debajo). Junto a cada rodilla, su flexión con color según el límite.
        /// </summary>
        private void DrawGround()
        {
            var cam = Cam();
            if (cam == null || _rig.Bones == null) return;
            float ankleH = _rig.Spec[(int)BoneId.LeftFoot].Length;
            for (int f = 0; f < 2; f++)
            {
                var ankle = _rig.Bone(f == 0 ? BoneId.LeftFoot : BoneId.RightFoot).position;
                var knee = _rig.Bone(f == 0 ? BoneId.LeftLowerLeg : BoneId.RightLowerLeg).position;
                float g = _rig.FootGround[f];
                if (g < _rig.transform.position.y - 5f) continue;   // en el aire
                Vector3 ground = new Vector3(ankle.x, g, ankle.z);
                Vector3 r = _rig.transform.right * 0.15f, fw = _rig.transform.forward * 0.15f;
                Line(cam, ground - r, ground + r, Color.white); Line(cam, ground - fw, ground + fw, Color.white);
                Vector3 min = ground + Vector3.up * (ankleH + LegSolver.Sole);
                Line(cam, min - r * 0.8f, min + r * 0.8f, new Color(1f, 0.6f, 0.1f));
                Dot(cam, ankle, ankle.y >= min.y - 0.005f ? Color.green : Color.red, 7f);
                float flex = _rig.KneeFlexion[f];
                Vector3 sk = cam.WorldToScreenPoint(knee);
                if (sk.z > 0f)
                {
                    var prev = GUI.color;
                    GUI.color = flex > LegSolver.MaxKneeFlexion - 5f ? Color.red : (flex > 120f ? Color.yellow : Color.green);
                    GUI.Label(new Rect(sk.x + 6f, Screen.height - sk.y - 10f, 60f, 20f), flex.ToString("0") + "°");
                    GUI.color = prev;
                }
            }
        }

        private static void Line(Camera cam, Vector3 a, Vector3 b, Color c)
        {
            Vector3 sa = cam.WorldToScreenPoint(a), sb = cam.WorldToScreenPoint(b);
            if (sa.z <= 0f || sb.z <= 0f) return;
            Vector2 pa = new Vector2(sa.x, Screen.height - sa.y), pb = new Vector2(sb.x, Screen.height - sb.y);
            float len = (pb - pa).magnitude;
            if (len < 0.5f) return;
            float ang = Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg;
            var m = GUI.matrix; var prev = GUI.color;
            GUIUtility.RotateAroundPivot(ang, pa);
            GUI.color = c; GUI.DrawTexture(new Rect(pa.x, pa.y - 1f, len, 2f), Texture2D.whiteTexture);
            GUI.matrix = m; GUI.color = prev;
        }

        private static void Dot(Camera cam, Vector3 p, Color c, float size)
        {
            Vector3 s = cam.WorldToScreenPoint(p);
            if (s.z <= 0f) return;
            var prev = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(s.x - size * 0.5f, Screen.height - s.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
