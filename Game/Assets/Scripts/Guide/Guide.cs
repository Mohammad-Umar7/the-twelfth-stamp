using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TwelfthStamp
{
    /// <summary>One step of a place's walkthrough.</summary>
    public class GuideStep
    {
        public string Text;                        // what to do now (rich text, key tags from Hud.KeyTag)
        public string Hint;                        // shown when the player has been on this step for a while
        public System.Func<bool> Done;             // the step is complete
        public System.Func<Vector3?> Target;       // where the marker goes (null: no marker)
        public System.Func<float> Progress;        // 0..1 for a meter (null or < 0: none)
        public string Label = "";                  // the marker's name ("Reflector", "Trace")
        public System.Func<string> LabelNow;       // when the name changes with the target
    }

    /// <summary>A place's walkthrough: the briefing shown on arrival, the steps, the danger.</summary>
    public class PlaceGuide
    {
        public string Title;                       // "Egypt · The Sun Without a Shadow"
        public string Briefing;                    // one or two sentences: the idea of the place
        public string Danger;                      // the threat here and what stops it
        public readonly List<GuideStep> Steps = new List<GuideStep>();
        public System.Func<string> Alert;          // live warning (the Passenger is close, the tear is unstable)
    }

    /// <summary>
    /// Tells the player what to do, everywhere. For the active place it finds the first unfinished step and shows it in a
    /// banner that is always on screen (with key prompts and a meter where a step has one), puts a marker on the step's
    /// target (on screen with its distance, and a soft beacon in the world, which VR uses), shows a short briefing the
    /// first time the player arrives somewhere, feeds the full checklist to the objectives panel (Tab), and after a while
    /// on one step adds a hint. Steps come from GuideBook. Built on the HUD's canvas; nothing here changes the game's state.
    /// </summary>
    public class Guide : MonoBehaviour
    {
        public static Guide I { get; private set; }

        /// <summary>Where the current step's marker is (the minimap shows it too).</summary>
        public Vector3? Target { get; private set; }
        /// <summary>The checklist (rich text) for the objectives panel and the full map.</summary>
        public string ChecklistText { get; private set; } = "";
        /// <summary>True while the game shows something the guide must not cover (menus, shifts).</summary>
        public bool Cinematic { get; private set; }
        /// <summary>The current step's index in the active place's guide (tests), -1 when there is none.</summary>
        public int StepIndex => m_Step;
        public PlaceGuide Current => m_Guide;
        public bool BriefingShowing => m_Brief != null && m_Brief.gameObject.activeSelf;
        public bool BannerShowing => m_Banner != null && m_Banner.gameObject.activeSelf;
        /// <summary>Desktop, while the screen marker shows: the space it takes (icon, arrow and name), in screen pixels.</summary>
        public Rect? MarkerRect { get; private set; }

        /// <summary>Closes the arrival briefing (the skip and use inputs do the same).</summary>
        public void DismissBriefing() => m_BriefingUntil = 0f;

        const float HintAfter = 40f;
        const float BriefingSeconds = 16f;

        Cell m_Cell;
        PlaceGuide m_Guide;
        int m_Step = -1;
        float m_StepSince, m_ObjectiveRefresh, m_BriefingUntil;
        string m_LastChecklist;
        readonly HashSet<CellId> m_Briefed = new HashSet<CellId>();

        RectTransform m_Root, m_Banner, m_Marker, m_Brief, m_Meter;
        CanvasGroup m_BannerFade;
        Image m_MeterFill, m_MarkerIcon, m_MarkerArrow;
        TextMeshProUGUI m_Header, m_Text, m_Hint, m_Alert, m_Footer, m_MarkerLabel, m_BriefText;
        Transform m_Beacon;
        Material m_BeaconMat;
        static Sprite s_Panel, s_Diamond, s_Chevron;
        static readonly Vector3[] s_Corners = new Vector3[4];
        readonly List<Rect> m_Avoid = new List<Rect>(4);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("Guide");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Guide>();
            go.AddComponent<MapView>();
        }

        void Awake() { I = this; }

        void LateUpdate()
        {
            if (Hud.I == null || Game.Cells == null) return;
            if (m_Root == null && !BuildUi()) return;

            var cell = Game.Cells.Active;
            if (cell != m_Cell)
            {
                m_Cell = cell;
                m_Guide = cell != null ? GuideBook.For(cell) : null;
                m_Step = -1;
                m_LastChecklist = null;
                m_BriefingUntil = 0f;   // a briefing never carries over to the next place
            }
            Cinematic = Game.InMenu || DimensionShift.Running || (Game.Story != null && Game.Story.Busy);
            bool live = m_Guide != null && !Cinematic && cell != null && (cell.Encounter == null || cell.Encounter.IsLive) && Game.Rig != null;
            if (!live)
            {
                Target = null;
                m_Banner.gameObject.SetActive(false);
                m_Brief.gameObject.SetActive(false);
                m_Marker.gameObject.SetActive(false);
                MarkerRect = null;
                if (m_Beacon != null) m_Beacon.gameObject.SetActive(false);
                return;
            }

            // The first unfinished step.
            int step = m_Guide.Steps.Count;
            for (int i = 0; i < m_Guide.Steps.Count; i++)
            {
                bool done;
                try { done = m_Guide.Steps[i].Done != null && m_Guide.Steps[i].Done(); }
                catch (System.Exception e) { Debug.LogWarning("[TS-Guide] step check failed: " + e.Message); done = false; }
                if (!done) { step = i; break; }
            }
            if (step != m_Step)
            {
                bool advanced = m_Step >= 0 && step > m_Step;
                m_Step = step;
                m_StepSince = Time.time;
                if (advanced) AudioDirector.I?.PlayUI("ui_confirm", 0.45f);
            }
            var s = step < m_Guide.Steps.Count ? m_Guide.Steps[step] : null;

            // Briefing the first time the player is here. In VR it waits for the title card: the small HUD there has no room
            // for both (on desktop the title card moves aside for it).
            if (!m_Briefed.Contains(cell.Id) && !(Game.Mode == GameMode.VR && Hud.I.TitleShowing))
            {
                m_Briefed.Add(cell.Id);
                m_BriefingUntil = Time.unscaledTime + BriefingSeconds;
                m_BriefText.text = BriefingText();
            }
            bool briefing = Time.unscaledTime < m_BriefingUntil;
            if (briefing && (DimensionShift.SkipPressed() || (Game.Input != null && Game.Input.Interact != null && Game.Input.Interact.WasPressedThisFrame())))
            {
                m_BriefingUntil = 0f;
                briefing = false;
            }
            m_Brief.gameObject.SetActive(briefing && (MapView.I == null || !MapView.I.FullOpen));

            // Banner (not while a briefing is still to come).
            m_Banner.gameObject.SetActive(s != null && !briefing && m_Briefed.Contains(cell.Id) && (MapView.I == null || !MapView.I.FullOpen));
            PlaceBanner();
            m_BannerFade.alpha = Mathf.MoveTowards(m_BannerFade.alpha, 1f, Time.unscaledDeltaTime * 6f);
            Vector3? target = null;
            if (s != null)
            {
                string place = cell.Country != null ? cell.Country.DisplayName.ToUpperInvariant() : PlaceName(cell);
                m_Header.text = place + "  ·  STEP " + (step + 1) + " OF " + m_Guide.Steps.Count;
                m_Text.text = s.Text;
                float p = s.Progress != null ? s.Progress() : -1f;
                m_Meter.gameObject.SetActive(p >= 0f);
                if (p >= 0f) m_MeterFill.fillAmount = Mathf.Clamp01(p);
                bool stuck = Time.time - m_StepSince > HintAfter && !string.IsNullOrEmpty(s.Hint);
                m_Hint.gameObject.SetActive(stuck);
                if (stuck) m_Hint.text = "<b>Hint</b>  " + s.Hint;
                string alert = m_Guide.Alert != null ? m_Guide.Alert() : null;
                m_Alert.gameObject.SetActive(!string.IsNullOrEmpty(alert));
                if (!string.IsNullOrEmpty(alert)) m_Alert.text = alert;
                Layout();
                try { target = s.Target != null ? s.Target() : null; } catch { target = null; }
            }
            Target = target;
            UpdateMarker(s, briefing ? null : target);

            // Objectives panel (Tab) and the full map show the whole list.
            ChecklistText = Checklist(step);
            if (ChecklistText != m_LastChecklist || Time.unscaledTime > m_ObjectiveRefresh)
            {
                bool flash = m_LastChecklist != null && ChecklistText != m_LastChecklist;
                m_LastChecklist = ChecklistText;
                m_ObjectiveRefresh = Time.unscaledTime + 1.5f;
                Hud.I.SetObjective(ChecklistText, false);
            }
        }

        static string PlaceName(Cell c)
        {
            switch (c.Id)
            {
                case CellId.ApartmentNormal: return "HOME";
                case CellId.ApartmentCorrupt: return "THE MARGIN";
                case CellId.ShopNormal: case CellId.ShopCorrupt: return "MARRAKECH";
                default: return c.Id.ToString().ToUpperInvariant();
            }
        }

        string BriefingText()
        {
            var sb = new StringBuilder();
            sb.Append("<size=62%><color=#EDCF99><b>").Append(m_Guide.Title.ToUpperInvariant()).Append("</b></color></size>\n");
            if (!string.IsNullOrEmpty(m_Guide.Briefing)) sb.Append("<size=80%>").Append(m_Guide.Briefing).Append("</size>\n\n");
            sb.Append("<size=62%><color=#EDCF99><b>WHAT TO DO</b></color></size>\n<size=78%>");
            for (int i = 0; i < m_Guide.Steps.Count; i++)
                sb.Append(i + 1).Append(".  ").Append(m_Guide.Steps[i].Text).Append('\n');
            sb.Append("</size>");
            if (!string.IsNullOrEmpty(m_Guide.Danger))
                sb.Append("\n<size=62%><color=#ED6654><b>DANGER</b></color></size>\n<size=78%>").Append(m_Guide.Danger).Append("</size>\n");
            sb.Append("\n<size=60%><color=#8E8E8A>").Append(Hud.KeyTag(Intent.Skip)).Append("  got it     ")
              .Append(Game.Mode == GameMode.VR ? "" : Hud.KeyTag(Intent.Objectives) + "  steps     " + Hud.KeyTag(Intent.Map) + "  map")
              .Append("</color></size>");
            return sb.ToString();
        }

        string Checklist(int step)
        {
            if (m_Guide == null) return "";
            var sb = new StringBuilder();
            sb.Append("<b>").Append(m_Guide.Title).Append("</b>\n");
            for (int i = 0; i < m_Guide.Steps.Count; i++)
            {
                var t = m_Guide.Steps[i].Text;
                if (i < step) sb.Append("<color=#7E8C86>✓  ").Append(Strip(t)).Append("</color>\n");
                else if (i == step) sb.Append("<color=#8CF2D9>▶</color>  <b>").Append(t).Append("</b>\n");
                else sb.Append("<color=#9A9A96>○  ").Append(Strip(t)).Append("</color>\n");
            }
            if (!string.IsNullOrEmpty(m_Guide.Danger)) sb.Append("<size=88%><color=#ED6654>").Append(m_Guide.Danger).Append("</color></size>");
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>Done and upcoming steps without their coloured key tags.</summary>
        static string Strip(string s)
        {
            var sb = new StringBuilder();
            bool tag = false;
            foreach (char c in s)
            {
                if (c == '<') { tag = true; continue; }
                if (c == '>') { tag = false; continue; }
                if (!tag) sb.Append(c);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ marker
        void UpdateMarker(GuideStep s, Vector3? target)
        {
            bool vr = Game.Mode == GameMode.VR;
            var head = Game.Rig.Head;
            bool show = target.HasValue && (target.Value - head.position).magnitude > 1.6f;
            // World beacon (both modes): a soft pillar of light over the target.
            if (m_Beacon == null) BuildBeacon();
            m_Beacon.gameObject.SetActive(show);
            if (show)
            {
                var t = target.Value;
                m_Beacon.position = new Vector3(t.x, t.y, t.z);
                var to = head.position - m_Beacon.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.001f) m_Beacon.rotation = Quaternion.LookRotation(-to.normalized, Vector3.up);
                float d = to.magnitude;
                m_Beacon.localScale = new Vector3(Mathf.Clamp(d * 0.02f, 0.3f, 1.6f), 1f, 1f);
                float a = 0.55f + 0.2f * Mathf.Sin(Time.time * Mathf.PI * 0.9f);
                m_BeaconMat.SetColor("_TintColor", new Color(0.55f, 0.95f, 0.85f, a));
            }
            // Screen marker (desktop): a diamond over the target with its name and distance. When the target is off screen
            // it waits at the edge pointing the way, far enough in for its name to stay on screen, and clear of the panels
            // along the edges.
            if (vr || !show) { m_Marker.gameObject.SetActive(false); MarkerRect = null; return; }
            float dist = (target.Value - Game.Rig.Feet).magnitude;
            string label = s.LabelNow != null ? s.LabelNow() : s.Label;
            m_MarkerLabel.text = (string.IsNullOrEmpty(label) ? "" : label + "\n") + "<size=80%>" + Mathf.RoundToInt(dist) + " m</size>";
            var size = m_MarkerLabel.GetPreferredValues(m_MarkerLabel.text, 220f, 0f);
            float px = m_Root.lossyScale.x;                                     // canvas units to screen pixels
            float half = Mathf.Clamp(size.x / 2f, 24f, 110f) * px;              // its reach either side,
            float up = 48f * px, down = (22f + size.y) * px;                    // above (icon, arrow) and below (its name)
            var cam = Game.Rig.Cam;
            var sp = cam.WorldToScreenPoint(target.Value + Vector3.up * 0.35f);
            bool behind = sp.z < 0f;
            float sw = Screen.width, sh = Screen.height;
            Vector2 p = new Vector2(sp.x, sp.y);
            if (behind) p = new Vector2(sw - p.x, sh - p.y);
            float mx = Mathf.Max(60f, half + 8f), mb = Mathf.Max(60f, down + 8f), mt = Mathf.Max(60f, up + 8f);
            bool off = behind || p.x < mx || p.x > sw - mx || p.y < mb || p.y > sh - mt;
            var c = new Vector2(sw / 2f, sh / 2f);
            if (off)
            {
                var dir = (p - c);
                if (behind && dir.sqrMagnitude < 1f) dir = Vector2.down;
                dir.Normalize();
                float kx = (sw / 2f - mx) / Mathf.Max(0.001f, Mathf.Abs(dir.x));
                float ky = (dir.y < 0f ? sh / 2f - mb : sh / 2f - mt) / Mathf.Max(0.001f, Mathf.Abs(dir.y));
                p = c + dir * Mathf.Min(kx, ky);
                KeepClear(ref p, kx <= ky, half, up, down);
                m_MarkerArrow.gameObject.SetActive(true);
                m_MarkerArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            }
            else m_MarkerArrow.gameObject.SetActive(false);
            m_Marker.gameObject.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Root, p, null, out var local);
            m_Marker.localPosition = local;
            MarkerRect = Rect.MinMaxRect(p.x - half, p.y - down, p.x + half, p.y + up);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 1.4f);
            m_MarkerIcon.rectTransform.localScale = Vector3.one * pulse;
        }

        /// <summary>
        /// Moves a marker held at the screen edge further in, along that edge's normal, until it is clear of the panels
        /// there: the objectives panel (Tab), the step banner, the passport card and the minimap. Never past the middle.
        /// </summary>
        void KeepClear(ref Vector2 p, bool side, float half, float up, float down)
        {
            m_Avoid.Clear();
            AddAvoid(Hud.I.ObjectivesPanel);
            AddAvoid(BannerShowing ? m_Banner : null);
            AddAvoid(Hud.I.PassportCard);
            if (MapView.I != null && MapView.I.MiniScreenRect.HasValue) m_Avoid.Add(MapView.I.MiniScreenRect.Value);
            float sw = Screen.width, sh = Screen.height, gap = 8f;
            for (int pass = 0; pass < 2; pass++)
                foreach (var r in m_Avoid)
                {
                    if (!Rect.MinMaxRect(p.x - half - gap, p.y - down - gap, p.x + half + gap, p.y + up + gap).Overlaps(r)) continue;
                    if (side) p.x = p.x < sw / 2f ? Mathf.Min(r.xMax + half + gap, sw / 2f - half) : Mathf.Max(r.xMin - half - gap, sw / 2f + half);
                    else p.y = p.y > sh / 2f ? Mathf.Max(r.yMin - up - gap, sh / 2f + up) : Mathf.Min(r.yMax + down + gap, sh / 2f - down);
                }
        }

        void AddAvoid(RectTransform rt)
        {
            if (rt == null) return;
            rt.GetWorldCorners(s_Corners);   // desktop: the HUD canvas is a screen-space overlay, so these are screen pixels
            m_Avoid.Add(Rect.MinMaxRect(s_Corners[0].x, s_Corners[0].y, s_Corners[2].x, s_Corners[2].y));
        }

        void BuildBeacon()
        {
            var go = new GameObject("GuideBeacon");
            DontDestroyOnLoad(go);
            m_Beacon = go.transform;
            m_BeaconMat = new Material(Mats.Particle(Tex.Streak, true, new Color(0.55f, 0.95f, 0.85f), 1.2f)) { name = "GuideBeacon" };
            m_BeaconMat.SetVector("_NearFade", new Vector4(1.2f, 1.5f, 0f, 0f));
            var q = MeshFactory.Quad("Pillar", m_Beacon, 0.35f, 3.2f, m_BeaconMat, new Vector3(0f, 1.6f, 0f), Quaternion.identity);
            q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var dm = new Material(Mats.Particle(Tex.SoftDot, true, new Color(0.7f, 1f, 0.92f), 1.6f)) { name = "GuideBeaconTop" };
            var top = MeshFactory.Quad("Glow", m_Beacon, 0.45f, 0.45f, dm, new Vector3(0f, 2.3f, 0f), Quaternion.identity);
            top.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ UI
        bool BuildUi()
        {
            var canvas = Hud.I.GetComponentInChildren<Canvas>(true);
            if (canvas == null) return false;
            MakeSprites();
            // Built again when the HUD is rebuilt (the rig switched modes): show the current step and checklist again.
            m_Step = -1;
            m_LastChecklist = null;
            bool vr = Game.Mode == GameMode.VR;
            m_Root = canvas.GetComponent<RectTransform>();

            m_Banner = Panel("GuideBanner", m_Root, new Color(0.02f, 0.024f, 0.03f, 0.78f));
            m_BannerFade = m_Banner.gameObject.AddComponent<CanvasGroup>();
            PlaceBanner();
            m_Header = Label(m_Banner, 14, Hud.Amber, true);
            m_Header.characterSpacing = 6f;
            m_Text = Label(m_Banner, 23, Hud.Ivory, false);
            m_Meter = new GameObject("Meter", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Meter.SetParent(m_Banner, false);
            var mbg = MakeImage("Bg", m_Meter, new Color(1f, 1f, 1f, 0.12f), s_Panel);
            mbg.type = Image.Type.Sliced;
            Stretch(mbg.rectTransform);
            m_MeterFill = MakeImage("Fill", m_Meter, Hud.Teal, null);
            Stretch(m_MeterFill.rectTransform);
            m_MeterFill.type = Image.Type.Filled;
            m_MeterFill.fillMethod = Image.FillMethod.Horizontal;
            m_MeterFill.sprite = Sprite.Create(Tex.White, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            m_Hint = Label(m_Banner, 18, new Color(0.86f, 0.84f, 0.78f), false);
            m_Hint.fontStyle = FontStyles.Italic;
            m_Alert = Label(m_Banner, 19, Hud.Danger, false);
            m_Footer = Label(m_Banner, 13, new Color(0.6f, 0.6f, 0.58f), false);
            m_Footer.text = vr ? "" : Hud.KeyTag(Intent.Objectives) + "  all steps     " + Hud.KeyTag(Intent.Map) + "  map";

            // Briefing card, over the view for a few seconds on arrival. Desktop: 150 from the left edge at any aspect ratio,
            // and the title card moves aside for it. VR: centred, once the title card has gone.
            m_Brief = Panel("GuideBriefing", m_Root, new Color(0.02f, 0.024f, 0.03f, 0.86f));
            m_Brief.anchorMin = m_Brief.anchorMax = m_Brief.pivot = vr ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
            m_Brief.anchoredPosition = vr ? new Vector2(0f, 40f) : new Vector2(150f, 40f);
            m_Brief.sizeDelta = new Vector2(660f, 600f);
            m_BriefText = Label(m_Brief, 30, Hud.Ivory, false);
            m_BriefText.rectTransform.anchorMin = Vector2.zero;
            m_BriefText.rectTransform.anchorMax = Vector2.one;
            m_BriefText.rectTransform.offsetMin = new Vector2(34f, 28f);
            m_BriefText.rectTransform.offsetMax = new Vector2(-34f, -28f);
            m_BriefText.alignment = TextAlignmentOptions.TopLeft;
            m_BriefText.enableAutoSizing = true;
            m_BriefText.fontSizeMin = 14f;
            m_BriefText.fontSizeMax = 30f;

            // Screen marker (desktop).
            m_Marker = new GameObject("GuideMarker", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Marker.SetParent(m_Root, false);
            m_Marker.sizeDelta = new Vector2(40f, 40f);
            m_MarkerIcon = MakeImage("Icon", m_Marker, new Color(0.55f, 0.95f, 0.85f, 0.95f), s_Diamond);
            m_MarkerIcon.rectTransform.sizeDelta = new Vector2(26f, 26f);
            m_MarkerArrow = MakeImage("Arrow", m_Marker, new Color(0.55f, 0.95f, 0.85f, 0.95f), s_Chevron);
            m_MarkerArrow.rectTransform.sizeDelta = new Vector2(30f, 18f);
            m_MarkerArrow.rectTransform.pivot = new Vector2(0.5f, -1.2f);
            m_MarkerLabel = Label(m_Marker, 16, new Color(0.88f, 0.98f, 0.95f), false);
            m_MarkerLabel.alignment = TextAlignmentOptions.Top;
            m_MarkerLabel.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            m_MarkerLabel.rectTransform.sizeDelta = new Vector2(220f, 44f);
            m_MarkerLabel.rectTransform.pivot = new Vector2(0.5f, 1f);

            m_Banner.gameObject.SetActive(false);
            m_Brief.gameObject.SetActive(false);
            m_Marker.gameObject.SetActive(false);
            Hud.I.KeepTitleClearOf(m_Brief);
            Hud.I.KeepTitleClearOf(m_Banner);
            return true;
        }

        /// <summary>
        /// Desktop: bottom left, above the subtitle band, except while the passport is in view. The passport is held in the
        /// lower left at every aspect ratio, so then the banner waits at the top left under the toast line (beside the
        /// objectives panel while Tab has it open), and fades in there instead of jumping. VR: top right of the view.
        /// </summary>
        void PlaceBanner()
        {
            Vector2 corner, pos;
            if (Game.Mode == GameMode.VR) { corner = Vector2.one; pos = new Vector2(-110f, -110f); }
            else if (Game.Passport != null && Game.Passport.InView)
            {
                var tab = Hud.I.ObjectivesPanel;
                corner = new Vector2(0f, 1f);
                pos = new Vector2(tab != null ? Hud.I.CanvasRect(tab).xMax + m_Root.rect.width / 2f + 16f : 36f, -120f);
            }
            else { corner = Vector2.zero; pos = new Vector2(36f, 128f); }
            if (m_Banner.anchorMin == corner && m_Banner.anchoredPosition == pos) return;
            m_Banner.anchorMin = m_Banner.anchorMax = m_Banner.pivot = corner;
            m_Banner.anchoredPosition = pos;
            m_BannerFade.alpha = 0f;
        }

        /// <summary>Stacks the banner's lines and sizes the panel to them.</summary>
        void Layout()
        {
            const float W = 560f, P = 18f;
            float y = -P;
            y = Stack(m_Header, y, W - 2 * P, 4f);
            y = Stack(m_Text, y, W - 2 * P, 8f);
            if (m_Meter.gameObject.activeSelf)
            {
                m_Meter.anchorMin = m_Meter.anchorMax = new Vector2(0f, 1f);
                m_Meter.pivot = new Vector2(0f, 1f);
                m_Meter.anchoredPosition = new Vector2(P, y - 2f);
                m_Meter.sizeDelta = new Vector2(W - 2 * P, 7f);
                y -= 17f;
            }
            if (m_Hint.gameObject.activeSelf) y = Stack(m_Hint, y, W - 2 * P, 6f);
            if (m_Alert.gameObject.activeSelf) y = Stack(m_Alert, y, W - 2 * P, 6f);
            if (!string.IsNullOrEmpty(m_Footer.text)) y = Stack(m_Footer, y - 2f, W - 2 * P, 0f);
            m_Banner.sizeDelta = new Vector2(W, -y + P - 4f);
        }

        static float Stack(TextMeshProUGUI t, float y, float w, float gap)
        {
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            var pref = t.GetPreferredValues(t.text, w, 0f);
            rt.sizeDelta = new Vector2(w, pref.y);
            rt.anchoredPosition = new Vector2(18f, y);
            return y - pref.y - gap;
        }

        static RectTransform Panel(string name, Transform parent, Color c)
        {
            var img = MakeImage(name, parent, c, s_Panel);
            img.type = Image.Type.Sliced;
            return img.rectTransform;
        }

        static Image MakeImage(string name, Transform parent, Color c, Sprite s)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = c;
            img.sprite = s;
            img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static TextMeshProUGUI Label(Transform parent, float size, Color c, bool bold)
        {
            var t = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (TextFactory.Sans != null) t.font = TextFactory.Sans;
            t.fontSize = size;
            t.color = c;
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        static void MakeSprites()
        {
            if (s_Panel != null) return;
            const int n = 48, r = 14;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "GuidePanel" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (n - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (n - r)));
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            s_Panel = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            s_Diamond = Icon(48, (x, y) => Mathf.Clamp01((0.92f - (Mathf.Abs(x) + Mathf.Abs(y))) * 16f) * (Mathf.Abs(x) + Mathf.Abs(y) > 0.5f ? 1f : 0.55f));
            s_Chevron = Icon(48, (x, y) => (y < 0.6f - Mathf.Abs(x) * 1.2f && y > 0.15f - Mathf.Abs(x) * 1.2f) ? 1f : 0f);
        }

        static Sprite Icon(int n, System.Func<float, float, float> alpha)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "GuideIcon" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / n * 2f - 1f, fy = (y + 0.5f) / n * 2f - 1f;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(fx, fy)) * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
