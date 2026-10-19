using System;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// A genuine arrival trace: hidden until the local puzzle reveals it, then the player presses their passport to it
    /// to verify the country's seal. Can be blocked (e.g. the Passenger standing at the desk).
    /// </summary>
    public class TraceMark : Interactable
    {
        public CountryId Country;
        public bool Revealed { get; private set; }
        public Func<string> BlockedReason;   // non-null result = cannot verify yet (shown as prompt)
        public event Action<TraceMark> Verified;

        Material m_Mat;
        GameObject m_Visual;
        float m_Glow;

        public static TraceMark Create(Transform parent, CountryId country, Vector3 localPos, Quaternion localRot, float size = 0.32f, bool onFloor = false)
        {
            var go = new GameObject("Trace_" + country);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var t = go.AddComponent<TraceMark>();
            t.Country = country;
            t.Build(size, onFloor);
            return t;
        }

        void Build(float size, bool onFloor)
        {
            Prompt = "Press your passport to the arrival trace";
            MaxDistance = 2.4f;
            var tex = Tex.Art(CountryDatabase.Get(Country).StampTexture);
            m_Mat = Mats.UnlitInstance(new Color(0.55f, 0.95f, 0.85f, 0f), tex != null ? tex : Tex.Ring, true, 1.6f);
            m_Mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m_Mat.SetFloat("_Cull", 0f);
            var rot = onFloor ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
            m_Visual = MeshFactory.Quad("TraceVisual", transform, size, size, m_Mat, Vector3.zero, rot);
            m_Visual.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = onFloor ? new Vector3(size, 0.3f, size) : new Vector3(size, size, 0.25f);
            Usable = false;
        }

        public void Reveal(bool withSound = true)
        {
            if (Revealed) return;
            Revealed = true;
            Usable = true;
            if (withSound) AudioDirector.I?.Play("pp_seam_shimmer", transform.position, 0.8f);
        }

        public void Hide()
        {
            Revealed = false;
            Usable = false;
        }

        public override bool CanUse(Interactor who)
        {
            if (!base.CanUse(who) || !Revealed) return false;
            return !Game.Campaign.IsVerified(Country);
        }

        public override string GetPrompt(Interactor who)
        {
            var reason = BlockedReason?.Invoke();
            return reason ?? Prompt;
        }

        public override void Use(Interactor who)
        {
            if (BlockedReason?.Invoke() != null)
            {
                AudioDirector.I?.Play("ui_back", transform.position, 0.6f);
                return;
            }
            Usable = false;
            Verified?.Invoke(this);
            Game.Story?.VerifyCountry(Country, this);
        }

        void Update()
        {
            bool verified = Game.Campaign != null && Game.Campaign.IsVerified(Country);
            float target = !Revealed ? 0f : verified ? 0.35f : 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
            m_Glow = Mathf.MoveTowards(m_Glow, target, Time.deltaTime * 1.5f);
            var c = verified ? new Color(0.85f, 0.75f, 0.55f) : new Color(0.55f, 0.95f, 0.85f);
            m_Mat.SetColor("_Color", new Color(c.r, c.g, c.b, m_Glow));
            m_Visual.SetActive(m_Glow > 0.01f);
        }
    }
}
