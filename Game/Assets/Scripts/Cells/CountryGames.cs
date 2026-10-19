using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// The Passenger hunting the player while a country's game is played (Japan's claw machine, the Saudi maze, the Taj
    /// Mahal puzzle). It stalks in, rushes now and then, and loses interest when the player gets well away, so the player
    /// runs, loses it, and comes back to the game. Stop() hands it back to the story (the stamp chase).
    /// </summary>
    public class GhostHunt : MonoBehaviour
    {
        public Cell Cell;
        public Vector3 SpawnLocal, RetreatLocal;
        public PassengerState SpawnState = PassengerState.Stalk;
        public float StalkSpeed = 0.6f;
        public float RushEvery = 14f, RushSeconds = 2.4f;   // RushEvery 0: it never rushes
        public float LoseDistance = 13f, RestSeconds = 8f;
        public string Arrives = "something is coming for you";
        public bool Running { get; private set; }
        float m_Timer, m_Rush;
        bool m_Out, m_Rushing;

        public void Begin(float delay)
        {
            Running = true;
            m_Out = false;
            m_Timer = delay;
            m_Rush = 0f;
            m_Rushing = false;
        }

        public void Stop()
        {
            Running = false;
            m_Rushing = false;
            var pc = Game.Passenger;
            if (pc != null) { pc.AllowChase = true; pc.RetreatSeconds = 20f; }
        }

        void Update()
        {
            if (!Running || Cell == null || Cell.Mode != CellMode.Active || Cell.Encounter == null || !Cell.Encounter.IsLive) return;
            var story = Game.Story;
            var pc = Game.Passenger;
            if (story == null || pc == null || Game.Rig == null || story.Busy || story.Failing) return;
            // Once the seal is verified (by the game or any other way) the story owns the figure: the stamp chase.
            if (story.WaitingStamp != null || (Cell.Country != null && Game.Campaign != null && Game.Campaign.IsVerified(Cell.Country.Id))) { Stop(); return; }
            if (Game.Campaign != null && Game.Campaign.vitality <= 0f) return; // caught: the Retry card is up
            float dt = Time.deltaTime;
            if (!m_Out || !pc.Visible || pc.transform.parent != Cell.transform)
            {
                if (m_Out) { m_Out = false; m_Timer = 4f; } // sent away by something else: it comes back in a moment
                m_Timer -= dt;
                if (m_Timer > 0f) return;
                var feet = Cell.transform.InverseTransformPoint(Game.Rig.Feet);
                var from = SpawnLocal;
                if (Flat(from - feet) < 7f) from = RetreatLocal; // never on top of the player
                var look = new Vector3(feet.x - from.x, 0f, feet.z - from.z);
                float yaw = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look).eulerAngles.y : 0f;
                pc.Spawn(Cell, from, yaw, SpawnState);
                pc.Threatening = true;
                pc.WalkSpeed = StalkSpeed;
                pc.AllowChase = false;
                pc.RetreatPoint = Cell.transform.TransformPoint(RetreatLocal);
                pc.RetreatSeconds = RestSeconds;
                AudioDirector.I?.Play("pass_whisper", pc.ChestPosition, 0.8f);
                if (!string.IsNullOrEmpty(Arrives)) Hud.I?.ShowCaption(Arrives, 2.5f);
                m_Out = true;
                m_Timer = RushEvery;
                return;
            }
            if (!m_Rushing && pc.State == PassengerState.Chase) return; // someone else's chase
            pc.AllowChase = false;
            pc.WalkSpeed = StalkSpeed;
            float dist = Flat(pc.transform.position - Game.Rig.Feet);
            if (pc.State == PassengerState.Chase)
            {
                // Only a rush this hunt started is called off; any other chase is left alone.
                m_Rush -= dt;
                if (m_Rushing && m_Rush <= 0f) { m_Rushing = false; pc.SetState(PassengerState.Stalk); }
            }
            else if (pc.State == PassengerState.Stalk)
            {
                if (dist > LoseDistance)
                {
                    pc.SetState(PassengerState.Retreat);
                    Hud.I?.ShowCaption("it has lost you, for now: go back", 2.5f);
                    return;
                }
                if (RushEvery > 0f)
                {
                    m_Timer -= dt;
                    if (m_Timer <= 0f && dist < 10f && dist > 2.5f)
                    {
                        m_Timer = RushEvery * Random.Range(0.8f, 1.25f);
                        m_Rush = RushSeconds;
                        m_Rushing = true;
                        pc.StartChase();
                        Hud.I?.ShowCaption("it rushes at you: run", 2f);
                    }
                }
            }
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    }

    /// <summary>Picture quads with an explicit UV rectangle, seen from both sides.</summary>
    public static class ImageQuad
    {
        /// <summary>A w x h quad in the parent's XZ plane (horizontal) or XY plane (upright, facing -Z).</summary>
        public static GameObject Make(string name, Transform parent, Vector3 pos, float w, float h, Rect uv, Material mat, bool horizontal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            float x = w / 2f, y = h / 2f;
            var v = horizontal
                ? new[] { new Vector3(-x, 0, -y), new Vector3(x, 0, -y), new Vector3(x, 0, y), new Vector3(-x, 0, y) }
                : new[] { new Vector3(-x, -y, 0), new Vector3(x, -y, 0), new Vector3(x, y, 0), new Vector3(-x, y, 0) };
            var m = new Mesh { name = name };
            m.vertices = v;
            m.uv = new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax) };
            m.triangles = new[] { 0, 3, 2, 0, 2, 1, 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }

    // ====================================================================== Japan: the claw machine
    /// <summary>
    /// A Japanese claw machine in the lantern street. Arrow buttons step the claw over a 4 x 3 grid of prizes, DROP lowers
    /// it, grabs whatever is under it and carries it to the chute (front-left); the prize comes out on the tray. One prize
    /// is the golden golden passport that opens the way out of Japan.
    /// </summary>
    public class ClawMachine : MonoBehaviour
    {
        public const int Cols = 4, Rows = 3;
        public PushButton Left, Right, Back, Front, Drop;
        public int ClawCol, ClawRow;
        public int GoldCol = 3, GoldRow = 2;
        public bool Busy { get; private set; }
        public Grabbable Golden { get; private set; }   // grabbable once it is on the tray
        public bool GoldenOut => Golden != null;
        public event System.Action<ClawMachine, bool> Delivered;   // true: the golden passport
        public event System.Action<Grabbable> GoldenTaken;

        const float FloorY = 1.03f, CarriageY = 1.86f;
        Transform m_Carriage, m_Head, m_Cable, m_Bridge, m_Stick;
        Vector2 m_Lean;
        readonly List<Transform> m_All = new List<Transform>();
        readonly Transform[] m_Fingers = new Transform[3];
        readonly Dictionary<Vector2Int, Transform> m_Prizes = new Dictionary<Vector2Int, Transform>();
        Transform m_PrizeRoot, m_GoldenT;
        Material m_Gold;
        readonly List<Material> m_Plush = new List<Material>();
        Vector3 m_TrayLocal;

        static Vector3 Slot(int c, int r) => new Vector3(-0.45f + c * 0.3f, 0f, -0.33f + r * 0.29f);

        public static ClawMachine Build(Transform parent, Vector3 pos, float yaw)
        {
            var root = new GameObject("ClawMachine");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var cm = root.AddComponent<ClawMachine>();
            cm.Construct();
            return cm;
        }

        void Construct()
        {
            var t = transform;
            var paint = Mats.Lit(new Color(0.85f, 0.18f, 0.32f), 0.55f);
            var white = Mats.Lit(new Color(0.95f, 0.93f, 0.9f), 0.5f);
            var chrome = Mats.Lit(new Color(0.8f, 0.8f, 0.82f), 0.85f, 0.9f);
            var felt = Mats.Lit(new Color(0.25f, 0.55f, 0.75f), 0.15f);
            var dark = Mats.Lit(new Color(0.05f, 0.05f, 0.06f), 0.3f);
            var glass = Mats.Glass(new Color(0.8f, 0.9f, 1f, 0.12f));
            var sign = Mats.Emissive(new Color(1f, 0.55f, 0.75f), new Color(2.4f, 0.7f, 1.4f));
            // Cabinet, control ledge, glass case, roof and marquee.
            MeshFactory.Box("Cabinet", t, new Vector3(0f, 0.475f, 0f), new Vector3(1.3f, 0.95f, 1.1f), paint, true);
            MeshFactory.Box("Stripe", t, new Vector3(0f, 0.7f, -0.556f), new Vector3(1.3f, 0.08f, 0.01f), white, false);
            MeshFactory.Box("Ledge", t, new Vector3(0f, 0.97f, -0.66f), new Vector3(1.3f, 0.06f, 0.26f), white, true);
            MeshFactory.Box("PrizeFloor", t, new Vector3(0f, 0.99f, 0f), new Vector3(1.26f, 0.04f, 1.06f), felt, false);
            foreach (var x in new[] { -0.63f, 0.63f })
                foreach (var z in new[] { -0.53f, 0.53f })
                    MeshFactory.Box("Post", t, new Vector3(x, 1.45f, z), new Vector3(0.04f, 1.0f, 0.04f), chrome, false);
            MeshFactory.Box("GlassFront", t, new Vector3(0f, 1.45f, -0.545f), new Vector3(1.22f, 0.96f, 0.01f), glass, false);
            MeshFactory.Box("GlassL", t, new Vector3(-0.625f, 1.45f, 0f), new Vector3(0.01f, 0.96f, 1.04f), glass, false);
            MeshFactory.Box("GlassR", t, new Vector3(0.625f, 1.45f, 0f), new Vector3(0.01f, 0.96f, 1.04f), glass, false);
            MeshFactory.Box("BackPanel", t, new Vector3(0f, 1.45f, 0.545f), new Vector3(1.22f, 0.96f, 0.01f), Mats.Emissive(new Color(0.3f, 0.15f, 0.35f), new Color(0.35f, 0.12f, 0.4f)), false);
            var caseCol = new GameObject("CaseCollider");
            caseCol.transform.SetParent(t, false);
            caseCol.AddComponent<BoxCollider>().size = new Vector3(1.3f, 1.0f, 1.1f);
            caseCol.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            MeshFactory.Box("Roof", t, new Vector3(0f, 2.03f, 0f), new Vector3(1.36f, 0.16f, 1.16f), paint, true);
            MeshFactory.Box("Marquee", t, new Vector3(0f, 2.2f, -0.5f), new Vector3(1.3f, 0.26f, 0.1f), sign, false);
            var title = TextFactory.World(t, "CLAW  CATCHER", 0.11f, new Color(0.25f, 0.02f, 0.12f), TMPro.TextAlignmentOptions.Center, false, 1.2f);
            title.transform.localPosition = new Vector3(0f, 2.2f, -0.556f);
            var lightGo = new GameObject("CaseLight");
            lightGo.transform.SetParent(t, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.75f, -0.2f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.7f, 0.85f); l.intensity = 1.6f; l.range = 2.6f; l.shadows = LightShadows.None;
            var front = new GameObject("FrontLight");
            front.transform.SetParent(t, false);
            front.transform.localPosition = new Vector3(0f, 2.1f, -1.0f);
            var fl = front.AddComponent<Light>();
            fl.type = LightType.Point; fl.color = new Color(1f, 0.6f, 0.8f); fl.intensity = 1.3f; fl.range = 3.2f; fl.shadows = LightShadows.None;

            // The chute (front-left, under the claw's home) and the prize tray it empties into.
            var home = Slot(0, 0);
            MeshFactory.Box("ChuteHole", t, new Vector3(home.x, FloorY - 0.015f, home.z), new Vector3(0.26f, 0.01f, 0.24f), dark, false);
            MeshFactory.Box("ChuteWallR", t, new Vector3(home.x + 0.14f, FloorY + 0.06f, home.z), new Vector3(0.01f, 0.14f, 0.24f), glass, false);
            MeshFactory.Box("ChuteWallB", t, new Vector3(home.x, FloorY + 0.06f, home.z + 0.13f), new Vector3(0.28f, 0.14f, 0.01f), glass, false);
            m_TrayLocal = new Vector3(0.38f, 0.5f, -0.93f);
            MeshFactory.Box("Tray", t, new Vector3(0.38f, 0.44f, -0.8f), new Vector3(0.36f, 0.04f, 0.5f), dark, true);
            MeshFactory.Box("TrayLip", t, new Vector3(0.38f, 0.48f, -1.05f), new Vector3(0.36f, 0.08f, 0.01f), chrome, false);
            var trayText = TextFactory.World(t, "PRIZE", 0.05f, Color.white, TMPro.TextAlignmentOptions.Center, false, 0.4f);
            trayText.transform.localPosition = new Vector3(0.38f, 0.58f, -0.557f);

            // Gantry: rails, the carriage, its cable, the head and three fingers.
            MeshFactory.Box("RailL", t, new Vector3(-0.58f, CarriageY + 0.04f, 0f), new Vector3(0.03f, 0.03f, 1.0f), chrome, false);
            MeshFactory.Box("RailR", t, new Vector3(0.58f, CarriageY + 0.04f, 0f), new Vector3(0.03f, 0.03f, 1.0f), chrome, false);
            m_Carriage = new GameObject("Carriage").transform;
            m_Carriage.SetParent(t, false);
            m_Bridge = MeshFactory.Box("Bridge", m_Carriage, new Vector3(0f, 0.04f, 0f), new Vector3(1.16f, 0.03f, 0.05f), chrome, false).transform;
            MeshFactory.Box("Trolley", m_Carriage, new Vector3(0f, 0f, 0f), new Vector3(0.1f, 0.06f, 0.1f), dark, false);
            m_Cable = MeshFactory.Box("Cable", m_Carriage, new Vector3(0f, -0.5f, 0f), new Vector3(0.008f, 1f, 0.008f), dark, false).transform;
            m_Head = new GameObject("Head").transform;
            m_Head.SetParent(m_Carriage, false);
            MeshFactory.Cylinder("Hub", m_Head, new Vector3(0f, -0.03f, 0f), 0.045f, 0.07f, chrome, 12, false);
            for (int i = 0; i < 3; i++)
            {
                var pivot = new GameObject("Finger" + i).transform;
                pivot.SetParent(m_Head, false);
                pivot.localPosition = new Vector3(0f, -0.03f, 0f);
                pivot.localRotation = Quaternion.Euler(0f, i * 120f, 0f);
                var knuckle = new GameObject("Knuckle").transform;
                knuckle.SetParent(pivot, false);
                knuckle.localPosition = new Vector3(0f, 0f, 0.04f);
                MeshFactory.Box("Bone", knuckle, new Vector3(0f, -0.06f, 0.015f), new Vector3(0.012f, 0.12f, 0.012f), chrome, false);
                MeshFactory.Box("Tip", knuckle, new Vector3(0f, -0.12f, -0.005f), new Vector3(0.012f, 0.012f, 0.04f), chrome, false);
                m_Fingers[i] = knuckle;
            }
            SetFingers(0.35f);
            m_PrizeRoot = new GameObject("Prizes").transform;
            m_PrizeRoot.SetParent(t, false);
            m_Gold = Mats.Lit(new Color(1f, 0.78f, 0.25f), 0.8f, 1f, null, false, new Color(0.5f, 0.35f, 0.05f));
            m_Plush.Add(Mats.Lit(new Color(0.98f, 0.7f, 0.8f), 0.1f));
            m_Plush.Add(Mats.Lit(new Color(0.6f, 0.85f, 0.95f), 0.1f));
            m_Plush.Add(Mats.Lit(new Color(0.95f, 0.92f, 0.6f), 0.1f));
            m_Plush.Add(Mats.Lit(new Color(0.7f, 0.95f, 0.7f), 0.1f));
            m_Plush.Add(Mats.Lit(new Color(0.85f, 0.75f, 0.98f), 0.1f));

            // Controls on the ledge: a cross of arrow buttons on the left, the big DROP button on the right.
            Left = MakeButton("Move the claw left", new Vector3(-0.5f, 1.0f, -0.67f), "<", new Color(0.25f, 0.55f, 0.95f));
            Right = MakeButton("Move the claw right", new Vector3(-0.26f, 1.0f, -0.67f), ">", new Color(0.25f, 0.55f, 0.95f));
            Back = MakeButton("Move the claw back", new Vector3(-0.38f, 1.0f, -0.585f), "^", new Color(0.25f, 0.55f, 0.95f));
            Front = MakeButton("Move the claw forward", new Vector3(-0.38f, 1.0f, -0.755f), "v", new Color(0.25f, 0.55f, 0.95f));
            Drop = MakeButton("Drop the claw", new Vector3(0.3f, 1.0f, -0.67f), "DROP", new Color(0.95f, 0.15f, 0.12f), 0.065f);
            // A ball-top joystick in the middle of the arrow cross: it leans the way the claw moves.
            m_Stick = new GameObject("Joystick").transform;
            m_Stick.SetParent(t, false);
            m_Stick.localPosition = new Vector3(-0.38f, 1.0f, -0.67f);
            MeshFactory.Cylinder("Gate", t, new Vector3(-0.38f, 0.995f, -0.67f), 0.035f, 0.012f, Mats.Lit(new Color(0.08f, 0.08f, 0.09f), 0.5f), 16, false);
            MeshFactory.Cylinder("Shaft", m_Stick, Vector3.zero, 0.008f, 0.09f, chrome, 8, false);
            MeshFactory.Sphere("Ball", m_Stick, new Vector3(0f, 0.1f, 0f), 0.024f, Mats.Emissive(new Color(0.95f, 0.15f, 0.12f), new Color(0.6f, 0.05f, 0.03f)));
            Left.Pressed += _ => Step(-1, 0);
            Right.Pressed += _ => Step(1, 0);
            Back.Pressed += _ => Step(0, 1);
            Front.Pressed += _ => Step(0, -1);
            Drop.Pressed += _ => DropClaw();
            PlaceCarriage();
        }

        PushButton MakeButton(string prompt, Vector3 at, string label, Color c, float r = 0.045f)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            var cap = MeshFactory.Cylinder("Cap", go.transform, Vector3.zero, r, 0.03f, Mats.Emissive(c, c * 0.8f), 16, false);
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(r * 2.2f, 0.07f, r * 2.2f);
            col.center = new Vector3(0f, 0.02f, 0f);
            var txt = TextFactory.World(go.transform, label, label.Length > 1 ? 0.032f : 0.05f, Color.white, TMPro.TextAlignmentOptions.Center, false, 0.2f);
            txt.transform.localPosition = new Vector3(0f, 0.034f, 0f);
            txt.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var b = go.AddComponent<PushButton>();
            b.Prompt = prompt;
            b.Cap = cap.transform;
            b.PressOffset = new Vector3(0f, -0.012f, 0f);
            b.MaxDistance = 2.2f;
            return b;
        }

        /// <summary>A fresh game: the claw at home, plush toys in most slots and the golden passport in one of the far ones.</summary>
        public void ResetGame()
        {
            StopAllCoroutines();
            Busy = false;
            foreach (var c in m_All) if (c != null && (c.GetComponent<Grabbable>() == null || c.GetComponent<Grabbable>().Holder == null)) Destroy(c.gameObject);
            m_All.Clear();
            m_Prizes.Clear();
            Golden = null;
            m_GoldenT = null;
            var far = new[] { new Vector2Int(3, 2), new Vector2Int(2, 2), new Vector2Int(3, 1), new Vector2Int(1, 2) };
            var gold = far[Random.Range(0, far.Length)];
            GoldCol = gold.x; GoldRow = gold.y;
            int n = 0;
            for (int c = 0; c < Cols; c++)
                for (int r = 0; r < Rows; r++)
                {
                    var key = new Vector2Int(c, r);
                    if (c == 0 && r == 0) continue; // the chute
                    if (key == gold) { m_GoldenT = MakeLuckyCat(key); m_Prizes[key] = m_GoldenT; continue; }
                    if ((c * 7 + r * 3) % 5 == 1) continue; // a couple of gaps
                    m_Prizes[key] = MakePlush(key, n++);
                }
            ClawCol = 0; ClawRow = 0;
            m_Head.localPosition = new Vector3(0f, -0.1f, 0f);
            SetFingers(0.35f);
            PlaceCarriage();
            SetButtons(true);
        }

        /// <summary>The game is over (the seal is verified): no prizes left to win.</summary>
        public void SetSolved()
        {
            ResetGame();
            if (m_Prizes.TryGetValue(new Vector2Int(GoldCol, GoldRow), out var g)) { Destroy(g.gameObject); m_Prizes.Remove(new Vector2Int(GoldCol, GoldRow)); }
            m_GoldenT = null;
            SetButtons(false);
        }

        void SetButtons(bool on)
        {
            foreach (var b in new[] { Left, Right, Back, Front, Drop }) if (b != null) b.Usable = on;
        }

        Transform MakePlush(Vector2Int k, int i)
        {
            var p = new GameObject("Plush").transform;
            p.SetParent(m_PrizeRoot, false);
            p.localPosition = Slot(k.x, k.y) + new Vector3(0f, FloorY, 0f);
            p.localRotation = Quaternion.Euler(0f, Random.Range(-40f, 40f) + 180f, 0f);
            m_All.Add(p);
            var m = m_Plush[i % m_Plush.Count];
            MeshFactory.Sphere("Body", p, new Vector3(0f, 0.07f, 0f), 0.075f, m);
            MeshFactory.Sphere("Head", p, new Vector3(0f, 0.16f, 0f), 0.055f, m);
            MeshFactory.Sphere("EarL", p, new Vector3(-0.035f, 0.21f, 0f), 0.02f, m);
            MeshFactory.Sphere("EarR", p, new Vector3(0.035f, 0.21f, 0f), 0.02f, m);
            var eye = Mats.Lit(new Color(0.05f, 0.05f, 0.05f), 0.6f);
            MeshFactory.Sphere("EyeL", p, new Vector3(-0.02f, 0.17f, -0.05f), 0.008f, eye);
            MeshFactory.Sphere("EyeR", p, new Vector3(0.02f, 0.17f, -0.05f), 0.008f, eye);
            return p;
        }

        Transform MakeLuckyCat(Vector2Int k)
        {
            var p = new GameObject("GoldenPassport").transform;
            p.SetParent(m_PrizeRoot, false);
            p.localPosition = Slot(k.x, k.y) + new Vector3(0f, FloorY, 0f);
            p.localRotation = Quaternion.Euler(0f, 180f, 0f);
            m_All.Add(p);
            BuildPassportPrize(p, m_Gold);
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(p, false);
            glow.transform.localPosition = new Vector3(0f, 0.15f, 0.1f);
            glow.type = LightType.Point; glow.color = new Color(1f, 0.8f, 0.35f); glow.intensity = 0.8f; glow.range = 0.8f; glow.shadows = LightShadows.None;
            return p;
        }

        void Update()
        {
            if (m_Bridge != null && m_Carriage != null) m_Bridge.localPosition = new Vector3(-m_Carriage.localPosition.x, 0.04f, 0f);
            if (m_Stick != null)
            {
                m_Lean = Vector2.MoveTowards(m_Lean, Vector2.zero, Time.deltaTime * 2.5f);
                m_Stick.localRotation = Quaternion.Euler(m_Lean.y * 22f, 0f, -m_Lean.x * 22f);
            }
        }

        /// <summary>The special prize: a golden passport standing on a little plinth, a red seal on its cover.</summary>
        static void BuildPassportPrize(Transform p, Material gold)
        {
            var red = Mats.Emissive(new Color(0.85f, 0.12f, 0.1f), new Color(0.9f, 0.1f, 0.05f));
            var pages = Mats.Lit(new Color(0.96f, 0.92f, 0.8f), 0.3f);
            MeshFactory.Box("Plinth", p, new Vector3(0f, 0.015f, 0f), new Vector3(0.13f, 0.03f, 0.09f), Mats.Lit(new Color(0.15f, 0.1f, 0.12f), 0.5f), false);
            var book = new GameObject("Book").transform;
            book.SetParent(p, false);
            book.localPosition = new Vector3(0f, 0.03f, 0f);
            MeshFactory.Box("Cover", book, new Vector3(0f, 0.08f, 0f), new Vector3(0.11f, 0.16f, 0.022f), gold, false);
            MeshFactory.Box("Pages", book, new Vector3(0.004f, 0.08f, 0f), new Vector3(0.104f, 0.15f, 0.026f), pages, false);
            MeshFactory.Cylinder("Seal", book, new Vector3(0f, 0.085f, -0.012f), 0.026f, 0.004f, red, 16, false).transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var label = TextFactory.World(book, "PASSPORT", 0.012f, new Color(0.35f, 0.2f, 0.02f), TMPro.TextAlignmentOptions.Center, true, 0.1f);
            label.transform.localPosition = new Vector3(0f, 0.135f, -0.0125f);
        }

        static void BuildCat(Transform p, Material gold)
        {
            var red = Mats.Lit(new Color(0.85f, 0.1f, 0.1f), 0.4f);
            var black = Mats.Lit(new Color(0.03f, 0.03f, 0.03f), 0.6f);
            MeshFactory.Sphere("Body", p, new Vector3(0f, 0.075f, 0f), 0.08f, gold);
            MeshFactory.Sphere("Head", p, new Vector3(0f, 0.18f, 0f), 0.065f, gold);
            MeshFactory.Frustum("EarL", p, new Vector3(-0.04f, 0.22f, 0f), 0.02f, 0.002f, 0.04f, gold, 8);
            MeshFactory.Frustum("EarR", p, new Vector3(0.04f, 0.22f, 0f), 0.02f, 0.002f, 0.04f, gold, 8);
            MeshFactory.Sphere("Paw", p, new Vector3(0.07f, 0.23f, -0.02f), 0.022f, gold); // the raised beckoning paw
            MeshFactory.Box("Arm", p, new Vector3(0.07f, 0.19f, -0.02f), new Vector3(0.025f, 0.06f, 0.025f), gold, false);
            MeshFactory.Box("Collar", p, new Vector3(0f, 0.125f, 0f), new Vector3(0.1f, 0.012f, 0.1f), red, false);
            MeshFactory.Sphere("Bell", p, new Vector3(0f, 0.115f, -0.05f), 0.014f, gold);
            MeshFactory.Sphere("EyeL", p, new Vector3(-0.022f, 0.19f, -0.058f), 0.008f, black);
            MeshFactory.Sphere("EyeR", p, new Vector3(0.022f, 0.19f, -0.058f), 0.008f, black);
        }

        void PlaceCarriage()
        {
            var s = Slot(ClawCol, ClawRow);
            m_Carriage.localPosition = new Vector3(s.x, CarriageY, s.z);
            UpdateCable();
        }

        void UpdateCable()
        {
            float len = Mathf.Max(0.02f, -m_Head.localPosition.y);
            m_Cable.localScale = new Vector3(1f, len, 1f);
            m_Cable.localPosition = new Vector3(0f, -len / 2f, 0f);
        }

        void SetFingers(float open)
        {
            // open 0: closed round a prize, 1: wide open.
            for (int i = 0; i < 3; i++) if (m_Fingers[i] != null) m_Fingers[i].localRotation = Quaternion.Euler(Mathf.Lerp(-8f, -38f, open), 0f, 0f);
        }

        public void Step(int dc, int dr)
        {
            if (Busy) return;
            m_Lean = new Vector2(dc, dr);
            int c = Mathf.Clamp(ClawCol + dc, 0, Cols - 1), r = Mathf.Clamp(ClawRow + dr, 0, Rows - 1);
            if (c == ClawCol && r == ClawRow) { AudioDirector.I?.Play("dial_click", transform.position + Vector3.up, 0.5f, 0.7f); return; }
            StartCoroutine(MoveTo(c, r, 0.3f));
        }

        IEnumerator MoveTo(int c, int r, float seconds)
        {
            Busy = true;
            var from = m_Carriage.localPosition;
            var s = Slot(c, r);
            var to = new Vector3(s.x, CarriageY, s.z);
            AudioDirector.I?.Play("dial_click", transform.position + Vector3.up * 1.8f, 0.6f, 1.3f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds) { m_Carriage.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)); yield return null; }
            m_Carriage.localPosition = to;
            ClawCol = c; ClawRow = r;
            Busy = false;
        }

        public void DropClaw()
        {
            if (Busy || m_PrizeRoot == null) return;
            StartCoroutine(DropRoutine());
        }

        IEnumerator DropRoutine()
        {
            Busy = true;
            AudioDirector.I?.Play("lever_clunk", transform.position + Vector3.up * 1.2f, 0.7f);
            // Open and lower to the prizes.
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f) { SetFingers(Mathf.Lerp(0.35f, 1f, t)); yield return null; }
            float top = -0.1f, bottom = -(CarriageY - FloorY) + 0.25f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.1f)
            {
                m_Head.localPosition = new Vector3(0f, Mathf.Lerp(top, bottom, t), 0f);
                UpdateCable();
                yield return null;
            }
            AudioDirector.I?.Play("grab", m_Head.position, 0.6f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f) { SetFingers(Mathf.Lerp(1f, 0f, t)); yield return null; }
            var key = new Vector2Int(ClawCol, ClawRow);
            m_Prizes.TryGetValue(key, out var prize);
            if (prize != null)
            {
                m_Prizes.Remove(key);
                prize.SetParent(m_Head, true);
            }
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.0f)
            {
                m_Head.localPosition = new Vector3(0f, Mathf.Lerp(bottom, top, t), 0f);
                UpdateCable();
                yield return null;
            }
            // Back to the chute, one slot at a time.
            if (ClawCol != 0 || ClawRow != 0)
            {
                var from = m_Carriage.localPosition;
                var hs = Slot(0, 0);
                var to = new Vector3(hs.x, CarriageY, hs.z);
                float secs = 0.25f * (ClawCol + ClawRow) + 0.3f;
                for (float t = 0f; t < 1f; t += Time.deltaTime / secs) { m_Carriage.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)); yield return null; }
                m_Carriage.localPosition = to;
                ClawCol = 0; ClawRow = 0;
            }
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f) { SetFingers(Mathf.Lerp(0f, 0.6f, t)); yield return null; }
            if (prize == null)
            {
                AudioDirector.I?.Play("dial_click", transform.position + Vector3.up, 0.5f, 0.6f);
                Hud.I?.ShowCaption("the claw comes back empty", 2f);
                SetFingers(0.35f);
                Busy = false;
                yield break;
            }
            // The prize drops down the chute and rolls out on the tray.
            prize.SetParent(transform, true);
            var start = prize.localPosition;
            var hole = Slot(0, 0) + new Vector3(0f, 0.6f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.45f) { prize.localPosition = Vector3.Lerp(start, hole, t * t); yield return null; }
            prize.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.35f);
            AudioDirector.I?.Play("drop_soft", transform.TransformPoint(m_TrayLocal), 0.9f);
            prize.localPosition = m_TrayLocal;
            prize.localRotation = Quaternion.Euler(0f, 180f, 0f);
            prize.gameObject.SetActive(true);
            SetFingers(0.35f);
            bool golden = prize == m_GoldenT;
            if (golden)
            {
                var col = prize.gameObject.AddComponent<BoxCollider>();
                col.size = new Vector3(0.18f, 0.24f, 0.14f);
                col.center = new Vector3(0f, 0.11f, 0f);
                Golden = PropLibrary.MakeGrabbable(prize.gameObject, "golden_passport", "Take the golden passport");
                Golden.SetHome();
                Golden.Grabbed += OnGoldenGrabbed;
                AudioDirector.I?.Play("coin_token", prize.position, 1f);
                SetButtons(false);
            }
            else
            {
                Hud.I?.ShowToast("Just a plush toy. Not the one you need: aim for the golden passport.", 3f);
                StartCoroutine(ClearPlush(prize));
            }
            Delivered?.Invoke(this, golden);
            Busy = false;
        }

        IEnumerator ClearPlush(Transform p)
        {
            yield return new WaitForSeconds(2f);
            if (p != null) Destroy(p.gameObject);
        }

        // Every grab is reported (the encounter ignores repeats once it is solved): a grab that came while the encounter
        // could not take it (a catch, a retry) must not leave the cat unable to count later (playtest: it stayed in the air).
        void OnGoldenGrabbed(Grabbable g, Interactor who) => GoldenTaken?.Invoke(g);

        /// <summary>The golden passport is in a hand right now.</summary>
        public bool GoldenHeld => Golden != null && Golden.Holder != null;

        /// <summary>Puts a won but untaken golden passport back on the prize tray (after a catch or a retry left it somewhere).</summary>
        public void ReturnGolden()
        {
            if (Golden != null && Golden.Holder == null) Golden.ReturnHome();
        }

        /// <summary>Where the golden passport is (in the case, on the claw, or on the tray), for the guide.</summary>
        public Vector3 GoldenPosition => Golden != null ? Golden.transform.position : m_GoldenT != null ? m_GoldenT.position : transform.position + Vector3.up;
        public Vector3 ControlsPosition => transform.TransformPoint(new Vector3(-0.1f, 1.0f, -0.67f));
    }

    // ====================================================================== Saudi Arabia: the maze
    /// <summary>
    /// A walled mud-brick maze at dusk with water glasses along the way and a portal past its north end. The layout is a
    /// fixed-seed recursive backtracker, so it is the same every time (the guide and the tests walk its solution).
    /// </summary>
    public class SandMaze : MonoBehaviour
    {
        public const int W = 6, H = 6;
        public const float Size = 2.6f, WallH = 2.7f, WallT = 0.3f;
        public Vector3 Origin;   // the south-west corner, in the parent's space
        public Vector2Int Start = new Vector2Int(2, 0), Exit = new Vector2Int(3, H - 1);
        public readonly List<Vector2Int> Solution = new List<Vector2Int>();
        public readonly List<Transform> Waters = new List<Transform>();
        public Transform Portal;
        public VistaInfo StampSpot;
        bool[,] m_N, m_E;   // a passage north of / east of cell (x, y)
        Material m_PortalMat;
        readonly List<Material> m_WaterMats = new List<Material>();

        public Vector3 CellCenter(Vector2Int c) => Origin + new Vector3((c.x + 0.5f) * Size, 0f, (c.y + 0.5f) * Size);
        public Vector2Int CellAt(Vector3 local)
        {
            var d = local - Origin;
            return new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(d.x / Size), 0, W - 1), Mathf.Clamp(Mathf.FloorToInt(d.z / Size), 0, H - 1));
        }
        public float NorthEdgeZ => Origin.z + H * Size;
        public Vector3 PortalLocal => CellCenter(Exit) + new Vector3(0f, 0f, Size * 0.5f + 5.2f);
        /// <summary>The player is out of the maze, in the portal yard.</summary>
        public bool Escaped(Vector3 local) => local.z > NorthEdgeZ + 0.8f && Mathf.Abs(local.x - CellCenter(Exit).x) < 3.2f;

        public static SandMaze Build(Transform parent, Vector3 origin, LightingPreset lighting)
        {
            var go = new GameObject("Maze");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<SandMaze>();
            m.Origin = origin;
            m.Generate(1207);
            m.Construct(lighting);
            return m;
        }

        void Generate(int seed)
        {
            m_N = new bool[W, H];
            m_E = new bool[W, H];
            var rng = new System.Random(seed);
            var seen = new bool[W, H];
            var stack = new Stack<Vector2Int>();
            stack.Push(Start);
            seen[Start.x, Start.y] = true;
            var dirs = new[] { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };
            while (stack.Count > 0)
            {
                var c = stack.Peek();
                var options = new List<Vector2Int>();
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (n.x >= 0 && n.y >= 0 && n.x < W && n.y < H && !seen[n.x, n.y]) options.Add(n);
                }
                if (options.Count == 0) { stack.Pop(); continue; }
                var next = options[rng.Next(options.Count)];
                Open(c, next);
                seen[next.x, next.y] = true;
                stack.Push(next);
            }
            // A perfect maze: every cell reachable and exactly one way from the start to the exit (playtest, 18 Oct: extra
            // openings made it read as "no correct path").
            // Solution by breadth-first search.
            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var q = new Queue<Vector2Int>();
            q.Enqueue(Start);
            prev[Start] = Start;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == Exit) break;
                foreach (var n in Neighbours(c)) if (!prev.ContainsKey(n)) { prev[n] = c; q.Enqueue(n); }
            }
            Solution.Clear();
            for (var c = Exit; ; c = prev[c]) { Solution.Insert(0, c); if (c == Start) break; }
        }

        void Open(Vector2Int a, Vector2Int b)
        {
            if (b.y > a.y) m_N[a.x, a.y] = true;
            else if (b.y < a.y) m_N[b.x, b.y] = true;
            else if (b.x > a.x) m_E[a.x, a.y] = true;
            else m_E[b.x, b.y] = true;
        }

        public IEnumerable<Vector2Int> Neighbours(Vector2Int c)
        {
            if (c.y < H - 1 && m_N[c.x, c.y]) yield return new Vector2Int(c.x, c.y + 1);
            if (c.y > 0 && m_N[c.x, c.y - 1]) yield return new Vector2Int(c.x, c.y - 1);
            if (c.x < W - 1 && m_E[c.x, c.y]) yield return new Vector2Int(c.x + 1, c.y);
            if (c.x > 0 && m_E[c.x - 1, c.y]) yield return new Vector2Int(c.x - 1, c.y);
        }

        void Construct(LightingPreset lighting)
        {
            var t = transform;
            var mud = Mats.Lit(new Color(0.86f, 0.66f, 0.47f), 0.15f, 0f, "sandstone_blocks");
            var cap = Mats.Lit(new Color(0.72f, 0.52f, 0.36f), 0.2f, 0f, "plaza_stone");
            var sand = Mats.Lit(new Color(0.9f, 0.76f, 0.56f), 0.1f, 0f, "plaza_stone");
            var o = Origin;
            float wx = W * Size, wz = H * Size;
            MeshFactory.Floor("MazeSand", t, o + new Vector3(wx / 2f, 0f, wz / 2f + 4f), new Vector2(wx + 40f, wz + 48f), sand, "sand", 0.3f, 2f);

            void Wall(Vector3 a, Vector3 b)
            {
                // A straight run from a to b (both on the floor), with a darker coping.
                var mid = (a + b) / 2f;
                var len = (b - a).magnitude + WallT;
                bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
                var size = alongX ? new Vector3(len, WallH, WallT) : new Vector3(WallT, WallH, len);
                MeshFactory.Box("MazeWall", t, mid + Vector3.up * WallH / 2f, size, mud, true, 1.4f);
                MeshFactory.Box("Coping", t, mid + Vector3.up * (WallH + 0.05f), size + new Vector3(0.08f, -WallH + 0.1f, 0.08f), cap, false);
            }

            // South boundary and west boundary, then each cell's closed north and east sides, merged into runs.
            Wall(o, o + new Vector3(wx, 0, 0));
            Wall(o, o + new Vector3(0, 0, wz));
            for (int y = 0; y < H; y++)
            {
                int run = -1;
                for (int x = 0; x <= W; x++)
                {
                    bool closed = x < W && !(y == H - 1 && x == Exit.x) && (y == H - 1 || !m_N[x, y]);
                    if (closed && run < 0) run = x;
                    if (!closed && run >= 0)
                    {
                        float z = o.z + (y + 1) * Size;
                        Wall(new Vector3(o.x + run * Size, 0, z), new Vector3(o.x + x * Size, 0, z));
                        run = -1;
                    }
                }
            }
            for (int x = 0; x < W; x++)
            {
                int run = -1;
                for (int y = 0; y <= H; y++)
                {
                    bool closed = y < H && (x == W - 1 || !m_E[x, y]);
                    if (closed && run < 0) run = y;
                    if (!closed && run >= 0)
                    {
                        float xx = o.x + (x + 1) * Size;
                        Wall(new Vector3(xx, 0, o.z + run * Size), new Vector3(xx, 0, o.z + y * Size));
                        run = -1;
                    }
                }
            }

            // The portal yard past the exit: walled on three sides, the portal at its far end.
            var exitC = CellCenter(Exit);
            float yz0 = NorthEdgeZ, yz1 = NorthEdgeZ + 7.5f;
            Wall(new Vector3(exitC.x - 3.3f, 0, yz0), new Vector3(exitC.x - 3.3f, 0, yz1));
            Wall(new Vector3(exitC.x + 3.3f, 0, yz0), new Vector3(exitC.x + 3.3f, 0, yz1));
            Wall(new Vector3(exitC.x - 3.3f, 0, yz1), new Vector3(exitC.x + 3.3f, 0, yz1));
            BuildPortal(PortalLocal, cap);
            var beaconMat = Mats.UnlitInstance(new Color(0.55f, 0.9f, 1f, 0.35f), Tex.SoftDot, true, 1.6f);
            for (int i = 0; i < 2; i++)
            {
                var q = MeshFactory.Quad("ExitBeacon" + i, t, 1.6f, 22f, beaconMat, PortalLocal + new Vector3(0f, 11f, 0f), Quaternion.Euler(0f, i * 90f, 0f));
                q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var exitSign = TextFactory.World(t, "EXIT", 0.6f, new Color(0.6f, 0.95f, 1f), TMPro.TextAlignmentOptions.Center, true, 3f);
            exitSign.transform.localPosition = CellCenter(Exit) + new Vector3(0f, WallH + 0.7f, Size * 0.5f);

            // Lanterns on the walls every few cells so the corridors read at dusk.
            var lampMat = Mats.Emissive(new Color(1f, 0.75f, 0.45f), new Color(2.2f, 1.3f, 0.5f));
            for (int i = 0; i < Solution.Count; i += 5)
            {
                var c = CellCenter(Solution[i]);
                var lg = new GameObject("MazeLamp");
                lg.transform.SetParent(t, false);
                lg.transform.localPosition = c + new Vector3(0f, 2.3f, 0f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.7f, 0.42f); l.intensity = 1.3f; l.range = 5.5f; l.shadows = LightShadows.None;
            }

            // Water glasses: in dead ends spread through the maze, and two along the way through.
            var spots = new List<Vector2Int>();
            var deadEnds = new List<Vector2Int>();
            for (int x = 0; x < W; x++)
                for (int y = 0; y < H; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (c == Start || c == Exit) continue;
                    int n = 0; foreach (var _ in Neighbours(c)) n++;
                    if (n == 1) deadEnds.Add(c);
                }
            for (int i = 0; i < deadEnds.Count && spots.Count < 4; i += Mathf.Max(1, deadEnds.Count / 4)) spots.Add(deadEnds[i]);
            foreach (var f in new[] { 0.3f, 0.65f })
            {
                var c = Solution[Mathf.Clamp(Mathf.RoundToInt(f * (Solution.Count - 1)), 1, Solution.Count - 2)];
                if (!spots.Contains(c)) spots.Add(c);
            }
            var stool = Mats.Lit(new Color(0.45f, 0.3f, 0.2f), 0.3f, 0f, "wood_dark");
            var glass = Mats.Glass(new Color(0.85f, 0.95f, 1f, 0.2f));
            foreach (var c in spots)
            {
                var w = new GameObject("WaterGlass").transform;
                w.SetParent(t, false);
                w.localPosition = CellCenter(c) + new Vector3(0.55f, 0f, 0.55f);
                MeshFactory.Box("Stool", w, new Vector3(0f, 0.25f, 0f), new Vector3(0.36f, 0.5f, 0.36f), stool, false);
                MeshFactory.Frustum("Glass", w, new Vector3(0f, 0.5f, 0f), 0.045f, 0.055f, 0.14f, glass, 14);
                var wm = Mats.Lit(new Color(0.4f, 0.75f, 1f), 0.9f, 0f, null, false, new Color(0.3f, 0.8f, 1.6f));
                m_WaterMats.Add(wm);
                MeshFactory.Frustum("Water", w, new Vector3(0f, 0.505f, 0f), 0.04f, 0.05f, 0.11f, wm, 14);
                var lg = new GameObject("WaterGlow");
                lg.transform.SetParent(w, false);
                lg.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(0.45f, 0.75f, 1f); l.intensity = 1.2f; l.range = 2.6f; l.shadows = LightShadows.None;
                Waters.Add(w);
            }
        }

        void BuildPortal(Vector3 at, Material stone)
        {
            Portal = new GameObject("Portal").transform;
            Portal.SetParent(transform, false);
            Portal.localPosition = at;
            MeshFactory.Box("PillarL", Portal, new Vector3(-1.75f, 1.7f, 0f), new Vector3(0.45f, 3.4f, 0.45f), stone, true);
            MeshFactory.Box("PillarR", Portal, new Vector3(1.75f, 1.7f, 0f), new Vector3(0.45f, 3.4f, 0.45f), stone, true);
            MeshFactory.Box("Lintel", Portal, new Vector3(0f, 3.55f, 0f), new Vector3(4.0f, 0.4f, 0.5f), stone, true);
            var ring = new MeshBuilder();
            ring.AddTorus(Vector3.zero, 1.35f, 0.09f, 40, 8, Quaternion.Euler(90f, 0f, 0f));
            var ringGo = MeshFactory.FromBuilder("Ring", ring, Mats.Emissive(new Color(0.6f, 0.9f, 1f), new Color(1.2f, 2.2f, 3f)), Portal);
            ringGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            m_PortalMat = Mats.UnlitInstance(new Color(0.5f, 0.85f, 1f, 0.55f), Tex.Ring, true, 1.6f);
            for (int i = 0; i < 2; i++)
            {
                var disc = MeshFactory.Quad("Swirl" + i, Portal, 2.6f, 2.6f, m_PortalMat, new Vector3(0f, 1.6f, 0.02f * i), Quaternion.identity);
                disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var lg = new GameObject("PortalLight");
            lg.transform.SetParent(Portal, false);
            lg.transform.localPosition = new Vector3(0f, 1.6f, -1.2f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.55f, 0.85f, 1f); l.intensity = 2.2f; l.range = 8f; l.shadows = LightShadows.None;
            // The stamp waits in the ring (StoryDirector.OfferStamp asks Landmarks.TryStampSpot).
            var spot = new GameObject("PortalStampSpot");
            spot.transform.SetParent(transform, false);
            StampSpot = spot.AddComponent<VistaInfo>();
            StampSpot.HasStampSpot = true;
            StampSpot.Reachable = true;
            StampSpot.StampLocal = at + new Vector3(0f, 1.6f, 0f);
            StampSpot.GroundY = at.y;
        }

        /// <summary>Every glass full again (a fresh attempt).</summary>
        public void Refill()
        {
            foreach (var w in Waters) if (w != null) SetFull(w, true);
        }

        static void SetFull(Transform w, bool full)
        {
            var water = w.Find("Water");
            if (water != null) water.gameObject.SetActive(full);
            var glow = w.Find("WaterGlow");
            if (glow != null) glow.gameObject.SetActive(full);
        }

        public static bool IsFull(Transform w) { var water = w.Find("Water"); return water != null && water.gameObject.activeSelf; }

        /// <summary>Drinks a full glass within reach of the player's feet (local space). Returns true when one was drunk.</summary>
        public bool TryDrink(Vector3 feetLocal)
        {
            foreach (var w in Waters)
            {
                if (w == null || !IsFull(w)) continue;
                var d = w.localPosition - feetLocal; d.y = 0f;
                if (d.magnitude > 1.1f) continue;
                SetFull(w, false);
                return true;
            }
            return false;
        }

        void Update()
        {
            if (Portal == null) return;
            int i = 0;
            foreach (Transform c in Portal)
                if (c.name.StartsWith("Swirl")) c.localRotation = Quaternion.Euler(0f, 0f, Time.time * (i++ == 0 ? 40f : -25f));
            if (m_PortalMat != null) m_PortalMat.SetColor("_Color", new Color(0.5f, 0.85f, 1f, 0.45f + 0.15f * Mathf.Sin(Time.time * 2.2f)));
        }
    }

    // ====================================================================== India: the Taj Mahal picture puzzle
    /// <summary>
    /// A 6-piece picture puzzle of the Taj Mahal on a table in the garden, the real thing standing at the end of the axis.
    /// The pieces lie on the cloth either side of an empty board; pick one up and put it in its place on the board: only
    /// the right place takes it, and a piece held near its place snaps in by itself. Six in place and the picture is whole.
    /// </summary>
    public class TajJigsaw : MonoBehaviour
    {
        public const int Cols = 3, Rows = 2, Count = Cols * Rows;
        const float BoardW = 0.66f, BoardH = 0.6f;
        const float PieceW = BoardW / Cols, PieceH = BoardH / Rows;
        const float SnapDistance = 0.13f;
        readonly Grabbable[] m_Pieces = new Grabbable[Count];
        readonly Socket[] m_Slots = new Socket[Count];
        readonly Material[] m_Outline = new Material[Count];
        readonly Vector3[] m_Rest = new Vector3[Count];
        readonly float[] m_RestYaw = new float[Count];
        readonly float[] m_Aimed = new float[Count];
        Transform m_Board;
        bool m_Completing;
        public bool Solved { get; private set; }
        public bool Scrambled { get; private set; }
        public event System.Action<TajJigsaw> Completed;
        public Vector3 BoardPosition => m_Board != null ? m_Board.position : transform.position;
        public int Placed { get { int n = 0; foreach (var s in m_Slots) if (s != null && s.Current != null) n++; return n; } }
        public int Misplaced => Count - Placed;
        public Grabbable Piece(int i) => m_Pieces[i];
        public Socket Slot(int i) => m_Slots[i];

        // Slot i: column i % 3 (left to right), row i / 3 (0 = the top half of the picture, the far side of the board).
        static Vector3 SlotPos(int i) => new Vector3((i % Cols - 1) * PieceW, 0.02f, (0.5f - i / Cols) * PieceH);

        public static TajJigsaw Build(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("TajPuzzleTable");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var j = go.AddComponent<TajJigsaw>();
            j.Construct();
            return j;
        }

        void Construct()
        {
            var t = transform;
            var wood = Mats.Lit(new Color(0.42f, 0.24f, 0.14f), 0.45f, 0f, "wood_dark");
            var cloth = Mats.Lit(new Color(0.55f, 0.08f, 0.1f), 0.2f);
            var brass = Mats.Lit(new Color(0.78f, 0.6f, 0.32f), 0.7f, 0.9f, "metal_brushed");
            var backing = Mats.Lit(new Color(0.9f, 0.86f, 0.78f), 0.3f);
            // A wide table: the board in the middle, three pieces on the cloth either side.
            MeshFactory.Box("TableTop", t, new Vector3(0f, 0.8f, 0f), new Vector3(1.7f, 0.05f, 0.9f), wood, true);
            MeshFactory.Box("Cloth", t, new Vector3(0f, 0.827f, 0f), new Vector3(1.6f, 0.004f, 0.84f), cloth, false);
            foreach (var x in new[] { -0.78f, 0.78f })
                foreach (var z in new[] { -0.38f, 0.38f })
                    MeshFactory.Box("Leg", t, new Vector3(x, 0.39f, z), new Vector3(0.07f, 0.78f, 0.07f), wood, true);
            var tex = Tex.Art("taj_puzzle");
            // The board, tipped toward the player and resting on the cloth (its near edge) and a stand (its far edge).
            const float tilt = 18f;
            float rise = (BoardH / 2f + 0.04f) * Mathf.Sin(tilt * Mathf.Deg2Rad);
            m_Board = new GameObject("Board").transform;
            m_Board.SetParent(t, false);
            m_Board.localPosition = new Vector3(0f, 0.83f + rise + 0.022f, 0.08f);
            m_Board.localRotation = Quaternion.Euler(-tilt, 0f, 0f);
            MeshFactory.Box("Frame", m_Board, new Vector3(0f, -0.005f, 0f), new Vector3(BoardW + 0.08f, 0.03f, BoardH + 0.08f), brass, false);
            MeshFactory.Box("Bed", m_Board, new Vector3(0f, 0.008f, 0f), new Vector3(BoardW + 0.02f, 0.01f, BoardH + 0.02f), Mats.Lit(new Color(0.1f, 0.08f, 0.07f), 0.3f), false);
            MeshFactory.Box("Stand", t, new Vector3(0f, 0.93f, 0.33f), new Vector3(0.5f, 0.2f, 0.05f), wood, false);
            for (int i = 0; i < Count; i++)
            {
                int c = i % Cols, r = i / Cols;
                var uv = new Rect(c / (float)Cols, 1f - (r + 1) / (float)Rows, 1f / Cols, 1f / Rows);
                // The empty place on the board: a faint ghost of its part of the picture, so it is clear where it goes.
                var slotGo = new GameObject("Place" + i);
                slotGo.transform.SetParent(m_Board, false);
                slotGo.transform.localPosition = SlotPos(i);
                m_Outline[i] = Mats.UnlitInstance(new Color(1f, 1f, 1f, 0.16f), tex, true, 0.9f);
                ImageQuad.Make("Hint", slotGo.transform, new Vector3(0f, 0.004f, 0f), PieceW - 0.01f, PieceH - 0.01f, uv, m_Outline[i], true);
                var sc = slotGo.AddComponent<BoxCollider>();
                sc.isTrigger = true;
                sc.size = new Vector3(PieceW, 0.06f, PieceH);
                var sock = slotGo.AddComponent<TajSlot>();
                sock.Accepts.Add("taj_piece_" + i);
                sock.LockWhenFilled = true;
                sock.Prompt = "Put the piece here";
                sock.MaxDistance = 2.4f;
                sock.Filled += OnFilled;
                m_Slots[i] = sock;

                // The piece itself.
                var piece = new GameObject("TajPiece" + i);
                piece.transform.SetParent(t, false);
                MeshFactory.Box("Backing", piece.transform, new Vector3(0f, 0.006f, 0f), new Vector3(PieceW - 0.004f, 0.012f, PieceH - 0.004f), backing, false);
                ImageQuad.Make("Image", piece.transform, new Vector3(0f, 0.0135f, 0f), PieceW - 0.01f, PieceH - 0.01f, uv, Mats.UnlitInstance(Color.white, tex, false, 0.95f), true);
                piece.AddComponent<BoxCollider>().size = new Vector3(PieceW, 0.03f, PieceH);
                var g = PropLibrary.MakeGrabbable(piece, "taj_piece_" + i, "Pick up this piece");
                g.UsePhysicsOnRelease = false;          // a dropped piece lies where it is put, never rolls off the table
                g.DesktopHoldEuler = new Vector3(-60f, 0f, 0f); // held up toward the eyes, the picture facing the player
                g.HoldLocalEuler = new Vector3(-60f, 0f, 0f);
                m_Pieces[i] = g;
            }
            // Where the pieces wait: three on the cloth to the left of the board, three to the right, shuffled.
            var rest = new[] { new Vector3(-0.62f, 0.83f, 0.22f), new Vector3(-0.62f, 0.83f, -0.12f), new Vector3(-0.45f, 0.83f, -0.3f),
                               new Vector3(0.62f, 0.83f, 0.22f), new Vector3(0.62f, 0.83f, -0.12f), new Vector3(0.45f, 0.83f, -0.3f) };
            for (int i = 0; i < Count; i++) { m_Rest[i] = rest[i]; m_RestYaw[i] = (i % 2 == 0 ? -1f : 1f) * (8f + 6f * i); }
            // The postcard of the finished picture, propped on the stand behind the board.
            var card = new GameObject("ReferenceCard").transform;
            card.SetParent(t, false);
            card.localPosition = new Vector3(0.62f, 0.95f, 0.36f);
            card.localRotation = Quaternion.Euler(15f, -15f, 0f);
            MeshFactory.Box("CardBack", card, new Vector3(0f, 0f, 0.004f), new Vector3(0.21f, 0.21f, 0.006f), Mats.Lit(new Color(0.95f, 0.93f, 0.88f), 0.3f), false);
            ImageQuad.Make("CardImage", card, Vector3.zero, 0.19f, 0.19f, new Rect(0, 0, 1, 1), Mats.Unlit(Color.white, tex, false, 0.95f), false);
            var lg = new GameObject("TableLamp");
            lg.transform.SetParent(t, false);
            lg.transform.localPosition = new Vector3(0f, 1.9f, -0.4f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.82f, 0.6f); l.intensity = 1.2f; l.range = 3f; l.shadows = LightShadows.None;
            Scramble();
        }

        /// <summary>Every piece back on the cloth beside the board (a fresh puzzle).</summary>
        public void Scramble()
        {
            Solved = false;
            Scrambled = true;
            m_Completing = false;
            var order = new int[Count];
            for (int i = 0; i < Count; i++) order[i] = i;
            for (int i = Count - 1; i > 0; i--) { int k = Random.Range(0, i + 1); (order[i], order[k]) = (order[k], order[i]); }
            for (int i = 0; i < Count; i++)
            {
                var g = m_Pieces[i];
                if (g == null) continue;
                if (g.Holder != null) g.Holder.ForceRelease(false);
                if (g.InSocket != null) g.InSocket.Release(g);
                g.transform.SetParent(transform, false);
                g.transform.localPosition = m_Rest[order[i]];
                g.transform.localRotation = Quaternion.Euler(0f, m_RestYaw[order[i]], 0f);
                // The cell is built while hidden, before the pieces' Awake has cached their bodies.
                var rb = g.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; rb.detectCollisions = true; rb.interpolation = RigidbodyInterpolation.None; rb.position = g.transform.position; rb.rotation = g.transform.rotation; }
                g.Usable = true;
                g.SetHome();
                m_Outline[i].SetColor("_Color", new Color(1f, 1f, 1f, 0.16f));
            }
        }

        /// <summary>The picture whole (already solved on an earlier visit).</summary>
        public void ShowSolved()
        {
            for (int i = 0; i < Count; i++)
            {
                var g = m_Pieces[i];
                if (g == null || m_Slots[i].Current == g) continue;
                if (g.Holder != null) g.Holder.ForceRelease(false);
                if (g.InSocket != null) g.InSocket.Release(g);
                m_Slots[i].Put(g);
            }
            Solved = true;
            Scrambled = true;
        }

        void OnFilled(Socket s, Grabbable g)
        {
            int i = System.Array.IndexOf(m_Slots, s);
            if (i >= 0) m_Outline[i].SetColor("_Color", new Color(1f, 1f, 1f, 0f));
            AudioDirector.I?.Play("drop_soft", s.transform.position, 0.7f, 1.3f);
            if (Solved || m_Completing) return;
            if (Placed < Count) { Hud.I?.ShowCaption("click: the piece fits (" + Placed + "/" + Count + ")", 1.6f); return; }
            m_Completing = true;
            Solved = true;
            Completed?.Invoke(this);
        }

        void Update()
        {
            // A piece held close to its own place snaps in by itself (desktop: hold it over its place; VR: bring it near).
            for (int i = 0; i < Count; i++)
            {
                var g = m_Pieces[i];
                if (g == null || g.Holder == null || m_Slots[i].Current != null) continue;
                // Measured from the hand, not the piece: a held body trails the hand by a frame or two.
                var holdAt = g.Holder.HoldPoint != null ? g.Holder.HoldPoint.position : g.transform.position;
                var near = Vector3.Distance(holdAt, m_Slots[i].transform.position) < SnapDistance;
                bool aimed = g.Holder.Focused == m_Slots[i];
                m_Aimed[i] = aimed ? m_Aimed[i] + Time.deltaTime : 0f;
                // Aimed at its own place for a moment (the only place that takes it): it snaps in without a press.
                if (near || m_Aimed[i] > 0.35f) { m_Aimed[i] = 0f; g.Holder.PlaceInto(m_Slots[i]); }
                else if (aimed) m_Outline[i].SetColor("_Color", new Color(1f, 0.95f, 0.6f, 0.45f + 0.15f * Mathf.Sin(Time.time * 8f)));
                else m_Outline[i].SetColor("_Color", new Color(1f, 1f, 1f, 0.16f));
            }
        }
    }

    /// <summary>A place on the Taj Mahal board: takes only its own piece.</summary>
    public class TajSlot : Socket
    {
        public override string GetPrompt(Interactor who) => "Put the piece here";
    }
}
