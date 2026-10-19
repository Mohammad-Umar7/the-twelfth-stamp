using UnityEngine;

namespace TwelfthStamp
{
    public enum ColliderKind { None, Box, Mesh, ConvexMesh, Sphere }

    /// <summary>GameObject helpers on top of MeshBuilder: one call per prop part, with sensible colliders.</summary>
    public static class MeshFactory
    {
        public static GameObject Make(string name, Mesh mesh, Material mat, Transform parent, Vector3 localPos = default, Quaternion? localRot = null, ColliderKind collider = ColliderKind.None, int layer = Layers.Default)
        {
            var go = new GameObject(name);
            go.layer = layer;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            switch (collider)
            {
                case ColliderKind.Box: go.AddComponent<BoxCollider>(); break;
                case ColliderKind.Mesh: go.AddComponent<MeshCollider>().sharedMesh = mesh; break;
                case ColliderKind.ConvexMesh: var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true; break;
                case ColliderKind.Sphere: go.AddComponent<SphereCollider>(); break;
            }
            return go;
        }

        public static GameObject FromBuilder(string name, MeshBuilder b, Material mat, Transform parent, ColliderKind collider = ColliderKind.None, int layer = Layers.Default)
            => Make(name, b.ToMesh(name), mat, parent, Vector3.zero, null, collider, layer);

        /// <summary>Axis-aligned (in parent space) box positioned by its centre.</summary>
        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, bool collider = true, float uvScale = 1f, Quaternion? rotation = null, int layer = Layers.Default)
        {
            var b = new MeshBuilder();
            b.AddBox(Vector3.zero, size, Quaternion.identity, uvScale);
            var go = Make(name, b.ToMesh(name), mat, parent, center, rotation, collider ? ColliderKind.Box : ColliderKind.None, layer);
            return go;
        }

        /// <summary>Floor slab (top face at y = top) on the Floor layer with a footstep surface.</summary>
        public static GameObject Floor(string name, Transform parent, Vector3 centerTop, Vector2 size, Material mat, string surface = "stone", float thickness = 0.2f, float uvScale = 1f)
        {
            var go = Box(name, parent, centerTop - Vector3.up * thickness * 0.5f, new Vector3(size.x, thickness, size.y), mat, true, uvScale, null, Layers.Floor);
            go.AddComponent<Surface>().Kind = surface;
            return go;
        }

        public static GameObject Cylinder(string name, Transform parent, Vector3 baseCenter, float radius, float height, Material mat, int segments = 16, bool collider = true, float uvScale = 1f, int layer = Layers.Default)
        {
            var b = new MeshBuilder();
            b.AddCylinder(Vector3.zero, radius, height, segments, uvScale);
            var go = Make(name, b.ToMesh(name), mat, parent, baseCenter, null, ColliderKind.None, layer);
            if (collider)
            {
                var c = go.AddComponent<CapsuleCollider>();
                c.radius = radius; c.height = Mathf.Max(height, radius * 2f); c.center = new Vector3(0, height * 0.5f, 0);
            }
            return go;
        }

        public static GameObject Frustum(string name, Transform parent, Vector3 baseCenter, float r0, float r1, float height, Material mat, int segments = 16, bool collider = false, int layer = Layers.Default)
        {
            var b = new MeshBuilder();
            b.AddFrustum(Vector3.zero, r0, r1, height, segments);
            return Make(name, b.ToMesh(name), mat, parent, baseCenter, null, collider ? ColliderKind.ConvexMesh : ColliderKind.None, layer);
        }

        public static GameObject Sphere(string name, Transform parent, Vector3 center, float radius, Material mat, int lon = 18, int lat = 12, bool collider = false, int layer = Layers.Default)
        {
            var b = new MeshBuilder();
            b.AddSphere(Vector3.zero, radius, lon, lat);
            return Make(name, b.ToMesh(name), mat, parent, center, null, collider ? ColliderKind.Sphere : ColliderKind.None, layer);
        }

        /// <summary>Vertical quad in local XY, visible from -Z (like Unity's built-in Quad), UVs 0..1.</summary>
        public static Mesh QuadMesh(float w, float h, string name = "Quad")
        {
            var b = new MeshBuilder();
            b.AddQuad(new Vector3(w / 2, -h / 2, 0), new Vector3(-w / 2, -h / 2, 0), new Vector3(-w / 2, h / 2, 0), new Vector3(w / 2, h / 2, 0),
                new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1));
            return b.ToMesh(name);
        }

        public static GameObject Quad(string name, Transform parent, float w, float h, Material mat, Vector3 localPos = default, Quaternion? localRot = null, int layer = Layers.Default)
            => Make(name, QuadMesh(w, h, name), mat, parent, localPos, localRot, ColliderKind.None, layer);

        /// <summary>Horizontal disc facing +Y.</summary>
        public static GameObject Disc(string name, float radius, Material mat, Transform parent = null)
        {
            var b = new MeshBuilder();
            b.AddDisc(Vector3.zero, Vector3.up, radius, 32);
            return Make(name, b.ToMesh(name), mat, parent);
        }

        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
        }

        public static void NoShadows(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
