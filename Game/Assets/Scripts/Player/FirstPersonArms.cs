using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Desktop first-person arms. Saeed's forearms and hands rest just under the frame and breathe, swing up into the
    /// bottom corners with the gait, pump into view on a run ("hmf, hmf"), hold the open passport (left) and a carried
    /// item (right), reach on Interact and strain towards a tear's seam. A spring-damper makes them trail mouse look and
    /// the body's starts, stops, footfalls and landings. Lowered out of view (eased, never popped) in menus and cutscenes.
    /// Children of the camera, no colliders, no shadows: never part of any raycast.
    /// </summary>
    [DefaultExecutionOrder(100)] // after the passport has followed its mount this frame
    public class FirstPersonArms : MonoBehaviour
    {
        // ------------------------------------------------------------------ poses (right hand, camera space; the left mirrors)
        // Wrist positions and hand rotations (+Z towards the fingers, +Y back of the hand), from the prototype framing at 72° FOV.
        static readonly Vector3 IdlePos = new Vector3(0.215f, -0.3f, 0.255f);
        static readonly Quaternion IdleRot = Quaternion.LookRotation(new Vector3(-0.12f, -0.62f, 0.78f), new Vector3(0.62f, 0.62f, 0.25f));
        // Playtest: the hands should be seen coming up and down while moving. At 72° vertical FOV the frame's bottom edge is
        // 36° below centre; the forward walk swing peaks at ~27° and the top of the run pump at ~23° (well in view), the
        // back swing and the bottom of the pump drop out of the frame, so each hand rises into the corner every stride.
        static readonly Vector3 WalkFwdPos = new Vector3(0.19f, -0.165f, 0.32f);
        static readonly Quaternion WalkFwdRot = Quaternion.LookRotation(new Vector3(-0.18f, -0.28f, 0.94f), new Vector3(0.55f, 0.8f, 0.15f));
        static readonly Vector3 WalkBackPos = new Vector3(0.22f, -0.27f, 0.24f);
        static readonly Quaternion WalkBackRot = Quaternion.LookRotation(new Vector3(-0.08f, -0.75f, 0.66f), new Vector3(0.62f, 0.55f, 0.3f));
        static readonly Vector3 RunTopPos = new Vector3(0.12f, -0.125f, 0.3f);
        static readonly Quaternion RunTopRot = Quaternion.LookRotation(new Vector3(-0.38f, 0.32f, 0.87f), new Vector3(0.95f, 0.25f, 0.15f));
        static readonly Vector3 RunBottomPos = new Vector3(0.235f, -0.33f, 0.18f);
        static readonly Quaternion RunBottomRot = Quaternion.LookRotation(new Vector3(-0.1f, -0.6f, 0.79f), new Vector3(1f, 0.1f, 0.05f));

        // Left hand on the open passport (book space: x towards the spine, y up, z away from the eye). It holds the outer edge
        // low down: palm against the edge, fingers curled behind the cover, thumb along the edge outside the book. Checked in
        // screen space at 72° FOV: no part of the hand in front of the book falls on either printed page (stamps, return box).
        static readonly Vector3 PassportPalm = new Vector3(-0.118f, -0.08f, 0f);
        static readonly Quaternion PassportRot = Quaternion.LookRotation(new Vector3(-0.3214f, 0.342f, 0.883f), Vector3.down) * Quaternion.Euler(0f, 0f, -60f);
        static readonly HandPose PassportFingers = new HandPose(0.8f, 0.8f, 0.8f, 0.8f, 0.2f, 0f, -0.6f);

        // Right hand holding a carried item from below and behind, thumb on its front.
        static readonly Quaternion HoldRot = Quaternion.LookRotation(new Vector3(-0.1f, 1f, 0.3f), new Vector3(0f, -0.3f, 1f));
        static readonly HandPose HoldFingers = new HandPose(0.22f, 0.26f, 0.2f, 0.18f, 1.1f, 0.6f, -0.6f);

        // Interact: a quick reach towards what the reticle is on, fingers opening then closing.
        static readonly Vector3 ReachPalm = new Vector3(0.11f, -0.185f, 0.47f);
        static readonly Vector3 ReachFwd = new Vector3(-0.22f, 0.05f, 1f), ReachUp = new Vector3(0.5f, 1f, 0f);
        static readonly HandPose ReachOpen = new HandPose(0.12f, 0.15f, 0.2f, 0.25f, 0.15f, 0.3f, 0f, 0.35f);
        static readonly HandPose ReachGrab = new HandPose(0.65f, 0.7f, 0.75f, 0.8f, 0.45f, 0.45f, 0.2f);

        // Widening a tear: palm out towards the seam, fingers clawed and spread, trembling with the effort.
        static readonly Vector3 TearPalm = new Vector3(0.11f, -0.15f, 0.42f);
        static readonly Quaternion TearRot = Quaternion.LookRotation(new Vector3(-0.1f, 0.85f, 0.5f), new Vector3(0.15f, 0.45f, -1f));
        static readonly HandPose TearFingers = new HandPose(0.42f, 0.45f, 0.5f, 0.55f, 0.25f, 0.45f, 0f, 1f);

        // Body: shoulders relative to the eye, arm lengths, the sway pivot (near the chin).
        const float ShoulderX = 0.19f, ShoulderY = -0.26f, ShoulderZ = -0.06f, UpperArm = 0.29f, ForearmLength = 0.27f;
        static readonly Vector3 SwayPivot = new Vector3(0f, -0.15f, 0f);
        const float ShowSeconds = 0.45f;   // rise / lower time

        DesktopRig m_Rig;
        HandRig m_Left, m_Right;
        float m_ShowL, m_ShowR;            // 0 lowered out of view .. 1 shown
        float m_PassW, m_HoldW, m_TearW;   // context blends
        float m_ReachT = 10f;              // seconds since Interact
        Vector3 m_ReachDir = Vector3.forward;
        float m_Breath, m_Clock;
        HandPose m_FingersL = HandPose.Relaxed, m_FingersR = HandPose.Relaxed; // as last applied (for the mirror body)

        // Spring-damper lag shared by both arms: offset (m) and rotation (deg) in camera space.
        Vector3 m_SwayPos, m_SwayPosVel, m_SwayRot, m_SwayRotVel;
        Quaternion m_PrevHead;
        Vector3 m_PrevVel;
        bool m_Primed;

        PassportController m_BookOwner;
        Transform m_Book;
        Grabbable m_Item;
        readonly List<Renderer> m_ItemRenderers = new List<Renderer>();

        public static FirstPersonArms Create(DesktopRig rig)
        {
            var go = new GameObject("FirstPersonArms");
            go.layer = Layers.PlayerBody;
            go.transform.SetParent(rig.Head, false);
            var arms = go.AddComponent<FirstPersonArms>();
            arms.m_Rig = rig;
            arms.m_Left = HandMesh.Build("LeftArm", go.transform, HandSide.Left);
            arms.m_Right = HandMesh.Build("RightArm", go.transform, HandSide.Right);
            arms.m_Left.Visible = false;
            arms.m_Right.Visible = false;
            rig.Footstep += arms.OnFootstep;
            return arms;
        }

        void OnDestroy()
        {
            if (m_Rig != null) m_Rig.Footstep -= OnFootstep;
        }

        // ------------------------------------------------------------------ events from the rig
        /// <summary>Interact pressed: reach towards what the reticle is on.</summary>
        public void OnInteract()
        {
            m_ReachT = 0f;
            var hand = m_Rig.MainHand;
            var head = m_Rig.Head;
            var world = hand != null && hand.RayHitSomething ? hand.LastRayEnd : head.position + head.forward;
            var local = head.InverseTransformPoint(world);
            m_ReachDir = local.sqrMagnitude > 1e-4f ? local / local.magnitude : Vector3.forward;
        }

        /// <summary>Landed after a fall: the arms drop with the body and spring back.</summary>
        public void OnLanded(float impact)
        {
            m_SwayPosVel.y -= Mathf.Min(impact * 0.07f, 0.4f);
            m_SwayRotVel.x += Mathf.Min(impact * 8f, 45f);
        }

        /// <summary>Teleported (PlaceAt): no swing or reach carries over.</summary>
        public void ResetMotion()
        {
            m_SwayPos = m_SwayPosVel = m_SwayRot = m_SwayRotVel = Vector3.zero;
            m_Primed = false;
            m_ReachT = 10f;
        }

        /// <summary>
        /// For the mirror body: this frame's first-person hand (wrist and rotation in world space, fingers) and how strongly a
        /// held pose (passport on the left; carried item, reach or tear on the right) should override the body's own arm
        /// swing. False while that arm is lowered out of view.
        /// </summary>
        public bool GetHand(bool left, out Vector3 wrist, out Quaternion rotation, out HandPose fingers, out float hold)
        {
            var arm = left ? m_Left : m_Right;
            wrist = arm.Root.position;
            rotation = arm.Root.rotation;
            fingers = left ? m_FingersL : m_FingersR;
            hold = left ? m_PassW : Mathf.Max(m_HoldW, Mathf.Max(ReachWeight(m_ReachT), m_TearW));
            return arm.Visible && (left ? m_ShowL : m_ShowR) > 0.5f;
        }

        void OnFootstep(string surface, bool running)
        {
            // Each footfall jolts the arms: a soft bob walking, a hard "hmf" running.
            m_SwayPosVel.y -= running ? 0.13f : 0.045f;
            m_SwayRotVel.x += running ? 22f : 7f;
        }

        // ------------------------------------------------------------------ per frame
        void LateUpdate()
        {
            if (m_Rig == null || m_Rig.Cam == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float udt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            var head = m_Rig.Head;
            var hand = m_Rig.MainHand;

            // What the arms may do: hidden in menus and pauses; in cutscenes only a hand that is holding something stays.
            var book = Book();
            bool bookUp = book != null && book.gameObject.activeInHierarchy;
            bool holding = hand != null && hand.Held != null;
            bool menu = Game.InMenu || Game.Paused || m_Rig.PointerMode;
            bool locked = Game.InputLocked;
            m_ShowL = Mathf.MoveTowards(m_ShowL, menu || (locked && !bookUp) ? 0f : 1f, udt / ShowSeconds);
            m_ShowR = Mathf.MoveTowards(m_ShowR, menu || (locked && !holding) ? 0f : 1f, udt / ShowSeconds);
            m_Left.Visible = m_ShowL > 0.001f;
            m_Right.Visible = m_ShowR > 0.001f;
            if (!m_Left.Visible && !m_Right.Visible) { m_Primed = false; return; }

            m_PassW = Approach(m_PassW, bookUp ? 1f : 0f, 14f, udt);
            m_HoldW = Approach(m_HoldW, holding ? 1f : 0f, 10f, dt);
            bool widening = m_Rig.Widening;
            m_TearW = Approach(m_TearW, widening ? 1f : 0f, widening ? 8f : 4f, dt);
            m_ReachT += dt;
            if (!holding) m_Item = null;
            else if (hand.Held != m_Item) CacheItem(hand.Held);

            // Body state.
            var vel = m_Rig.Velocity;
            float speed = new Vector2(vel.x, vel.z).magnitude;
            float walk = Mathf.Clamp01(speed / Mathf.Max(0.1f, m_Rig.WalkSpeed));
            float run = Mathf.InverseLerp(m_Rig.WalkSpeed * 1.05f, m_Rig.WalkSpeed * m_Rig.SprintMultiplier, speed);
            float exert = m_Rig.Exertion;
            float phase = m_Rig.GaitPhase;
            m_Breath += udt * 2f * Mathf.PI / Mathf.Lerp(4.2f, 1.5f, exert); // slow breaths, quick after a run
            m_Clock += dt;
            UpdateSway(head, vel, exert, dt);

            // Right arm: gait, then holding, reaching and straining on top.
            Locomotion(-Mathf.Cos(phase), walk, run, exert, phase, false, out var rp, out var rr, out var rf);
            float swayR = 1f;
            if (m_HoldW > 0.001f && m_Item != null && HoldTarget(head, out var hp, out var hr))
            {
                Blend(ref rp, ref rr, ref rf, hp, hr, HoldFingers, m_HoldW);
                swayR = 1f - 0.8f * m_HoldW; // the item hangs off the camera, so the hand mostly stays with it
            }
            float reach = ReachWeight(m_ReachT);
            if (reach > 0.001f)
            {
                ReachTarget(out var p2, out var r2, out var f2);
                Blend(ref rp, ref rr, ref rf, p2, r2, f2, reach);
            }
            if (m_TearW > 0.001f)
            {
                TearTarget(head, out var p3, out var r3, out var f3);
                Blend(ref rp, ref rr, ref rf, p3, r3, f3, m_TearW);
                swayR = Mathf.Lerp(swayR, 1f, m_TearW);
            }
            Apply(m_Right, rp, rr, rf, m_ShowR, swayR, false);

            // Left arm: gait, or the passport.
            Locomotion(Mathf.Cos(phase), walk, run, exert, phase, true, out var lp, out var lr, out var lf);
            float swayL = 1f;
            if (m_PassW > 0.001f && bookUp)
            {
                PassportTarget(book, head, out var pp, out var pr);
                Blend(ref lp, ref lr, ref lf, pp, pr, PassportFingers, m_PassW);
                swayL = 1f - m_PassW; // the book is fixed to the view; the hand holding it must not drift off it
            }
            Apply(m_Left, lp, lr, lf, m_ShowL, swayL, true);
        }

        /// <summary>Idle breathing, walking swing and running pump for one arm (right-hand numbers, mirrored for the left).</summary>
        void Locomotion(float swing, float walk, float run, float exert, float phase, bool left, out Vector3 pos, out Quaternion rot, out HandPose fingers)
        {
            float b = Mathf.Sin(m_Breath + (left ? 0.35f : 0f));
            var breath = new Vector3(0f, (0.0045f + 0.007f * exert) * b, 0.002f * Mathf.Sin(m_Breath + 0.6f));
            pos = IdlePos + breath;
            rot = IdleRot * Quaternion.Euler(-1.2f * b, 0f, 0f);
            fingers = HandPose.Relaxed;
            if (walk > 0.001f)
            {
                // Opposite to the legs; the hand rises into the corner of the view on its forward swing.
                float k = 0.5f + 0.5f * swing;
                pos = Vector3.Lerp(pos, Vector3.Lerp(WalkBackPos, WalkFwdPos, k) + breath * 0.5f, walk);
                rot = Quaternion.Slerp(rot, Quaternion.Slerp(WalkBackRot, WalkFwdRot, k), walk);
            }
            if (run > 0.001f)
            {
                // Pump: a sharpened swing snaps the fist up on each stride; exertion loosens, lowers and flops it.
                float pump = Mathf.Sign(swing) * Mathf.Pow(Mathf.Abs(swing), 0.75f);
                float k = 0.5f + 0.5f * pump;
                var p = Vector3.Lerp(RunBottomPos, RunTopPos, k) + new Vector3(0.014f * exert * Mathf.Sin(phase), -exert * Mathf.Lerp(0.035f, 0.015f, k), 0f);
                var r = Quaternion.Slerp(RunBottomRot, RunTopRot, k) * Quaternion.Euler(12f * exert * (1f - k), 0f, 0f);
                pos = Vector3.Lerp(pos, p, run);
                rot = Quaternion.Slerp(rot, r, run);
                fingers = HandPose.Lerp(fingers, HandPose.Fist.Curled(-0.3f * exert), run);
                // Leaning into the run: the torso tips forward, bringing the arms up and out in front.
                var lean = Quaternion.Euler(-4f * run, 0f, 0f);
                pos = SwayPivot + lean * (pos - SwayPivot);
                rot = lean * rot;
            }
            if (left) { pos = Mirror(pos); rot = Mirror(rot); }
        }

        void PassportTarget(Transform book, Transform head, out Vector3 pos, out Quaternion rot)
        {
            // Follows the book itself, so the hand rises and lowers with the passport's own animation.
            var palm = head.InverseTransformPoint(book.TransformPoint(PassportPalm));
            rot = Quaternion.Inverse(head.rotation) * book.rotation * PassportRot;
            pos = palm - rot * HandRig.PalmAnchor;
        }

        bool HoldTarget(Transform head, out Vector3 pos, out Quaternion rot)
        {
            pos = default; rot = HoldRot;
            if (!ItemBounds(head, out var b)) return false;
            var palm = new Vector3(b.center.x + 0.01f, b.min.y - 0.012f, b.max.z + 0.004f);
            palm = new Vector3(Mathf.Clamp(palm.x, 0.06f, 0.34f), Mathf.Clamp(palm.y, -0.4f, -0.12f), Mathf.Clamp(palm.z, 0.28f, 0.62f));
            pos = palm - rot * HandRig.PalmAnchor;
            return true;
        }

        void ReachTarget(out Vector3 pos, out Quaternion rot, out HandPose fingers)
        {
            // Towards the target but never across the reticle.
            var palm = Vector3.Lerp(ReachPalm, m_ReachDir * 0.47f, 0.5f);
            palm = new Vector3(Mathf.Max(palm.x, 0.05f), Mathf.Min(palm.y, -0.1f), Mathf.Clamp(palm.z, 0.3f, 0.55f));
            rot = Quaternion.LookRotation(Vector3.Lerp(ReachFwd, m_ReachDir, 0.5f), ReachUp);
            pos = palm - rot * HandRig.PalmAnchor;
            fingers = HandPose.Lerp(ReachOpen, ReachGrab, Mathf.SmoothStep(0f, 1f, (m_ReachT - 0.1f) / 0.14f));
        }

        void TearTarget(Transform head, out Vector3 pos, out Quaternion rot, out HandPose fingers)
        {
            var tears = Game.Tears;
            var seam = tears != null && tears.Active != null ? tears.Active.OpeningCenter
                : tears != null && tears.HasTarget ? tears.TargetPoint : head.position + head.forward * 2f;
            var dir = head.InverseTransformDirection(seam - head.position);
            if (dir.sqrMagnitude > 1e-6f) dir /= dir.magnitude;
            float t = m_Clock;
            float heave = Mathf.Sin(t * 2f * Mathf.PI * 1.4f);
            // Leaning towards the seam, trembling, heaving in and out with the pull.
            var palm = TearPalm + new Vector3(Mathf.Clamp(dir.x, -0.4f, 0.4f) * 0.06f, Mathf.Clamp(dir.y, -0.4f, 0.4f) * 0.05f, 0.012f * heave);
            palm += new Vector3((Mathf.PerlinNoise(t * 9f, 0.3f) - 0.5f) * 0.006f, (Mathf.PerlinNoise(0.7f, t * 11f) - 0.5f) * 0.006f, 0f);
            rot = TearRot * Quaternion.Euler(4f * Mathf.Sin(t * 2f * Mathf.PI * 1.4f + 0.5f), 0f, 0f);
            pos = palm - rot * HandRig.PalmAnchor;
            fingers = TearFingers.Curled(0.06f * Mathf.Sin(t * 2f * Mathf.PI * 1.4f + 1f));
        }

        void Apply(HandRig arm, Vector3 pos, Quaternion rot, in HandPose fingers, float show, float sway, bool left)
        {
            if (!arm.Visible) return;
            if (sway > 0.001f)
            {
                // Spring lag, pivoting near the chin: the hands trail the look and the body.
                var q = Quaternion.Euler(m_SwayRot * sway);
                pos = SwayPivot + q * (pos - SwayPivot) + m_SwayPos * sway;
                rot = q * rot;
            }
            // Lowered below the frame while hidden.
            float e = Mathf.SmoothStep(0f, 1f, show);
            pos += new Vector3(0f, -0.34f, -0.06f) * (1f - e);
            rot *= Quaternion.Euler(28f * (1f - e), 0f, 0f);
            arm.Root.localPosition = pos;
            arm.Root.localRotation = rot;
            arm.SetPose(fingers);
            if (left) m_FingersL = fingers; else m_FingersR = fingers;
            var shoulder = new Vector3(left ? -ShoulderX : ShoulderX, ShoulderY, ShoulderZ);
            var pole = new Vector3(left ? -0.6f : 0.6f, -1f, -0.35f);
            arm.AimForearm(transform.TransformPoint(Elbow(shoulder, pos, pole)));
        }

        // ------------------------------------------------------------------ physics
        void UpdateSway(Transform head, Vector3 vel, float exert, float dt)
        {
            var headRot = head.rotation;
            if (!m_Primed) { m_PrevHead = headRot; m_PrevVel = vel; m_Primed = true; }
            if (dt <= 0f) return;
            var dq = Quaternion.Inverse(m_PrevHead) * headRot;
            m_PrevHead = headRot;
            dq.ToAngleAxis(out float ang, out var axis);
            if (ang > 180f) ang -= 360f;
            bool valid = Mathf.Abs(ang) < 30f && !float.IsNaN(axis.x) && !float.IsInfinity(axis.x); // cuts and teleports don't swing
            var angVel = valid ? axis * (ang / dt) : Vector3.zero;                                     // deg/s, camera space
            var acc = Vector3.ClampMagnitude(Quaternion.Inverse(headRot) * ((vel - m_PrevVel) / dt), 25f); // m/s², camera space
            m_PrevVel = vel;

            // The arms trail the look (pitch/yaw), bank into turns and tip back as the body speeds up; then overshoot and settle.
            var rotTarget = new Vector3(Mathf.Clamp(-angVel.x * 0.022f, -6f, 6f) - acc.z * 0.25f,
                                        Mathf.Clamp(-angVel.y * 0.022f, -6f, 6f),
                                        Mathf.Clamp(angVel.y * 0.012f, -4f, 4f));
            var posTarget = Vector3.ClampMagnitude(new Vector3(-angVel.y * 0.00006f, angVel.x * 0.00006f, 0f) - acc * 0.0035f, 0.05f);
            float zeta = Mathf.Lerp(0.5f, 0.36f, exert); // looser when out of breath
            Spring(ref m_SwayRot, ref m_SwayRotVel, rotTarget, 11f, zeta, dt);
            Spring(ref m_SwayPos, ref m_SwayPosVel, posTarget, 9f, zeta, dt);
            m_SwayPos = Vector3.ClampMagnitude(m_SwayPos, 0.09f);
        }

        static void Spring(ref Vector3 x, ref Vector3 v, Vector3 target, float omega, float zeta, float dt)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / 0.01f)); // stable at low frame rates
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                var a = (target - x) * (omega * omega) - v * (2f * zeta * omega);
                v += a * h;
                x += v * h;
            }
        }

        /// <summary>Two-bone reach from the shoulder: where the elbow sits for this wrist, bent towards the pole.</summary>
        static Vector3 Elbow(Vector3 shoulder, Vector3 wrist, Vector3 pole)
        {
            var d = wrist - shoulder;
            float dist = d.magnitude;
            if (dist < 1e-4f) return shoulder + Vector3.down * UpperArm;
            var dir = d / dist;
            dist = Mathf.Clamp(dist, Mathf.Abs(UpperArm - ForearmLength) + 0.001f, UpperArm + ForearmLength - 0.001f);
            float x = (UpperArm * UpperArm - ForearmLength * ForearmLength + dist * dist) / (2f * dist);
            float r = Mathf.Sqrt(Mathf.Max(0f, UpperArm * UpperArm - x * x));
            var bend = pole - Vector3.Dot(pole, dir) * dir;
            float bm = bend.magnitude;
            bend = bm > 1e-5f ? bend / bm : Vector3.down;
            return shoulder + dir * x + bend * r;
        }

        // ------------------------------------------------------------------ helpers
        static float Approach(float x, float target, float rate, float dt) => x + (target - x) * (1f - Mathf.Exp(-rate * dt));

        static float ReachWeight(float t)
        {
            if (t >= 0.5f) return 0f;
            if (t < 0.15f) { float u = t / 0.15f; return 1f - (1f - u) * (1f - u); }
            return 1f - Mathf.SmoothStep(0f, 1f, (t - 0.15f) / 0.35f);
        }

        static void Blend(ref Vector3 pos, ref Quaternion rot, ref HandPose fingers, Vector3 p, Quaternion r, in HandPose f, float w)
        {
            pos = Vector3.Lerp(pos, p, w);
            rot = Quaternion.Slerp(rot, r, w);
            fingers = HandPose.Lerp(fingers, f, w);
        }

        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);
        static Quaternion Mirror(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);

        Transform Book()
        {
            var p = Game.Passport;
            if (p == null) return null;
            if (m_BookOwner != p) { m_BookOwner = p; m_Book = p.transform.Find("Book"); }
            return m_Book;
        }

        void CacheItem(Grabbable item)
        {
            m_Item = item;
            m_ItemRenderers.Clear();
            item.GetComponentsInChildren(false, m_ItemRenderers);
        }

        /// <summary>The held item's box in camera space (from its renderers' own bounds).</summary>
        bool ItemBounds(Transform head, out Bounds b)
        {
            b = default;
            bool any = false;
            var toCam = head.worldToLocalMatrix;
            foreach (var r in m_ItemRenderers)
            {
                if (r == null || !r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                // Mesh renderers: their own tight local box. Skinned ones: the world box (their local box is in root-bone space).
                bool skinned = r is SkinnedMeshRenderer;
                var lb = skinned ? r.bounds : r.localBounds;
                var m = skinned ? toCam : toCam * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return any;
        }
    }
}
