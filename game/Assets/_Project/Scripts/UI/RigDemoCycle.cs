using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using SevenDays.Animation;
using SevenDays.Procedural;
using SevenDays.Weapons;

namespace SevenDays.UI
{
    /// <summary>
    /// F7: ciclo automático de animación, sin tocar al jugador, con cámara propia. Tres partes en bucle:
    ///  1) PIERNAS (de lado): caminar, correr, correr con arma, media rodilla y agachado, con rodilla IK/hueso/malla, bota-suelo, torso y pelvis medidos.
    ///  2) ARMAS (cerca de las manos): con cada arma, sacarla, a la cadera, apuntar, disparar, recargar y bajarla corriendo, con la distancia de cada mano a su
    ///     punto de agarre, dedos y arma dentro de algo y codos (GripProbe, el mismo código que las pruebas).
    ///  3) COMPLEXIÓN: el soldado de frente y de lado.
    /// Con F6 abierto y "Esqueleto vs malla" marcado, el panel dibuja sobre el soldado de la demostración. Las teclas [ y ] saltan de tramo.
    /// </summary>
    public class RigDemoCycle : MonoBehaviour
    {
        public static RigDemoCycle Current { get; private set; }
        /// <summary>El rig que se está mostrando (el del tramo actual).</summary>
        public ProceduralRig Rig => _weapon != null && _weapon.Rig.gameObject.activeSelf ? _weapon.Rig : _legs;
        public Camera SideCamera { get; private set; }

        private enum Kind { Legs, Weapon, Front, Side }
        private struct Segment { public Kind Kind; public string Name; public float Time; public RigScenarioMode Mode; public WeaponData Weapon; public GripStep Step; }

        private readonly List<Segment> _segments = new List<Segment>();
        private const float Lane = 8f;
        private Transform _player;
        private ProceduralRig _legs;
        private WeaponScenario _weapon;
        private Vector3 _origin, _axis;
        private float _dir = 1f, _t, _stand;
        private int _index;
        private readonly Dictionary<string, string> _results = new Dictionary<string, string>();
        private float _kneeMax, _kneeMin, _leanSum, _pelvisLo, _pelvisHi, _clearMin, _gripMax, _fingerMax;
        private int _samples;
        private GUIStyle _style;

        public void Initialize(Transform player) => _player = player;

        private void BuildSegments()
        {
            _segments.Clear();
            void Leg(RigScenarioMode m, string n) => _segments.Add(new Segment { Kind = Kind.Legs, Name = n, Time = 5f, Mode = m });
            Leg(RigScenarioMode.Walk, "Caminar"); Leg(RigScenarioMode.Run, "Correr"); Leg(RigScenarioMode.RunWeapon, "Correr con arma");
            Leg(RigScenarioMode.HalfKneel, "Media rodilla"); Leg(RigScenarioMode.Squat, "Agachado (Ctrl)");
            var weapons = new List<WeaponData>(WeaponLibrary.All);
            if (weapons.Count == 0) weapons = WeaponCatalog.Defaults();
            foreach (var w in weapons)
            {
                void W(GripStep s, string n, float t) => _segments.Add(new Segment { Kind = Kind.Weapon, Name = w.displayName + ": " + n, Time = t, Weapon = w, Step = s });
                W(GripStep.Draw, "sacar", 1.2f); W(GripStep.Hip, "sin apuntar", 1.6f); W(GripStep.Aim, "apuntando", 1.6f);
                if (w.IsMelee) { W(GripStep.Melee, "golpe", 1.2f); W(GripStep.HeavyMelee, "golpe cargado", 1.4f); }
                else { W(GripStep.Fire, "disparando", 1.8f); W(GripStep.Reload, "recargando", w.reloadTime + 0.6f); }
                W(GripStep.LowReady, "bajar el arma (correr)", 1.6f);
            }
            _segments.Add(new Segment { Kind = Kind.Front, Name = "Complexión: de frente", Time = 3f });
            _segments.Add(new Segment { Kind = Kind.Side, Name = "Complexión: de lado", Time = 3f });
        }

