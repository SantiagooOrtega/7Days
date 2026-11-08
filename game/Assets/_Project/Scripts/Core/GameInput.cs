using UnityEngine;
using UnityEngine.InputSystem;

namespace SevenDays.Core
{
    /// <summary>
    /// Lectura de teclado y ratón en un solo lugar (Input System). Si luego se quiere
    /// remapear controles o usar gamepad, solo hay que tocar este archivo.
    /// </summary>
    public static class GameInput
    {
        public static Vector2 Move
        {
            get
            {
                var k = Keyboard.current;
                if (k == null) return Vector2.zero;
                var v = new Vector2(
                    (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f),
                    (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f));
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        /// <summary>Mientras es true (rueda de armas abierta) el ratón no mira, no dispara ni apunta: lo usa la rueda para elegir con el movimiento del ratón.</summary>
        public static bool InputCaptured;

        public static Vector2 Look => Mouse.current != null && !InputCaptured ? Mouse.current.delta.ReadValue() : Vector2.zero;
        /// <summary>Movimiento crudo del ratón, también con la entrada capturada.</summary>
        public static Vector2 RawLook => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        public static float Scroll => Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;

        public static bool FireHeld => Mouse.current != null && !InputCaptured && Mouse.current.leftButton.isPressed;
        public static bool FirePressed => Mouse.current != null && !InputCaptured && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool AimPressed => Mouse.current != null && !InputCaptured && Mouse.current.rightButton.wasPressedThisFrame;
        public static bool AimHeld => Mouse.current != null && !InputCaptured && Mouse.current.rightButton.isPressed;
        public static bool WheelPressed => Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
        public static bool WheelHeld => Keyboard.current != null && Keyboard.current.tabKey.isPressed;

        public static bool SprintHeld => Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        public static bool CrouchHeld => Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;
        public static bool JumpPressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        public static bool InteractPressed => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        public static bool InteractHeld => Keyboard.current != null && Keyboard.current.eKey.isPressed;
        public static bool MedkitPressed => Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame;
        public static bool BandagePressed => Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame;
        public static bool ReloadPressed => Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        public static bool CoverPressed => Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
        public static bool HelpPressed => Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
        public static bool ShoulderSwapPressed => Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
        public static bool FireModePressed => Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame;
        public static bool DebugPanelPressed => Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame;
        public static bool EscapePressed => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        /// <summary>Índice del arma elegida con las teclas 1-9 este frame, o -1.</summary>
        public static int WeaponSlotPressed
        {
            get
            {
                var k = Keyboard.current;
                if (k == null) return -1;
                if (k.digit1Key.wasPressedThisFrame) return 0;
                if (k.digit2Key.wasPressedThisFrame) return 1;
                if (k.digit3Key.wasPressedThisFrame) return 2;
                if (k.digit4Key.wasPressedThisFrame) return 3;
                if (k.digit5Key.wasPressedThisFrame) return 4;
                return -1;
            }
        }
    }
}
