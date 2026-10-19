using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// One "hand". Desktop: a single interactor driven by the centre-screen ray. VR: one per controller, using a
    /// near-grab sphere first and the aim ray second. The rig feeds button edges; all game logic lives in the
    /// Interactables, so both modes share the same behaviour.
    /// </summary>
    public class Interactor : MonoBehaviour
    {
        public HandSide Side = HandSide.Desktop;
        public Transform HoldPoint;     // where held items attach
        public Transform RayOrigin;     // aim pose (VR) or camera (desktop)
        public float NearRadius = 0.11f;
        public float RayLength = 6f;

        public Interactable Focused { get; private set; }
        public Grabbable Held { get; private set; }
        public Vector3 LastRayEnd { get; private set; }
        public bool RayHitSomething { get; private set; }
        public bool FocusIsNear { get; private set; }

        readonly Collider[] m_Overlap = new Collider[16];
        Vector3 m_PrevHoldPos;
        Vector3 m_Velocity;

        bool IsDesktop => Side == HandSide.Desktop;

        void LateUpdate()
        {
            if (HoldPoint != null)
            {
                var p = HoldPoint.position;
                if (Time.deltaTime > 0f) m_Velocity = Vector3.Lerp(m_Velocity, (p - m_PrevHoldPos) / Time.deltaTime, 0.5f);
                m_PrevHoldPos = p;
            }
        }

        /// <summary>Recompute what this hand is aiming at. Call once per frame from the rig.</summary>
        public void UpdateFocus(bool allowed)
        {
            Interactable best = null;
            FocusIsNear = false;
            RayHitSomething = false;
            if (allowed && RayOrigin != null)
            {
                if (!IsDesktop)
                {
                    // Near: closest interactable to the hand.
                    int n = Physics.OverlapSphereNonAlloc(HoldPoint.position, NearRadius, m_Overlap, 1 << Layers.Interactable, QueryTriggerInteraction.Collide);
                    float bestD = float.MaxValue;
                    for (int i = 0; i < n; i++)
                    {
                        var it = m_Overlap[i].GetComponentInParent<Interactable>();
                        if (it == null || it == Held || !CanFocus(it)) continue;
                        float d = Vector3.Distance(m_Overlap[i].ClosestPoint(HoldPoint.position), HoldPoint.position);
                        if (d < bestD) { bestD = d; best = it; }
                    }
                    if (best != null) FocusIsNear = true;
                }
                if (best == null)
                {
                    var ray = new Ray(RayOrigin.position, RayOrigin.forward);
                    float len = IsDesktop ? 3.2f : RayLength;
                    if (Physics.Raycast(ray, out var hit, len, Layers.InteractMask, QueryTriggerInteraction.Collide))
                    {
                        RayHitSomething = true;
                        LastRayEnd = hit.point;
                        var it = hit.collider.GetComponentInParent<Interactable>();
                        if (it != null && it != Held && CanFocus(it) && hit.distance <= it.MaxDistance + (IsDesktop ? 0f : 3.5f))
                            best = it;
                    }
                    else LastRayEnd = ray.GetPoint(len);
                }
            }
            if (best != Focused)
            {
                if (Focused != null) Focused.SetFocused(this, false);
                Focused = best;
                if (Focused != null) Focused.SetFocused(this, true);
            }
        }

        bool CanFocus(Interactable it)
        {
            if (it is Grabbable g) return Held == null && g.CanUse(this) && (IsDesktop || FocusAllowsFar(g));
            if (it is Socket s) return Held != null && s.CanUse(this);
            return it.CanUse(this);
        }

        // In VR, grabbables are near-only unless they are small and the ray is short (comfort: no stretching).
        bool FocusAllowsFar(Grabbable g) => true;

        /// <summary>Primary "use" (desktop E, VR trigger).</summary>
        public void PressUse()
        {
            if (Held != null)
            {
                if (Focused is Socket s && s.Accepts_(Held)) { PlaceInto(s); return; }
                if (Focused != null && !(Focused is Grabbable) && Focused.CanUse(this)) { Focused.Use(this); return; }
                if (Held.OnActivate != null) { Held.OnActivate(Held, this); return; }
                if (IsDesktop) { Drop(); return; }
                return;
            }
            if (Focused == null) return;
            if (Focused is Grabbable g)
            {
                if (IsDesktop) Grab(g);
                else if (FocusIsNear) Grab(g);
                else Grab(g); // VR far grab via trigger: pulls the item to the hand
                return;
            }
            Focused.Use(this);
        }

        /// <summary>VR grip press.</summary>
        public void PressGrab()
        {
            if (Held != null) return;
            if (Focused is Grabbable g) Grab(g);
            else if (Focused != null && FocusIsNear && Focused.CanUse(this)) Focused.Use(this);
        }

        /// <summary>VR grip release.</summary>
        public void ReleaseGrab()
        {
            if (Held == null) return;
            // Snap into a nearby compatible socket if any.
            var sockets = FindSockets(HoldPoint.position, 0.25f);
            foreach (var s in sockets)
                if (s.Accepts_(Held)) { PlaceInto(s); return; }
            var g = Held;
            Held = null;
            g.DetachFrom(this, m_Velocity, true);
        }

        public void Grab(Grabbable g)
        {
            if (g == null || Held != null) return;
            if (g.Holder != null) g.Holder.ForceRelease(false);
            Held = g;
            g.AttachTo(this, HoldPoint, IsDesktop);
            AudioDirector.Haptic(Side, 0.35f, 0.05f);
            Focused = null;
        }

        public void PlaceInto(Socket s)
        {
            if (Held == null) return;
            var g = Held;
            Held = null;
            g.DetachFrom(this, Vector3.zero, false);
            s.Put(g);
            AudioDirector.Haptic(Side, 0.5f, 0.06f);
        }

        /// <summary>Desktop drop: set the item down on the surface in front of the player.</summary>
        public void Drop()
        {
            if (Held == null) return;
            var g = Held;
            Held = null;
            g.DetachFrom(this, Vector3.zero, false);
            var origin = RayOrigin.position + RayOrigin.forward * 0.7f;
            if (Physics.Raycast(origin + Vector3.up * 0.3f, Vector3.down, out var hit, 3f, Layers.InteractMask | Layers.FloorMask, QueryTriggerInteraction.Ignore))
            {
                g.transform.position = hit.point + Vector3.up * 0.04f;
                g.transform.rotation = Quaternion.Euler(0, RayOrigin.eulerAngles.y, 0);
                g.SyncBody();
            }
            g.Body.isKinematic = false;
            AudioDirector.I?.Play("drop_soft", g.transform.position, 0.6f);
        }

        public void ForceRelease(bool returnHome)
        {
            if (Held == null) return;
            var g = Held;
            Held = null;
            g.DetachFrom(this, Vector3.zero, false);
            if (returnHome) g.ReturnHome();
        }

        static readonly List<Socket> s_Sockets = new List<Socket>();
        static List<Socket> FindSockets(Vector3 p, float radius)
        {
            s_Sockets.Clear();
            var cols = Physics.OverlapSphere(p, radius, 1 << Layers.Interactable, QueryTriggerInteraction.Collide);
            foreach (var c in cols)
            {
                var s = c.GetComponentInParent<Socket>();
                if (s != null && !s_Sockets.Contains(s)) s_Sockets.Add(s);
            }
            s_Sockets.Sort((a, b) => Vector3.Distance(a.Attach.position, p).CompareTo(Vector3.Distance(b.Attach.position, p)));
            return s_Sockets;
        }

        public string CurrentPrompt()
        {
            if (Focused == null) return Held != null && Held.OnActivate != null && !string.IsNullOrEmpty(Held.ActivatePrompt) ? Held.ActivatePrompt : null;
            return Focused.GetPrompt(this);
        }
    }
}
