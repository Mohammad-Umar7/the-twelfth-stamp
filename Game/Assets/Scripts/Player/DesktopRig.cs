using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwelfthStamp
{
    /// <summary>Keyboard + mouse first-person rig. Never touches XR.</summary>
    public class DesktopRig : PlayerRig
    {
        CharacterController m_Controller;
        Interactor m_Hand;
        Transform m_PassportMount;
        FirstPersonArms m_Arms;
        float m_Pitch;
        float m_Yaw;
        float m_VerticalSpeed;
        bool m_Pointer;
        readonly List<Interactor> m_Hands = new List<Interactor>();

        // Body motion: the horizontal velocity eases in and out; the view bobs with the gait and dips on landing.
        Vector3 m_Velocity;
        float m_AirTime, m_FallSpeed, m_Grounded = 1f;
        float m_Dip, m_DipVel;
        int m_StepParity;
        float m_LastStepDistance;

        public const float EyeHeight = 1.62f;
        public float WalkSpeed = 2.6f;
        /// <summary>Hold Shift while moving forward (playtest: walking felt slow between encounters).</summary>
        public float SprintMultiplier = 1.75f;
        /// <summary>Footstep stride (m) walking and sprinting, eased by speed; two strides make one gait cycle. The longer running
        /// stride stops the steps pattering (3.9 a second at a sprint instead of 6.9); the head bob and arm pump use the same stride.</summary>
        public const float StrideWalk = 0.7f, StrideRun = 1.18f;
        const float Accel = 12f, SprintAccel = 6f, Decel = 16f; // m/s²: start, sprint build-up, braking
        // Head bob: vertical travel walking / running and lateral travel (peak to peak, m), running roll (±degrees).
        const float BobWalk = 0.025f, BobRun = 0.05f, BobSide = 0.015f, BobRoll = 0.6f;

        public override IReadOnlyList<Interactor> Hands => m_Hands;
        public override Interactor MainHand => m_Hand;
        public override Transform PassportMount => m_PassportMount;
        public override Ray TearAimRay => new Ray(Head.position, Head.forward);

        /// <summary>Horizontal body velocity (m/s, world).</summary>
        public Vector3 Velocity => m_Velocity;
        /// <summary>Gait cycle, 0..2π over two strides; a footfall lands at 0 and at π.</summary>
        public float GaitPhase { get; private set; }
        /// <summary>True while the cursor is free for a menu.</summary>
        public bool PointerMode => m_Pointer;
        /// <summary>True while the player holds Tear to open or widen a tear (the right hand strains at the seam).</summary>
        public bool Widening { get; private set; }
        /// <summary>Automated tests only: drive the movement input (and Shift) without a keyboard, through the same path.</summary>
        public Vector2? TestMove;
        public bool TestSprint;
        public FirstPersonArms Arms => m_Arms;

        public static DesktopRig Create(Transform parent)
        {
            var go = new GameObject("DesktopRig");
            go.transform.SetParent(parent, false);
            go.layer = Layers.PlayerBody;
            var rig = go.AddComponent<DesktopRig>();
            rig.Build();
            return rig;
        }

        void Build()
        {
            m_Controller = gameObject.AddComponent<CharacterController>();
            m_Controller.height = 1.75f;
            m_Controller.radius = 0.28f;
            m_Controller.center = new Vector3(0, 0.875f, 0);
            m_Controller.stepOffset = 0.32f;
            m_Controller.slopeLimit = 50f;
            m_Controller.skinWidth = 0.03f;
            // Momentum makes the first steps tiny; at high frame rates they fall under the default 1 mm minimum move and
            // the controller would never start moving (seen at ~900 fps in the tests; a 144-240 Hz display is close).
            m_Controller.minMoveDistance = 0f;

            var head = new GameObject("Head");
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0, EyeHeight, 0);
            Cam = head.AddComponent<Camera>();
            Cam.fieldOfView = 72f;
            ConfigureCamera(Cam);
            head.AddComponent<AudioListener>();
            head.tag = "MainCamera";

            var hold = new GameObject("HoldPoint").transform;
            hold.SetParent(head.transform, false);
            hold.localPosition = new Vector3(0.2f, -0.2f, 0.48f);

            m_Hand = head.AddComponent<Interactor>();
            m_Hand.Side = HandSide.Desktop;
            m_Hand.HoldPoint = hold;
            m_Hand.RayOrigin = head.transform;
            m_Hands.Add(m_Hand);

            m_PassportMount = new GameObject("PassportMount").transform;
            m_PassportMount.SetParent(head.transform, false);
            m_PassportMount.localPosition = new Vector3(-0.19f, -0.155f, 0.37f); // lower left: keeps the reticle and the right-hand card clear
            m_PassportMount.localRotation = Quaternion.Euler(21f, -26f, 0f); // page faces the eyes
            m_Arms = FirstPersonArms.Create(this);
            MirrorAvatar.Create(this); // Saeed's whole body, drawn only in mirrors
            m_LastFeet = transform.position;
            SetPointerMode(false);
        }

        public override void PlaceAt(Vector3 feet, float yaw)
        {
            m_Controller.enabled = false;
            // 2 cm above the floor: placing the capsule exactly on the surface lets the controller (minMoveDistance 0)
            // "depenetrate" sideways at floor seams, which stalled scripted walks; gravity settles it at once.
            transform.position = feet + Vector3.up * 0.02f;
            m_Yaw = yaw;
            m_Pitch = 0f;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            // No momentum, bob or landing carries across a teleport.
            m_Velocity = Vector3.zero;
            m_Dip = m_DipVel = 0f;
            m_AirTime = m_FallSpeed = 0f;
            m_Grounded = 1f;
            Head.localPosition = new Vector3(0, EyeHeight, 0);
            Head.localRotation = Quaternion.identity;
            m_Controller.enabled = true;
            m_VerticalSpeed = 0f;
            m_LastFeet = Feet;
            if (m_Arms != null) m_Arms.ResetMotion();
        }

        public float Yaw => m_Yaw;
        public float Pitch => m_Pitch;

        /// <summary>Scripted view (film capture): turns the view as mouse look would, without the teleport reset of PlaceAt.</summary>
        public void SetLook(float yaw, float pitch)
        {
            m_Yaw = yaw;
            m_Pitch = Mathf.Clamp(pitch, -82f, 82f);
            transform.rotation = Quaternion.Euler(0, m_Yaw, 0);
            Head.localRotation = Quaternion.Euler(m_Pitch, 0, 0);
        }

        /// <summary>Tilts the view up or down (story shots frame their subject while input is locked).</summary>
        public void SetPitch(float pitch)
        {
            m_Pitch = Mathf.Clamp(pitch, -82f, 82f);
            Head.localRotation = Quaternion.Euler(m_Pitch, 0, 0);
        }

        public override void SetPointerMode(bool on)
        {
            m_Pointer = on;
            Cursor.lockState = on ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = on;
        }

        public override void GetPointers(List<PointerState> pointers)
        {
            var input = Game.Input;
            var mouse = Mouse.current;
            Vector2 pos = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
            var ray = m_Pointer ? Cam.ScreenPointToRay(pos) : new Ray(Head.position, Head.forward);
            pointers.Add(new PointerState
            {
                Ray = ray,
                Down = input.Click.WasPressedThisFrame(),
                Held = input.Click.IsPressed(),
                Up = input.Click.WasReleasedThisFrame(),
                Hand = HandSide.Desktop
            });
        }

        void Update()
        {
            var input = Game.Input;
            if (input == null) return;

            // Focus loss: free the cursor so the player can leave the window; the pause menu handles the rest.
            if (!Application.isFocused && !m_Pointer) Cursor.lockState = CursorLockMode.None;
            else if (Application.isFocused && !m_Pointer && Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;

            bool canAct = !Game.InputLocked && !Game.InMenu;

            if (canAct)
            {
                float sens = 0.075f * (Game.Settings?.mouseSensitivity ?? 1f);
                var look = input.Look.ReadValue<Vector2>();
                m_Yaw += look.x * sens;
                m_Pitch -= look.y * sens * ((Game.Settings?.invertY ?? false) ? -1f : 1f);
                m_Pitch = Mathf.Clamp(m_Pitch, -82f, 82f);
                transform.rotation = Quaternion.Euler(0, m_Yaw, 0);
                Head.localRotation = Quaternion.Euler(m_Pitch, 0, 0);
            }

            float dt = Time.deltaTime;
            var move = canAct ? (TestMove ?? input.Move.ReadValue<Vector2>()) : Vector2.zero;
            var dir = transform.right * move.x + transform.forward * move.y;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            bool sprint = canAct && (TestMove.HasValue ? TestSprint : input.Sprint != null && input.Sprint.IsPressed()) && move.y > 0.1f;
            Sprinting = sprint;

            // Landing after a real fall (not a kerb or a stair): a springy dip of the view and a jolt through the arms.
            bool grounded = m_Controller.isGrounded;
            if (grounded && m_AirTime > 0.15f && m_FallSpeed > 2f)
            {
                m_DipVel -= Mathf.Min(m_FallSpeed * 0.22f, 1f);
                if (m_Arms != null) m_Arms.OnLanded(m_FallSpeed);
            }
            if (grounded) { m_AirTime = 0f; m_FallSpeed = 0f; }
            else if (dt > 0f) { m_AirTime += dt; m_FallSpeed = Mathf.Max(m_FallSpeed, -m_VerticalSpeed); }

            if (grounded) m_VerticalSpeed = -1f; else m_VerticalSpeed -= 9.81f * dt;
            if (m_Controller.enabled && dt > 0f)
            {
                // Momentum: ease into a walk, let a sprint build, brake a little harder than we start.
                var target = dir * WalkSpeed * (sprint ? SprintMultiplier : 1f);
                bool braking = target.sqrMagnitude < m_Velocity.sqrMagnitude - 1e-4f || Vector3.Dot(target, m_Velocity) < 0f;
                float rate = braking ? Decel : m_Velocity.magnitude >= WalkSpeed - 0.05f ? SprintAccel : Accel;
                m_Velocity = Vector3.MoveTowards(m_Velocity, target, rate * dt);
                var hits = m_Controller.Move((m_Velocity + Vector3.up * m_VerticalSpeed) * dt);
                // Walls and furniture take the speed away (no stored momentum flinging the player round a corner), but only
                // when something was actually hit sideways: a tiny step can report zero velocity and must not cancel a run.
                if ((hits & CollisionFlags.Sides) != 0)
                {
                    var actual = m_Controller.velocity;
                    actual.y = 0f;
                    if (actual.sqrMagnitude < m_Velocity.sqrMagnitude) m_Velocity = actual;
                }
            }
            float stride = Mathf.Lerp(StrideWalk, StrideRun, Mathf.InverseLerp(WalkSpeed, WalkSpeed * SprintMultiplier, m_Velocity.magnitude));
            UpdateFootsteps(stride);
            UpdateBob(dt, grounded, stride);

            m_Hand.UpdateFocus(canAct);
            if (!canAct) { HoldRemoveGlasses = 0f; Widening = false; return; }

            if (input.Interact.WasPressedThisFrame())
            {
                if (m_Arms != null) m_Arms.OnInteract();
                m_Hand.PressUse();
            }
            if (input.PassportToggle.WasPressedThisFrame()) Game.Passport?.Toggle(HandSide.Desktop);

            var passportOpen = Game.Passport != null && Game.Passport.IsOpen;
            if (passportOpen)
            {
                float scroll = input.DestScroll.ReadValue<float>();
                if (scroll > 0.1f) Game.Passport.CycleDestination(-1);
                else if (scroll < -0.1f) Game.Passport.CycleDestination(1);
                if (input.DestPrev.WasPressedThisFrame()) Game.Passport.CycleDestination(-1);
                if (input.DestNext.WasPressedThisFrame()) Game.Passport.CycleDestination(1);
            }

            var tears = Game.Tears;
            Widening = passportOpen && tears != null && input.Tear.IsPressed() && (tears.HasTarget || (tears.Active != null && !tears.Active.Closing));
            if (tears != null)
            {
                if (passportOpen && input.Tear.IsPressed()) tears.HoldWiden(Time.deltaTime, HandSide.Desktop);
                else tears.ReleaseWiden();
                if (input.Seal.IsPressed()) tears.HoldSeal(Time.deltaTime, HandSide.Desktop);
                else tears.ReleaseSeal();
            }

            if (!passportOpen && input.Tear.WasPressedThisFrame() && m_Hand.Held != null && m_Hand.Held.OnActivate != null)
                m_Hand.Held.OnActivate(m_Hand.Held, m_Hand);

            if (input.Recall.WasPressedThisFrame()) Game.Story?.RecallEssential(m_Hand);
            if (input.Objectives.WasPressedThisFrame()) Hud.I?.ToggleObjectives();

            if (input.RemoveGlasses.IsPressed()) HoldRemoveGlasses += Time.deltaTime;
            else HoldRemoveGlasses = 0f;
            if (HoldRemoveGlasses >= 2f) { HoldRemoveGlasses = 0f; Game.Story?.RequestEarlyGlassesRemoval(); }
        }

        /// <summary>
        /// Head bob from the step cycle, plus the landing dip: an additive offset on the camera's local pose, rebuilt from
        /// scratch every frame (never accumulates). Mouse look is untouched apart from a tiny roll while running.
        /// Off with Reduced effects.
        /// </summary>
        void UpdateBob(float dt, bool grounded, float stride)
        {
            // Gait phase from the footstep stride counter (PlayerRig resets it at each footfall).
            if (m_StepDistance < m_LastStepDistance - 1e-4f) m_StepParity ^= 1;
            m_LastStepDistance = m_StepDistance;
            float s = Mathf.Clamp01(m_StepDistance / stride);
            GaitPhase = (m_StepParity + s) * Mathf.PI;

            if (dt > 0f)
            {
                m_Grounded = Mathf.MoveTowards(m_Grounded, grounded ? 1f : 0f, dt * 6f);
                // Landing dip: an underdamped spring, springy and settled in about half a second.
                const float w = 14f, z = 0.45f;
                int n = Mathf.Max(1, Mathf.CeilToInt(dt / 0.01f));
                float h = dt / n;
                for (int i = 0; i < n; i++)
                {
                    m_DipVel += (-w * w * m_Dip - 2f * z * w * m_DipVel) * h;
                    m_Dip += m_DipVel * h;
                }
                m_Dip = Mathf.Max(m_Dip, -0.07f);
            }

            var pos = new Vector3(0f, EyeHeight, 0f);
            float roll = 0f;
            if (Game.Settings == null || !Game.Settings.reducedEffects)
            {
                float speed = m_Velocity.magnitude;
                float walk = Mathf.Clamp01(speed / WalkSpeed) * m_Grounded;
                float run = Mathf.InverseLerp(WalkSpeed, WalkSpeed * SprintMultiplier, speed) * m_Grounded;
                // Lowest on each footfall: smooth when walking, a sharp drop onto the foot when running. Both average zero.
                float walkShape = -Mathf.Cos(2f * Mathf.PI * s);
                float runShape = 2f * Mathf.Sin(Mathf.PI * s) - 1.2732f;
                float travel = BobWalk * walk + (BobRun - BobWalk) * run;
                pos.y += 0.5f * travel * Mathf.Lerp(walkShape, runShape, run) + m_Dip;
                pos.x += 0.5f * BobSide * walk * Mathf.Sin(GaitPhase);
                roll = -BobRoll * run * Mathf.Sin(GaitPhase);
            }
            Head.localPosition = pos;
            Head.localRotation = Quaternion.Euler(m_Pitch, 0f, roll);
        }

        public override void ApplySettings()
        {
            if (Cam != null) Cam.fieldOfView = 72f;
        }
    }
}
