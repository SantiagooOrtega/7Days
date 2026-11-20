using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SevenDays.Core;
using SevenDays.Combat;
using SevenDays.Player;
using SevenDays.Procedural;
using SevenDays.World.Loot;

namespace SevenDays.World
{
    /// <summary>
    /// Bucle de los 7 días: Preparación -> Oleada -> Resumen -> siguiente día. Jefes en los días 3, 5 y 7; victoria al terminar el 7; derrota al morir.
    /// La lógica (fases, tamaño de oleada, desbloqueos) está en DayCycle/WaveState (puras, con pruebas); aquí solo se generan zombis por los bordes del mapa y se dibuja el HUD.
    /// El jugador, su inventario y su vida siguen de un día al siguiente porque es la misma escena. Reintentar recarga la escena: no hay estado estático propio.
    /// </summary>
    public class DayLoop : MonoBehaviour
    {
        public static DayLoop Instance { get; private set; }
        public DayCycle Cycle { get; private set; }
        public WaveState Wave { get; private set; }
        /// <summary>Distancia mínima del jugador a un punto de aparición.</summary>
        public const float MinSpawnDistance = 15f;
        /// <summary>Vida que se recupera al empezar cada día (fracción de la máxima).</summary>
        public const float HealBetweenDays = 0.3f;
        /// <summary>Si en una oleada ya generada entera nadie muere en este tiempo, los rezagados (atascados) se retiran para no bloquear la partida.</summary>
        public const float StuckSeconds = 75f;

        private MapData _map;
        private GameObject _player;
        private Health _playerHealth;
        private ZombieManager _zombies;
        private LootManager _loot;
        private List<HouseController> _houses;
        private readonly HashSet<ZombieController> _alive = new HashSet<ZombieController>();
        private readonly List<ZombieController> _scratch = new List<ZombieController>();
        private ZombieController _boss;
        private readonly Dictionary<string, int> _killsByType = new Dictionary<string, int>();
        private readonly System.Random _rng = new System.Random();
        private float _sinceKill;
        private string _summary = "";
        private float _summaryUntil, _titleUntil;
        /// <summary>Una línea por día terminado (F4 y pantalla final).</summary>
        public readonly List<string> History = new List<string>();
        public const float SummaryPanelSeconds = 12f, TitleSeconds = 2.5f;
        private AmmoSupply _ammo;
        private GUIStyle _top, _big, _body, _button;

        public static DayLoop Create(Transform parent, MapData map, GameObject player, ZombieManager zombies, LootManager loot, List<HouseController> houses)
        {
            var go = new GameObject("DayLoop");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<DayLoop>();
            d._map = map; d._player = player; d._zombies = zombies; d._loot = loot; d._houses = houses;
            d._playerHealth = player.GetComponent<Health>();
            d._playerHealth.Died += d.OnPlayerDied;
            zombies.ZombieKilled += d.OnZombieKilled;
            Instance = d;
            d._ammo = loot != null ? AmmoSupply.Create(parent, loot, player) : null;
            d.Cycle = new DayCycle();
            d.Cycle.PhaseChanged += d.OnPhase;
            d.BeginDay();
            return d;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_playerHealth != null) _playerHealth.Died -= OnPlayerDied;
            if (_zombies != null) _zombies.ZombieKilled -= OnZombieKilled;
            GameInput.InputCaptured = false;
        }

        /// <summary>Tecla N (depuración): salta la fase actual.</summary>
        public void DebugSkip()
        {
            switch (Cycle.Phase)
            {
                case DayPhase.Preparation: Cycle.StartWave(); break;
                case DayPhase.Wave: _zombies.DespawnAll(); _alive.Clear(); _boss = null; Cycle.WaveCleared(); break;
                case DayPhase.Summary: Cycle.NextDay(); break;   // (también pasa solo a los 3 s)
            }
        }

        // ---------------------------------------------------------------- fases

