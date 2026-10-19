using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>A place that accepts specific items. VR: release the item near it. Desktop: hold the item and press E on it.</summary>
    public class Socket : Interactable
    {
        public List<string> Accepts = new List<string>();
        public float SnapRadius = 0.18f;
        public bool LockWhenFilled;
        public Transform Attach;

        public Grabbable Current { get; private set; }
        public event Action<Socket, Grabbable> Filled;
        public event Action<Socket, Grabbable> Emptied;

        protected override void Awake()
        {
            base.Awake();
            if (Attach == null) Attach = transform;
            if (string.IsNullOrEmpty(Prompt) || Prompt == "Use") Prompt = "Place";   // keep a prompt the cell set
        }

        public bool Accepts_(Grabbable g) => g != null && Current == null && (Accepts.Count == 0 || Accepts.Contains(g.ItemId));

        public override bool CanUse(Interactor who) => base.CanUse(who) && who != null && Accepts_(who.Held);

        public override string GetPrompt(Interactor who) => who != null && who.Held != null ? "Place " + PrettyName(who.Held.ItemId) : Prompt;

        static string PrettyName(string id) => id.Replace('_', ' ');

        public override void Use(Interactor who)
        {
            if (who?.Held != null && Accepts_(who.Held)) who.PlaceInto(this);
        }

        public void Put(Grabbable g)
        {
            if (!Accepts_(g)) return;
            Current = g;
            g.InSocket = this;
            g.Body.isKinematic = true;
            g.Body.detectCollisions = true;
            g.transform.SetParent(Attach, false);
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;
            g.Body.interpolation = RigidbodyInterpolation.None;
            g.SyncBody();
            if (LockWhenFilled) g.Usable = false;
            AudioDirector.I?.Play("place_socket", Attach.position, 0.8f);
            Filled?.Invoke(this, g);
        }

        public void Release(Grabbable g)
        {
            if (Current != g) return;
            Current = null;
            g.InSocket = null;
            Emptied?.Invoke(this, g);
        }
    }

    /// <summary>Rotates a part in fixed steps (reflectors, optical panels, dials). Use = one step.</summary>
    public class StepRotator : Interactable
    {
        public Transform Target;
        public Vector3 Axis = Vector3.up;
        public float StepDegrees = 15f;
        public int Steps = 24;
        public int Index;
        public float TurnSeconds = 0.18f;
        public event Action<StepRotator, int> Changed;

        Quaternion m_Base;
        bool m_Turning;

        protected override void Awake()
        {
            base.Awake();
            if (Target == null) Target = transform;
            m_Base = Target.localRotation;
            if (string.IsNullOrEmpty(Prompt) || Prompt == "Use") Prompt = "Turn";    // keep a prompt the cell set
            Apply(true);
        }

        public float Angle => Index * StepDegrees;

        public override bool CanUse(Interactor who) => base.CanUse(who) && !m_Turning;

        public override void Use(Interactor who)
        {
            Index = (Index + 1) % Mathf.Max(1, Steps);
            AudioDirector.I?.Play("dial_click", transform.position, 0.8f);
            AudioDirector.Haptic(who != null ? who.Side : HandSide.Desktop, 0.25f, 0.04f);
            Apply(false);
            Changed?.Invoke(this, Index);
        }

        public void SetIndex(int i, bool instant = true)
        {
            Index = ((i % Steps) + Steps) % Steps;
            Apply(instant);
        }

        void Apply(bool instant)
        {
            var target = m_Base * Quaternion.AngleAxis(Index * StepDegrees, Axis);
            if (instant || !isActiveAndEnabled) { Target.localRotation = target; return; }
            StartCoroutine(Turn(target));
        }

        IEnumerator Turn(Quaternion to)
        {
            m_Turning = true;
            var from = Target.localRotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / TurnSeconds;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // ease out
                Target.localRotation = Quaternion.Slerp(from, to, e);
                yield return null;
            }
            Target.localRotation = to;
            m_Turning = false;
        }
    }

    /// <summary>A pressable button or switch with a short press animation.</summary>
    public class PushButton : Interactable
    {
        public Transform Cap;
        public Vector3 PressOffset = new Vector3(0, -0.01f, 0);
        public string Sound = "button_press";
        public bool OneShot;
        bool m_Used;

        public event Action<PushButton> Pressed;

        public override bool CanUse(Interactor who) => base.CanUse(who) && !(OneShot && m_Used);

        public override void Use(Interactor who)
        {
            m_Used = true;
            if (!string.IsNullOrEmpty(Sound)) AudioDirector.I?.Play(Sound, transform.position, 0.8f);
            AudioDirector.Haptic(who != null ? who.Side : HandSide.Desktop, 0.3f, 0.05f);
            if (Cap != null) StartCoroutine(Press());
            base.Use(who);
            Pressed?.Invoke(this);
        }

        public void ResetButton() => m_Used = false;

        IEnumerator Press()
        {
            var basePos = Cap.localPosition;
            Cap.localPosition = basePos + PressOffset;
            yield return new WaitForSeconds(0.15f);
            Cap.localPosition = basePos;
        }
    }
}
