using System.Collections;
using UnityEngine;
using SevenDays.Core;

namespace SevenDays.World
{
    /// <summary>Blanco de práctica: parpadea al recibir daño, cae al morir y reaparece. Puede moverse de lado a lado.</summary>
    [RequireComponent(typeof(Health))]
    public class TargetDummy : MonoBehaviour
    {
        public float StrafeAmplitude;
        public float StrafeSpeed = 1f;
        public float RespawnDelay = 4f;

        private Health _health;
        private Renderer _renderer;
        private Collider _collider;
        private Material _normal;
        private Material _flash;
        private Vector3 _origin;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _renderer = GetComponentInChildren<Renderer>();
            _collider = GetComponentInChildren<Collider>();
            _normal = _renderer.sharedMaterial;
            _flash = Prim.GetMaterial(Color.white);
            _origin = transform.position;
            _health.Damaged += _ => StartCoroutine(Flash());
            _health.Died += () => StartCoroutine(DieAndRespawn());
        }

        private void Update()
        {
            if (StrafeAmplitude > 0f && !_health.IsDead)
                transform.position = _origin + transform.right * (Mathf.Sin(Time.time * StrafeSpeed) * StrafeAmplitude);
        }

        private IEnumerator Flash()
        {
            _renderer.sharedMaterial = _flash;
            yield return new WaitForSeconds(0.07f);
            if (!_health.IsDead) _renderer.sharedMaterial = _normal;
        }

        private IEnumerator DieAndRespawn()
        {
            _renderer.enabled = false;
            _collider.enabled = false;
            yield return new WaitForSeconds(RespawnDelay);
            _health.ResetHealth();
            _renderer.sharedMaterial = _normal;
            _renderer.enabled = true;
            _collider.enabled = true;
        }
    }
}
