using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>Morocco — Memory Match in the shop that remembers. Match the four pairs of cards on the counter (glasses,
    /// cameras, passports, stamps), recover the calibration insert from the drawer that opens, read the optician's notes
    /// in the glasses case, and learn why the HOME stamp is forged.</summary>
    public class MoroccoEncounter : Encounter
    {
        ShopCell S => (ShopCell)Cell;
        // Read-only state for the guide (Guide/GuideBook.Morocco.cs) and the tests.
        public MemoryMatch Memory => m_Game;
        public Grabbable Insert => m_Insert;
        public Socket CaseSocket => m_CaseSocket;
        public bool DrawerOpen => m_DrawerOpen;
        public bool NotesRead => m_NotesRead;
        MemoryMatch m_Game;
        Grabbable m_Insert;
        Socket m_CaseSocket;
        GameObject m_Props, m_Case;
        LightCone m_SafeLight;
        bool m_DrawerOpen, m_NotesRead, m_ThreatStarted;
        Vector3 m_DrawerHome;
        bool m_DrawerHomeSet;

        public override string Objective =>
            !m_DrawerOpen ? "Memory match on the counter: flip two cards at a time and find all four pairs (glasses, camera, passport, stamp)." :
            !m_NotesRead ? "Take the calibration insert from the drawer and fit it inside the glasses case." :
            "Verify the shop's arrival trace on the counter.";

        public override void OnEnter(bool firstVisit)
        {
            Game.Passenger?.Despawn();
            if (m_SafeLight == null)
            {
                var go = new GameObject("CounterLampLight");
                go.transform.SetParent(S.transform, false);
                go.transform.localPosition = new Vector3(0, 2.35f, -0.4f);
                go.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                m_SafeLight = go.AddComponent<LightCone>();
                m_SafeLight.Length = 2.6f; m_SafeLight.StartRadius = 0.3f; m_SafeLight.EndRadius = 1.9f;
                m_SafeLight.Sunlight = false; m_SafeLight.OwnerCell = Cell;
                m_SafeLight.Build(new Color(1f, 0.78f, 0.5f), 0.25f);
                m_SafeLight.SetStrength(0.7f);
                // Gameplay light volume only: its shell read as giant translucent sheets from the arrival point. The real
                // lamp and a soft pool on the counter carry the look.
                m_SafeLight.SetShaftVisible(false);
                // The whole lamp-lit area around the counter is safe (playtest: with sudden death the narrow cone was not).
                var poolGo = new GameObject("CounterLightPool");
                poolGo.transform.SetParent(S.transform, false);
                poolGo.transform.localPosition = new Vector3(0f, 0f, -0.4f);
                var lightPool = poolGo.AddComponent<LightPool>();
                lightPool.Radius = 2.5f; lightPool.Height = 3f; lightPool.OwnerCell = Cell;
                var pool = MeshFactory.Quad("LampPool", S.transform, 1.6f, 1.2f, Mats.Unlit(new Color(1f, 0.78f, 0.5f, 0.22f), Tex.SoftDot, true), new Vector3(0f, 1.046f, 0.1f), Quaternion.Euler(90, 0, 0));
                pool.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // The old puzzle's outlines on the counter are gone: the cards lie there now.
            foreach (var mark in S.CounterMarks) if (mark != null) mark.gameObject.SetActive(false);
            if (Game.Campaign.IsVerified(CountryId.Morocco))
            {
                S.Trace.Reveal(false);
                Hud.I?.SetObjective("The real way home is the one you can prove. Eleven seals, then Berlin.", false);
                return;
            }
            Narration.I?.Say("mor_01", null, false, null, false, true);
            if (m_Props != null && (m_DrawerOpen || m_NotesRead || (m_Game != null && m_Game.Flips > 0))) ResetPuzzle();
            if (m_Props == null) BuildPuzzle();
            Hud.I?.ShowToast("Eight cards lie face down on the counter. Flip two at a time and match all four pairs.", 4.5f);
            Hud.I?.SetObjective(Objective, false);
            m_ThreatStarted = false;
            SpawnObserver();
        }

        /// <summary>
        /// The shop is cached for the session, so a new story (or a loaded save, or a retry) that has not verified Morocco
        /// would find the game already played. Put it all back: remove the cards, the insert and the case, close the
        /// drawer, hide the trace.
        /// </summary>
        void ResetPuzzle()
        {
            StopAllCoroutines();
            if (m_Insert != null) Destroy(m_Insert.gameObject);
            if (m_Props != null) Destroy(m_Props);
            if (m_DrawerHomeSet) S.DrawerRoot.localPosition = m_DrawerHome;
            S.Trace.Hide();
            m_Props = null; m_Case = null; m_Insert = null; m_CaseSocket = null; m_Game = null;
            m_DrawerOpen = false; m_NotesRead = false;
        }

        void BuildPuzzle()
        {
            if (!m_DrawerHomeSet) { m_DrawerHome = S.DrawerRoot.localPosition; m_DrawerHomeSet = true; }
            m_Props = new GameObject("MoroccoProps");
            m_Props.transform.SetParent(S.transform, false);
            // A baize board on the counter's right-hand half (Cells/MemoryMatch.cs), clear of the display case, the trace
            // and the spot where the glasses case is set down.
            m_Game = MemoryMatch.Build(m_Props.transform, new Vector3(0.5f, 1.056f, 0.13f));
            m_Game.FirstFlip += _ => { if (IsLive && !m_ThreatStarted) StartThreat(); };
            m_Game.PairChecked += OnPairChecked;
            m_Game.Completed += _ => OnAllPairs();
            Cell.Recache();
        }

        void SpawnObserver()
        {
            var pc = Game.Passenger;
            pc.RetreatPoint = S.transform.TransformPoint(new Vector3(-16f, 0f, -1f));
            pc.RetreatSeconds = 15f;
            pc.Spawn(Cell, new Vector3(-13f, 0.01f, -0.9f), 90f, PassengerState.Observe, Disguise.OpticianCoat);
            pc.Threatening = true;
            pc.WalkSpeed = 0.6f;
        }

        void StartThreat()
        {
            m_ThreatStarted = true;
            if (Game.Passenger != null && Game.Passenger.Visible)
            {
                Game.Passenger.SetState(PassengerState.Stalk);
                Hud.I?.ShowCaption("footsteps on glass, somewhere down the aisles", 3f);
                Narration.I?.Say("tut_danger", null, true, null, false, true);
            }
        }

        void OnPairChecked(MemoryMatch m, int kind, bool matched)
        {
            if (!IsLive || m_DrawerOpen) return;
            if (matched)
            {
                if (!m.Solved) Hud.I?.ShowToast("A pair of " + Plural(kind) + "! " + m.MatchedPairs + " / " + MemoryMatch.Pairs, 2.2f);
                return;
            }
            // A wrong guess: the optician comes toward the counter, to the edge of the lamplight.
            var pc = Game.Passenger;
            if (pc != null && pc.Visible && m_ThreatStarted && pc.State == PassengerState.Stalk)
            {
                pc.Investigate(S.transform.TransformPoint(new Vector3(0.5f, 0f, -2.2f)), 4f);
                AudioDirector.I?.Play("pass_whisper", pc.ChestPosition, 0.6f);
                Hud.I?.ShowCaption("glass crunches closer. Stay in the lamplight", 2.5f);
            }
        }

        static string Plural(int kind) => kind == 0 ? "glasses" : kind == 1 ? "cameras" : kind == 2 ? "passports" : "stamps";

        void OnAllPairs()
        {
            if (m_DrawerOpen) return;
            m_DrawerOpen = true;
            StartCoroutine(OpenDrawer());
        }

        IEnumerator OpenDrawer()
        {
            AudioDirector.I?.Play("drawer_open", S.DrawerRoot.position, 0.9f);
            var start = S.DrawerRoot.localPosition;
            float t = 0f;
            while (t < 1f) { t += Time.deltaTime / 0.8f; S.DrawerRoot.localPosition = start + new Vector3(0, 0, -0.3f) * Mathf.SmoothStep(0, 1, t); yield return null; }
            // The calibration insert: a clear acetate sheet with registration marks.
            var go = new GameObject("CalibrationInsert");
            go.transform.SetParent(m_Props.transform, false);
            go.transform.position = S.DrawerRoot.position + Vector3.up * 0.06f;
            var tex = Tex.Art("calibration_insert");
            var quad = MeshFactory.Quad("Sheet", go.transform, 0.11f, 0.15f, Mats.Unlit(new Color(0.85f, 1f, 0.95f, 0.85f), tex, true, 1.2f), Vector3.zero, Quaternion.Euler(90, 0, 0));
            go.AddComponent<BoxCollider>().size = new Vector3(0.12f, 0.02f, 0.16f);
            m_Insert = PropLibrary.MakeGrabbable(go, "calibration_insert", "Take the calibration insert", true);
            m_Insert.SetHome();
            Hud.I?.ShowToast("All four pairs! A drawer slides open by itself.", 2.5f);

            // The glasses case Saeed has carried since Marrakech, set down on the counter.
            m_Case = PropLibrary.GlassesCase(m_Props.transform, true);
            m_Case.transform.position = S.CounterTop.position + new Vector3(-0.3f, 0f, 0.1f);
            m_Case.transform.rotation = Quaternion.Euler(0, 180f, 0);
            var sockGo = new GameObject("InsertSocket");
            sockGo.transform.SetParent(m_Case.transform, false);
            sockGo.transform.localPosition = new Vector3(0, 0.016f, 0);
            var col = sockGo.AddComponent<BoxCollider>(); col.isTrigger = true; col.size = new Vector3(0.18f, 0.08f, 0.1f);
            m_CaseSocket = sockGo.AddComponent<Socket>();
            m_CaseSocket.Accepts.Add("calibration_insert");
            m_CaseSocket.LockWhenFilled = true;
            m_CaseSocket.Prompt = "Fit the insert inside the glasses case";
            m_CaseSocket.Filled += (s, g) => StartCoroutine(ReadNotes());
            Cell.Recache();
            Hud.I?.SetObjective(Objective, false);
        }

        IEnumerator ReadNotes()
        {
            // The insert exposes the optician's hidden notes in the case lining.
            Hud.I?.ShowToast("Hidden handwriting appears in the case lining.", 3f);
            AudioDirector.I?.Play("pp_seam_shimmer", m_Case.transform.position, 0.9f);
            Game.Campaign.SetFlag(StoryFlags.OpticianNotesRead);
            bool done = false;
            Narration.I?.Say("opt_01");
            Narration.I?.Say("opt_02");
            Narration.I?.Say("opt_03");
            Narration.I?.Say("opt_04", null, false, () => done = true);
            while (!done) yield return null;
            m_NotesRead = true;
            S.Trace.Reveal();
            Hud.I?.SetObjective(Objective, false);
        }

        public void OnVerified()
        {
            if (Game.Passenger != null && Game.Passenger.Visible) Game.Passenger.SetState(PassengerState.Retreat);
            Hud.I?.ShowToast("Calibration insert recovered. It will show which home is real.", 4f);
        }

        void Update()
        {
            if (!IsLive) return;
            if (S.Lamp != null) S.Lamp.intensity = 1.9f + Mathf.PerlinNoise(Time.time * 3f, 0.2f) * 0.6f;
        }

        public override void ResetLocal()
        {
            Game.Passenger?.Despawn();
            m_ThreatStarted = false;
        }
    }
}