        private void BeginDay()
        {
            int day = Cycle.Day;
            double refill = day > 1 && _loot != null ? _loot.NewDay(day) : 0;
            if (_ammo != null) _ammo.BeginDay(day, refill);
            foreach (var h in _houses) if (h != null) h.SetDay(day);
            // el director parte de la dificultad del día, la ajusta con las estadísticas acumuladas y quita los tipos aún bloqueados
            var tracker = CombatMetricsTracker.Instance;
            var reasons = tracker != null ? tracker.DirectorReasons : new List<string>();
            reasons.Clear();
            var totals = tracker != null ? tracker.Metrics.Stored : null;
            _zombies.SetTuning(DayPlans.ApplyUnlocks(DirectorRules.Adjust(DirectorTuning.ForDay(day), totals, reasons), day));
            foreach (var r in reasons) Debug.Log("[7Days] Director: " + r);
            if (day > 1) _playerHealth.Heal(_playerHealth.Max * HealBetweenDays);
            _killsByType.Clear();
            _titleUntil = Time.unscaledTime + TitleSeconds;   // título grande "DÍA N" con fundido
            HudMessages.Add("Prepárate: busca munición y armas (Enter: empezar ya)", new Color(1f, 0.92f, 0.65f), 4f);
        }

        private void OnPhase(DayPhase p)
        {
            switch (p)
            {
                case DayPhase.Preparation: BeginDay(); break;
                case DayPhase.Wave:
                    Wave = new WaveState(DayPlans.For(Cycle.Day, _zombies.MaxZombies));
                    _alive.Clear(); _boss = null; _sinceKill = 0f;
                    if (_ammo != null) _ammo.BeginWave();
                    HudMessages.Add("¡OLEADA " + Cycle.Day + "!", new Color(1f, 0.4f, 0.3f), 3.5f);
                    foreach (var id in DayPlans.NewToday(Cycle.Day))
                    {
                        var t = _zombies.TypeById(id);
                        HudMessages.Add("Nuevo zombi: " + (t != null ? t.displayName : id), new Color(1f, 0.7f, 0.3f), 4f);
                    }
                    if (Wave.HasBoss) HudMessages.Add("Hoy viene un JEFE", new Color(1f, 0.3f, 0.3f), 4f);
                    break;
                case DayPhase.Summary:
                case DayPhase.Victory:
                    _zombies.DespawnAll();   // invocados del jefe o rezagados
                    if (_ammo != null) _ammo.EndWave();
                    _summary = BuildSummary();
                    History.Add(DayLine());
                    _summaryUntil = Time.unscaledTime + SummaryPanelSeconds;   // panel pequeño, no bloquea
                    if (p == DayPhase.Summary) HudMessages.Add("OLEADA SUPERADA", new Color(0.6f, 1f, 0.6f), CycleSummarySeconds);
                    // las estadísticas del día se suman a las guardadas y empiezan las del siguiente
                    if (CombatMetricsTracker.Instance != null) CombatMetricsTracker.Instance.EndDay(Cycle.Day + 1);
                    break;
                case DayPhase.Defeat:
                    _summary = BuildSummary();
                    History.Add(DayLine() + " (muerte)");
                    break;
            }
        }

        private const float CycleSummarySeconds = DayCycle.SummarySeconds;

        private string DayLine()
        {
            var tr = CombatMetricsTracker.Instance;
            if (tr == null) return "Día " + Cycle.Day;
            var s = tr.Snapshot;
            return "Día " + Cycle.Day + ": " + s.kills + " bajas · " + (s.accuracy * 100f).ToString("0") + "% precisión · " + s.damageTaken.ToString("0") + " daño recibido";
        }

