using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// The fictional Festival of Passports booklet: the seal page (eleven seals + return field), the destination
    /// list, the portrait that shows vitality, and the gutter seam the player pulls to tear the world.
    /// Desktop: F raises it into view. VR: summoned into the passport hand (X), docked with X again.
    /// </summary>
    public class PassportController : MonoBehaviour
    {
        public static PassportController I { get; private set; }

        public bool IsOpen { get; private set; }
        /// <summary>True while the booklet is on screen: open, or still being lowered after Close.</summary>
        public bool InView => IsOpen || m_Show > 0f;
        public bool Available = true;
        public CountryId? Selected { get; private set; }
        public CountryDef SelectedDef => Selected.HasValue ? CountryDatabase.Get(Selected.Value) : null;

        Transform m_Book;
        Transform m_SeamHandle;
        Material m_SeamMat;
        readonly Dictionary<CountryId, MeshRenderer> m_Seals = new Dictionary<CountryId, MeshRenderer>();
        // Every country was really stamped on the trip; in the Margin a seal only becomes inked once its arrival trace is verified.
        readonly Dictionary<CountryId, Material> m_SealInked = new Dictionary<CountryId, Material>();
        readonly Dictionary<CountryId, Material> m_SealFaint = new Dictionary<CountryId, Material>();
        readonly Dictionary<CountryId, TextMeshPro> m_Rows = new Dictionary<CountryId, TextMeshPro>();
        MeshRenderer m_Counterfeit, m_VoidMark;
        Material m_PortraitMat;
        Texture2D m_PortraitNormal, m_PortraitDrained;
        TextMeshPro m_SelectedLabel;
        HandSide m_HeldBy = HandSide.Desktop;
        Interactor m_Puller;
        float m_Show;            // 0..1 raise animation
        Coroutine m_Anim;

        // Layout (metres, page space). The booklet is slightly larger than a real passport for VR legibility.
        const float PageW = 0.105f, PageH = 0.148f;

        public static PassportController Create(Transform parent)
        {
            var go = new GameObject("Passport");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<PassportController>();
            I.Build();
            return I;
        }

        void Build()
        {
            m_Book = new GameObject("Book").transform;
            m_Book.SetParent(transform, false);

            var leather = Mats.Lit(new Color(0.1f, 0.13f, 0.24f), 0.35f, 0f, "leather_navy");
            var paper = PageGlow(Mats.Lit(new Color(0.95f, 0.92f, 0.84f), 0.15f, 0f, "paper_ivory"), 0.12f);
            // Covers (open, lying behind the pages).
            MeshFactory.Box("CoverL", m_Book, new Vector3(-PageW / 2 - 0.001f, 0, 0.0035f), new Vector3(PageW + 0.006f, PageH + 0.006f, 0.003f), leather, false);
            MeshFactory.Box("CoverR", m_Book, new Vector3(PageW / 2 + 0.001f, 0, 0.0035f), new Vector3(PageW + 0.006f, PageH + 0.006f, 0.003f), leather, false);
            MeshFactory.Box("PagesL", m_Book, new Vector3(-PageW / 2, 0, 0.0008f), new Vector3(PageW, PageH, 0.003f), paper, false);
            MeshFactory.Box("PagesR", m_Book, new Vector3(PageW / 2, 0, 0.0008f), new Vector3(PageW, PageH, 0.003f), paper, false);

            // Left page: insert with eleven seals + return field.
            var insertTex = Tex.Art("passport_insert_page");
            var left = MeshFactory.Quad("InsertPage", m_Book, PageW * 0.97f, PageH * 0.97f, PageGlow(Mats.Printed(insertTex, Color.white, 0.12f), 0.16f), new Vector3(-PageW / 2, 0, -0.0008f));
            BuildSeals(left.transform);

            // Right page: ID page with portrait and destination list.
            var idTex = Tex.Art("passport_id_page");
            var right = MeshFactory.Quad("IdPage", m_Book, PageW * 0.97f, PageH * 0.97f, PageGlow(Mats.Printed(idTex, Color.white, 0.12f), 0.16f), new Vector3(PageW / 2, 0, -0.0008f));
            m_PortraitNormal = Tex.Art("saeed_portrait");
            m_PortraitDrained = Tex.Art("saeed_portrait_drained");
            m_PortraitMat = new Material(Mats.Template("Lit_Emissive"));
            m_PortraitMat.SetTexture("_BaseMap", m_PortraitNormal != null ? m_PortraitNormal : Tex.White);
            m_PortraitMat.SetColor("_BaseColor", m_PortraitNormal != null ? Color.white : new Color(0.7f, 0.68f, 0.62f));
            m_PortraitMat.SetFloat("_Smoothness", 0.2f);
            m_PortraitMat.SetTexture("_EmissionMap", m_PortraitNormal != null ? m_PortraitNormal : Tex.White);
            m_PortraitMat.SetColor("_EmissionColor", new Color(0.14f, 0.14f, 0.14f));
            MeshFactory.Quad("Portrait", right.transform, 0.03f, 0.0375f, m_PortraitMat, new Vector3(-0.026f, 0.046f, -0.0005f));
            var name = TextFactory.World(right.transform, "SAEED AL MANSOORI", 0.0042f, new Color(0.15f, 0.14f, 0.16f), TextAlignmentOptions.Left, false, 0.06f);
            name.transform.localPosition = new Vector3(0.018f, 0.056f, -0.0006f);
            name.fontStyle = FontStyles.Bold;
            var sub = TextFactory.World(right.transform, "Festival of Passports\ncommemorative insert\n<size=80%>Not an identity document</size>", 0.0031f, new Color(0.3f, 0.28f, 0.3f), TextAlignmentOptions.Left, false, 0.06f);
            sub.transform.localPosition = new Vector3(0.018f, 0.04f, -0.0006f);
            BuildRows(right.transform);

            // Sans: Inter has the arrows and the tick. On desktop the HUD card shows the destination instead.
            m_SelectedLabel = TextFactory.World(m_Book, "", 0.0085f, new Color(0.95f, 0.92f, 0.85f), TextAlignmentOptions.Center, false, 0.24f);
            m_SelectedLabel.gameObject.SetActive(Game.Mode == GameMode.VR);
            m_SelectedLabel.transform.localPosition = new Vector3(0, PageH / 2 + 0.014f, -0.002f);
            m_SelectedLabel.outlineWidth = 0.2f;
            m_SelectedLabel.outlineColor = new Color32(0, 0, 0, 220);

            // Gutter seam + pull handle.
            m_SeamMat = Mats.UnlitInstance(new Color(0.5f, 0.95f, 0.85f, 0.35f), Tex.Fibre, true, 1.6f);
            m_SeamMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            var seam = new MeshBuilder();
            for (int i = 0; i < 14; i++)
            {
                float y = -PageH / 2 + 0.008f + i * (PageH - 0.016f) / 13f;
                seam.AddQuad(new Vector3(0.0035f, y - 0.0012f, 0), new Vector3(-0.0035f, y - 0.0012f, 0), new Vector3(-0.0035f, y + 0.0012f, 0), new Vector3(0.0035f, y + 0.0012f, 0),
                    new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1));
            }
            MeshFactory.Make("Seam", seam.ToMesh("Seam"), m_SeamMat, m_Book, new Vector3(0, 0, -0.0015f));
            m_SeamHandle = MeshFactory.Sphere("SeamHandle", m_Book, new Vector3(0, PageH / 2 - 0.004f, -0.006f), 0.0065f, Mats.Emissive(new Color(0.6f, 0.95f, 0.85f), new Color(0.6f, 1.6f, 1.3f))).transform;

            MeshFactory.NoShadows(m_Book.gameObject);
            m_Book.gameObject.SetActive(false);
            Refresh();
        }

        /// <summary>A copy of a page material that also glows faintly with its own texture (readable in the dark).</summary>
        static Material PageGlow(Material printed, float amount)
        {
            var m = new Material(Mats.Template("Lit_Emissive")) { name = printed.name + "_Glow" };
            var tex = printed.GetTexture("_BaseMap");
            var tint = printed.GetColor("_BaseColor");
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", printed.GetFloat("_Smoothness"));
            if (printed.HasProperty("_BumpMap") && printed.GetTexture("_BumpMap") != null) m.SetTexture("_BumpMap", printed.GetTexture("_BumpMap"));
            m.SetTexture("_EmissionMap", tex != null ? tex : Tex.White);
            m.SetColor("_EmissionColor", new Color(tint.r * amount, tint.g * amount, tint.b * amount));
            return m;
        }

        void BuildSeals(Transform page)
        {
            // Positions follow the generated insert layout (11 ovals + return field); fallback grid if missing.
            var layout = InsertLayout.Load();
            var stampMatCache = new Dictionary<string, Material>();
            foreach (var def in CountryDatabase.All)
            {
                var uv = layout.Seal((int)def.Id);
                var pos = new Vector3((uv.x - 0.5f) * PageW * 0.97f, (uv.y - 0.5f) * PageH * 0.97f, -0.0004f);
                var tex = Tex.Art(def.StampTexture);
                var mat = Mats.Printed(tex, Color.white, 0.1f, true);
                float size = layout.SealSize * PageW * 0.97f;
                var q = MeshFactory.Quad("Seal_" + def.StableId, page, size, size, mat, pos);
                m_Seals[def.Id] = q.GetComponent<MeshRenderer>();
                m_SealInked[def.Id] = mat;
                m_SealFaint[def.Id] = Mats.Printed(tex, new Color(0.7f, 0.66f, 0.62f), 0.1f, true);
            }
            var rf = layout.ReturnField;
            var rpos = new Vector3((rf.center.x - 0.5f) * PageW * 0.97f, (rf.center.y - 0.5f) * PageH * 0.97f, -0.0004f);
            var forged = Tex.Art(CountryDatabase.CounterfeitStampTexture);
            m_Counterfeit = MeshFactory.Quad("Counterfeit", page, rf.height * PageH * 0.97f * 0.92f, rf.height * PageH * 0.97f * 0.92f, Mats.Printed(forged, Color.white, 0.1f, true), rpos).GetComponent<MeshRenderer>();
            var label = TextFactory.World(m_Counterfeit.transform, CountryDatabase.CounterfeitLabel, 0.0028f, new Color(0.55f, 0.08f, 0.08f), TextAlignmentOptions.Center, false, 0.08f);
            label.transform.localPosition = new Vector3(0, -0.016f, -0.0003f);
            m_VoidMark = MeshFactory.Quad("Void", m_Counterfeit.transform, 0.05f, 0.022f, Mats.Unlit(new Color(0.45f, 0.06f, 0.06f, 0.85f), null, true), new Vector3(0, 0, -0.0004f), Quaternion.Euler(0, 0, -14f)).GetComponent<MeshRenderer>();
            var voidText = TextFactory.World(m_VoidMark.transform, "VOID", 0.008f, new Color(0.95f, 0.88f, 0.82f), TextAlignmentOptions.Center, true, 0.05f);
            voidText.transform.localPosition = new Vector3(0, 0, -0.0002f);
        }

        void BuildRows(Transform page)
        {
            int i = 0;
            foreach (var def in CountryDatabase.InRecommendedOrder)
            {
                var t = TextFactory.World(page, def.DisplayName, 0.0043f, new Color(0.2f, 0.18f, 0.2f), TextAlignmentOptions.Left, false, 0.085f);
                t.transform.localPosition = new Vector3(0.0f, 0.018f - i * 0.0083f, -0.0006f);
                t.rectTransform.sizeDelta = new Vector2(0.085f, 0.008f);
                m_Rows[def.Id] = t;
                // Pointable in VR (and clickable in desktop pointer mode) to select a destination directly.
                var col = t.gameObject.AddComponent<BoxCollider>();
                col.size = new Vector3(0.085f, 0.0075f, 0.004f);
                col.isTrigger = true;
                var it = t.gameObject.AddComponent<Interactable>();
                it.Prompt = "Choose " + def.DisplayName;
                it.MaxDistance = 1f;
                var id = def.Id;
                it.OnUse = _ => Select(id, true);
                i++;
            }
        }

        // ------------------------------------------------------------------ open / close
        public bool IsHeldBy(HandSide side) => IsOpen && Game.Mode == GameMode.VR && m_HeldBy == side;

        public void Toggle(HandSide hand)
        {
            if (IsOpen) Close(); else Open(hand);
        }

        public void Open(HandSide hand)
        {
            if (!Available) { Hud.I?.ShowToast("You don't have your passport out yet.", 2f); return; }
            if (IsOpen) return;
            IsOpen = true;
            m_HeldBy = Game.Mode == GameMode.VR ? Game.Input.PassportHand : HandSide.Desktop;
            if (Game.Mode == GameMode.VR)
            {
                // Release anything in the passport hand first (never require three hands).
                foreach (var h in Game.Rig.Hands) if (h.Side == m_HeldBy) h.ForceRelease(false);
            }
            if (!Selected.HasValue || !Game.Campaign.IsDestinationAvailable(Selected.Value)) SelectFirstAvailable();
            Refresh();
            m_Book.gameObject.SetActive(true);
            if (m_Anim != null) StopCoroutine(m_Anim);
            m_Anim = StartCoroutine(Animate(1f));
            AudioDirector.I?.Play("pp_open", Game.Rig.PassportMount.position, 0.7f);
            AudioDirector.Haptic(m_HeldBy, 0.3f, 0.05f);
            Game.Story?.OnPassportOpened();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (m_Puller != null) EndSeamPull(m_Puller);
            if (m_Anim != null) StopCoroutine(m_Anim);
            m_Anim = StartCoroutine(Animate(0f));
            AudioDirector.I?.Play("pp_close", Game.Rig.PassportMount.position, 0.6f);
        }

        IEnumerator Animate(float target)
        {
            while (!Mathf.Approximately(m_Show, target))
            {
                m_Show = Mathf.MoveTowards(m_Show, target, Time.unscaledDeltaTime * 4.5f);
                yield return null;
            }
            if (target <= 0f) m_Book.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            var rig = Game.Rig;
            if (rig == null || m_Book == null) return;
            var mount = rig.PassportMount;
            transform.position = mount.position;
            transform.rotation = mount.rotation;
            float e = Mathf.SmoothStep(0f, 1f, m_Show);
            m_Book.localPosition = Vector3.down * (1f - e) * (Game.Mode == GameMode.Desktop ? 0.25f : 0.05f);
            m_Book.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, e);
            m_Book.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(20f, 0f, e));

            // Seam glows when a world seam is targeted.
            bool targeted = Game.Tears != null && Game.Tears.HasTarget;
            if (m_SeamMat != null)
            {
                float a = targeted ? 0.9f + 0.1f * Mathf.Sin(Time.time * 8f) : 0.35f;
                m_SeamMat.SetColor("_Color", new Color(0.5f, 0.95f, 0.85f, a));
            }

            // Vitality portrait.
            if (m_PortraitMat != null && m_PortraitDrained != null && Game.Campaign != null)
            {
                bool drained = Game.Campaign.vitality < 0.55f;
                var want = drained ? m_PortraitDrained : m_PortraitNormal;
                if (m_PortraitMat.GetTexture("_BaseMap") != want) m_PortraitMat.SetTexture("_BaseMap", want);
                m_PortraitMat.SetColor("_BaseColor", Color.Lerp(new Color(0.65f, 0.7f, 0.75f), Color.white, Mathf.InverseLerp(0.2f, 1f, Game.Campaign.vitality)));
            }

            // VR seam pull update.
            if (m_Puller != null && IsOpen)
            {
                float d = Vector3.Distance(m_Puller.HoldPoint.position, m_SeamHandle.position);
                Game.Tears?.SetPull(d, m_Puller.Side);
            }
        }

        // ------------------------------------------------------------------ destinations
        public List<CountryDef> Destinations => CountryDatabase.InRecommendedOrder.ToList();

        void SelectFirstAvailable()
        {
            var next = Game.Campaign.NextRecommended();
            var current = Game.Cells != null && Game.Cells.Active != null ? CountryDatabase.ByCell(Game.Cells.Active.Id) : null;
            foreach (var d in Destinations)
            {
                if (!Game.Campaign.IsDestinationAvailable(d.Id) || (current != null && current.Id == d.Id)) continue;
                if (next != null && d.RecommendedOrder < next.RecommendedOrder && Game.Campaign.IsVerified(d.Id) && d.Id != CountryId.Egypt) continue;
                Selected = d.Id;
                return;
            }
            Selected = null;
        }

        public void CycleDestination(int dir)
        {
            var list = Destinations;
            int idx = Selected.HasValue ? list.FindIndex(d => d.Id == Selected.Value) : -1;
            for (int k = 0; k < list.Count; k++)
            {
                idx = (idx + dir + list.Count) % list.Count;
                if (Game.Campaign.IsDestinationAvailable(list[idx].Id)) { Select(list[idx].Id, true); return; }
            }
        }

        public void Select(CountryId id, bool feedback)
        {
            if (!Game.Campaign.IsDestinationAvailable(id))
            {
                Hud.I?.ShowToast(CountryDatabase.Get(id).DisplayName + " is not available yet.", 1.8f);
                return;
            }
            Selected = id;
            Refresh();
            if (feedback)
            {
                AudioDirector.I?.Play("pp_tab", Game.Rig.PassportMount.position, 0.6f);
                AudioDirector.Haptic(m_HeldBy, 0.15f, 0.03f);
            }
            Game.Story?.OnDestinationSelected(id);
        }

        /// <summary>Carries the selection over to a rebuilt passport (the rig switched modes) without the story hook.</summary>
        public void RestoreSelection(CountryId? id)
        {
            Selected = id;
            Refresh();
        }

        public void Refresh()
        {
            var c = Game.Campaign;
            if (c == null) return;
            var current = Game.Cells != null && Game.Cells.Active != null ? CountryDatabase.ByCell(Game.Cells.Active.Id) : null;
            foreach (var def in CountryDatabase.All)
            {
                if (m_Seals.TryGetValue(def.Id, out var r))
                {
                    bool inked = c.IsVerified(def.Id) || !c.HasFlag(StoryFlags.GlassesWorn);
                    r.enabled = true;
                    r.sharedMaterial = inked ? m_SealInked[def.Id] : m_SealFaint[def.Id];
                }
                if (!m_Rows.TryGetValue(def.Id, out var row)) continue;
                bool avail = c.IsDestinationAvailable(def.Id);
                string mark = c.IsVerified(def.Id) ? "✓ " : c.IsVisited(def.Id) ? "• " : "   ";
                string here = current != null && current.Id == def.Id ? "  <size=75%>(here)</size>" : "";
                string res = def.Id == CountryId.Egypt && c.HasResource(ResourceId.Sunlight) ? "  <size=75%>☀</size>" : "";
                bool sel = Selected.HasValue && Selected.Value == def.Id;
                row.text = mark + def.DisplayName + res + here;
                row.color = !avail ? new Color(0.55f, 0.53f, 0.52f, 0.6f) : sel ? new Color(0.62f, 0.12f, 0.1f) : new Color(0.2f, 0.18f, 0.2f);
                row.fontStyle = sel ? FontStyles.Bold | FontStyles.Underline : FontStyles.Normal;
            }
            bool forged = c.HasFlag(StoryFlags.CounterfeitRevealed);
            if (m_Counterfeit != null) m_Counterfeit.gameObject.SetActive(forged);
            if (m_VoidMark != null) m_VoidMark.gameObject.SetActive(c.HasFlag(StoryFlags.ForgedCancelled));
            var sd = SelectedDef;
            if (m_SelectedLabel != null)
                m_SelectedLabel.text = sd == null ? "" : "◀  " + sd.DisplayName + "  ▶" + (c.IsVerified(sd.Id) ? "\n<size=60%><color=#9FD9C6>✓ verified</color></size>" : "");
        }

        /// <summary>Ink animation when a seal is verified.</summary>
        public IEnumerator PlayVerify(CountryId id)
        {
            Refresh();
            if (!m_Seals.TryGetValue(id, out var r)) yield break;
            bool wasOpen = IsOpen;
            if (!IsOpen) Open(Game.Mode == GameMode.VR ? Game.Input.PassportHand : HandSide.Desktop);
            r.enabled = true;
            r.sharedMaterial = m_SealInked[id];
            var t = r.transform;
            var baseScale = t.localScale;
            float k = 0f;
            AudioDirector.I?.Play("stamp_hit", Game.Rig.PassportMount.position, 1f);
            while (k < 1f)
            {
                k += Time.deltaTime / 0.45f;
                float s = k < 0.3f ? Mathf.Lerp(1.4f, 0.92f, k / 0.3f) : Mathf.Lerp(0.92f, 1f, (k - 0.3f) / 0.7f);
                t.localScale = baseScale * s;
                yield return null;
            }
            t.localScale = baseScale;
            AudioDirector.I?.Play("seal_verify", Game.Rig.PassportMount.position, 0.9f);
            yield return new WaitForSeconds(1.2f);
            if (!wasOpen) Close();
        }

        public IEnumerator PlayCounterfeitReveal()
        {
            Game.Campaign.SetFlag(StoryFlags.CounterfeitRevealed);
            if (!IsOpen) Open(Game.Mode == GameMode.VR ? Game.Input.PassportHand : HandSide.Desktop);
            yield return new WaitForSeconds(0.6f);
            Refresh();
            var t = m_Counterfeit.transform;
            var baseScale = t.localScale;
            float k = 0f;
            AudioDirector.I?.Play("seal_counterfeit", Game.Rig.PassportMount.position, 1f, 1f, Bus.SFX, true);
            while (k < 1f)
            {
                k += Time.deltaTime / 1.6f;
                t.localScale = baseScale * Mathf.Lerp(0.2f, 1f, Mathf.SmoothStep(0, 1, k));
                yield return null;
            }
            t.localScale = baseScale;
            Hud.I?.ShowCaption("a twelfth stamp inks itself: HOME — ENTRY GRANTED", 4f);
        }

        // ------------------------------------------------------------------ VR seam pull
        public bool TryBeginSeamPull(Interactor hand)
        {
            if (!IsOpen || Game.Mode != GameMode.VR || hand.Side == m_HeldBy || m_SeamHandle == null) return false;
            if (Vector3.Distance(hand.HoldPoint.position, m_SeamHandle.position) > 0.09f) return false;
            if (Game.Tears == null || (!Game.Tears.HasTarget && Game.Tears.Active == null)) { Hud.I?.ShowToast("Aim at a stitched seam first", 1.5f); return false; }
            m_Puller = hand;
            AudioDirector.I?.Play("pp_page", m_SeamHandle.position, 0.6f);
            AudioDirector.Haptic(hand.Side, 0.4f, 0.05f);
            return true;
        }

        public bool IsPulling(Interactor hand) => m_Puller == hand;

        public void EndSeamPull(Interactor hand)
        {
            if (m_Puller != hand) return;
            m_Puller = null;
            Game.Tears?.EndPull();
        }
    }

    /// <summary>Seal positions on the insert page (from Art/passport_insert_layout.json, or a fallback grid).</summary>
    public class InsertLayout
    {
        [System.Serializable] class SealJ { public string id; public float[] center; public float[] radius; }
        [System.Serializable] class RectJ { public float xMin, yMin, width, height; }
        [System.Serializable] class Ret { public float[] center; public RectJ rect; }
        [System.Serializable] class File { public SealJ[] seals; public Ret return_field; }

        readonly Vector2[] m_Seals = new Vector2[11];
        public float SealSize = 0.17f;                       // fraction of page width
        public Rect ReturnField = new Rect(0.17f, 0.094f, 0.664f, 0.176f); // UV rect (origin bottom-left)

        public Vector2 Seal(int i) => m_Seals[Mathf.Clamp(i, 0, 10)];

        public static InsertLayout Load()
        {
            var l = new InsertLayout();
            int k = 0;
            int[] rows = { 4, 4, 3 };
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r]; c++)
                    l.m_Seals[k++] = new Vector2((c + 0.5f) / rows[r] * 0.86f + 0.07f, 0.79f - r * 0.19f);
            var ta = Resources.Load<TextAsset>("Art/passport_insert_layout");
            if (ta == null) return l;
            try
            {
                var f = JsonUtility.FromJson<File>(ta.text);
                if (f.seals != null && f.seals.Length >= 11)
                {
                    for (int i = 0; i < 11; i++)
                        if (f.seals[i].center != null && f.seals[i].center.Length >= 2)
                            l.m_Seals[i] = new Vector2(f.seals[i].center[0], f.seals[i].center[1]);
                    if (f.seals[0].radius != null && f.seals[0].radius.Length >= 1)
                        l.SealSize = Mathf.Clamp(f.seals[0].radius[0] * 2f * 0.95f, 0.1f, 0.25f);
                }
                if (f.return_field != null && f.return_field.rect != null && f.return_field.rect.width > 0f)
                    l.ReturnField = new Rect(f.return_field.rect.xMin, f.return_field.rect.yMin, f.return_field.rect.width, f.return_field.rect.height);
            }
            catch (System.Exception e) { Debug.LogWarning("[TS-Passport] layout: " + e.Message); }
            return l;
        }
    }
}
