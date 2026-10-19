using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// One open tear between the active cell and a destination cell. The opening is a stencil "hole" (TearMark +
    /// TearClear materials) through which the destination pocket (real 3D geometry, PocketLit) is rendered — correct
    /// in both eyes because it is ordinary geometry, not a render texture.
    /// </summary>
    public class Tear : MonoBehaviour
    {
        public SeamAnchor Anchor;
        public CountryDef Destination;
        public Cell DestinationCell;
        public float Aperture;          // 0..1
        public float Contamination;     // 0..1
        public float SealProgress;      // 0..1 while the player holds Seal
        public bool Closing;

        Transform m_Opening;
        Material m_Mark, m_Clear, m_RimMat;
        Light m_Light;
        AudioSource m_Hum, m_Bleed;
        ParticleSystem m_Fibres;
        SunBeam m_Beam;
        Interactable m_StepThrough;
        float m_PassengerPressure;

        public TearSize Size =>
            Aperture >= TearController.DoorThreshold ? TearSize.Doorway :
            Aperture >= TearController.WindowThreshold ? TearSize.Window :
            Aperture >= TearController.SlitThreshold ? TearSize.Slit : TearSize.Closed;

        public float Width => Mathf.Lerp(0.035f, 1.15f, Mathf.SmoothStep(0f, 1f, Aperture));
        public float Height => Mathf.Lerp(1.05f, 2.15f, Aperture);
        /// <summary>Slits and windows sit at the seam's height; a doorway grows down to the floor so you can walk through.</summary>
        public Vector3 OpeningCenter
        {
            get
            {
                float doorBlend = Mathf.InverseLerp(TearController.WindowThreshold, TearController.DoorThreshold, Aperture);
                float y = Mathf.Lerp(Anchor.CenterHeight, Height * 0.5f + 0.02f, doorBlend);
                return Anchor.FloorPoint + Vector3.up * y;
            }
        }
        public SunBeam Beam => m_Beam;

        /// <summary>Extra contamination pressure (e.g. the Passenger pressing on the other side), 0..1.</summary>
        public void SetPassengerPressure(float p) => m_PassengerPressure = Mathf.Clamp01(p);

        public void Init(SeamAnchor anchor, CountryDef dest, Cell destCell, LightingPreset destLighting)
        {
            Anchor = anchor;
            Destination = dest;
            DestinationCell = destCell;
            transform.position = anchor.Center;
            transform.rotation = anchor.transform.rotation;

            m_Opening = new GameObject("Opening").transform;
            m_Opening.SetParent(transform, false);
            var quad = MeshFactory.QuadMesh(1f, 1f, "TearQuad");
            m_Mark = Mats.TearMark();
            m_Clear = Mats.TearClear(destLighting != null ? destLighting.SkyHorizon : Color.gray);
            var markGo = MeshFactory.Make("TearMark", quad, m_Mark, m_Opening);
            var clearGo = MeshFactory.Make("TearClear", quad, m_Clear, m_Opening);
            clearGo.GetComponent<MeshRenderer>().sharedMaterial = m_Clear;
            foreach (var r in new[] { markGo.GetComponent<Renderer>(), clearGo.GetComponent<Renderer>() })
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            // The clear pass must draw the same shape: use the mask alpha via a cutout child mesh built from the
            // texture outline is overkill; the clear pass is limited by stencil == 1 written by the mark pass.

            BuildRim(dest.Tint);
            m_Light = new GameObject("TearLight").AddComponent<Light>();
            m_Light.transform.SetParent(transform, false);
            m_Light.transform.localPosition = Vector3.forward * 0.25f;
            m_Light.type = LightType.Point;
            m_Light.range = 4f;
            m_Light.color = Color.Lerp(dest.Tint, new Color(0.6f, 1f, 0.9f), 0.35f);
            m_Light.intensity = 0f;
            m_Light.shadows = LightShadows.None;

            m_Hum = AudioDirector.I?.Loop("tear_hum_loop", transform, 0f, Bus.SFX, 1f, 1.5f, 18f);
            var amb = CellManager.AmbienceOf(destCell != null ? destCell.Id : CellId.None);
            if (!string.IsNullOrEmpty(amb)) m_Bleed = AudioDirector.I?.Loop(amb, transform, 0f, Bus.Ambience, 1f, 1.5f, 20f);

            if (dest.Id == CountryId.Egypt && (Game.Campaign.ResourceFlowsFrom(CountryId.Egypt, ResourceId.Sunlight)))
            {
                m_Beam = new GameObject("SunBeam").AddComponent<SunBeam>();
                m_Beam.transform.SetParent(transform, false);
                m_Beam.Init(this);
            }

            m_StepThrough = new GameObject("StepThrough").AddComponent<Interactable>();
            m_StepThrough.transform.SetParent(transform, false);
            var col = m_StepThrough.gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1f, 2f, 0.3f);
            m_StepThrough.Prompt = "Step through to " + dest.DisplayName;
            m_StepThrough.MaxDistance = 2.2f;
            m_StepThrough.Usable = false;
            m_StepThrough.OnUse = _ => Game.Tears?.Traverse();
            UpdateVisuals();
        }

        void BuildRim(Color tint)
        {
            m_RimMat = Mats.UnlitInstance(Color.Lerp(tint, new Color(0.7f, 1f, 0.92f), 0.5f), Tex.Fibre, true, 2.2f);
            m_RimMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m_RimMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m_RimMat.SetFloat("_Cull", 0f);

            // Fibres along the almond outline (unit size; scaled with the opening).
            var b = new MeshBuilder();
            int n = 46;
            var rnd = new System.Random(7);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < n; i++)
                {
                    float fy = (i + 0.5f) / n;
                    float prof = Mathf.Pow(Mathf.Sin(fy * Mathf.PI), 0.8f) * 0.46f;
                    var p = new Vector3(side * prof, fy - 0.5f, 0);
                    float len = 0.04f + (float)rnd.NextDouble() * 0.09f;
                    float ang = (float)(rnd.NextDouble() - 0.5) * 1.4f;
                    var dir = new Vector3(side * Mathf.Cos(ang), Mathf.Sin(ang), 0);
                    var perp = new Vector3(-dir.y, dir.x, 0) * 0.006f;
                    var a = p - dir * 0.01f;
                    var e = p + dir * len;
                    b.AddQuad(a - perp, e - perp, e + perp, a + perp, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                }
            var rim = MeshFactory.Make("Rim", b.ToMesh("TearRim"), m_RimMat, m_Opening, Vector3.forward * 0.004f);
            rim.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Drifting fibres / ink motes.
            var psGo = new GameObject("Fibres");
            psGo.transform.SetParent(transform, false);
            m_Fibres = psGo.AddComponent<ParticleSystem>();
            var main = m_Fibres.main;
            main.startLifetime = 1.6f;
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.035f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 1f, 0.9f, 0.7f), new Color(0.35f, 0.08f, 0.08f, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            var emission = m_Fibres.emission;
            emission.rateOverTime = 0f;
            var shape = m_Fibres.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.05f, 1f, 0.02f);
            var r = psGo.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, true, Color.white, 1.4f, Mats.StencilAlways);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!Closing && Aperture >= TearController.SlitThreshold)
            {
                float rate = Size == TearSize.Doorway ? 0.075f : Size == TearSize.Window ? 0.06f : 0.018f;
                rate *= Game.Story != null ? Game.Story.ContaminationScale : 1f;
                rate *= 1f + m_PassengerPressure * 2.5f;
                Contamination = Mathf.Clamp01(Contamination + rate * dt);
            }
            UpdateVisuals();
            m_StepThrough.Usable = !Closing && Size == TearSize.Doorway && Anchor.AllowTraverse;
        }

        public void UpdateVisuals()
        {
            if (m_Opening == null) return;
            float w = Width, h = Height;
            m_Opening.position = OpeningCenter;
            m_Opening.localScale = new Vector3(w, h, 1f);
            float open = Mathf.Clamp01(Aperture / TearController.SlitThreshold);
            m_Opening.gameObject.SetActive(Aperture > 0.003f);
            if (m_Light != null) m_Light.intensity = Mathf.Lerp(0f, 2.2f, Aperture) * open;
            if (m_RimMat != null)
            {
                float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 3f, 0.3f);
                m_RimMat.SetFloat("_Intensity", (1.4f + Contamination * 2f) * flicker * open);
            }
            if (m_Fibres != null)
            {
                var e = m_Fibres.emission;
                e.rateOverTime = 10f + Aperture * 40f + Contamination * 30f;
                var shape = m_Fibres.shape;
                shape.scale = new Vector3(Mathf.Max(0.05f, w), h, 0.02f);
                m_Fibres.transform.position = OpeningCenter;
            }
            if (m_Hum != null) AudioDirector.I.SetBaseVolume(m_Hum, 0.15f + Aperture * 0.55f);
            if (m_Bleed != null) AudioDirector.I.SetBaseVolume(m_Bleed, Mathf.Lerp(0.05f, 0.9f, Aperture));
            if (m_StepThrough != null)
            {
                m_StepThrough.transform.position = OpeningCenter;
                m_StepThrough.transform.localScale = new Vector3(w, h * 0.5f, 1f);
            }
            if (m_Beam != null) m_Beam.SetStrength(Size == TearSize.Closed ? 0f : Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(TearController.SlitThreshold, TearController.WindowThreshold, Aperture)));
        }

        /// <summary>Whether a world point is inside the opening (in its plane, with a depth tolerance).</summary>
        public bool InsideOpening(Vector3 p, float depthTolerance = 0.45f)
        {
            var local = Anchor.transform.InverseTransformPoint(p);
            var c = Anchor.transform.InverseTransformPoint(OpeningCenter);
            float dx = (local.x - c.x) / Mathf.Max(0.01f, Width * 0.5f);
            float dy = (local.y - c.y) / Mathf.Max(0.01f, Height * 0.5f);
            return dx * dx + dy * dy <= 1f && Mathf.Abs(local.z) <= depthTolerance;
        }

        public void Cleanup()
        {
            if (m_Hum != null) AudioDirector.I?.FadeOutAndStop(m_Hum, 0.4f);
            if (m_Bleed != null) AudioDirector.I?.FadeOutAndStop(m_Bleed, 0.6f);
            if (m_Beam != null) m_Beam.Shutdown();
            Destroy(gameObject);
        }
    }
}
