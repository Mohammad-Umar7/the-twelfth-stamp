using System.Collections;
using System.Linq;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// The story opens at home in Abu Dhabi. Mum's voicemail plays; going through the bag turns up the glasses case,
    /// which brings back the Marrakech corner shop (a flashback, see PrologueChapter); back in the present the only
    /// thing left to do is put the glasses on, and the room quietly stops being the room: the fridge hum stops, a
    /// recorded word repeats, the reflection becomes wrong. Not a jump scare; the Margin arrives by subtraction.
    /// </summary>
    public static class HomecomingChapter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => Homecoming.Routine = Run;

        static bool s_Worn;

        public static IEnumerator Run()
        {
            var story = StoryDirector.I;
            var c = Game.Campaign;
            story.TearsAllowed = false;
            story.ThreatAllowed = false;
            story.ShowVitality = false;
            Game.Passport?.Close();
            Game.Passport.Available = false; // the passport matters once the Margin begins
            s_Worn = false;

            yield return story.EnterCell(CellId.ApartmentNormal, false, "Home");
            var apt = (ApartmentCell)Game.Cells.Active;
            // Start beside the open bag, facing it.
            var bagFront = apt.Suitcase.position + new Vector3(0.05f, 0f, -1.3f);
            Game.Rig.PlaceAt(new Vector3(bagFront.x, apt.transform.position.y, bagFront.z), Quaternion.LookRotation(apt.Suitcase.position - bagFront).eulerAngles.y);
            if (Game.Rig is DesktopRig desk) desk.SetPitch(45f);
            Hud.I?.ShowTitleCard("ABU DHABI", "Home", 3.5f);
            Narration.I?.Say("home_01", null, false, null, false, true);
            c.SetFlag(StoryFlags.HomeArrived);

            // What is still in the bag: the camera, the passport and the glasses case from Marrakech.
            var props = new GameObject("HomecomingProps").transform;
            props.SetParent(apt.transform, false);
            var cam = PropLibrary.VintageCamera(props).transform;
            cam.position = apt.Suitcase.position + new Vector3(-0.18f, 0.22f, 0.02f);
            var pass = PropLibrary.PassportClosed(props).transform;
            pass.position = apt.Suitcase.position + new Vector3(0.05f, 0.21f, -0.05f);
            var caseGo = PropLibrary.GlassesCase(props, false);
            caseGo.transform.position = apt.Suitcase.position + new Vector3(0.22f, 0.21f, 0.05f);
            var hall = new GameObject("HallLight").AddComponent<Light>();
            hall.transform.SetParent(props, false);
            hall.transform.position = apt.Suitcase.position + new Vector3(0f, 1.7f, -0.35f);
            hall.type = LightType.Point; hall.range = 3f; hall.intensity = 1.4f; hall.color = new Color(1f, 0.82f, 0.62f);
            hall.shadows = LightShadows.Soft;
            apt.Recache();

            // Mum's voicemail plays by itself (the Margin will replay it wrong later).
            yield return new WaitForSeconds(2.5f);
            AudioDirector.I?.Play("phone_buzz", apt.PhoneRoot.position, 0.9f);
            Hud.I?.ShowCaption("your phone buzzes on the table", 2.5f);
            apt.PhoneScreen.SetColor("_Color", new Color(0.55f, 0.75f, 0.85f, 1f));
            apt.PhoneScreen.SetFloat("_Intensity", 1.4f);
            yield return new WaitForSeconds(1.4f);
            AudioDirector.I?.Play("voicemail_beep", apt.PhoneRoot.position, 0.8f);
            bool heard = false;
            Narration.I?.Say("mom_01", apt.PhoneRoot, false, () => heard = true, true);
            for (float t = 0f; !heard && t < 9f; t += Time.deltaTime) yield return null;

            // The one thing to do: go through the bag.
            var bagUse = apt.Suitcase.gameObject.AddComponent<Interactable>();
            bagUse.Prompt = "Go through your bag";
            bool opened = false;
            bagUse.OnUse = _ => opened = true;
            Hud.I?.SetObjective("Go through your bag.", false);
            while (!opened) yield return null;
            UnityEngine.Object.Destroy(bagUse);
            Hud.I?.SetObjective("", false);

            // Camera and passport come out and are set aside; then the glasses case, into the light.
            AudioDirector.I?.Play("grab", apt.Suitcase.position + Vector3.up * 0.3f, 0.8f);
            yield return Lift(cam, apt.Suitcase.position + new Vector3(-0.62f, 0.02f, -0.15f), Quaternion.Euler(0f, 35f, 0f), 0.6f);
            yield return Lift(pass, apt.Suitcase.position + new Vector3(-0.5f, 0.01f, -0.42f), Quaternion.Euler(0f, -20f, 0f), 0.6f);
            var caseSpot = apt.Suitcase.position + new Vector3(0.05f, 0.24f, -0.22f);
            yield return Lift(caseGo.transform, caseSpot, Quaternion.Euler(0f, 180f, 0f), 0.8f);
            c.SetFlag(StoryFlags.Unpacked);
            Game.SaveNow();
            yield return new WaitForSeconds(0.8f);

            // ...and he remembers where it came from.
            if (!c.HasFlag(StoryFlags.PrologueDone)) yield return PrologueChapter.Flashback();
            // Present: he holds the case up in front of him and opens it. The only thing left is to put them on.
            var head = Game.Rig.Head;
            var fwd = head.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            var held = head.position + fwd * 0.55f + Vector3.down * 0.2f;
            yield return Lift(caseGo.transform, held, Quaternion.LookRotation(fwd, Vector3.up), 0.7f);
            if (Game.Rig is DesktopRig desk2) desk2.SetPitch(Mathf.Atan2(0.2f, 0.55f) * Mathf.Rad2Deg);
            var lamp = new GameObject("CaseLight").AddComponent<Light>();
            lamp.transform.SetParent(props, false);
            lamp.transform.position = head.position + fwd * 0.25f + Vector3.up * 0.15f;
            lamp.type = LightType.Point; lamp.range = 1.1f; lamp.intensity = 0.14f; lamp.color = new Color(1f, 0.9f, 0.78f);
            lamp.shadows = LightShadows.None;
            yield return new WaitForSeconds(0.4f);
            AudioDirector.I?.Play("case_open", caseGo.transform.position, 0.8f);
            PropLibrary.SetCaseOpen(caseGo, true);
            var glassesGo = PropLibrary.Glasses(props, true);
            glassesGo.transform.position = caseGo.transform.position + Vector3.up * 0.03f;
            glassesGo.transform.rotation = caseGo.transform.rotation * Quaternion.Euler(-90, 0, 0);
            var col = glassesGo.AddComponent<BoxCollider>();
            col.size = new Vector3(0.2f, 0.1f, 0.12f); // the whole open case is the target
            var wear = glassesGo.AddComponent<Interactable>();
            wear.Prompt = "Put on the glasses";
            wear.OnUse = _ => s_Worn = true;
            apt.Recache();
            AudioDirector.I?.Play("glasses_unfold", glassesGo.transform.position, 0.7f);
            Narration.I?.Say("home_02", null, false, null, false, true);
            Hud.I?.SetObjective("Put on the glasses.", false);
            while (!s_Worn) yield return null;
            // He sets the empty case back on the bag as the room starts to change.
            UnityEngine.Object.Destroy(lamp.gameObject);
            caseGo.transform.position = caseSpot;
            caseGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            PropLibrary.SetCaseOpen(caseGo, false);
            yield return WearAndCorrupt(apt, glassesGo);
        }

        /// <summary>Moves a prop in a short arc (unpacking happens on its own; the player only watches).</summary>
        static IEnumerator Lift(Transform t, Vector3 to, Quaternion rot, float seconds)
        {
            var from = t.position; var r0 = t.rotation;
            for (float k = 0f; k < 1f; )
            {
                k = Mathf.Min(1f, k + Time.deltaTime / seconds);
                float e = Mathf.SmoothStep(0f, 1f, k);
                t.position = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 0.12f;
                t.rotation = Quaternion.Slerp(r0, rot, e);
                yield return null;
            }
        }

        /// <summary>
        /// The glasses go on, and the room stops being the room. They rise from the case to the eyes as an object (the camera
        /// stays where the player has it); the fridge dies and the voicemail starts repeating; the dimension shift tears the
        /// room apart in Egypt's light and it reassembles as the Margin's copy of itself; the seam in the kitchen wakes: it splits open on Egypt by itself, sunlight
        /// and the desert through the gap, then pulls shut to a glowing scar, the way on that the tutorial then teaches.
        /// (Playtest, 18 Oct: the Between's causeway run and the red shift into it were cut; "when I put on the glasses the
        /// seam should start to Egypt with animation".)
        /// </summary>
        static IEnumerator WearAndCorrupt(ApartmentCell apt, GameObject glasses)
        {
            var c = Game.Campaign;
            Game.InputLocks++;
            yield return GlassesOn(glasses.transform);
            c.SetFlag(StoryFlags.GlassesWorn);
            Game.SaveNow();
            Hud.I?.SetObjective("", false);

            // Subtraction first: the fridge stops, a recorded word repeats.
            AudioDirector.I?.Play("fridge_stop", apt.FridgeRoot.position, 0.9f);
            AudioDirector.I?.SetAmbience("amb_margin", 3f, 0.5f);
            Hud.I?.ShowCaption("the fridge stops humming", 2.5f);
            yield return new WaitForSeconds(Game.IsAutoTest ? 0.4f : 1.6f);
            AudioDirector.I?.Play("mom_01_glitch", apt.PhoneRoot.position, 0.9f, 1f, Bus.Voice);
            Hud.I?.ShowCaption("the voicemail repeats: \"unpacked... unpacked...\"", 3.5f);
            yield return new WaitForSeconds(Game.IsAutoTest ? 0.5f : 2.4f);

            // The room tears apart (the dimension shift travel uses, in Egypt's light: playtest, 18 Oct, "add the animation
            // that was there when I was teleporting") and reassembles as the Margin's copy, the player facing the kitchen seam.
            ApartmentCell corrupt = null;
            SeamAnchor seam = null;
            yield return DimensionShift.Run(() =>
            {
                corrupt = (ApartmentCell)Game.Cells.Activate(CellId.ApartmentCorrupt);
                corrupt.RefreshProbe();
                Game.Cells.Prepare(CellId.Egypt); // the seam opens on it next: build it behind the slam
                seam = KitchenSeam(corrupt);
                FaceSeam(seam, 1.3f);
                c.SetFlag(StoryFlags.SeamRevealed);
            }, Game.IsAutoTest ? 2f : 4.5f, CellId.Egypt);
            yield return SeamWakes(seam);
            Game.InputLocks--;

            Game.Passport.Available = true;
            c.MarkVisited(CountryId.UAE); // UAE is verified later, by the encounter
            Game.Passport.Refresh();
            Game.SaveCheckpoint(CellId.ApartmentCorrupt);
            StoryDirector.I.TearsAllowed = true;
            StoryDirector.I.ThreatAllowed = true;
            StoryDirector.I.ShowVitality = true;
            if (corrupt.Encounter != null) { corrupt.Encounter.Entered = true; corrupt.Encounter.OnEnter(true); }
        }

        /// <summary>The Margin apartment's kitchen seam, where the first tear opens.</summary>
        public static SeamAnchor KitchenSeam(ApartmentCell cell) =>
            cell.Seams.FirstOrDefault(s => s != null && s.Id == "kitchen") ?? cell.Seams.FirstOrDefault(s => s != null);

        /// <summary>Stands the player <paramref name="distance"/> in front of a seam, facing it. Only ever behind a dark fade
        /// (in VR it turns the rig, like a snap turn).</summary>
        public static void FaceSeam(SeamAnchor seam, float distance)
        {
            var stand = seam.FloorPoint + seam.Forward * distance;
            var look = seam.Center - stand;
            look.y = 0f;
            Game.Rig.PlaceAt(stand, Quaternion.LookRotation(look).eulerAngles.y);
            if (Game.Rig is DesktopRig desk) desk.SetPitch(Mathf.Atan2(DesktopRig.EyeHeight - seam.CenterHeight, distance) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// The seam wakes: the scar flares in Egypt's light, splits open on Egypt by itself (a real tear held by the story:
        /// the sunlight and the desert through the gap, a wide slit, never wide enough for anything to reach through), stays a
        /// few breaths, and pulls shut to a glowing scar. It counts for nothing: no visit, no tutorial step, no readout.
        /// </summary>
        public static IEnumerator SeamWakes(SeamAnchor seam)
        {
            float pace = Game.IsAutoTest ? 0.35f : 1f; // tests run the beats faster
            for (float t = 0f; t < 1f; t += Time.deltaTime / (1.2f * pace)) { seam.Flare = Mathf.SmoothStep(0f, 1f, t); yield return null; }
            seam.Flare = 1f;
            Hud.I?.ShowCaption("the seam splits open: sunlight, and sand on the wind", 4f);
            var tear = Game.Tears.OpenForStory(seam, CountryDatabase.Get(CountryId.Egypt));
            if (tear != null)
            {
                const float Widest = 0.36f; // a wide slit: under the window size
                for (float t = 0f; t < 1f && tear != null && !tear.Closing; t += Time.deltaTime / (2.2f * pace))
                {
                    tear.Aperture = Mathf.Lerp(TearController.SlitThreshold, Widest, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
                yield return new WaitForSeconds(2.4f * pace);
                Game.Tears.Close(false);
                for (float t = 0f; t < 3f && Game.Tears.Active != null; t += Time.deltaTime) yield return null;
            }
            for (float t = 0f; t < 1f; t += Time.deltaTime / (1.2f * pace)) { seam.Flare = 1f - Mathf.SmoothStep(0f, 1f, t); yield return null; }
            seam.Flare = 0f;
        }

        /// <summary>
        /// The glasses rise from the open case to the eyes as an object, turning to face the way the player looks, and
        /// settle with a click: on desktop just in front of the eyes, so the rims and bridge frame the view for a moment; in VR
        /// a hand's width from the face (nothing is drawn against the eyes). The camera is never moved.
        /// </summary>
        public static IEnumerator GlassesOn(Transform glasses)
        {
            var head = Game.Rig.Head;
            bool vr = Game.Mode == GameMode.VR;
            float stop = vr ? 0.24f : 0.058f, drop = vr ? 0.02f : 0.006f;
            var from = glasses.position;
            var r0 = glasses.rotation;
            AudioDirector.I?.Play("glasses_unfold", from, 0.7f);
            const float Seconds = 1.5f;
            for (float k = 0f; k < 1f; )
            {
                k = Mathf.Min(1f, k + Time.deltaTime / Seconds);
                float e = Mathf.SmoothStep(0f, 1f, k);
                var to = head.position + head.forward * stop - head.up * drop;
                glasses.position = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 0.08f;
                glasses.rotation = Quaternion.Slerp(r0, Quaternion.LookRotation(-head.forward, head.up), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.4f)));
                yield return null;
            }
            AudioDirector.I?.Play("case_close", head.position, 0.35f, 1.6f);
            for (float t = 0f; t < (vr ? 0.25f : 0.9f); t += Time.deltaTime)
            {
                glasses.position = head.position + head.forward * stop - head.up * drop;
                glasses.rotation = Quaternion.LookRotation(-head.forward, head.up);
                yield return null;
            }
            glasses.gameObject.SetActive(false);
        }
    }

    /// <summary>UAE — Whack-a-Ghost. Little sheet ghosts pop out of the furniture in the Margin's copy of the apartment:
    /// stamp ten of them (Cells/GhostWhack.cs) and the homecoming seal is verified. Then the first threat steps out of the
    /// bathroom and the tutorial teaches the way out: open the passport, a tear, widen it to a doorway, step through.</summary>
    public class UaeEncounter : Encounter
    {
        ApartmentCell A => (ApartmentCell)Cell;
        bool m_ThreatStarted, m_Escaping;
        // Read-only state for the guide (Guide/GuideBook.Uae.cs).
        public GhostWhack Ghosts => A.Ghosts;
        public int Stamped => A.Ghosts != null ? A.Ghosts.Hits : 0;
        public bool ThreatStarted => m_ThreatStarted;
        public bool Escaping => m_Escaping;
        float m_ReflectionAlpha;
        // Where the apartment's first tear leads: Egypt in the story, the level's first country in a level run.
        static string FirstStop => Game.Campaign != null && Game.Campaign.IsLevelRun && Game.Campaign.NextRecommended() != null ? Game.Campaign.NextRecommended().DisplayName : "Egypt";

        public override string Objective => Game.Campaign.IsVerified(CountryId.UAE)
            ? "Open a tear to " + FirstStop + ", widen it to a doorway and step through."
            : "Whack-a-ghost! Stamp the ghosts popping out of the furniture: look at one while it is up and press " + Hud.KeyTag(Intent.Interact) +
              ".   Ghosts stamped " + Stamped + " / " + GhostWhack.HitsToWin;

        public override void OnEnter(bool firstVisit)
        {
            var c = Game.Campaign;
            m_ThreatStarted = false;
            m_Escaping = false;
            A.ReflectionMat.SetColor("_Color", new Color(0.75f, 0.75f, 0.7f, 0.16f));
            A.ReflectionFigureMat.SetColor("_Color", new Color(0.03f, 0.035f, 0.04f, 0f));
            A.Trace.Hide(); // the ghosts took the genuine note's place: stamping them verifies the seal
            Game.Tears.Opened -= OnOpened; Game.Tears.Opened += OnOpened;
            Game.Tears.Sealed -= OnSealed; Game.Tears.Sealed += OnSealed;
            Game.Passenger?.Despawn();
            var ghosts = A.Ghosts;
            ghosts.Stamped -= OnStamped; ghosts.Stamped += OnStamped;
            ghosts.Completed -= OnCleared; ghosts.Completed += OnCleared;
            bool verified = c.IsVerified(CountryId.UAE);
            if (verified) ghosts.Stop();
            else
            {
                Tutorial.I?.End(); // the objective counts the ghosts; the tutorial checklist comes with the way out
                ghosts.Begin(firstVisit ? 2f : 1.2f);
                Hud.I?.ShowToast("Ghosts are popping out of the furniture! Stamp ten of them: look at one while it is up and press " + Hud.KeyTag(Intent.Interact) + ".", 5f);
            }
            if (!c.HasFlag(StoryFlags.TutorialDone) && verified) BeginEscape();
            else Hud.I?.SetObjective(Objective, false);
        }

        public override void OnExit()
        {
            Game.Tears.Opened -= OnOpened;
            Game.Tears.Sealed -= OnSealed;
            if (A.Ghosts != null)
            {
                A.Ghosts.Stamped -= OnStamped;
                A.Ghosts.Completed -= OnCleared;
                A.Ghosts.Stop();
            }
            if (m_Escaping && Game.Campaign.IsVerified(CountryId.UAE))
            {
                Game.Campaign.SetFlag(StoryFlags.TutorialDone);
                Tutorial.I?.End();
            }
        }

        void OnStamped(GhostWhack g, GhostSpot s)
        {
            if (!IsLive) return;
            Hud.I?.SetObjective(Objective, false);
            Hud.I?.ShowCaption("STAMPED!   " + g.Hits + " / " + GhostWhack.HitsToWin, 1.4f);
            if (g.Hits == GhostWhack.HitsToWin / 2) Hud.I?.ShowToast("Halfway! They're getting faster.", 2.5f);
        }

        void OnCleared(GhostWhack g)
        {
            if (!IsLive || Game.Campaign.IsVerified(CountryId.UAE)) return;
            AudioDirector.I?.Play("seal_verify", Game.Rig != null ? Game.Rig.Head.position : (Vector3?)null, 0.7f);
            Hud.I?.ShowToast("Ten ghosts stamped! The homecoming seal takes the ink.", 3.5f);
            Game.Story.VerifyCountry(CountryId.UAE, null);
        }

        void OnOpened(Tear t)
        {
            if (!IsLive) return;
            Game.Campaign.SetFlag(StoryFlags.FirstTearDone);
        }

        void OnSealed(Tear t)
        {
            if (!IsLive) return;
            if (Game.Campaign.IsVerified(CountryId.UAE) && !m_ThreatStarted) StartThreat();
        }

        public void OnVerified()
        {
            if (Game.Tears.Active != null) Narration.I?.Say("tut_seal", null, true);
            else StartThreat();
        }

        void StartThreat()
        {
            if (m_ThreatStarted) return;
            m_ThreatStarted = true;
            StartCoroutine(ThreatRoutine());
        }

        System.Collections.IEnumerator ThreatRoutine()
        {
            // Early escalation: first only in the reflection, then in the room, slowly.
            AudioDirector.I?.Play("reflection_sting", null, 0.8f, 1f, Bus.SFX, true);
            float t = 0f;
            while (t < 1f) { t += Time.deltaTime / 2.5f; A.ReflectionFigureMat.SetColor("_Color", new Color(0.03f, 0.035f, 0.04f, t * 0.55f)); yield return null; }
            yield return new WaitForSeconds(2.5f);
            var pc = Game.Passenger;
            pc.RetreatPoint = A.transform.TransformPoint(new Vector3(2.75f, 0f, -6.2f));
            pc.RetreatSeconds = 14f;
            pc.Spawn(Cell, new Vector3(2.75f, 0.01f, -5.2f), 0f, PassengerState.Observe, Disguise.Kandura);
            pc.AllowChase = false; // the tutorial's first threat stays a slow stalk
            pc.Threatening = true;
            AudioDirector.I?.Play("pass_creak_1", pc.ChestPosition, 0.8f);
            Hud.I?.ShowCaption("glass creaks in the bathroom doorway", 3f);
            yield return new WaitForSeconds(3.5f);
            pc.SetState(PassengerState.Stalk);
            pc.WalkSpeed = 0.55f;
            BeginEscape();
        }

        void BeginEscape()
        {
            m_Escaping = true;
            Tutorial.I?.Begin(TutorialStep.OpenPassport, TutorialStep.OpenTear, TutorialStep.WidenTear, TutorialStep.StepThrough);
            Narration.I?.Say("tut_traverse", null, true);
            Hud.I?.SetObjective(Objective, false);
        }

        void Update()
        {
            if (!IsLive) return;
            // The reflection corridor breathes faintly.
            m_ReflectionAlpha = 0.14f + Mathf.Sin(Time.time * 0.6f) * 0.03f;
            A.ReflectionMat.SetColor("_Color", new Color(0.75f, 0.75f, 0.7f, m_ReflectionAlpha));
        }

        public override void ResetLocal()
        {
            Game.Passenger?.Despawn();
            m_ThreatStarted = false;
            A.Ghosts?.Stop();
        }
    }
}
