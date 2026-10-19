using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TwelfthStamp
{
    /// <summary>
    /// Maps for every place. On entering a cell its walkable floors and its walls are drawn top-down into a texture once
    /// (parchment floors, dark walls and props, an inked outline), then shown as a minimap in the top-right corner
    /// (player-centred, turning with the view) and as a full map on M (north up, with a legend and the step list).
    /// Icons: you, the next objective (from the Guide), the waiting stamp, tear seams, the landmark, and the Passenger
    /// when it is near and visible. Built on the HUD's canvas, so the minimap also shows in VR (the full map is desktop).
    /// </summary>
    public class MapView : MonoBehaviour
    {
        public static MapView I { get; private set; }

        const float MiniRadius = 118f;      // minimap radius, UI px
        const float MiniMetres = 24f;       // metres from the centre to the rim
        const float FullSize = 860f;        // full map, UI px
        const float MiniInset = 30f;        // desktop: the minimap's top and right edges from the screen corner, UI px

        static readonly Color32 Outside = new Color32(14, 16, 20, 230);
        static readonly Color32 FloorEdge = new Color32(96, 84, 66, 255);
        static readonly Color32 Wall = new Color32(52, 48, 45, 255);
        static readonly Color32 Water = new Color32(92, 128, 140, 255);
        // floor colours by footstep surface: 1 paving/stone, 4 sand, 5 tile, 6 wood
        static readonly Color32[] FloorColours =
        {
            default, new Color32(200, 188, 160, 255), default, default,
            new Color32(222, 196, 140, 255), new Color32(214, 210, 198, 255), new Color32(176, 136, 96, 255),
        };

        class MapData
        {
            public Texture2D Tex;
            public float MinX, MinZ, Ppm;
            public Vector3? Landmark;
            public Vector2 ToPx(Vector3 w) => new Vector2((w.x - MinX) * Ppm, (w.z - MinZ) * Ppm);
        }

        readonly Dictionary<Cell, MapData> m_Maps = new Dictionary<Cell, MapData>();
        RectTransform m_Root, m_Mini, m_MiniRot, m_MiniMap, m_Full, m_FullMap;
        RawImage m_MiniImg, m_FullImg;
        Image m_MiniPlayer, m_FullPlayer, m_MiniNorth;
        TextMeshProUGUI m_FullTitle, m_FullSteps, m_MiniLabel, m_MiniHint;
        readonly List<Image> m_MiniIcons = new List<Image>();
        readonly List<Image> m_FullIcons = new List<Image>();
        readonly List<Image> m_MiniEdge = new List<Image>();
        bool m_FullOpen;
        float m_MiniFit = 1f;
        Cell m_Cell;
        MapData m_Data;
        static Sprite s_Circle, s_Ring, s_Diamond, s_Arrow, s_Star, s_Dot, s_Card, s_Halo;
        const int PostcardsTotal = 33;   // three in each of the eleven countries
        static readonly Vector3[] s_Corners = new Vector3[4];

        public bool FullOpen => m_FullOpen;
        public string MiniLabel => m_MiniLabel != null ? m_MiniLabel.text : "";
        public bool HasMap => m_Data != null && m_Data.Tex != null;
        public bool MiniShowing => m_Mini != null && m_Mini.gameObject.activeSelf;
        /// <summary>Desktop, while the minimap shows: it with its label and hint, in screen pixels (the guide's edge marker
        /// keeps clear of it).</summary>
        public Rect? MiniScreenRect { get; private set; }
        public string MapSize => HasMap ? m_Data.Tex.width + "x" + m_Data.Tex.height + " px, " + (m_Data.Tex.width / m_Data.Ppm).ToString("0") + " x " + (m_Data.Tex.height / m_Data.Ppm).ToString("0") + " m" : "none";

        /// <summary>Opens or closes the full map (M does the same on desktop).</summary>
        public void SetFull(bool open) => m_FullOpen = open;

        void Awake() { I = this; }

        // ------------------------------------------------------------------ per frame
        void LateUpdate()
        {
            if (Hud.I == null || Game.Cells == null) return;
            if (m_Root == null && !BuildUi()) return;
            var cell = Game.Cells.Active;
            if (cell != m_Cell)
            {
                m_Cell = cell;
                m_Data = null;
                if (cell != null && cell.Id != CellId.Menu)
                {
                    if (!m_Maps.TryGetValue(cell, out m_Data) || m_Data == null)
                        m_Maps[cell] = m_Data = Generate(cell);
                }
                m_MiniImg.texture = m_FullImg.texture = m_Data != null ? m_Data.Tex : null;
            }
            bool vr = Game.Mode == GameMode.VR;
            bool show = m_Data != null && Game.Rig != null && !Game.InMenu && !DimensionShift.Running
                        && (Guide.I == null || !Guide.I.Cinematic);
            if (!show) m_FullOpen = false;
            else if (!vr && Game.Input != null && Game.Input.Map != null && Game.Input.Map.WasPressedThisFrame()) m_FullOpen = !m_FullOpen;
            m_Mini.gameObject.SetActive(false); // no minimap (18 Oct, the user asked for it gone); M still opens the full map on desktop
            m_Full.gameObject.SetActive(show && m_FullOpen);
            if (show && m_FullOpen) Hud.I.HideTitleCard();   // an arrival title would show through the map's panels
            MiniScreenRect = null;
            if (!show) return;

            var feet = Game.Rig.Feet;
            float yaw = Game.Rig.Head.eulerAngles.y;
            var px = m_Data.ToPx(feet);
            var size = new Vector2(m_Data.Tex.width, m_Data.Tex.height);
            var markers = Markers();
            if (!m_FullOpen)
            {
                float k = MiniRadius / (MiniMetres * m_Data.Ppm);   // UI px per map px
                m_MiniMap.sizeDelta = size * k;
                m_MiniMap.pivot = new Vector2(px.x / size.x, px.y / size.y);
                m_MiniMap.anchoredPosition = Vector2.zero;
                m_MiniRot.localRotation = Quaternion.Euler(0f, 0f, yaw);
                m_MiniNorth.rectTransform.anchoredPosition = Quaternion.Euler(0f, 0f, yaw) * new Vector2(0f, MiniRadius - 2f);
                PlaceIcons(m_MiniIcons, m_MiniMap, markers, k, -yaw, true, px);
                if (!vr) FitMinimap();
                var here = m_Cell != null ? m_Cell.Country : null;
                m_MiniLabel.text = here == null ? "" : here.Place + (HasPostcards(here.Id) ? "  \u00B7  postcards " + Postcard.CollectedIn(here.Id) + "/3" : "");
            }
            else
            {
                float k = Mathf.Min(FullSize / size.x, FullSize / size.y);
                m_FullMap.sizeDelta = size * k;
                m_FullPlayer.rectTransform.anchoredPosition = px * k;
                m_FullPlayer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -yaw);
                PlaceIcons(m_FullIcons, m_FullMap, markers, k, 0f, false, px);
                var c = m_Cell != null ? m_Cell.Country : null;
                m_FullTitle.text = c != null ? "<b>" + c.DisplayName.ToUpperInvariant() + "</b>  <color=#A8A8A4><nobr>" + c.Place + "</nobr></color>" +
                    "\n<size=60%><color=#F2E4C0>Postcards found here " + Postcard.CollectedIn(c.Id) + " / 3     all " + Postcard.CollectedTotal() + " / " + PostcardsTotal + "</color></size>" : "";
                m_FullSteps.text = (Guide.I != null ? Guide.I.ChecklistText : "") +
                    "\n\n<size=80%><color=#8E8E8A>" + Legend() + "\nM  close map</color></size>";
                // The steps start under the title, however many lines a long place name takes (the place moves down whole).
                float th = m_FullTitle.GetPreferredValues(m_FullTitle.text, 520f, 0f).y;
                m_FullTitle.rectTransform.sizeDelta = new Vector2(520f, th);
                m_FullSteps.rectTransform.anchoredPosition = new Vector2(230f, FullSize / 2f - 32f - th);
                m_FullSteps.rectTransform.sizeDelta = new Vector2(520f, FullSize - 42f - th);
            }
        }

        /// <summary>
        /// Desktop. The passport card is pinned to the middle of the right edge, so on a wide, short screen (2560x1080) it
        /// reaches up into the minimap's column, and a tear's card is taller still. Then the minimap, its label and its hint
        /// shrink toward the corner, just enough to stay above the card, and grow back when the card goes.
        /// </summary>
        void FitMinimap()
        {
            float fit = 1f;
            var card = Hud.I.PassportCard;
            if (card != null)
            {
                var r = Hud.I.CanvasRect(card);
                float top = m_Root.rect.height / 2f - MiniInset;    // the minimap's top edge, from the canvas centre
                float drop = MiniRadius * 2f + 49f;                 // from there down to the bottom of the "M map" hint
                bool column = r.xMax > m_Root.rect.width / 2f - MiniInset - MiniRadius * 2f - 40f;
                if (column && r.yMax > top - drop - 10f) fit = Mathf.Clamp((top - r.yMax - 10f) / drop, 0.5f, 1f);
            }
            // At once when the card appears (it does in one frame), gently when it goes.
            m_MiniFit = fit < m_MiniFit ? fit : Mathf.MoveTowards(m_MiniFit, fit, Time.unscaledDeltaTime * 1.5f);
            m_Mini.localScale = new Vector3(m_MiniFit, m_MiniFit, 1f);

            var a = ScreenRect(m_Mini);
            var l = ScreenRect(m_MiniLabel.rectTransform);
            a = Rect.MinMaxRect(Mathf.Min(a.xMin, l.xMin), Mathf.Min(a.yMin, l.yMin), Mathf.Max(a.xMax, l.xMax), a.yMax);
            if (m_MiniHint != null) a.yMin = Mathf.Min(a.yMin, ScreenRect(m_MiniHint.rectTransform).yMin);
            MiniScreenRect = a;
        }

        /// <summary>Where a panel on the HUD canvas is in screen pixels (desktop: the canvas is a screen-space overlay).</summary>
        static Rect ScreenRect(RectTransform rt)
        {
            rt.GetWorldCorners(s_Corners);
            return Rect.MinMaxRect(s_Corners[0].x, s_Corners[0].y, s_Corners[2].x, s_Corners[2].y);
        }

        struct Marker
        {
            public Vector3 Pos;
            public Sprite Sprite;
            public Color Color;
            public float Size;
            public bool Clamp;     // keep it on the minimap rim when it is out of range
            public bool Pulse;
        }

        readonly List<Marker> m_Markers = new List<Marker>();

        List<Marker> Markers()
        {
            m_Markers.Clear();
            if (m_Data.Landmark.HasValue)
                m_Markers.Add(new Marker { Pos = m_Data.Landmark.Value, Sprite = s_Star, Color = new Color(0.98f, 0.86f, 0.55f), Size = 20f, Clamp = false });
            if (m_Cell != null)
                foreach (var s in m_Cell.Seams)
                    if (s != null && s.isActiveAndEnabled)
                        m_Markers.Add(new Marker { Pos = s.Center, Sprite = s_Ring, Color = Hud.Teal, Size = 13f });
            foreach (var l in SwitchLamp.All)
                if (l != null && l.isActiveAndEnabled)
                    m_Markers.Add(l.On
                        ? new Marker { Pos = l.transform.position, Sprite = s_Halo, Color = new Color(1f, 0.84f, 0.45f, 0.85f), Size = 30f }
                        : new Marker { Pos = l.transform.position, Sprite = s_Dot, Color = new Color(0.72f, 0.66f, 0.52f, 0.85f), Size = 9f });
            foreach (var card in Postcard.All)
                if (card != null && card.isActiveAndEnabled && !card.Collected)
                    m_Markers.Add(new Marker { Pos = card.transform.position, Sprite = s_Card, Color = new Color(0.98f, 0.92f, 0.76f), Size = 17f });
            var stamp = Game.Story != null ? Game.Story.WaitingStamp : null;
            if (stamp != null)
                m_Markers.Add(new Marker { Pos = stamp.transform.position, Sprite = s_Star, Color = new Color(1f, 0.78f, 0.3f), Size = 26f, Clamp = true, Pulse = true });
            var target = Guide.I != null ? Guide.I.Target : null;
            if (target.HasValue)
                m_Markers.Add(new Marker { Pos = target.Value, Sprite = s_Diamond, Color = new Color(0.55f, 0.95f, 0.85f), Size = 22f, Clamp = true, Pulse = true });
            var pc = Game.Passenger;
            if (pc != null && pc.Visible && Game.Rig != null && (pc.transform.position - Game.Rig.Feet).sqrMagnitude < 30f * 30f)
                m_Markers.Add(new Marker { Pos = pc.transform.position, Sprite = s_Dot, Color = Hud.Danger, Size = 16f, Pulse = true });
            return m_Markers;
        }

        void PlaceIcons(List<Image> pool, RectTransform map, List<Marker> markers, float k, float counter, bool mini, Vector2 playerPx)
        {
            while (pool.Count < markers.Count)
            {
                var img = MakeImage("Icon", map, Color.white, s_Diamond);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.zero;
                pool.Add(img);
            }
            for (int i = 0; i < pool.Count; i++)
            {
                var img = pool[i];
                if (i >= markers.Count) { img.gameObject.SetActive(false); continue; }
                var m = markers[i];
                var p = m_Data.ToPx(m.Pos);
                if (mini)
                {
                    var d = (p - playerPx) * k;
                    float lim = MiniRadius - 10f;
                    if (d.magnitude > lim)
                    {
                        if (!m.Clamp) { img.gameObject.SetActive(false); continue; }
                        d = d.normalized * lim;
                        p = playerPx + d / k;
                    }
                }
                img.gameObject.SetActive(true);
                img.sprite = m.Sprite;
                float pulse = m.Pulse ? 1f + 0.18f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 1.6f) : 1f;
                img.color = m.Color;
                img.rectTransform.sizeDelta = Vector2.one * m.Size * pulse * (mini ? 1f : 1.25f);
                img.rectTransform.anchoredPosition = p * k;
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, counter);
                img.transform.SetAsLastSibling();
            }
        }

        static string Legend()
        {
            return "<color=#8CF2D9>◆</color> next objective   <color=#FFC64D>★</color> stamp / landmark   " +
                   "<color=#85E6CC>○</color> tear seam   <color=#ED6654>●</color> the Passenger\n" +
                   "<color=#FAEBC2>■</color> postcard to find   <color=#FFD673>●</color> lit lamp: it will not come into the light";
        }

        static bool HasPostcards(CountryId id)
        {
            foreach (var pc in Postcard.All) if (pc != null && pc.Country == id) return true;
            return false;
        }

        // ------------------------------------------------------------------ map generation
        MapData Generate(Cell cell)
        {
            float floorY = cell.Arrival != null ? cell.Arrival.position.y : cell.transform.position.y;
            var origin = cell.Arrival != null ? cell.Arrival.position : cell.transform.position;
            var cols = cell.GetComponentsInChildren<Collider>(false);
            // The play area: the cell's bound walls when it has them, otherwise its floors near the arrival.
            bool bounded = false;
            var area = new Bounds(origin, new Vector3(30f, 4f, 30f));
            var play = new Bounds(origin, Vector3.zero);
            foreach (var c in cols)
                if (c.enabled && !c.isTrigger && c.gameObject.name == "Bound")
                {
                    if (!bounded) { play = c.bounds; bounded = true; }
                    else play.Encapsulate(c.bounds);
                }
            if (bounded)
            {
                area = play;
                area.Expand(new Vector3(6f, 0f, 6f));
            }
            else
            {
                foreach (var c in cols)
                {
                    if (!c.enabled || c.isTrigger || c.gameObject.layer != Layers.Floor) continue;
                    var b = c.bounds;
                    if ((b.center - origin).sqrMagnitude > 220f * 220f) continue;   // the Between, far vistas
                    if (b.max.y < floorY - 2.5f || b.min.y > floorY + 4f) continue;
                    area.Encapsulate(b);
                }
                area.Expand(new Vector3(10f, 0f, 10f));
            }
            var ext = area.extents;
            ext.x = Mathf.Min(ext.x, 140f);
            ext.z = Mathf.Min(ext.z, 140f);
            area.extents = ext;
            float ppm = Mathf.Min(5f, 900f / Mathf.Max(area.size.x, area.size.z));
            int w = Mathf.Max(32, Mathf.CeilToInt(area.size.x * ppm));
            int h = Mathf.Max(32, Mathf.CeilToInt(area.size.z * ppm));
            var data = new MapData { MinX = area.min.x, MinZ = area.min.z, Ppm = ppm };
            var kind = new byte[w * h];   // 0 outside, 1 paving, 2 wall/prop, 3 water, 4 sand, 5 tile, 6 wood

            foreach (var c in cols)
            {
                if (!c.enabled || c.isTrigger) continue;
                var b = c.bounds;
                if (b.max.x < area.min.x || b.min.x > area.max.x || b.max.z < area.min.z || b.min.z > area.max.z) continue;
                bool floor = c.gameObject.layer == Layers.Floor;
                if (floor)
                {
                    if (b.max.y < floorY - 2.5f || b.min.y > floorY + 4f) continue;
                    var surf = c.GetComponent<Surface>();
                    string sk = surf != null ? surf.Kind : "stone";
                    byte fk = sk == "sand" ? (byte)4 : sk == "tile" ? (byte)5 : sk == "wood" ? (byte)6 : (byte)1;
                    RasterCollider(c, data, kind, w, h, fk, true);
                }
                else
                {
                    if (b.max.y < floorY + 0.25f || b.min.y > floorY + 2.2f) continue;   // only what blocks a walker
                    if (b.size.x * b.size.z < 0.04f && b.size.y < 0.5f) continue;
                    RasterCollider(c, data, kind, w, h, 2, false);
                }
            }

            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    byte k = kind[i];
                    bool isFloor = k == 1 || k >= 4;
                    Color32 col = isFloor ? FloorColours[k] : k == 2 ? Wall : k == 3 ? Water : Outside;
                    if (isFloor)
                    {
                        bool edge = false;
                        for (int dy = -1; dy <= 1 && !edge; dy++)
                            for (int dx = -1; dx <= 1 && !edge; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= w || yy >= h) { edge = true; continue; }
                                byte o = kind[yy * w + xx];
                                if (o == 0 || o == 2) edge = true;
                            }
                        if (edge) col = FloorEdge;
                        else if (k != 4)
                        {
                            // a faint grid every 2 m on paving reads as scale
                            float wx = data.MinX + x / ppm, wz = data.MinZ + y / ppm;
                            if (Mathf.Repeat(wx, 2f) < 1f / ppm || Mathf.Repeat(wz, 2f) < 1f / ppm)
                                col = new Color32((byte)(col.r - 14), (byte)(col.g - 14), (byte)(col.b - 12), 255);
                        }
                    }
                    if (bounded)
                    {
                        float wx = data.MinX + (x + 0.5f) / ppm, wz = data.MinZ + (y + 0.5f) / ppm;
                        if (wx < play.min.x || wx > play.max.x || wz < play.min.z || wz > play.max.z)
                            col = new Color32((byte)(col.r / 3), (byte)(col.g / 3), (byte)(col.b / 3), col.a);   // out of bounds
                    }
                    px[i] = col;
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Map_" + cell.Id, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply();
            data.Tex = tex;

            var info = cell.GetComponentInChildren<VistaInfo>(true);
            if (info != null)
            {
                var rs = info.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    Renderer biggest = rs[0];
                    foreach (var r in rs) if (r.bounds.size.y > biggest.bounds.size.y) biggest = r;
                    var lp = biggest.bounds.center;
                    lp.x = Mathf.Clamp(lp.x, area.min.x + 2f, area.max.x - 2f);
                    lp.z = Mathf.Clamp(lp.z, area.min.z + 2f, area.max.z - 2f);
                    data.Landmark = lp;
                }
            }
            Debug.Log($"[TS-Map] {cell.Id}: {w}x{h} px at {ppm:0.0} px/m, {cols.Length} colliders");
            return data;
        }

        static void RasterCollider(Collider c, MapData d, byte[] kind, int w, int h, byte k, bool floor)
        {
            if (c is BoxCollider box)
            {
                var t = box.transform;
                var hs = box.size * 0.5f;
                var corners = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    var lc = box.center + new Vector3(i == 0 || i == 3 ? -hs.x : hs.x, floor ? hs.y : 0f, i < 2 ? -hs.z : hs.z);
                    corners[i] = t.TransformPoint(lc);
                }
                FillTri(d, kind, w, h, corners[0], corners[1], corners[2], k);
                FillTri(d, kind, w, h, corners[0], corners[2], corners[3], k);
                if (!floor) for (int i = 0; i < 4; i++) Line(d, kind, w, h, corners[i], corners[(i + 1) % 4], k);
            }
            else if (c is MeshCollider mc && mc.sharedMesh != null && mc.sharedMesh.isReadable)
            {
                var mesh = mc.sharedMesh;
                var v = mesh.vertices;
                var t = mesh.triangles;
                var m = mc.transform.localToWorldMatrix;
                var wv = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) wv[i] = m.MultiplyPoint3x4(v[i]);
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = wv[t[i]], b = wv[t[i + 1]], cc = wv[t[i + 2]];
                    var n = Vector3.Cross(b - a, cc - a);
                    if (floor)
                    {
                        if (n.sqrMagnitude < 1e-10f || Mathf.Abs(n.normalized.y) < 0.55f) continue;
                        FillTri(d, kind, w, h, a, b, cc, k);
                    }
                    else
                    {
                        float lo = Mathf.Min(a.y, Mathf.Min(b.y, cc.y)), hi = Mathf.Max(a.y, Mathf.Max(b.y, cc.y));
                        FillTri(d, kind, w, h, a, b, cc, k);
                        if (hi - lo > 0.2f) { Line(d, kind, w, h, a, b, k); Line(d, kind, w, h, b, cc, k); Line(d, kind, w, h, cc, a, k); }
                    }
                }
            }
            else
            {
                var b = c.bounds;
                var a0 = new Vector3(b.min.x, 0f, b.min.z);
                var a1 = new Vector3(b.max.x, 0f, b.min.z);
                var a2 = new Vector3(b.max.x, 0f, b.max.z);
                var a3 = new Vector3(b.min.x, 0f, b.max.z);
                FillTri(d, kind, w, h, a0, a1, a2, k);
                FillTri(d, kind, w, h, a0, a2, a3, k);
            }
        }

        static void FillTri(MapData d, byte[] kind, int w, int h, Vector3 a, Vector3 b, Vector3 c, byte k)
        {
            Vector2 pa = d.ToPx(a), pb = d.ToPx(b), pc = d.ToPx(c);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))));
            if (x1 < x0 || y1 < y0) return;
            float area = (pb.x - pa.x) * (pc.y - pa.y) - (pc.x - pa.x) * (pb.y - pa.y);
            if (Mathf.Abs(area) < 1e-6f) return;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = (pb.x - px) * (pc.y - py) - (pc.x - px) * (pb.y - py);
                    float w1 = (pc.x - px) * (pa.y - py) - (pa.x - px) * (pc.y - py);
                    float w2 = (pa.x - px) * (pb.y - py) - (pb.x - px) * (pa.y - py);
                    bool inside = area > 0 ? (w0 >= 0 && w1 >= 0 && w2 >= 0) : (w0 <= 0 && w1 <= 0 && w2 <= 0);
                    if (!inside) continue;
                    int i = y * w + x;
                    if (k == 2 || kind[i] == 0) kind[i] = k;
                }
        }

        static void Line(MapData d, byte[] kind, int w, int h, Vector3 a, Vector3 b, byte k)
        {
            Vector2 pa = d.ToPx(a), pb = d.ToPx(b);
            int n = Mathf.CeilToInt((pb - pa).magnitude) + 1;
            for (int i = 0; i <= n; i++)
            {
                var p = Vector2.Lerp(pa, pb, i / (float)n);
                int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                kind[y * w + x] = k;
            }
        }

        // ------------------------------------------------------------------ UI
        bool BuildUi()
        {
            var canvas = Hud.I.GetComponentInChildren<Canvas>(true);
            if (canvas == null) return false;
            MakeSprites();
            // Built again when the HUD is rebuilt (the rig switched modes): the old icons went with the old canvas, and the
            // new map images need the current cell's map.
            m_MiniIcons.Clear();
            m_FullIcons.Clear();
            m_MiniEdge.Clear();
            m_Cell = null;
            bool vr = Game.Mode == GameMode.VR;
            m_Root = new GameObject("Maps", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Root.SetParent(canvas.transform, false);
            m_Root.anchorMin = Vector2.zero; m_Root.anchorMax = Vector2.one; m_Root.offsetMin = m_Root.offsetMax = Vector2.zero;

            // Minimap: a round window, top right (VR: lower left of the view, out of the way of the prompts).
            m_Mini = new GameObject("Minimap", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Mini.SetParent(m_Root, false);
            var corner = vr ? new Vector2(0f, 0f) : new Vector2(1f, 1f);
            m_Mini.anchorMin = m_Mini.anchorMax = corner;
            m_Mini.pivot = vr ? new Vector2(0.5f, 0.5f) : Vector2.one;   // desktop: held by its top-right corner, to shrink into it
            m_Mini.anchoredPosition = vr ? new Vector2(190f, 190f) : new Vector2(-MiniInset, -MiniInset);
            m_Mini.sizeDelta = Vector2.one * (MiniRadius * 2f + 8f);
            var rim = MakeImage("Rim", m_Mini, new Color(0.02f, 0.025f, 0.03f, 0.82f), s_Circle);
            rim.rectTransform.sizeDelta = Vector2.one * (MiniRadius * 2f + 8f);
            var maskImg = MakeImage("Mask", m_Mini, Color.white, s_Circle);
            maskImg.rectTransform.sizeDelta = Vector2.one * MiniRadius * 2f;
            var mask = maskImg.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            m_MiniRot = new GameObject("Turn", typeof(RectTransform)).GetComponent<RectTransform>();
            m_MiniRot.SetParent(maskImg.transform, false);
            m_MiniRot.sizeDelta = Vector2.zero;
            m_MiniImg = new GameObject("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            m_MiniMap = m_MiniImg.rectTransform;
            m_MiniMap.SetParent(m_MiniRot, false);
            m_MiniImg.raycastTarget = false;
            var ring = MakeImage("Ring", m_Mini, new Color(0.93f, 0.81f, 0.6f, 0.55f), s_Ring);
            ring.rectTransform.sizeDelta = Vector2.one * (MiniRadius * 2f + 4f);
            m_MiniPlayer = MakeImage("You", m_Mini, new Color(0.98f, 0.96f, 0.9f), s_Arrow);
            m_MiniPlayer.rectTransform.sizeDelta = new Vector2(20f, 24f);
            m_MiniNorth = MakeImage("North", m_Mini, new Color(0.93f, 0.81f, 0.6f), s_Dot);
            m_MiniNorth.rectTransform.sizeDelta = Vector2.one * 9f;
            m_MiniLabel = Label(m_Mini, 15, new Color(0.85f, 0.83f, 0.78f), TextAlignmentOptions.Center);
            m_MiniLabel.rectTransform.anchoredPosition = new Vector2(0f, -MiniRadius - 18f);
            m_MiniLabel.rectTransform.sizeDelta = new Vector2(270f, 22f);
            m_MiniLabel.textWrappingMode = TextWrappingModes.NoWrap;   // one line, a little smaller for a long place name, so it
            m_MiniLabel.enableAutoSizing = true;                       // never wraps onto the hint below or reaches the screen edge
            m_MiniLabel.fontSizeMin = 11f;
            m_MiniLabel.fontSizeMax = 15f;
            Hud.I.KeepTitleClearOf(m_Mini);   // with its label and hint below: on wide screens they reach the title card's line
            Hud.I.KeepTitleClearOf(m_MiniLabel.rectTransform);
            if (!vr)
            {
                m_MiniHint = Label(m_Mini, 13, new Color(0.62f, 0.62f, 0.6f), TextAlignmentOptions.Center);
                m_MiniHint.text = "M  map";
                m_MiniHint.rectTransform.anchoredPosition = new Vector2(0f, -MiniRadius - 36f);
                m_MiniHint.rectTransform.sizeDelta = new Vector2(200f, 18f);
                Hud.I.KeepTitleClearOf(m_MiniHint.rectTransform);
            }

            // Full map (desktop): centre of the screen, steps beside it.
            m_Full = new GameObject("FullMap", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Full.SetParent(m_Root, false);
            m_Full.anchorMin = Vector2.zero; m_Full.anchorMax = Vector2.one; m_Full.offsetMin = m_Full.offsetMax = Vector2.zero;
            var dim = MakeImage("Dim", m_Full, new Color(0.01f, 0.012f, 0.016f, 0.78f), null);
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;
            var frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(m_Full, false);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = new Vector2(-230f, -10f);
            frame.sizeDelta = Vector2.one * FullSize;
            m_FullImg = new GameObject("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            m_FullMap = m_FullImg.rectTransform;
            m_FullMap.SetParent(frame, false);
            m_FullMap.anchorMin = m_FullMap.anchorMax = new Vector2(0.5f, 0.5f);
            m_FullMap.pivot = new Vector2(0.5f, 0.5f);
            m_FullImg.raycastTarget = false;
            m_FullPlayer = MakeImage("You", m_FullMap, new Color(0.98f, 0.96f, 0.9f), s_Arrow);
            m_FullPlayer.rectTransform.anchorMin = m_FullPlayer.rectTransform.anchorMax = Vector2.zero;
            m_FullPlayer.rectTransform.sizeDelta = new Vector2(24f, 30f);
            var column = MakeImage("Column", m_Full, new Color(0.02f, 0.024f, 0.03f, 0.82f), null);
            column.rectTransform.anchorMin = column.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            column.rectTransform.pivot = new Vector2(0f, 1f);
            column.rectTransform.anchoredPosition = new Vector2(212f, FullSize / 2f + 8f);
            column.rectTransform.sizeDelta = new Vector2(556f, FullSize + 16f);
            m_FullTitle = Label(m_Full, 30, new Color(0.96f, 0.94f, 0.89f), TextAlignmentOptions.TopLeft);
            m_FullTitle.rectTransform.anchorMin = m_FullTitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            m_FullTitle.rectTransform.pivot = new Vector2(0f, 1f);
            m_FullTitle.rectTransform.anchoredPosition = new Vector2(230f, FullSize / 2f - 10f);
            m_FullTitle.rectTransform.sizeDelta = new Vector2(520f, 80f);
            m_FullSteps = Label(m_Full, 21, new Color(0.92f, 0.9f, 0.85f), TextAlignmentOptions.TopLeft);
            m_FullSteps.rectTransform.anchorMin = m_FullSteps.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            m_FullSteps.rectTransform.pivot = new Vector2(0f, 1f);
            m_FullSteps.rectTransform.anchoredPosition = new Vector2(230f, FullSize / 2f - 90f);
            m_FullSteps.rectTransform.sizeDelta = new Vector2(520f, FullSize - 100f);
            m_Full.gameObject.SetActive(false);
            m_Mini.gameObject.SetActive(false);
            return true;
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

        static TextMeshProUGUI Label(Transform parent, float size, Color c, TextAlignmentOptions align)
        {
            var t = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (TextFactory.Sans != null) t.font = TextFactory.Sans;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        static void MakeSprites()
        {
            if (s_Circle != null) return;
            s_Circle = Shape(64, (x, y) => Mathf.Clamp01((1f - Mathf.Sqrt(x * x + y * y)) * 32f));
            s_Ring = Shape(64, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return Mathf.Clamp01((0.09f - Mathf.Abs(r - 0.88f)) * 40f); });
            s_Dot = Shape(32, (x, y) => Mathf.Clamp01((1f - Mathf.Sqrt(x * x + y * y)) * 6f));
            s_Diamond = Shape(64, (x, y) => Mathf.Clamp01((1f - (Mathf.Abs(x) + Mathf.Abs(y))) * 20f));
            s_Arrow = Shape(64, (x, y) =>
            {
                // a chevron arrow pointing up (+y)
                float yy = (y + 1f) / 2f;            // 0 bottom .. 1 top
                float half = (1f - yy) * 0.9f;
                bool body = Mathf.Abs(x) < half && yy > 0.08f;
                float notch = 0.28f * (1f - Mathf.Abs(x) / 0.9f);
                return body && yy > notch ? 1f : 0f;
            });
            s_Card = Shape(48, (x, y) =>
            {
                // a postcard: a small rounded rectangle with a stamp corner
                float dx = Mathf.Max(0f, Mathf.Abs(x) - 0.62f), dy = Mathf.Max(0f, Mathf.Abs(y) - 0.38f);
                float a = Mathf.Clamp01((0.16f - Mathf.Sqrt(dx * dx + dy * dy)) * 30f);
                bool stamp = x > 0.28f && x < 0.62f && y > 0.06f && y < 0.38f;
                return stamp ? a * 0.45f : a;
            });
            s_Halo = Shape(64, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); float a = Mathf.Clamp01(1f - r); return a * a; });
            s_Star = Shape(64, (x, y) =>
            {
                float a = Mathf.Atan2(y, x), r = Mathf.Sqrt(x * x + y * y);
                float edge = 0.55f + 0.4f * Mathf.Pow(Mathf.Abs(Mathf.Cos(2.5f * a)), 3f);
                return Mathf.Clamp01((edge - r) * 20f);
            });
        }

        static Sprite Shape(int n, System.Func<float, float, float> alpha)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "MapIcon" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / n * 2f - 1f, fy = (y + 0.5f) / n * 2f - 1f;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(fx, fy)) * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
