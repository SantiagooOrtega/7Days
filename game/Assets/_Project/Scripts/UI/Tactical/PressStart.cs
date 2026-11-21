using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SevenDays.UI.Tactical
{
    /// <summary>"PULSA ENTER PARA EMPEZAR" de la pantalla de inicio: late entre el 60 y el 100 % de opacidad (sin memoria por frame) y Enter, Espacio o clic para seguir.</summary>
    public class PressStart : MonoBehaviour
    {
        public Text Label;
        public event Action Pressed;
        private float _since;

        private void Update()
        {
            _since += Time.unscaledDeltaTime;
            if (Label != null) { var c = Label.color; c.a = 0.6f + 0.4f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f)); Label.color = c; }   // late entre el 60 y el 100 %
            if (_since < 0.3f) return;   // que un Enter anterior no lo salte
            var k = Keyboard.current; var m = Mouse.current;
            bool go = (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) || (m != null && m.leftButton.wasPressedThisFrame);
            if (go) { UiSounds.Accept(); Pressed?.Invoke(); }
        }

        /// <summary>Para las pruebas: como pulsar Enter.</summary>
        public void Press() => Pressed?.Invoke();
    }
}
