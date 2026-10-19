using System.Collections;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// New York's arcade game ("Among Us, super simple"): the harbour terminal's arrival hall is a space-station arcade.
    /// Three chunky cartoon crewmates stand on pads in front of the ARRIVALS gate, each with a big VOTE button in front of
    /// it. Two tiny tasks at two consoles (Fix wiring: press three coloured buttons in the order its little screen shows;
    /// Swipe card: put the crew keycard in the reader) unlock the security screen over the gate, which gives the one clue
    /// that singles the impostor out. Each crewmate has one oddity (no backpack, a cracked visor, standing in green vent
    /// slime), so the clue is needed and points at exactly one of them. Voting the impostor ejects it; voting an innocent
    /// crewmate is reported (the encounter sets the figure on the player) and the vote can be cast again.
    /// </summary>
    public class ImpostorGame : MonoBehaviour
    {
        public enum Oddity { NoBackpack, CrackedVisor, VentSlime }
        public const int CrewCount = 3;
        public static readonly string[] Names = { "RED", "BLUE", "YELLOW" };
        static readonly Color[] Suits = { new Color(0.86f, 0.12f, 0.1f), new Color(0.18f, 0.34f, 0.95f), new Color(0.98f, 0.84f, 0.16f) };
        public static readonly string[] WireNames = { "RED", "YELLOW", "BLUE" };
        static readonly Color[] WireCols = { new Color(1f, 0.16f, 0.12f), new Color(1f, 0.84f, 0.12f), new Color(0.22f, 0.5f, 1f) };
        public static readonly float[] CrewX = { 0.7f, 2.2f, 3.7f };
        public const float CrewZ = 4.9f, VoteZ = 3.95f, VoteY = 0.96f;
        public static readonly Vector3 WiringAt = new Vector3(-0.5f, 0f, 1.3f), CardAt = new Vector3(4.0f, 0f, 1.3f);
        static readonly Vector3 ScreenAt = new Vector3(2.2f, 3.42f, 6.0f);

        public readonly Crewmate[] Crew = new Crewmate[CrewCount];
        public readonly PushButton[] Votes = new PushButton[CrewCount];
        public readonly PushButton[] WireButtons = new PushButton[3];   // red, yellow, blue, left to right
        public readonly int[] WireOrder = { 0, 1, 2 };                  // the wire colour to press first, second, third
        public Grabbable Keycard;
        public Socket Reader;
        public int Impostor { get; private set; }
        public Oddity Clue { get; private set; }
        public int WireStep { get; private set; }
        public bool WiringDone { get; private set; }
        public bool CardDone { get; private set; }
        public bool TasksDone => WiringDone && CardDone;
        public bool Busy { get; private set; }
        public bool Ejected { get; private set; }
        public int WrongVotes { get; private set; }
        public int LockedPresses { get; private set; }
        public int WrongWires { get; private set; }
        public string ScreenText => m_Screen != null ? m_Screen.text : "";

        // Where things are, for the guide and the tests.
        public Vector3 WiringPosition => transform.TransformPoint(WiringAt + new Vector3(0f, 1.0f, -0.1f));
        public Vector3 CardConsolePosition => transform.TransformPoint(CardAt + new Vector3(0f, 1.0f, 0f));
        public Vector3 KeycardPosition => Keycard != null ? Keycard.transform.position : CardConsolePosition;
        public Vector3 ReaderPosition => Reader != null ? Reader.transform.position : CardConsolePosition;
        public Vector3 ScreenPosition => transform.TransformPoint(ScreenAt);
        public Vector3 VotePosition(int i) => Votes[i] != null ? Votes[i].transform.position : transform.position;

        public event System.Action<ImpostorGame, string> TaskDone;   // "wiring" or "card"
        public event System.Action<ImpostorGame, int, bool> Voted;   // which crewmate, and whether it was the impostor
        public event System.Action<ImpostorGame> ImpostorGone;       // the ejection has played out

        TMPro.TextMeshPro m_Screen, m_WireStatus, m_ReaderText;
        Material m_ScreenMat, m_VoteOn, m_VoteOff, m_LedRed, m_LedGreen;
        readonly Renderer[] m_VoteCaps = new Renderer[CrewCount];
        readonly Material[] m_OrderMats = new Material[3];
        readonly TMPro.TextMeshPro[] m_OrderNums = new TMPro.TextMeshPro[3];
        readonly GameObject[] m_WireLoose = new GameObject[3], m_WireFixed = new GameObject[3];
        Transform m_CardAttach;
        Vector3 m_AttachUp, m_AttachDown;
        Renderer m_Led;
        float m_WireFlash;

        public static ImpostorGame Build(Transform parent)
        {
            var go = new GameObject("SpaceStationArcade");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<ImpostorGame>();
            g.Construct();
            return g;
        }

        // ------------------------------------------------------------------ construction
        void Construct()
        {
            var t = transform;
            var dark = Mats.Lit(new Color(0.07f, 0.08f, 0.11f), 0.6f, 0.4f);
            var panel = Mats.Lit(new Color(0.55f, 0.6f, 0.7f), 0.55f, 0.6f, "metal_brushed");
            var navy = Mats.Lit(new Color(0.05f, 0.07f, 0.16f), 0.5f, 0.3f);
            var cyan = Mats.Emissive(new Color(0.3f, 0.95f, 1f), new Color(0.6f, 2.4f, 3.0f));
            var magenta = Mats.Emissive(new Color(1f, 0.3f, 0.85f), new Color(2.6f, 0.6f, 2.2f));
            m_VoteOff = Mats.Lit(new Color(0.32f, 0.3f, 0.3f), 0.4f);
            m_VoteOn = Mats.Emissive(new Color(1f, 0.16f, 0.1f), new Color(2.4f, 0.28f, 0.16f));
            m_LedRed = Mats.Emissive(new Color(1f, 0.15f, 0.1f), new Color(2.5f, 0.2f, 0.1f));
            m_LedGreen = Mats.Emissive(new Color(0.3f, 1f, 0.35f), new Color(0.4f, 2.6f, 0.5f));
            var k = new KitBatch();

            // The stage: three pads in front of the ARRIVALS gate, a neon outline on the floor, a VOTE pedestal per crewmate.
            k.Box(cyan, new Vector3(2.2f, 0.004f, 3.55f), new Vector3(4.6f, 0.006f, 0.05f));
            k.Box(cyan, new Vector3(2.2f, 0.004f, 5.75f), new Vector3(4.6f, 0.006f, 0.05f));
            k.Box(cyan, new Vector3(-0.1f, 0.004f, 4.65f), new Vector3(0.05f, 0.006f, 2.2f));
            k.Box(cyan, new Vector3(4.5f, 0.004f, 4.65f), new Vector3(0.05f, 0.006f, 2.2f));
            for (int i = 0; i < CrewCount; i++)
            {
                Crew[i] = Crewmate.Build(t, new Vector3(CrewX[i], 0f, CrewZ), Suits[i], Names[i], i * 2.1f);
                var ped = new Vector3(CrewX[i], 0f, VoteZ);
                k.Box(dark, ped + new Vector3(0f, 0.47f, 0f), new Vector3(0.3f, 0.94f, 0.3f));
                k.Box(panel, ped + new Vector3(0f, 0.94f, 0f), new Vector3(0.36f, 0.04f, 0.36f));
                k.Box(cyan, ped + new Vector3(0f, 0.92f, -0.181f), new Vector3(0.36f, 0.015f, 0.01f));
                Solid(t, ped + new Vector3(0f, 0.48f, 0f), new Vector3(0.36f, 0.96f, 0.36f), Layers.Default);
                var plate = TextFactory.World(t, Names[i], 0.06f, Suits[i] * 1.2f, TMPro.TextAlignmentOptions.Center, false, 0.3f);
                plate.fontStyle = TMPro.FontStyles.Bold;
                plate.transform.localPosition = ped + new Vector3(0f, 0.7f, -0.153f);
                int id = i;
                Votes[i] = MakeButton(t, ped + new Vector3(0f, VoteY, 0f), 0.08f, m_VoteOff, "VOTE", "Vote " + Names[i] + " out", out m_VoteCaps[i]);
                Votes[i].Sound = "button_press";
                Votes[i].Pressed += _ => Vote(id);
            }

            BuildWiring(t, k, dark, panel, cyan);
            BuildCardReader(t, k, dark, panel, cyan);
            BuildScreen(t, k, dark, cyan);
            BuildDecor(t, k, navy, cyan, magenta);
            k.Bake(t, "ArcadeDetail");
            ResetGame();
        }

        void BuildWiring(Transform t, KitBatch k, Material dark, Material panel, Material cyan)
        {
            var c = WiringAt;
            k.Box(dark, c + new Vector3(0f, 0.48f, 0f), new Vector3(0.8f, 0.96f, 0.46f));
            k.Box(panel, c + new Vector3(0f, 0.98f, 0f), new Vector3(0.86f, 0.04f, 0.52f));
            k.Box(cyan, c + new Vector3(0f, 1.0f, -0.262f), new Vector3(0.86f, 0.015f, 0.015f));
            Solid(t, c + new Vector3(0f, 0.5f, 0f), new Vector3(0.86f, 1.0f, 0.52f), Layers.Default);
            // The little screen at the back of the top: three coloured squares in the order to press them.
            k.Box(dark, c + new Vector3(0f, 1.17f, 0.2f), new Vector3(0.56f, 0.3f, 0.05f));
            MeshFactory.Quad("WireScreen", t, 0.5f, 0.25f, Mats.Unlit(new Color(0.04f, 0.08f, 0.16f), null, false, 1f), c + new Vector3(0f, 1.17f, 0.173f));
            for (int s = 0; s < 3; s++)
            {
                m_OrderMats[s] = Mats.UnlitInstance(WireCols[s], null, false, 1.2f);
                MeshFactory.Quad("Order" + s, t, 0.1f, 0.08f, m_OrderMats[s], c + new Vector3(-0.15f + s * 0.15f, 1.15f, 0.171f));
                m_OrderNums[s] = TextFactory.World(t, (s + 1).ToString(), 0.028f, Color.white, TMPro.TextAlignmentOptions.Center, false, 0.12f);
                m_OrderNums[s].transform.localPosition = c + new Vector3(-0.15f + s * 0.15f, 1.235f, 0.17f);
            }
            m_WireStatus = TextFactory.World(t, "PRESS IN THIS ORDER", 0.018f, new Color(0.7f, 0.95f, 1f), TMPro.TextAlignmentOptions.Center, false, 0.48f);
            m_WireStatus.transform.localPosition = c + new Vector3(0f, 1.085f, 0.17f);
            var head = TextFactory.World(t, "TASK 1: FIX WIRING", 0.04f, new Color(0.55f, 1f, 1f), TMPro.TextAlignmentOptions.Center, false, 0.8f);
            head.fontStyle = TMPro.FontStyles.Bold;
            head.transform.localPosition = c + new Vector3(0f, 1.38f, 0.2f);
            k.Box(dark, c + new Vector3(0f, 1.38f, 0.23f), new Vector3(0.78f, 0.1f, 0.03f));
            // The wire panel on the front: three loose wires that join up as the buttons are pressed in order.
            k.Box(dark, c + new Vector3(0f, 0.6f, -0.236f), new Vector3(0.64f, 0.44f, 0.01f));
            for (int w = 0; w < 3; w++)
            {
                float y = 0.74f - w * 0.14f;
                var wm = Mats.Lit(WireCols[w], 0.5f, 0f, null, false, WireCols[w] * 0.35f);
                k.Box(wm, c + new Vector3(-0.27f, y, -0.248f), new Vector3(0.04f, 0.05f, 0.025f));
                k.Box(wm, c + new Vector3(0.27f, y, -0.248f), new Vector3(0.04f, 0.05f, 0.025f));
                m_WireFixed[w] = MeshFactory.Box("WireFixed" + w, t, c + new Vector3(0f, y, -0.252f), new Vector3(0.52f, 0.022f, 0.022f), wm, false);
                var loose = new GameObject("WireLoose" + w);
                loose.transform.SetParent(t, false);
                MeshFactory.Box("L", loose.transform, c + new Vector3(-0.19f, y - 0.045f, -0.252f), new Vector3(0.17f, 0.02f, 0.02f), wm, false, 1f, Quaternion.Euler(0f, 0f, -32f));
                MeshFactory.Box("R", loose.transform, c + new Vector3(0.19f, y - 0.045f, -0.252f), new Vector3(0.17f, 0.02f, 0.02f), wm, false, 1f, Quaternion.Euler(0f, 0f, 32f));
                m_WireLoose[w] = loose;
                int id = w;
                WireButtons[w] = MakeButton(t, c + new Vector3(-0.2f + w * 0.2f, 1.0f, -0.1f), 0.05f, Mats.Emissive(WireCols[w], WireCols[w] * 1.4f), "", "Press the " + WireNames[w].ToLowerInvariant() + " wire button", out _);
                WireButtons[w].Sound = "dial_click";
                WireButtons[w].Pressed += _ => PressWire(id);
            }
        }

        void BuildCardReader(Transform t, KitBatch k, Material dark, Material panel, Material cyan)
        {
            var c = CardAt;
            k.Box(dark, c + new Vector3(0f, 0.48f, 0f), new Vector3(0.8f, 0.96f, 0.46f));
            k.Box(panel, c + new Vector3(0f, 0.98f, 0f), new Vector3(0.86f, 0.04f, 0.52f));
            k.Box(cyan, c + new Vector3(0f, 1.0f, -0.262f), new Vector3(0.86f, 0.015f, 0.015f));
            Solid(t, c + new Vector3(0f, 0.5f, 0f), new Vector3(0.86f, 1.0f, 0.52f), Layers.Default);
            var head = TextFactory.World(t, "TASK 2: SWIPE CARD", 0.04f, new Color(0.55f, 1f, 1f), TMPro.TextAlignmentOptions.Center, false, 0.8f);
            head.fontStyle = TMPro.FontStyles.Bold;
            head.transform.localPosition = c + new Vector3(0f, 1.38f, 0.2f);
            k.Box(dark, c + new Vector3(0f, 1.38f, 0.23f), new Vector3(0.78f, 0.1f, 0.03f));
            // The reader: a dark box with a slot on top, a status LED and a little display on its face.
            var reader = c + new Vector3(0.14f, 0f, 0.06f);
            k.Box(dark, reader + new Vector3(0f, 1.1f, 0f), new Vector3(0.22f, 0.2f, 0.14f));
            k.Box(Mats.Lit(new Color(0.01f, 0.01f, 0.01f), 0.2f), reader + new Vector3(0f, 1.202f, 0f), new Vector3(0.13f, 0.008f, 0.016f));
            k.Box(cyan, reader + new Vector3(0f, 1.201f, -0.072f), new Vector3(0.22f, 0.006f, 0.006f));
            m_Led = MeshFactory.Sphere("ReaderLed", t, reader + new Vector3(-0.07f, 1.16f, -0.072f), 0.016f, m_LedRed).GetComponent<Renderer>();
            m_ReaderText = TextFactory.World(t, "SWIPE\nCARD", 0.017f, new Color(1f, 0.55f, 0.45f), TMPro.TextAlignmentOptions.Center, false, 0.14f);
            m_ReaderText.transform.localPosition = reader + new Vector3(0.025f, 1.085f, -0.073f);
            var sock = new GameObject("CardReader");
            sock.transform.SetParent(t, false);
            sock.transform.localPosition = reader + new Vector3(0f, 1.27f, 0f);
            var sc = sock.AddComponent<BoxCollider>();
            sc.isTrigger = true;
            sc.size = new Vector3(0.22f, 0.16f, 0.16f);
            m_CardAttach = new GameObject("Slot").transform;
            m_CardAttach.SetParent(sock.transform, false);
            m_AttachUp = new Vector3(0f, -0.036f, 0f);
            m_AttachDown = new Vector3(0f, -0.062f, 0f);
            m_CardAttach.localPosition = m_AttachUp;
            m_CardAttach.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Reader = sock.AddComponent<Socket>();
            Reader.Prompt = "Swipe the keycard here";
            Reader.Accepts.Add("crew_keycard");
            Reader.Attach = m_CardAttach;
            Reader.LockWhenFilled = true;
            Reader.Filled += OnCardIn;
            // The keycard, lying in a tray beside the reader.
            k.Box(dark, c + new Vector3(-0.18f, 1.006f, -0.05f), new Vector3(0.16f, 0.012f, 0.12f));
            var card = new GameObject("CrewKeycard");
            card.transform.SetParent(t, false);
            card.transform.localPosition = c + new Vector3(-0.18f, 1.016f, -0.05f);
            MeshFactory.Box("Card", card.transform, Vector3.zero, new Vector3(0.086f, 0.004f, 0.054f), Mats.Lit(new Color(0.92f, 0.95f, 1f), 0.6f), false);
            MeshFactory.Box("Stripe", card.transform, new Vector3(0f, 0.0025f, 0.012f), new Vector3(0.086f, 0.0012f, 0.012f), Mats.Lit(new Color(0.05f, 0.05f, 0.06f), 0.5f), false);
            MeshFactory.Box("Chip", card.transform, new Vector3(-0.022f, 0.0025f, -0.01f), new Vector3(0.016f, 0.0012f, 0.013f), Mats.Lit(new Color(0.95f, 0.75f, 0.3f), 0.8f, 0.9f), false);
            MeshFactory.Box("Badge", card.transform, new Vector3(0.022f, 0.0025f, -0.008f), new Vector3(0.03f, 0.0012f, 0.02f), cyan, false);
            var cc = card.AddComponent<BoxCollider>();
            cc.size = new Vector3(0.13f, 0.04f, 0.1f);
            cc.center = new Vector3(0f, 0.01f, 0f);
            Keycard = PropLibrary.MakeGrabbable(card, "crew_keycard", "Take the crew keycard");
            Keycard.SetHome();
        }

        void BuildScreen(Transform t, KitBatch k, Material dark, Material cyan)
        {
            // The security screen hangs over the ARRIVALS sign, facing the hall.
            k.Box(dark, ScreenAt, new Vector3(2.12f, 0.88f, 0.08f));
            foreach (var y in new[] { -0.45f, 0.45f }) k.Box(cyan, ScreenAt + new Vector3(0f, y, -0.042f), new Vector3(2.16f, 0.02f, 0.02f));
            foreach (var x in new[] { -1.07f, 1.07f }) k.Box(cyan, ScreenAt + new Vector3(x, 0f, -0.042f), new Vector3(0.02f, 0.9f, 0.02f));
            foreach (var x in new[] { -0.7f, 0.7f }) k.Box(dark, ScreenAt + new Vector3(x, 0.54f, 0.02f), new Vector3(0.03f, 0.2f, 0.03f));
            m_ScreenMat = Mats.UnlitInstance(new Color(0.04f, 0.07f, 0.16f), null, false, 1f);
            MeshFactory.Quad("SecurityScreen", t, 2.0f, 0.76f, m_ScreenMat, ScreenAt + new Vector3(0f, 0f, -0.043f));
            m_Screen = TextFactory.World(t, "", 0.11f, Color.white, TMPro.TextAlignmentOptions.Center, false, 1.9f);
            m_Screen.rectTransform.sizeDelta = new Vector2(1.9f, 0.72f);
            m_Screen.transform.localPosition = ScreenAt + new Vector3(0f, 0f, -0.047f);
        }

        void BuildDecor(Transform t, KitBatch k, Material navy, Material cyan, Material magenta)
        {
            // SPACE STATION ARCADE, hung from the canopy beam over the walk (readable from the arrival).
            var s = new Vector3(1.9f, 3.15f, 0.9f);
            k.Box(navy, s, new Vector3(3.9f, 0.62f, 0.06f));
            foreach (var y in new[] { -0.31f, 0.31f }) k.Box(magenta, s + new Vector3(0f, y, -0.035f), new Vector3(3.96f, 0.025f, 0.025f));
            foreach (var x in new[] { -1.97f, 1.97f }) k.Box(magenta, s + new Vector3(x, 0f, -0.035f), new Vector3(0.025f, 0.64f, 0.025f));
            foreach (var x in new[] { -1.5f, 1.5f }) k.Box(Mats.Lit(new Color(0.6f, 0.62f, 0.66f), 0.6f, 0.7f), s + new Vector3(x, 0.53f, 0f), new Vector3(0.02f, 0.44f, 0.02f));
            var title = TextFactory.World(t, "SPACE STATION ARCADE", 0.19f, new Color(0.55f, 1f, 1f), TMPro.TextAlignmentOptions.Center, false, 3.3f);
            title.fontStyle = TMPro.FontStyles.Bold;
            title.transform.localPosition = s + new Vector3(0.12f, 0f, -0.035f);
            // A ringed planet and a few stars on the sign.
            var planet = Mats.Emissive(new Color(1f, 0.55f, 0.2f), new Color(1.8f, 0.8f, 0.25f));
            k.Of(planet).AddSphere(s + new Vector3(-1.68f, 0f, -0.06f), 0.15f, 14, 8);
            k.Of(cyan).AddTorus(s + new Vector3(-1.68f, 0f, -0.06f), 0.24f, 0.012f, 24, 5, Quaternion.Euler(70f, 0f, 20f));
            foreach (var p in new[] { new Vector2(-1.35f, 0.18f), new Vector2(1.78f, -0.15f), new Vector2(1.6f, 0.2f), new Vector2(-1.3f, -0.2f) })
                k.Of(magenta).AddSphere(s + new Vector3(p.x, p.y, -0.05f), 0.025f, 8, 5);
            // Neon tubes on the canopy columns and under the fascia, so the whole hall reads as an arcade.
            foreach (var z in new[] { -1f, 1.8f, 4.6f, 7.4f })
            {
                k.Box(z < 3f ? magenta : cyan, new Vector3(5.32f, 2.0f, z - 0.19f), new Vector3(0.035f, 3.2f, 0.035f));
                if (Mathf.Abs(z - 1.8f) > 0.1f) k.Box(z < 3f ? cyan : magenta, new Vector3(-5.32f, 2.0f, z - 0.19f), new Vector3(0.035f, 3.2f, 0.035f));
            }
            k.Box(magenta, new Vector3(0f, 3.735f, -1.645f), new Vector3(12.2f, 0.03f, 0.03f));
            k.Box(cyan, new Vector3(6.14f, 3.735f, 3.0f), new Vector3(0.03f, 0.03f, 9.2f));
        }

        PushButton MakeButton(Transform parent, Vector3 at, float r, Material capMat, string label, string prompt, out Renderer cap)
        {
            var go = new GameObject("Button_" + (label.Length > 0 ? label : prompt));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var capGo = MeshFactory.Cylinder("Cap", go.transform, Vector3.zero, r, 0.04f, capMat, 20, false);
            cap = capGo.GetComponent<Renderer>();
            MeshFactory.Cylinder("Collar", go.transform, new Vector3(0f, -0.012f, 0f), r * 1.28f, 0.016f, Mats.Lit(new Color(0.75f, 0.77f, 0.8f), 0.7f, 0.8f), 20, false);
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(r * 2.4f, 0.08f, r * 2.4f);
            col.center = new Vector3(0f, 0.025f, 0f);
            if (label.Length > 0)
            {
                var txt = TextFactory.World(capGo.transform, label, 0.03f, Color.white, TMPro.TextAlignmentOptions.Center, false, 0.2f);
                txt.fontStyle = TMPro.FontStyles.Bold;
                txt.transform.localPosition = new Vector3(0f, 0.042f, 0f);
                txt.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            var b = go.AddComponent<PushButton>();
            b.Prompt = prompt;
            b.Cap = capGo.transform;
            b.PressOffset = new Vector3(0f, -0.014f, 0f);
            b.MaxDistance = 2.4f;
            return b;
        }

        static void Solid(Transform parent, Vector3 center, Vector3 size, int layer)
        {
            var go = new GameObject("Solid");
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        // ------------------------------------------------------------------ the game
        public static string ClueShort(Oddity o) => o == Oddity.NoBackpack ? "NO BACKPACK" : o == Oddity.CrackedVisor ? "a CRACKED VISOR" : "its feet in GREEN VENT SLIME";

        /// <summary>The clue on the security screen: a lead-in, the keyword in big letters, and why.</summary>
        public static string ClueLead(Oddity o) => o == Oddity.NoBackpack ? "The impostor has" : o == Oddity.CrackedVisor ? "The impostor's visor is" : "The impostor stands in";
        public static string ClueWord(Oddity o) => o == Oddity.NoBackpack ? "NO BACKPACK" : o == Oddity.CrackedVisor ? "CRACKED" : "VENT SLIME";
        public static string ClueWhy(Oddity o) => o == Oddity.NoBackpack ? "it left its backpack in a vent" : o == Oddity.CrackedVisor ? "it smashed it in a vent" : "it just crawled out of a vent";

        /// <summary>A fresh round: a new impostor and clue, each crewmate with one oddity, the tasks undone.</summary>
        public void ResetGame()
        {
            StopAllCoroutines();
            Busy = false;
            Ejected = false;
            WrongVotes = 0;
            LockedPresses = 0;
            WrongWires = 0;
            Impostor = Random.Range(0, CrewCount);
            Clue = (Oddity)Random.Range(0, 3);
            // The two innocent crewmates get the two other oddities, so every one of them looks a little off.
            var others = new System.Collections.Generic.List<Oddity> { Oddity.NoBackpack, Oddity.CrackedVisor, Oddity.VentSlime };
            others.Remove(Clue);
            if (Random.value < 0.5f) others.Reverse();
            int n = 0;
            for (int i = 0; i < CrewCount; i++)
            {
                Crew[i].SetOddity(i == Impostor ? Clue : others[n++]);
                Crew[i].Restore();
                Votes[i].Usable = true;
            }
            // Wiring: a new order, every wire loose.
            for (int i = 0; i < 3; i++) WireOrder[i] = i;
            for (int i = 2; i > 0; i--) { int j = Random.Range(0, i + 1); (WireOrder[i], WireOrder[j]) = (WireOrder[j], WireOrder[i]); }
            WireStep = 0;
            WiringDone = false;
            ShowWires();
            for (int i = 0; i < 3; i++) WireButtons[i].Usable = true;
            // The keycard back in its tray.
            CardDone = false;
            if (Keycard != null) { if (Keycard.Body != null) Keycard.ReturnHome(); Keycard.Usable = true; } // no Body before the cell first wakes
            if (m_CardAttach != null) m_CardAttach.localPosition = m_AttachUp;
            if (m_Led != null) m_Led.sharedMaterial = m_LedRed;
            if (m_ReaderText != null) { m_ReaderText.text = "SWIPE\nCARD"; m_ReaderText.color = new Color(1f, 0.55f, 0.45f); }
            RefreshVoteCaps();
            RefreshScreen();
        }

        /// <summary>The game was won on an earlier visit: tasks done, the impostor gone, nothing left to press.</summary>
        public void SetSolved()
        {
            ResetGame();
            WiringDone = true;
            WireStep = 3;
            ShowWires();
            CardDone = true;
            if (m_Led != null) m_Led.sharedMaterial = m_LedGreen;
            if (m_ReaderText != null) m_ReaderText.text = "ACCESS\nGRANTED";
            Ejected = true;
            Crew[Impostor].HideGone();
            foreach (var b in Votes) b.Usable = false;
            foreach (var b in WireButtons) b.Usable = false;
            if (Keycard != null) Keycard.Usable = false;
            RefreshVoteCaps();
            m_Screen.text = "<size=70%>STATION SECURITY</size>\n<color=#7dffb0><b>IMPOSTOR EJECTED</b></color>\n<size=65%>The station is safe.</size>";
            m_ScreenMat.SetColor("_Color", new Color(0.03f, 0.14f, 0.08f));
        }

        void PressWire(int colour)
        {
            if (WiringDone || Busy) return;
            if (WireOrder[WireStep] == colour)
            {
                WireStep++;
                AudioDirector.I?.Play("dial_click", WireButtons[colour].transform.position, 0.8f, 1.2f + WireStep * 0.12f);
                if (WireStep >= 3)
                {
                    WiringDone = true;
                    foreach (var b in WireButtons) b.Usable = false;
                    AudioDirector.I?.Play("lever_clunk", WiringPosition, 0.7f, 1.2f);
                    AudioDirector.I?.Play("coin_token", WiringPosition, 0.6f, 1.3f);
                }
                ShowWires();
                if (WiringDone) TaskDone?.Invoke(this, "wiring");
                RefreshScreen();
            }
            else
            {
                WireStep = 0;
                WrongWires++;
                m_WireFlash = 1.2f;
                AudioDirector.I?.Play("dial_click", WireButtons[colour].transform.position, 1f, 0.45f);
                AudioDirector.I?.Play("drop_hard", WiringPosition, 0.35f, 1.6f);
                Hud.I?.ShowToast("Wrong order: the wires spark loose. Press them in the order on the little screen.", 2.5f);
                ShowWires();
            }
        }

        void ShowWires()
        {
            for (int s = 0; s < 3; s++)
            {
                int w = WireOrder[s];
                bool fixedNow = s < WireStep;
                m_WireFixed[w].SetActive(fixedNow);
                m_WireLoose[w].SetActive(!fixedNow);
                m_OrderMats[s].SetColor("_Color", WireCols[w] * (fixedNow ? 0.45f : 1f));
                m_OrderNums[s].text = fixedNow ? "OK" : (s + 1).ToString();
            }
            if (m_WireStatus != null)
            {
                m_WireStatus.text = WiringDone ? "WIRING FIXED" : "PRESS IN THIS ORDER";
                m_WireStatus.color = WiringDone ? new Color(0.5f, 1f, 0.6f) : new Color(0.7f, 0.95f, 1f);
            }
        }

        void OnCardIn(Socket s, Grabbable g)
        {
            if (CardDone) return;
            StartCoroutine(SwipeRoutine());
        }

        IEnumerator SwipeRoutine()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                m_CardAttach.localPosition = Vector3.Lerp(m_AttachUp, m_AttachDown, t);
                yield return null;
            }
            m_CardAttach.localPosition = m_AttachDown;
            CardDone = true;
            m_Led.sharedMaterial = m_LedGreen;
            m_ReaderText.text = "ACCESS\nGRANTED";
            m_ReaderText.color = new Color(0.5f, 1f, 0.6f);
            AudioDirector.I?.Play("button_press", ReaderPosition, 0.8f, 1.6f);
            AudioDirector.I?.Play("coin_token", ReaderPosition, 0.6f, 1.3f);
            TaskDone?.Invoke(this, "card");
            RefreshScreen();
        }

        void Vote(int i)
        {
            if (Busy || Ejected || Crew[i].Gone) return;
            if (!TasksDone)
            {
                LockedPresses++;
                AudioDirector.I?.Play("dial_click", VotePosition(i), 1f, 0.4f);
                Hud.I?.ShowToast("Voting is locked. Finish both station tasks first: " + (WiringDone ? "" : "Fix wiring") + (!WiringDone && !CardDone ? " and " : "") + (CardDone ? "" : "Swipe card") + ".", 3f);
                return;
            }
            StartCoroutine(VoteRoutine(i));
        }

        IEnumerator VoteRoutine(int i)
        {
            Busy = true;
            m_Screen.text = "<size=70%>EMERGENCY VOTE</size>\n<size=130%><b>" + Names[i] + "</b></size>\n...";
            m_ScreenMat.SetColor("_Color", new Color(0.12f, 0.08f, 0.02f));
            AudioDirector.I?.Play("clock_tick", ScreenPosition, 0.8f);
            yield return new WaitForSeconds(0.8f);
            if (i == Impostor)
            {
                Ejected = true;
                foreach (var b in Votes) b.Usable = false;
                RefreshVoteCaps();
                m_Screen.text = "<size=70%>EMERGENCY VOTE</size>\n<size=130%><color=#7dffb0><b>" + Names[i] + "</b></color></size>\n<size=85%><color=#7dffb0><b>WAS THE IMPOSTOR</b></color></size>\n<size=65%>Ejected into space.</size>";
                m_ScreenMat.SetColor("_Color", new Color(0.03f, 0.14f, 0.08f));
                AudioDirector.I?.Play("pass_sting", Crew[i].transform.position + Vector3.up, 0.9f, 1.15f, Bus.SFX, true);
                AudioDirector.I?.Play("door_open", Crew[i].transform.position, 0.8f, 0.8f);
                Voted?.Invoke(this, i, true);
                yield return Crew[i].Eject();
                Busy = false;
                ImpostorGone?.Invoke(this);
                yield break;
            }
            WrongVotes++;
            m_Screen.text = "<size=70%>EMERGENCY VOTE</size>\n<size=130%><color=#ff6a5a><b>" + Names[i] + "</b></color></size>\n<size=80%><color=#ff6a5a><b>WAS NOT THE IMPOSTOR</b></color></size>\n<size=65%>Something heard the vote.</size>";
            m_ScreenMat.SetColor("_Color", new Color(0.2f, 0.03f, 0.03f));
            AudioDirector.I?.Play("dial_click", VotePosition(i), 1f, 0.35f);
            Crew[i].Innocent();
            Voted?.Invoke(this, i, false);
            yield return new WaitForSeconds(2.2f);
            Busy = false;
            RefreshScreen();
        }

        void RefreshVoteCaps()
        {
            for (int i = 0; i < CrewCount; i++)
                if (m_VoteCaps[i] != null) m_VoteCaps[i].sharedMaterial = TasksDone && !Ejected ? m_VoteOn : m_VoteOff;
            for (int i = 0; i < CrewCount; i++)
                if (Votes[i] != null) Votes[i].Prompt = TasksDone ? "Vote " + Names[i] + " out" : "Vote " + Names[i] + " out (locked: do the two tasks)";
        }

        void RefreshScreen()
        {
            RefreshVoteCaps();
            if (m_Screen == null || Ejected) return;
            if (!TasksDone)
            {
                m_Screen.text = "<size=70%>STATION SECURITY CAM</size>\n<size=110%><color=#ffb36a><b>LOCKED</b></color></size>\n<size=65%>do the two station tasks first</size>\n<size=75%>" +
                                (WiringDone ? "<color=#7dffb0>[X] FIX WIRING</color>" : "[  ] FIX WIRING") + "\n" +
                                (CardDone ? "<color=#7dffb0>[X] SWIPE CARD</color>" : "[  ] SWIPE CARD") + "</size>";
                m_ScreenMat.SetColor("_Color", new Color(0.04f, 0.07f, 0.16f));
                return;
            }
            m_Screen.text = "<size=70%>SECURITY CAM: <color=#ff6a5a>IMPOSTOR SPOTTED</color></size>\n<size=75%>" + ClueLead(Clue) + "</size>\n<size=125%><color=#ff6a5a><b>" + ClueWord(Clue) + "</b></color></size>\n<size=55%>(" + ClueWhy(Clue) + ")</size>\n<size=75%><color=#ffd76a><b>VOTE IT OUT</b></color></size>";
            m_ScreenMat.SetColor("_Color", new Color(0.16f, 0.03f, 0.05f));
        }

        void Update()
        {
            if (m_WireFlash > 0f)
            {
                m_WireFlash -= Time.deltaTime;
                bool on = m_WireFlash > 0f && Mathf.Repeat(m_WireFlash, 0.3f) > 0.15f;
                if (m_WireStatus != null)
                {
                    m_WireStatus.text = m_WireFlash > 0f ? "WRONG ORDER!" : WiringDone ? "WIRING FIXED" : "PRESS IN THIS ORDER";
                    m_WireStatus.color = on ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.6f, 0.5f);
                    if (m_WireFlash <= 0f) ShowWires();
                }
            }
        }
    }

    /// <summary>
    /// One chunky cartoon crewmate on its pad: a bean-shaped suit on stubby legs, a wide glassy visor, a backpack with an
    /// antenna (so even from the front you see whether it has one). It idles (bobs and turns to show its side), shakes its
    /// head when wrongly voted, and is ejected out over the harbour when found out.
    /// </summary>
    public class Crewmate : MonoBehaviour
    {
        public const float PadH = 0.42f;   // the riser: the VOTE pedestal in front never hides the visor
        public string Name;
        public Transform Visual;
        public GameObject Backpack, Visor, CrackedVisor, Slime;
        public ImpostorGame.Oddity Oddity { get; private set; }
        public bool Gone { get; private set; }
        Renderer m_PadRing;
        Material m_RingOn, m_RingRed, m_Blink;
        float m_Phase, m_Shake, m_Red;
        Renderer m_Bulb;

        public static Crewmate Build(Transform parent, Vector3 pos, Color suit, string name, float phase)
        {
            var root = new GameObject("Crewmate_" + name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            var c = root.AddComponent<Crewmate>();
            c.Name = name;
            c.m_Phase = phase;
            c.Construct(suit);
            return c;
        }

        void Construct(Color suit)
        {
            var t = transform;
            var padMat = Mats.Lit(new Color(0.16f, 0.18f, 0.22f), 0.65f, 0.7f, "metal_brushed");
            m_RingOn = Mats.Emissive(new Color(0.3f, 0.95f, 1f), new Color(0.6f, 2.4f, 3.0f));
            m_RingRed = Mats.Emissive(new Color(1f, 0.15f, 0.1f), new Color(3f, 0.3f, 0.2f));
            MeshFactory.Cylinder("Pad", t, Vector3.zero, 0.5f, PadH, padMat, 28, false);
            MeshFactory.Cylinder("PadBase", t, Vector3.zero, 0.56f, 0.06f, padMat, 28, false);
            var ring = new MeshBuilder();
            ring.AddTorus(new Vector3(0f, PadH, 0f), 0.48f, 0.022f, 32, 6, Quaternion.identity);
            ring.AddTorus(new Vector3(0f, 0.07f, 0f), 0.54f, 0.016f, 32, 6, Quaternion.identity);
            m_PadRing = MeshFactory.FromBuilder("PadRing", ring, m_RingOn, t).GetComponent<Renderer>();
            // The body doesn't block the aim (IgnoreRaycast) but the player can't walk through it.
            var solid = new GameObject("Solid");
            solid.layer = Layers.IgnoreRaycast;
            solid.transform.SetParent(t, false);
            var bc = solid.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, (PadH + 1.2f) / 2f, 0f);
            bc.size = new Vector3(1.0f, PadH + 1.2f, 1.0f);

            Visual = new GameObject("Visual").transform;
            Visual.SetParent(t, false);
            Visual.localPosition = new Vector3(0f, PadH, 0f);
            var suitMat = Mats.Lit(suit, 0.45f, 0f, null, false, suit * 0.08f);
            var shade = Mats.Lit(suit * 0.62f, 0.4f);
            // Bean body (a lathe), stubby legs with rounded feet.
            var body = new MeshBuilder();
            body.AddLathe(new Vector3(0f, 0f, 0f), new[]
            {
                new Vector2(0.001f, 0.25f), new Vector2(0.2f, 0.26f), new Vector2(0.28f, 0.31f), new Vector2(0.315f, 0.42f), new Vector2(0.325f, 0.6f),
                new Vector2(0.315f, 0.78f), new Vector2(0.285f, 0.9f), new Vector2(0.225f, 0.99f), new Vector2(0.13f, 1.05f), new Vector2(0.001f, 1.075f),
            }, 28);
            MeshFactory.FromBuilder("Body", body, suitMat, Visual);
            foreach (var x in new[] { -0.14f, 0.14f })
            {
                var leg = new MeshBuilder();
                leg.AddLathe(new Vector3(x, 0f, 0f), new[]
                {
                    new Vector2(0.001f, 0f), new Vector2(0.08f, 0.006f), new Vector2(0.11f, 0.035f), new Vector2(0.122f, 0.09f), new Vector2(0.125f, 0.32f), new Vector2(0.001f, 0.33f),
                }, 18);
                MeshFactory.FromBuilder("Leg", leg, suitMat, Visual);
            }
            // The wide glassy visor, with a highlight.
            var visorMat = Mats.Lit(new Color(0.55f, 0.82f, 0.95f), 0.95f, 0.25f, null, false, new Color(0.05f, 0.12f, 0.17f));
            Visor = new GameObject("Visor");
            Visor.transform.SetParent(Visual, false);
            var vs = MeshFactory.Sphere("Glass", Visor.transform, new Vector3(0f, 0.74f, -0.2f), 1f, visorMat, 24, 14);
            vs.transform.localScale = new Vector3(0.25f, 0.14f, 0.17f);
            var hl = MeshFactory.Sphere("Highlight", Visor.transform, new Vector3(0.1f, 0.79f, -0.345f), 1f, Mats.Emissive(Color.white, new Color(1.5f, 1.5f, 1.5f)), 10, 6);
            hl.transform.localScale = new Vector3(0.06f, 0.026f, 0.02f);
            // The cracked, grimy visor (one crewmate's oddity).
            CrackedVisor = new GameObject("CrackedVisor");
            CrackedVisor.transform.SetParent(Visual, false);
            var cv = MeshFactory.Sphere("Glass", CrackedVisor.transform, new Vector3(0f, 0.74f, -0.2f), 1f, Mats.Lit(new Color(0.3f, 0.36f, 0.36f), 0.35f, 0.1f), 24, 14);
            cv.transform.localScale = new Vector3(0.25f, 0.14f, 0.17f);
            var crack = Mats.Lit(new Color(0.02f, 0.02f, 0.02f), 0.2f);
            var cracks = new[] { (new Vector3(-0.05f, 0.76f, -0.367f), 28f, 0.13f), (new Vector3(0.05f, 0.73f, -0.367f), -40f, 0.12f), (new Vector3(0.0f, 0.79f, -0.36f), 75f, 0.09f), (new Vector3(-0.12f, 0.72f, -0.35f), -15f, 0.08f), (new Vector3(0.12f, 0.77f, -0.348f), 55f, 0.08f) };
            foreach (var (p, a, len) in cracks)
                MeshFactory.Box("Crack", CrackedVisor.transform, p, new Vector3(len, 0.008f, 0.01f), crack, false, 1f, Quaternion.Euler(0f, 0f, a));
            var grime = MeshFactory.Sphere("Grime", CrackedVisor.transform, new Vector3(-0.08f, 0.7f, -0.335f), 1f, Mats.Lit(new Color(0.28f, 0.2f, 0.1f), 0.1f), 10, 6);
            grime.transform.localScale = new Vector3(0.08f, 0.04f, 0.03f);
            // Backpack with an antenna and a blinking bulb that shows above the head.
            Backpack = new GameObject("Backpack");
            Backpack.transform.SetParent(Visual, false);
            var pack = new MeshBuilder();
            pack.AddRoundedPlate(new Vector3(0f, 0.6f, 0.36f), 0.44f, 0.48f, 0.1f, 0.18f, Quaternion.identity);
            MeshFactory.FromBuilder("Pack", pack, shade, Backpack.transform);
            MeshFactory.Cylinder("Antenna", Backpack.transform, new Vector3(0.13f, 0.82f, 0.38f), 0.012f, 0.46f, Mats.Lit(new Color(0.7f, 0.72f, 0.75f), 0.7f, 0.8f), 8, false);
            m_Blink = Mats.Emissive(new Color(1f, 0.25f, 0.2f), new Color(3f, 0.5f, 0.3f));
            m_Bulb = MeshFactory.Sphere("Bulb", Backpack.transform, new Vector3(0.13f, 1.3f, 0.38f), 0.035f, m_Blink, 10, 6).GetComponent<Renderer>();
            // Green vent slime round its feet (on the pad, under a vent grate), with drips on the legs.
            Slime = new GameObject("VentSlime");
            Slime.transform.SetParent(t, false);
            Slime.transform.localPosition = new Vector3(0f, PadH - 0.08f, 0f); // its parts are laid out for a pad top at 0.08
            var slime = Mats.Emissive(new Color(0.35f, 1f, 0.2f), new Color(0.5f, 2.2f, 0.3f));
            var grate = Mats.Lit(new Color(0.1f, 0.1f, 0.1f), 0.5f, 0.8f);
            MeshFactory.Box("Vent", Slime.transform, new Vector3(0.18f, 0.083f, -0.28f), new Vector3(0.34f, 0.008f, 0.22f), grate, false, 1f, Quaternion.Euler(0f, 20f, 0f));
            for (int i = 0; i < 4; i++)
                MeshFactory.Box("Slat", Slime.transform, new Vector3(0.18f, 0.089f, -0.28f) + Quaternion.Euler(0f, 20f, 0f) * new Vector3(0f, 0f, -0.075f + i * 0.05f), new Vector3(0.32f, 0.006f, 0.012f), slime, false, 1f, Quaternion.Euler(0f, 20f, 0f));
            MeshFactory.Cylinder("Puddle", Slime.transform, new Vector3(0f, 0.081f, -0.05f), 0.44f, 0.01f, slime, 24, false);
            foreach (var p in new[] { new Vector3(-0.2f, 0.09f, -0.18f), new Vector3(0.24f, 0.09f, -0.12f), new Vector3(-0.08f, 0.09f, 0.28f) })
                MeshFactory.Sphere("Blob", Slime.transform, p, 0.05f, slime, 10, 6).transform.localScale = new Vector3(1.2f, 0.35f, 1f);
            foreach (var p in new[] { new Vector3(-0.14f, 0.3f, -0.11f), new Vector3(0.15f, 0.22f, -0.115f), new Vector3(-0.17f, 0.17f, -0.1f) })
                MeshFactory.Sphere("Drip", Slime.transform, p, 0.028f, slime, 8, 5).transform.localScale = new Vector3(0.8f, 1.6f, 0.6f);
            SetOddity(ImpostorGame.Oddity.NoBackpack);
        }

        public void SetOddity(ImpostorGame.Oddity o)
        {
            Oddity = o;
            Backpack.SetActive(o != ImpostorGame.Oddity.NoBackpack);
            Visor.SetActive(o != ImpostorGame.Oddity.CrackedVisor);
            CrackedVisor.SetActive(o == ImpostorGame.Oddity.CrackedVisor);
            Slime.SetActive(o == ImpostorGame.Oddity.VentSlime);
        }

        public void Restore()
        {
            StopAllCoroutines();
            Gone = false;
            m_Shake = 0f;
            m_Red = 0f;
            Visual.gameObject.SetActive(true);
            Visual.localPosition = new Vector3(0f, PadH, 0f);
            Visual.localRotation = Quaternion.identity;
            Visual.localScale = Vector3.one;
            m_PadRing.sharedMaterial = m_RingOn;
            if (Oddity == ImpostorGame.Oddity.VentSlime) Slime.SetActive(true);
        }

        public void HideGone()
        {
            StopAllCoroutines();
            Gone = true;
            Visual.gameObject.SetActive(false);
            Slime.SetActive(false);
        }

        /// <summary>Wrongly voted: it shakes its head and its pad flashes red.</summary>
        public void Innocent()
        {
            m_Shake = 1.4f;
            m_Red = 1.6f;
        }

        /// <summary>Found out: it is blasted up and out over the harbour, spinning, and is gone.</summary>
        public IEnumerator Eject()
        {
            Gone = true;
            m_Shake = 0f;
            m_PadRing.sharedMaterial = m_RingRed;
            var from = Visual.localPosition;
            float spin = 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 2.4f)
            {
                spin += Time.deltaTime * 720f;
                Visual.localPosition = from + new Vector3(16f * t, 2.4f * t + 7f * t * t, 0f);
                Visual.localRotation = Quaternion.Euler(0f, spin * 0.3f, spin);
                Visual.localScale = Vector3.one * Mathf.Lerp(1f, 0f, Mathf.Clamp01((t - 0.55f) / 0.45f));
                yield return null;
            }
            Visual.gameObject.SetActive(false);
            Slime.SetActive(false);
            m_PadRing.sharedMaterial = m_RingOn;
        }

        void Update()
        {
            if (Gone || Visual == null) return;
            float t = Time.time;
            float yaw = 32f * Mathf.Sin(t * 0.55f + m_Phase);
            if (m_Shake > 0f)
            {
                m_Shake -= Time.deltaTime;
                yaw += 28f * Mathf.Sin(t * 38f) * Mathf.Clamp01(m_Shake);
            }
            Visual.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Visual.localPosition = new Vector3(0f, PadH + 0.018f * Mathf.Abs(Mathf.Sin(t * 2.1f + m_Phase)), 0f);
            if (m_Red > 0f)
            {
                m_Red -= Time.deltaTime;
                m_PadRing.sharedMaterial = m_Red > 0f && Mathf.Repeat(m_Red, 0.4f) > 0.2f ? m_RingRed : m_RingOn;
            }
            if (m_Bulb != null) m_Bulb.enabled = Mathf.Repeat(t + m_Phase, 1.2f) > 0.5f;
        }
    }
}
