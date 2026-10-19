using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>Welcome vignette: a passport and the glasses under a warm desk lamp; the window's reflection is
    /// almost, but not quite, the room (a figure stands behind you only in the reflection, fading in slowly).</summary>
    public class MenuCell : Cell
    {
        Material m_GhostFigure;
        float m_T;

        protected override void Build()
        {
            Id = CellId.Menu;
            Lighting = new LightingPreset
            {
                SunColor = new Color(0.35f, 0.42f, 0.6f), SunIntensity = 0.12f, SunEuler = new Vector3(40f, 160f, 0f), ShadowStrength = 0.3f,
                AmbientSky = new Color(0.2f, 0.2f, 0.25f), AmbientEquator = new Color(0.17f, 0.14f, 0.12f), AmbientGround = new Color(0.07f, 0.06f, 0.05f),
                FogColor = new Color(0.04f, 0.045f, 0.06f), FogDensity = 0.01f,
                SkyZenith = new Color(0.02f, 0.025f, 0.05f), SkyHorizon = new Color(0.12f, 0.1f, 0.12f), SkyGround = new Color(0.03f, 0.03f, 0.04f),
                SunDiscColor = new Color(0, 0, 0, 0), Stars = 0.4f,
                PostExposure = 0.3f, Saturation = -5f, Contrast = 12f, ColorFilter = new Color(1f, 0.96f, 0.9f), Bloom = 0.7f, Vignette = 0.32f
            };
            Ambience = "amb_apartment";
            Music = "mus_menu";
            AddSun();

            var floor = Mats.Lit(new Color(0.42f, 0.3f, 0.2f), 0.45f, 0f, "wood_oak_floor");
            var wall = Mats.Lit(new Color(0.72f, 0.66f, 0.58f), 0.2f, 0f, "plaster_warm");
            var wood = Mats.Lit(new Color(0.3f, 0.19f, 0.12f), 0.55f, 0f, "wood_dark");
            var brass = Mats.Lit(new Color(0.75f, 0.58f, 0.3f), 0.65f, 0.9f);
            var g = Group("Room");
            MeshFactory.Floor("Floor", g, new Vector3(0, 0, 0), new Vector2(6f, 6f), floor, "wood", 0.2f, 1f);
            MeshFactory.Box("BackWallL", g, new Vector3(-2.1f, 1.5f, 3f), new Vector3(1.8f, 3f, 0.2f), wall, true, 1.5f);
            MeshFactory.Box("BackWallR", g, new Vector3(2.1f, 1.5f, 3f), new Vector3(1.8f, 3f, 0.2f), wall, true, 1.5f);
            MeshFactory.Box("BackWallLow", g, new Vector3(0, 0.45f, 3f), new Vector3(2.4f, 0.9f, 0.2f), wall, true, 1.5f);
            MeshFactory.Box("BackWallTop", g, new Vector3(0, 2.75f, 3f), new Vector3(2.4f, 0.5f, 0.2f), wall, true, 1.5f);
            MeshFactory.Box("SideL", g, new Vector3(-3f, 1.5f, 0), new Vector3(0.2f, 3f, 6f), wall, true, 1.5f);
            MeshFactory.Box("SideR", g, new Vector3(3f, 1.5f, 0), new Vector3(0.2f, 3f, 6f), wall, true, 1.5f);
            MeshFactory.Box("Ceiling", g, new Vector3(0, 3.05f, 0), new Vector3(6f, 0.1f, 6f), wall, false, 2f);
            MeshFactory.Box("Back", g, new Vector3(0, 1.5f, -3f), new Vector3(6f, 3f, 0.2f), wall, true, 1.5f);
            // Window glass with the night city beyond.
            var glass = MeshFactory.Box("Window", g, new Vector3(0, 1.8f, 3f), new Vector3(2.4f, 1.8f, 0.02f), Mats.Glass(new Color(0.25f, 0.3f, 0.35f, 0.18f)), false);
            glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Landmarks.Skyline(g, new Vector3(0, -20f, 3f), 140f, 70f, 0f, 22, Mats.Backdrop(new Color(0.06f, 0.07f, 0.1f), Lighting), Mats.Backdrop(new Color(1f, 0.8f, 0.55f), Lighting, new Color(1.2f, 0.85f, 0.5f)), 7);

            // Desk, lamp, passport, glasses, case.
            MeshFactory.Box("DeskTop", g, new Vector3(0, 0.76f, 1.6f), new Vector3(1.5f, 0.05f, 0.75f), wood, true);
            foreach (var x in new[] { -0.68f, 0.68f }) foreach (var z in new[] { 1.3f, 1.9f })
                MeshFactory.Box("Leg", g, new Vector3(x, 0.37f, z), new Vector3(0.05f, 0.74f, 0.05f), wood, false);
            MeshFactory.Cylinder("LampBase", g, new Vector3(0.5f, 0.785f, 1.8f), 0.08f, 0.02f, brass, 18, false);
            MeshFactory.Cylinder("LampStem", g, new Vector3(0.5f, 0.8f, 1.8f), 0.012f, 0.42f, brass, 8, false);
            var shade = MeshFactory.Frustum("Shade", g, new Vector3(0.48f, 1.16f, 1.76f), 0.15f, 0.07f, 0.14f, Mats.Emissive(new Color(0.12f, 0.25f, 0.2f), new Color(0.35f, 0.3f, 0.15f)), 18);
            shade.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            AddSpotLight(g, new Vector3(0.45f, 1.15f, 1.72f), new Vector3(85f, 0, 0), new Color(1f, 0.76f, 0.48f), 2.2f, 3.5f, 100f).shadows = LightShadows.Soft;
            AddPointLight(g, new Vector3(0.2f, 2.5f, 0.6f), new Color(1f, 0.82f, 0.6f), 0.9f, 7f);
            AddPointLight(g, new Vector3(0.4f, 1.1f, 1.5f), new Color(1f, 0.7f, 0.42f), 0.5f, 4f);

            var passport = PropLibrary.PassportClosed(g);
            passport.transform.localPosition = new Vector3(-0.18f, 0.785f, 1.55f);
            passport.transform.localRotation = Quaternion.Euler(0, -14f, 0);
            var glasses = PropLibrary.Glasses(g, true);
            glasses.transform.localPosition = new Vector3(0.12f, 0.8f, 1.5f);
            glasses.transform.localRotation = Quaternion.Euler(-80f, 20f, 0);
            var gcase = PropLibrary.GlassesCase(g, true);
            gcase.transform.localPosition = new Vector3(0.18f, 0.785f, 1.78f);
            gcase.transform.localRotation = Quaternion.Euler(0, 8f, 0);

            // The reflection: a faint mirrored lamp glow and a figure standing behind the player, fading in.
            m_GhostFigure = Mats.UnlitInstance(new Color(0.05f, 0.06f, 0.07f, 0f), Tex.SoftDot, true, 1f);
            var fig = MeshFactory.Quad("ReflectionFigure", g, 0.55f, 1.9f, m_GhostFigure, new Vector3(-0.55f, 1.4f, 3.12f));
            fig.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var lampGhost = Mats.UnlitInstance(new Color(1f, 0.7f, 0.4f, 0.1f), Tex.SoftDot, true, 1.5f);
            lampGhost.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            MeshFactory.Quad("LampReflection", g, 0.35f, 0.35f, lampGhost, new Vector3(0.48f, 1.2f, 3.1f));

            var anchor = new GameObject("MenuAnchor").transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = new Vector3(-0.42f, 1.42f, 1.15f);

            // Brightness calibration card on the desk: a near-black square inside black (unlit, so only exposure changes it).
            MeshFactory.Quad("CalibrationCard", g, 0.16f, 0.11f, Mats.Unlit(new Color(0f, 0f, 0f)), new Vector3(0.62f, 0.79f, 1.42f), Quaternion.Euler(70f, 0, 0));
            MeshFactory.Quad("CalibrationSquare", g, 0.05f, 0.05f, Mats.Unlit(new Color(0.022f, 0.022f, 0.022f)), new Vector3(0.62f, 0.7905f, 1.4185f), Quaternion.Euler(70f, 0, 0));
            SetArrival(new Vector3(0, 0, 0.1f), 0f);
            AddBounds(Vector3.zero, new Vector3(5.6f, 3f, 5.6f));
        }

        void Update()
        {
            if (Mode != CellMode.Active || m_GhostFigure == null) return;
            m_T += Time.unscaledDeltaTime;
            // Almost imperceptible: reaches only 18% opacity after ~90 s, and only in the glass.
            float a = Mathf.Clamp01((m_T - 20f) / 70f) * 0.18f;
            m_GhostFigure.SetColor("_Color", new Color(0.05f, 0.06f, 0.07f, a));
        }
    }
}
