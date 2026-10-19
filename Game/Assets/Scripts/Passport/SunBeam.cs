using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Registry of gameplay light sources (borrowed sunlight, reflector beams, lamps, lanterns). Light is a
    /// deterministic gameplay volume with a matching visual — the render of the destination does not "carry" light.
    /// </summary>
    public static class LightVolumes
    {
        public interface ISource
        {
            /// <summary>0..1 exposure of a world point to this light.</summary>
            float ExposureAt(Vector3 point);
            bool IsSunlight { get; }
        }

        static readonly List<ISource> s_Sources = new List<ISource>();

        public static void Register(ISource s) { if (!s_Sources.Contains(s)) s_Sources.Add(s); }
        public static void Unregister(ISource s) => s_Sources.Remove(s);

        public static float Exposure(Vector3 p, bool sunlightOnly = false)
        {
            float e = 0f;
            foreach (var s in s_Sources)
            {
                if (sunlightOnly && !s.IsSunlight) continue;
                e = Mathf.Max(e, s.ExposureAt(p));
            }
            return e;
        }

        public static void Clear() => s_Sources.Clear();
    }

    /// <summary>
    /// A pool of light around a lamp (a vertical cylinder): anything in it is fully exposed. Used where a lamp makes a
    /// safe place, e.g. the Marrakech counter, so the rule "light pushes it back" holds for the whole lit area.
    /// </summary>
    public class LightPool : MonoBehaviour, LightVolumes.ISource
    {
        public float Radius = 2.4f, Height = 3f, Strength = 1f;
        public Cell OwnerCell;
        public bool IsSunlight => false;

        void OnEnable() => LightVolumes.Register(this);
        void OnDisable() => LightVolumes.Unregister(this);

        public float ExposureAt(Vector3 p)
        {
            if (Strength <= 0.01f || (OwnerCell != null && OwnerCell.Mode != CellMode.Active)) return 0f;
            var local = transform.InverseTransformPoint(p);
            if (local.y < -0.2f || local.y > Height) return 0f;
            float d = new Vector2(local.x, local.z).magnitude;
            return d > Radius ? 0f : Strength * Mathf.Clamp01((Radius - d) / 0.4f + 0.5f);
        }
    }

    /// <summary>A cone of light: visual shaft + real spot light + gameplay volume.</summary>
    public class LightCone : MonoBehaviour, LightVolumes.ISource
    {
        public float Length = 9f;
        public float StartRadius = 0.2f;
        public float EndRadius = 1.4f;
        public float Strength = 1f;
        public bool Sunlight = true;
        public bool CheckOcclusion = true;

        protected Light m_Spot;
        protected Material m_Mat;
        protected GameObject m_Shaft;
        protected Color m_Color;

        public bool IsSunlight => Sunlight;

        public void Build(Color color, float intensity)
        {
            m_Color = color;
            m_Mat = Mats.BeamInstance(color, intensity);
            var b = new MeshBuilder();
            // Cone along +Z: build a frustum along +Y, then rotate so +Y maps to +Z.
            b.AddFrustum(Vector3.zero, 1f, 1f, 1f, 28, false, false, 1f, Quaternion.identity);
            m_Shaft = MeshFactory.Make("Shaft", b.ToMesh("Shaft"), m_Mat, transform, Vector3.zero, Quaternion.Euler(90f, 0, 0));
            m_Shaft.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mf = m_Shaft.GetComponent<MeshFilter>();
            // Remap vertices into a cone (radius grows along length) with uv.y = 0..1 along the beam.
            var mesh = mf.sharedMesh;
            var v = mesh.vertices;
            var uv = mesh.uv;
            for (int i = 0; i < v.Length; i++)
            {
                float t = Mathf.Clamp01(v[i].y);
                float r = Mathf.Lerp(StartRadius, EndRadius, t);
                v[i] = new Vector3(v[i].x * r, t * Length, v[i].z * r);
                uv[i] = new Vector2(uv[i].x, t);
            }
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            m_Spot = new GameObject("Spot").AddComponent<Light>();
            m_Spot.transform.SetParent(transform, false);
            m_Spot.type = LightType.Spot;
            m_Spot.color = color;
            m_Spot.range = Length * 1.4f;
            m_Spot.spotAngle = Mathf.Clamp(2f * Mathf.Atan2(EndRadius, Length) * Mathf.Rad2Deg * 1.4f, 20f, 80f);
            m_Spot.innerSpotAngle = m_Spot.spotAngle * 0.6f;
            m_Spot.shadows = LightShadows.None;
            LightVolumes.Register(this);
        }

        bool m_ShaftHidden;

        /// <summary>Hide the visible shaft but keep the light and the gameplay volume (for lamps whose cone would fill the view).</summary>
        public void SetShaftVisible(bool on)
        {
            m_ShaftHidden = !on;
            if (m_Shaft != null) m_Shaft.SetActive(on && Strength > 0.01f);
        }

        public void SetStrength(float s)
        {
            Strength = Mathf.Clamp01(s);
            if (m_Mat != null) m_Mat.SetFloat("_Intensity", 1.4f * Strength);
            if (m_Spot != null) m_Spot.intensity = 9f * Strength;
            if (m_Shaft != null) m_Shaft.SetActive(Strength > 0.01f && !m_ShaftHidden);
            if (m_Spot != null) m_Spot.enabled = Strength > 0.01f;
        }

        /// <summary>If set, the light only acts while this cell is the active world (not when seen as a pocket).</summary>
        public Cell OwnerCell;

        public float ExposureAt(Vector3 p)
        {
            if (Strength <= 0.01f || !isActiveAndEnabled) return 0f;
            if (OwnerCell != null && OwnerCell.Mode != CellMode.Active) return 0f;
            var local = transform.InverseTransformPoint(p);
            if (local.z < 0f || local.z > Length) return 0f;
            float r = Mathf.Lerp(StartRadius, EndRadius, local.z / Length);
            float d = new Vector2(local.x, local.y).magnitude;
            if (d > r * 1.15f) return 0f;
            if (CheckOcclusion && Physics.Linecast(transform.position + transform.forward * 0.3f, p, 1 << Layers.Default, QueryTriggerInteraction.Ignore))
                return 0f;
            float edge = 1f - Mathf.Clamp01((d - r * 0.7f) / (r * 0.45f));
            return Strength * edge;
        }

        protected virtual void OnDestroy() => LightVolumes.Unregister(this);
    }

    /// <summary>Egypt's sunlight pouring out of an open tear into the current world.</summary>
    public class SunBeam : LightCone
    {
        Tear m_Tear;

        public void Init(Tear tear)
        {
            m_Tear = tear;
            Length = 10f;
            StartRadius = 0.25f;
            EndRadius = 1.6f;
            Sunlight = true;
            // Sunlight enters very slightly downward, out of the opening along the seam's forward.
            transform.localRotation = Quaternion.Euler(3f, 0, 0);
            Build(new Color(1f, 0.82f, 0.55f), 1.2f);
            SetStrength(0f);
            AudioDirector.I?.Play("sunbeam_on", tear.transform.position, 0.8f);
            m_Loop = AudioDirector.I?.Loop("sunbeam_loop", transform, 0.4f, Bus.SFX, 1f, 2f, 16f);
        }

        AudioSource m_Loop;

        void LateUpdate()
        {
            if (m_Tear == null) return;
            transform.position = m_Tear.OpeningCenter + m_Tear.Anchor.Forward * 0.05f;
            StartRadius = Mathf.Max(0.05f, m_Tear.Width * 0.45f);
            if (m_Loop != null) AudioDirector.I.SetBaseVolume(m_Loop, 0.15f + Strength * 0.45f);
        }

        public void Shutdown()
        {
            if (m_Loop != null) AudioDirector.I?.FadeOutAndStop(m_Loop, 0.4f);
            LightVolumes.Unregister(this);
        }
    }
}