        private void Update()
        {
            var k = Keyboard.current;
            if (k != null && k.f7Key.wasPressedThisFrame) { if (_legs == null) Begin(); else End(); }
            if (_legs == null) return;
            if (k != null && k.rightBracketKey.wasPressedThisFrame) Next(1);
            if (k != null && k.leftBracketKey.wasPressedThisFrame) Next(-1);

            var seg = _segments[_index];
            float dt = Time.deltaTime;
            _t += dt;
            if (seg.Kind == Kind.Weapon)
            {
                _weapon.Tick(dt);
                if (seg.Step == GripStep.LowReady && Vector3.Distance(_weapon.Rig.transform.position, _origin) > Lane) _weapon.Rig.transform.position = _origin;   // vuelve al inicio del carril
            }
            else
            {
                var mode = seg.Kind == Kind.Legs ? seg.Mode : RigScenarioMode.Idle;
                Transform t = _legs.transform;
                // va y viene por un carril; al llegar al final da media vuelta (el rig gira con pasos)
                float along = Vector3.Dot(t.position - _origin, _axis);
                if (along > Lane && _dir > 0f || along < 0f && _dir < 0f) _dir = -_dir;
                t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(_axis * _dir), 400f * dt);
                t.position += t.forward * RigScenario.Speed(mode) * dt;
                if (Physics.Raycast(t.position + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~((1 << 2) | (1 << 31)), QueryTriggerInteraction.Ignore))
                    t.position = new Vector3(t.position.x, hit.point.y, t.position.z);
                RigScenario.Drive(_legs, mode);   // el rig de piernas avanza solo en su LateUpdate
            }
            if (_t > Mathf.Min(1f, seg.Time * 0.4f)) Sample(seg);
            if (_t >= seg.Time) { _results[seg.Name] = Summary(seg); Next(1); }
        }

        private void Next(int step)
        {
            _index = (_index + step + _segments.Count) % _segments.Count;
            _t = 0f; ResetSamples();
            var seg = _segments[_index];
            bool weapon = seg.Kind == Kind.Weapon;
            _legs.gameObject.SetActive(!weapon);
            _weapon.Rig.gameObject.SetActive(weapon);
            if (weapon)
            {
                if (_weapon.Data != seg.Weapon) _weapon.Equip(seg.Weapon, false);
                _weapon.Rig.transform.SetPositionAndRotation(_origin, Quaternion.LookRotation(_axis));
                _weapon.SetStep(seg.Step);
            }
        }

        private void LateUpdate()
        {
            if (_legs == null) return;
            var seg = _segments[_index];
            var rig = Rig;
            Vector3 center = rig.transform.position + Vector3.up * 0.85f;
            Vector3 side = Vector3.Cross(Vector3.up, _axis).normalized;
            switch (seg.Kind)
            {
                case Kind.Weapon:
                {
                    // cerca de las manos: un poco delante y a la derecha, mirando al arma
                    Vector3 gun = _weapon.Pose.position;
                    Vector3 f = rig.transform.forward, r = rig.transform.right;
                    SideCamera.transform.position = gun + r * 0.75f + f * 0.45f + Vector3.up * 0.12f;
                    SideCamera.transform.LookAt(gun + f * 0.1f);
                    SideCamera.fieldOfView = 40f;
                    break;
                }
                case Kind.Front:
                    SideCamera.transform.position = center + rig.transform.forward * 3.4f + Vector3.up * 0.15f;
                    SideCamera.transform.LookAt(center + Vector3.up * 0.1f);
                    SideCamera.fieldOfView = 38f;
                    break;
                default:   // piernas y complexión de lado: vista de perfil fija respecto al carril
                    SideCamera.transform.position = center + side * 4.2f + Vector3.up * 0.1f;
                    SideCamera.transform.LookAt(center);
                    SideCamera.fieldOfView = 38f;
                    break;
            }
        }

        private void Begin()
        {
            if (_player == null) return;
            _axis = Vector3.ProjectOnPlane(_player.forward, Vector3.up).normalized;
            if (_axis.sqrMagnitude < 0.5f) _axis = Vector3.forward;
            _origin = _player.position + Vector3.Cross(Vector3.up, _axis) * 3f;
            BuildSegments();
            _legs = RigScenario.CreateSoldier(_origin);
            _legs.transform.rotation = Quaternion.LookRotation(_axis);
            _legs.Pose = LocomotionPoseSettings.Load();
            _legs.ResetPose();
            _stand = _legs.StandPelvis;
            _weapon = WeaponScenario.Create(_segments.Find(s => s.Kind == Kind.Weapon).Weapon ?? WeaponCatalog.Defaults()[0], _origin);
            _weapon.Rig.Pose = _legs.Pose;
            var cam = new GameObject("Cámara lateral F7");
            SideCamera = cam.AddComponent<Camera>();
            SideCamera.depth = 50f;           // se dibuja encima de la cámara del jugador
            SideCamera.nearClipPlane = 0.05f;
            _index = -1; _dir = 1f; _results.Clear();
            Current = this;
            Next(1);
        }

        private void End()
        {
            if (_legs != null) Kill(_legs.gameObject);
            _weapon?.Destroy();
            if (SideCamera != null) Kill(SideCamera.gameObject);
            _legs = null; _weapon = null; SideCamera = null;
            if (Current == this) Current = null;
        }

