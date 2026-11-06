using System.Collections.Generic;
using UnityEngine;

namespace SevenDays.Core
{
    /// <summary>Crea objetos con primitivas de Unity coloreadas (sin assets externos). Los materiales se comparten por color.</summary>
    public static class Prim
    {
        private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();
        private static Material _template;

        public static GameObject Create(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color,
            Transform parent = null, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        public static Material GetMaterial(Color color)
        {
            Color32 c = color;
            int key = (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
            if (Cache.TryGetValue(key, out var existing) && existing != null) return existing;

            if (_template == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _template = probe.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(probe);
            }
            var mat = new Material(_template) { enableInstancing = true };
            mat.SetColor("_BaseColor", color); // URP Lit
            mat.SetColor("_Color", color);
            Cache[key] = mat;
            return mat;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            _template = null;
        }
    }
}
