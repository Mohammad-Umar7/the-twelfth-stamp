using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// A postcard left behind by someone who passed through: three hidden in every country, the reason to explore beyond the
    /// puzzle (playtest: "gameplay should be really amazing and worth it"). It hangs in the air, turning slowly in a faint
    /// warm glow, with a mote or two drifting off it so it can be spotted down a street. Take it (E / trigger): it flies
    /// to you, its line shows, and it is kept as the campaign flag "postcard:&lt;country&gt;:&lt;n&gt;" (saved with the
    /// next checkpoint). Placed, uncollected postcards are listed in <see cref="All"/> for the maps.
    /// </summary>
    public class Postcard : Interactable
    {
        public const int PerCountry = 3;

        /// <summary>Every postcard placed in a built cell (collected ones included; check <see cref="Collected"/>).</summary>
        public static readonly List<Postcard> All = new List<Postcard>();

        public CountryId Country;
        public int Number;
        public string Line;

        Transform m_Card;
        Transform m_Glow;
        Material m_GlowMat;
        ParticleSystem m_Motes;
        float m_Phase;
        bool m_Flying;

        public static string FlagFor(CountryId country, int number) => "postcard:" + CountryDatabase.Get(country).StableId + ":" + number;

        public bool Collected => Game.Campaign != null && Game.Campaign.HasFlag(FlagFor(Country, Number));

        public static int CollectedIn(CountryId country)
        {
            int n = 0;
            if (Game.Campaign == null) return 0;
            for (int i = 1; i <= PerCountry; i++) if (Game.Campaign.HasFlag(FlagFor(country, i))) n++;
            return n;
        }

        public static int CollectedTotal()
        {
            int n = 0;
            for (int c = 0; c < CountryDatabase.CountryCount; c++) n += CollectedIn((CountryId)c);
            return n;
        }

        /// <summary>
        /// Places postcard <paramref name="number"/> (1..3) of <paramref name="country"/> at a local position (the card's
        /// centre, so about 1.1 m above the floor to hang at chest height). <paramref name="line"/> is what the card says.
        /// </summary>
        public static Postcard Create(Transform parent, CountryId country, int number, Vector3 localPos, string line)
        {
            var go = new GameObject("Postcard_" + country + "_" + number);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var p = go.AddComponent<Postcard>();
            p.Country = country;
            p.Number = Mathf.Clamp(number, 1, PerCountry);
            p.Line = line;
            p.Build();
            return p;
        }

        void Build()
        {
            Prompt = "Take the postcard";
            MaxDistance = 2.6f;
            m_Phase = Random.value * 10f;
            var def = CountryDatabase.Get(Country);

            m_Card = new GameObject("Card").transform;
            m_Card.SetParent(transform, false);
            // Card stock with a printed stamp on the front and a written back, both faces lit softly so they read at night.
            var front = Mats.UnlitInstance(new Color(0.93f, 0.89f, 0.8f, 1f), null, false, 1.05f);
            var back = Mats.UnlitInstance(new Color(0.86f, 0.82f, 0.74f, 1f), null, false, 0.95f);
            MeshFactory.Quad("Front", m_Card, 0.17f, 0.115f, front, new Vector3(0f, 0f, -0.0015f), Quaternion.identity);
            MeshFactory.Quad("Back", m_Card, 0.17f, 0.115f, back, new Vector3(0f, 0f, 0.0015f), Quaternion.Euler(0f, 180f, 0f));
            var art = Tex.Art(def.StampTexture);
            if (art != null)
            {
                var stamp = Mats.UnlitInstance(new Color(1f, 1f, 1f, 0.95f), art, true, 1f);
                MeshFactory.Quad("Stamp", m_Card, 0.075f, 0.075f, stamp, new Vector3(0.035f, 0.008f, -0.003f), Quaternion.identity);
            }
            var place = TextFactory.World(m_Card, def.Place, 0.0085f, new Color(0.22f, 0.17f, 0.12f), TextAlignmentOptions.Left, true, 0.09f);
            place.transform.localPosition = new Vector3(-0.035f, -0.035f, -0.003f);
            var greeting = TextFactory.World(m_Card, "Greetings", 0.011f, def.Tint * 0.8f, TextAlignmentOptions.Left, true, 0.09f);
            greeting.transform.localPosition = new Vector3(-0.035f, 0.03f, -0.003f);
            var note = TextFactory.World(m_Card, Line, 0.0055f, new Color(0.18f, 0.16f, 0.2f), TextAlignmentOptions.TopLeft, true, 0.15f);
            note.transform.localPosition = new Vector3(0f, 0f, 0.003f);
            note.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // A faint warm glow behind it and a few motes drifting off: findable, never a beacon.
            m_GlowMat = Mats.UnlitInstance(new Color(1f, 0.85f, 0.6f, 0.35f), Tex.SoftDot, true, 1.4f);
            m_GlowMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m_GlowMat.SetFloat("_ZWrite", 0f);
            m_Glow = MeshFactory.Quad("Glow", transform, 0.55f, 0.55f, m_GlowMat, Vector3.zero, Quaternion.identity).transform;
            m_Motes = MakeMotes();

            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = gameObject.AddComponent<SphereCollider>();
            col.radius = 0.22f;
            col.isTrigger = true;
            ApplyCollected(); // a loaded save may already hold it
        }

        ParticleSystem MakeMotes()
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.026f);
            main.startColor = new Color(1f, 0.86f, 0.6f, 0.9f);
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 3.5f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.12f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, true, Color.white, 1.5f, Mats.StencilNotEqual);
            ps.Play();
            return ps;
        }

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            ApplyCollected();
        }

        void OnDisable() => All.Remove(this);

        void ApplyCollected()
        {
            bool taken = Collected;
            Usable = !taken;
            if (m_Card != null && !m_Flying)
            {
                m_Card.gameObject.SetActive(!taken);
                m_Glow.gameObject.SetActive(!taken);
                m_Motes.gameObject.SetActive(!taken);
            }
        }

        public override bool CanUse(Interactor who) => base.CanUse(who) && !Collected && !m_Flying;

        public override void Use(Interactor who)
        {
            if (!CanUse(who)) return;
            Game.Campaign.SetFlag(FlagFor(Country, Number));
            Usable = false;
            int n = CollectedIn(Country);
            AudioDirector.I?.Play("pp_page", transform.position, 0.9f, 1.05f);
            AudioDirector.I?.Play("pp_seam_shimmer", transform.position, 0.45f, 1.3f);
            Hud.I?.ShowToast("Postcard " + n + " of " + PerCountry + ", " + CountryDatabase.Get(Country).Place + ": “" + Line + "”", 5f);
            base.Use(who);
            StartCoroutine(FlyToPlayer());
        }

        System.Collections.IEnumerator FlyToPlayer()
        {
            m_Flying = true;
            m_Motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            var start = m_Card.position;
            var startRot = m_Card.rotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.5f;
                var rig = Game.Rig;
                var target = rig != null ? rig.Head.position + rig.Head.forward * 0.25f - Vector3.up * 0.35f : start;
                float k = Mathf.SmoothStep(0f, 1f, t);
                m_Card.position = Vector3.Lerp(start, target, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.12f;
                if (rig != null) m_Card.rotation = Quaternion.Slerp(startRot, Quaternion.LookRotation(rig.Head.forward, Vector3.up), k);
                m_Card.localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, k);
                m_GlowMat.SetColor("_Color", new Color(1f, 0.85f, 0.6f, 0.35f * (1f - k)));
                yield return null;
            }
            m_Card.gameObject.SetActive(false);
            m_Glow.gameObject.SetActive(false);
            m_Flying = false;
        }

        void Update()
        {
            if (m_Card == null || m_Flying || !m_Card.gameObject.activeSelf) return;
            float t = Time.time + m_Phase;
            m_Card.localPosition = new Vector3(0f, Mathf.Sin(t * 1.3f) * 0.03f, 0f);
            m_Card.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f) * 8f, t * 40f % 360f, Mathf.Sin(t * 0.9f) * 5f);
            var cam = Game.Rig != null ? Game.Rig.Cam : null;
            if (cam != null) m_Glow.rotation = Quaternion.LookRotation(m_Glow.position - cam.transform.position, Vector3.up);
            float pulse = 0.28f + 0.12f * Mathf.Sin(t * 2.1f);
            m_GlowMat.SetColor("_Color", new Color(1f, 0.85f, 0.6f, pulse));
        }
    }
}
