using UnityEngine;
using SevenDays.Weapons;

namespace SevenDays.Player
{
    /// <summary>
    /// Pasos y respiración provisionales: un paso por cada pisada del ciclo de la cámara de carrera (más fuerte corriendo), y un jadeo en bucle cuyo volumen y
    /// ritmo suben con la fatiga. Sonidos generados por código (ProceduralAudio).
    /// </summary>
    public class PlayerAudio : MonoBehaviour
    {
        private AudioSource _steps, _breath;
        private PlayerCamera _camera;
        private PlayerMotor _motor;
        private int _variant;

        private void Start()
        {
            _camera = GetComponent<PlayerCamera>();
            _motor = GetComponent<PlayerMotor>();
            _steps = gameObject.AddComponent<AudioSource>();
            _steps.playOnAwake = false; _steps.spatialBlend = 0.2f;
            _breath = gameObject.AddComponent<AudioSource>();
            _breath.playOnAwake = false; _breath.loop = true; _breath.spatialBlend = 0f; _breath.volume = 0f;
            _breath.clip = ProceduralAudio.Breath();
            _breath.Play();
        }

        private void Update()
        {
            if (_camera == null || _camera.RunCam == null || _motor == null) return;
            var o = _camera.RunOutput;
            if (o.Stepped && _motor.IsGrounded)
            {
                _variant = (_variant + 1) % 3;
                _steps.pitch = Random.Range(0.92f, 1.08f);
                _steps.PlayOneShot(ProceduralAudio.Step(_variant), Mathf.Lerp(0.25f, 0.6f, _motor.Speed01) * (_motor.IsCrouched ? 0.5f : 1f));
            }
            // respiración: casi inaudible descansado, clara con fatiga; el ritmo sube (pitch) con el cansancio
            float fatigue = _camera.RunCam.Fatigue;
            float target = 0.04f + 0.5f * fatigue + (_motor.IsSprinting ? 0.08f : 0f);
            _breath.volume = Mathf.MoveTowards(_breath.volume, target, Time.deltaTime * 0.6f);
            _breath.pitch = 1f + 0.35f * fatigue;
        }
    }
}
