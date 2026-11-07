using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using SevenDays.Animation;
using SevenDays.Procedural;

namespace SevenDays.UI
{
    /// <summary>
    /// F7: ciclo automático de la animación de piernas, sin tocar al jugador. Aparece un soldado junto al jugador que camina, corre, corre con arma, se pone en
    /// media rodilla y se agacha (5 s cada uno, en bucle), visto de lado con una cámara propia. En pantalla, en vivo, la rodilla según el IK, los huesos y las mallas,
    /// la distancia de la bota al suelo, la inclinación del torso y la altura de la pelvis; y una tabla con lo medido en cada tramo. Con F6 abierto y "Esqueleto vs
    /// malla" marcado, el panel dibuja sobre este soldado.
    /// </summary>
    public class RigDemoCycle : MonoBehaviour
    {
        public static RigDemoCycle Current { get; private set; }
        public ProceduralRig Rig { get; private set; }
        public Camera SideCamera { get; private set; }

        private static readonly RigScenarioMode[] Sequence = { RigScenarioMode.Walk, RigScenarioMode.Run, RigScenarioMode.RunWeapon, RigScenarioMode.HalfKneel, RigScenarioMode.Squat };
        private static readonly string[] Names = { "Caminar", "Correr", "Correr con arma", "Media rodilla", "Agachado (Ctrl)" };
        private const float SegmentTime = 5f, Lane = 8f;

        private Transform _player;
        private Vector3 _origin, _axis;
        private float _dir = 1f, _t, _stand;
        private int _index;
        private readonly string[] _results = new string[Sequence.Length];
        // medidas del tramo actual (se ignora el primer segundo: transición)
        private float _kneeMax, _kneeMin, _leanSum, _pelvisLo, _pelvisHi, _clearMin;
        private int _samples;
        private GUIStyle _style;

        public void Initialize(Transform player) => _player = player;

        private void Update()
        {
            var k = Keyboard.current;
            if (k != null && k.f7Key.wasPressedThisFrame) { if (Rig == null) Begin(); else End(); }
            if (Rig == null) return;

            var mode = Sequence[_index];
            float dt = Time.deltaTime;
            // va y viene por un carril de 8 m; al llegar al final da media vuelta (el rig gira con pasos)
            Transform t = Rig.transform;
            float along = Vector3.Dot(t.position - _origin, _axis);
            if (along > Lane && _dir > 0f || along < 0f && _dir < 0f) _dir = -_dir;
            t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(_axis * _dir), 400f * dt);
            t.position += t.forward * RigScenario.Speed(mode) * dt;
            if (Physics.Raycast(t.position + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~((1 << 2) | (1 << 31)), QueryTriggerInteraction.Ignore))
                t.position = new Vector3(t.position.x, hit.point.y, t.position.z);
            RigScenario.Drive(Rig, mode);   // el rig avanza solo en su LateUpdate

            _t += dt;
            if (_t > 1f) Sample();
            if (_t >= SegmentTime) { _results[_index] = Summary(); _index = (_index + 1) % Sequence.Length; _t = 0f; ResetSamples(); }
        }

        private void LateUpdate()
        {
            if (Rig == null) return;
            // cámara lateral fija respecto al carril (no gira con el soldado): vista de perfil a la altura de la cadera
            Vector3 center = Rig.transform.position + Vector3.up * 0.85f;
            SideCamera.transform.position = center + Vector3.Cross(Vector3.up, _axis).normalized * 4.2f + Vector3.up * 0.1f;
            SideCamera.transform.LookAt(center);
        }

