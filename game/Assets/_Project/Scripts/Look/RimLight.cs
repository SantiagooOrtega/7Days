using UnityEngine;

namespace SevenDays.Look
{
    /// <summary>
    /// Luz de contorno suave: una luz fría sin sombras que se mantiene delante del jugador (respecto a la cámara)
    /// y algo arriba, para que sus bordes se separen del fondo. Es una sola luz puntual, barata.
    /// </summary>
    public class RimLight : MonoBehaviour
    {
        private Transform _player;
        private Transform _camera;
        private Light _light;

        public void Initialize(Transform player, Transform camera)
        {
            _player = player;
            _camera = camera;
            var go = new GameObject("RimLight");
            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(0.72f, 0.86f, 1f);
            _light.range = 5.5f;
            _light.intensity = 1.4f;
            _light.shadows = LightShadows.None;
        }

        private void LateUpdate()
        {
            Vector3 fwd = _camera.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : _player.forward;
            _light.transform.position = _player.position + fwd * 2.0f + Vector3.up * 2.1f;
        }

        private void OnDestroy()
        {
            if (_light != null) Destroy(_light.gameObject);
        }
    }
}
