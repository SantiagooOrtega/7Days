using UnityEngine;
using SevenDays.Core;

namespace SevenDays.Combat
{
    /// <summary>Granada en vuelo (del pool de GrenadeManager): rebota con física, mecha de 2.5 s y al acabar avisa al gestor para que detone.</summary>
    public class Grenade : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public SphereCollider Collider { get; private set; }
        public float FuseLeft { get; private set; }
        public bool Live { get; private set; }
        private GrenadeManager _owner;

        public static Grenade Create(Transform parent, GrenadeManager owner, PhysicsMaterial bounce)
        {
            var go = new GameObject("Grenade");
            go.transform.SetParent(parent, false);
            go.layer = 2;   // los raycasts de disparos y de cobertura no la ven
            var g = go.AddComponent<Grenade>();
            g._owner = owner;
            var vis = Prim.Create(PrimitiveType.Sphere, "Cuerpo", Vector3.zero, new Vector3(0.09f, 0.11f, 0.09f), Prim.GetMaterial(new Color(0.3f, 0.34f, 0.2f)), go.transform, false);
            vis.layer = 2;
            vis.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var spoon = Prim.Create(PrimitiveType.Cube, "Palanca", new Vector3(0.03f, 0.04f, 0f), new Vector3(0.012f, 0.06f, 0.02f), Prim.GetMaterial(new Color(0.12f, 0.12f, 0.13f)), go.transform, false);
            spoon.layer = 2;
            g.Collider = go.AddComponent<SphereCollider>(); g.Collider.radius = 0.06f; g.Collider.sharedMaterial = bounce;
            g.Body = go.AddComponent<Rigidbody>();
            g.Body.mass = 0.4f; g.Body.linearDamping = 0.05f; g.Body.angularDamping = 0.3f;
            g.Body.interpolation = RigidbodyInterpolation.Interpolate;
            g.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.SetActive(false);
            return g;
        }

        public void Launch(Vector3 position, Vector3 velocity, float fuse)
        {
            transform.SetPositionAndRotation(position, Random.rotation);
            gameObject.SetActive(true);
            Body.linearVelocity = velocity;
            Body.angularVelocity = Random.insideUnitSphere * 8f;
            FuseLeft = fuse;
            Live = true;
        }

        private void Update()
        {
            if (!Live) return;
            FuseLeft -= Time.deltaTime;
            // cayó fuera del mundo: se recoge sin explotar
            if (transform.position.y < -20f) { Live = false; _owner.Recycle(this); return; }
            if (FuseLeft <= 0f) { Live = false; _owner.Detonate(this); }
        }
    }
}
