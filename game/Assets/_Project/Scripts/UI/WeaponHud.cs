using System.Collections.Generic;
using UnityEngine;
using SevenDays.Core;
using SevenDays.Player;
using SevenDays.Procedural;

namespace SevenDays.UI
{
    /// <summary>
    /// Pega el selector de armas (WeaponHudView) al juego: ranuras con los iconos del inventario, arma activa, munición (cargador / reserva) y modo de disparo, y la
    /// rueda radial (mantén Tab: el tiempo se ralentiza suavemente hasta 0.25x, el ratón elige el sector y al soltar Tab se cambia de arma; Esc cancela). Teclas
    /// 1-5 y rueda del ratón siguen funcionando como siempre (PlayerShooting). Solo trabaja cuando algo cambia: sin alocaciones por frame.
    /// </summary>
    public class WeaponHud : MonoBehaviour
    {
        public WeaponHudView View { get; private set; }
        public bool WheelOpen => _open;

        private PlayerShooting _shooting;
        private PlayerInventory _inventory;
        private Health _health;
        private readonly List<WeaponData> _data = new List<WeaponData>(WeaponBarState.Slots);
        private readonly SlowMotion _slow = new SlowMotion();
        private bool _dirty = true, _open, _started;
        private int _lastActive = -1, _lastMag = -1, _lastReserve = -1, _selected;
        private string _lastMode;
        private V2 _virtual;

        public void Initialize(GameObject player)
        {
            _shooting = player.GetComponent<PlayerShooting>();
            _inventory = player.GetComponent<PlayerInventory>();
            _health = player.GetComponent<Health>();
            View = WeaponHudView.Create(transform);
            _inventory.Changed += OnChanged;
            _shooting.WeaponChanged += OnWeaponChanged;
        }

        private void OnChanged() => _dirty = true;
        private void OnWeaponChanged(WeaponState w) { _dirty = true; _lastActive = -1; }

        private void OnDisable() => Release();
        private void OnDestroy()
        {
            Release();
            if (_inventory != null) _inventory.Changed -= OnChanged;
            if (_shooting != null) _shooting.WeaponChanged -= OnWeaponChanged;
        }

        private void Release()
        {
            GameInput.InputCaptured = false;
            if (_open || Time.timeScale != 1f) Time.timeScale = 1f;
            _open = false; _slow.Reset();
        }

        private void Update()
        {
            if (View == null || _shooting == null) return;
            var list = _inventory.Weapons;
            int active = 0;
            for (int i = 0; i < list.Count; i++) if (ReferenceEquals(list[i], _shooting.Current)) { active = i; break; }

            if (_dirty)
            {
                _dirty = false;
                _data.Clear();
                for (int i = 0; i < list.Count && i < WeaponBarState.Slots; i++) _data.Add(list[i].Data);
                View.SetSlots(_data);
                _lastActive = -1;
            }
            View.Bar.Set(list.Count, active, instant: !_started);
            _started = true;

            // textos solo cuando cambian
            var wpn = _shooting.Current; var d = wpn.Data;
            int reserve = _shooting.CurrentReserve;
            string mode = d.IsMelee ? null : _shooting.FireControl.Mode;
            if (active != _lastActive || wpn.Magazine != _lastMag || reserve != _lastReserve || mode != _lastMode)
            {
                _lastActive = active; _lastMag = wpn.Magazine; _lastReserve = reserve; _lastMode = mode;
                View.SetTexts(WeaponHudText.Ammo(d, wpn.Magazine, reserve), WeaponHudText.Mode(d, mode), WeaponHudText.Name(d));
            }

            UpdateWheel(list, active);
            View.Tick(Time.unscaledDeltaTime);
        }

        private void UpdateWheel(IReadOnlyList<WeaponState> list, int active)
        {
            bool dead = _health != null && _health.IsDead;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            int count = Mathf.Min(list.Count, WeaponBarState.Slots);
            if (!_open && GameInput.WheelPressed && locked && !dead && count > 1)
            {
                _open = true; _virtual = new V2(0f, 0f); _selected = active;
                GameInput.InputCaptured = true;
            }
            if (_open)
            {
                bool cancel = GameInput.EscapePressed || dead || !locked;
                var raw = GameInput.RawLook;
                _virtual = RadialWheel.Accumulate(_virtual, raw.x, raw.y, 140f);
                int idx = RadialWheel.IndexAt(_virtual.X, _virtual.Y, count);
                if (idx >= 0) _selected = idx;
                var sel = list[Mathf.Clamp(_selected, 0, count - 1)];
                View.SetWheel(true, _selected, count);
                View.SetWheelTexts(sel.Data.displayName, WeaponHudText.Ammo(sel.Data, sel.Magazine, _inventory.Reserve(sel)));
                if (!GameInput.WheelHeld || cancel)
                {
                    _open = false;
                    GameInput.InputCaptured = false;
                    if (!cancel && _selected != active) _shooting.SelectSlot(_selected);
                    View.SetWheel(false, -1, count);
                }
            }
            // ralentización suave mientras la rueda está abierta
            float scale = _slow.Tick(Time.unscaledDeltaTime, _open);
            Time.timeScale = !_open && scale > 0.995f ? 1f : scale;
        }
    }
}
