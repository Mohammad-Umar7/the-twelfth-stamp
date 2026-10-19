using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TwelfthStamp
{
    public enum TutorialStep { Look, Move, Grab, Use, OpenPassport, ChooseDestination, OpenTear, WidenTear, Seal, StepThrough, Recall, Pause }

    /// <summary>
    /// Interactive onboarding checklist. Steps advance on real actions (never on a timer); the current step's
    /// controls are highlighted on the VR controllers and its live binding is shown on screen.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        public static Tutorial I { get; private set; }

        readonly List<TutorialStep> m_Steps = new List<TutorialStep>();
        readonly HashSet<TutorialStep> m_Done = new HashSet<TutorialStep>();
        public event Action<TutorialStep> StepDone;
        public event Action Completed;
        public bool Active => m_Steps.Count > 0 && m_Steps.Any(s => !m_Done.Contains(s));

        Vector3 m_StartPos;
        Quaternion m_StartRot;

        public static Tutorial Create(Transform parent)
        {
            var go = new GameObject("Tutorial");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<Tutorial>();
            return I;
        }

        void Start()
        {
            if (Game.Tears != null)
            {
                Game.Tears.Opened += _ => Notify(TutorialStep.OpenTear);
                Game.Tears.SizeChanged += (_, s) => { if (s >= TearSize.Window) Notify(TutorialStep.WidenTear); };
                Game.Tears.Sealed += _ => Notify(TutorialStep.Seal);
                Game.Tears.Traversed += _ => Notify(TutorialStep.StepThrough);
            }
        }

        public void Begin(params TutorialStep[] steps)
        {
            m_Steps.Clear();
            m_Steps.AddRange(steps);
            m_Done.Clear();
            if (Game.Rig != null) { m_StartPos = Game.Rig.Feet; m_StartRot = Game.Rig.Head.rotation; }
            Refresh();
        }

        public void End()
        {
            m_Steps.Clear();
            ClearHighlights();
            Hud.I?.SetObjective("", false);
        }

        public bool IsDone(TutorialStep s) => m_Done.Contains(s);

        public void Notify(TutorialStep s)
        {
            if (!m_Steps.Contains(s) || m_Done.Contains(s)) return;
            m_Done.Add(s);
            AudioDirector.I?.PlayUI("ui_confirm", 0.6f);
            StepDone?.Invoke(s);
            Refresh();
            if (!Active) { ClearHighlights(); Completed?.Invoke(); }
        }

        void Update()
        {
            if (!Active || Game.Rig == null || Game.Paused) return;
            if (m_Steps.Contains(TutorialStep.Look) && Quaternion.Angle(Game.Rig.Head.rotation, m_StartRot) > 35f) Notify(TutorialStep.Look);
            if (m_Steps.Contains(TutorialStep.Move) && Vector3.Distance(Game.Rig.Feet, m_StartPos) > 1.2f) Notify(TutorialStep.Move);
            foreach (var h in Game.Rig.Hands) if (h.Held != null) Notify(TutorialStep.Grab);
        }

        public TutorialStep? Current => m_Steps.Where(s => !m_Done.Contains(s)).Select(s => (TutorialStep?)s).FirstOrDefault();

        static Intent IntentOf(TutorialStep s)
        {
            switch (s)
            {
                case TutorialStep.Look: return Intent.Look;
                case TutorialStep.Move: return Game.Mode == GameMode.VR ? Intent.Teleport : Intent.Move;
                case TutorialStep.Grab: return Intent.Grab;
                case TutorialStep.Use: return Intent.Interact;
                case TutorialStep.OpenPassport: return Intent.Passport;
                case TutorialStep.ChooseDestination: return Intent.Destination;
                case TutorialStep.OpenTear: return Intent.Tear;
                case TutorialStep.WidenTear: return Intent.Tear;
                case TutorialStep.Seal: return Intent.Seal;
                case TutorialStep.StepThrough: return Intent.Move;
                case TutorialStep.Recall: return Intent.Recall;
                default: return Intent.Pause;
            }
        }

        static string Describe(TutorialStep s)
        {
            switch (s)
            {
                case TutorialStep.Look: return "Look around";
                case TutorialStep.Move: return "Move";
                case TutorialStep.Grab: return "Pick something up";
                case TutorialStep.Use: return "Use / inspect";
                case TutorialStep.OpenPassport: return "Open your passport";
                case TutorialStep.ChooseDestination: return "Choose a destination";
                case TutorialStep.OpenTear: return "Open a tear at a seam";
                case TutorialStep.WidenTear: return "Widen it to a window";
                case TutorialStep.Seal: return "Seal the tear";
                case TutorialStep.StepThrough: return "Widen to a doorway and step through";
                case TutorialStep.Recall: return "Recall an essential item";
                default: return "Pause";
            }
        }

        void Refresh()
        {
            if (m_Steps.Count == 0) return;
            var lines = new List<string>();
            foreach (var s in m_Steps)
            {
                bool done = m_Done.Contains(s);
                string box = done ? "<color=#9FD9C6>✓</color>" : "<color=#EDCF99>•</color>";
                string label = Describe(s) + (done ? "" : "   " + Hud.KeyTag(IntentOf(s)));
                lines.Add(box + "  " + (done ? "<color=#8A8F8D>" + label + "</color>" : label));
            }
            Hud.I?.SetObjective(string.Join("\n", lines), false);
            HighlightCurrent();
        }

        void HighlightCurrent()
        {
            if (Game.Mode != GameMode.VR || !(Game.Rig is VRRig vr)) return;
            ClearHighlights();
            var cur = Current;
            if (!cur.HasValue) return;
            foreach (var c in Game.Input.XRControls(IntentOf(cur.Value)))
                vr.ModelOf(c.Hand)?.SetCallout(c.Control, c.Label, true);
        }

        void ClearHighlights()
        {
            if (Game.Rig is VRRig vr)
            {
                vr.ModelOf(HandSide.Left)?.ClearCallouts();
                vr.ModelOf(HandSide.Right)?.ClearCallouts();
            }
        }
    }
}
