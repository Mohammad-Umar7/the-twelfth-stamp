using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwelfthStamp
{
    /// <summary>Gameplay intents. On-screen labels and controller callouts are generated from the real bindings.</summary>
    public enum Intent
    {
        Move, Look, Interact, Grab, Passport, Destination, Tear, Seal, Recall, Pause, Teleport, Turn, RemoveGlasses, Objectives, Skip, Sprint, Map
    }

    /// <summary>A physical control on a Quest-style controller that an intent is bound to (for 3D callouts).</summary>
    public struct ControlRef
    {
        public HandSide Hand;
        public string Control; // primaryButton, secondaryButton, grip, trigger, thumbstick, menu
        public string Label;
    }

    /// <summary>
    /// Builds the InputActionAsset in code (one source of truth). Desktop: keyboard + mouse. VR: OpenXR controller
    /// usages ({PrimaryButton}, {GripButton}, ...) which the Oculus Touch / Quest Touch Plus profiles provide.
    /// </summary>
    public class GameInput
    {
        public readonly InputActionAsset Asset;
        public readonly InputActionMap DesktopMap, XRMap;

        // Desktop
        public InputAction Move, Look, Interact, PassportToggle, DestScroll, DestPrev, DestNext, Tear, Seal, Recall, Pause, Objectives, RemoveGlasses, Skip, Sprint, Point, Click, Map;
        // XR raw controls
        public InputAction LGrip, RGrip, LTrigger, RTrigger, LStick, RStick, X, Y, A, B, Menu;
        public InputAction HeadPos, HeadRot, LPos, LRot, RPos, RRot, LAimPos, LAimRot, RAimPos, RAimRot, LTracked, RTracked;

        public GameInput()
        {
            Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            Asset.name = "TwelfthStampInput";

            DesktopMap = Asset.AddActionMap("Desktop");
            Move = DesktopMap.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Look = DesktopMap.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            Interact = DesktopMap.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            PassportToggle = DesktopMap.AddAction("Passport", InputActionType.Button, "<Keyboard>/f");
            DestScroll = DesktopMap.AddAction("DestinationScroll", InputActionType.Value, "<Mouse>/scroll/y");
            DestPrev = DesktopMap.AddAction("DestinationPrev", InputActionType.Button, "<Keyboard>/leftBracket");
            DestNext = DesktopMap.AddAction("DestinationNext", InputActionType.Button, "<Keyboard>/rightBracket");
            Tear = DesktopMap.AddAction("Tear", InputActionType.Button, "<Mouse>/leftButton");
            Seal = DesktopMap.AddAction("Seal", InputActionType.Button, "<Mouse>/rightButton");
            Recall = DesktopMap.AddAction("Recall", InputActionType.Button, "<Keyboard>/r");
            Pause = DesktopMap.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            Objectives = DesktopMap.AddAction("Objectives", InputActionType.Button, "<Keyboard>/tab");
            RemoveGlasses = DesktopMap.AddAction("RemoveGlasses", InputActionType.Button, "<Keyboard>/g");
            Skip = DesktopMap.AddAction("Skip", InputActionType.Button, "<Keyboard>/space");
            Skip.AddBinding("<Keyboard>/enter");
            Sprint = DesktopMap.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            Map = DesktopMap.AddAction("Map", InputActionType.Button, "<Keyboard>/m");   // the full map (Guide/MapView.cs)
            Point = DesktopMap.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position");
            Click = DesktopMap.AddAction("Click", InputActionType.Button, "<Mouse>/leftButton");

            XRMap = Asset.AddActionMap("XR");
            LGrip = XRMap.AddAction("LeftGrip", InputActionType.Button, "<XRController>{LeftHand}/{GripButton}");
            RGrip = XRMap.AddAction("RightGrip", InputActionType.Button, "<XRController>{RightHand}/{GripButton}");
            LTrigger = XRMap.AddAction("LeftTrigger", InputActionType.Button, "<XRController>{LeftHand}/{TriggerButton}");
            RTrigger = XRMap.AddAction("RightTrigger", InputActionType.Button, "<XRController>{RightHand}/{TriggerButton}");
            LStick = XRMap.AddAction("LeftStick", InputActionType.Value, "<XRController>{LeftHand}/{Primary2DAxis}");
            RStick = XRMap.AddAction("RightStick", InputActionType.Value, "<XRController>{RightHand}/{Primary2DAxis}");
            X = XRMap.AddAction("X", InputActionType.Button, "<XRController>{LeftHand}/{PrimaryButton}");
            Y = XRMap.AddAction("Y", InputActionType.Button, "<XRController>{LeftHand}/{SecondaryButton}");
            A = XRMap.AddAction("A", InputActionType.Button, "<XRController>{RightHand}/{PrimaryButton}");
            B = XRMap.AddAction("B", InputActionType.Button, "<XRController>{RightHand}/{SecondaryButton}");
#if UNITY_WEBGL
            // Browser VR: the headset's browser keeps the menu buttons for itself, so pause is a left thumbstick press.
            Menu = XRMap.AddAction("Menu", InputActionType.Button, "<XRController>{LeftHand}/{Primary2DAxisClick}");
#else
            Menu = XRMap.AddAction("Menu", InputActionType.Button, "<XRController>{LeftHand}/{MenuButton}");
#endif

            HeadPos = XRMap.AddAction("HeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition");
            HeadRot = XRMap.AddAction("HeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation");
            LPos = XRMap.AddAction("LeftPosition", InputActionType.Value, "<XRController>{LeftHand}/devicePosition");
            LRot = XRMap.AddAction("LeftRotation", InputActionType.Value, "<XRController>{LeftHand}/deviceRotation");
            RPos = XRMap.AddAction("RightPosition", InputActionType.Value, "<XRController>{RightHand}/devicePosition");
            RRot = XRMap.AddAction("RightRotation", InputActionType.Value, "<XRController>{RightHand}/deviceRotation");
            LAimPos = XRMap.AddAction("LeftAimPosition", InputActionType.Value, "<XRController>{LeftHand}/pointerPosition");
            LAimRot = XRMap.AddAction("LeftAimRotation", InputActionType.Value, "<XRController>{LeftHand}/pointerRotation");
            RAimPos = XRMap.AddAction("RightAimPosition", InputActionType.Value, "<XRController>{RightHand}/pointerPosition");
            RAimRot = XRMap.AddAction("RightAimRotation", InputActionType.Value, "<XRController>{RightHand}/pointerRotation");
            LTracked = XRMap.AddAction("LeftTracked", InputActionType.Button, "<XRController>{LeftHand}/isTracked");
            RTracked = XRMap.AddAction("RightTracked", InputActionType.Button, "<XRController>{RightHand}/isTracked");
        }

        public void Enable(GameMode mode)
        {
            DesktopMap.Enable(); // keyboard shortcuts (Esc etc.) remain usable for a spectator in VR mode
            if (mode == GameMode.VR) XRMap.Enable(); else XRMap.Disable();
        }

        // ------------------------------------------------------------------ hand roles (left-handed swaps)
        public bool LeftHanded => Game.Settings != null && Game.Settings.leftHanded;
        public HandSide PassportHand => LeftHanded ? HandSide.Right : HandSide.Left;
        public HandSide DominantHand => LeftHanded ? HandSide.Left : HandSide.Right;
        public InputAction PassportButton => LeftHanded ? A : X;      // summon / dock
        public InputAction RecallButton => LeftHanded ? B : Y;        // recall essential item
        public InputAction SealButton => LeftHanded ? X : A;          // hold to seal
        public InputAction DestinationButton => LeftHanded ? Y : B;   // next destination
        public InputAction MoveStick => LeftHanded ? RStick : LStick; // teleport / smooth move
        public InputAction TurnStick => LeftHanded ? LStick : RStick; // snap turn, up/down = destination

        public InputAction GripOf(HandSide h) => h == HandSide.Left ? LGrip : RGrip;
        public InputAction TriggerOf(HandSide h) => h == HandSide.Left ? LTrigger : RTrigger;

        // ------------------------------------------------------------------ labels from the actual bindings
        public static string DesktopLabel(InputAction action)
        {
            if (action == null) return "?";
            var labels = new List<string>();
            foreach (var b in action.bindings)
            {
                if (b.isComposite) continue;
                if (b.isPartOfComposite) continue;
                labels.Add(PathLabel(b.effectivePath));
            }
            if (labels.Count == 0 && action.bindings.Any(b => b.isPartOfComposite))
            {
                var parts = action.bindings.Where(b => b.isPartOfComposite).Take(4).Select(b => PathLabel(b.effectivePath));
                return string.Join("", parts);
            }
            return string.Join(" / ", labels.Distinct());
        }

        static string PathLabel(string path)
        {
            switch (path)
            {
                case "<Mouse>/leftButton": return "Left Mouse";
                case "<Mouse>/rightButton": return "Right Mouse";
                case "<Mouse>/scroll/y": return "Mouse Wheel";
                case "<Mouse>/delta": return "Mouse";
                case "<Keyboard>/escape": return "Esc";
                case "<Keyboard>/space": return "Space";
                case "<Keyboard>/enter": return "Enter";
                case "<Keyboard>/tab": return "Tab";
                case "<Keyboard>/leftBracket": return "[";
                case "<Keyboard>/rightBracket": return "]";
            }
            return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice).ToUpperInvariant();
        }

        public static ControlRef XRControl(InputAction action)
        {
            var path = action.bindings.Count > 0 ? action.bindings[0].effectivePath : "";
            var hand = path.Contains("{LeftHand}") ? HandSide.Left : HandSide.Right;
            string control = "button", label = "?";
            if (path.Contains("{PrimaryButton}")) { control = "primaryButton"; label = hand == HandSide.Left ? "X" : "A"; }
            else if (path.Contains("{SecondaryButton}")) { control = "secondaryButton"; label = hand == HandSide.Left ? "Y" : "B"; }
            else if (path.Contains("{GripButton}")) { control = "grip"; label = "Grip"; }
            else if (path.Contains("{TriggerButton}")) { control = "trigger"; label = "Trigger"; }
            else if (path.Contains("{Primary2DAxis}")) { control = "thumbstick"; label = "Thumbstick"; }
            else if (path.Contains("{Primary2DAxisClick}")) { control = "thumbstick"; label = "Thumbstick press"; }
            else if (path.Contains("{MenuButton}")) { control = "menu"; label = "Menu"; }
            return new ControlRef { Hand = hand, Control = control, Label = label };
        }

        static string HandName(HandSide h) => h == HandSide.Left ? "left" : "right";

        /// <summary>Human-readable binding for an intent in the current mode (used by prompts, Controls page, onboarding).</summary>
        public string Label(Intent intent)
        {
            if (Game.Mode == GameMode.Desktop)
            {
                switch (intent)
                {
                    case Intent.Move: return DesktopLabel(Move);
                    case Intent.Look: return "Mouse";
                    case Intent.Interact: return DesktopLabel(Interact);
                    case Intent.Grab: return DesktopLabel(Interact);
                    case Intent.Passport: return DesktopLabel(PassportToggle);
                    case Intent.Destination: return DesktopLabel(DestScroll) + " or " + DesktopLabel(DestPrev) + " " + DesktopLabel(DestNext);
                    case Intent.Tear: return "Hold " + DesktopLabel(Tear);
                    case Intent.Seal: return "Hold " + DesktopLabel(Seal);
                    case Intent.Recall: return DesktopLabel(Recall);
                    case Intent.Pause: return DesktopLabel(Pause);
                    case Intent.Objectives: return DesktopLabel(Objectives);
                    case Intent.RemoveGlasses: return "Hold " + DesktopLabel(RemoveGlasses);
                    case Intent.Skip: return DesktopLabel(Skip);
                    case Intent.Sprint: return "Hold " + DesktopLabel(Sprint);
                    case Intent.Map: return DesktopLabel(Map);
                    case Intent.Teleport: return DesktopLabel(Move);
                    case Intent.Turn: return "Mouse";
                }
                return "?";
            }
            switch (intent)
            {
                case Intent.Move: return Game.Settings.smoothLocomotion ? HandName(XRControl(MoveStick).Hand) + " thumbstick" : "Push " + HandName(XRControl(MoveStick).Hand) + " thumbstick, release to teleport";
                case Intent.Teleport: return "Push " + HandName(XRControl(MoveStick).Hand) + " thumbstick, release";
                case Intent.Turn: return HandName(XRControl(TurnStick).Hand) + " thumbstick ← →";
                case Intent.Look: return "Look around";
                case Intent.Grab: return "Grip (either hand)";
                case Intent.Interact: return "Trigger (either hand)";
                case Intent.Passport: return XRControl(PassportButton).Label;
                case Intent.Recall: return XRControl(RecallButton).Label;
                case Intent.Seal: return "Hold " + XRControl(SealButton).Label;
                case Intent.Destination: return XRControl(DestinationButton).Label + " or " + HandName(XRControl(TurnStick).Hand) + " thumbstick ↑ ↓";
                case Intent.Tear: return "Grip the seam and pull, or hold " + HandName(DominantHand) + " Trigger";
                case Intent.Pause: return XRControl(Menu).Label + " (left controller)";
                case Intent.Objectives: return "Look at your passport";
                case Intent.RemoveGlasses: return "Pause menu → Remove the glasses";
                case Intent.Skip: return "Trigger";
                case Intent.Map: return "No map in VR (the full map is on M in desktop mode)";   // every controller button is taken; the minimap is gone
                case Intent.Sprint: return Game.Settings.smoothLocomotion ? "Push the " + HandName(XRControl(MoveStick).Hand) + " thumbstick fully forward" : "Teleport in quick hops";
            }
            return "?";
        }

        /// <summary>Which physical controls an intent uses in VR (for controller callouts). Empty in desktop mode.</summary>
        public List<ControlRef> XRControls(Intent intent)
        {
            var list = new List<ControlRef>();
            void Add(InputAction a, string labelOverride = null)
            {
                var c = XRControl(a);
                if (labelOverride != null) c.Label = labelOverride;
                list.Add(c);
            }
            switch (intent)
            {
                case Intent.Grab: Add(LGrip, "Grab"); Add(RGrip, "Grab"); break;
                case Intent.Interact: Add(LTrigger, "Use"); Add(RTrigger, "Use"); break;
                case Intent.Passport: Add(PassportButton, "Passport"); break;
                case Intent.Recall: Add(RecallButton, "Recall"); break;
                case Intent.Seal: Add(SealButton, "Seal (hold)"); break;
                case Intent.Destination: Add(DestinationButton, "Destination"); break;
                case Intent.Tear: Add(GripOf(DominantHand), "Pull seam"); Add(TriggerOf(DominantHand), "Widen (hold)"); break;
                case Intent.Teleport: Add(MoveStick, Game.Settings.smoothLocomotion ? "Move" : "Teleport"); break;
                case Intent.Move: Add(MoveStick, Game.Settings.smoothLocomotion ? "Move" : "Teleport"); break;
                case Intent.Turn: Add(TurnStick, "Turn"); break;
                case Intent.Pause: Add(Menu, "Pause"); break;
            }
            return list;
        }
    }
}
