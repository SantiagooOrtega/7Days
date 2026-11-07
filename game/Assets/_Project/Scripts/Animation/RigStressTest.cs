using System.Collections.Generic;
using UnityEngine;
using SevenDays.Procedural;

namespace SevenDays.Animation
{
    /// <summary>
    /// Prueba de carga para el panel F6: crea N soldados con rig procedural que caminan y corren en círculos alrededor del jugador, para medir el coste de la animación
    /// con muchos personajes (los zombis usarán este mismo rig en el Bloque 5). Se crean una vez y se reutilizan.
    /// </summary>
    public class RigStressTest : MonoBehaviour
    {
        private readonly List<ProceduralRig> _rigs = new List<ProceduralRig>();
        private readonly List<float> _phase = new List<float>();
        private Transform _center;

        public int Count => _rigs.Count;

        public static RigStressTest Create(Transform center)
        {
            var go = new GameObject("RigStressTest");
            var t = go.AddComponent<RigStressTest>();
            t._center = center;
            return t;
        }

        public void SetCount(int n)
        {
            while (_rigs.Count < n)
            {
                var root = new GameObject("StressRig " + _rigs.Count);
                root.transform.SetParent(transform, false);
                var rig = root.AddComponent<ProceduralRig>();
                var p = RigProportions.Soldier(); p.height *= Random.Range(0.92f, 1.08f);
                rig.Build(p);
                RigVisuals.BuildSoldier(rig, RigPalette.Soldier);
                foreach (var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 2;
                _rigs.Add(rig);
                _phase.Add(Random.value * Mathf.PI * 2f);
            }
            for (int i = 0; i < _rigs.Count; i++) _rigs[i].gameObject.SetActive(i < n);
        }

        private void Update()
        {
            if (_center == null) return;
            for (int i = 0; i < _rigs.Count; i++)
            {
                var r = _rigs[i];
                if (!r.gameObject.activeSelf) continue;
                float radius = 4f + (i % 5) * 3f;
                float speed = i % 3 == 0 ? 6f : (i % 3 == 1 ? 3.5f : 1.6f);   // corren, trotan y caminan
                _phase[i] += speed / radius * Time.deltaTime;
                Vector3 p = _center.position + new Vector3(Mathf.Cos(_phase[i]), 0f, Mathf.Sin(_phase[i])) * radius;
                Vector3 tangent = new Vector3(-Mathf.Sin(_phase[i]), 0f, Mathf.Cos(_phase[i]));
                r.transform.SetPositionAndRotation(p, Quaternion.LookRotation(tangent));
                r.Inputs = new RigInputs { Grounded = true, MoveY = speed / 6.8f, LookDirection = tangent, DesiredForward = tangent, ArmSwing = 1f };
            }
        }
    }
}
