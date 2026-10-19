using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace TwelfthStamp
{
    /// <summary>
    /// Quest 3 (PC VR over Link, OpenXR) rig: tracked head and controllers, smooth stick movement (a full push runs) + snap
    /// turn by default, teleport and smooth turning as options, left-handed swap, seated offset, head-in-wall fade,
    /// controller callouts.
    /// Scenery moves, the player's viewpoint is never driven by scripts (only by teleport/turn the player chose).
    /// </summary>
    public class VRRig : PlayerRig
    {
        Transform m_Offset;
        Transform m_LeftGrip, m_RightGrip, m_LeftAim, m_RightAim;
        Transform m_LeftFrame, m_RightFrame; // under each grip, turned to the real controller (AlignFrame)
        Interactor m_Left, m_Right;
        ControllerModel m_LeftModel, m_RightModel;
        Transform m_PassportMountL, m_PassportMountR;
        LineRenderer m_LeftLaser, m_RightLaser, m_Arc;
        Transform m_Reticle;
        CharacterController m_Body;
        readonly List<Interactor> m_Hands = new List<Interactor>();
        readonly List<Vector3> m_ArcPoints = new List<Vector3>();
        bool m_Aiming, m_ArcValid, m_TurnArmed = true, m_Pointer;
        VRHands m_HandVisuals;
        int m_StepParity;
        float m_LastStepDistance;
        Vector3 m_ArcTarget;
        float m_DestinationRepeat;
        bool m_HeadBlocked;

        public override IReadOnlyList<Interactor> Hands => m_Hands;
        public override Interactor MainHand => Game.Input.DominantHand == HandSide.Left ? m_Left : m_Right;
        /// <summary>The drawn hands (finger poses and tracked wrists).</summary>
        public VRHands HandVisuals => m_HandVisuals;
        /// <summary>Gait cycle, 0..2π over two strides; a footfall lands at 0 and at π (the mirror body's legs follow it).</summary>
        public float GaitPhase { get; private set; }
        public Interactor PassportHandInteractor => Game.Input.PassportHand == HandSide.Left ? m_Left : m_Right;
        public override Transform PassportMount => Game.Input.PassportHand == HandSide.Left ? m_PassportMountL : m_PassportMountR;
        public Transform GripOf(HandSide h) => h == HandSide.Left ? m_LeftGrip : m_RightGrip;
        public ControllerModel ModelOf(HandSide h) => h == HandSide.Left ? m_LeftModel : m_RightModel;
        public Transform AimOf(HandSide h) => h == HandSide.Left ? m_LeftAim : m_RightAim;
        /// <summary>The controller's frame: a child of the grip turned about the handle to match the real controller. The
        /// controller model, the drawn hand, the hold point and the passport mount all sit in it.</summary>
        public Transform FrameOf(HandSide h) => h == HandSide.Left ? m_LeftFrame : m_RightFrame;

        /// <summary>
        /// Capture only (--vr-preview, no headset): stops the tracked-pose drivers so a film script can pose the head and the
        /// hands, and widens the window camera towards a headset's view. Never used in play.
        /// </summary>
        public void UseScriptedPose()
        {
            foreach (var d in GetComponentsInChildren<TrackedPoseDriver>(true)) d.enabled = false;
            if (Cam != null) Cam.fieldOfView = 78f;
        }

        public override Ray TearAimRay
        {
            get
            {
                // Aim tears with the dominant hand's pointer (the passport hand is busy holding the book).
                var aim = Game.Input.DominantHand == HandSide.Left ? m_LeftAim : m_RightAim;
                return new Ray(aim.position, aim.forward);
            }
        }

        public static VRRig Create(Transform parent)
        {
            var go = new GameObject("VRRig");
            go.transform.SetParent(parent, false);
            go.layer = Layers.PlayerBody;
            var rig = go.AddComponent<VRRig>();
            rig.Build();
            return rig;
        }

        void Build()
        {
            var input = Game.Input;
            m_Body = gameObject.AddComponent<CharacterController>();
            m_Body.height = 1.6f; m_Body.radius = 0.22f; m_Body.center = new Vector3(0, 0.8f, 0);
            m_Body.stepOffset = 0.3f; m_Body.skinWidth = 0.02f;
            m_Body.minMoveDistance = 0f; // a gentle stick push at 90-120 Hz must still move (default 1 mm minimum)

            m_Offset = new GameObject("CameraOffset").transform;
            m_Offset.SetParent(transform, false);

            var head = new GameObject("Head");
            head.transform.SetParent(m_Offset, false);
            head.transform.localPosition = new Vector3(0, 1.6f, 0);
            Cam = head.AddComponent<Camera>();
            ConfigureCamera(Cam);
#if UNITY_WEBGL
            // Browser VR draws each eye in its own pass on the headset's mobile GPU: no MSAA (it blacks out the Quest
            // browser's view), HDR, post-processing or shadows.
            Cam.allowMSAA = false;
            Cam.allowHDR = false;
            var camData = Cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = false;
            camData.renderShadows = false;
#endif
            head.AddComponent<AudioListener>();
            head.tag = "MainCamera";
            var tpd = head.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tpd.positionInput = new InputActionProperty(input.HeadPos);
            tpd.rotationInput = new InputActionProperty(input.HeadRot);

            m_LeftGrip = TrackedChild("LeftGrip", input.LPos, input.LRot);
            m_RightGrip = TrackedChild("RightGrip", input.RPos, input.RRot);
            m_LeftAim = TrackedChild("LeftAim", input.LAimPos, input.LAimRot);
            m_RightAim = TrackedChild("RightAim", input.RAimPos, input.RAimRot);

            m_LeftFrame = new GameObject("LeftControllerFrame").transform;
            m_LeftFrame.SetParent(m_LeftGrip, false);
            m_RightFrame = new GameObject("RightControllerFrame").transform;
            m_RightFrame.SetParent(m_RightGrip, false);

            m_Left = MakeHand(HandSide.Left, m_LeftGrip, m_LeftFrame, m_LeftAim);
            m_Right = MakeHand(HandSide.Right, m_RightGrip, m_RightFrame, m_RightAim);
            m_LeftModel = ControllerModel.Create(m_LeftFrame, HandSide.Left);
            m_RightModel = ControllerModel.Create(m_RightFrame, HandSide.Right);
            // Hands are drawn at the grips; the controller models sit in the same hand frame and show (ghosted) only with their labels.
            m_LeftModel.transform.localRotation = VRHands.GripToModel;
            m_RightModel.transform.localRotation = VRHands.GripToModel;
            m_HandVisuals = VRHands.Create(this);

            m_PassportMountL = Mount(m_LeftFrame, false);
            m_PassportMountR = Mount(m_RightFrame, true);

            m_LeftLaser = Laser("LeftLaser");
            m_RightLaser = Laser("RightLaser");
            m_Arc = Laser("TeleportArc");
            m_Arc.widthMultiplier = 0.012f;

            var ret = MeshFactory.Disc("TeleportReticle", 0.22f, Mats.Unlit(new Color(0.55f, 0.9f, 0.8f, 0.8f), Tex.Ring, true));
            ret.transform.SetParent(transform, false);
            m_Reticle = ret.transform;
            m_Reticle.gameObject.SetActive(false);
            m_LastFeet = Feet;
            ApplySettings();
            MirrorAvatar.Create(this); // Saeed's whole body, drawn only in mirrors
        }

        Transform TrackedChild(string name, InputAction pos, InputAction rot)
        {
            var t = new GameObject(name).transform;
            t.SetParent(m_Offset, false);
            var d = t.gameObject.AddComponent<TrackedPoseDriver>();
            d.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            d.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            d.positionInput = new InputActionProperty(pos);
            d.rotationInput = new InputActionProperty(rot);
            return t;
        }

        Interactor MakeHand(HandSide side, Transform grip, Transform frame, Transform aim)
        {
            var hold = new GameObject("HoldPoint").transform;
            hold.SetParent(frame, false);
            // Authored in the controller model's frame (y up, z forward as held); same alignment as the model and hands.
            hold.localPosition = VRHands.GripToModel * new Vector3(0, -0.01f, 0.05f);
            hold.localRotation = VRHands.GripToModel;
            var it = grip.gameObject.AddComponent<Interactor>();
            it.Side = side;
            it.HoldPoint = hold;
            it.RayOrigin = aim;
            m_Hands.Add(it);
            return it;
        }

        static Transform Mount(Transform frame, bool mirrored)
        {
            var t = new GameObject("PassportMount").transform;
            t.SetParent(frame, false);
            // Authored in the controller model's frame: in front of and above the controller as held, tilted to the eyes.
            t.localPosition = VRHands.GripToModel * new Vector3(mirrored ? -0.02f : 0.02f, 0.07f, 0.07f);
            t.localRotation = VRHands.GripToModel * Quaternion.Euler(48f, mirrored ? -12f : 12f, 0f);
            return t;
        }

        LineRenderer Laser(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.004f;
            lr.positionCount = 2;
            lr.material = Mats.Unlit(new Color(0.75f, 0.95f, 0.88f, 0.55f), null, true);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        public override void ApplySettings()
        {
            if (m_Offset == null) return;
            var s = Game.Settings;
            m_Offset.localPosition = new Vector3(0, s.seated ? s.seatedHeight : 0f, 0);
        }

        public override void PlaceAt(Vector3 feet, float yaw)
        {
            float headYaw = Head.eulerAngles.y;
            transform.RotateAround(new Vector3(Head.position.x, transform.position.y, Head.position.z), Vector3.up, yaw - headYaw);
            var f = Feet;
            transform.position += new Vector3(feet.x - f.x, feet.y - transform.position.y, feet.z - f.z);
            m_LastFeet = Feet;
        }

        public override void SetPointerMode(bool on) => m_Pointer = on;

        public override void GetPointers(List<PointerState> pointers)
        {
            var input = Game.Input;
            pointers.Add(new PointerState { Ray = new Ray(m_LeftAim.position, m_LeftAim.forward), Down = input.LTrigger.WasPressedThisFrame(), Held = input.LTrigger.IsPressed(), Up = input.LTrigger.WasReleasedThisFrame(), Hand = HandSide.Left });
            pointers.Add(new PointerState { Ray = new Ray(m_RightAim.position, m_RightAim.forward), Down = input.RTrigger.WasPressedThisFrame(), Held = input.RTrigger.IsPressed(), Up = input.RTrigger.WasReleasedThisFrame(), Hand = HandSide.Right });
            // Spectator keyboard/mouse can also drive menus on the desktop mirror.
            if (input.Click.WasPressedThisFrame() || input.Click.IsPressed() || input.Click.WasReleasedThisFrame())
            {
                var mouse = Mouse.current;
                if (mouse != null)
                    pointers.Add(new PointerState { Ray = Cam.ScreenPointToRay(mouse.position.ReadValue()), Down = input.Click.WasPressedThisFrame(), Held = input.Click.IsPressed(), Up = input.Click.WasReleasedThisFrame(), Hand = HandSide.Desktop });
            }
        }

        void Update()
        {
            var input = Game.Input;
            if (input == null) return;
            bool canAct = !Game.InputLocked && !Game.InMenu;

            UpdateHeadBlock();
            AlignFrame(m_LeftGrip, m_LeftAim, m_LeftFrame, HandSide.Left);
            AlignFrame(m_RightGrip, m_RightAim, m_RightFrame, HandSide.Right);
            UpdateLocomotion(canAct);
            // Longer strides as the stick run builds (2.0 m/s walk, 3.4 m/s run), so running steps don't patter.
            float stride = Mathf.Lerp(0.75f, 1.15f, Mathf.InverseLerp(2f, 3.4f, MoveSpeed));
            UpdateFootsteps(stride);
            if (m_StepDistance < m_LastStepDistance - 1e-4f) m_StepParity ^= 1; // a footfall reset the stride counter
            m_LastStepDistance = m_StepDistance;
            GaitPhase = (m_StepParity + Mathf.Clamp01(m_StepDistance / stride)) * Mathf.PI;

            foreach (var h in m_Hands) h.UpdateFocus(canAct && !(Game.Passport != null && Game.Passport.IsHeldBy(h.Side)));
            UpdateLasers(canAct);

            if (input.Menu.WasPressedThisFrame() || input.Pause.WasPressedThisFrame()) MenuSystem.I?.TogglePause();
            if (!canAct) { HoldRemoveGlasses = 0f; return; }

            // Hands: grip = grab, trigger = use.
            HandButtons(m_Left, input.LGrip, input.LTrigger);
            HandButtons(m_Right, input.RGrip, input.RTrigger);

            if (input.PassportButton.WasPressedThisFrame()) Game.Passport?.Toggle(input.PassportHand);
            bool passportOpen = Game.Passport != null && Game.Passport.IsOpen;
            if (passportOpen)
            {
                if (input.DestinationButton.WasPressedThisFrame()) Game.Passport.CycleDestination(1);
                float v = input.TurnStick.ReadValue<Vector2>().y;
                m_DestinationRepeat -= Time.deltaTime;
                if (Mathf.Abs(v) > 0.7f && m_DestinationRepeat <= 0f)
                {
                    Game.Passport.CycleDestination(v > 0 ? -1 : 1);
                    m_DestinationRepeat = 0.35f;
                }
                else if (Mathf.Abs(v) < 0.3f) m_DestinationRepeat = 0f;
            }

            if (Game.Tears != null)
            {
                // One-handed alternative: hold the dominant trigger while aiming at a seam (passport open), and keep holding to
                // widen the tear it opened. Targeting stops once a tear is open, so widening must not need a target (playtest,
                // 18 Oct, Quest 3: holding the trigger opened a slit that never grew).
                var dominantTrigger = input.TriggerOf(input.DominantHand);
                bool tearOpen = Game.Tears.Active != null && !Game.Tears.Active.Closing;
                if (passportOpen && dominantTrigger.IsPressed() && (Game.Tears.HasTarget || tearOpen)) Game.Tears.HoldWiden(Time.deltaTime, input.DominantHand);
                else if (!Game.Tears.IsPulling) Game.Tears.ReleaseWiden();
                if (input.SealButton.IsPressed()) Game.Tears.HoldSeal(Time.deltaTime, input.DominantHand);
                else Game.Tears.ReleaseSeal();
            }

            if (input.RecallButton.WasPressedThisFrame()) Game.Story?.RecallEssential(MainHand);
        }

        void HandButtons(Interactor hand, InputAction grip, InputAction trigger)
        {
            bool passportHolder = Game.Passport != null && Game.Passport.IsHeldBy(hand.Side);
            if (grip.WasPressedThisFrame())
            {
                if (Game.Passport != null && Game.Passport.TryBeginSeamPull(hand)) { }
                else if (!passportHolder) hand.PressGrab();
            }
            if (grip.WasReleasedThisFrame())
            {
                if (Game.Passport != null && Game.Passport.IsPulling(hand)) Game.Passport.EndSeamPull(hand);
                else hand.ReleaseGrab();
            }
            if (trigger.WasPressedThisFrame() && !passportHolder)
            {
                bool tearUse = Game.Passport != null && Game.Passport.IsOpen && Game.Tears != null && (Game.Tears.HasTarget || Game.Tears.Active != null) && hand.Side == Game.Input.DominantHand;
                if (!tearUse) hand.PressUse();
            }
        }

        void UpdateLasers(bool canAct)
        {
            DrawLaser(m_LeftLaser, m_Left, m_LeftAim, canAct);
            DrawLaser(m_RightLaser, m_Right, m_RightAim, canAct);
        }

        void DrawLaser(LineRenderer lr, Interactor hand, Transform aim, bool canAct)
        {
            bool show = m_Pointer || (canAct && hand.Focused != null && !hand.FocusIsNear);
            bool tearAim = canAct && Game.Passport != null && Game.Passport.IsOpen && hand.Side == Game.Input.DominantHand;
            if (tearAim) show = true;
            lr.enabled = show;
            if (!show) return;
            var start = aim.position;
            Vector3 end;
            if (m_Pointer && Physics.Raycast(aim.position, aim.forward, out var hit, 8f, Layers.UIMask, QueryTriggerInteraction.Collide)) end = hit.point;
            else if (tearAim && Game.Tears != null && Game.Tears.HasTarget) end = Game.Tears.TargetPoint;
            else end = hand.RayHitSomething ? hand.LastRayEnd : aim.position + aim.forward * (m_Pointer ? 3f : 1.5f);
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
        }

        // ------------------------------------------------------------------ locomotion
        void UpdateLocomotion(bool canAct)
        {
            var input = Game.Input;
            var s = Game.Settings;
            Vector2 move = canAct ? input.MoveStick.ReadValue<Vector2>() : Vector2.zero;
            Vector2 turn = canAct ? input.TurnStick.ReadValue<Vector2>() : Vector2.zero;
            bool passportOpen = Game.Passport != null && Game.Passport.IsOpen;

            if (s.smoothLocomotion)
            {
                m_Arc.enabled = false; m_Reticle.gameObject.SetActive(false);
                float speed = 0f;
                if (move.sqrMagnitude > 0.04f)
                {
                    // The stick runs (playtest, 18 Oct: "the mc should run when I use the joystick"): a full push is a run at
                    // once, 3.4 m/s (the Passenger chases at 2.8 m/s in VR), a half push a walk; no hold before the run.
                    speed = Mathf.Lerp(1.4f, 3.4f, Mathf.InverseLerp(0.3f, 0.85f, Mathf.Clamp01(move.magnitude)));
                    var dir = Forward * move.y + Vector3.Cross(Vector3.up, Forward) * move.x;
                    if (dir.sqrMagnitude > 1f) dir.Normalize();
                    var local = transform.InverseTransformPoint(Head.position);
                    m_Body.center = new Vector3(local.x, 0.8f, local.z);
                    m_Body.Move(dir * speed * Time.deltaTime + Vector3.down * 2f * Time.deltaTime);
                }
                Sprinting = speed > 2.7f; // breathing, heartbeat and the sleeves read this
            }
            else
            {
                Sprinting = false;
                if (move.y > 0.6f) m_Aiming = true;
                if (m_Aiming)
                {
                    var aimT = input.MoveStick == input.LStick ? m_LeftAim : m_RightAim;
                    ComputeArc(aimT.position, aimT.forward);
                    if (move.magnitude < 0.25f)
                    {
                        m_Aiming = false;
                        m_Arc.enabled = false;
                        m_Reticle.gameObject.SetActive(false);
                        if (m_ArcValid) StartCoroutine(TeleportRoutine(m_ArcTarget));
                    }
                }
            }

            // Turning (the destination selection uses the same stick's up/down while the passport is open).
            if (s.smoothTurn)
            {
                if (Mathf.Abs(turn.x) > 0.2f) transform.RotateAround(new Vector3(Head.position.x, 0, Head.position.z), Vector3.up, turn.x * 75f * Time.deltaTime);
            }
            else
            {
                if (m_TurnArmed && Mathf.Abs(turn.x) > 0.75f && !(passportOpen && Mathf.Abs(turn.y) > Mathf.Abs(turn.x)))
                {
                    m_TurnArmed = false;
                    transform.RotateAround(new Vector3(Head.position.x, 0, Head.position.z), Vector3.up, Mathf.Sign(turn.x) * s.snapAngle);
                    m_LastFeet = Feet;
                }
                else if (Mathf.Abs(turn.x) < 0.3f) m_TurnArmed = true;
            }
        }

        void ComputeArc(Vector3 origin, Vector3 dir)
        {
            m_ArcPoints.Clear();
            var v = dir.normalized * 7.5f;
            var p = origin;
            m_ArcValid = false;
            const float dt = 0.03f;
            for (int i = 0; i < 90; i++)
            {
                var next = p + v * dt;
                v += Vector3.down * 9.81f * dt;
                m_ArcPoints.Add(p);
                if (Physics.Linecast(p, next, out var hit, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    m_ArcPoints.Add(hit.point);
                    bool floor = hit.collider.gameObject.layer == Layers.Floor && hit.normal.y > 0.7f;
                    var flat = hit.point - Feet; flat.y = 0;
                    bool clear = !Physics.CheckCapsule(hit.point + Vector3.up * 0.35f, hit.point + Vector3.up * 1.5f, 0.2f, 1 << Layers.Default, QueryTriggerInteraction.Ignore);
                    m_ArcValid = floor && clear && flat.magnitude < 7f;
                    m_ArcTarget = hit.point;
                    break;
                }
                p = next;
            }
            m_Arc.enabled = true;
            m_Arc.positionCount = m_ArcPoints.Count;
            m_Arc.SetPositions(m_ArcPoints.ToArray());
            var col = m_ArcValid ? new Color(0.55f, 0.95f, 0.8f, 0.85f) : new Color(0.9f, 0.4f, 0.35f, 0.6f);
            m_Arc.startColor = m_Arc.endColor = col;
            m_Reticle.gameObject.SetActive(m_ArcValid);
            if (m_ArcValid) { m_Reticle.position = m_ArcTarget + Vector3.up * 0.02f; m_Reticle.rotation = Quaternion.identity; }
        }

        System.Collections.IEnumerator TeleportRoutine(Vector3 target)
        {
            Fader.I?.Flash(0.08f, 0.1f);
            yield return new WaitForSecondsRealtime(0.08f);
            var f = Feet;
            transform.position += new Vector3(target.x - f.x, target.y - transform.position.y, target.z - f.z);
            m_LastFeet = Feet;
            AudioDirector.I?.Play("foot_stone_1", target, 0.25f);
        }

        /// <summary>
        /// Turns a controller frame about its grip's handle axis so the controller model, the hand round it, the hold point
        /// and the passport mount point where the aim ray points. OpenXR fixes the grip's +Z up the handle; which way the
        /// grip's X and Y face round that axis is the runtime's, and VRHands.GripToModel assumed one (playtest on Quest 3,
        /// 18 Oct: the hands lay flat across the controllers, a quarter turn out). Both poses are fixed to the controller's
        /// body, so the turn is constant; it is re-read every frame only so a reconnected controller is right too.
        /// </summary>
        void AlignFrame(Transform grip, Transform aim, Transform frame, HandSide side)
        {
            if (grip.localPosition.sqrMagnitude < 1e-8f || aim.localPosition.sqrMagnitude < 1e-8f) return; // no pose yet
            var a = Quaternion.Inverse(grip.localRotation) * (aim.localRotation * Vector3.forward); // the aim ray in grip space
            if (a.x * a.x + a.y * a.y < 0.09f) return; // nearly along the handle: nothing to read the turn from
            // GripToModel leaves the model's forward at +90 degrees in the grip's XY plane: turn that onto the aim ray.
            float roll = Mathf.Atan2(a.y, a.x) * Mathf.Rad2Deg - 90f;
            var want = Quaternion.AngleAxis(roll, Vector3.forward);
            float change = Quaternion.Angle(frame.localRotation, want);
            if (change < 0.25f) return;
            frame.localRotation = want;
            if (change > 5f)
                Debug.Log("[TS-VR] " + side + " controller frame: aim ray " + Vector3.Angle(a, Vector3.forward).ToString("0") +
                          " deg off the grip's handle axis; frame turned " + roll.ToString("0") + " deg about it (aim in grip space " + a.ToString("F2") + ")");
        }

        void UpdateHeadBlock()
        {
            bool blocked = Physics.CheckSphere(Head.position, 0.1f, 1 << Layers.Default, QueryTriggerInteraction.Ignore);
            if (blocked != m_HeadBlocked)
            {
                m_HeadBlocked = blocked;
                Fader.I?.SetBlocker(blocked);
            }
        }
    }
}
