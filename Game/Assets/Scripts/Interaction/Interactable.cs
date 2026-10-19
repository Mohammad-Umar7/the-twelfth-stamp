using System;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Anything the player can use. Shared by desktop (centre-screen ray, E) and VR (hand ray / near hand, trigger).
    /// Put a collider on this object or a child; the Interactor searches parents for this component.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        public string Prompt = "Use";
        public bool Usable = true;
        public float MaxDistance = 2.6f;
        public bool HideWhenUnusable;
        public Action<Interactor> OnUse;
        public Action<Interactor, bool> OnFocus;

        /// <summary>Optional point for the focus marker; defaults to renderer bounds centre.</summary>
        public Transform MarkerPoint;

        public virtual bool CanUse(Interactor who) => Usable && isActiveAndEnabled;

        public virtual string GetPrompt(Interactor who) => Prompt;

        public virtual void Use(Interactor who)
        {
            OnUse?.Invoke(who);
        }

        public virtual void SetFocused(Interactor who, bool focused)
        {
            OnFocus?.Invoke(who, focused);
        }

        public Vector3 MarkerPosition
        {
            get
            {
                if (MarkerPoint != null) return MarkerPoint.position;
                var r = GetComponentInChildren<Renderer>();
                return r != null ? r.bounds.center : transform.position;
            }
        }

        protected virtual void Awake()
        {
            SetLayerRecursive(gameObject, Layers.Interactable);
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }
    }

    public static class Layers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int Pocket = 6;
        public const int PlayerBody = 7;
        public const int Interactable = 8;
        public const int Passenger = 9;
        public const int Floor = 10;
        public const int UI3D = 11;
        public const int Seam = 12;
        /// <summary>Seen only by mirror cameras (the player's own full body in a reflection), never by the player camera.</summary>
        public const int MirrorOnly = 13;
        /// <summary>Live mirror glass (PlanarMirror): drawn by the player camera, never into a reflection or a probe.</summary>
        public const int MirrorGlass = 14;

        public static int WorldMask => (1 << Default) | (1 << Interactable) | (1 << Floor) | (1 << Passenger);
        public static int FloorMask => (1 << Floor);
        public static int InteractMask => (1 << Default) | (1 << Interactable) | (1 << Floor);
        public static int UIMask => 1 << UI3D;
    }
}
