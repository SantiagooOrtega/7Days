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
        private ProceduralRig _rig;
        private bool _open, _bones, _ground;
        private GUIStyle _label, _head;
        private Vector2 _scroll;
        private RigStressTest _stress;
        private float _totalMs, _frameMs;

        public void Initialize(ProceduralRig rig) => _rig = rig;

        private void Update()
        {
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
            _frameMs = Mathf.Lerp(_frameMs, Time.unscaledDeltaTime * 1000f, 0.05f);
        }

        private void OnGUI()
        {
            if (_rig == null) return;
            if (_bones && Event.current.type == EventType.Repaint) DrawBones();
            if (_ground && Event.current.type == EventType.Repaint) DrawGround();
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
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

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
            var cam = Camera.main;
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
            var cam = Camera.main;
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
