using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// A lamp the player can light or put out (E / trigger). Lit, it is a gameplay light: a pool on the ground the
    /// Passenger will not stand in (LightPool, "light pushes it back"), so lighting lamps along a route makes safe places
    /// to run to. It sputters for a moment as it catches. <see cref="Create"/> builds a standing street lantern;
    /// <see cref="Attach"/> makes an existing lamp switchable. The switch lives on its own child (a trigger round the
    /// light), so the lamp's own solid colliders keep their layers and stay solid.
    /// </summary>
    public class SwitchLamp : Interactable
    {
        /// <summary>Every switchable lamp in a built cell (for the maps).</summary>
        public static readonly System.Collections.Generic.List<SwitchLamp> All = new System.Collections.Generic.List<SwitchLamp>();

        public bool On { get; private set; }
        public LightPool Pool { get; private set; }

        Light m_Light;
        Renderer[] m_Glass;
        Material m_OnMat, m_OffMat;
        float m_Intensity, m_Catch;

        /// <summary>A standing lantern (iron post, glass head) at a local floor position; the safe radius is on the ground.</summary>
        public static SwitchLamp Create(Transform parent, Vector3 localFloor, Color color, bool startOn = false, float poolRadius = 2.6f, float height = 2.4f)
        {
            var root = new GameObject("SwitchLamp").transform;
            root.SetParent(parent, false);
            root.localPosition = localFloor;
            var iron = Mats.Lit(new Color(0.16f, 0.16f, 0.17f), 0.45f, 0.6f);
            MeshFactory.Cylinder("Base", root, Vector3.zero, 0.11f, 0.18f, iron, 12, true);
            MeshFactory.Cylinder("Post", root, new Vector3(0f, 0.18f, 0f), 0.035f, height - 0.5f, iron, 10, true);
            MeshFactory.Box("Cap", root, new Vector3(0f, height, 0f), new Vector3(0.26f, 0.05f, 0.26f), iron, false);
            var glass = MeshFactory.Box("Glass", root, new Vector3(0f, height - 0.17f, 0f), new Vector3(0.2f, 0.28f, 0.2f), iron, false);
            for (int i = 0; i < 4; i++)
                MeshFactory.Box("Rib", root, new Vector3(0f, height - 0.17f, 0f) + Quaternion.Euler(0, i * 90f, 0) * new Vector3(0.1f, 0f, 0.1f),
                    new Vector3(0.018f, 0.3f, 0.018f), iron, false);
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, height - 0.2f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = poolRadius * 2.4f;
            l.intensity = 2.2f;
            l.shadows = LightShadows.None;
            return Attach(root.gameObject, l, new[] { glass.GetComponent<Renderer>() }, color, poolRadius, startOn, root.position);
        }

        /// <summary>
        /// Makes an existing lamp switchable: its light, the glass renderers that glow when lit, and a safe pool on the
        /// floor under it (found by a ray down from the light unless <paramref name="floor"/> is given).
        /// </summary>
        public static SwitchLamp Attach(GameObject lampRoot, Light light, Renderer[] glass, Color color, float poolRadius = 2.6f, bool startOn = false, Vector3? floor = null)
        {
            var at = light != null ? light.transform.position : lampRoot.transform.position + Vector3.up * 2f;
            var sw = new GameObject("Switch");
            sw.transform.SetParent(lampRoot.transform, false);
            sw.transform.position = at;
            var col = sw.AddComponent<SphereCollider>();
            col.radius = 0.35f;
            col.isTrigger = true;
            var lamp = sw.AddComponent<SwitchLamp>();
            lamp.m_Light = light;
            lamp.m_Intensity = light != null ? light.intensity : 0f;
            lamp.m_Glass = glass ?? new Renderer[0];
            lamp.m_OnMat = Mats.Emissive(new Color(0.95f, 0.9f, 0.8f), color * 2.2f);
            lamp.m_OffMat = Mats.Lit(new Color(0.32f, 0.31f, 0.28f), 0.85f, 0f);

            var ground = floor ?? (Physics.Raycast(at, Vector3.down, out var hit, 6f, Layers.FloorMask | (1 << Layers.Default), QueryTriggerInteraction.Ignore) ? hit.point : at - Vector3.up * 2.2f);
            var poolGo = new GameObject("LightPool");
            poolGo.transform.SetParent(lampRoot.transform, false);
            poolGo.transform.position = ground;
            lamp.Pool = poolGo.AddComponent<LightPool>();
            lamp.Pool.Radius = poolRadius;
            lamp.Pool.Height = 3f;
            lamp.Pool.OwnerCell = lampRoot.GetComponentInParent<Cell>();
            lamp.MaxDistance = 3f;
            lamp.MarkerPoint = sw.transform;
            lamp.Set(startOn, false);
            return lamp;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public override string GetPrompt(Interactor who) => On ? "Put out the lamp" : "Light the lamp";

        public override void Use(Interactor who)
        {
            if (!CanUse(who)) return;
            Set(!On, true);
            base.Use(who);
        }

        public void Set(bool on, bool withSound)
        {
            On = on;
            if (Pool != null) Pool.Strength = on ? 1f : 0f;
            if (m_Light != null) { m_Light.enabled = on; m_Light.intensity = m_Intensity; }
            foreach (var r in m_Glass) if (r != null) r.sharedMaterial = on ? m_OnMat : m_OffMat;
            m_Catch = on && withSound ? 0.45f : 0f;
            if (withSound) AudioDirector.I?.Play(on ? "lantern_place" : "button_press", transform.position, 0.7f, on ? 1.1f : 0.8f);
        }

        void Update()
        {
            if (m_Catch <= 0f || m_Light == null) return;
            // Catching: a few uneven sputters before it settles.
            m_Catch -= Time.deltaTime;
            m_Light.intensity = m_Catch > 0f ? m_Intensity * Mathf.Lerp(0.25f, 1.1f, Mathf.PerlinNoise(Time.time * 28f, 0.3f)) : m_Intensity;
        }
    }
}
