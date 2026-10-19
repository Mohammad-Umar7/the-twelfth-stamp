using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Added to each Blender model mesh at import (TwelfthStampModelRules in ProjectSetup.cs): how many metres one UV unit
    /// spans on that mesh, so Models.Spawn can keep tiling textures at the game's scale.
    /// </summary>
    public class UvDensity : MonoBehaviour
    {
        public float MetresPerUv = 1f;
    }
}
