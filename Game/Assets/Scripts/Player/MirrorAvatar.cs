using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Saeed's whole body, for reflections only: everything is on Layers.MirrorOnly, which the player cameras skip and the
    /// planar mirrors draw. It stands at the player's feet and turns after the head on a spring (never more than 40 degrees
    /// behind), looks where the camera looks, walks and runs with the gait, and holds what the first-person hands hold
    /// (desktop) or reaches for the tracked hands (VR). Uses the rigged Resources/Models/Saeed.fbx when present (bones found
    /// by name), otherwise a procedural stand-in: white kandura, the same hands, dark curly hair and a short beard, the
    /// satchel. In the corrupted apartment the reflection runs a third of a second late, and once, after the player turns
    /// away and back, its head is still turned to the glass, staring, before it snaps back.
    /// </summary>
    [DefaultExecutionOrder(120)] // after the rig, the first-person arms and the VR hands have posed this frame
    public class MirrorAvatar : MonoBehaviour
    {
        public static MirrorAvatar I { get; private set; }

        /// <summary>
        /// Optional override: from an eye position, the world direction into the mirror the player faces, or null. Without
        /// it the staring reflection looks towards the nearest live PlanarMirror's glass (as last seen before the player
        /// turned away), or back the way the player was facing.
        /// </summary>
        public static System.Func<Vector3, Vector3?> FacingMirror;

        /// <summary>The body's head bone (model or stand-in), e.g. for a mirror that wants to frame the face.</summary>
        public Transform HeadBone => m_Bone[(int)B.Head];
        /// <summary>True when the Blender model drives the body, false for the procedural stand-in.</summary>
        public bool UsingModel { get; private set; }

        enum B { Hips, Spine, Chest, Neck, Head, ShoulderL, ShoulderR, UpperArmL, UpperArmR, ForearmL, ForearmR, HandL, HandR, UpperLegL, UpperLegR, LowerLegL, LowerLegR, FootL, FootR, Count }

        // Name variants (lower case, separators removed); sided bones also try l / r / left / right before or after the name.
        static readonly string[] ShoulderNames = { "shoulder", "clavicle", "collar" };
        static readonly string[] UpperArmNames = { "upperarm", "arm", "uparm" };
        static readonly string[] ForearmNames = { "forearm", "lowerarm" };
        static readonly string[] HandNames = { "hand", "wrist" };
        static readonly string[] UpperLegNames = { "upperleg", "thigh", "upleg" };
        static readonly string[] LowerLegNames = { "lowerleg", "calf", "shin", "leg" };
        static readonly string[] FootNames = { "foot", "ankle" };
        static readonly (B bone, int side, string[] names)[] Lookup =
        {
            (B.Hips, 0, new[] { "hips", "pelvis", "hip" }), (B.Spine, 0, new[] { "spine", "spine1", "spine01", "abdomen" }),
            (B.Chest, 0, new[] { "chest", "spine2", "spine02", "upperchest", "spine3", "spine03" }), (B.Neck, 0, new[] { "neck", "neck01", "neck1" }), (B.Head, 0, new[] { "head" }),
            (B.ShoulderL, -1, ShoulderNames), (B.ShoulderR, 1, ShoulderNames), (B.UpperArmL, -1, UpperArmNames), (B.UpperArmR, 1, UpperArmNames),
            (B.ForearmL, -1, ForearmNames), (B.ForearmR, 1, ForearmNames), (B.HandL, -1, HandNames), (B.HandR, 1, HandNames),
            (B.UpperLegL, -1, UpperLegNames), (B.UpperLegR, 1, UpperLegNames), (B.LowerLegL, -1, LowerLegNames), (B.LowerLegR, 1, LowerLegNames),
            (B.FootL, -1, FootNames), (B.FootR, 1, FootNames),
        };

        // Stand-in skeleton at rest (metres, body space: +Z forward, +X right; eyes at 1.62 m), arms hanging.
        static readonly Vector3[] StandInRest =
        {
            new Vector3(0f, 0.97f, 0f), new Vector3(0f, 1.08f, 0f), new Vector3(0f, 1.25f, 0f), new Vector3(0f, 1.46f, 0.005f), new Vector3(0f, 1.555f, 0.01f),
            new Vector3(-0.04f, 1.42f, 0f), new Vector3(0.04f, 1.42f, 0f), new Vector3(-0.185f, 1.415f, 0f), new Vector3(0.185f, 1.415f, 0f),
            new Vector3(-0.2f, 1.125f, 0f), new Vector3(0.2f, 1.125f, 0f), new Vector3(-0.205f, 0.86f, 0.01f), new Vector3(0.205f, 0.86f, 0.01f),
            new Vector3(-0.095f, 0.93f, 0f), new Vector3(0.095f, 0.93f, 0f), new Vector3(-0.1f, 0.5f, 0.01f), new Vector3(0.1f, 0.5f, 0.01f),
            new Vector3(-0.105f, 0.085f, 0f), new Vector3(0.105f, 0.085f, 0f),
        };
        static readonly int[] StandInParent = { -1, 0, 1, 2, 3, 2, 2, 5, 6, 7, 8, 9, 10, 0, 0, 13, 14, 15, 16 };

        const float BehindMax = 40f;        // body never lags the head by more than this (degrees)
        const float CorruptLag = 0.35f;     // the corrupted reflection runs this far behind (seconds)

        struct HandIn { public bool Valid; public Vector3 Wrist; public Quaternion Rot; public HandPose Fingers; public float Hold; }
        struct Sample { public float Time; public Vector3 Feet, HeadPos, Vel; public Quaternion HeadRot; public float Speed, Phase, Exert; public bool Sprint; public HandIn L, R; }

        PlayerRig m_Rig;
        readonly Transform[] m_Bone = new Transform[(int)B.Count];
        readonly Quaternion[] m_RestRot = new Quaternion[(int)B.Count];
        readonly Vector3[] m_RestPos = new Vector3[(int)B.Count];
        Vector3 m_RestEye;
        float m_UpperArm, m_Forearm, m_Thigh, m_Shin, m_Torso;
        Vector3 m_ArmHingeL, m_ArmHingeR, m_LegHinge;
        HandRig m_HandL, m_HandR;          // stand-in only: the same hands as the first-person view
        Transform m_Robe;                   // stand-in only: the lower robe sways with the gait

        float m_Scale = 1f, m_StandHeight = 1.62f;
        float m_BodyYaw, m_BodyYawVel;
        bool m_Primed;
        Vector3 m_PrevFeet, m_VrVel;
        readonly Sample[] m_Buffer = new Sample[256];
        int m_BufferHead, m_BufferCount;

        // The corrupted reflection's one stare: Waiting -> Away (turned from the glass) -> Holding (back, still staring) -> Done.
        enum Stare { Waiting, Away, Holding, Done }
        Stare m_Stare = Stare.Done;
        float m_StareTime, m_RefYaw, m_StareWeight, m_MirrorSeen = -10f;
        Vector3 m_StareDir = Vector3.forward, m_MirrorDir = Vector3.forward;
        Cell m_StareCell;

        public static MirrorAvatar Create(PlayerRig rig)
        {
            var go = new GameObject("MirrorAvatar");
            go.transform.SetParent(rig.transform.parent, false);
            go.layer = Layers.MirrorOnly;
            var avatar = go.AddComponent<MirrorAvatar>();
            avatar.m_Rig = rig;
            avatar.Build();
            I = avatar;
            return avatar;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        // ------------------------------------------------------------------ build
        void Build()
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            var model = Models.Spawn("Saeed", transform, SlotFor, Vector3.zero, 0f);
            if (model != null && FindBones(model))
            {
                UsingModel = true;
                foreach (var a in model.GetComponentsInChildren<Animator>(true)) a.enabled = false; // the bones are driven here
            }
            else
            {
                if (model != null)
                {
                    Debug.LogWarning("[TS-Mirror] Saeed model found but its bones are not all named as expected; using the stand-in");
                    DestroyImmediate(model);
                }
                BuildStandIn();
            }
            CaptureRest();
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.MirrorOnly;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }
            // Desktop eyes sit at a fixed height: scale the body so its eyes meet the camera.
            if (m_Rig is DesktopRig) m_Scale = DesktopRig.EyeHeight / Mathf.Max(0.5f, m_RestEye.y);
        }

        static Material SlotFor(string importedName)
        {
            var slot = Models.SlotOf(importedName);
            var textured = SaeedMaterial(slot);
            if (textured != null) return textured;
            switch (slot)
            {
                case "TS_Skin": case "TS_Face": case "TS_Body": return HandMesh.BodySkinMaterial;
                case "TS_Kandura": case "TS_Cotton": case "TS_Robe": case "TS_Cloth": case "TS_Fabric": case "TS_FabricWhite": return HandMesh.KanduraMaterial;
                case "TS_Hair": case "TS_Beard": return HairMaterial;
                case "TS_Eye": case "TS_Eyes": return EyeMaterial;
                case "TS_Leather": case "TS_Strap": case "TS_Satchel": case "TS_Bag": return LeatherMaterial;
                case "TS_Suede": case "TS_Shoe": case "TS_Shoes": return SuedeMaterial;
            }
            return null;
        }

        static readonly Dictionary<string, Material> s_SaeedMats = new Dictionary<string, Material>();
        static Material s_EyeMat;

        /// <summary>
        /// Saeed's own painted maps (Resources/Art/Saeed: albedo + "_n" normal per slot, from the Blender build), so the
        /// face, beard and fabric keep their detail. Null when the art is missing (the flat materials below are used).
        /// </summary>
        static Material SaeedMaterial(string slot)
        {
            if (slot == null) return null;
            if (s_SaeedMats.TryGetValue(slot, out var cached)) return cached;
            string file;
            float smooth;
            switch (slot)
            {
                case "TS_Skin": file = "saeed_skin"; smooth = 0.36f; break;
                case "TS_Kandura": file = "saeed_kandura"; smooth = 0.2f; break;
                case "TS_Hair": file = "saeed_hair"; smooth = 0.32f; break;
                case "TS_Eyes": file = "saeed_eyes"; smooth = 0.86f; break;
                case "TS_Leather": file = "saeed_leather"; smooth = 0.42f; break;
                case "TS_Suede": file = "saeed_suede"; smooth = 0.08f; break;
                default: return null;
            }
            var albedo = Tex.Art("Saeed/" + file);
            if (albedo == null) return null;
            var normal = Tex.Art("Saeed/" + file + "_n");
            var m = new Material(Mats.Template(normal != null ? "Lit_Normal" : "Lit_Opaque")) { name = "Saeed_" + file };
            // Untinted: the skin bake already uses the first-person hands' colour (HandMesh.SkinColor), so the mirror
            // and the hands agree.
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", albedo);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); }
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            s_SaeedMats[slot] = m;
            if (slot == "TS_Eyes") s_EyeMat = m;
            return m;
        }

        /// <summary>The model's eyes (its TS_Eyes part), in body space at rest: where the camera should sit.</summary>
        bool TryModelEyes(out Vector3 eye)
        {
            eye = default;
            if (s_EyeMat == null) return false;
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                var mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length && i < mesh.subMeshCount; i++)
                {
                    if (mats[i] != s_EyeMat) continue;
                    var b = mesh.GetSubMesh(i).bounds;
                    if (b.size.sqrMagnitude < 1e-8f) return false;
                    eye = transform.InverseTransformPoint(smr.transform.TransformPoint(b.center)) + new Vector3(0f, 0f, 0.012f);
                    return eye.y > 0.8f && eye.y < 2.4f;
                }
            }
            return false;
        }

        static Material HairMaterial => Mats.Lit(new Color(0.07f, 0.055f, 0.05f), 0.3f);
        static Material EyeMaterial => Mats.Lit(new Color(0.03f, 0.025f, 0.025f), 0.85f);
        static Material LeatherMaterial => Mats.Lit(new Color(0.42f, 0.27f, 0.16f), 0.38f);
        static Material SuedeMaterial => Mats.Lit(new Color(0.46f, 0.33f, 0.22f), 0.08f);

        bool FindBones(GameObject model)
        {
            var all = model.GetComponentsInChildren<Transform>(true);
            var norm = new string[all.Length];
            for (int i = 0; i < all.Length; i++) norm[i] = Normalise(all[i].name);
            foreach (var (bone, side, names) in Lookup)
            {
                Transform found = null;
                foreach (var n in names)
                {
                    for (int i = 0; i < all.Length && found == null; i++)
                        if (Matches(norm[i], n, side)) found = all[i];
                    if (found != null) break;
                }
                m_Bone[(int)bone] = found;
            }
            B[] essential = { B.Hips, B.Head, B.UpperArmL, B.UpperArmR, B.ForearmL, B.ForearmR, B.HandL, B.HandR, B.UpperLegL, B.UpperLegR, B.LowerLegL, B.LowerLegR, B.FootL, B.FootR };
            foreach (var b in essential) if (m_Bone[(int)b] == null) return false;
            return true;
        }

        static string Normalise(string name)
        {
            var s = name.ToLowerInvariant();
            int colon = s.LastIndexOf(':');
            if (colon >= 0) s = s.Substring(colon + 1); // "mixamorig:LeftArm"
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
            return sb.ToString();
        }

        static bool Matches(string n, string bone, int side)
        {
            if (side == 0) return n == bone;
            string a = side < 0 ? "l" : "r", word = side < 0 ? "left" : "right";
            return n == bone + a || n == a + bone || n == bone + word || n == word + bone;
        }

        void CaptureRest()
        {
            for (int i = 0; i < (int)B.Count; i++)
            {
                var t = m_Bone[i];
                if (t == null) continue;
                m_RestRot[i] = Quaternion.Inverse(transform.rotation) * t.rotation;
                m_RestPos[i] = transform.InverseTransformPoint(t.position);
            }
            m_UpperArm = Vector3.Distance(Rest(B.UpperArmL), Rest(B.ForearmL));
            m_Forearm = Vector3.Distance(Rest(B.ForearmL), Rest(B.HandL));
            m_Thigh = Vector3.Distance(Rest(B.UpperLegL), Rest(B.LowerLegL));
            m_Shin = Vector3.Distance(Rest(B.LowerLegL), Rest(B.FootL));
            m_Torso = Mathf.Max(0.2f, Rest(B.Head).y - Rest(B.Hips).y);
            // Eyes ahead of and above the head bone, in proportion to the body.
            float k = Rest(B.Head).y / 1.555f;
            m_RestEye = Rest(B.Head) + new Vector3(0f, 0.065f, 0.08f) * k;
            if (UsingModel && TryModelEyes(out var eyes)) m_RestEye = eyes; // the Blender model knows where its eyes are
            m_ArmHingeL = RestHinge(B.UpperArmL, B.ForearmL, B.HandL, Vector3.forward);
            m_ArmHingeR = RestHinge(B.UpperArmR, B.ForearmR, B.HandR, Vector3.forward);
            m_LegHinge = RestHinge(B.UpperLegL, B.LowerLegL, B.FootL, Vector3.back);
        }

        Vector3 Rest(B b) => m_RestPos[(int)b];

        /// <summary>The limb's hinge at rest: from its bend if it has one, else the way it bends (elbows forward, knees back).</summary>
        Vector3 RestHinge(B upper, B lower, B end, Vector3 bendTowards)
        {
            var u = (Rest(lower) - Rest(upper)).normalized;
            var l = (Rest(end) - Rest(lower)).normalized;
            var h = Vector3.Cross(u, l);
            if (h.magnitude > 0.05f) return h.normalized;
            return Unit(Vector3.Cross(u, bendTowards), Vector3.right);
        }

        // ------------------------------------------------------------------ stand-in body
        void BuildStandIn()
        {
            var body = new GameObject("StandIn").transform;
            body.SetParent(transform, false);
            for (int i = 0; i < (int)B.Count; i++)
            {
                var t = new GameObject(((B)i).ToString()).transform;
                var parent = StandInParent[i] < 0 ? body : m_Bone[StandInParent[i]];
                t.SetParent(parent, false);
                t.position = transform.TransformPoint(StandInRest[i]);
                m_Bone[i] = t;
            }
            var kandura = HandMesh.KanduraMaterial;
            var skin = HandMesh.BodySkinMaterial;

            // Robe: from the waist to the ankles, a little fuller at the hem, drape falling slightly forward.
            m_Robe = Part("Robe", B.Hips, kandura, mb => mb.Loft(Frame(B.Hips, new Vector3(0f, 1.06f, 0f), Vector3.down, Vector3.forward),
                Rings((0f, 0f, 0.162f, 0.122f), (0.1f, 0f, 0.178f, 0.133f), (0.25f, 0.002f, 0.19f, 0.143f), (0.45f, 0.004f, 0.196f, 0.15f), (0.7f, 0.006f, 0.206f, 0.158f),
                      (0.9f, 0.008f, 0.214f, 0.165f), (0.985f, 0.008f, 0.218f, 0.168f), (0.99f, 0.008f, 0.212f, 0.162f), (0.975f, 0.008f, 0.206f, 0.156f)),
                28, 0, _ => default, 0f, 0f, 2.3f, HandMesh.UvMap.World(0.12f))).transform;
            // Torso up to the round, collarless neck.
            Part("Torso", B.Chest, kandura, mb => mb.Loft(Frame(B.Chest, new Vector3(0f, 0.98f, 0f), Vector3.up, Vector3.back),
                Rings((0f, 0f, 0.163f, 0.123f), (0.12f, 0f, 0.166f, 0.118f), (0.27f, -0.004f, 0.18f, 0.12f), (0.37f, -0.002f, 0.198f, 0.117f), (0.43f, 0f, 0.205f, 0.108f),
                      (0.465f, 0.004f, 0.172f, 0.092f), (0.485f, 0.006f, 0.105f, 0.072f), (0.492f, 0.006f, 0.064f, 0.058f)),
                24, 0, _ => default, 0f, 0.004f, 2.4f, HandMesh.UvMap.World(0.12f)));
            // The collar tassel (furakha) hanging from the neck opening.
            Part("Furakha", B.Chest, kandura, mb =>
            {
                mb.Loft(Frame(B.Chest, new Vector3(0f, 1.462f, 0.07f), new Vector3(0f, -0.97f, 0.24f), Vector3.forward), Rings((0f, 0f, 0.0028f, 0.0028f), (0.24f, 0f, 0.0028f, 0.0028f)),
                    6, 0, _ => default, 0.003f, 0f, 2f, HandMesh.UvMap.World(0.12f));
                mb.Loft(Frame(B.Chest, new Vector3(0f, 1.229f, 0.128f), new Vector3(0f, -0.97f, 0.24f), Vector3.forward), Rings((0f, 0f, 0.0045f, 0.0045f), (0.05f, 0f, 0.0085f, 0.0085f)),
                    10, 0, _ => default, 0.004f, 0.003f, 2f, HandMesh.UvMap.World(0.12f));
            });
            Part("Neck", B.Neck, skin, mb => mb.Loft(Frame(B.Neck, new Vector3(0f, 1.42f, 0.005f), Vector3.up, Vector3.back), Rings((0f, 0f, 0.052f, 0.05f), (0.15f, 0f, 0.05f, 0.048f)),
                16, 0, _ => default, 0f, 0f, 2f, HandMesh.UvMap.World(0.12f)));

            // Head: face, nose, ears, eyes, brows; dark curly hair and a short beard.
            Part("Head", B.Head, skin, mb =>
            {
                mb.Ellipsoid(Frame(B.Head, new Vector3(0f, 1.625f, 0.014f), Vector3.forward, Vector3.up), new Vector3(0.077f, 0.104f, 0.097f), 24, 16, 0, 0.12f);
                mb.Loft(Frame(B.Head, new Vector3(0f, 1.642f, 0.104f), new Vector3(0f, -0.94f, 0.34f), Vector3.forward), Rings((0f, 0f, 0.0095f, 0.009f), (0.036f, 0.002f, 0.0115f, 0.0105f)),
                    10, 0, _ => default, 0.006f, 0.008f, 2f, HandMesh.UvMap.World(0.12f));
                foreach (float s in new[] { -1f, 1f })
                    mb.Ellipsoid(Frame(B.Head, new Vector3(0.077f * s, 1.622f, 0.004f), Vector3.forward, Vector3.up), new Vector3(0.011f, 0.028f, 0.018f), 10, 8, 0, 0.12f);
            });
            Part("Eyes", B.Head, EyeMaterial, mb =>
            {
                foreach (float s in new[] { -1f, 1f })
                    mb.Ellipsoid(Frame(B.Head, new Vector3(0.03f * s, 1.638f, 0.097f), Vector3.forward, Vector3.up), new Vector3(0.0105f, 0.0075f, 0.006f), 10, 8, 0, 0.12f);
            });
            Part("Hair", B.Head, HairMaterial, mb =>
            {
                mb.Ellipsoid(Frame(B.Head, new Vector3(0f, 1.648f, -0.004f), Vector3.forward, Vector3.up), new Vector3(0.085f, 0.098f, 0.104f), 28, 18, 0, 0.12f, Curls);
                mb.Ellipsoid(Frame(B.Head, new Vector3(0f, 1.595f, 0.027f), Vector3.forward, Vector3.up), new Vector3(0.078f, 0.07f, 0.082f), 24, 14, 0, 0.12f, Beard);
                foreach (float s in new[] { -1f, 1f })
                    mb.Ellipsoid(Frame(B.Head, new Vector3(0.031f * s, 1.656f, 0.1f), Vector3.forward, Vector3.up), new Vector3(0.017f, 0.0042f, 0.006f), 10, 6, 0, 0.12f);
            });

            // Sleeves on the upper arms; the forearms and hands are the first-person hands with their kandura sleeves.
            foreach (var (bone, s) in new[] { (B.UpperArmL, -1f), (B.UpperArmR, 1f) })
            {
                var b = bone;
                Part("Sleeve", b, kandura, mb => mb.Loft(Frame(b, StandInRest[(int)b] + new Vector3(0f, -0.01f, 0f), Vector3.down, Vector3.forward),
                    Rings((0f, 0f, 0.056f, 0.054f), (0.15f, 0f, 0.055f, 0.053f), (0.28f, 0f, 0.052f, 0.05f)), 16, 0, _ => default, 0.022f, 0.02f, 2f, HandMesh.UvMap.World(0.12f)));
            }
            m_HandL = HandMesh.Build("MirrorHandL", transform, HandSide.Left);
            m_HandR = HandMesh.Build("MirrorHandR", transform, HandSide.Right);

            // Legs (the white under-robe; only the ankles show below the hem) and brown suede shoes.
            foreach (var (thigh, shin, foot) in new[] { (B.UpperLegL, B.LowerLegL, B.FootL), (B.UpperLegR, B.LowerLegR, B.FootR) })
            {
                var t = thigh; var sh = shin; var f = foot;
                Part("Thigh", t, kandura, mb => mb.Loft(Frame(t, StandInRest[(int)t], Vector3.down, Vector3.forward), Rings((0f, 0f, 0.07f, 0.068f), (0.43f, 0f, 0.055f, 0.053f)),
                    14, 0, _ => default, 0.03f, 0.02f, 2f, HandMesh.UvMap.World(0.12f)));
                Part("Shin", sh, skin, mb => mb.Loft(Frame(sh, StandInRest[(int)sh], Vector3.down, Vector3.forward), Rings((0f, 0f, 0.05f, 0.048f), (0.4f, 0f, 0.036f, 0.035f)),
                    12, 0, _ => default, 0.02f, 0.02f, 2f, HandMesh.UvMap.World(0.12f)));
                Part("Shoe", f, SuedeMaterial, mb => mb.Loft(Frame(f, StandInRest[(int)f] + new Vector3(0f, -0.045f, -0.065f), Vector3.forward, Vector3.up),
                    Rings((0f, 0f, 0.042f, 0.04f), (0.06f, 0f, 0.047f, 0.042f), (0.14f, -0.004f, 0.049f, 0.035f), (0.21f, -0.012f, 0.044f, 0.026f), (0.25f, -0.018f, 0.034f, 0.018f)),
                    16, 0, _ => default, 0.02f, 0.018f, 2.6f, HandMesh.UvMap.World(0.12f)));
            }

            // The satchel: strap over the right shoulder to the left hip, and the bag at the hip.
            Part("SatchelStrap", B.Chest, LeatherMaterial, mb =>
            {
                Vector3[] path =
                {
                    new Vector3(0.13f, 1.432f, 0.04f), new Vector3(0.07f, 1.335f, 0.118f), new Vector3(-0.02f, 1.2f, 0.131f), new Vector3(-0.11f, 1.07f, 0.133f), new Vector3(-0.168f, 0.985f, 0.104f),
                    new Vector3(-0.196f, 0.955f, 0f), new Vector3(-0.152f, 1.0f, -0.114f), new Vector3(-0.05f, 1.15f, -0.128f), new Vector3(0.06f, 1.3f, -0.121f), new Vector3(0.132f, 1.42f, -0.062f),
                    new Vector3(0.152f, 1.458f, -0.004f),
                };
                var local = new Vector3[path.Length];
                var outward = new Vector3[path.Length];
                var origin = StandInRest[(int)B.Chest];
                for (int i = 0; i < path.Length; i++)
                {
                    local[i] = path[i] - origin;
                    var o = new Vector3(path[i].x, 0f, path[i].z);
                    outward[i] = Unit(o, Vector3.forward) + (path[i].y > 1.4f ? Vector3.up * 1.2f : Vector3.zero);
                }
                mb.Band(Matrix4x4.identity, local, outward, 0.04f, 0.005f, true, 0);
            });
            Part("Satchel", B.Hips, LeatherMaterial, mb =>
            {
                var f = Frame(B.Hips, new Vector3(-0.225f, 0.87f, 0.04f), Vector3.up, Vector3.left);
                mb.Loft(f, Rings((0f, 0f, 0.072f, 0.03f), (0.15f, 0f, 0.076f, 0.032f), (0.17f, 0f, 0.074f, 0.031f)), 18, 0, _ => default, 0.012f, 0.01f, 4f, HandMesh.UvMap.World(0.12f));
                mb.Loft(Frame(B.Hips, new Vector3(-0.231f, 0.94f, 0.04f), Vector3.up, Vector3.left), Rings((0f, 0f, 0.079f, 0.029f), (0.1f, 0f, 0.08f, 0.03f)),
                    18, 0, _ => default, 0f, 0.008f, 4f, HandMesh.UvMap.World(0.12f));
            });
        }

        /// <summary>Hair: tight curls over the scalp; below the hairline at the front, and over the ears, it tapers into the skull.</summary>
        static Vector3 Curls(Vector3 q)
        {
            var dir = Unit(q, Vector3.up);
            float curl = Mathf.Sin(q.x * 260f) * Mathf.Sin(q.y * 240f + 1.3f) * Mathf.Sin(q.z * 250f + 0.7f);
            float face = Smooth01(Mathf.InverseLerp(0.03f, 0.06f, q.z)) * (1f - Smooth01(Mathf.InverseLerp(0.02f, 0.05f, q.y)));
            float ears = Smooth01(Mathf.InverseLerp(-0.03f, -0.06f, q.y)) * Smooth01(Mathf.InverseLerp(-0.03f, 0.0f, q.z));
            float r = (1f + 0.035f * curl) * Mathf.Lerp(1f, 0.86f, Mathf.Max(face, ears));
            return q * r + dir * 0.0005f * curl;
        }

        /// <summary>Beard: a short shell over jaw and chin, tapering into the face above and at the back.</summary>
        static Vector3 Beard(Vector3 q)
        {
            float sink = Mathf.Max(Smooth01(Mathf.InverseLerp(-0.012f, 0.008f, q.y)), Smooth01(Mathf.InverseLerp(-0.01f, -0.035f, q.z)));
            float curl = Mathf.Sin(q.x * 420f) * Mathf.Sin(q.y * 380f) * Mathf.Sin(q.z * 400f);
            return q * (Mathf.Lerp(1f, 0.84f, sink) + 0.015f * curl);
        }

        GameObject Part(string name, B bone, Material mat, System.Action<HandMesh.SkinBuilder> build)
        {
            var mb = new HandMesh.SkinBuilder();
            build(mb);
            var go = new GameObject(name);
            go.transform.SetParent(m_Bone[(int)bone], false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name, false);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>A loft frame in a stand-in bone's space: origin at a body-space point, +Z along <paramref name="along"/>.</summary>
        static Matrix4x4 Frame(B bone, Vector3 bodyPoint, Vector3 along, Vector3 up)
            => Matrix4x4.TRS(bodyPoint - StandInRest[(int)bone], Quaternion.LookRotation(along.normalized, up), Vector3.one);

        static HandMesh.Ring[] Rings(params (float z, float cy, float a, float b)[] r)
        {
            var rings = new HandMesh.Ring[r.Length];
            for (int i = 0; i < r.Length; i++) rings[i] = new HandMesh.Ring(r[i].z, 0f, r[i].cy, r[i].a, r[i].b);
            return rings;
        }

        // ------------------------------------------------------------------ per frame
        void LateUpdate()
        {
            if (m_Rig == null) { Destroy(gameObject); return; } // the rig went away (scene reset): so does its reflection
            if (m_Rig.Cam == null) return;
            float dt = Time.deltaTime;
            var cur = Gather(dt);
            bool jumped = m_Primed && Vector3.Distance(cur.Feet, m_PrevFeet) > 1.5f;
            m_PrevFeet = cur.Feet;
            if (!m_Primed || jumped) { m_BufferCount = 0; m_BodyYaw = Yaw(cur.HeadRot * Vector3.forward, 0f); m_BodyYawVel = 0f; m_Primed = true; }
            Push(cur);

            var cell = Game.Cells != null ? Game.Cells.Active : null;
            bool corrupt = cell != null && cell.Id == CellId.ApartmentCorrupt;
            var s = corrupt ? SampleAt(cur.Time - CorruptLag) : cur;
            UpdateStare(cur, cell, corrupt, dt);

            // Scale (VR: grows to the tallest standing height seen, so crouching bends the knees instead of shrinking him).
            if (m_Rig is VRRig)
            {
                float headH = cur.HeadPos.y - cur.Feet.y;
                m_StandHeight = headH > m_StandHeight ? headH : Mathf.MoveTowards(m_StandHeight, Mathf.Max(headH, 1.2f), dt * 0.01f);
                m_Scale = Mathf.Clamp(m_StandHeight / Mathf.Max(0.5f, m_RestEye.y), 0.85f, 1.25f);
            }

            // Body: turns after the head on a spring, never more than BehindMax behind.
            float headYaw = Yaw(s.HeadRot * Vector3.forward, m_BodyYaw);
            if (dt > 0f)
            {
                float err = Mathf.DeltaAngle(m_BodyYaw, headYaw);
                m_BodyYawVel += (err * 40f - m_BodyYawVel * 11f) * dt;
                m_BodyYaw += m_BodyYawVel * dt;
                err = Mathf.DeltaAngle(m_BodyYaw, headYaw);
                if (Mathf.Abs(err) > BehindMax) m_BodyYaw = headYaw - Mathf.Sign(err) * BehindMax;
            }
            var bodyRot = Quaternion.Euler(0f, m_BodyYaw, 0f);
            // Feet under the player, shifted back so the body's eyes (ahead of the head bone) meet the camera.
            transform.SetPositionAndRotation(s.Feet - bodyRot * Vector3.forward * (m_RestEye.z * m_Scale), bodyRot);
            transform.localScale = Vector3.one * m_Scale;

            Pose(s, dt);
        }

        Sample Gather(float dt)
        {
            var head = m_Rig.Head;
            var s = new Sample
            {
                Time = Time.time, Feet = m_Rig.Feet, HeadPos = head.position, HeadRot = head.rotation,
                Speed = m_Rig.MoveSpeed, Sprint = m_Rig.Sprinting, Exert = m_Rig.Exertion,
            };
            if (m_Rig is DesktopRig d)
            {
                s.Vel = d.Velocity;
                s.Phase = d.GaitPhase;
                var arms = d.Arms;
                if (arms != null)
                {
                    s.L.Valid = arms.GetHand(true, out s.L.Wrist, out s.L.Rot, out s.L.Fingers, out s.L.Hold);
                    s.R.Valid = arms.GetHand(false, out s.R.Wrist, out s.R.Rot, out s.R.Fingers, out s.R.Hold);
                }
            }
            else if (m_Rig is VRRig v)
            {
                if (dt > 0f && m_Primed)
                {
                    var step = s.Feet - m_PrevFeet;
                    step.y = 0f;
                    if (step.magnitude < 0.5f) m_VrVel = Vector3.Lerp(m_VrVel, step / dt, 1f - Mathf.Exp(-dt * 8f));
                }
                s.Vel = m_VrVel;
                s.Phase = v.GaitPhase;
                var hands = v.HandVisuals;
                if (hands != null)
                {
                    s.L.Valid = hands.GetHand(HandSide.Left, out s.L.Wrist, out s.L.Rot, out s.L.Fingers);
                    s.R.Valid = hands.GetHand(HandSide.Right, out s.R.Wrist, out s.R.Rot, out s.R.Fingers);
                    s.L.Hold = s.L.Valid ? 1f : 0f;
                    s.R.Hold = s.R.Valid ? 1f : 0f;
                }
            }
            if (!s.L.Valid) s.L.Fingers = HandPose.Relaxed;
            if (!s.R.Valid) s.R.Fingers = HandPose.Relaxed;
            return s;
        }

        // ------------------------------------------------------------------ pose
        void Pose(in Sample s, float dt)
        {
            var inv = Quaternion.Inverse(transform.rotation);
            var headLocal = transform.InverseTransformPoint(s.HeadPos);
            var look = inv * (s.HeadRot * Vector3.forward);
            var lookUp = inv * (s.HeadRot * Vector3.up);
            var velLocal = inv * s.Vel;
            velLocal.y = 0f;
            float speed = new Vector2(s.Vel.x, s.Vel.z).magnitude;
            float walk = Smooth01(Mathf.InverseLerp(0.15f, 1.0f, speed));
            float run = s.Sprint ? Smooth01(Mathf.InverseLerp(1.5f, 3.0f, speed)) : 0f;
            float phase = s.Phase;
            float swingL = Mathf.Cos(phase), swingR = -Mathf.Cos(phase); // the left arm leads at the first footfall (as the first-person arms)

            // Hips: follow the head (crouch fully, lean a little), sway over the stance foot, counter-twist the shoulders.
            var delta = headLocal - m_RestEye;
            var hipsOffset = new Vector3(delta.x * 0.3f + 0.018f * walk * Mathf.Sin(phase), Mathf.Clamp(delta.y, -0.7f, 0.03f), delta.z * 0.3f);
            SetPos(B.Hips, Rest(B.Hips) + hipsOffset);
            SetRot(B.Hips, Quaternion.Euler(0f, -3f * walk * swingL, 0f) * m_RestRot[(int)B.Hips]);

            // Spine and chest: lean to bring the head over the remaining offset, lean into a run, twist with the arms.
            float lean = Mathf.Atan2(delta.z * 0.7f, m_Torso) * Mathf.Rad2Deg + 6f * run;
            float roll = -Mathf.Atan2(delta.x * 0.7f, m_Torso) * Mathf.Rad2Deg;
            var qSpine = Quaternion.Euler(lean * 0.4f, 1.5f * walk * swingL, roll * 0.4f);
            var qChest = qSpine * Quaternion.Euler(lean * 0.6f, 2.5f * walk * swingL, roll * 0.6f);
            if (m_Bone[(int)B.Spine] != null) SetRot(B.Spine, qSpine * m_RestRot[(int)B.Spine]);
            if (m_Bone[(int)B.Chest] != null) SetRot(B.Chest, qChest * m_RestRot[(int)B.Chest]);
            if (m_Bone[(int)B.ShoulderL] != null) SetRot(B.ShoulderL, qChest * m_RestRot[(int)B.ShoulderL]);
            if (m_Bone[(int)B.ShoulderR] != null) SetRot(B.ShoulderR, qChest * m_RestRot[(int)B.ShoulderR]);

            // Head: where the camera looks (clamped to what a neck can do); the neck takes a third of it.
            var chestFwd = qChest * Vector3.forward;
            var target = Clamp(look, chestFwd, 80f, 60f, 55f);
            if (m_StareWeight > 0.001f)
            {
                var stare = Clamp(inv * m_StareDir, chestFwd, 80f, 20f, 20f);
                target = Vector3.Slerp(target, stare, m_StareWeight);
            }
            if (m_Bone[(int)B.Neck] != null)
                SetRot(B.Neck, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.forward, target), 0.35f) * qChest * m_RestRot[(int)B.Neck]);
            SetRot(B.Head, Look(target, lookUp) * m_RestRot[(int)B.Head]);

            // Arms, then legs.
            Arm(true, s.L, swingL, walk, run, s.Exert);
            Arm(false, s.R, swingR, walk, run, s.Exert);
            float stride = Mathf.Lerp(0.72f, 1.16f, run) / m_Scale;
            var dir = velLocal.sqrMagnitude > 0.02f ? velLocal.normalized : Vector3.forward;
            Leg(true, phase + Mathf.PI, dir, stride, walk, run);
            Leg(false, phase, dir, stride, walk, run);

            // Stand-in: the robe swings a touch with the stride.
            if (m_Robe != null) m_Robe.localRotation = Quaternion.Euler(2.5f * walk * Mathf.Sin(phase * 2f) * (1f + run), 1.5f * walk * swingL, 1.2f * walk * Mathf.Sin(phase));
        }

        void Arm(bool left, in HandIn hand, float swing, float walk, float run, float exert)
        {
            B upper = left ? B.UpperArmL : B.UpperArmR, lower = left ? B.ForearmL : B.ForearmR, end = left ? B.HandL : B.HandR;
            float side = left ? -1f : 1f;
            var shoulder = transform.InverseTransformPoint(m_Bone[(int)upper].position);
            float k = m_UpperArm + m_Forearm;

            // The body's own arm: hanging, swinging with the walk opposite the legs, pumping on a run.
            var wrist = shoulder + new Vector3(0.035f * side, -0.94f * k, 0.07f * k);
            wrist += new Vector3(0f, 0.05f * k * Mathf.Max(0f, swing), 0.3f * k * swing) * walk;
            float pump = Mathf.Sign(swing) * Mathf.Pow(Mathf.Abs(swing), 0.75f);
            var running = shoulder + Vector3.Lerp(new Vector3(0.1f * side, -0.75f, -0.25f) * k, new Vector3(-0.07f * side, -0.32f, 0.43f) * k, 0.5f + 0.5f * pump);
            running.y -= 0.05f * k * exert;
            wrist = Vector3.Lerp(wrist, running, run);
            Vector3 fingers = Vector3.zero, back = Vector3.zero;
            bool fromHand = false;
            float hold = hand.Valid ? Mathf.Clamp01(hand.Hold) : 0f;
            if (hold > 0f)
            {
                // Holding something in first person (or a tracked hand in VR): the body's hand goes where that hand is.
                var inv = Quaternion.Inverse(transform.rotation);
                wrist = Vector3.Lerp(wrist, transform.InverseTransformPoint(hand.Wrist), hold);
                fingers = inv * (hand.Rot * Vector3.forward);
                back = inv * (hand.Rot * Vector3.up);
                fromHand = true;
            }

            var elbow = Elbow(shoulder, wrist, new Vector3(0.7f * side, -0.6f, -0.6f), m_UpperArm, m_Forearm);
            var upperDir = Unit(elbow - shoulder, Vector3.down);
            var reach = elbow + Unit(wrist - elbow, upperDir) * m_Forearm;
            var foreDir = Unit(reach - elbow, upperDir);
            var hinge = Vector3.Cross(upperDir, foreDir);
            var restHinge = left ? m_ArmHingeL : m_ArmHingeR;
            if (hinge.magnitude < 0.05f) hinge = Unit(Vector3.Cross(upperDir, Vector3.forward), restHinge); else hinge.Normalize();
            Aim(upper, upperDir, Vector3.Cross(hinge, upperDir), RestDir(upper, lower), Vector3.Cross(restHinge, RestDir(upper, lower)));
            Aim(lower, foreDir, Vector3.Cross(hinge, foreDir), RestDir(lower, end), Vector3.Cross(restHinge, RestDir(lower, end)));

            // Hand: from the held pose, or relaxed (fingers down the forearm, back of the hand outwards).
            var relaxedFingers = foreDir;
            var relaxedBack = left ? Vector3.Cross(foreDir, Vector3.forward) : Vector3.Cross(Vector3.forward, foreDir);
            if (fromHand)
            {
                fingers = Vector3.Slerp(relaxedFingers, Unit(fingers, relaxedFingers), hold);
                back = Vector3.Slerp(Unit(relaxedBack, Vector3.up), Unit(back, Vector3.up), hold);
            }
            else { fingers = relaxedFingers; back = relaxedBack; }
            var restFingers = RestDir(lower, end);
            var restBack = left ? Vector3.Cross(restFingers, Vector3.forward) : Vector3.Cross(Vector3.forward, restFingers);
            Aim(end, Unit(fingers, foreDir), back, restFingers, restBack);

            // Stand-in hands: the first-person hand rig on the wrist, its sleeve aimed at the elbow.
            var rig = left ? m_HandL : m_HandR;
            if (rig != null)
            {
                rig.Root.SetPositionAndRotation(transform.TransformPoint(reach), transform.rotation * Frame(Unit(fingers, foreDir), back));
                rig.SetPose(hold > 0f ? HandPose.Lerp(HandPose.Relaxed, hand.Fingers, hold) : HandPose.Lerp(HandPose.Relaxed, HandPose.Fist.Curled(-0.2f), run));
                rig.AimForearm(transform.TransformPoint(elbow));
            }
        }

        void Leg(bool left, float psi, Vector3 dir, float stride, float walk, float run)
        {
            B upper = left ? B.UpperLegL : B.UpperLegR, lower = left ? B.LowerLegL : B.LowerLegR, foot = left ? B.FootL : B.FootR;
            var hip = transform.InverseTransformPoint(m_Bone[(int)upper].position);
            // A foot strikes at psi = 0 in front, rolls back under the body, lifts and swings through (psi pi..2 pi).
            float c = Mathf.Cos(psi), sn = Mathf.Sin(psi);
            float lift = sn < 0f ? -sn * Mathf.Lerp(0.08f, 0.22f, run) / m_Scale : 0f;
            var ankle = Rest(foot) + dir * (0.5f * stride * c * walk) + Vector3.up * (lift * walk);
            ankle -= dir * (0.08f * run * Mathf.Max(0f, -sn) / m_Scale); // a running heel kicks up behind
            var knee = Elbow(hip, ankle, Vector3.forward + dir * 0.3f, m_Thigh, m_Shin);
            var thighDir = Unit(knee - hip, Vector3.down);
            var reach = knee + Unit(ankle - knee, thighDir) * m_Shin;
            var shinDir = Unit(reach - knee, thighDir);
            var hinge = Vector3.Cross(thighDir, shinDir);
            if (hinge.magnitude < 0.05f) hinge = Unit(Vector3.Cross(thighDir, Vector3.back), m_LegHinge); else hinge.Normalize();
            Aim(upper, thighDir, Vector3.Cross(thighDir, hinge), RestDir(upper, lower), Vector3.Cross(RestDir(upper, lower), m_LegHinge));
            Aim(lower, shinDir, Vector3.Cross(shinDir, hinge), RestDir(lower, foot), Vector3.Cross(RestDir(lower, foot), m_LegHinge));
            // Toes up at the heel strike, heel up at the push-off; flat when standing.
            float pitch = walk * (16f * c - 6f) * (sn < 0f ? 0.6f : 1f);
            var toes = Quaternion.AngleAxis(-pitch, Vector3.Cross(Vector3.up, dir)) * dir;
            Look(foot, toes, Vector3.up);
        }

        // ------------------------------------------------------------------ the corrupted reflection
        void UpdateStare(in Sample cur, Cell cell, bool corrupt, float dt)
        {
            if (cell != m_StareCell) { m_StareCell = cell; m_Stare = corrupt ? Stare.Waiting : Stare.Done; m_RefYaw = Yaw(cur.HeadRot * Vector3.forward, 0f); }
            float yaw = Yaw(cur.HeadRot * Vector3.forward, m_RefYaw);
            if (m_Stare != Stare.Done && MirrorDirection(cur.HeadPos) is Vector3 seen) { m_MirrorDir = seen; m_MirrorSeen = Time.time; }
            switch (m_Stare)
            {
                case Stare.Waiting:
                    // The way the player has been facing lately; a quick turn well away from it starts the stare.
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, m_RefYaw)) > 110f)
                    {
                        m_StareDir = Time.time - m_MirrorSeen < 2.5f ? m_MirrorDir : Quaternion.Euler(0f, m_RefYaw, 0f) * Vector3.forward;
                        m_Stare = Stare.Away;
                        m_StareTime = 0f;
                    }
                    else m_RefYaw = Mathf.LerpAngle(m_RefYaw, yaw, 1f - Mathf.Exp(-dt * 0.7f));
                    break;
                case Stare.Away:
                    m_StareTime += dt;
                    if (m_StareTime > 6f) { m_Stare = Stare.Waiting; m_RefYaw = yaw; break; }
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, Yaw(m_StareDir, yaw))) < 45f) { m_Stare = Stare.Holding; m_StareTime = 0f; }
                    break;
                case Stare.Holding:
                    m_StareTime += dt;
                    if (m_StareTime > 0.55f) m_Stare = Stare.Done;
                    break;
            }
            // Facing the glass (into the live mirror once it is back in view); snaps back fast when done.
            bool staring = m_Stare == Stare.Away || m_Stare == Stare.Holding;
            if (m_Stare == Stare.Holding && Time.time - m_MirrorSeen < 0.1f) m_StareDir = m_MirrorDir;
            m_StareWeight = staring ? 1f : Mathf.MoveTowards(m_StareWeight, 0f, dt / 0.08f);
        }

        /// <summary>Horizontal direction from the eye into the nearest mirror that is drawing (in view), or null.</summary>
        static Vector3? MirrorDirection(Vector3 eye)
        {
            if (FacingMirror != null)
            {
                var d = FacingMirror(eye);
                return d.HasValue && d.Value.sqrMagnitude > 1e-6f ? d.Value.normalized : (Vector3?)null;
            }
            PlanarMirror best = null;
            float bestDist = 6f;
            foreach (var m in PlanarMirror.All)
            {
                if (m == null || !m.Live || m.Glass == null) continue;
                float dist = Vector3.Distance(eye, m.Glass.bounds.center);
                if (dist < bestDist) { bestDist = dist; best = m; }
            }
            if (best == null) return null;
            // Straight into the glass, so the reflection's stare comes straight back out at the player.
            best.GetPlane(out _, out var normal);
            var to = -normal;
            to.y = 0f;
            return to.sqrMagnitude > 1e-4f ? to.normalized : (Vector3?)null;
        }

        // ------------------------------------------------------------------ lag buffer
        void Push(in Sample s)
        {
            m_Buffer[m_BufferHead] = s;
            m_BufferHead = (m_BufferHead + 1) % m_Buffer.Length;
            m_BufferCount = Mathf.Min(m_BufferCount + 1, m_Buffer.Length);
        }

        Sample SampleAt(float time)
        {
            // Newest first; find the pair around the wanted time and blend them.
            int newest = (m_BufferHead - 1 + m_Buffer.Length) % m_Buffer.Length;
            var later = m_Buffer[newest];
            for (int i = 1; i < m_BufferCount; i++)
            {
                var earlier = m_Buffer[(newest - i + m_Buffer.Length) % m_Buffer.Length];
                if (earlier.Time <= time)
                {
                    float span = later.Time - earlier.Time;
                    return Blend(earlier, later, span > 1e-5f ? (time - earlier.Time) / span : 1f);
                }
                later = earlier;
            }
            return later; // not that much history yet: the oldest there is
        }

        static Sample Blend(in Sample a, in Sample b, float t)
        {
            float dp = b.Phase - a.Phase;
            if (dp < -Mathf.PI) dp += 2f * Mathf.PI; else if (dp > Mathf.PI) dp -= 2f * Mathf.PI;
            return new Sample
            {
                Time = Mathf.Lerp(a.Time, b.Time, t), Feet = Vector3.Lerp(a.Feet, b.Feet, t), HeadPos = Vector3.Lerp(a.HeadPos, b.HeadPos, t),
                Vel = Vector3.Lerp(a.Vel, b.Vel, t), HeadRot = Quaternion.Slerp(a.HeadRot, b.HeadRot, t), Speed = Mathf.Lerp(a.Speed, b.Speed, t),
                Phase = a.Phase + dp * t, Exert = Mathf.Lerp(a.Exert, b.Exert, t), Sprint = t < 0.5f ? a.Sprint : b.Sprint,
                L = BlendHand(a.L, b.L, t), R = BlendHand(a.R, b.R, t),
            };
        }

        static HandIn BlendHand(in HandIn a, in HandIn b, float t) => new HandIn
        {
            Valid = a.Valid || b.Valid, Wrist = Vector3.Lerp(a.Wrist, b.Wrist, t), Rot = Quaternion.Slerp(a.Rot, b.Rot, t),
            Fingers = HandPose.Lerp(a.Fingers, b.Fingers, t), Hold = Mathf.Lerp(a.Valid ? a.Hold : 0f, b.Valid ? b.Hold : 0f, t),
        };

        // ------------------------------------------------------------------ bone helpers (body space = this transform's space)
        void SetPos(B b, Vector3 bodyPoint) => m_Bone[(int)b].position = transform.TransformPoint(bodyPoint);
        void SetRot(B b, Quaternion bodyRotation) => m_Bone[(int)b].rotation = transform.rotation * bodyRotation;

        Vector3 RestDir(B from, B to) => Unit(Rest(to) - Rest(from), Vector3.down);

        /// <summary>Turns a bone from its rest frame (rest direction + side) to a new one, whatever its own axes are.</summary>
        void Aim(B b, Vector3 dir, Vector3 side, Vector3 restDir, Vector3 restSide)
            => SetRot(b, Frame(dir, side) * Quaternion.Inverse(Frame(restDir, restSide)) * m_RestRot[(int)b]);

        /// <summary>Faces a bone (head, foot) that faced body +Z with +Y up at rest towards a new forward and up.</summary>
        void Look(B b, Vector3 forward, Vector3 up) => SetRot(b, Look(forward, up) * m_RestRot[(int)b]);

        static Quaternion Look(Vector3 forward, Vector3 up) => Frame(forward, up) * Quaternion.Inverse(Frame(Vector3.forward, Vector3.up));

        static Quaternion Frame(Vector3 forward, Vector3 up)
        {
            var f = Unit(forward, Vector3.forward);
            var u = up - Vector3.Dot(up, f) * f;
            if (u.sqrMagnitude < 1e-8f) u = Vector3.Cross(f, Mathf.Abs(f.y) < 0.9f ? Vector3.up : Vector3.right);
            return Quaternion.LookRotation(f, u);
        }

        /// <summary>A look direction limited to a neck's reach from the chest's facing (yaw, then pitch up / down).</summary>
        static Vector3 Clamp(Vector3 look, Vector3 facing, float yawMax, float downMax, float upMax)
        {
            float fy = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            var flat = new Vector2(look.x, look.z);
            float yaw = flat.sqrMagnitude > 1e-6f ? Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg : fy;
            float rel = Mathf.Clamp(Mathf.DeltaAngle(fy, yaw), -yawMax, yawMax);
            float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(look.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -upMax, downMax);
            return Quaternion.Euler(pitch, fy + rel, 0f) * Vector3.forward;
        }

        /// <summary>Two-bone reach: where the middle joint sits for this end point, bent towards the pole.</summary>
        static Vector3 Elbow(Vector3 root, Vector3 end, Vector3 pole, float a, float b)
        {
            var d = end - root;
            float dist = d.magnitude;
            if (dist < 1e-4f) return root + Vector3.down * a;
            var dir = d / dist;
            dist = Mathf.Clamp(dist, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            float x = (a * a - b * b + dist * dist) / (2f * dist);
            float r = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            var bend = pole - Vector3.Dot(pole, dir) * dir;
            return root + dir * x + Unit(bend, Vector3.down) * r;
        }

        static float Yaw(Vector3 dir, float fallback)
        {
            var flat = new Vector2(dir.x, dir.z);
            return flat.sqrMagnitude > 1e-4f ? Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg : fallback;
        }

        static float Smooth01(float t) => t * t * (3f - 2f * t);

        static Vector3 Unit(Vector3 v, Vector3 fallback)
        {
            float m = Mathf.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
            return m > 1e-9f ? v / m : fallback;
        }
    }
}