        private void OnDisable() => End();

        private static void Kill(GameObject go) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }

        private void ResetSamples() { _kneeMax = 0f; _kneeMin = 999f; _leanSum = 0f; _pelvisLo = 99f; _pelvisHi = -99f; _clearMin = 99f; _gripMax = 0f; _fingerMax = 0f; _samples = 0; }

        private void Sample(Segment seg)
        {
            if (seg.Kind == Kind.Weapon)
            {
                var g = GripProbe.Measure(_weapon);
                _gripMax = Mathf.Max(_gripMax, Mathf.Max(g.RightError, g.LeftError));
                _fingerMax = Mathf.Max(_fingerMax, g.FingerPenetration);
                _samples++;
                return;
            }
            var rig = _legs;
            for (int f = 0; f < 2; f++)
            {
                var r = RigProbe.Leg(rig, f);
                _kneeMax = Mathf.Max(_kneeMax, r.MeshKnee); _kneeMin = Mathf.Min(_kneeMin, r.MeshKnee);
                _clearMin = Mathf.Min(_clearMin, r.LowestMeshY - r.GroundY);
            }
            _leanSum += RigProbe.TorsoLean(rig);
            float py = rig.Bone(BoneId.Hips).position.y - rig.transform.position.y;
            _pelvisLo = Mathf.Min(_pelvisLo, py); _pelvisHi = Mathf.Max(_pelvisHi, py);
            _samples++;
        }

        private string Summary(Segment seg)
        {
            if (_samples == 0) return "-";
            if (seg.Kind == Kind.Weapon)
                return "mano-agarre máx " + (_gripMax * 100f).ToString("0.0") + " cm  dedos dentro máx " + (_fingerMax * 100f).ToString("0.0") + " cm";
            if (seg.Kind != Kind.Legs) return "ok";
            return "rodilla " + _kneeMin.ToString("0") + "-" + _kneeMax.ToString("0") + "°  torso " + (_leanSum / _samples).ToString("0.0") + "°  pelvis " +
                   ((_pelvisHi - _stand) * 100f).ToString("+0.0;-0.0") + " cm (rebote " + ((_pelvisHi - _pelvisLo) * 100f).ToString("0.0") + ")  bota-suelo mín " + (_clearMin * 100f).ToString("+0.0;-0.0") + " cm";
        }

        private void OnGUI()
        {
            if (_legs == null) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            var seg = _segments[_index];
            var sb = new StringBuilder(900);
            sb.Append("F7 · CICLO AUTOMÁTICO (F7 sale, [ ] cambian de tramo)   ").Append(_index + 1).Append('/').Append(_segments.Count).Append("  ").Append(seg.Name)
              .Append("  ").Append(_t.ToString("0.0")).Append(" / ").Append(seg.Time.ToString("0.0")).Append(" s\n");
            if (seg.Kind == Kind.Weapon)
            {
                sb.Append(GripProbe.Line(_weapon)).Append('\n');
            }
            else
            {
                for (int f = 0; f < 2; f++)
                {
                    var r = RigProbe.Leg(_legs, f);
                    sb.Append(f == 0 ? "Izq" : "Der").Append(": rodilla IK ").Append(r.IkKnee.ToString("0")).Append("°  hueso ").Append(r.BoneKnee.ToString("0"))
                      .Append("°  malla ").Append(r.MeshKnee.ToString("0")).Append("°   bota-suelo ").Append(((r.LowestMeshY - r.GroundY) * 100f).ToString("+0.0;-0.0")).Append(" cm\n");
                }
                sb.Append("Torso ").Append(RigProbe.TorsoLean(_legs).ToString("0.0")).Append("° (pelvis ").Append(_legs.PelvisLean.ToString("0.0")).Append("°)   pelvis ")
                  .Append(((_legs.Bone(BoneId.Hips).position.y - _legs.transform.position.y - _stand) * 100f).ToString("+0.0;-0.0")).Append(" cm respecto a de pie\n");
            }
            sb.Append("\nÚltimas medidas:\n");
            int shown = 0;
            for (int i = 1; i <= _segments.Count && shown < 6; i++)
            {
                var s = _segments[(_index - i + _segments.Count) % _segments.Count];
                if (_results.TryGetValue(s.Name, out var res)) { sb.Append(s.Name).Append(": ").Append(res).Append('\n'); shown++; }
            }
            var rect = new Rect(10, 10, 900, 230);
            var prev = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = prev;
            GUI.Label(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12), sb.ToString(), _style);
        }
    }
}
