using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.Procedural;

namespace SevenDays.Combat
{
    /// <summary>
    /// Respuesta visual y sonora de los impactos: números de daño flotantes (blanco al cuerpo, amarillo con "CABEZA" al headshot, gris si el daño se redujo),
    /// sonidos distintos (impacto, headshot, muerte) y los tiempos del hitmarker de la mira (normal y de muerte). Los números salen de un buffer circular fijo y
    /// los de una misma ráfaga sobre el mismo zombi se suman (la escopeta no llena la pantalla de 8 números).
    /// </summary>
    public class CombatFeedback : MonoBehaviour
    {
        public static CombatFeedback Instance { get; private set; }
        public float HitUntil { get; private set; }
        public float KillUntil { get; private set; }
        public float HeadUntil { get; private set; }

        private struct Popup { public Vector3 Pos; public float Damage; public float Born; public FeedbackMath.NumberKind Kind; public bool Alive; public string Text; }
        private const int MaxPopups = 24;
        private const float Life = 0.85f;
        private readonly Popup[] _popups = new Popup[MaxPopups];
        private int _nextPopup;
        private static readonly string[] IntStrings = BuildIntStrings(400);

        private struct Pending { public GameObject Target; public Vector3 Pos; public float Damage; public HitReport Report; public bool Killed; }
        private readonly List<Pending> _pending = new List<Pending>(16);

        private AudioSource _audio;
        private AudioClip _hit, _head, _kill, _killHead;
        private Camera _cam;
        private GUIStyle _style;
        private PlayerShooting _shooting;

        public static CombatFeedback Create(Transform parent, PlayerShooting shooting)
        {
            var go = new GameObject("CombatFeedback");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<CombatFeedback>();
            f.Init(shooting);
            return f;
        }

        private void Init(PlayerShooting shooting)
        {
            Instance = this;
            _shooting = shooting;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f; _audio.playOnAwake = false;
            AudioMix.Route(_audio, AudioGroup.UI);
            _hit = Tone("hit", 0.06f, 520f, 260f, 0.35f, 0.5f);
            _head = Tone("head", 0.16f, 1500f, 900f, 0.5f, 0.1f);       // más agudo y largo: se distingue sin mirar
            _kill = Tone("kill", 0.18f, 180f, 70f, 0.6f, 0.35f);
            _killHead = Tone("killhead", 0.24f, 1100f, 120f, 0.6f, 0.25f);
            shooting.ShotHitReported += OnShotHit;
        }

        private void OnDestroy() { if (_shooting != null) _shooting.ShotHitReported -= OnShotHit; if (Instance == this) Instance = null; }

        private void OnShotHit(ShotHit s)
        {
            if (!s.Report.Valid) return;
            // junta los perdigones de la misma ráfaga sobre el mismo zombi
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].Target != s.Target) continue;
                var p = _pending[i];
                p.Damage += s.Report.FinalDamage; p.Killed |= s.Report.Killed;
                if (s.Report.Headshot) p.Report.Headshot = true;
                p.Report.Reduced &= s.Report.Reduced;
                _pending[i] = p;
                return;
            }
            _pending.Add(new Pending { Target = s.Target, Pos = s.Point, Damage = s.Report.FinalDamage, Report = s.Report, Killed = s.Report.Killed });
        }

        private void LateUpdate()
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                var p = _pending[i];
                var r = p.Report; r.FinalDamage = p.Damage; r.Killed = p.Killed;
                Show(p.Pos, p.Damage, FeedbackMath.NumberFor(r));
                float t = Time.time;
                HitUntil = t + 0.12f;
                if (r.Headshot) HeadUntil = t + 0.25f;
                if (p.Killed) KillUntil = t + 0.35f;
                _audio.PlayOneShot(p.Killed ? (r.Headshot ? _killHead : _kill) : (r.Headshot ? _head : _hit), p.Killed ? 0.9f : 0.7f);
            }
            _pending.Clear();
        }

        private void Show(Vector3 pos, float damage, FeedbackMath.NumberKind kind)
        {
            int d = Mathf.RoundToInt(damage);
            string text = (d >= 0 && d < IntStrings.Length ? IntStrings[d] : "999+");
            if (kind == FeedbackMath.NumberKind.Head) text = text + " CABEZA";
            _popups[_nextPopup] = new Popup { Pos = pos + Vector3.up * 0.2f + Random.insideUnitSphere * 0.15f, Damage = damage, Born = Time.time, Kind = kind, Alive = true, Text = text };
            _nextPopup = (_nextPopup + 1) % MaxPopups;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            for (int i = 0; i < MaxPopups; i++)
            {
                var p = _popups[i];
                if (!p.Alive) continue;
                float age = Time.time - p.Born;
                if (age > Life) { _popups[i].Alive = false; continue; }
                Vector3 sp = _cam.WorldToScreenPoint(p.Pos + Vector3.up * (age * 0.9f));
                if (sp.z <= 0f) continue;
                float a = Mathf.Clamp01(1f - age / Life);
                bool head = p.Kind == FeedbackMath.NumberKind.Head;
                _style.fontSize = head ? 26 : 20;
                Color c = head ? new Color(1f, 0.85f, 0.15f) : (p.Kind == FeedbackMath.NumberKind.Reduced ? new Color(0.62f, 0.62f, 0.62f) : Color.white);
                var rect = new Rect(sp.x - 90f, Screen.height - sp.y - 16f, 180f, 32f);
                _style.normal.textColor = new Color(0f, 0f, 0f, a * 0.8f);
                GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), p.Text, _style);   // sombra
                _style.normal.textColor = new Color(c.r, c.g, c.b, a);
                GUI.Label(rect, p.Text, _style);
            }
        }

        private static string[] BuildIntStrings(int n)
        {
            var a = new string[n];
            for (int i = 0; i < n; i++) a[i] = i.ToString();
            return a;
        }

        /// <summary>Tono corto procedural (sin archivos de audio): barrido de frecuencia con un poco de ruido.</summary>
        private static AudioClip Tone(string name, float seconds, float f0, float f1, float volume, float noise)
        {
            const int rate = 22050;
            int n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, t);
                phase += 2f * Mathf.PI * f / rate;
                float env = Mathf.Exp(-5f * t) * Mathf.Min(1f, i / 80f);
                data[i] = (Mathf.Sin(phase) * (1f - noise) + ((float)rng.NextDouble() * 2f - 1f) * noise) * env * volume;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
