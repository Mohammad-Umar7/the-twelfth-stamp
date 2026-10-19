using System;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>A prop that can be picked up, carried, placed in sockets and (if essential) recalled.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : Interactable
    {
        public string ItemId = "item";
        public bool Essential;
        public bool UsePhysicsOnRelease = true;
        public Vector3 HoldLocalPosition = Vector3.zero;
        public Vector3 HoldLocalEuler = Vector3.zero;
        public Vector3 DesktopHoldEuler = new Vector3(0, 180, 0);

        public Interactor Holder { get; private set; }
        public Socket InSocket { get; internal set; }
        public Rigidbody Body { get; private set; }

        public event Action<Grabbable, Interactor> Grabbed;
        public event Action<Grabbable> Released;
        /// <summary>Called when the player activates the held item (desktop Left Mouse / E on nothing, VR trigger).</summary>
        public Action<Grabbable, Interactor> OnActivate;
        public string ActivatePrompt;

        Vector3 m_HomePos;
        Quaternion m_HomeRot;
        Transform m_HomeParent;
        bool m_HomeSet;

        protected override void Awake()
        {
            base.Awake();
            Body = GetComponent<Rigidbody>();
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Prompt = string.IsNullOrEmpty(Prompt) || Prompt == "Use" ? "Pick up" : Prompt;
        }

        void Start()
        {
            if (!m_HomeSet) SetHome();
        }

        public void SetHome()
        {
            m_HomeParent = transform.parent;
            m_HomePos = transform.localPosition;
            m_HomeRot = transform.localRotation;
            m_HomeSet = true;
        }

        public override bool CanUse(Interactor who) => base.CanUse(who) && Holder == null;

        public override string GetPrompt(Interactor who) => Prompt;

        public override void Use(Interactor who)
        {
            who.Grab(this);
        }

        internal void AttachTo(Interactor who, Transform holdPoint, bool desktop)
        {
            if (InSocket != null) InSocket.Release(this);
            Holder = who;
            Body.isKinematic = true;
            Body.detectCollisions = false;
            // No interpolation while held: an interpolated body overrides the transform with its own last physics pose, so
            // the item trailed the hand and, once let go, could stay drawn (and hit-tested) where it was last held
            // (playtest, 18 Oct: the claw machine's prize floating in mid-air, "not attached to my hand").
            Body.interpolation = RigidbodyInterpolation.None;
            transform.SetParent(holdPoint, true);
            transform.localPosition = HoldLocalPosition;
            transform.localRotation = Quaternion.Euler(desktop ? DesktopHoldEuler : HoldLocalEuler);
            SyncBody();
            Grabbed?.Invoke(this, who);
            AudioDirector.I?.Play("grab", transform.position, 0.6f);
        }

        internal void DetachFrom(Interactor who, Vector3 velocity, bool keepPhysics)
        {
            if (Holder != who) return;
            Holder = null;
            var cell = Game.Cells != null ? Game.Cells.Active : null;
            transform.SetParent(cell != null ? cell.transform : null, true);
            Body.detectCollisions = true;
            Body.isKinematic = !(keepPhysics && UsePhysicsOnRelease);
            SyncBody();
            // Smooth only while physics moves it (a throw or a drop); a resting item stays exactly where it is put.
            Body.interpolation = Body.isKinematic ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            if (!Body.isKinematic) Body.linearVelocity = Vector3.ClampMagnitude(velocity, 4f);
            Released?.Invoke(this);
        }

        /// <summary>Return to where the item started (used by Recall and encounter resets).</summary>
        public void ReturnHome()
        {
            if (Holder != null) Holder.ForceRelease(false);
            if (InSocket != null) InSocket.Release(this);
            if (!m_HomeSet) return;
            transform.SetParent(m_HomeParent, false);
            transform.localPosition = m_HomePos;
            transform.localRotation = m_HomeRot;
            Body.isKinematic = true;
            Body.detectCollisions = true;
            Body.interpolation = RigidbodyInterpolation.None;
            SyncBody();
        }

        /// <summary>Moves the physics body to where the transform was just put (scripted moves of a body).</summary>
        public void SyncBody()
        {
            if (Body == null) return;
            Body.position = transform.position;
            Body.rotation = transform.rotation;
        }

        void Update()
        {
            // Never lose an item through the floor.
            if (Holder == null && InSocket == null && transform.position.y < -5f) ReturnHome();
        }
    }
}