        private string BuildSummary()
        {
            var sb = new System.Text.StringBuilder();
            var tr = CombatMetricsTracker.Instance;
            if (tr != null)
            {
                var s = tr.Snapshot;
                sb.AppendLine("Bajas: " + s.kills + "   (a la cabeza " + s.headshotKills + ")");
                sb.AppendLine("Precisión: " + (s.accuracy * 100f).ToString("0") + "%   (" + s.shotsHit + "/" + s.shotsFired + ")");
                sb.AppendLine("Daño recibido: " + s.damageTaken.ToString("0") + "   Tiempo: " + Mathf.FloorToInt(s.timePlayed / 60f) + ":" + Mathf.FloorToInt(s.timePlayed % 60f).ToString("00"));
            }
            if (_killsByType.Count > 0)
            {
                sb.Append("Por tipo: ");
                foreach (var kv in _killsByType) { var t = _zombies.TypeById(kv.Key); sb.Append((t != null ? t.displayName : kv.Key) + " " + kv.Value + "  "); }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- oleada

        private void Update()
        {
            if (Cycle == null) return;
            Cycle.Tick(Time.deltaTime);
            var k = Keyboard.current;
            bool enter = k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
            if (Cycle.Phase == DayPhase.Preparation && enter) Cycle.StartWave();

            if (Cycle.Phase == DayPhase.Wave) TickWave(Time.deltaTime);

            bool overlay = Cycle.IsOver;
            if (overlay) { GameInput.InputCaptured = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            else if (GameInput.InputCaptured && _wasOverlay) GameInput.InputCaptured = false;
            _wasOverlay = overlay;
        }
        private bool _wasOverlay;

        private void TickWave(float dt)
        {
            // zombis que dejaron de estar en uso sin pasar por la muerte (no debería pasar) cuentan como muertos para no bloquear la oleada
            _scratch.Clear();
            foreach (var z in _alive) if (z == null || !z.InUse || z.Health.IsDead) _scratch.Add(z);
            foreach (var z in _scratch) { _alive.Remove(z); Wave.OnKilled(); }
            if (_boss != null && (!_boss.InUse || _boss.Health.IsDead)) { _boss = null; Wave.OnBossDead(); }

            if (Wave.WantsSpawn(dt, _alive.Count) && TryPickSpawn(out var pos))
            {
                var z = _zombies.SpawnByTuning(pos);
                if (z != null) { _alive.Add(z); Wave.OnSpawned(); JoinHorde(z); }
            }
            if (Wave.WantsBoss && TryPickSpawn(out var bpos))
            {
                var b = _zombies.Spawn(Wave.Plan.bossId, bpos);
                if (b != null) { _boss = b; Wave.OnBossSpawned(); JoinHorde(b); HudMessages.Add("¡" + b.Type.displayName.ToUpper() + "!", new Color(1f, 0.25f, 0.2f), 4f); }
            }

            _sinceKill += dt;
            if (Wave.AllSpawned && _sinceKill > StuckSeconds && _alive.Count > 0 && (_boss == null))
            {
                Debug.LogWarning("[7Days] " + _alive.Count + " zombis atascados: se retiran para cerrar la oleada.");
                foreach (var z in _alive) { z.Deactivate(); Wave.OnKilled(); }
                _alive.Clear();
            }
            if (Wave.IsCleared(_alive.Count)) Cycle.WaveCleared();
        }

        /// <summary>Modo horda: persigue al jugador desde que aparece, con retardo y velocidad propios (parámetros del director).</summary>
        private void JoinHorde(ZombieController z)
        {
            var t = _zombies.Tuning;
            z.JoinHorde((float)_rng.NextDouble() * t.hordeDelayMax, 1f + ((float)_rng.NextDouble() * 2f - 1f) * t.hordeSpeedJitter);
        }

        private void OnZombieKilled(ZombieController z)
        {
            _sinceKill = 0f;
            if (z.Type != null) _killsByType[z.Type.id] = (_killsByType.TryGetValue(z.Type.id, out int n) ? n : 0) + 1;
            if (_alive.Remove(z)) { Wave?.OnKilled(); if (_ammo != null) _ammo.OnKill(z.transform.position); }
            if (z == _boss) { _boss = null; Wave?.OnBossDead(); HudMessages.Add("¡Jefe abatido! Recoge su botín", new Color(1f, 0.85f, 0.3f), 4f); }
        }

        private void OnPlayerDied() => Cycle.PlayerDied();

        /// <summary>
        /// Elige un punto de aparición del borde del mapa: a 15 m o más del jugador, sobre el NavMesh y, si se puede, sin línea de visión con él. Si ninguno cumple todo,
        /// relaja primero la visión y luego la distancia.
        /// </summary>
        private bool TryPickSpawn(out Vector3 pos)
        {
            pos = default;
            if (_map == null || _map.spawns == null || _map.spawns.Length == 0 || _player == null) return false;
            Vector3 eye = _player.transform.position + Vector3.up * 1.6f;
            int mask = ~((1 << 2) | HitboxLayer.Mask);   // el segmento empieza dentro del jugador: su cápsula no cuenta
            var best = new List<Vector3>(); var ok = new List<Vector3>(); var any = new List<Vector3>();
            foreach (var s in _map.spawns)
            {
                if (!NavMesh.SamplePosition(new Vector3(s.x, 0f, s.z), out var hit, 4f, NavMesh.AllAreas)) continue;
                Vector3 p = hit.position;
                any.Add(p);
                if ((p - _player.transform.position).sqrMagnitude < MinSpawnDistance * MinSpawnDistance) continue;
                ok.Add(p);
                if (Physics.Linecast(eye, p + Vector3.up * 1.5f, mask, QueryTriggerInteraction.Ignore)) best.Add(p);
            }
            var list = best.Count > 0 ? best : (ok.Count > 0 ? ok : any);
            if (list.Count == 0) return false;
            pos = list[_rng.Next(list.Count)];
            return true;
        }

        // ---------------------------------------------------------------- HUD

        private void OnGUI()
        {
            if (Cycle == null) return;
            if (_top == null)
            {
                _top = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
                _big = new GUIStyle(_top) { fontSize = 64 };
                _body = new GUIStyle(_top) { fontSize = 26, fontStyle = FontStyle.Normal };
                _button = new GUIStyle(GUI.skin.button) { fontSize = 28 };
            }
            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;

            string line = "DÍA " + Cycle.Day + " DE " + DayPlans.Days;
            if (Cycle.Phase == DayPhase.Preparation) line += "  ·  PREPARACIÓN " + Mathf.CeilToInt(Cycle.PrepLeft) + " s  (Enter: empezar ya)";
            else if (Cycle.Phase == DayPhase.Wave && Wave != null) line += "  ·  ZOMBIS RESTANTES " + Wave.Remaining(_alive.Count) + (Wave.HasBoss && !Wave.BossDead ? "  ·  JEFE" : "");
            Shade(new Rect(w / 2 - 480, 62, 960, 46), 0.5f);
            GUI.Label(new Rect(0, 62, w, 46), line, _top);

            // título grande "DÍA N" que aparece y se funde
            float tl = _titleUntil - Time.unscaledTime;
            if (tl > 0f && !Cycle.IsOver)
            {
                float a = Mathf.Clamp01(tl / 0.8f) * Mathf.Clamp01((TitleSeconds - tl) / 0.3f);
                var prev = GUI.color; GUI.color = new Color(1f, 1f, 1f, a);
                GUI.Label(new Rect(0, 300, w, 160), "DÍA " + Cycle.Day, new GUIStyle(_big) { fontSize = 140 });
                GUI.color = prev;
            }
            // resumen del día anterior: panel pequeño a la derecha, no bloquea ni pausa
            if (!Cycle.IsOver && Time.unscaledTime < _summaryUntil && _summary.Length > 0)
            {
                Rect r = new Rect(w - 560, 130, 530, 230);
                Shade(r, 0.55f);
                GUI.Label(new Rect(r.x + 16, r.y + 8, r.width - 32, 40), "Resumen del día " + (Cycle.Phase == DayPhase.Summary ? Cycle.Day : Cycle.Day - 1), new GUIStyle(_body) { alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Bold });
                GUI.Label(new Rect(r.x + 16, r.y + 50, r.width - 32, r.height - 56), _summary, new GUIStyle(_body) { alignment = TextAnchor.UpperLeft, fontSize = 20, wordWrap = true });
            }
            if (Cycle.Phase == DayPhase.Victory) Panel(w, "¡HAS SOBREVIVIDO 7 DÍAS!", string.Join("\n", History), "Jugar otra vez", Retry, "Menú", ToMenu);
            else if (Cycle.Phase == DayPhase.Defeat) Panel(w, "HAS MUERTO — DÍA " + Cycle.Day, _summary + "\n" + string.Join("\n", History), "Reintentar", Retry, "Menú", ToMenu);
        }

        private void Panel(float w, string title, string body, string a, System.Action onA, string b, System.Action onB)
        {
            Rect r = new Rect(w / 2 - 520, 200, 1040, 640);
            Shade(r, 0.82f);
            GUI.Label(new Rect(r.x, r.y + 30, r.width, 90), title, _big);
            GUI.Label(new Rect(r.x + 40, r.y + 140, r.width - 80, 340), body, new GUIStyle(_body) { fontSize = 22, wordWrap = true });
            float y = r.yMax - 120;
            if (GUI.Button(new Rect(w / 2 - 440, y, 400, 70), a, _button)) onA();
            if (GUI.Button(new Rect(w / 2 + 40, y, 400, 70), b, _button)) onB();
        }

        private static void Shade(Rect r, float a)
        {
            var prev = GUI.color; GUI.color = new Color(0, 0, 0, a);
            GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = prev;
        }

        /// <summary>Reintentar: recarga la escena con el mismo mapa (MapIO.SelectedId se conserva a propósito); todo lo demás se reconstruye.</summary>
        public static void Retry()
        {
            GameInput.InputCaptured = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Vuelve al menú si existe la escena Menu en Build Profiles; si no, reintenta.</summary>
        public static void ToMenu()
        {
            GameInput.InputCaptured = false;
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded("Menu")) SceneManager.LoadScene("Menu");
            else Retry();
        }
    }
}
