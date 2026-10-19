using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Generic Quest-style (ring-less) touch controller (the Blender model, or a procedural one if it is missing), with
    /// labelled callouts attached to the actual controls. Labels are generated from GameInput bindings, so they always match what the buttons do.
    /// Labels appear during onboarding highlights, or whenever the player raises a controller to look at it.
    /// In VR the player's hands are drawn instead (VRHands); the controller then appears, ghosted, only while its labels are up.
    /// </summary>
    public class ControllerModel : MonoBehaviour
    {
        public HandSide Side;
        /// <summary>Set by VRHands: the hands are drawn, so the controller shows (ghosted) only while its labels are up.</summary>
        public bool HandsShown;
        readonly Dictionary<string, Transform> m_Controls = new Dictionary<string, Transform>();
        readonly Dictionary<string, Renderer> m_ControlRenderers = new Dictionary<string, Renderer>();
        readonly Dictionary<string, Callout> m_Callouts = new Dictionary<string, Callout>();
        readonly HashSet<string> m_Highlighted = new HashSet<string>();
        readonly Dictionary<Material, Material> m_GhostOf = new Dictionary<Material, Material>();
        readonly Dictionary<Material, Material> m_SolidOf = new Dictionary<Material, Material>();
        Renderer[] m_ModelRenderers;
        bool m_Ghosted, m_ModelHidden;
        Material m_Body, m_Button, m_ButtonLit;

        /// <summary>True while any button label is showing on this controller.</summary>
        public bool LabelsShown
        {
            get
            {
                if (m_AutoShown || m_Highlighted.Count > 0) return true;
                foreach (var kv in m_Callouts) if (kv.Value.Root.activeSelf) return true;
                return false;
            }
        }

        class Callout
        {
            public GameObject Root;
            public TextMeshPro Text;
            public LineRenderer Line;
            public Transform Anchor;
            public Vector3 Offset;
        }

        public static ControllerModel Create(Transform grip, HandSide side)
        {
            var go = new GameObject(side + "ControllerModel");
            go.transform.SetParent(grip, false);
            var m = go.AddComponent<ControllerModel>();
            m.Side = side;
            m.Build();
            return m;
        }

        void Build()
        {
            m_Body = Mats.Lit(new Color(0.86f, 0.86f, 0.84f), 0.45f);
            m_Button = Mats.Lit(new Color(0.22f, 0.23f, 0.25f), 0.5f);
            m_ButtonLit = Mats.Emissive(new Color(0.3f, 0.45f, 0.42f), new Color(0.35f, 1.1f, 0.85f));
            if (BuildModel())
            {
                MeshFactory.NoShadows(gameObject);
                m_ModelRenderers = GetComponentsInChildren<Renderer>(true); // before any callout exists
                return;
            }
            float s = Side == HandSide.Left ? 1f : -1f; // mirror x for the right hand

            // Head (flattened ellipsoid) and handle.
            var head = MeshFactory.Sphere("Head", transform, new Vector3(0, 0.012f, 0.028f), 1f, m_Body, 20, 14);
            head.transform.localScale = new Vector3(0.034f, 0.022f, 0.042f);
            var hb = new MeshBuilder();
            hb.AddFrustum(Vector3.zero, 0.019f, 0.016f, 0.1f, 18, true, true, 1f, Quaternion.Euler(-215f, 0, 0));
            MeshFactory.FromBuilder("Handle", hb, m_Body, transform).transform.localPosition = new Vector3(0, 0.0f, 0.012f);

            // Face plate controls.
            float faceY = 0.033f;
            AddButton("thumbstick", new Vector3(-0.009f * s, faceY + 0.004f, 0.036f), 0.0085f, 0.008f, true);
            AddButton("primaryButton", new Vector3(0.011f * s, faceY, 0.02f), 0.0055f, 0.003f, false);
            AddButton("secondaryButton", new Vector3(0.013f * s, faceY, 0.036f), 0.0055f, 0.003f, false);
            if (Side == HandSide.Left) AddButton("menu", new Vector3(-0.011f * s, faceY - 0.002f, 0.012f), 0.0035f, 0.002f, false);
            else AddButton("system", new Vector3(-0.011f * s, faceY - 0.002f, 0.012f), 0.0035f, 0.002f, false);

            // Trigger (front, under the head) and grip (inner side of handle).
            var trig = MeshFactory.Box("trigger", transform, new Vector3(0, -0.006f, 0.062f), new Vector3(0.016f, 0.018f, 0.01f), m_Button, false, 1f, Quaternion.Euler(25f, 0, 0));
            Register("trigger", trig.transform);
            var grip = MeshFactory.Box("grip", transform, new Vector3(0.017f * s, -0.035f, -0.01f), new Vector3(0.006f, 0.04f, 0.02f), m_Button, false, 1f, Quaternion.Euler(-35f, 0, 0));
            Register("grip", grip.transform);
            MeshFactory.NoShadows(gameObject);
            m_ModelRenderers = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>
        /// The Blender controller (Art/Models/MODELS.md): origin at the grip centre, pointing along +Z with the face plate
        /// up, every control a named child. Registering those children puts the callouts and highlights on the real buttons.
        /// </summary>
        bool BuildModel()
        {
            var plate = Mats.Lit(new Color(0.07f, 0.07f, 0.08f), 0.6f);
            var model = Models.Spawn(Side == HandSide.Left ? "Controller_Left" : "Controller_Right", transform,
                Models.Slots(("TS_ControllerBody", m_Body), ("TS_ControllerButton", m_Button), ("TS_ControllerDark", plate)), Vector3.zero, 0f);
            if (model == null) return false;
            foreach (var id in new[] { "thumbstick", "primaryButton", "secondaryButton", Side == HandSide.Left ? "menu" : "system", "trigger", "grip" })
            {
                var t = Models.Child(model, id);
                if (t != null) Register(id, t);
                else Debug.LogWarning("[TS-Controller] Model has no '" + id + "' control");
            }
            return true;
        }

        void AddButton(string id, Vector3 pos, float radius, float height, bool stick)
        {
            var b = new MeshBuilder();
            if (stick)
            {
                b.AddCylinder(Vector3.zero, radius * 0.35f, height, 10);
                b.AddFrustum(new Vector3(0, height, 0), radius, radius * 0.9f, 0.004f, 18);
            }
            else b.AddCylinder(Vector3.zero, radius, height, 18);
            var go = MeshFactory.FromBuilder(id, b, m_Button, transform);
            go.transform.localPosition = pos;
            Register(id, go.transform);
        }

        void Register(string id, Transform t)
        {
            m_Controls[id] = t;
            var r = t.GetComponent<Renderer>();
            if (r != null) m_ControlRenderers[id] = r;
        }

        public Transform Control(string id) => m_Controls.TryGetValue(id, out var t) ? t : transform;

        /// <summary>Show a callout on a control; highlight = glow + pulse (onboarding "press this").</summary>
        public void SetCallout(string control, string text, bool highlight)
        {
            if (!m_Controls.ContainsKey(control)) return;
            if (!m_Callouts.TryGetValue(control, out var c))
            {
                c = MakeCallout(control);
                m_Callouts[control] = c;
            }
            c.Text.text = text;
            c.Root.SetActive(true);
            c.Line.enabled = true;
            if (highlight) m_Highlighted.Add(control); else m_Highlighted.Remove(control);
            if (m_ControlRenderers.TryGetValue(control, out var r)) r.sharedMaterial = highlight ? m_ButtonLit : Look(m_Button);
        }

        public void ClearCallouts()
        {
            foreach (var kv in m_Callouts) { kv.Value.Root.SetActive(false); kv.Value.Line.enabled = false; }
            foreach (var id in m_Highlighted)
                if (m_ControlRenderers.TryGetValue(id, out var r)) r.sharedMaterial = Look(m_Button);
            m_Highlighted.Clear();
        }

        // ------------------------------------------------------------------ hands mode (VR): hidden, or ghosted while labelled
        void ApplyHandsMode()
        {
            if (m_ModelRenderers == null) return;
            bool show = !HandsShown || LabelsShown;
            if (show == m_ModelHidden)
            {
                m_ModelHidden = !show;
                foreach (var r in m_ModelRenderers) if (r != null) r.enabled = show;
            }
            if (HandsShown != m_Ghosted)
            {
                m_Ghosted = HandsShown;
                foreach (var r in m_ModelRenderers)
                {
                    if (r == null) continue;
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = Look(mats[i]);
                    r.sharedMaterials = mats;
                }
            }
        }

        /// <summary>The material a slot should wear now: a ghosted copy while the hands are drawn (the onboarding glow stays solid).</summary>
        Material Look(Material m)
        {
            if (m == null || m == m_ButtonLit) return m;
            if (!m_Ghosted) return m_SolidOf.TryGetValue(m, out var solid) ? solid : m;
            if (m_SolidOf.ContainsKey(m)) return m;
            if (!m_GhostOf.TryGetValue(m, out var ghost))
            {
                var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                ghost = new Material(Mats.Glass(new Color(Mathf.Lerp(c.r, 0.8f, 0.5f), Mathf.Lerp(c.g, 0.88f, 0.5f), Mathf.Lerp(c.b, 0.94f, 0.5f), 0.3f), 0.7f))
                    { name = m.name + "_Ghost" };
                m_GhostOf[m] = ghost;
                m_SolidOf[ghost] = m;
            }
            return ghost;
        }

        Callout MakeCallout(string control)
        {
            float s = Side == HandSide.Left ? 1f : -1f;
            var anchor = m_Controls[control];
            var root = new GameObject("Callout_" + control);
            root.transform.SetParent(transform, false);
            var text = TextFactory.World(root.transform, "", 0.0095f, new Color(0.96f, 0.94f, 0.88f), TextAlignmentOptions.Center, false, 0.16f);
            text.outlineWidth = 0.18f;
            text.outlineColor = new Color32(10, 12, 14, 255);
            var line = new GameObject("Line").AddComponent<LineRenderer>();
            line.transform.SetParent(root.transform, false);
            line.useWorldSpace = true;
            line.widthMultiplier = 0.0012f;
            line.positionCount = 2;
            line.material = Mats.Unlit(new Color(0.9f, 0.9f, 0.85f, 0.8f), null, true);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Vector3 offset;
            switch (control)
            {
                // Beside the handle on the outer side (the grip's label has the inner side). Ahead of and below the head, where
                // it used to be, the body hid it whenever the face plate was turned to the eye.
                case "trigger": offset = new Vector3(-0.1f * s, 0f, -0.06f); break;
                case "grip": offset = new Vector3(0.07f * s, -0.04f, -0.02f); break;
                case "thumbstick": offset = new Vector3(-0.07f * s, 0.05f, 0.03f); break;
                case "secondaryButton": offset = new Vector3(0.07f * s, 0.06f, 0.04f); break;
                case "primaryButton": offset = new Vector3(0.08f * s, 0.03f, 0.0f); break;
                case "menu": offset = new Vector3(-0.07f * s, 0.035f, -0.01f); break;
                default: offset = new Vector3(0, 0.06f, 0); break;
            }
            return new Callout { Root = root, Text = text, Line = line, Anchor = anchor, Offset = offset };
        }

        void LateUpdate()
        {
            var rig = Game.Rig;
            if (rig == null) return;
            var head = rig.Head;

            // Auto labels when the player looks at a raised controller.
            bool inspecting = Vector3.Distance(transform.position, head.position) < 0.42f &&
                              Vector3.Dot(head.forward, (transform.position - head.position).normalized) > 0.75f;
            if (inspecting && m_Highlighted.Count == 0 && Game.Input != null && !Game.InMenu) ShowBindingLabels();
            else if (!inspecting && m_Highlighted.Count == 0 && m_AutoShown) { ClearCallouts(); m_AutoShown = false; }
            ApplyHandsMode();

            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
            foreach (var kv in m_Callouts)
            {
                var c = kv.Value;
                if (!c.Root.activeSelf) continue;
                var p = c.Anchor.position;
                var labelPos = transform.TransformPoint(transform.InverseTransformPoint(p) + c.Offset);
                c.Root.transform.position = labelPos;
                c.Text.transform.rotation = Quaternion.LookRotation(labelPos - head.position, Vector3.up);
                c.Line.SetPosition(0, p);
                c.Line.SetPosition(1, labelPos - (labelPos - p).normalized * 0.012f);
                c.Text.alpha = m_Highlighted.Contains(kv.Key) ? pulse : 1f;
            }
        }

        bool m_AutoShown;

        void ShowBindingLabels()
        {
            if (m_AutoShown) return;
            m_AutoShown = true;
            var labels = new Dictionary<string, List<string>>();
            foreach (Intent intent in System.Enum.GetValues(typeof(Intent)))
                foreach (var c in Game.Input.XRControls(intent))
                {
                    if (c.Hand != Side) continue;
                    if (!labels.TryGetValue(c.Control, out var list)) labels[c.Control] = list = new List<string>();
                    if (!list.Contains(c.Label)) list.Add(c.Label);
                }
            foreach (var kv in labels) SetCallout(kv.Key, string.Join(" / ", kv.Value), false);
        }
    }
}
