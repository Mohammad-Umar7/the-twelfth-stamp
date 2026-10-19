using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwelfthStamp
{
    /// <summary>Finger pose for a procedural hand. Finger curl: 0 straight, 1 wrapped round a controller handle, ~1.3 a tight fist.</summary>
    public struct HandPose
    {
        public float Index, Middle, Ring, Pinky;
        /// <summary>0..1: fingers fanned apart.</summary>
        public float Spread;
        /// <summary>Thumb: curl bends its joints, oppose swings it in front of the palm (negative lifts it away), tuck draws it in towards the index.</summary>
        public float ThumbCurl, ThumbOppose, ThumbTuck;

        public HandPose(float index, float middle, float ring, float pinky, float thumbCurl, float thumbOppose, float thumbTuck, float spread = 0f)
        {
            Index = index; Middle = middle; Ring = ring; Pinky = pinky;
            ThumbCurl = thumbCurl; ThumbOppose = thumbOppose; ThumbTuck = thumbTuck; Spread = spread;
        }

        public static HandPose Lerp(in HandPose a, in HandPose b, float t) => new HandPose(
            a.Index + (b.Index - a.Index) * t, a.Middle + (b.Middle - a.Middle) * t, a.Ring + (b.Ring - a.Ring) * t, a.Pinky + (b.Pinky - a.Pinky) * t,
            a.ThumbCurl + (b.ThumbCurl - a.ThumbCurl) * t, a.ThumbOppose + (b.ThumbOppose - a.ThumbOppose) * t,
            a.ThumbTuck + (b.ThumbTuck - a.ThumbTuck) * t, a.Spread + (b.Spread - a.Spread) * t);

        /// <summary>Adds to all four finger curls (looser or tighter grip).</summary>
        public HandPose Curled(float add) => new HandPose(Index + add, Middle + add, Ring + add, Pinky + add, ThumbCurl, ThumbOppose, ThumbTuck, Spread);

        public static readonly HandPose Relaxed = new HandPose(0.25f, 0.3f, 0.36f, 0.42f, 0.2f, 0.15f, 0.25f);
        public static readonly HandPose Fist = new HandPose(1.15f, 1.2f, 1.25f, 1.3f, 0.5f, 0.1f, 0.9f);
    }

    /// <summary>
    /// Saeed's hand with the sleeve of his white kandura (and, on the left wrist, his watch): one skinned mesh with three
    /// materials (skin, kandura, trim for nails and watch), built in code. Desktop arms, VR hands and the mirror body share it.
    /// </summary>
    public class HandRig
    {
        public readonly HandSide Side;
        /// <summary>The wrist joint. +Z runs towards the fingers, +Y is the back of the hand, the thumb is on the inner side.</summary>
        public readonly Transform Root;
        /// <summary>Child of Root at the wrist. Its +Z runs back along the forearm; AimForearm points it at the elbow.</summary>
        public readonly Transform Forearm;
        public readonly SkinnedMeshRenderer Renderer;
        readonly Transform[] m_Bones;
        readonly Quaternion m_ThumbRest;
        readonly float m_Sign;
        readonly bool m_Watch;

        /// <summary>The middle of the palm's surface, in Root space (where a held thing rests).</summary>
        public static readonly Vector3 PalmAnchor = new Vector3(0f, -0.0145f, 0.05f);

        internal HandRig(HandSide side, Transform[] bones, SkinnedMeshRenderer smr, Quaternion thumbRest)
        {
            Side = side;
            m_Bones = bones;
            Root = bones[0];
            Forearm = bones[HandMesh.BoneForearm];
            Renderer = smr;
            m_ThumbRest = thumbRest;
            m_Sign = side == HandSide.Left ? -1f : 1f;
            m_Watch = bones.Length > HandMesh.BoneMinute;
        }

        public bool Visible
        {
            get => Renderer.enabled;
            set { if (Renderer.enabled != value) Renderer.enabled = value; }
        }

        /// <summary>Bends the fingers and thumb (rotations mirror for the left hand); the watch keeps the real time.</summary>
        public void SetPose(in HandPose p)
        {
            float s = m_Sign;
            for (int f = 0; f < 4; f++)
            {
                float c = f == 0 ? p.Index : f == 1 ? p.Middle : f == 2 ? p.Ring : p.Pinky;
                HandMesh.JointAngles(c, out float a0, out float a1, out float a2);
                float yaw = HandMesh.RestYaw[f] + HandMesh.Converge[f] * Mathf.Clamp01(c) + HandMesh.SpreadDeg[f] * p.Spread;
                int b = HandMesh.BoneFinger + f * 3;
                m_Bones[b].localRotation = Quaternion.Euler(a0, yaw * s, 0f);
                m_Bones[b + 1].localRotation = Quaternion.Euler(a1, 0f, 0f);
                m_Bones[b + 2].localRotation = Quaternion.Euler(a2, 0f, 0f);
            }
            var oppAxis = new Vector3(0.0995f * s, 0f, 0.995f);
            m_Bones[HandMesh.BoneThumb].localRotation = Quaternion.AngleAxis(p.ThumbOppose * HandMesh.ThumbOpposeDeg * s, oppAxis)
                * Quaternion.AngleAxis(p.ThumbTuck * HandMesh.ThumbTuckDeg * s, Vector3.up) * m_ThumbRest
                * Quaternion.Euler(p.ThumbCurl * HandMesh.ThumbCurlDeg.x, 0f, 0f);
            m_Bones[HandMesh.BoneThumb + 1].localRotation = Quaternion.Euler(p.ThumbCurl * HandMesh.ThumbCurlDeg.y, 0f, 0f);
            m_Bones[HandMesh.BoneThumb + 2].localRotation = Quaternion.Euler(p.ThumbCurl * HandMesh.ThumbCurlDeg.z, 0f, 0f);
            if (m_Watch)
            {
                // Clockwise from 12 (the far side of the wrist), seen on the dial.
                var now = System.DateTime.Now;
                float minutes = now.Minute + now.Second / 60f;
                m_Bones[HandMesh.BoneHour].localRotation = Quaternion.Euler(0f, (now.Hour % 12 + minutes / 60f) * 30f, 0f);
                m_Bones[HandMesh.BoneMinute].localRotation = Quaternion.Euler(0f, minutes * 6f, 0f);
            }
        }

        /// <summary>Points the sleeve from the wrist at the elbow (world), keeping its seam turned with the back of the hand.</summary>
        public void AimForearm(Vector3 elbow)
        {
            var d = elbow - Root.position;
            float m = Mathf.Sqrt(d.x * d.x + d.y * d.y + d.z * d.z);
            if (m < 1e-4f) return;
            d /= m;
            var up = Root.up;
            up -= Vector3.Dot(up, d) * d;
            if (up.sqrMagnitude < 1e-6f) up = Root.forward;
            Forearm.rotation = Quaternion.LookRotation(d, up);
        }
    }

    /// <summary>
    /// Builds the procedural hand: palm with knuckle heads, three-phalanx fingers (capsule joints that stay closed at any bend,
    /// a slight waist between joints), thumb, nails with a free edge, wrist, the kandura sleeve with its folded cuff, and a
    /// wristwatch on the left. Skin uses generated albedo and normal maps (tendons, veins, knuckle and palm creases, pores).
    /// Geometry is made for a right hand and mirrored for the left. Normals are exact surface normals, normalised without
    /// Unity's small-vector cut-off (millimetre faces stay lit).
    /// </summary>
    public static class HandMesh
    {
        internal const int BoneHand = 0, BoneForearm = 1, BoneThumb = 2, BoneFinger = 5, BoneCount = 17, BoneHour = 17, BoneMinute = 18;
        internal const int SubSkin = 0, SubSleeve = 1, SubTrim = 2;

        // ------------------------------------------------------------------ dimensions (metres, right hand, wrist space)
        // Fingers: knuckle position, rest splay, phalanx lengths, radii at base / middle joint / end joint / tip.
        internal static readonly Vector3[] Mcp = { new Vector3(-0.0265f, 0.001f, 0.09f), new Vector3(-0.008f, 0.002f, 0.0945f), new Vector3(0.0098f, 0.001f, 0.0905f), new Vector3(0.0265f, -0.0015f, 0.0815f) };
        internal static readonly float[] RestYaw = { -5f, -1f, 3f, 8f };
        internal static readonly float[] Converge = { 3f, 0f, -3f, -7f };   // fingers draw together as they close
        internal static readonly float[] SpreadDeg = { -10f, -2f, 5f, 12f };
        static readonly Vector3[] Lengths = { new Vector3(0.044f, 0.026f, 0.022f), new Vector3(0.048f, 0.03f, 0.023f), new Vector3(0.045f, 0.028f, 0.022f), new Vector3(0.035f, 0.021f, 0.019f) };
        static readonly Vector4[] Radii = { new Vector4(0.0094f, 0.0088f, 0.0081f, 0.0071f), new Vector4(0.0097f, 0.0091f, 0.0084f, 0.0073f), new Vector4(0.0092f, 0.0086f, 0.0079f, 0.0069f), new Vector4(0.0082f, 0.0076f, 0.007f, 0.0061f) };

        static readonly Vector3 ThumbBase = new Vector3(-0.019f, -0.0115f, 0.023f);
        static readonly Vector3 ThumbDir = new Vector3(-0.5f, -0.34f, 0.8f);
        static readonly Vector3 ThumbNail = new Vector3(-0.88f, 0.45f, -0.1f);
        static readonly Vector3 ThumbLen = new Vector3(0.044f, 0.032f, 0.027f);
        static readonly Vector4 ThumbRad = new Vector4(0.0118f, 0.0102f, 0.0094f, 0.0082f);
        internal const float ThumbOpposeDeg = 45f, ThumbTuckDeg = 28f;
        internal static readonly Vector3 ThumbCurlDeg = new Vector3(12f, 45f, 60f);

        // Finger joint angles (knuckle, middle, end) at curl 1 (round a handle) and at the tight fist.
        static readonly Vector3 GripAngles = new Vector3(80f, 72f, 44f);
        static readonly Vector3 FistAngles = new Vector3(90f, 106f, 70f);

        // Palm and wrist loft: z, centre x, centre y, half width, half thickness, forearm weight (the wrist bends smoothly).
        internal static readonly float[,] Palm =
        {
            { -0.045f, 0f, 0f, 0.0258f, 0.019f, 1f },
            { -0.018f, 0f, 0f, 0.027f, 0.0185f, 0.55f },
            { 0.006f, -0.001f, -0.001f, 0.031f, 0.0178f, 0f },
            { 0.032f, -0.002f, -0.0022f, 0.0378f, 0.017f, 0f },
            { 0.058f, -0.001f, -0.0014f, 0.0418f, 0.0154f, 0f },
            { 0.074f, 0f, -0.0006f, 0.0426f, 0.0142f, 0f },
            { 0.084f, 0f, 0f, 0.0418f, 0.0134f, 0f },
            { 0.092f, 0f, 0.0006f, 0.0385f, 0.0118f, 0f },
        };
        internal const float PalmExponent = 2.7f, PalmCap0 = 0.012f, PalmCap1 = 0.009f;

        // Kandura sleeve along the forearm (z from the wrist towards the elbow, radius), ordered inside -> rolled hem -> the
        // folded cuff -> the cuff's top edge stepping down onto the sleeve -> the sleeve loosening towards the elbow.
        static readonly Vector2[] Kandura =
        {
            new Vector2(0.075f, 0.0334f), new Vector2(0.017f, 0.0334f), new Vector2(0.0112f, 0.035f), new Vector2(0.0132f, 0.0371f), new Vector2(0.022f, 0.0378f),
            new Vector2(0.036f, 0.0381f), new Vector2(0.0392f, 0.0378f), new Vector2(0.0402f, 0.0366f), new Vector2(0.052f, 0.0374f), new Vector2(0.075f, 0.0398f),
            new Vector2(0.09f, 0.0414f), new Vector2(0.105f, 0.0422f), new Vector2(0.13f, 0.0452f), new Vector2(0.16f, 0.0472f), new Vector2(0.2f, 0.05f),
            new Vector2(0.24f, 0.0524f), new Vector2(0.268f, 0.0532f), new Vector2(0.288f, 0.0508f), new Vector2(0.3f, 0.043f), new Vector2(0.307f, 0.026f),
        };

        // Watch (left wrist, forearm space): dial centre along the arm, case radius; 12 o'clock towards the little-finger side.
        const float WatchZ = 0.003f, WatchR = 0.0145f, CaseBottom = 0.0188f, CaseHeight = 0.0072f;

        // Skin atlas regions (u ranges; v spans each loft, joints at DomeFinger and 1 - DomeFinger).
        internal const float AtlasPad = 3f / HandSkin.Width, DomePalm = 0.06f, DomeFinger = 0.2f;
        internal static readonly Vector2 PalmU = new Vector2(AtlasPad, 0.5f - AtlasPad);
        internal static readonly Vector2 FingerU = new Vector2(0.5f + AtlasPad, 0.75f - AtlasPad);
        internal static readonly Vector2 ThumbU = new Vector2(0.75f + AtlasPad, 1f - AtlasPad);

        internal static void JointAngles(float c, out float a0, out float a1, out float a2)
        {
            if (c <= 1f) { a0 = GripAngles.x * c; a1 = GripAngles.y * c; a2 = GripAngles.z * c; return; }
            float t = Mathf.Clamp01((c - 1f) / 0.3f);
            a0 = Mathf.Lerp(GripAngles.x, FistAngles.x, t); a1 = Mathf.Lerp(GripAngles.y, FistAngles.y, t); a2 = Mathf.Lerp(GripAngles.z, FistAngles.z, t);
        }

        // ------------------------------------------------------------------ materials (three: skin, kandura, trim)
        // A faint self-glow (about 5-6 % of albedo, set as linear values) keeps the hands readable in the darkest rooms.
        static Material s_Skin, s_Kandura, s_Trim, s_BodySkin;
        /// <summary>Off-white kandura cotton (about #EDEBE4) and Saeed's warm light-brown skin (sRGB).</summary>
        internal static readonly Color KanduraColor = new Color(0.91f, 0.9f, 0.87f), SkinColor = new Color(0.7f, 0.52f, 0.4f);

        public static Material SkinMaterial
        {
            get
            {
                if (s_Skin != null) return s_Skin;
                HandSkin.Ensure();
                s_Skin = new Material(Mats.Template("Lit_NormalEmissive")) { name = "TS_HandSkin" };
                s_Skin.SetColor("_BaseColor", Color.white);
                s_Skin.SetTexture("_BaseMap", HandSkin.Albedo);
                s_Skin.SetTexture("_BumpMap", HandSkin.Normal);
                s_Skin.SetFloat("_BumpScale", 1f);
                s_Skin.SetFloat("_Smoothness", 0.36f);
                s_Skin.SetFloat("_Metallic", 0f);
                s_Skin.SetTexture("_EmissionMap", HandSkin.Albedo);
                s_Skin.SetVector("_EmissionColor", new Vector4(0.06f, 0.06f, 0.06f, 1f));
                return s_Skin;
            }
        }

        /// <summary>The kandura's cotton: a plain off-white base (the linen albedo is too dark to tint up to white) with the
        /// linen weave as relief. The first-person sleeves, and the robe of the mirror body.</summary>
        public static Material KanduraMaterial
        {
            get
            {
                if (s_Kandura != null) return s_Kandura;
                var normal = Tex.Normal("fabric_linen");
                s_Kandura = new Material(Mats.Template(normal != null ? "Lit_NormalEmissive" : "Lit_Emissive")) { name = "TS_Kandura" };
                s_Kandura.SetColor("_BaseColor", KanduraColor);
                s_Kandura.SetTexture("_BaseMap", Tex.White);
                if (normal != null) { s_Kandura.SetTexture("_BumpMap", normal); s_Kandura.SetFloat("_BumpScale", 0.45f); }
                s_Kandura.SetFloat("_Smoothness", 0.18f);
                s_Kandura.SetFloat("_Metallic", 0f);
                s_Kandura.SetTexture("_EmissionMap", Tex.White);
                var lin = KanduraColor.linear;
                s_Kandura.SetVector("_EmissionColor", new Vector4(lin.r * 0.05f, lin.g * 0.05f, lin.b * 0.05f, 1f));
                return s_Kandura;
            }
        }

        /// <summary>Plain skin (no hand atlas) for the face and other body parts of the mirror body's stand-in.</summary>
        public static Material BodySkinMaterial
        {
            get
            {
                if (s_BodySkin != null) return s_BodySkin;
                s_BodySkin = new Material(Mats.Template("Lit_Emissive")) { name = "TS_BodySkin" };
                s_BodySkin.SetColor("_BaseColor", SkinColor);
                s_BodySkin.SetFloat("_Smoothness", 0.38f);
                s_BodySkin.SetFloat("_Metallic", 0f);
                s_BodySkin.SetTexture("_EmissionMap", Tex.White);
                var lin = SkinColor.linear;
                s_BodySkin.SetVector("_EmissionColor", new Vector4(lin.r * 0.06f, lin.g * 0.06f, lin.b * 0.06f, 1f));
                return s_BodySkin;
            }
        }

        /// <summary>Nails and watch share one small generated atlas (the watch's markers and hands glow faintly).</summary>
        public static Material TrimMaterial
        {
            get
            {
                if (s_Trim != null) return s_Trim;
                HandTrim.Ensure();
                s_Trim = new Material(Mats.Template("Lit_Emissive")) { name = "TS_HandTrim" };
                s_Trim.SetColor("_BaseColor", Color.white);
                s_Trim.SetTexture("_BaseMap", HandTrim.Albedo);
                s_Trim.SetFloat("_Smoothness", 0.58f);
                s_Trim.SetFloat("_Metallic", 0f);
                s_Trim.SetTexture("_EmissionMap", HandTrim.Emission);
                s_Trim.SetVector("_EmissionColor", new Vector4(1f, 1f, 1f, 1f));
                return s_Trim;
            }
        }

        static Quaternion Mirror(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);
        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        static Quaternion ThumbRestRight() => Quaternion.LookRotation(ThumbDir.normalized, ThumbNail);

        /// <summary>Creates the hand under <paramref name="parent"/>: no colliders, no shadows, on the PlayerBody layer.</summary>
        public static HandRig Build(string name, Transform parent, HandSide side)
        {
            bool left = side == HandSide.Left;
            int count = left ? BoneMinute + 1 : BoneCount; // the left wrist carries the watch and its two hands
            var root = new GameObject(name);
            root.layer = Layers.PlayerBody;
            root.transform.SetParent(parent, false);

            // Right-hand rest frames (wrist space); the left hand's bones are their mirror image.
            var rest = new Matrix4x4[count];
            var pos = new Vector3[count];
            var rot = new Quaternion[count];
            var parentOf = new int[count];
            parentOf[0] = -1; pos[0] = Vector3.zero; rot[0] = Quaternion.identity;
            parentOf[1] = 0; pos[1] = Vector3.zero; rot[1] = Quaternion.LookRotation(Vector3.back, Vector3.up);
            var thumbRest = ThumbRestRight();
            parentOf[2] = 0; pos[2] = ThumbBase; rot[2] = thumbRest;
            parentOf[3] = 2; pos[3] = new Vector3(0f, 0f, ThumbLen.x); rot[3] = Quaternion.identity;
            parentOf[4] = 3; pos[4] = new Vector3(0f, 0f, ThumbLen.y); rot[4] = Quaternion.identity;
            for (int f = 0; f < 4; f++)
            {
                int b = BoneFinger + f * 3;
                parentOf[b] = 0; pos[b] = Mcp[f]; rot[b] = Quaternion.Euler(0f, RestYaw[f], 0f);
                parentOf[b + 1] = b; pos[b + 1] = new Vector3(0f, 0f, Lengths[f].x); rot[b + 1] = Quaternion.identity;
                parentOf[b + 2] = b + 1; pos[b + 2] = new Vector3(0f, 0f, Lengths[f].y); rot[b + 2] = Quaternion.identity;
            }
            if (left)
            {
                // Watch hands pivot on the dial centre (forearm space, on the x = 0 plane, so mirroring leaves them alone).
                parentOf[BoneHour] = BoneForearm; pos[BoneHour] = new Vector3(0f, CaseBottom + CaseHeight + 0.0016f, WatchZ); rot[BoneHour] = Quaternion.identity;
                parentOf[BoneMinute] = BoneForearm; pos[BoneMinute] = new Vector3(0f, CaseBottom + CaseHeight + 0.002f, WatchZ); rot[BoneMinute] = Quaternion.identity;
            }
            for (int i = 0; i < count; i++)
            {
                var local = Matrix4x4.TRS(pos[i], rot[i], Vector3.one);
                rest[i] = parentOf[i] < 0 ? local : rest[parentOf[i]] * local;
            }

            var bones = new Transform[count];
            bones[0] = root.transform;
            string[] names = { "Hand", "Forearm", "Thumb1", "Thumb2", "Thumb3" };
            for (int i = 1; i < count; i++)
            {
                string bn = i < names.Length ? names[i] : i == BoneHour ? "WatchHour" : i == BoneMinute ? "WatchMinute" : "Finger" + ((i - BoneFinger) / 3) + "_" + ((i - BoneFinger) % 3);
                var t = new GameObject(bn).transform;
                t.gameObject.layer = Layers.PlayerBody;
                t.SetParent(bones[parentOf[i]], false);
                t.localPosition = left ? Mirror(pos[i]) : pos[i];
                t.localRotation = left ? Mirror(rot[i]) : rot[i];
                bones[i] = t;
            }

            var mb = new SkinBuilder();
            BuildGeometry(mb, rest);
            if (left)
            {
                mb.MirrorX();
                BuildWatch(mb, rest[BoneForearm]); // the forearm frame is the same on both sides; the watch is built as it sits
            }

            var bind = new Matrix4x4[count];
            var rootToWorld = root.transform.localToWorldMatrix;
            for (int i = 0; i < count; i++) bind[i] = bones[i].worldToLocalMatrix * rootToWorld;

            var mesh = mb.ToMesh(name + "Mesh");
            mesh.bindposes = bind;

            var smr = root.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones[0];
            smr.sharedMaterials = new[] { SkinMaterial, KanduraMaterial, TrimMaterial };
            smr.quality = SkinQuality.Bone2;
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(new Vector3(0f, 0f, 0.02f), new Vector3(0.8f, 0.8f, 0.8f)); // the sleeve can swing to any side
            smr.shadowCastingMode = ShadowCastingMode.Off;
            smr.receiveShadows = true;
            smr.skinnedMotionVectors = false;
            smr.lightProbeUsage = LightProbeUsage.BlendProbes;

            return new HandRig(side, bones, smr, left ? Mirror(thumbRest) : thumbRest);
        }

        static void BuildGeometry(SkinBuilder mb, Matrix4x4[] rest)
        {
            // Palm, knuckle heads and wrist (the wrist rings lean on the forearm bone so a bent wrist stays smooth).
            int rows = Palm.GetLength(0);
            var palm = new Ring[rows];
            var palmW = new float[rows];
            for (int i = 0; i < rows; i++)
            {
                palm[i] = new Ring(Palm[i, 0], Palm[i, 1], Palm[i, 2], Palm[i, 3], Palm[i, 4]);
                palmW[i] = Palm[i, 5];
            }
            mb.Loft(Matrix4x4.identity, palm, 28, SubSkin, i => Weight(BoneHand, BoneForearm, palmW[i]), PalmCap0, PalmCap1, PalmExponent,
                UvMap.Region(PalmU, DomePalm), KnuckleHeads);

            // Thumb: metacarpal (forms the ball of the thumb), two phalanges and the nail.
            for (int j = 0; j < 3; j++)
            {
                int bone = BoneThumb + j;
                float len = ThumbLen[j], r0 = ThumbRad[j], r1 = ThumbRad[j + 1], rm = (r0 + r1) * 0.5f * 0.95f;
                float cap1 = j == 2 ? r1 * 1.2f : r1 * 0.95f;
                var rings = new[] { new Ring(0f, 0f, 0f, r0 * 1.04f, r0 * 0.94f), new Ring(len * 0.5f, 0f, 0f, rm * 1.04f, rm * 0.94f), new Ring(len, 0f, 0f, r1 * 1.04f, r1 * 0.94f) };
                mb.Loft(rest[bone], rings, 12, SubSkin, i => Weight(bone, bone, 0f), r0 * 0.95f, cap1, 2f, UvMap.Region(ThumbU, DomeFinger));
                if (j == 2) mb.Nail(rest[bone], bone, len, r0, r1, cap1, 1.04f, 0.94f, 0.7f);
            }

            // Fingers: each phalanx a tapered capsule centred on its joints (closed at any bend), narrower between joints.
            for (int f = 0; f < 4; f++)
                for (int j = 0; j < 3; j++)
                {
                    int bone = BoneFinger + f * 3 + j;
                    float len = Lengths[f][j], r0 = Radii[f][j], r1 = Radii[f][j + 1], rm = (r0 + r1) * 0.5f * 0.94f;
                    float k0 = j == 0 ? 1.1f : 1f; // proud knuckles
                    float cap1 = j == 2 ? r1 * 1.2f : r1 * 0.92f;
                    var rings = new[] { new Ring(0f, 0f, 0f, r0 * 1.06f * k0, r0 * 0.92f * k0), new Ring(len * 0.5f, 0f, 0f, rm * 1.06f, rm * 0.92f), new Ring(len, 0f, 0f, r1 * 1.06f, r1 * 0.92f) };
                    mb.Loft(rest[bone], rings, 10, SubSkin, i => Weight(bone, bone, 0f), r0 * 0.95f * k0, cap1, 2f, UvMap.Region(FingerU, DomeFinger));
                    if (j == 2) mb.Nail(rest[bone], bone, len, r0, r1, cap1, 1.06f, 0.92f, 0.62f);
                }

            // The kandura sleeve with its folded cuff and rolled hem, on the forearm bone.
            var forearm = rest[BoneForearm];
            var fw = Weight(BoneForearm, BoneForearm, 0f);
            var sleeve = new Ring[Kandura.Length];
            for (int i = 0; i < Kandura.Length; i++) sleeve[i] = new Ring(Kandura[i].x, 0f, 0f, Kandura[i].y * 1.07f, Kandura[i].y * 0.93f);
            mb.Loft(forearm, sleeve, 22, SubSleeve, i => fw, 0f, 0.004f, 2f, UvMap.World(0.12f));
        }

        /// <summary>The watch on the left wrist, partly under the cuff: leather strap, steel case and crown, dial, and two hands.</summary>
        static void BuildWatch(SkinBuilder mb, Matrix4x4 forearm)
        {
            var fw = Weight(BoneForearm, BoneForearm, 0f);
            var strap = new[] { new Ring(WatchZ - 0.0075f, 0f, 0.0004f, 0.0272f, 0.0196f), new Ring(WatchZ + 0.0075f, 0f, 0.0004f, 0.0276f, 0.02f) };
            mb.Loft(forearm, strap, 24, SubTrim, i => fw, 0f, 0f, 2.6f, UvMap.Rect(HandTrim.LeatherRect));
            // 12 o'clock towards the little-finger side (+x here), 3 o'clock (the crown) towards the hand (-z).
            var e3 = Vector3.back;
            var e12 = Vector3.right;
            mb.Disc(forearm, new Vector3(0f, CaseBottom, WatchZ), Vector3.up, e3, e12, WatchR, CaseHeight, 0.0012f, 20, SubTrim, fw,
                HandTrim.DialCentre, HandTrim.DialRadius, HandTrim.SteelCentre, 0.08f);
            mb.Disc(forearm, new Vector3(0f, CaseBottom + CaseHeight * 0.5f, WatchZ - WatchR - 0.0003f), Vector3.back, Vector3.right, Vector3.up, 0.0018f, 0.0022f, 0f, 8, SubTrim, fw,
                HandTrim.SteelCentre, 0.04f, HandTrim.SteelCentre, 0.04f);
            mb.Bar(forearm, BoneHour, new Vector3(0f, CaseBottom + CaseHeight + 0.0016f, WatchZ), e12, Vector3.up, 0.0076f, 0.0013f, SubTrim, HandTrim.LumeCentre);
            mb.Bar(forearm, BoneMinute, new Vector3(0f, CaseBottom + CaseHeight + 0.002f, WatchZ), e12, Vector3.up, 0.0116f, 0.0009f, SubTrim, HandTrim.LumeCentre);
        }

        /// <summary>Knuckle heads: low rounded bumps on the back of the hand at each finger's knuckle.</summary>
        static Vector3 KnuckleHeads(Vector3 p, float t)
        {
            float s = Mathf.Sin(t);
            if (s <= 0f) return p;
            float bump = 0f;
            for (int f = 0; f < 4; f++)
            {
                float dx = (p.x - Mcp[f].x) / 0.0072f, dz = (p.z - (Mcp[f].z - 0.004f)) / 0.009f;
                bump += 0.0024f * Mathf.Exp(-dx * dx - dz * dz);
            }
            return new Vector3(p.x, p.y + bump * s * s, p.z);
        }

        static BoneWeight Weight(int b0, int b1, float w1)
        {
            if (w1 <= 0f) return new BoneWeight { boneIndex0 = b0, weight0 = 1f };
            return new BoneWeight { boneIndex0 = b0, weight0 = 1f - w1, boneIndex1 = b1, weight1 = w1 };
        }

        internal struct Ring
        {
            public float Z, Cx, Cy, A, B;
            public Ring(float z, float cx, float cy, float a, float b) { Z = z; Cx = cx; Cy = cy; A = a; B = b; }
        }

        /// <summary>How a loft lays out its UVs: an atlas region (u round, v along with fixed joint bands) or world-scaled tiling.</summary>
        internal struct UvMap
        {
            public bool IsWorld;
            public float Scale, U0, U1, V0, V1, Dome;
            public static UvMap Region(Vector2 u, float dome) => new UvMap { U0 = u.x, U1 = u.y, V0 = 0f, V1 = 1f, Dome = dome };
            public static UvMap Rect(Rect r) => new UvMap { U0 = r.xMin, U1 = r.xMax, V0 = r.yMin, V1 = r.yMax, Dome = 0f };
            public static UvMap World(float scale) => new UvMap { IsWorld = true, Scale = scale };
        }

        /// <summary>Skinned-mesh lists with lofting and small-part helpers. Normals come from the surface itself (dU x dV), so
        /// profiles that fold back on themselves (a rolled hem) light correctly inside and out.</summary>
        internal class SkinBuilder
        {
            readonly List<Vector3> m_V = new List<Vector3>();
            readonly List<Vector3> m_N = new List<Vector3>();
            readonly List<Vector2> m_UV = new List<Vector2>();
            readonly List<BoneWeight> m_W = new List<BoneWeight>();
            readonly List<int>[] m_T = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            readonly List<Ring> m_Rings = new List<Ring>();
            readonly List<int> m_RingSrc = new List<int>();

            int Add(Vector3 p, Vector3 n, Vector2 uv, BoneWeight w)
            {
                m_V.Add(p); m_N.Add(n); m_UV.Add(uv); m_W.Add(w);
                return m_V.Count - 1;
            }

            /// <summary>Unit vector without the 1e-5 cut-off of Vector3.normalized.</summary>
            public static Vector3 Unit(Vector3 v, Vector3 fallback)
            {
                float m = Mathf.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
                return m > 1e-12f ? v / m : fallback;
            }

            /// <summary>Adds a triangle wound so its front faces <paramref name="outward"/>.</summary>
            void Tri(int sub, int a, int b, int c, Vector3 outward)
            {
                var n = Vector3.Cross(m_V[b] - m_V[a], m_V[c] - m_V[a]);
                var t = m_T[sub];
                if (Vector3.Dot(n, outward) >= 0f) { t.Add(a); t.Add(b); t.Add(c); }
                else { t.Add(a); t.Add(c); t.Add(b); }
            }

            static Vector2 Superellipse(float t, float a, float b, float e)
            {
                float c = Mathf.Cos(t), s = Mathf.Sin(t);
                float k = 2f / e;
                return new Vector2(a * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), k), b * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), k));
            }

            /// <summary>Lofted surface along a bone's +Z (rings ordered along the surface), with optional domes closing either end.</summary>
            public void Loft(Matrix4x4 frame, IList<Ring> rings, int sides, int sub, System.Func<int, BoneWeight> weight, float cap0, float cap1, float exponent,
                UvMap uv, System.Func<Vector3, float, Vector3> displace = null)
            {
                m_Rings.Clear(); m_RingSrc.Clear();
                var first = rings[0];
                var last = rings[rings.Count - 1];
                if (cap0 > 0f)
                    for (int k = 3; k >= 1; k--)
                    {
                        float ph = k / 4f * Mathf.PI * 0.5f;
                        m_Rings.Add(new Ring(first.Z - cap0 * Mathf.Sin(ph), first.Cx, first.Cy, first.A * Mathf.Cos(ph), first.B * Mathf.Cos(ph)));
                        m_RingSrc.Add(0);
                    }
                for (int i = 0; i < rings.Count; i++) { m_Rings.Add(rings[i]); m_RingSrc.Add(i); }
                if (cap1 > 0f)
                    for (int k = 1; k <= 3; k++)
                    {
                        float ph = k / 4f * Mathf.PI * 0.5f;
                        m_Rings.Add(new Ring(last.Z + cap1 * Mathf.Sin(ph), last.Cx, last.Cy, last.A * Mathf.Cos(ph), last.B * Mathf.Cos(ph)));
                        m_RingSrc.Add(rings.Count - 1);
                    }

                int n = m_Rings.Count;
                var p = new Vector3[n * sides];
                for (int i = 0; i < n; i++)
                {
                    var r = m_Rings[i];
                    for (int j = 0; j < sides; j++)
                    {
                        float t = 2f * Mathf.PI * j / sides;
                        var q = Superellipse(t, r.A, r.B, exponent);
                        var pt = new Vector3(r.Cx + q.x, r.Cy + q.y, r.Z);
                        p[i * sides + j] = displace != null ? displace(pt, t) : pt;
                    }
                }

                // V: world-scaled arc length, or the atlas band (domes in the end bands, the tube between them by z).
                var vs = new float[n];
                for (int i = 1; i < n; i++) vs[i] = vs[i - 1] + Vector3.Distance(p[i * sides], p[(i - 1) * sides]);
                if (!uv.IsWorld)
                    for (int i = 0; i < n; i++) vs[i] = uv.V0 + (uv.V1 - uv.V0) * BandV(m_Rings[i].Z, first.Z, last.Z, cap0, cap1, uv.Dome);

                int cols = sides + 1; // duplicated seam column for clean UVs
                var idx = new int[n * cols];
                for (int i = 0; i < n; i++)
                {
                    var w = weight(m_RingSrc[i]);
                    var r = m_Rings[i];
                    float perimeter = Mathf.PI * (r.A + r.B);
                    for (int j = 0; j <= sides; j++)
                    {
                        int jj = j % sides;
                        var dt = p[i * sides + (jj + 1) % sides] - p[i * sides + (jj + sides - 1) % sides];
                        var du = p[Mathf.Min(i + 1, n - 1) * sides + jj] - p[Mathf.Max(i - 1, 0) * sides + jj];
                        var radial = new Vector3(p[i * sides + jj].x - r.Cx, p[i * sides + jj].y - r.Cy, 0f);
                        var nrm = Unit(Vector3.Cross(dt, du), Unit(radial, Vector3.up));
                        var pos = frame.MultiplyPoint3x4(p[i * sides + jj]);
                        var nw = Unit(frame.MultiplyVector(nrm), Vector3.up);
                        float uu = j / (float)sides;
                        var tex = uv.IsWorld ? new Vector2(uu * perimeter / uv.Scale, vs[i] / uv.Scale) : new Vector2(uv.U0 + (uv.U1 - uv.U0) * uu, vs[i]);
                        idx[i * cols + j] = Add(pos, nw, tex, w);
                    }
                }

                var tl = m_T[sub];
                for (int i = 0; i < n - 1; i++)
                    for (int j = 0; j < sides; j++)
                    {
                        int a = idx[i * cols + j], b = idx[i * cols + j + 1], c = idx[(i + 1) * cols + j + 1], d = idx[(i + 1) * cols + j];
                        tl.Add(a); tl.Add(b); tl.Add(c);
                        tl.Add(a); tl.Add(c); tl.Add(d);
                    }
                float uMid = uv.IsWorld ? 0f : (uv.U0 + uv.U1) * 0.5f;
                if (cap0 > 0f)
                {
                    int pole = Add(frame.MultiplyPoint3x4(new Vector3(first.Cx, first.Cy, first.Z - cap0)), Unit(frame.MultiplyVector(Vector3.back), Vector3.back),
                        new Vector2(uMid, uv.IsWorld ? 0f : uv.V0), weight(0));
                    for (int j = 0; j < sides; j++) { tl.Add(pole); tl.Add(idx[j + 1]); tl.Add(idx[j]); }
                }
                if (cap1 > 0f)
                {
                    int pole = Add(frame.MultiplyPoint3x4(new Vector3(last.Cx, last.Cy, last.Z + cap1)), Unit(frame.MultiplyVector(Vector3.forward), Vector3.forward),
                        new Vector2(uMid, uv.IsWorld ? (vs[n - 1] + cap1) / uv.Scale : uv.V1), weight(rings.Count - 1));
                    for (int j = 0; j < sides; j++) { tl.Add(idx[(n - 1) * cols + j]); tl.Add(idx[(n - 1) * cols + j + 1]); tl.Add(pole); }
                }
            }

            /// <summary>0..1 along a skin loft: dome bands at the ends, the tube between them proportional to z (shared by HandSkin).</summary>
            internal static float BandV(float z, float z0, float z1, float cap0, float cap1, float dome)
            {
                if (z < z0) return cap0 > 0f ? dome * Mathf.Clamp01(1f - (z0 - z) / cap0) : dome;
                if (z > z1) return cap1 > 0f ? 1f - dome * Mathf.Clamp01(1f - (z - z1) / cap1) : 1f - dome;
                return dome + (1f - 2f * dome) * (z1 > z0 ? (z - z0) / (z1 - z0) : 0f);
            }

            /// <summary>Surface point and exact normal on a phalanx capsule (bone space), z along the bone and onto the tip dome.</summary>
            static void CapsulePoint(float t, float z, float len, float r0, float r1, float cap1, float kx, float ky, out Vector3 p, out Vector3 n)
            {
                if (z <= len)
                {
                    float s = z / len;
                    float r = r0 + (r1 - r0) * s;
                    // Matches the loft's three rings (base, 0.94 waist, end) closely enough for a nail resting on it.
                    float w = 1f - 0.06f * Mathf.Sin(Mathf.PI * s);
                    float a = r * kx * w, b = r * ky * w;
                    p = new Vector3(a * Mathf.Cos(t), b * Mathf.Sin(t), z);
                    n = Unit(new Vector3(Mathf.Cos(t) / a, Mathf.Sin(t) / b, (r0 - r1) / len * 0.9f), Vector3.up);
                    return;
                }
                float a1 = r1 * kx, b1 = r1 * ky;
                float ph = Mathf.Asin(Mathf.Clamp01((z - len) / cap1));
                float cp = Mathf.Cos(ph), sp = Mathf.Sin(ph);
                p = new Vector3(a1 * Mathf.Cos(t) * cp, b1 * Mathf.Sin(t) * cp, z);
                n = Unit(new Vector3(cp * Mathf.Cos(t) / a1, cp * Mathf.Sin(t) / b1, sp / cap1), Vector3.forward);
            }

            /// <summary>A curved nail plate on the back of a distal phalanx; its free edge runs straight on past the fingertip.</summary>
            public void Nail(Matrix4x4 frame, int bone, float len, float r0, float r1, float cap1, float kx, float ky, float width)
            {
                const int rows = 7, cols = 5;
                float z0 = 0.36f * len, z1 = len + 0.62f * cap1;
                var w = Weight(bone, bone, 0f);
                var top = new Vector3[rows * cols];
                var bot = new Vector3[rows * cols];
                var nrm = new Vector3[rows * cols];
                for (int i = 0; i < rows; i++)
                {
                    float s = i / (float)(rows - 1);
                    float z = z0 + (z1 - z0) * s;
                    float half = width * (0.78f + 0.22f * Mathf.Sin(Mathf.Min(1f, s * 1.4f) * Mathf.PI * 0.5f)); // narrower at the cuticle
                    for (int j = 0; j < cols; j++)
                    {
                        float c = -1f + 2f * j / (cols - 1);
                        float t = Mathf.PI * 0.5f + c * half;
                        CapsulePoint(t, z, len, r0, r1, cap1, kx, ky, out var pt, out var nn);
                        if (i == rows - 1)
                        {
                            // Free edge: carry on along the nail instead of following the fingertip down.
                            CapsulePoint(t, z0 + (z1 - z0) * (rows - 2) / (rows - 1), len, r0, r1, cap1, kx, ky, out var prev, out _);
                            pt = prev + Unit(pt - prev, Vector3.forward) * Vector3.Distance(pt, prev) + nn * 0.0006f;
                        }
                        float lift = 0.00028f + 0.00022f * (1f - c * c);
                        top[i * cols + j] = pt + nn * lift;
                        bot[i * cols + j] = pt + nn * 0.00004f;
                        nrm[i * cols + j] = nn;
                    }
                }
                // Top (lit, glossy plate) and underside (the free edge seen from below), UVs into the nail strip.
                var ti = new int[rows * cols];
                var bi = new int[rows * cols];
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                    {
                        int k = i * cols + j;
                        var uvTop = HandTrim.NailUv(j / (float)(cols - 1), i / (float)(rows - 1));
                        ti[k] = Add(frame.MultiplyPoint3x4(top[k]), Unit(frame.MultiplyVector(nrm[k]), Vector3.up), uvTop, w);
                        bi[k] = Add(frame.MultiplyPoint3x4(bot[k]), Unit(frame.MultiplyVector(-nrm[k]), Vector3.down), HandTrim.NailUv(j / (float)(cols - 1), 0.97f), w);
                    }
                for (int i = 0; i < rows - 1; i++)
                    for (int j = 0; j < cols - 1; j++)
                    {
                        int k = i * cols + j;
                        var up = frame.MultiplyVector(nrm[k]);
                        Tri(SubTrim, ti[k], ti[k + 1], ti[k + cols + 1], up); Tri(SubTrim, ti[k], ti[k + cols + 1], ti[k + cols], up);
                        Tri(SubTrim, bi[k], bi[k + 1], bi[k + cols + 1], -up); Tri(SubTrim, bi[k], bi[k + cols + 1], bi[k + cols], -up);
                    }
                // Rim walls round the plate, so the edge reads as a thickness.
                var ring = new List<int>();
                for (int j = 0; j < cols; j++) ring.Add(j);
                for (int i = 1; i < rows; i++) ring.Add(i * cols + cols - 1);
                for (int j = cols - 2; j >= 0; j--) ring.Add((rows - 1) * cols + j);
                for (int i = rows - 2; i > 0; i--) ring.Add(i * cols);
                var centre = Vector3.zero;
                foreach (var v in top) centre += v;
                centre /= top.Length;
                for (int k = 0; k < ring.Count; k++)
                {
                    int a = ring[k], b = ring[(k + 1) % ring.Count];
                    var outward = Vector3.Cross(top[b] - top[a], nrm[a]);
                    if (Vector3.Dot(outward, top[a] - centre) < 0f) outward = -outward;
                    var on = Unit(frame.MultiplyVector(outward), Vector3.up);
                    var uvA = HandTrim.NailUv(a % cols / (float)(cols - 1), a / cols / (float)(rows - 1));
                    var uvB = HandTrim.NailUv(b % cols / (float)(cols - 1), b / cols / (float)(rows - 1));
                    int ta = Add(frame.MultiplyPoint3x4(top[a]), on, uvA, w), tb = Add(frame.MultiplyPoint3x4(top[b]), on, uvB, w);
                    int ba = Add(frame.MultiplyPoint3x4(bot[a]), on, uvA, w), bb = Add(frame.MultiplyPoint3x4(bot[b]), on, uvB, w);
                    Tri(SubTrim, ta, tb, ba, on); Tri(SubTrim, tb, bb, ba, on);
                }
            }

            /// <summary>A short cylinder along <paramref name="normal"/> with a gently domed cap: button, watch case, crown.
            /// The cap maps as a disc (e3 = +u, e12 = +v) round <paramref name="capUv"/>; the side round <paramref name="sideUv"/>.</summary>
            public void Disc(Matrix4x4 frame, Vector3 centre, Vector3 normal, Vector3 e3, Vector3 e12, float radius, float height, float dome, int sides, int sub, BoneWeight w,
                Vector2 capUv, float capUvRadius, Vector2 sideUv, float sideUvRadius)
            {
                var c0 = centre;
                var c1 = centre + normal * height;
                var nw = Unit(frame.MultiplyVector(normal), Vector3.up);
                var sb = new int[sides];
                var st = new int[sides];
                var cp = new int[sides];
                var bv = new int[sides];
                for (int k = 0; k < sides; k++)
                {
                    float th = 2f * Mathf.PI * k / sides;
                    var d = e3 * Mathf.Cos(th) + e12 * Mathf.Sin(th);
                    var dw = Unit(frame.MultiplyVector(d), Vector3.up);
                    var sideUvK = sideUv + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * sideUvRadius;
                    var rim = frame.MultiplyPoint3x4(c1 + d * radius * 0.97f);
                    var rimN = Unit(nw * 0.8f + dw * 0.35f, nw); // the cap's rim leans out a little so the edge catches light like a bevel
                    sb[k] = Add(frame.MultiplyPoint3x4(c0 + d * radius), dw, sideUvK, w);
                    st[k] = Add(frame.MultiplyPoint3x4(c1 + d * radius), dw, sideUvK, w);
                    bv[k] = Add(rim, rimN, sideUvK, w);
                    cp[k] = Add(rim, rimN, capUv + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * capUvRadius * 0.97f, w);
                }
                int apex = Add(frame.MultiplyPoint3x4(c1 + normal * dome), nw, capUv, w);
                for (int k = 0; k < sides; k++)
                {
                    int k1 = (k + 1) % sides;
                    var outward = frame.MultiplyVector((e3 * Mathf.Cos(2f * Mathf.PI * (k + 0.5f) / sides) + e12 * Mathf.Sin(2f * Mathf.PI * (k + 0.5f) / sides)));
                    Tri(sub, sb[k], sb[k1], st[k1], outward); Tri(sub, sb[k], st[k1], st[k], outward);
                    Tri(sub, cp[k], cp[k1], apex, nw);
                    Tri(sub, st[k], st[k1], bv[k1], nw); Tri(sub, st[k], bv[k1], bv[k], nw); // bevel ring between side and cap
                }
            }

            /// <summary>A thin flat bar from <paramref name="pivot"/> along <paramref name="dir"/> (a watch hand), on its own bone.</summary>
            public void Bar(Matrix4x4 frame, int bone, Vector3 pivot, Vector3 dir, Vector3 up, float length, float width, int sub, Vector2 uv)
            {
                var w = Weight(bone, bone, 0f);
                var side = Vector3.Cross(up, dir) * (width * 0.5f);
                var tail = pivot - dir * (length * 0.22f);
                var tip = pivot + dir * length;
                var nw = Unit(frame.MultiplyVector(up), Vector3.up);
                int a = Add(frame.MultiplyPoint3x4(tail - side), nw, uv, w), b = Add(frame.MultiplyPoint3x4(tail + side), nw, uv, w);
                int c = Add(frame.MultiplyPoint3x4(tip + side * 0.55f), nw, uv, w), d = Add(frame.MultiplyPoint3x4(tip - side * 0.55f), nw, uv, w);
                Tri(sub, a, b, c, nw); Tri(sub, a, c, d, nw);
            }

            /// <summary>An ellipsoid (latitude-longitude grid round +Y) with optional displacement (curls); normals from the
            /// displaced surface itself, poles from the frame.</summary>
            public void Ellipsoid(Matrix4x4 frame, Vector3 radii, int lon, int lat, int sub, float uvScale, System.Func<Vector3, Vector3> displace = null)
            {
                var w = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                int rows = lat - 1;
                var p = new Vector3[rows * lon];
                for (int i = 0; i < rows; i++)
                {
                    float ph = -Mathf.PI * 0.5f + Mathf.PI * (i + 1) / lat;
                    for (int j = 0; j < lon; j++)
                    {
                        float th = 2f * Mathf.PI * j / lon;
                        var q = new Vector3(radii.x * Mathf.Cos(ph) * Mathf.Cos(th), radii.y * Mathf.Sin(ph), radii.z * Mathf.Cos(ph) * Mathf.Sin(th));
                        p[i * lon + j] = displace != null ? displace(q) : q;
                    }
                }
                var south = new Vector3(0f, -radii.y, 0f);
                var north = new Vector3(0f, radii.y, 0f);
                if (displace != null) { south = displace(south); north = displace(north); }
                int cols = lon + 1;
                var idx = new int[rows * cols];
                float girth = Mathf.PI * (radii.x + radii.z);
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j <= lon; j++)
                    {
                        int jj = j % lon;
                        var dt = p[i * lon + (jj + 1) % lon] - p[i * lon + (jj + lon - 1) % lon];
                        var du = (i + 1 < rows ? p[(i + 1) * lon + jj] : north) - (i > 0 ? p[(i - 1) * lon + jj] : south);
                        var q = p[i * lon + jj];
                        var analytic = Unit(new Vector3(q.x / (radii.x * radii.x), q.y / (radii.y * radii.y), q.z / (radii.z * radii.z)), Vector3.up);
                        var n = Unit(Vector3.Cross(du, dt), analytic);
                        if (Vector3.Dot(n, analytic) < 0f) n = -n;
                        idx[i * cols + j] = Add(frame.MultiplyPoint3x4(q), Unit(frame.MultiplyVector(n), Vector3.up),
                            new Vector2(j / (float)lon * girth / uvScale, (i + 1) / (float)lat * radii.y * 2f / uvScale), w);
                    }
                for (int i = 0; i < rows - 1; i++)
                    for (int j = 0; j < lon; j++)
                    {
                        int a = idx[i * cols + j], b = idx[i * cols + j + 1], c = idx[(i + 1) * cols + j + 1], d = idx[(i + 1) * cols + j];
                        var outward = m_N[a] + m_N[c];
                        Tri(sub, a, b, c, outward); Tri(sub, a, c, d, outward);
                    }
                int sp = Add(frame.MultiplyPoint3x4(south), Unit(frame.MultiplyVector(Vector3.down), Vector3.down), new Vector2(0f, 0f), w);
                int np = Add(frame.MultiplyPoint3x4(north), Unit(frame.MultiplyVector(Vector3.up), Vector3.up), new Vector2(0f, radii.y * 2f / uvScale), w);
                for (int j = 0; j < lon; j++)
                {
                    Tri(sub, sp, idx[j], idx[j + 1], m_N[sp]);
                    Tri(sub, np, idx[(rows - 1) * cols + j + 1], idx[(rows - 1) * cols + j], m_N[np]);
                }
            }

            /// <summary>A flat strap (top face and two edges) along a path over a body; outward points away from the body.</summary>
            public void Band(Matrix4x4 frame, IList<Vector3> path, IList<Vector3> outward, float width, float thickness, bool closed, int sub)
            {
                var w = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                int n = path.Count;
                var tl = new int[n]; var tr = new int[n]; var el = new int[n]; var etl = new int[n]; var er = new int[n]; var etr = new int[n];
                var outs = new Vector3[n]; var sides = new Vector3[n];
                float run = 0f;
                for (int k = 0; k < n; k++)
                {
                    int k0 = closed ? (k + n - 1) % n : Mathf.Max(k - 1, 0), k1 = closed ? (k + 1) % n : Mathf.Min(k + 1, n - 1);
                    var t = Unit(path[k1] - path[k0], Vector3.forward);
                    var o = outward[k] - Vector3.Dot(outward[k], t) * t;
                    o = Unit(o, Vector3.up);
                    var side = Unit(Vector3.Cross(t, o), Vector3.right) * (width * 0.5f);
                    outs[k] = o; sides[k] = side;
                    if (k > 0) run += Vector3.Distance(path[k], path[k - 1]);
                    var top = path[k] + o * thickness;
                    var nTop = Unit(frame.MultiplyVector(o), Vector3.up);
                    var nL = Unit(frame.MultiplyVector(-side), Vector3.left);
                    var nR = Unit(frame.MultiplyVector(side), Vector3.right);
                    tl[k] = Add(frame.MultiplyPoint3x4(top - side), nTop, new Vector2(0f, run * 10f), w);
                    tr[k] = Add(frame.MultiplyPoint3x4(top + side), nTop, new Vector2(1f, run * 10f), w);
                    etl[k] = Add(frame.MultiplyPoint3x4(top - side), nL, new Vector2(0f, run * 10f), w);
                    el[k] = Add(frame.MultiplyPoint3x4(path[k] - side), nL, new Vector2(0f, run * 10f), w);
                    etr[k] = Add(frame.MultiplyPoint3x4(top + side), nR, new Vector2(1f, run * 10f), w);
                    er[k] = Add(frame.MultiplyPoint3x4(path[k] + side), nR, new Vector2(1f, run * 10f), w);
                }
                int segs = closed ? n : n - 1;
                for (int k = 0; k < segs; k++)
                {
                    int k1 = (k + 1) % n;
                    var o = frame.MultiplyVector(outs[k]);
                    var sl = frame.MultiplyVector(-sides[k]);
                    var sr = frame.MultiplyVector(sides[k]);
                    Tri(sub, tl[k], tr[k], tr[k1], o); Tri(sub, tl[k], tr[k1], tl[k1], o);
                    Tri(sub, el[k], etl[k], etl[k1], sl); Tri(sub, el[k], etl[k1], el[k1], sl);
                    Tri(sub, er[k], etr[k], etr[k1], sr); Tri(sub, er[k], etr[k1], er[k1], sr);
                }
            }

            /// <summary>Turns the right hand into the left: mirror X, keep normals outward, flip the winding.</summary>
            public void MirrorX()
            {
                for (int i = 0; i < m_V.Count; i++)
                {
                    m_V[i] = new Vector3(-m_V[i].x, m_V[i].y, m_V[i].z);
                    m_N[i] = new Vector3(-m_N[i].x, m_N[i].y, m_N[i].z);
                }
                foreach (var t in m_T)
                    for (int i = 0; i < t.Count; i += 3) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; }
            }

            public Mesh ToMesh(string name, bool skinned = true)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(m_V);
                mesh.SetNormals(m_N);
                mesh.SetUVs(0, m_UV);
                int subs = m_T.Length;
                while (subs > 1 && m_T[subs - 1].Count == 0) subs--; // no empty trailing submeshes
                mesh.subMeshCount = subs;
                for (int k = 0; k < subs; k++) mesh.SetTriangles(m_T[k], k);
                if (skinned) mesh.boneWeights = m_W.ToArray();
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }

    /// <summary>
    /// Procedural skin maps for the hand, generated once: albedo (warm medium-brown back, lighter palm and finger pads, darker
    /// knuckles) and a tangent-space normal map (extensor tendons, veins, knuckle wrinkles, palm and wrist creases, pores).
    /// Laid out to match HandMesh's skin UVs: the palm loft in u 0..0.5, the phalanges in 0.5..0.75, the thumb in 0.75..1.
    /// </summary>
    static class HandSkin
    {
        public const int Width = 1024, Height = 256;
        public static Texture2D Albedo, Normal;

        static readonly Color Dorsal = new Color(0.7f, 0.52f, 0.4f), Volar = new Color(0.82f, 0.66f, 0.56f);
        static readonly float[] TendonWrist = { -0.011f, -0.004f, 0.004f, 0.011f };
        static readonly Vector2[][] Veins =
        {
            new[] { new Vector2(-0.002f, -0.01f), new Vector2(-0.006f, 0.015f), new Vector2(-0.012f, 0.035f), new Vector2(-0.016f, 0.055f), new Vector2(-0.018f, 0.072f) },
            new[] { new Vector2(-0.006f, 0.015f), new Vector2(0.004f, 0.03f), new Vector2(0.012f, 0.045f), new Vector2(0.016f, 0.062f) },
            new[] { new Vector2(0.012f, -0.008f), new Vector2(0.017f, 0.012f), new Vector2(0.022f, 0.03f), new Vector2(0.024f, 0.05f) },
            new[] { new Vector2(-0.016f, 0.055f), new Vector2(-0.004f, 0.06f), new Vector2(0.012f, 0.058f), new Vector2(0.024f, 0.05f) },
        };
        // Palm creases (x, z on the palm side): heart line, head line, life line round the ball of the thumb.
        static readonly Vector2[][] Creases =
        {
            new[] { new Vector2(0.042f, 0.068f), new Vector2(0.025f, 0.072f), new Vector2(0.01f, 0.077f), new Vector2(-0.006f, 0.079f), new Vector2(-0.016f, 0.083f) },
            new[] { new Vector2(-0.036f, 0.064f), new Vector2(-0.02f, 0.058f), new Vector2(-0.005f, 0.054f), new Vector2(0.012f, 0.048f), new Vector2(0.026f, 0.042f) },
            new[] { new Vector2(-0.036f, 0.064f), new Vector2(-0.03f, 0.05f), new Vector2(-0.024f, 0.035f), new Vector2(-0.02f, 0.02f), new Vector2(-0.016f, 0.006f) },
        };
        static readonly float[] CreaseDepth = { 0.00055f, 0.0005f, 0.00055f };

        public static void Ensure()
        {
            if (Albedo != null) return;
            int w = Width, h = Height;
            var height = new float[w * h];
            var albedo = new Color32[w * h];
            float zMin = HandMesh.Palm[0, 0], zMax = HandMesh.Palm[HandMesh.Palm.GetLength(0) - 1, 0];
            for (int py = 0; py < h; py++)
            {
                float v = (py + 0.5f) / h;
                // This row's palm cross-section (v -> z through the same bands the mesh uses).
                float z = zMin + (zMax - zMin) * Mathf.Clamp01((v - HandMesh.DomePalm) / (1f - 2f * HandMesh.DomePalm));
                PalmRing(z, out float cx, out float cy, out float ra, out float rb);
                for (int px = 0; px < w; px++)
                {
                    float u = (px + 0.5f) / w;
                    float hh;
                    Color c;
                    if (u < 0.5f) PalmTexel(Wrap((u - HandMesh.PalmU.x) / (HandMesh.PalmU.y - HandMesh.PalmU.x)), z, cx, cy, ra, rb, out hh, out c);
                    else if (u < 0.75f) PhalanxTexel(Wrap((u - HandMesh.FingerU.x) / (HandMesh.FingerU.y - HandMesh.FingerU.x)), v, false, out hh, out c);
                    else PhalanxTexel(Wrap((u - HandMesh.ThumbU.x) / (HandMesh.ThumbU.y - HandMesh.ThumbU.x)), v, true, out hh, out c);
                    // Pores and fine grain; a little blotchiness in the colour.
                    float g = Noise(px * 0.9f, py * 0.9f, 7) * 0.65f + Noise(px * 2.3f, py * 2.3f, 11) * 0.35f;
                    hh += (g - 0.5f) * 0.00004f;
                    float tone = 0.97f + 0.06f * Noise(px * 0.08f, py * 0.08f, 3);
                    height[py * w + px] = hh;
                    albedo[py * w + px] = new Color(c.r * tone, c.g * tone, c.b * tone, 1f);
                }
            }

            // Tangent-space normals from the height field (metres per texel differ between the palm and the phalanges).
            var normal = new Color32[w * h];
            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    bool palm = px < w / 2;
                    float mx = palm ? 0.25f / (w * 0.5f) : 0.056f / (w * 0.25f);
                    float my = palm ? 0.135f / ((1f - 2f * HandMesh.DomePalm) * h) : 0.03f / ((1f - 2f * HandMesh.DomeFinger) * h);
                    int x0 = Mathf.Max(px - 1, 0), x1 = Mathf.Min(px + 1, w - 1), y0 = Mathf.Max(py - 1, 0), y1 = Mathf.Min(py + 1, h - 1);
                    float dx = (height[py * w + x1] - height[py * w + x0]) / ((x1 - x0) * mx);
                    float dy = (height[y1 * w + px] - height[y0 * w + px]) / ((y1 - y0) * my);
                    float inv = 1f / Mathf.Sqrt(dx * dx + dy * dy + 1f);
                    normal[py * w + px] = new Color(-dx * inv * 0.5f + 0.5f, -dy * inv * 0.5f + 0.5f, inv * 0.5f + 0.5f, 1f);
                }

            Albedo = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { name = "TS_HandSkin_Albedo", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            Albedo.SetPixels32(albedo);
            Albedo.Apply(true, true);
            Normal = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { name = "TS_HandSkin_Normal", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            Normal.SetPixels32(normal);
            Normal.Apply(true, true);
        }

        static float Wrap(float u) => u - Mathf.Floor(u);

        static void PalmRing(float z, out float cx, out float cy, out float a, out float b)
        {
            var p = HandMesh.Palm;
            int last = p.GetLength(0) - 1;
            int i = 0;
            while (i < last - 1 && z > p[i + 1, 0]) i++;
            float t = Mathf.Clamp01((z - p[i, 0]) / (p[i + 1, 0] - p[i, 0]));
            cx = Mathf.Lerp(p[i, 1], p[i + 1, 1], t); cy = Mathf.Lerp(p[i, 2], p[i + 1, 2], t);
            a = Mathf.Lerp(p[i, 3], p[i + 1, 3], t); b = Mathf.Lerp(p[i, 4], p[i + 1, 4], t);
        }

        /// <summary>Back of the hand: tendons fanning to the knuckles, veins and their arch, darker knuckles, wrist wrinkles.
        /// Palm: the three main creases, wrist creases, a lighter tone.</summary>
        static void PalmTexel(float u, float z, float cx, float cy, float a, float b, out float h, out Color col)
        {
            float t = 2f * Mathf.PI * u;
            float c = Mathf.Cos(t), s = Mathf.Sin(t);
            float x = cx + a * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / HandMesh.PalmExponent);
            float dors = Smooth(0.15f, 0.55f, s), vol = Smooth(0.15f, 0.55f, -s);
            h = 0f;
            col = Color.Lerp(Dorsal, Volar, 0.6f * vol);
            if (dors > 0f)
            {
                float zm = Smooth(0.004f, 0.02f, z) * (1f - Smooth(0.074f, 0.09f, z));
                float fan = Smooth(-0.005f, 0.085f, z);
                for (int f = 0; f < 4; f++)
                {
                    float d = (x - Mathf.Lerp(TendonWrist[f], HandMesh.Mcp[f].x, fan)) / 0.0028f;
                    h += 0.0006f * Mathf.Exp(-d * d) * zm * dors;
                }
                foreach (var vein in Veins)
                {
                    if (!Near(vein, x, z, 0.007f)) continue;
                    float d = PolyDist(x, z, vein) / 0.0021f;
                    float g = Mathf.Exp(-d * d) * dors;
                    h += 0.0005f * g;
                    col *= 1f - 0.06f * g;
                }
                for (int f = 0; f < 4; f++)
                {
                    float dx = (x - HandMesh.Mcp[f].x) / 0.008f, dz = (z - HandMesh.Mcp[f].z + 0.004f) / 0.008f;
                    col *= 1f - 0.12f * Mathf.Exp(-dx * dx - dz * dz) * dors;
                }
                float wrinkle = 1f - Smooth(0.02f, 0.03f, Mathf.Abs(x));
                for (int k = 0; k < 2; k++)
                {
                    float d = (z - (k == 0 ? -0.004f : -0.012f)) / 0.0009f;
                    h -= 0.00016f * Mathf.Exp(-d * d) * dors * wrinkle;
                }
            }
            if (vol > 0f)
            {
                for (int k = 0; k < Creases.Length; k++)
                {
                    if (!Near(Creases[k], x, z, 0.004f)) continue;
                    float d = PolyDist(x, z, Creases[k]) / 0.0009f;
                    float g = Mathf.Exp(-d * d) * vol;
                    h -= CreaseDepth[k] * g;
                    col *= 1f - 0.13f * g;
                }
                for (int k = 0; k < 2; k++)
                {
                    float d = (z - (k == 0 ? 0.002f : -0.006f)) / 0.0008f;
                    float g = Mathf.Exp(-d * d) * vol;
                    h -= 0.0004f * g;
                    col *= 1f - 0.08f * g;
                }
            }
            col.a = 1f;
        }

        /// <summary>One phalanx (joints at v = DomeFinger and 1 - DomeFinger): wrinkle arcs over each knuckle on the back, a
        /// pair of flexion creases at each joint on the palm side, darker skin over the knuckles, lighter pads.</summary>
        static void PhalanxTexel(float u, float v, bool thumb, out float h, out Color col)
        {
            float s = Mathf.Sin(2f * Mathf.PI * u);
            float dors = Smooth(0.2f, 0.6f, s), vol = Smooth(0.2f, 0.6f, -s);
            float du = u - 0.25f;
            if (du > 0.5f) du -= 1f;
            col = Color.Lerp(Dorsal, Volar, 0.65f * vol);
            h = 0f;
            for (int e = 0; e < 2; e++)
            {
                float vj = e == 0 ? HandMesh.DomeFinger : 1f - HandMesh.DomeFinger, side = e == 0 ? 1f : -1f;
                if (dors > 0f)
                {
                    float arc = 0.05f * (du / 0.12f) * (du / 0.12f);
                    float across = Mathf.Exp(-(du / 0.13f) * (du / 0.13f));
                    int lines = thumb ? 2 : 3;
                    for (int k = 0; k < lines; k++)
                    {
                        float d = (v - (vj + side * (0.025f + 0.03f * k + arc))) / 0.012f;
                        h -= (0.00022f - 0.00004f * k) * Mathf.Exp(-d * d) * dors * across;
                    }
                    float dk = (v - vj) / 0.07f;
                    col *= 1f - 0.1f * Mathf.Exp(-dk * dk) * dors;
                }
                if (vol > 0f)
                {
                    float acrossV = (Mathf.Abs(du) - 0.5f) / 0.16f;
                    for (int k = 0; k < 2; k++)
                    {
                        float d = (v - vj - side * 0.02f - (k == 0 ? 0.012f : -0.012f)) / 0.007f;
                        float g = Mathf.Exp(-d * d) * vol * Mathf.Exp(-acrossV * acrossV);
                        h -= 0.0004f * g;
                        col *= 1f - 0.1f * g;
                    }
                }
            }
            col.a = 1f;
        }

        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static bool Near(Vector2[] pts, float x, float z, float margin)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var p in pts) { x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.y); z1 = Mathf.Max(z1, p.y); }
            return x > x0 - margin && x < x1 + margin && z > z0 - margin && z < z1 + margin;
        }

        static float PolyDist(float x, float z, Vector2[] pts)
        {
            float best = float.MaxValue;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i];
                var d = pts[i + 1] - a;
                float t = Mathf.Clamp01(((x - a.x) * d.x + (z - a.y) * d.y) / (d.x * d.x + d.y * d.y));
                float ex = x - a.x - t * d.x, ez = z - a.y - t * d.y;
                best = Mathf.Min(best, ex * ex + ez * ez);
            }
            return Mathf.Sqrt(best);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                n = (n ^ (n >> 13)) * 1274126177u;
                n ^= n >> 16;
                return (n & 0xffff) / 65535f;
            }
        }

        internal static float Noise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }
    }

    /// <summary>
    /// The small atlas for the trim material: nail strip (pale lunula, pink-beige plate, cream free edge), leather strap with
    /// stitching, brushed steel, a navy dial with cream hour markers, cream hands, and a pearl cuff button. The emission map
    /// lifts everything by 5 % of its colour and makes the dial markers and hands glow faintly in the dark.
    /// </summary>
    static class HandTrim
    {
        const int Size = 256;
        public static Texture2D Albedo, Emission;
        public static readonly Rect LeatherRect = new Rect(0.27f, 0.03f, 0.21f, 0.44f);
        public static readonly Vector2 SteelCentre = new Vector2(0.375f, 0.75f);
        public static readonly Vector2 DialCentre = new Vector2(0.75f, 0.75f);
        public const float DialRadius = 0.235f;
        public static readonly Vector2 LumeCentre = new Vector2(0.625f, 0.125f);
        public static readonly Vector2 ButtonCentre = new Vector2(0.875f, 0.25f);
        public const float ButtonRadius = 0.1f;

        /// <summary>Across (0..1) and along (0 cuticle .. 1 free edge) a nail, into the nail strip.</summary>
        public static Vector2 NailUv(float across, float along) => new Vector2(0.02f + 0.21f * across, 0.02f + 0.96f * along);

        public static void Ensure()
        {
            if (Albedo != null) return;
            var albedo = new Color32[Size * Size];
            var glow = new Color32[Size * Size];
            for (int py = 0; py < Size; py++)
                for (int px = 0; px < Size; px++)
                {
                    float u = (px + 0.5f) / Size, v = (py + 0.5f) / Size;
                    float lume = 0f;
                    Color c = Texel(u, v, px, py, ref lume);
                    albedo[py * Size + px] = c;
                    var lin = c.linear;
                    glow[py * Size + px] = new Color(lin.r * 0.05f + lume * 0.26f, lin.g * 0.05f + lume * 0.3f, lin.b * 0.05f + lume * 0.22f, 1f);
                }
            Albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false) { name = "TS_HandTrim_Albedo", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            Albedo.SetPixels32(albedo);
            Albedo.Apply(true, true);
            Emission = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true) { name = "TS_HandTrim_Emission", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            Emission.SetPixels32(glow);
            Emission.Apply(true, true);
        }

        static Color Texel(float u, float v, int px, int py, ref float lume)
        {
            if (u < 0.25f)
            {
                // Nail strip: v runs from the cuticle (0) to the free edge (1).
                float a = Mathf.Clamp01((u - 0.02f) / 0.21f), s = Mathf.Clamp01((v - 0.02f) / 0.96f);
                var c = new Color(0.8f, 0.62f, 0.55f) * (0.97f + 0.05f * HandSkin.Noise(px * 0.15f, py * 0.02f, 21)); // faint lengthwise ridges
                c = Color.Lerp(new Color(0.87f, 0.75f, 0.69f), c, Mathf.Clamp01((s - 0.06f) / 0.14f));               // lunula
                c = Color.Lerp(c, new Color(0.6f, 0.43f, 0.34f), Mathf.Clamp01(1f - Mathf.Abs(s - 0.035f) / 0.03f));   // cuticle
                c = Color.Lerp(c, new Color(0.93f, 0.89f, 0.8f), Mathf.Clamp01((s - 0.8f) / 0.06f));                   // free edge
                c *= 1f - 0.12f * Mathf.Clamp01((Mathf.Abs(a - 0.5f) - 0.36f) / 0.14f);                               // nail folds
                c.a = 1f;
                return c;
            }
            if (u < 0.5f)
            {
                if (v < 0.5f)
                {
                    // Leather strap with stitching along both edges.
                    var c = new Color(0.25f, 0.15f, 0.09f) * (0.88f + 0.2f * HandSkin.Noise(px * 0.6f, py * 0.6f, 31));
                    float across = Mathf.Clamp01((v - LeatherRect.yMin) / LeatherRect.height);
                    bool stitchRow = Mathf.Abs(across - 0.15f) < 0.035f || Mathf.Abs(across - 0.85f) < 0.035f;
                    if (stitchRow && (px / 3) % 2 == 0) c = new Color(0.66f, 0.56f, 0.42f);
                    c.a = 1f;
                    return c;
                }
                var steel = new Color(0.72f, 0.73f, 0.75f) * (0.9f + 0.14f * HandSkin.Noise(px * 0.05f, py * 1.7f, 41)); // brushed
                steel.a = 1f;
                return steel;
            }
            if (v >= 0.5f)
            {
                // Dial: navy face, chapter ring, twelve cream hour markers (12 at +v, 3 at +u), steel bezel outside.
                float dx = u - DialCentre.x, dy = v - DialCentre.y;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > DialRadius) return new Color(0.74f, 0.75f, 0.77f, 1f);
                var c = r > 0.2f ? new Color(0.14f, 0.15f, 0.2f) : new Color(0.07f, 0.08f, 0.12f);
                float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;          // clockwise from 12
                float off = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 30f) * 30f)) * Mathf.Deg2Rad * r;
                bool major = Mathf.Abs(Mathf.DeltaAngle(ang, 0f)) < 3f;
                if (r > 0.155f && r < 0.212f && off < (major ? 0.011f : 0.0065f)) { c = new Color(0.9f, 0.9f, 0.8f); lume = 1f; }
                c.a = 1f;
                return c;
            }
            if (u < 0.75f)
            {
                lume = v < 0.25f ? 1f : 0f;
                return v < 0.25f ? new Color(0.9f, 0.9f, 0.82f, 1f) : new Color(0.72f, 0.73f, 0.75f, 1f);
            }
            {
                // Pearl button with a soft rim and four holes.
                float dx = u - ButtonCentre.x, dy = v - ButtonCentre.y;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                var c = new Color(0.9f, 0.89f, 0.86f) * (r > ButtonRadius * 0.75f ? 0.9f : 1f);
                for (int k = 0; k < 4; k++)
                {
                    float hx = dx - (k % 2 == 0 ? -0.024f : 0.024f), hy = dy - (k < 2 ? -0.024f : 0.024f);
                    if (hx * hx + hy * hy < 0.011f * 0.011f) c = new Color(0.36f, 0.34f, 0.31f);
                }
                c.a = 1f;
                return c;
            }
        }
    }
}
