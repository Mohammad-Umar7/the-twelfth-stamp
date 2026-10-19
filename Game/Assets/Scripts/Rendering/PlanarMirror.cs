using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace TwelfthStamp
{
    /// <summary>
    /// A real planar mirror. While the glass is roughly in view, one reflection camera per eye (one on desktop) renders
    /// the room mirrored across the glass into a texture just before the player camera draws, and TS_PlanarMirror samples
    /// it at the glass's own screen position: the image lines up exactly and has true stereo depth in VR.
    /// Reflection cameras see the player's full body (<see cref="Layers.MirrorOnly"/>) and never the first-person arms
    /// (<see cref="Layers.PlayerBody"/>), the 3D UI or any mirror glass. The player camera is only read, never moved.
    /// How: the eye's view is reflected across the glass plane, the near plane is made oblique so it lies on the glass
    /// (nothing behind the wall gets in), and x is mirrored in the projection so triangles keep their winding (no
    /// inverted culling needed); the shader samples at 1 - u. Screen-space AO, which assumes a standard depth mapping, is
    /// paused while a reflection draws (URP's fog is per pixel from the view matrix, so it stays correct).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlanarMirror : MonoBehaviour
    {
        public static readonly List<PlanarMirror> All = new List<PlanarMirror>();

        const float ClipOffset = 0.012f;  // geometry within 12 mm in front of the glass (the glass, its bevel) stays out
        const float MaxDistance = 12f;

        /// <summary>Tests only: leave the player's body out of reflections, to prove it is what the mirror shows.</summary>
        public static bool TestHideBody;

        static readonly HashSet<Camera> s_ReflectionCams = new HashSet<Camera>();
        static readonly Matrix4x4 s_FlipX = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
        static readonly List<XRDisplaySubsystem> s_Displays = new List<XRDisplaySubsystem>();
        static ScreenSpaceAmbientOcclusion[] s_Ssao;
        static readonly List<ScriptableRendererFeature> s_PausedFeatures = new List<ScriptableRendererFeature>();
        static int s_Subscribers;

        Renderer m_Glass;
        Material m_Mat;
        Vector3 m_LocalPoint, m_LocalNormal, m_LocalCenter, m_LocalU, m_LocalV;
        float m_HalfU, m_HalfV;
        readonly Camera[] m_Cams = new Camera[2];
        readonly RenderTexture[] m_Tex = new RenderTexture[2];
        int m_Eyes = 1;
        bool m_Live;
        int m_RenderedFrame = -1;

        /// <summary>True while the reflection cameras are drawing (the glass is in view).</summary>
        public bool Live => m_Live;
        /// <summary>The last frame a reflection was drawn (-1 before the first).</summary>
        public int RenderedFrame => m_RenderedFrame;
        public Renderer Glass => m_Glass;
        public Camera ReflectionCamera(int eye) => m_Cams[Mathf.Clamp(eye, 0, 1)];
        public RenderTexture ReflectionTexture(int eye) => m_Tex[Mathf.Clamp(eye, 0, 1)];

        /// <summary>
        /// Turns <paramref name="glass"/> into a live mirror whose reflecting face points along
        /// <paramref name="worldNormal"/> (into the room). <paramref name="wrongness"/> 0..1 tints the reflection cold and
        /// dim, for the Margin's copy of home.
        /// </summary>
        public static PlanarMirror Attach(Renderer glass, Vector3 worldNormal, float wrongness = 0f)
        {
            if (glass == null || worldNormal.sqrMagnitude < 1e-6f) return null;
            var mirror = glass.GetComponent<PlanarMirror>();
            if (mirror == null) mirror = glass.gameObject.AddComponent<PlanarMirror>();
            mirror.Setup(glass, worldNormal.normalized, wrongness);
            return mirror;
        }

        void Setup(Renderer glass, Vector3 n, float wrongness)
        {
            m_Glass = glass;
            glass.gameObject.layer = Layers.MirrorGlass;
            glass.shadowCastingMode = ShadowCastingMode.Off;
            glass.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // The plane is the glass's front face: the bounds' extreme along the normal. In-plane axes for the shader's
            // edge darkening come from the same bounds.
            var b = glass.bounds;
            var up = Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up;
            var u = Vector3.Cross(up, n).normalized;
            var v = Vector3.Cross(n, u);
            var front = b.center + n * Support(b.extents, n);
            var t = transform;
            m_LocalPoint = t.InverseTransformPoint(front);
            m_LocalCenter = m_LocalPoint;
            m_LocalNormal = t.InverseTransformDirection(n);
            m_LocalU = t.InverseTransformDirection(u);
            m_LocalV = t.InverseTransformDirection(v);
            m_HalfU = Support(b.extents, u);
            m_HalfV = Support(b.extents, v);

            if (m_Mat == null) m_Mat = new Material(Mats.Template("TS_PlanarMirror")) { name = "PlanarMirror" };
            m_Mat.SetFloat("_Wrong", Mathf.Clamp01(wrongness));
            m_Mat.SetFloat("_ReflAmount", 0f);
            glass.sharedMaterial = m_Mat;
            UpdateGlassAxes();
        }

        static float Support(Vector3 extents, Vector3 dir) => Mathf.Abs(dir.x) * extents.x + Mathf.Abs(dir.y) * extents.y + Mathf.Abs(dir.z) * extents.z;

        /// <summary>The glass's front face: a point on it and its normal (world), pointing into the room.</summary>
        public void GetPlane(out Vector3 point, out Vector3 normal)
        {
            var t = transform;
            point = t.TransformPoint(m_LocalPoint);
            normal = t.TransformDirection(m_LocalNormal).normalized;
        }

        void UpdateGlassAxes()
        {
            if (m_Mat == null) return;
            var t = transform;
            var c = t.TransformPoint(m_LocalCenter);
            m_Mat.SetVector("_GlassCenter", new Vector4(c.x, c.y, c.z, 0f));
            var u = t.TransformDirection(m_LocalU).normalized;
            var v = t.TransformDirection(m_LocalV).normalized;
            m_Mat.SetVector("_GlassU", new Vector4(u.x, u.y, u.z, m_HalfU));
            m_Mat.SetVector("_GlassV", new Vector4(v.x, v.y, v.z, m_HalfV));
        }

        void OnEnable()
        {
            All.Add(this);
            if (s_Subscribers++ == 0)
            {
                RenderPipelineManager.beginContextRendering += OnBeginContext;
                RenderPipelineManager.beginCameraRendering += OnBeginCamera;
                RenderPipelineManager.endCameraRendering += OnEndCamera;
            }
        }

        void OnDisable()
        {
            All.Remove(this);
            if (--s_Subscribers == 0)
            {
                RenderPipelineManager.beginContextRendering -= OnBeginContext;
                RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
                RenderPipelineManager.endCameraRendering -= OnEndCamera;
            }
            SetLive(false, null, false);
        }

        void OnDestroy()
        {
            for (int i = 0; i < 2; i++)
            {
                if (m_Cams[i] != null) { s_ReflectionCams.Remove(m_Cams[i]); Destroy(m_Cams[i].gameObject); }
                if (m_Tex[i] != null) { m_Tex[i].Release(); Destroy(m_Tex[i]); }
            }
            if (m_Mat != null) Destroy(m_Mat);
        }

        // ------------------------------------------------------------------ when to draw

        void LateUpdate()
        {
            var main = Game.Rig != null ? Game.Rig.Cam : null;
            bool vr = Game.Mode == GameMode.VR && XRSettings.isDeviceActive;
            bool want = main != null && main.isActiveAndEnabled && m_Glass != null && m_Glass.enabled
                        && m_Glass.gameObject.layer == Layers.MirrorGlass   // a cell seen through a tear switches to Pocket
                        && InView(main, vr);
            SetLive(want, main, vr);
            if (want) UpdateGlassAxes();
        }

        bool InView(Camera main, bool vr)
        {
            GetPlane(out var p, out var n);
            var eye = main.transform.position;
            float side = Vector3.Dot(eye - p, n);
            if (side < 0.02f || side > MaxDistance) return false;
            var b = m_Glass.bounds;
            var to = b.center - eye;
            float dist = to.magnitude, radius = b.extents.magnitude;
            if (dist > MaxDistance + radius) return false;
            if (dist > radius)
            {
                float halfFov = vr ? 80f : Mathf.Max(main.fieldOfView, Camera.VerticalToHorizontalFieldOfView(main.fieldOfView, main.aspect)) * 0.5f + 12f;
                float angle = Vector3.Angle(main.transform.forward, to) - Mathf.Asin(Mathf.Clamp01(radius / dist)) * Mathf.Rad2Deg;
                if (angle > halfFov) return false;
            }
            // A wall between the eye and every corner of the glass hides it (doors and props never count).
            const int blockers = ~((1 << Layers.PlayerBody) | (1 << Layers.MirrorOnly) | (1 << Layers.MirrorGlass) | (1 << Layers.Pocket)
                                   | (1 << Layers.UI3D) | (1 << Layers.IgnoreRaycast) | (1 << Layers.Interactable) | (1 << Layers.Passenger));
            var c = b.center + n * 0.02f;
            var t = transform;
            var u = t.TransformDirection(m_LocalU).normalized * (m_HalfU * 0.9f);
            var v = t.TransformDirection(m_LocalV).normalized * (m_HalfV * 0.9f);
            if (!Physics.Linecast(eye, c, blockers, QueryTriggerInteraction.Ignore)) return true;
            for (int i = 0; i < 4; i++)
            {
                var corner = c + ((i & 1) == 0 ? u : -u) + ((i & 2) == 0 ? v : -v);
                if (!Physics.Linecast(eye, corner, blockers, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        void SetLive(bool on, Camera main, bool vr)
        {
            if (on) Prepare(main, vr);
            for (int i = 0; i < 2; i++)
                if (m_Cams[i] != null) m_Cams[i].enabled = on && i < m_Eyes;
            m_Live = on;
            if (!on && m_Mat != null) m_Mat.SetFloat("_ReflAmount", 0f);
        }

        float Scale(bool vr)
        {
            int p = Mathf.Clamp(Game.Settings != null ? Game.Settings.graphicsPreset : 2, 0, 3);
            return vr ? new[] { 0.4f, 0.5f, 0.6f, 0.7f }[p] : new[] { 0.5f, 0.65f, 0.8f, 1f }[p];
        }

        void Prepare(Camera main, bool vr)
        {
            m_Eyes = vr ? 2 : 1;
            float scale = Scale(vr);
            int w = vr ? XRSettings.eyeTextureWidth : main.pixelWidth;
            int h = vr ? XRSettings.eyeTextureHeight : main.pixelHeight;
            if (w <= 0 || h <= 0) { w = main.pixelWidth; h = main.pixelHeight; }
            w = Mathf.Max(64, Mathf.RoundToInt(w * scale));
            h = Mathf.Max(64, Mathf.RoundToInt(h * scale));
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            int msaa = urp != null && urp.msaaSampleCount > 1 ? 2 : 1;
            for (int i = 0; i < m_Eyes; i++)
            {
                if (m_Tex[i] == null || m_Tex[i].width != w || m_Tex[i].height != h || m_Tex[i].antiAliasing != msaa)
                {
                    if (m_Cams[i] != null) m_Cams[i].targetTexture = null;
                    if (m_Tex[i] != null) { m_Tex[i].Release(); Destroy(m_Tex[i]); }
                    m_Tex[i] = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGBHalf, 24) { msaaSamples = msaa })
                    {
                        name = "MirrorReflection" + i, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                    };
                    m_Tex[i].Create();
                }
                if (m_Cams[i] == null) m_Cams[i] = MakeCamera(i);
                var cam = m_Cams[i];
                cam.targetTexture = m_Tex[i];
                cam.depth = main.depth - 10f - i;
                cam.clearFlags = main.clearFlags;
                cam.backgroundColor = main.backgroundColor;
                cam.cullingMask = (main.cullingMask | (TestHideBody ? 0 : 1 << Layers.MirrorOnly))
                                  & ~((1 << Layers.PlayerBody) | (1 << Layers.UI3D) | (1 << Layers.MirrorGlass));
                cam.nearClipPlane = main.nearClipPlane;
                cam.farClipPlane = main.farClipPlane;
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                data.renderShadows = !vr; // two extra stereo views already cost enough in VR
            }
            m_Mat.SetTexture("_ReflTexL", m_Tex[0]);
            m_Mat.SetTexture("_ReflTexR", m_Eyes > 1 ? m_Tex[1] : m_Tex[0]);
        }

        Camera MakeCamera(int eye)
        {
            var go = new GameObject(eye == 0 ? "MirrorCamera_L" : "MirrorCamera_R");
            go.transform.SetParent(transform, false);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.useOcclusionCulling = false;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false; // linear HDR: the player camera grades the mirror with the room
            data.antialiasing = AntialiasingMode.None;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.allowXRRendering = false;
            s_ReflectionCams.Add(cam);
            return cam;
        }

        // ------------------------------------------------------------------ the reflected views

        /// <summary>After tracking has updated (onBeforeRender) and before any camera draws: aim each reflection camera.</summary>
        static void OnBeginContext(ScriptableRenderContext context, List<Camera> cameras)
        {
            for (int i = 0; i < All.Count; i++) All[i].AimCameras();
        }

        void AimCameras()
        {
            if (!m_Live) return;
            var main = Game.Rig != null ? Game.Rig.Cam : null;
            if (main == null) return;
            GetPlane(out var p, out var n);
            var reflect = ReflectionMatrix(new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, p)));
            for (int e = 0; e < m_Eyes; e++)
            {
                if (m_Cams[e] == null) continue;
                Matrix4x4 view, proj;
                if (m_Eyes == 2) EyeMatrices(main, e, out view, out proj);
                else { view = main.worldToCameraMatrix; proj = main.projectionMatrix; }
                Aim(m_Cams[e], view, proj, reflect, p, n);
            }
        }

        static void Aim(Camera rc, Matrix4x4 view, Matrix4x4 proj, Matrix4x4 reflect, Vector3 p, Vector3 n)
        {
            var rview = view * reflect;
            rc.worldToCameraMatrix = rview;
            // The transform only feeds _WorldSpaceCameraPos (specular), LOD and sorting: the reflected eye.
            var toWorld = rview.inverse;
            var forward = toWorld.MultiplyVector(Vector3.back);
            var up = toWorld.MultiplyVector(Vector3.up);
            if (forward.sqrMagnitude > 1e-8f && up.sqrMagnitude > 1e-8f)
                rc.transform.SetPositionAndRotation(toWorld.MultiplyPoint(Vector3.zero), Quaternion.LookRotation(forward, up));

            // Near plane on the glass (Lengyel's oblique frustum), computed in the reflected camera's space.
            var cpos = rview.MultiplyPoint(p + n * ClipOffset);
            var cnormal = rview.MultiplyVector(n).normalized;
            var clip = new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
            rc.projectionMatrix = proj;
            var oblique = rc.CalculateObliqueMatrix(clip);
            rc.projectionMatrix = s_FlipX * oblique;
            // Cull with the ordinary frustum: the oblique one's far plane is skewed and would drop visible things.
            rc.cullingMatrix = proj * rview;
        }

        static Matrix4x4 ReflectionMatrix(Vector4 plane)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1f - 2f * plane.x * plane.x; m.m01 = -2f * plane.x * plane.y; m.m02 = -2f * plane.x * plane.z; m.m03 = -2f * plane.w * plane.x;
            m.m10 = -2f * plane.y * plane.x; m.m11 = 1f - 2f * plane.y * plane.y; m.m12 = -2f * plane.y * plane.z; m.m13 = -2f * plane.w * plane.y;
            m.m20 = -2f * plane.z * plane.x; m.m21 = -2f * plane.z * plane.y; m.m22 = 1f - 2f * plane.z * plane.z; m.m23 = -2f * plane.w * plane.z;
            return m;
        }

        /// <summary>The eye's view and projection exactly as URP will render it: from the XR display, else the camera's stereo matrices.</summary>
        static void EyeMatrices(Camera main, int eye, out Matrix4x4 view, out Matrix4x4 proj)
        {
            SubsystemManager.GetSubsystems(s_Displays);
            foreach (var d in s_Displays)
            {
                if (d == null || !d.running) continue;
                int passes = d.GetRenderPassCount();
                if (passes == 1)
                {
                    d.GetRenderPass(0, out var pass);
                    if (pass.GetRenderParameterCount() > eye)
                    {
                        pass.GetRenderParameter(main, eye, out var rp);
                        view = rp.view; proj = rp.projection;
                        if (IsPerspective(proj)) return;
                    }
                }
                else if (passes > eye)
                {
                    d.GetRenderPass(eye, out var pass);
                    if (pass.GetRenderParameterCount() > 0)
                    {
                        pass.GetRenderParameter(main, 0, out var rp);
                        view = rp.view; proj = rp.projection;
                        if (IsPerspective(proj)) return;
                    }
                }
            }
            var stereoEye = eye == 0 ? Camera.StereoscopicEye.Left : Camera.StereoscopicEye.Right;
            view = main.GetStereoViewMatrix(stereoEye);
            proj = main.GetStereoProjectionMatrix(stereoEye);
            if (!IsPerspective(proj)) { view = main.worldToCameraMatrix; proj = main.projectionMatrix; }
        }

        static bool IsPerspective(Matrix4x4 proj) => proj.m32 < -0.5f && Mathf.Abs(proj.m00) > 1e-4f;

        // ------------------------------------------------------------------ per-camera state while a reflection draws

        static void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (!s_ReflectionCams.Contains(cam)) return;
            if (s_Ssao == null) s_Ssao = Resources.FindObjectsOfTypeAll<ScreenSpaceAmbientOcclusion>();
            s_PausedFeatures.Clear();
            foreach (var f in s_Ssao)
                if (f != null && f.isActive) { f.SetActive(false); s_PausedFeatures.Add(f); }
        }

        static void OnEndCamera(ScriptableRenderContext context, Camera cam)
        {
            if (!s_ReflectionCams.Contains(cam)) return;
            foreach (var f in s_PausedFeatures) if (f != null) f.SetActive(true);
            s_PausedFeatures.Clear();
            for (int i = 0; i < All.Count; i++)
            {
                var m = All[i];
                if (m.m_Cams[0] != cam && m.m_Cams[1] != cam) continue;
                m.m_RenderedFrame = Time.frameCount;
                // Drawn before the player camera in the same frame, so the glass shows it at once.
                if (m.m_Mat != null) m.m_Mat.SetFloat("_ReflAmount", 1f);
            }
        }
    }
}
