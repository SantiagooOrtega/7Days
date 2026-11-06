using UnityEngine;
using UnityEngine.Rendering;

namespace SevenDays.World
{
    /// <summary>
    /// Dibuja muchas copias de una malla con GPU instancing (Graphics.RenderMeshInstanced): cientos de árboles, arbustos o
    /// matas de hierba cuestan una sola llamada de dibujo por tipo, sin un GameObject por pieza. El material debe tener
    /// "Enable GPU Instancing" (los de Prim ya lo tienen).
    /// </summary>
    public class InstancedBatch : MonoBehaviour
    {
        private const int MaxPerCall = 1000;
        private Mesh _mesh;
        private Material[] _materials;
        private Matrix4x4[] _matrices;
        private ShadowCastingMode _shadows;

        public int Count => _matrices?.Length ?? 0;

        public void Init(Mesh mesh, Material material, Matrix4x4[] matrices, bool castShadows) => Init(mesh, new[] { material }, matrices, castShadows);

        /// <summary>Un material por submesh (por ejemplo placa de señal: frente con la textura y dorso de metal).</summary>
        public void Init(Mesh mesh, Material[] materials, Matrix4x4[] matrices, bool castShadows)
        {
            _mesh = mesh;
            _materials = materials;
            _matrices = matrices;
            _shadows = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        private void Update()
        {
            if (_matrices == null || _matrices.Length == 0) return;
            for (int sub = 0; sub < _materials.Length; sub++)
            {
                var rp = new RenderParams(_materials[sub]) { shadowCastingMode = _shadows, receiveShadows = false, layer = gameObject.layer };
                for (int start = 0; start < _matrices.Length; start += MaxPerCall)
                    Graphics.RenderMeshInstanced(rp, _mesh, sub, _matrices, Mathf.Min(MaxPerCall, _matrices.Length - start), start);
            }
        }
    }
}
