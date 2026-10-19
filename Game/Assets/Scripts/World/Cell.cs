using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    public enum CellMode { Hidden, Active, Pocket }

    /// <summary>
    /// A compact playable space (one country, the shop, the apartment...). Built procedurally on first use, kept
    /// for revisits. Active = the world you stand in (URP Lit, real lights, colliders). Pocket = seen through a tear
    /// (PocketLit with its own lighting, stencil-tested, no collisions with the player, no lights).
    /// </summary>
    public abstract class Cell : MonoBehaviour
    {
        public CellId Id;
        public LightingPreset Lighting = new LightingPreset();
        public Transform Arrival;                      // direct child, floor level, forward = facing on arrival
        public readonly List<SeamAnchor> Seams = new List<SeamAnchor>();
        public string Ambience;
        public string Music;
        public Light Sun;
        public Encounter Encounter { get; protected set; }
        public CellMode Mode { get; private set; } = CellMode.Hidden;
        public bool Built { get; private set; }

        Renderer[] m_Renderers;
        Material[][] m_LitMats;
        Material[][] m_PocketMats;
        bool[] m_RendererWasEnabled;
        Light[] m_Lights;
        bool[] m_LightWasEnabled;
        readonly Dictionary<GameObject, int> m_Layers = new Dictionary<GameObject, int>();
        ReflectionProbe m_Probe;
        GameObject m_Sky;
        Material m_SkyMat;

        public CountryDef Country => CountryDatabase.ByCell(Id);

        public void EnsureBuilt()
        {
            if (Built) return;
            Build();
            if (Arrival == null)
            {
                Arrival = new GameObject("Arrival").transform;
                Arrival.SetParent(transform, false);
            }
            BuildSkyAndProbe();
            Cache();
            Built = true;
        }

        /// <summary>Create geometry, props, lights, seams, the encounter. Called once.</summary>
        protected abstract void Build();

        void BuildSkyAndProbe()
        {
            m_SkyMat = Mats.SkyInstance(Lighting);
            var b = new MeshBuilder();
            b.AddSphere(Vector3.zero, 320f, 32, 16);
            m_Sky = MeshFactory.Make("SkyDome", b.ToMesh("SkyDome"), m_SkyMat, transform);
            var r = m_Sky.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            var probeGo = new GameObject("ReflectionProbe");
            probeGo.transform.SetParent(transform, false);
            probeGo.transform.localPosition = (Arrival != null ? Arrival.localPosition : Vector3.zero) + Vector3.up * 1.6f;
            m_Probe = probeGo.AddComponent<ReflectionProbe>();
            m_Probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            m_Probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            m_Probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            m_Probe.resolution = 256;
            m_Probe.size = new Vector3(60, 30, 60);
            m_Probe.boxProjection = false;
            m_Probe.importance = 1;
            m_Probe.cullingMask = ~((1 << Layers.Pocket) | (1 << Layers.PlayerBody) | (1 << Layers.UI3D) | (1 << Layers.MirrorOnly) | (1 << Layers.MirrorGlass));
        }

        UnityEngine.Rendering.ShadowCastingMode[] m_ShadowModes;

        void Cache()
        {
            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_LitMats = new Material[m_Renderers.Length][];
            m_PocketMats = new Material[m_Renderers.Length][];
            m_RendererWasEnabled = new bool[m_Renderers.Length];
            m_ShadowModes = new UnityEngine.Rendering.ShadowCastingMode[m_Renderers.Length];
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                m_LitMats[i] = m_Renderers[i].sharedMaterials;
                m_RendererWasEnabled[i] = m_Renderers[i].enabled;
                m_ShadowModes[i] = m_Renderers[i].shadowCastingMode;
            }
            m_Layers.Clear();
            m_Lights = GetComponentsInChildren<Light>(true);
            m_LightWasEnabled = new bool[m_Lights.Length];
            for (int i = 0; i < m_Lights.Length; i++) m_LightWasEnabled[i] = m_Lights[i].enabled;
            foreach (var t in GetComponentsInChildren<Transform>(true)) m_Layers[t.gameObject] = t.gameObject.layer;
        }

        /// <summary>Re-scan after props are added at runtime (e.g. an encounter spawns extra objects).</summary>
        public void Recache()
        {
            if (!Built) return;
            Cache();
        }

        // ------------------------------------------------------------------ modes
        public void Activate()
        {
            EnsureBuilt();
            if (Mode == CellMode.Hidden) Cache(); // pick up props dropped here since the last visit
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);
            ApplyMaterials(false);
            SetLayers(false);
            for (int i = 0; i < m_Lights.Length; i++) if (m_Lights[i] != null) m_Lights[i].enabled = m_LightWasEnabled[i];
            Lighting.ApplyGlobal(Sun);
            m_SkyMat.SetFloat("_StencilComp", Mats.StencilAlways);
            m_SkyMat.renderQueue = Mats.ActiveSkyQueue;
            if (Game.Rig != null) Game.Rig.Cam.backgroundColor = Lighting.ClearColor;
            if (m_Probe != null) { m_Probe.enabled = true; m_Probe.RenderProbe(); }
            foreach (var src in GetComponentsInChildren<AudioSource>(true)) src.mute = false;
            Mode = CellMode.Active;
        }

        public void Deactivate()
        {
            Mode = CellMode.Hidden;
            gameObject.SetActive(false);
        }

        public void ShowAsPocket(SeamAnchor anchor)
        {
            EnsureBuilt();
            if (Mode == CellMode.Hidden) Cache();
            gameObject.SetActive(true);
            // Arrival point sits on the seam's floor point, facing away from the viewer (into the destination).
            var rot = Quaternion.LookRotation(-anchor.Forward, Vector3.up) * Quaternion.Inverse(Arrival.localRotation);
            transform.rotation = rot;
            transform.position = anchor.FloorPoint - rot * Arrival.localPosition;
            ApplyMaterials(true);
            SetLayers(true);
            foreach (var l in m_Lights) if (l != null) l.enabled = false;
            if (m_Probe != null) m_Probe.enabled = false;
            foreach (var src in GetComponentsInChildren<AudioSource>(true)) src.mute = true;
            Mode = CellMode.Pocket;
            Encounter?.OnPocketShown();
        }

        public void HidePocket()
        {
            if (Mode != CellMode.Pocket) return;
            // Restore the world state before hiding so a later visit never caches pocket materials/layers as "lit".
            ApplyMaterials(false);
            SetLayers(false);
            for (int i = 0; i < m_Lights.Length; i++) if (m_Lights[i] != null) m_Lights[i].enabled = m_LightWasEnabled[i];
            if (m_Probe != null) m_Probe.enabled = true;
            foreach (var src in GetComponentsInChildren<AudioSource>(true)) src.mute = false;
            Deactivate();
        }

        void ApplyMaterials(bool pocket)
        {
            for (int i = 0; i < m_Renderers.Length; i++)
            {
                var r = m_Renderers[i];
                if (r == null) continue;
                if (!pocket)
                {
                    r.sharedMaterials = m_LitMats[i];
                    r.enabled = m_RendererWasEnabled[i];
                    r.shadowCastingMode = m_ShadowModes[i];
                    continue;
                }
                if (m_PocketMats[i] == null)
                {
                    var src = m_LitMats[i];
                    var dst = new Material[src.Length];
                    int shown = 0;
                    for (int k = 0; k < src.Length; k++)
                    {
                        dst[k] = Mats.PocketVariant(src[k], Lighting);
                        if (dst[k] != null) shown++;
                    }
                    // Nothing drawable: hide the renderer. A model mixing glass with opaque parts hides only the glass.
                    if (shown == 0 && src.Length > 0) dst = new Material[0];
                    else for (int k = 0; k < dst.Length; k++) if (dst[k] == null) dst[k] = Mats.PocketHidden;
                    m_PocketMats[i] = dst;
                }
                if (m_PocketMats[i].Length == 0) { r.enabled = false; continue; }
                r.sharedMaterials = m_PocketMats[i];
                r.enabled = m_RendererWasEnabled[i];
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        void SetLayers(bool pocket)
        {
            foreach (var kv in m_Layers)
            {
                if (kv.Key == null) continue;
                kv.Key.layer = pocket ? Layers.Pocket : kv.Value;
            }
            // Interactables put themselves on the Interactable layer in Awake, which for a cell built inactive runs only at
            // its first activation, after the layers above were cached. Put them back, or near-hand use and grabbing (VR),
            // which look only on that layer, miss everything in a place on its first visit and after it was a pocket.
            if (!pocket)
                foreach (var it in GetComponentsInChildren<Interactable>(true))
                    Interactable.SetLayerRecursive(it.gameObject, Layers.Interactable);
        }

        public void RefreshProbe()
        {
            if (m_Probe != null && Mode == CellMode.Active) m_Probe.RenderProbe();
        }

        // ------------------------------------------------------------------ build helpers
        protected Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            return t;
        }

        protected void SetArrival(Vector3 pos, float yaw)
        {
            if (Arrival == null)
            {
                Arrival = new GameObject("Arrival").transform;
                Arrival.SetParent(transform, false);
            }
            Arrival.localPosition = pos;
            Arrival.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        protected SeamAnchor AddSeam(string id, Vector3 floorPos, float yaw, float centerHeight = 1.25f, bool traverse = true)
        {
            var s = SeamAnchor.Create(transform, id, floorPos, yaw, centerHeight);
            s.AllowTraverse = traverse;
            Seams.Add(s);
            return s;
        }

        protected Light AddSun()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            Sun = go.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowBias = 0.04f;
            Sun.shadowNormalBias = 0.3f;
            return Sun;
        }

        protected Light AddPointLight(Transform parent, Vector3 pos, Color color, float intensity, float range, bool shadows = false)
        {
            var go = new GameObject("PointLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        protected Light AddSpotLight(Transform parent, Vector3 pos, Vector3 euler, Color color, float intensity, float range, float angle)
        {
            var go = new GameObject("SpotLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.6f;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>Invisible boundary walls so the player stays in the compact playable area.</summary>
        protected void AddBounds(Vector3 center, Vector3 size, float height = 4f)
        {
            var g = Group("Bounds");
            float t = 0.3f;
            void Wall(Vector3 c, Vector3 s)
            {
                var go = new GameObject("Bound");
                go.transform.SetParent(g, false);
                go.transform.localPosition = c;
                go.AddComponent<BoxCollider>().size = s;
            }
            Wall(center + new Vector3(0, height / 2, size.z / 2 + t / 2), new Vector3(size.x + 2 * t, height, t));
            Wall(center + new Vector3(0, height / 2, -size.z / 2 - t / 2), new Vector3(size.x + 2 * t, height, t));
            Wall(center + new Vector3(size.x / 2 + t / 2, height / 2, 0), new Vector3(t, height, size.z));
            Wall(center + new Vector3(-size.x / 2 - t / 2, height / 2, 0), new Vector3(t, height, size.z));
        }

        protected void SetEncounter(Encounter e)
        {
            Encounter = e;
            e.Cell = this;
        }
    }

    /// <summary>Per-cell gameplay logic (puzzle, threat choreography, verification).</summary>
    public abstract class Encounter : MonoBehaviour
    {
        public Cell Cell;
        public CountryDef Country => Cell != null ? Cell.Country : null;
        /// <summary>True between OnEnter (after the arrival fade) and OnExit. Updates must not run before OnEnter.</summary>
        public bool Entered;
        public bool IsLive => Cell != null && Cell.Mode == CellMode.Active && Entered;

        public virtual string Objective => Country != null ? Country.Objective : "";

        /// <summary>Called when the player arrives (after the fade). firstVisit = never visited before.</summary>
        public virtual void OnEnter(bool firstVisit) { }
        public virtual void OnExit() { }
        /// <summary>Reset local puzzle state after a failure/retry (never touches verified seals).</summary>
        public virtual void ResetLocal() { }
        public virtual void OnPocketShown() { }
    }
}
