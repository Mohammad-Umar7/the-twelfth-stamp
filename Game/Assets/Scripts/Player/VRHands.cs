using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwelfthStamp
{
    /// <summary>
    /// VR hands. Saeed's hand is fixed to each tracked controller grip (a constant modelling offset only, never animated
    /// off the tracked pose), the fingers follow the grip and trigger (a point when the index is off the trigger), and a
    /// jacket sleeve runs from the wrist back to an estimated elbow. The sleeve's far end lags on a spring, so stick
    /// walking, running and turning feel physical while the hand itself stays locked. The controller model shows only,
    /// ghosted, while its button labels are up. The camera is never touched (comfort).
    /// </summary>
    [DefaultExecutionOrder(90)]
    public class VRHands : MonoBehaviour
    {
        /// <summary>
        /// The controller model's frame under the OpenXR grip pose. In Unity the grip's +Z runs up the handle through the curled
        /// fingers (little finger to thumb), its +Y points forward and down out of the hand, and its +X is the same world side
        /// (towards the left) for both hands. The model points along +Z with its handle 22° forward of +Y: tilting 68° about X
        /// lines the handles up, and a 180° roll about the handle turns the head to point forward and the sticks outward.
        /// (Playtest, Quest 3: the 68° tilt alone left both controllers and hands pointing backwards, "crooked".) The hands,
        /// the hold point and the passport mount are placed through the same constant.
        /// </summary>
        public static readonly Quaternion GripToModel = Quaternion.AngleAxis(180f, Vector3.forward) * Quaternion.Euler(68f, 0f, 0f);

        // The hand in the controller model's frame: knuckles forward round the handle, thumb on the face plate, index at the trigger.
        static readonly Vector3 HandForwardInModel = new Vector3(0f, 0.052f, 0.9986f);
        static readonly Vector3 GripInHand = new Vector3(0.004f, -0.033f, 0.052f); // controller origin in wrist space (right hand)
        const float UpperArm = 0.3f, ForearmLength = 0.27f;
        static readonly HandPose ThumbUp = new HandPose(0f, 0f, 0f, 0f, 0f, -0.25f, -0.3f);

        class Hand
        {
            public HandSide Side;
            public HandRig Rig;
            public Transform GripT;
            public ControllerModel Model;
            public InputAction GripAxis, TriggerAxis, TriggerTouch, ThumbTouch;
            public float Grip, Trigger, IndexRest = 1f, ThumbDown = 1f;
            public HandPose Pose = HandPose.Relaxed;
            public Vector3 Lag, LagVel;                       // elbow-end spring offset (world, m)
            public Vector3 PrevRigVel, PrevWristLocal, PrevWristVel;
            public bool Primed;
        }

        VRRig m_Rig;
        readonly List<Hand> m_Hands = new List<Hand>();
        readonly List<InputAction> m_Actions = new List<InputAction>();
        Matrix4x4 m_PrevRig;
        Quaternion m_PrevRigRot;
        bool m_RigPrimed, m_YawPrimed;
        float m_BodyYaw, m_Kick;

        public static VRHands Create(VRRig rig)
        {
            var go = new GameObject("VRHands");
            go.transform.SetParent(rig.transform, false);
            var hands = go.AddComponent<VRHands>();
            hands.m_Rig = rig;
            hands.m_Hands.Add(hands.MakeHand(HandSide.Left));
            hands.m_Hands.Add(hands.MakeHand(HandSide.Right));
            rig.Footstep += hands.OnFootstep;
            return hands;
        }

        Hand MakeHand(HandSide side)
        {
            bool left = side == HandSide.Left;
            var grip = m_Rig.GripOf(side);
            var rig = HandMesh.Build(left ? "LeftHand" : "RightHand", m_Rig.FrameOf(side), side); // turned with the real controller
            // A fixed offset so the palm wraps the controller handle at the tracked grip pose (nothing animates it).
            var rot = Quaternion.LookRotation(HandForwardInModel, left ? Vector3.left : Vector3.right);
            var g = left ? new Vector3(-GripInHand.x, GripInHand.y, GripInHand.z) : GripInHand;
            rig.Root.localRotation = GripToModel * rot;
            rig.Root.localPosition = GripToModel * -(rot * g);
            rig.SetPose(new HandPose(0.55f, 0.9f, 0.93f, 0.96f, 0.12f, 0f, 0f));
            rig.Visible = false;
            string hand = "<XRController>" + (left ? "{LeftHand}" : "{RightHand}");
            return new Hand
            {
                Side = side, Rig = rig, GripT = grip, Model = m_Rig.ModelOf(side),
                GripAxis = Action(InputActionType.Value, hand + "/{Grip}"),
                TriggerAxis = Action(InputActionType.Value, hand + "/{Trigger}"),
                TriggerTouch = Action(InputActionType.Button, hand + "/{TriggerTouch}"),
                ThumbTouch = Action(InputActionType.Button, hand + "/{Primary2DAxisTouch}", hand + "/{PrimaryTouch}", hand + "/{SecondaryTouch}", hand + "/{ThumbrestTouch}"),
            };
        }

        /// <summary>Analog and touch controls for the fingers (GameInput only binds the buttons).</summary>
        InputAction Action(InputActionType type, params string[] paths)
        {
            var a = new InputAction(null, type);
            foreach (var p in paths) a.AddBinding(p);
            a.Enable();
            m_Actions.Add(a);
            return a;
        }

        void OnDestroy()
        {
            if (m_Rig != null) m_Rig.Footstep -= OnFootstep;
            foreach (var a in m_Actions) { a.Disable(); a.Dispose(); }
            m_Actions.Clear();
        }

        void OnFootstep(string surface, bool running)
        {
            // Stick walking only: each footfall jogs the sleeves ("hmf"), harder when running. Teleport hops don't.
            if (Game.Settings == null || !Game.Settings.smoothLocomotion || m_Rig.MoveSpeed < 0.5f) return;
            m_Kick += running ? 0.16f : 0.07f;
        }

        void LateUpdate()
        {
            if (m_Rig == null || Game.Input == null) return;
            float dt = Time.deltaTime;
            var head = m_Rig.Head;
            var rigT = m_Rig.transform;

            // Teleports and snap turns move the rig in one frame: carry the sleeves along rather than swinging them.
            bool jump = !m_RigPrimed;
            var turn = Quaternion.identity;
            if (m_RigPrimed)
            {
                var moved = rigT.position - (Vector3)m_PrevRig.GetColumn(3);
                turn = rigT.rotation * Quaternion.Inverse(m_PrevRigRot);
                jump = moved.sqrMagnitude > 0.5f * 0.5f || Quaternion.Angle(m_PrevRigRot, rigT.rotation) > 8f;
            }

            // The shoulders follow the head's heading a little late (never more than 40° behind).
            var hf = head.forward;
            hf.y = 0f;
            if (hf.sqrMagnitude > 1e-4f)
            {
                float yaw = Mathf.Atan2(hf.x, hf.z) * Mathf.Rad2Deg;
                if (!m_YawPrimed || jump) { m_BodyYaw = yaw; m_YawPrimed = true; }
                m_BodyYaw = Mathf.LerpAngle(m_BodyYaw, yaw, 1f - Mathf.Exp(-dt * 2.5f));
                float off = Mathf.DeltaAngle(m_BodyYaw, yaw);
                if (Mathf.Abs(off) > 40f) m_BodyYaw += off - Mathf.Sign(off) * 40f;
            }
            var body = Quaternion.Euler(0f, m_BodyYaw, 0f);

            float kick = m_Kick;
            m_Kick = 0f;
            foreach (var h in m_Hands) UpdateHand(h, dt, jump, turn, body, kick);
            m_PrevRig = rigT.localToWorldMatrix;
            m_PrevRigRot = rigT.rotation;
            m_RigPrimed = true;
        }

        void UpdateHand(Hand h, float dt, bool jump, Quaternion turn, Quaternion body, float kick)
        {
            var input = Game.Input;
            bool tracked = IsTracked(h, input);
            h.Rig.Visible = tracked;
            if (h.Model != null) h.Model.HandsShown = true;
            if (!tracked) { h.Primed = false; return; }
            if (dt <= 0f) return;

            Fingers(h, input, dt);

            // The sleeve: from the wrist to an elbow found by a two-bone reach off an estimated shoulder.
            float s = h.Side == HandSide.Left ? -1f : 1f;
            var fwd = body * Vector3.forward;
            var right = body * Vector3.right;
            var head = m_Rig.Head;
            var wrist = h.Rig.Root.position;
            var shoulder = head.position - fwd * 0.07f + Vector3.down * 0.2f + right * (0.18f * s);
            var pole = right * (0.55f * s) + Vector3.down - fwd * 0.35f;
            var target = Elbow(shoulder, wrist, pole);

            // Inertia: how the rig itself carried the elbow this frame (stick walking, smooth turning), and the hand's own motion.
            var rigT = m_Rig.transform;
            var rigVel = jump ? Vector3.zero : (target - m_PrevRig.MultiplyPoint3x4(rigT.InverseTransformPoint(target))) / dt;
            var wristLocal = rigT.InverseTransformPoint(wrist);
            if (jump) { h.Lag = turn * h.Lag; h.LagVel = turn * h.LagVel; }
            if (!h.Primed || jump)
            {
                h.PrevRigVel = rigVel;
                h.PrevWristLocal = wristLocal;
                h.PrevWristVel = Vector3.zero;
                h.Primed = true;
            }
            var rigAcc = Vector3.ClampMagnitude((rigVel - h.PrevRigVel) / dt, 60f);
            var wristVel = (wristLocal - h.PrevWristLocal) / dt;
            var handAcc = Vector3.ClampMagnitude(rigT.TransformDirection(wristVel - h.PrevWristVel) / dt, 40f);
            h.PrevRigVel = rigVel;
            h.PrevWristLocal = wristLocal;
            h.PrevWristVel = wristVel;

            // The elbow end trails the body's motion, swings past on starts, stops and turns, and settles; heavier when exerted.
            var force = -rigAcc * 0.15f - handAcc * 0.03f;
            var trail = Vector3.ClampMagnitude(-rigVel * 0.015f, 0.05f);
            h.LagVel.y -= kick;
            Spring(ref h.Lag, ref h.LagVel, trail, force, 11f, Mathf.Lerp(0.45f, 0.34f, m_Rig.Exertion), dt);
            h.Lag = Vector3.ClampMagnitude(h.Lag, 0.07f);
            h.Rig.AimForearm(target + h.Lag);
        }

        void Fingers(Hand h, GameInput input, float dt)
        {
            float grip = h.GripAxis.controls.Count > 0 ? h.GripAxis.ReadValue<float>() : input.GripOf(h.Side).IsPressed() ? 1f : 0f;
            float trigger = h.TriggerAxis.controls.Count > 0 ? h.TriggerAxis.ReadValue<float>() : input.TriggerOf(h.Side).IsPressed() ? 1f : 0f;
            bool indexOn = trigger > 0.06f || (h.TriggerTouch.controls.Count > 0 && h.TriggerTouch.IsPressed());
            // The thumb rests on the face plate (lifted while the labels show the buttons under it).
            bool labels = h.Model != null && h.Model.LabelsShown;
            bool thumbOn = !labels && (h.ThumbTouch.controls.Count == 0 || h.ThumbTouch.IsPressed());
            float k = 1f - Mathf.Exp(-dt * 18f), k2 = 1f - Mathf.Exp(-dt * 12f);
            h.Grip += (Mathf.Clamp01(grip) - h.Grip) * k;
            h.Trigger += (Mathf.Clamp01(trigger) - h.Trigger) * k;
            h.IndexRest += ((indexOn ? 1f : 0f) - h.IndexRest) * k2;
            h.ThumbDown += ((thumbOn ? 1f : 0f) - h.ThumbDown) * k2;
            // Holding the controller the fingers stay round the handle (playtest, 18 Oct: open, splayed hands while the real
            // ones held the controllers); squeezing the grip closes them tight.
            float wrap = Mathf.Lerp(0.9f, 1.1f, h.Grip);
            float index = Mathf.Lerp(Mathf.Lerp(0.2f, 0.55f, h.IndexRest), 0.95f, h.Trigger); // on the trigger it rests bent; off it, a loose point
            var thumb = HandPose.Lerp(ThumbUp, new HandPose(0f, 0f, 0f, 0f, 0.12f + 0.08f * h.Grip, 0.05f * h.Grip, 0.15f * h.Grip), h.ThumbDown);
            h.Pose = new HandPose(index, wrap, wrap + 0.03f, wrap + 0.06f, thumb.ThumbCurl, thumb.ThumbOppose, thumb.ThumbTuck);
            h.Rig.SetPose(h.Pose);
        }

        /// <summary>For the mirror body: a tracked hand's wrist and rotation (world) and its finger pose. False when not tracked.</summary>
        public bool GetHand(HandSide side, out Vector3 wrist, out Quaternion rotation, out HandPose fingers)
        {
            var h = m_Hands[side == HandSide.Left ? 0 : 1];
            wrist = h.Rig.Root.position;
            rotation = h.Rig.Root.rotation;
            fingers = h.Pose;
            return h.Rig.Visible;
        }

        static bool IsTracked(Hand h, GameInput input)
        {
            var a = h.Side == HandSide.Left ? input.LTracked : input.RTracked;
            if (a != null && a.controls.Count > 0) return a.IsPressed();
            return h.GripT.localPosition.sqrMagnitude > 1e-8f; // no tracking state reported: shown once a pose arrives
        }

        static void Spring(ref Vector3 x, ref Vector3 v, Vector3 target, Vector3 force, float omega, float zeta, float dt)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / 0.01f));
            float step = dt / n;
            for (int i = 0; i < n; i++)
            {
                var a = (target - x) * (omega * omega) - v * (2f * zeta * omega) + force;
                v += a * step;
                x += v * step;
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
    }
}
