using System;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// TEAR / SEAL rules. Exactly one tear may be open. Width sets function and risk: slit (look, narrow resource),
    /// window (reach, strong resource, things can come through), doorway (step through). Sealing is a short hold
    /// with fibre/ink/glass feedback; a one-handed button and a mouse button both work.
    /// </summary>
    public class TearController : MonoBehaviour
    {
        public const float SlitThreshold = 0.08f;
        public const float WindowThreshold = 0.42f;
        public const float DoorThreshold = 0.82f;
        public const float SealSeconds = 0.75f;
        public const float WidenPerSecond = 0.42f;
        public const float DetentSeconds = 0.4f;

        public Tear Active { get; private set; }
        public SeamAnchor Target { get; private set; }
        public bool HasTarget => Target != null;
        public Vector3 TargetPoint => Target != null ? Target.Center : Vector3.zero;
        public float Contamination => Active != null ? Active.Contamination : m_ResidualContamination;
        public string HintText { get; private set; }
        public bool IsPulling { get; private set; }
        /// <summary>True while the open tear is the story's own (OpenForStory): no readout, no size events, no breach.</summary>
        public bool StoryTear { get; private set; }

        public event Action<Tear> Opened;
        public event Action<Tear, TearSize> SizeChanged;
        public event Action<Tear> Sealed;
        public event Action<Tear> Breached;
        public event Action<CountryDef> Traversed;

        float m_Detent;
        TearSize m_LastSize;
        float m_ResidualContamination;
        bool m_BreachFired;
        HandSide m_LastHand = HandSide.Desktop;
        static readonly int k_PocketPlane = Shader.PropertyToID("_TS_PocketPlane");

        public static TearController Create(Transform parent)
        {
            var go = new GameObject("TearController");
            go.transform.SetParent(parent, false);
            return go.AddComponent<TearController>();
        }

        void Update()
        {
            UpdateTarget();
            if (Active != null)
            {
                var size = Active.Size;
                if (size != m_LastSize)
                {
                    if (size > m_LastSize) OnGrew(size);
                    m_LastSize = size;
                    if (!StoryTear) SizeChanged?.Invoke(Active, size);
                }
                if (!Active.Closing)
                {
                    if (Active.Contamination >= 1f && !m_BreachFired)
                    {
                        m_BreachFired = true;
                        AudioDirector.I?.Play("tear_breach", Active.OpeningCenter, 1f, 1f, Bus.SFX, true);
                        Breached?.Invoke(Active);
                    }
                    CheckTraversal();
                }
                SetPocketPlane(Active.Anchor);
                PostFx.I?.SetContamination(Active.Contamination);
                if (!StoryTear) Hud.I?.ShowTear(Active.Aperture, Active.SealProgress, TearLabel(Active));
            }
            else
            {
                m_ResidualContamination = Mathf.MoveTowards(m_ResidualContamination, 0f, Time.deltaTime * 0.15f);
                PostFx.I?.SetContamination(m_ResidualContamination);
            }
            m_Detent = Mathf.Max(0f, m_Detent - Time.deltaTime);
        }

        string TearLabel(Tear t)
        {
            string size = t.Size == TearSize.Doorway ? "Doorway" : t.Size == TearSize.Window ? "Window" : t.Size == TearSize.Slit ? "Slit" : "Opening";
            string beam = t.Beam != null ? " · sunlight" : "";
            return t.Destination.DisplayName + " · " + size + beam + " · contamination " + Mathf.RoundToInt(t.Contamination * 100) + "%";
        }

        // ------------------------------------------------------------------ targeting
        void UpdateTarget()
        {
            SeamAnchor best = null;
            HintText = null;
            var passport = Game.Passport;
            if (Game.Rig != null && passport != null && passport.IsOpen && !Game.InputLocked && Active == null)
            {
                var ray = Game.Rig.TearAimRay;
                float bestScore = float.MaxValue;
                var cell = Game.Cells != null ? Game.Cells.Active : null;
                if (cell != null)
                {
                    foreach (var s in cell.Seams)
                    {
                        if (s == null || !s.Revealed || !s.isActiveAndEnabled) continue;
                        var to = s.Center - ray.origin;
                        float dist = to.magnitude;
                        if (dist > 12f) continue;
                        float angle = Vector3.Angle(ray.direction, to);
                        if (angle > 14f) continue;
                        if (Vector3.Dot(s.Forward, -to.normalized) < 0.05f) continue; // must face the player
                        if (Physics.Linecast(ray.origin, s.Center - to.normalized * 0.15f, 1 << Layers.Default, QueryTriggerInteraction.Ignore)) continue;
                        float score = angle + dist * 0.5f;
                        if (score < bestScore) { bestScore = score; best = s; }
                    }
                }
                HintText = BuildHint(best);
            }
            else if (Active != null && !Active.Closing && Game.Passport != null && Game.Passport.IsOpen)
            {
                HintText = "<color=#E8D2A6>[" + Game.Input.Label(Intent.Seal) + "]</color>  Seal the tear   <color=#E8D2A6>[" + Game.Input.Label(Intent.Tear) + "]</color>  Widen";
            }
            if (best != Target)
            {
                if (Target != null) Target.SetTargeted(false);
                Target = best;
                if (Target != null) Target.SetTargeted(true);
            }
        }

        string BuildHint(SeamAnchor target)
        {
            if (target != null && target.HomeSeam)
                return Game.Campaign.CanOpenGenuineHome
                    ? "<color=#E8D2A6>[" + Game.Input.Label(Intent.Tear) + "]</color>  Open the genuine way home"
                    : "The wall resists. The forged authorisation still holds.";
            var dest = Game.Passport.SelectedDef;
            if (dest == null) return "Choose a destination  <color=#E8D2A6>[" + Game.Input.Label(Intent.Destination) + "]</color>";
            if (!CanOpenTo(dest, out var reason)) return reason;
            if (target == null) return "Aim at a stitched seam to open a tear to " + dest.DisplayName;
            return "<color=#E8D2A6>[" + Game.Input.Label(Intent.Tear) + "]</color>  Open a tear to " + dest.DisplayName;
        }

        public bool CanOpenTo(CountryDef dest, out string reason)
        {
            reason = null;
            var c = Game.Campaign;
            if (Game.Story != null && !Game.Story.TearsAllowed) { reason = "The seam won't open here."; return false; }
            if (!c.IsDestinationAvailable(dest.Id)) { reason = dest.DisplayName + " is not available yet."; return false; }
            var current = Game.Cells != null && Game.Cells.Active != null ? CountryDatabase.ByCell(Game.Cells.Active.Id) : null;
            if (current != null && current.Id == dest.Id) { reason = "You are already in " + dest.DisplayName + "."; return false; }
            if (Active != null) { reason = "Seal the open tear first."; return false; }
            return true;
        }

        // ------------------------------------------------------------------ open / widen
        public void HoldWiden(float dt, HandSide hand)
        {
            m_LastHand = hand;
            if (Active == null)
            {
                if (Target == null || Game.Passport == null) return;
                if (Target.HomeSeam) { if (Game.Campaign.CanOpenGenuineHome) Open(Target, CountryDatabase.Home); return; }
                if (Game.Passport.SelectedDef == null) return;
                if (!CanOpenTo(Game.Passport.SelectedDef, out _)) return;
                Open(Target, Game.Passport.SelectedDef);
                return;
            }
            if (Active.Closing || m_Detent > 0f) return;
            SetAperture(Active.Aperture + dt * WidenPerSecond, hand);
        }

        public void ReleaseWiden() { }

        /// <summary>VR two-handed seam pull: distance between the hands sets the aperture directly.</summary>
        public void SetPull(float handDistance, HandSide hand)
        {
            IsPulling = true;
            if (Active == null)
            {
                if (handDistance < 0.06f || Target == null) return;
                if (Target.HomeSeam) { if (Game.Campaign.CanOpenGenuineHome) Open(Target, CountryDatabase.Home); }
                else
                {
                    if (Game.Passport?.SelectedDef == null || !CanOpenTo(Game.Passport.SelectedDef, out _)) return;
                    Open(Target, Game.Passport.SelectedDef);
                }
            }
            if (Active == null || Active.Closing) return;
            float a = Mathf.InverseLerp(0.05f, 0.5f, handDistance);
            SetAperture(Mathf.Max(SlitThreshold, a), hand);
        }

        public void EndPull() => IsPulling = false;

        void SetAperture(float a, HandSide hand)
        {
            var before = Active.Size;
            Active.Aperture = Mathf.Clamp01(a);
            var after = Active.Size;
            if (after != before)
            {
                if (after > before) m_Detent = DetentSeconds;
                AudioDirector.Haptic(hand, 0.55f, 0.06f);
            }
            else if (Mathf.FloorToInt(a * 30f) != Mathf.FloorToInt((a - 0.01f) * 30f)) AudioDirector.Haptic(hand, 0.08f, 0.02f);
        }

        public Tear Open(SeamAnchor anchor, CountryDef dest)
        {
            var tear = Spawn(anchor, dest);
            if (tear == null) return null;
            tear.Contamination = m_ResidualContamination * 0.5f;
            Game.Campaign.tearsOpened++;
            Game.Campaign.MarkVisited(dest.Id);
            AudioDirector.I?.Play("tear_open_slit", anchor.Center, 1f);
            AudioDirector.Haptic(m_LastHand, 0.7f, 0.12f);
            Narration.I?.ClearTutorial();
            Opened?.Invoke(tear);
            return tear;
        }

        /// <summary>
        /// The story opens a tear by itself (the seam waking when the glasses go on): the same tear, light and view, but it
        /// counts for nothing (no visit, no tutorial step, no readout, no breach) and only the story closes it.
        /// </summary>
        public Tear OpenForStory(SeamAnchor anchor, CountryDef dest)
        {
            var tear = Spawn(anchor, dest);
            if (tear == null) return null;
            StoryTear = true;
            m_BreachFired = true;
            m_Detent = 0f;
            AudioDirector.I?.Play("tear_open_slit", anchor.Center, 1f);
            return tear;
        }

        Tear Spawn(SeamAnchor anchor, CountryDef dest)
        {
            if (Active != null) return null;
            var cell = Game.Cells.Prepare(dest.Cell);
            if (cell == null) return null;
            cell.ShowAsPocket(anchor);
            var go = new GameObject("Tear_" + dest.StableId);
            go.transform.SetParent(Game.Cells.Active != null ? Game.Cells.Active.transform : transform, true);
            var tear = go.AddComponent<Tear>();
            tear.Init(anchor, dest, cell, cell.Lighting);
            tear.Aperture = SlitThreshold;
            Active = tear;
            StoryTear = false;
            m_LastSize = TearSize.Slit;
            m_BreachFired = false;
            m_Detent = DetentSeconds;
            return tear;
        }

        void OnGrew(TearSize size)
        {
            if (size == TearSize.Window) AudioDirector.I?.Play("tear_open_window", Active.OpeningCenter, 1f);
            if (size == TearSize.Doorway) AudioDirector.I?.Play("tear_open_door", Active.OpeningCenter, 1f);
        }

        // ------------------------------------------------------------------ seal
        int m_SealHeldFrame = -10;

        public void HoldSeal(float dt, HandSide hand)
        {
            m_SealHeldFrame = Time.frameCount;
            if (Active == null || Active.Closing) return;
            if (Game.Rig != null && Vector3.Distance(Game.Rig.Head.position, Active.OpeningCenter) > 16f) return;
            if (Active.SealProgress <= 0f) AudioDirector.I?.Play("tear_stretch_loop", Active.OpeningCenter, 0.5f);
            Active.SealProgress = Mathf.Clamp01(Active.SealProgress + dt / SealSeconds);
            if (Mathf.FloorToInt(Active.SealProgress * 8f) != Mathf.FloorToInt((Active.SealProgress - dt / SealSeconds) * 8f))
                AudioDirector.Haptic(hand, 0.2f + Active.SealProgress * 0.4f, 0.03f);
            if (Active.SealProgress >= 1f) Close(true, hand);
        }

        public void ReleaseSeal()
        {
            // Any input source holding Seal this frame or last frame wins (VR button, mouse, accessibility paths).
            if (Time.frameCount - m_SealHeldFrame <= 1) return;
            if (Active != null && !Active.Closing) Active.SealProgress = Mathf.MoveTowards(Active.SealProgress, 0f, Time.deltaTime * 2.5f);
        }

        /// <summary>Close the active tear (sealed by the player, or forced by the story).</summary>
        public void Close(bool sealedByPlayer, HandSide hand = HandSide.Desktop, bool instant = false)
        {
            if (Active == null || Active.Closing) return;
            var tear = Active;
            tear.Closing = true;
            if (sealedByPlayer)
            {
                AudioDirector.I?.Play("tear_seal", tear.OpeningCenter, 1f);
                AudioDirector.Haptic(hand, 1f, 0.15f);
                if (Game.Campaign.SetFlag(StoryFlags.FirstSealDone)) { }
            }
            m_ResidualContamination = tear.Contamination * 0.6f;
            StartCoroutine(CloseRoutine(tear, instant ? 0f : 0.35f, sealedByPlayer));
        }

        System.Collections.IEnumerator CloseRoutine(Tear tear, float seconds, bool sealedByPlayer)
        {
            float start = tear.Aperture;
            float t = 0f;
            while (t < seconds && tear != null)
            {
                t += Time.deltaTime;
                tear.Aperture = Mathf.Lerp(start, 0f, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            if (Active == tear) { Active = null; StoryTear = false; }
            if (tear != null)
            {
                tear.DestinationCell?.HidePocket();
                tear.Cleanup();
                if (sealedByPlayer) Sealed?.Invoke(tear);
            }
            ClearPocketPlane();
        }

        /// <summary>Instantly remove any tear (checkpoint reset, travel).</summary>
        public void ForceReset()
        {
            if (Active != null)
            {
                Active.DestinationCell?.HidePocket();
                Active.Cleanup();
                Active = null;
            }
            StoryTear = false;
            m_ResidualContamination = 0f;
            ClearPocketPlane();
            PostFx.I?.SetContamination(0f);
        }

        // ------------------------------------------------------------------ traversal
        void CheckTraversal()
        {
            if (Active.Size != TearSize.Doorway || !Active.Anchor.AllowTraverse || Game.Rig == null) return;
            var head = Game.Rig.Head.position;
            var local = Active.Anchor.transform.InverseTransformPoint(head);
            // Walking up to (or teleporting into) the doorway: head within 0.45 m in front of the plane and inside it.
            if (local.z < 0.45f && local.z > -1.5f && Active.InsideOpening(new Vector3(head.x, Active.OpeningCenter.y, head.z), 2f))
                Traverse();
        }

        public void Traverse()
        {
            if (Active == null || Active.Closing || Active.Size != TearSize.Doorway) return;
            var dest = Active.Destination;
            Traversed?.Invoke(dest);
            Game.Story?.TravelThroughTear(dest);
        }

        // ------------------------------------------------------------------ pocket clip plane
        static void SetPocketPlane(SeamAnchor a)
        {
            var n = a.Forward;
            Shader.SetGlobalVector(k_PocketPlane, new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, a.Center)));
        }

        static void ClearPocketPlane() => Shader.SetGlobalVector(k_PocketPlane, new Vector4(0, 0, 0, -1));
    }
}