        private void Begin()
        {
            if (_player == null) return;
            _axis = Vector3.ProjectOnPlane(_player.forward, Vector3.up).normalized;
            if (_axis.sqrMagnitude < 0.5f) _axis = Vector3.forward;
            _origin = _player.position + Vector3.Cross(Vector3.up, _axis) * 3f;
            Rig = RigScenario.CreateSoldier(_origin);
            Rig.transform.rotation = Quaternion.LookRotation(_axis);
            Rig.Pose = LocomotionPoseSettings.Load();
            Rig.ResetPose();
            _stand = Rig.StandPelvis;
            var cam = new GameObject("Cámara lateral F7");
            SideCamera = cam.AddComponent<Camera>();
            SideCamera.depth = 50f;           // se dibuja encima de la cámara del jugador
            SideCamera.fieldOfView = 38f;
            SideCamera.nearClipPlane = 0.1f;
            _index = 0; _t = 0f; _dir = 1f;
            for (int i = 0; i < _results.Length; i++) _results[i] = null;
            ResetSamples();
            Current = this;
        }

        private void End()
        {
            if (Rig != null) Destroy(Rig.gameObject);
            if (SideCamera != null) Destroy(SideCamera.gameObject);
            Rig = null; SideCamera = null;
            if (Current == this) Current = null;
        }

        private void OnDisable() => End();

        private void ResetSamples() { _kneeMax = 0f; _kneeMin = 999f; _leanSum = 0f; _pelvisLo = 99f; _pelvisHi = -99f; _clearMin = 99f; _samples = 0; }

        private void Sample()
        {
            for (int f = 0; f < 2; f++)
            {
                var r = RigProbe.Leg(Rig, f);
                _kneeMax = Mathf.Max(_kneeMax, r.MeshKnee); _kneeMin = Mathf.Min(_kneeMin, r.MeshKnee);
                _clearMin = Mathf.Min(_clearMin, r.LowestMeshY - r.GroundY);
            }
            _leanSum += RigProbe.TorsoLean(Rig);
            float py = Rig.Bone(BoneId.Hips).position.y - Rig.transform.position.y;
            _pelvisLo = Mathf.Min(_pelvisLo, py); _pelvisHi = Mathf.Max(_pelvisHi, py);
            _samples++;
        }

        private string Summary()
        {
            if (_samples == 0) return "-";
            return "rodilla " + _kneeMin.ToString("0") + "-" + _kneeMax.ToString("0") + "°  torso " + (_leanSum / _samples).ToString("0.0") + "°  pelvis " +
                   ((_pelvisHi - _stand) * 100f).ToString("+0.0;-0.0") + " cm (rebote " + ((_pelvisHi - _pelvisLo) * 100f).ToString("0.0") + ")  bota-suelo mín " + (_clearMin * 100f).ToString("+0.0;-0.0") + " cm";
        }

        private void OnGUI()
        {
            if (Rig == null) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            var sb = new StringBuilder(600);
            sb.Append("F7 · CICLO AUTOMÁTICO (F7 sale)   ").Append(Names[_index]).Append("  ").Append(_t.ToString("0.0")).Append(" / ").Append(SegmentTime.ToString("0")).Append(" s\n");
            for (int f = 0; f < 2; f++)
            {
                var r = RigProbe.Leg(Rig, f);
                sb.Append(f == 0 ? "Izq" : "Der").Append(": rodilla IK ").Append(r.IkKnee.ToString("0")).Append("°  hueso ").Append(r.BoneKnee.ToString("0"))
                  .Append("°  malla ").Append(r.MeshKnee.ToString("0")).Append("°   bota-suelo ").Append(((r.LowestMeshY - r.GroundY) * 100f).ToString("+0.0;-0.0")).Append(" cm\n");
            }
            sb.Append("Torso ").Append(RigProbe.TorsoLean(Rig).ToString("0.0")).Append("° (pelvis ").Append(Rig.PelvisLean.ToString("0.0")).Append("°)   pelvis ")
              .Append(((Rig.Bone(BoneId.Hips).position.y - Rig.transform.position.y - _stand) * 100f).ToString("+0.0;-0.0")).Append(" cm respecto a de pie\n\nÚltimas medidas por tramo:\n");
            for (int i = 0; i < Sequence.Length; i++) sb.Append(Names[i]).Append(": ").Append(_results[i] ?? "(pendiente)").Append('\n');
            var rect = new Rect(10, 10, 760, 250);
            var prev = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = prev;
            GUI.Label(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12), sb.ToString(), _style);
        }
    }
}
