using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwelfthStamp
{
    /// <summary>
    /// The old shopkeeper of the Marrakech memory, the first person the player meets. One skinned mesh (eight materials)
    /// over a small skeleton, sculpted from parametric surfaces: a camel wool djellaba with folds, its pointed hood hanging
    /// down the back and a cream braid down the front; wide sleeves over arms that read as a body inside the robe; old
    /// hands with fingers; a cream crocheted kufi; a weathered face (cheekbones, an aquiline nose, deep-set eyes under
    /// heavy lids, bushy brows) with a full grey beard and moustache; half-moon reading glasses low on the nose; a pencil
    /// behind his right ear and a loupe on a cord. He breathes and shifts his weight, rests his hands on the counter,
    /// follows the player with his head and eyes (now and then glancing away), blinks, opens a hand in welcome
    /// (<see cref="Gesture"/>), nods (<see cref="Nod"/>) and lifts the glasses case to offer it (<see cref="Offer"/>).
    /// Character space: feet at the origin, facing +Z, +X his right. He keeps between the counter's back edge (0.24 m in
    /// front of him, top at 1.04 m) and the back-wall shelving (0.165 m behind him).
    /// </summary>
    public class Shopkeeper : MonoBehaviour
    {
        // ------------------------------------------------------------------ proportions
        const float UpperArm = 0.29f, Forearm = 0.27f, PalmCentre = -0.045f;
        const float WelcomeSeconds = 2.4f, NodSeconds = 1.1f;
        /// <summary>How long <see cref="Offer"/> takes with an item (reach, lift, present, put back).</summary>
        public const float OfferSeconds = 4.6f;
        const float OfferEmptySeconds = 3.4f;
        static readonly Vector3 HeadCentre = new Vector3(0f, 1.632f, 0.035f);
        static readonly Vector3 HeadRadii = new Vector3(0.077f, 0.104f, 0.09f);
        static readonly Vector3 PresentAt = new Vector3(0.02f, 1.27f, 0.44f); // where he holds a thing out to the player

        class Arm
        {
            public float Side;                       // +1 right, -1 left
            public Transform Shoulder, Elbow, Hand;
            public readonly Transform[] Prox = new Transform[5], Dist = new Transform[5]; // index, middle, ring, little, thumb
            public Vector3 ShoulderHome;
            public Vector3 Reached;                  // where the palm actually got to (character space)
        }

        struct HandPose
        {
            public Vector3 Pos, Palm, Finger;        // palm centre, palm normal, finger direction (character space)
            public float Curl, Spread;
            public HandPose(Vector3 pos, Vector3 palm, Vector3 finger, float curl, float spread)
            {
                Pos = pos; Palm = palm.normalized; Finger = finger.normalized; Curl = curl; Spread = spread;
            }
            public static HandPose Lerp(HandPose a, HandPose b, float t) => t <= 0f ? a : t >= 1f ? b : new HandPose(
                Vector3.Lerp(a.Pos, b.Pos, t), Vector3.Slerp(a.Palm, b.Palm, t), Vector3.Slerp(a.Finger, b.Finger, t),
                Mathf.Lerp(a.Curl, b.Curl, t), Mathf.Lerp(a.Spread, b.Spread, t));
        }

        static readonly HandPose RestR = new HandPose(new Vector3(0.205f, 1.06f, 0.33f), new Vector3(0.05f, -1f, 0.05f), new Vector3(-0.12f, -0.08f, 1f), 0.2f, 0.1f);
        static readonly HandPose RestL = new HandPose(new Vector3(-0.195f, 1.06f, 0.32f), new Vector3(-0.05f, -1f, 0.05f), new Vector3(0.12f, -0.08f, 1f), 0.22f, 0.1f);
        static readonly HandPose WelcomeR = new HandPose(new Vector3(0.37f, 1.32f, 0.3f), new Vector3(-0.25f, 0.82f, 0.5f), new Vector3(0.6f, 0.15f, 0.8f), 0.05f, 0.5f);
        static readonly HandPose WelcomeL = new HandPose(new Vector3(-0.27f, 1.17f, 0.33f), new Vector3(0.2f, 0.85f, 0.45f), new Vector3(-0.5f, 0.1f, 0.86f), 0.1f, 0.35f);
        static readonly HandPose PresentR = new HandPose(new Vector3(0.075f, 1.25f, 0.42f), new Vector3(-0.15f, 0.95f, 0.25f), new Vector3(0.15f, 0.15f, 1f), 0.12f, 0.2f);
        static readonly HandPose PresentL = new HandPose(new Vector3(-0.075f, 1.25f, 0.42f), new Vector3(0.15f, 0.95f, 0.25f), new Vector3(-0.15f, 0.15f, 1f), 0.12f, 0.2f);
        static readonly Vector3[] GlanceAt = { new Vector3(0.1f, 1.05f, 0.45f), new Vector3(0.9f, 1.15f, 0.5f), new Vector3(-0.3f, 1.6f, 4f) };

        readonly List<Transform> m_Bones = new List<Transform>();
        Transform m_Hips, m_Spine, m_Chest, m_Breath, m_Neck, m_Head;
        readonly Transform[] m_Eye = new Transform[2], m_Lid = new Transform[2], m_Brow = new Transform[2];
        Vector3[] m_BrowHome = new Vector3[2];
        readonly Arm[] m_Arms = { new Arm { Side = -1f }, new Arm { Side = 1f } };
        Vector3 m_HipsHome;

        float m_WelcomeT = -1f, m_NodT = -1f, m_OfferT = -1f, m_OfferLength;
        bool m_OfferNodded;
        Transform[] m_Items;
        Vector3[] m_ItemHome;
        Quaternion[] m_ItemHomeRot;
        Vector3 m_GripOffset, m_SupportOffset, m_Slide, m_PresentDelta;
        float m_Yaw, m_Pitch, m_YawVel, m_PitchVel;
        Vector2 m_EyeAim;
        float m_NextBlink = 1.5f, m_BlinkT = -1f, m_NextGlance = 6f, m_GlanceT = -1f;
        int m_Glance;
        readonly System.Random m_Rnd = new System.Random(1312);

        /// <summary>True while he is offering something (the items are lifted off the counter until it ends).</summary>
        public bool Busy => m_OfferT >= 0f;

        public static Shopkeeper Create(Transform parent)
        {
            var go = new GameObject("Shopkeeper");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<Shopkeeper>();
            s.Build();
            return s;
        }

        /// <summary>A welcoming open hand while speaking: the right hand lifts off the counter, palm up, and settles back.</summary>
        public void Gesture()
        {
            if (m_OfferT < 0f) m_WelcomeT = 0f;
        }

        /// <summary>A small double nod.</summary>
        public void Nod() => m_NodT = 0f;

        /// <summary>
        /// He reaches across the counter, lifts <paramref name="items"/> (moved together, as one group: pass the glasses case
        /// and the glasses lying in it), holds them out to the player for a moment with a nod, and sets them back exactly
        /// where they were (<see cref="OfferSeconds"/>). With no items he offers both open palms instead (3.4 s).
        /// </summary>
        public void Offer(params Transform[] items)
        {
            RestoreItems();
            m_WelcomeT = -1f;
            m_OfferNodded = false;
            m_Slide = Vector3.zero;
            var list = new List<Transform>();
            if (items != null) foreach (var t in items) if (t != null) list.Add(t);
            m_Items = list.Count > 0 ? list.ToArray() : null;
            m_OfferLength = m_Items != null ? OfferSeconds : OfferEmptySeconds;
            if (m_Items != null)
            {
                m_ItemHome = new Vector3[m_Items.Length];
                m_ItemHomeRot = new Quaternion[m_Items.Length];
                for (int i = 0; i < m_Items.Length; i++) { m_ItemHome[i] = m_Items[i].position; m_ItemHomeRot[i] = m_Items[i].rotation; }
                // Grip from above near its right end, support from below near its left end (character directions).
                var b = new Bounds(m_Items[0].position, Vector3.one * 0.04f);
                bool first = true;
                foreach (var r in m_Items[0].GetComponentsInChildren<Renderer>())
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                var right = transform.right;
                m_GripOffset = b.center + Vector3.up * (b.extents.y + 0.012f) + right * 0.035f - m_Items[0].position;
                m_SupportOffset = b.center - Vector3.up * (b.extents.y + 0.016f) - right * 0.04f - m_Items[0].position;
                m_PresentDelta = transform.TransformPoint(PresentAt) - b.center;
            }
            m_OfferT = 0f;
        }

        void OnDisable() => RestoreItems();

        void RestoreItems()
        {
            if (m_Items == null) return;
            for (int i = 0; i < m_Items.Length; i++)
                if (m_Items[i] != null) { m_Items[i].position = m_ItemHome[i]; m_Items[i].rotation = m_ItemHomeRot[i]; }
            m_Items = null;
            m_OfferT = -1f;
        }

        // ================================================================== build
        Transform NewBone(string name, Transform parent, Vector3 charPos, Quaternion? charRot = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = transform.TransformPoint(charPos);
            t.rotation = transform.rotation * (charRot ?? Quaternion.identity);
            m_Bones.Add(t);
            return t;
        }

        int Idx(Transform t) => m_Bones.IndexOf(t);
        Matrix4x4 CharOf(Transform bone) => transform.worldToLocalMatrix * bone.localToWorldMatrix;

        static BoneWeight W1(int a) => new BoneWeight { boneIndex0 = a, weight0 = 1f };

        static BoneWeight W2(int a, int b, float tb)
        {
            tb = Mathf.Clamp01(tb);
            if (tb <= 0.001f) return W1(a);
            if (tb >= 0.999f) return W1(b);
            return tb > 0.5f
                ? new BoneWeight { boneIndex0 = b, weight0 = tb, boneIndex1 = a, weight1 = 1f - tb }
                : new BoneWeight { boneIndex0 = a, weight0 = 1f - tb, boneIndex1 = b, weight1 = tb };
        }

        void Build()
        {
            // Pale tints: the fabric textures multiply.
            var robe = Mats.Lit(new Color(0.78f, 0.6f, 0.47f), 0.12f, 0f, "fabric_linen");
            var cream = Mats.Lit(new Color(1f, 0.96f, 0.88f), 0.2f, 0f, "fabric_linen");
            var skin = Mats.Lit(new Color(0.68f, 0.49f, 0.38f), 0.36f);
            var hair = Mats.Lit(new Color(0.76f, 0.74f, 0.7f), 0.26f);
            var dark = Mats.Lit(new Color(0.07f, 0.055f, 0.045f), 0.72f);
            var white = Mats.Lit(new Color(0.74f, 0.7f, 0.63f), 0.62f);
            var brass = Mats.Lit(new Color(0.86f, 0.68f, 0.38f), 0.75f, 0.9f);
            var glass = new Material(Mats.Glass(new Color(0.86f, 0.92f, 0.96f, 0.14f))) { name = "ShopkeeperLens" };
            glass.SetShaderPassEnabled("ShadowCaster", false);
            var mats = new[] { robe, cream, skin, hair, dark, white, brass, glass };

            BuildSkeleton();
            var fm = new FigureMesh(mats.Length);
            BuildRobe(fm);
            BuildHead(fm);
            for (int a = 0; a < 2; a++) BuildArm(fm, m_Arms[a]);
            BuildAccessories(fm);

            var go = new GameObject("Figure");
            go.transform.SetParent(transform, false);
            var binds = new Matrix4x4[m_Bones.Count];
            for (int i = 0; i < binds.Length; i++) binds[i] = m_Bones[i].worldToLocalMatrix * go.transform.localToWorldMatrix;
            var mesh = fm.ToMesh("Shopkeeper", binds);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = m_Bones.ToArray();
            smr.rootBone = m_Hips;
            smr.sharedMaterials = mats;
            smr.quality = SkinQuality.Bone2;
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(new Vector3(0f, 0f, 0.2f), new Vector3(1.4f, 2.2f, 1.5f));
            smr.shadowCastingMode = ShadowCastingMode.On;
            Animate(0f, 0f, transform.TransformPoint(new Vector3(0f, 1.6f, 2.5f)));
        }

        void BuildSkeleton()
        {
            m_Bones.Clear();
            m_Hips = NewBone("Hips", transform, new Vector3(0f, 0.95f, 0f));
            m_HipsHome = m_Hips.localPosition;
            m_Spine = NewBone("Spine", m_Hips, new Vector3(0f, 0.99f, 0.005f));
            m_Chest = NewBone("Chest", m_Spine, new Vector3(0f, 1.22f, 0.02f));
            m_Breath = NewBone("Breath", m_Chest, new Vector3(0f, 1.26f, 0.03f));
            m_Neck = NewBone("Neck", m_Chest, new Vector3(0f, 1.462f, 0.018f));
            m_Head = NewBone("Head", m_Neck, new Vector3(0f, 1.548f, 0.026f));
            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? -1f : 1f;
                var eye = EyeCentre(sx);
                m_Eye[s] = NewBone(sx < 0 ? "EyeL" : "EyeR", m_Head, eye);
                m_Lid[s] = NewBone(sx < 0 ? "LidL" : "LidR", m_Head, eye);
                m_Brow[s] = NewBone(sx < 0 ? "BrowL" : "BrowR", m_Head, HeadCentre + OnFace(sx * 0.033f, 0.031f, 0.003f));
                m_BrowHome[s] = m_Brow[s].localPosition;
            }
            foreach (var arm in m_Arms)
            {
                string n = arm.Side < 0 ? "L" : "R";
                arm.Shoulder = NewBone("Shoulder" + n, m_Chest, new Vector3(arm.Side * 0.172f, 1.385f, 0.03f));
                arm.ShoulderHome = arm.Shoulder.localPosition;
                arm.Elbow = NewBone("Elbow" + n, arm.Shoulder, new Vector3(arm.Side * 0.172f, 1.385f - UpperArm, 0.03f));
                arm.Hand = NewBone("Hand" + n, arm.Elbow, new Vector3(arm.Side * 0.172f, 1.385f - UpperArm - Forearm, 0.03f));
                // The hand's frame: fingers along -Y, palm toward +Z, thumb toward +X on the right (mirrored on the left).
                for (int f = 0; f < 5; f++)
                {
                    var (root, dir, prox) = FingerRoot(f);
                    root.x *= arm.Side; dir.x *= arm.Side;
                    var rot = BoneRot(dir, Vector3.forward);
                    var charRoot = new Vector3(arm.Side * 0.172f, 1.385f - UpperArm - Forearm, 0.03f) + root;
                    arm.Prox[f] = NewBone("Finger" + n + f, arm.Hand, charRoot, rot);
                    arm.Dist[f] = NewBone("FingerTip" + n + f, arm.Prox[f], charRoot + dir * prox, rot);
                }
            }
        }

        /// <summary>Finger roots in the hand's frame (right hand): position, direction and the first phalanx length.</summary>
        static (Vector3 root, Vector3 dir, float prox) FingerRoot(int f)
        {
            switch (f)
            {
                case 0: return (new Vector3(0.0275f, -0.088f, -0.002f), Vector3.down, 0.041f);
                case 1: return (new Vector3(0.0095f, -0.091f, -0.002f), Vector3.down, 0.045f);
                case 2: return (new Vector3(-0.0095f, -0.089f, -0.002f), Vector3.down, 0.042f);
                case 3: return (new Vector3(-0.027f, -0.083f, -0.002f), new Vector3(-0.06f, -1f, 0f).normalized, 0.033f);
                default: return (new Vector3(0.036f, -0.026f, 0.007f), new Vector3(0.5f, -0.72f, 0.48f).normalized, 0.036f);
            }
        }

        static float DistLen(int f) => f == 0 ? 0.043f : f == 1 ? 0.047f : f == 2 ? 0.044f : f == 3 ? 0.034f : 0.032f;
        static float FingerRadius(int f) => f == 4 ? 0.0108f : f == 3 ? 0.0082f : 0.0093f;

        // ------------------------------------------------------------------ the robe
        // Djellaba profile (y: half-width, half-depth, centre z): a flared hem, a slight belly, broad sloping shoulders.
        static readonly float[,] s_Robe =
        {
            { 0.03f, 0.285f, 0.222f, 0.0f }, { 0.30f, 0.268f, 0.205f, 0.0f }, { 0.60f, 0.25f, 0.188f, 0.002f }, { 0.88f, 0.236f, 0.168f, 0.01f },
            { 1.02f, 0.232f, 0.158f, 0.022f }, { 1.16f, 0.236f, 0.142f, 0.028f }, { 1.28f, 0.244f, 0.13f, 0.03f }, { 1.36f, 0.244f, 0.122f, 0.028f },
            { 1.41f, 0.226f, 0.112f, 0.026f }, { 1.45f, 0.18f, 0.098f, 0.024f }, { 1.48f, 0.118f, 0.082f, 0.022f }, { 1.50f, 0.072f, 0.066f, 0.02f },
            { 1.505f, 0.068f, 0.063f, 0.02f },
        };
        const float RobeE = 2.4f; // superellipse exponent of the cross-section (a little boxy)

        static Vector3 RobeProfile(float y)
        {
            int n = s_Robe.GetLength(0);
            if (y <= s_Robe[0, 0]) return new Vector3(s_Robe[0, 1], s_Robe[0, 2], s_Robe[0, 3]);
            if (y >= s_Robe[n - 1, 0]) return new Vector3(s_Robe[n - 1, 1], s_Robe[n - 1, 2], s_Robe[n - 1, 3]);
            int i = 0;
            while (i < n - 2 && y > s_Robe[i + 1, 0]) i++;
            float t = (y - s_Robe[i, 0]) / (s_Robe[i + 1, 0] - s_Robe[i, 0]);
            var r = Vector3.zero;
            for (int c = 1; c <= 3; c++)
            {
                float p0 = s_Robe[Mathf.Max(0, i - 1), c], p1 = s_Robe[i, c], p2 = s_Robe[i + 1, c], p3 = s_Robe[Mathf.Min(n - 1, i + 2), c];
                float v = 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
                if (c == 1) r.x = v; else if (c == 2) r.y = v; else r.z = v;
            }
            return r;
        }

        static float Se(float c) => Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / RobeE);

        /// <summary>The robe's surface at height y, angle th (0 = front, toward +X), pushed out by <paramref name="lift"/>.</summary>
        static Vector3 RobeAt(float y, float th, float lift = 0f, bool folds = true)
        {
            var p = RobeProfile(y);
            float s = Mathf.Sin(th), c = Mathf.Cos(th);
            float fold = 0f;
            if (folds)
            {
                float amp = 0.0085f * Ss(1.22f, 0.22f, y);
                fold = amp * (0.6f * Mathf.Sin(9f * th + 0.8f * Mathf.Sin(2.7f * y)) + 0.4f * Mathf.Sin(17f * th + 1.9f + 1.3f * y));
            }
            float k = fold + lift;
            return new Vector3((p.x + k) * Se(s), y, p.z + (p.y + k) * Se(c));
        }

        /// <summary>Front (+1) or back (-1) of the robe at height y and offset x (no folds).</summary>
        static float RobeZ(float y, float x, float side)
        {
            var p = RobeProfile(y);
            float u = Mathf.Clamp01(Mathf.Abs(x) / p.x);
            return p.z + side * p.y * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(u, RobeE)), 1f / RobeE);
        }

        void BuildRobe(FigureMesh fm)
        {
            int hips = Idx(m_Hips), spine = Idx(m_Spine), breath = Idx(m_Breath), chest = Idx(m_Chest);
            fm.Weights = p => p.y < 1.0f ? W2(hips, spine, Ss(0.86f, 1.04f, p.y)) : W2(spine, breath, Ss(1.04f, 1.22f, p.y));
            fm.Sub = 0;
            // The djellaba: hem to collar in one surface, with folds that deepen towards the hem.
            fm.Grid(30, 24, (u, v) => RobeAt(Mathf.Lerp(0.03f, 1.505f, v), u * Mathf.PI * 2f), (u, v) => new Vector3(0f, Mathf.Lerp(0.03f, 1.505f, v), RobeProfile(Mathf.Lerp(0.03f, 1.505f, v)).z), true, 0.3f);
            fm.Weights = null;

            // The hood hanging down the back: a pointed bag lying on the robe.
            fm.Bone = breath;
            fm.Grid(14, 12, (u, v) =>
            {
                float y = 1.462f - 0.42f * v;
                float w = 0.125f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * (0.06f + 0.94f * v))), 0.7f) * (1f - 0.3f * v);
                float t = 0.02f * Mathf.Sqrt(1f - v) + 0.003f;
                float ph = u * Mathf.PI * 2f;
                float x = w * Mathf.Cos(ph);
                float s = t * (1f + Mathf.Sin(ph));
                return new Vector3(x, y, RobeZ(y, x, -1f) - s - 0.003f);
            }, (u, v) => { float y = 1.462f - 0.42f * v; return new Vector3(0f, y, RobeZ(y, 0f, -1f) - 0.012f); }, true, 0.3f);
            // A tassel at its point.
            fm.Sub = 1;
            float tipY = 1.462f - 0.42f, tipZ = RobeZ(tipY, 0f, -1f) - 0.008f;
            fm.Ellipsoid(new Vector3(0f, tipY - 0.004f, tipZ), new Vector3(0.011f, 0.012f, 0.011f), 8, 6);
            fm.Tube(t => new Vector3(0f, tipY - 0.012f - 0.05f * t, tipZ - 0.004f * t), t => Vector2.one * Mathf.Lerp(0.006f, 0.013f, t), 4, 8, Vector3.forward, false, true);

            // The braid down the front opening, its dark knot buttons, the collar braid, the shirt showing at the neck.
            fm.Bone = breath;
            fm.Grid(1, 16, (u, v) =>
            {
                float y = Mathf.Lerp(1.497f, 1.12f, v);
                var p = RobeProfile(y);
                float half = Mathf.Pow(0.0125f / p.x, 1.2f);
                return RobeAt(y, (u - 0.5f) * 2f * half, 0.0028f, false);
            }, (u, v) => new Vector3(0f, Mathf.Lerp(1.497f, 1.12f, v), 0f), false, 0.12f);
            fm.Sub = 4;
            for (int i = 0; i < 7; i++)
            {
                float y = 1.465f - i * 0.05f;
                fm.Ellipsoid(RobeAt(y, 0f, 0.008f, false), new Vector3(0.0062f, 0.0062f, 0.0055f), 6, 4);
            }
            fm.Sub = 1;
            fm.Tube(t => RobeAt(1.494f, t * Mathf.PI * 2f, 0.003f, false), t => new Vector2(0.0048f, 0.0048f), 24, 5, Vector3.up, false, false, true);
            fm.Bone = chest;
            fm.Tube(t => new Vector3(0f, Mathf.Lerp(1.47f, 1.506f, t), 0.018f), t => new Vector2(0.06f, 0.056f), 2, 18, Vector3.forward, false, false);

            // Babouches peeking out under the hem.
            fm.Bone = hips;
            fm.Sub = 1;
            foreach (float sx in new[] { -1f, 1f })
                fm.Grid(10, 8, (u, v) =>
                {
                    float th = u * Mathf.PI;
                    float w = 0.043f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * (0.15f + 0.85f * v))), 0.6f);
                    float h = 0.045f * (1f - Mathf.Pow(v, 2.2f)) + 0.005f;
                    return new Vector3(sx * 0.085f + w * Mathf.Cos(th), h * Mathf.Sin(th) + 0.006f * Mathf.Pow(v, 4f), 0.04f + 0.2f * v);
                }, (u, v) => new Vector3(sx * 0.085f, 0f, 0.04f + 0.2f * v), false, 0.15f);
        }

        // ------------------------------------------------------------------ the head
        static float G(float x, float s) => Mathf.Exp(-(x * x) / (s * s));

        /// <summary>0 at <paramref name="e0"/>, 1 at <paramref name="e1"/>, smooth between (edges may run either way).</summary>
        static float Ss(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The skull and face (head-local, from the head centre) in the unit direction d.</summary>
        static Vector3 Skull(Vector3 d)
        {
            var R = HeadRadii;
            float r = 1f / Mathf.Sqrt(d.x * d.x / (R.x * R.x) + d.y * d.y / (R.y * R.y) + d.z * d.z / (R.z * R.z));
            var p = d * r;
            float front = Ss(0.15f, 0.8f, d.z);
            // The lower face narrows to the jaw, the chin comes forward, the back of the skull is full.
            if (p.y < 0f) p.x *= 1f - 0.2f * Ss(0f, -0.085f, p.y);
            p.z += 0.01f * G(p.y + 0.083f, 0.022f) * front;
            p.z -= 0.006f * G(p.y - 0.03f, 0.045f) * Ss(0.2f, 0.9f, -d.z);
            float ax = Mathf.Abs(p.x), bump = 0f;
            bump += (0.0055f * G(ax - 0.03f, 0.026f) + 0.0025f * G(p.x, 0.02f)) * G(p.y - 0.031f, 0.0095f) * front; // brow ridge
            bump -= 0.0075f * G(ax - 0.033f, 0.019f) * G(p.y - 0.009f, 0.012f) * front;                          // eye sockets
            bump += 0.0055f * G(ax - 0.048f, 0.014f) * G(p.y + 0.008f, 0.012f) * Ss(0.1f, 0.6f, d.z); // cheekbones
            bump -= 0.0045f * G(ax - 0.043f, 0.015f) * G(p.y + 0.04f, 0.014f) * front;                          // hollows under them
            bump -= 0.003f * G(ax - 0.066f, 0.012f) * G(p.y - 0.035f, 0.015f);                                  // temples
            bump += 0.0025f * G(p.x, 0.016f) * G(p.y + 0.071f, 0.0045f) * front;                                // lower lip
            return p + d * bump;
        }

        static Vector3 Dir(float th, float ph) => new Vector3(Mathf.Sin(ph) * Mathf.Sin(th), Mathf.Cos(ph), Mathf.Sin(ph) * Mathf.Cos(th));

        /// <summary>The head-local point on the face at (x, y) seen from the front, pushed out radially by lift.</summary>
        static Vector3 OnFace(float x, float y, float lift)
        {
            float tx = x, ty = y;
            Vector3 p = Vector3.zero, d = Vector3.forward;
            for (int i = 0; i < 4; i++)
            {
                d = new Vector3(tx, ty, Mathf.Sqrt(Mathf.Max(0.0004f, 0.0081f - tx * tx - ty * ty))).normalized;
                p = Skull(d);
                tx += x - p.x;
                ty += y - p.y;
            }
            return p + d * lift;
        }

        /// <summary>How far the nose stands off the face, from its bridge (v = 0, y = 0.02, blending into the brow) to its base (v = 1).</summary>
        static float NoseHeight(float v)
        {
            float h = 0.003f * Ss(0f, 0.15f, v) + 0.0205f * Ss(0f, 0.82f, v) + 0.0065f * G(v - 0.8f, 0.07f) + 0.0018f * G(v - 0.38f, 0.1f);
            if (v > 0.84f) h *= 1f - 0.85f * Ss(0.84f, 1f, v);
            return h;
        }

        static Vector3 EyeCentre(float sx)
        {
            var s = OnFace(sx * 0.032f, 0.008f, 0f);
            return HeadCentre + new Vector3(sx * 0.032f, 0.008f, s.z - 0.0122f + 0.0095f);
        }

        /// <summary>Where the beard starts (polar angle from the top, degrees) at azimuth |th| (degrees).</summary>
        static float CheekLine(float a)
        {
            float[] at = { 0f, 22f, 38f, 58f, 82f, 105f, 130f, 180f };
            float[] ph = { 134f, 128f, 117f, 103f, 91f, 87f, 87f, 87f };
            for (int i = 0; i < at.Length - 1; i++)
                if (a <= at[i + 1]) return Mathf.Lerp(ph[i], ph[i + 1], Mathf.SmoothStep(0f, 1f, (a - at[i]) / (at[i + 1] - at[i])));
            return ph[ph.Length - 1];
        }

        static float KufiRim(float th) => 57f + 9f * (1f - Mathf.Cos(th)) * 0.5f; // degrees from the top: lower at the back

        void BuildHead(FigureMesh fm)
        {
            int head = Idx(m_Head), neck = Idx(m_Neck), chest = Idx(m_Chest);
            var H = HeadCentre;
            // Skin: the head, denser over the face.
            fm.Sub = 2;
            fm.Bone = head;
            fm.Grid(26, 22, (u, v) =>
            {
                float s = 2f * u - 1f, t = 2f * v - 1f;
                float th = Mathf.PI * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 1.5f);
                float ph = Mathf.PI * (0.5f + 0.5f * Mathf.Sign(t) * Mathf.Pow(Mathf.Abs(t), 1.35f));
                return H + Skull(Dir(th, ph));
            }, (u, v) => H, true, 0.2f);
            // The nose: bridge, aquiline bump, a heavy tip and the wings, its base turning back under to the lip.
            fm.Grid(10, 10, (u, v) =>
            {
                float y = 0.02f - 0.058f * v;
                float w = 0.0045f + 0.0105f * Mathf.Pow(v, 1.5f) + 0.0065f * G(v - 0.87f, 0.075f);
                float h = NoseHeight(v);
                float x = w * (2f * u - 1f);
                float cross = Mathf.Pow(Mathf.Max(0f, 1f - (2f * u - 1f) * (2f * u - 1f)), 0.6f);
                var b = OnFace(x, y - (v > 0.84f ? (v - 0.84f) * 0.02f : 0f), -0.002f);
                return H + b + Vector3.forward * (h * cross);
            }, (u, v) => H, false, 0.05f);
            fm.Sub = 4;
            foreach (float sx in new[] { -1f, 1f }) // nostrils
                fm.Disc(H + OnFace(sx * 0.0075f, -0.036f, 0.0205f), new Vector3(sx * 0.2f, -1f, 0.35f), 0.0032f, 8);
            // Ears, standing a little out from the head.
            fm.Sub = 2;
            foreach (float sx in new[] { -1f, 1f })
            {
                var c = H + new Vector3(sx * 0.0795f, -0.004f, -0.006f);
                var rot = Quaternion.Euler(-14f, sx * 20f, 0f);
                fm.Grid(10, 6, (u, v) =>
                {
                    var d = Dir(u * Mathf.PI * 2f, v * Mathf.PI);
                    var q = new Vector3(d.x * 0.0075f, d.y * 0.031f, d.z * 0.0175f);
                    q.x -= sx * 0.004f * G(new Vector2(d.y, d.z).magnitude, 0.55f) * Ss(0f, 0.3f, d.x * sx); // the bowl of the ear (outer face only, eased in so the rim has no step)
                    q.x += sx * 0.002f * Mathf.Pow(Mathf.Abs(d.z), 3f);
                    return c + rot * q;
                }, (u, v) => c, true, 0.05f);
            }
            // Neck.
            fm.Bone = neck;
            fm.Tube(t => new Vector3(0f, Mathf.Lerp(1.43f, 1.6f, t), Mathf.Lerp(0.016f, 0.03f, t)), t => new Vector2(0.05f, 0.047f), 3, 12, Vector3.forward, false, false);

            // Eyes: wet whites, dark irises (eye bones aim them), heavy upper lids (lid bones blink) and lower lids.
            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? -1f : 1f;
                fm.Xf = CharOf(m_Eye[s]);
                fm.Bone = Idx(m_Eye[s]);
                fm.Sub = 5;
                fm.Ellipsoid(Vector3.zero, Vector3.one * 0.0122f, 10, 8);
                fm.Sub = 4;
                fm.Disc(new Vector3(0f, 0f, 0.0123f), Vector3.forward, 0.0057f, 12);
                fm.Xf = CharOf(m_Lid[s]);
                fm.Bone = Idx(m_Lid[s]);
                fm.Sub = 2;
                fm.Grid(12, 5, (u, v) => Dir(u * Mathf.PI * 2f, v * 74f * Mathf.Deg2Rad) * (0.0134f + 0.0006f * Mathf.Sin(v * Mathf.PI)), (u, v) => Vector3.zero, true, 0.05f);
                fm.Bone = head;
                fm.Grid(12, 3, (u, v) => Dir(u * Mathf.PI * 2f, Mathf.Lerp(112f, 172f, v) * Mathf.Deg2Rad) * 0.0131f, (u, v) => Vector3.zero, true, 0.05f);
                fm.Xf = Matrix4x4.identity;
                // Bushy brows on their own bones (they lift in the welcome).
                fm.Bone = Idx(m_Brow[s]);
                fm.Sub = 3;
                float[] bx = { 0.012f, 0.025f, 0.04f, 0.055f }, by = { 0.026f, 0.033f, 0.034f, 0.027f };
                fm.Tube(t =>
                {
                    float f = t * 3f;
                    int i = Mathf.Min(2, Mathf.FloorToInt(f));
                    float k = f - i;
                    return H + OnFace(sx * Mathf.Lerp(bx[i], bx[i + 1], k), Mathf.Lerp(by[i], by[i + 1], k), 0.0035f);
                }, t => new Vector2(Mathf.Lerp(0.0026f, 0.0016f, t), Mathf.Lerp(0.0036f, 0.0018f, t)), 6, 6, Vector3.up, true, true);
            }

            // The beard: a shell round the lower face, emerging below the cheek line, full at the chin and hanging onto
            // the chest; its lower part follows the neck so the head can turn.
            fm.Sub = 3;
            int beardHead = head;
            fm.Weights = p => W2(chest, beardHead, Ss(1.47f, 1.56f, p.y));
            fm.Grid(28, 16, (u, v) =>
            {
                float th = Mathf.Lerp(-128f, 128f, u), ph = Mathf.Lerp(70f, 174f, v);
                var d = Dir(th * Mathf.Deg2Rad, ph * Mathf.Deg2Rad);
                float a = Mathf.Abs(th), line = CheekLine(a);
                float m = Ss(line - 2f, line + 3.5f, ph) * (1f - Ss(112f, 126f, a));
                float chin = G(th / 45f, 1f) * Ss(112f, 150f, ph);
                float hang = Ss(138f, 174f, ph) * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(th * Mathf.Deg2Rad)), 1.5f);
                float strands = 0.0028f * Mathf.Sin(th * 0.2f + 2f * Mathf.Sin(ph * 0.1f)) + 0.0011f * Mathf.Sin(th * 0.55f + ph * 0.05f);
                float t = -0.004f + m * (0.012f + 0.012f * chin + 0.065f * hang + strands);
                return H + Skull(d) + d * t;
            }, (u, v) => H + new Vector3(0f, -0.04f, 0f), false, 0.08f);
            fm.Weights = null;
            fm.Bone = head;
            // A drooping moustache from under the nose to the corners of the mouth.
            foreach (float sx in new[] { -1f, 1f })
            {
                float[] mx = { 0.003f, 0.012f, 0.022f, 0.031f, 0.036f }, my = { -0.046f, -0.049f, -0.055f, -0.064f, -0.074f };
                fm.Tube(t =>
                {
                    float f = t * 4f;
                    int i = Mathf.Min(3, Mathf.FloorToInt(f));
                    float k = f - i;
                    return H + OnFace(sx * Mathf.Lerp(mx[i], mx[i + 1], k), Mathf.Lerp(my[i], my[i + 1], k), Mathf.Lerp(0.0062f, 0.005f, t));
                }, t => new Vector2(Mathf.Lerp(0.0046f, 0.003f, t), Mathf.Lerp(0.0066f, 0.004f, t)), 7, 6, Vector3.up, true, true);
            }
            // Short grey hair round the back and over the ears, under the cap.
            fm.Grid(18, 6, (u, v) =>
            {
                float th = Mathf.Lerp(65f, 295f, u), ph = Mathf.Lerp(50f, 110f, v);
                var d = Dir(th * Mathf.Deg2Rad, ph * Mathf.Deg2Rad);
                float a = Mathf.Abs(Mathf.DeltaAngle(0f, th));
                float m = Ss(KufiRim(th * Mathf.Deg2Rad) - 4f, KufiRim(th * Mathf.Deg2Rad) + 1f, ph) * (1f - Ss(100f + 6f * Ss(120f, 180f, a), 106f + 4f * Ss(120f, 180f, a), ph)) * Ss(65f, 82f, a);
                float t = -0.003f + m * (0.0045f + 0.0008f * Mathf.Sin(ph * 0.7f + th * 0.16f));
                return H + Skull(d) + d * t;
            }, (u, v) => H, false, 0.08f);
            // The kufi: a crocheted cream cap with a rolled rim.
            fm.Sub = 1;
            fm.Grid(24, 6, (u, v) =>
            {
                float th = u * Mathf.PI * 2f, ph = KufiRim(th) * v * Mathf.Deg2Rad;
                var d = Dir(th, ph);
                return H + Skull(d) + d * (0.0075f - 0.002f * Mathf.Pow(Mathf.Cos(ph), 6f));
            }, (u, v) => H, true, 0.07f);
            fm.Tube(t =>
            {
                float th = t * Mathf.PI * 2f;
                var d = Dir(th, KufiRim(th) * Mathf.Deg2Rad);
                return H + Skull(d) + d * 0.0062f;
            }, t => Vector2.one * 0.0042f, 18, 5, Vector3.up, false, false, true);
        }

        // ------------------------------------------------------------------ arms and hands
        void BuildArm(FigureMesh fm, Arm arm)
        {
            // Sleeves: a shoulder cap blending into the robe, the upper sleeve, the elbow, the wide forearm sleeve with a
            // braided cuff. Built in each bone's own frame (the limb runs down local -Y).
            fm.Sub = 0;
            fm.Xf = CharOf(arm.Shoulder);
            fm.Bone = Idx(arm.Shoulder);
            fm.Ellipsoid(new Vector3(0f, -0.03f, 0f), new Vector3(0.056f, 0.056f, 0.058f), 8, 6);
            fm.Tube(t => new Vector3(0f, Mathf.Lerp(-0.015f, -UpperArm - 0.02f, t), 0f),
                t => Vector2.one * (Mathf.Lerp(0.064f, 0.06f, t) + 0.003f * Mathf.Sin(t * 14f + 1f)), 6, 12, Vector3.forward, false, false);
            fm.Xf = CharOf(arm.Elbow);
            fm.Bone = Idx(arm.Elbow);
            fm.Ellipsoid(Vector3.zero, Vector3.one * 0.058f, 8, 5);
            fm.Tube(t => new Vector3(0f, Mathf.Lerp(0.03f, -0.238f, t), 0f),
                t => Vector2.one * (Mathf.Lerp(0.058f, 0.079f, Ss(0.15f, 1f, t)) + 0.004f * Mathf.Sin(t * 18f) * (1f - t)), 8, 12, Vector3.forward, false, false);
            fm.Disc(new Vector3(0f, -0.236f, 0f), Vector3.down, 0.077f, 12);
            fm.Sub = 1;
            fm.Tube(t => { float a = t * Mathf.PI * 2f; return new Vector3(Mathf.Cos(a) * 0.079f, -0.232f, Mathf.Sin(a) * 0.079f); }, t => Vector2.one * 0.0045f, 14, 4, Vector3.up, false, false, true);

            // The hand: wrist, palm with knuckles and the ball of the thumb, two-jointed fingers and thumb.
            fm.Sub = 2;
            fm.Xf = CharOf(arm.Hand);
            fm.Bone = Idx(arm.Hand);
            fm.Mirror = arm.Side < 0f;
            fm.Tube(t => new Vector3(0f, Mathf.Lerp(0.05f, -0.012f, t), 0f), t => new Vector2(0.023f, 0.017f), 3, 8, Vector3.forward, false, false);
            fm.Grid(10, 8, (u, v) =>
            {
                var d = Dir(u * Mathf.PI * 2f, v * Mathf.PI);
                var q = new Vector3(0.041f * Pw(d.x, 2.6f), -0.046f + 0.046f * Pw(d.y, 2.6f), 0.0135f * d.z);
                if (d.z < 0f) for (int f = 0; f < 4; f++) { var (r, _, _) = FingerRoot(f); q.z -= 0.0035f * G(q.x - r.x, 0.006f) * G(q.y - r.y + 0.004f, 0.007f); }
                return q;
            }, (u, v) => new Vector3(0f, -0.046f, 0f), true, 0.06f);
            fm.Ellipsoid(new Vector3(0.03f, -0.032f, 0.006f), new Vector3(0.016f, 0.026f, 0.013f), 8, 6);
            for (int f = 0; f < 5; f++)
            {
                var (_, _, prox) = FingerRoot(f);
                float r = FingerRadius(f), dl = DistLen(f);
                // In each phalanx's own frame (its bind rotation aims local -Y down the finger); mirrored for the left hand.
                fm.Xf = CharOf(arm.Prox[f]);
                fm.Bone = Idx(arm.Prox[f]);
                fm.Mirror = false; // the finger frames are already placed for each side
                fm.Tube(t => new Vector3(0f, Mathf.Lerp(0.006f, -prox, t), 0f), t => new Vector2(r, r * 0.9f) * Mathf.Lerp(1f, 0.92f, t), 3, 6, Vector3.forward, false, false);
                fm.Xf = CharOf(arm.Dist[f]);
                fm.Bone = Idx(arm.Dist[f]);
                fm.Ellipsoid(Vector3.zero, Vector3.one * r * 0.93f, 6, 3);
                fm.Tube(t => new Vector3(0f, Mathf.Lerp(0.004f, -dl, t), 0f),
                    t => new Vector2(r * 0.9f, r * 0.82f) * Mathf.Lerp(1f, 0.78f, t) * (t > 0.82f ? Mathf.Sqrt(Mathf.Max(0.02f, 1f - Mathf.Pow((t - 0.82f) / 0.18f, 2f))) : 1f),
                    5, 6, Vector3.forward, false, false);
            }
            fm.Mirror = false;
            fm.Xf = Matrix4x4.identity;
        }

        static float Pw(float c, float e) => Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / e);

        // ------------------------------------------------------------------ glasses, pencil, loupe
        void BuildAccessories(FigureMesh fm)
        {
            var H = HeadCentre;
            fm.Bone = Idx(m_Head);
            // Half-moon reading glasses low on the nose: thin gold rims, a bridge, temples back over the ears.
            var tilt = Quaternion.Euler(-8f, 0f, 0f);
            var lensTop = new Vector3[2];
            var outer = new Vector3[2];
            var inner = new Vector3[2];
            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? -1f : 1f;
                // Low on the nose, below the eyes (he looks over them), the inner edges clear of the nose.
                var c = H + OnFace(sx * 0.036f, -0.02f, 0f) + Vector3.forward * 0.012f;
                c.x = H.x + sx * 0.036f;
                c.y = H.y - 0.012f;
                var loop = new List<Vector3>();
                for (int i = 0; i <= 3; i++) loop.Add(new Vector3(Mathf.Lerp(-0.02f, 0.02f, i / 3f), 0f, 0f));
                for (int i = 1; i < 16; i++)
                {
                    float a = Mathf.PI * i / 16f;
                    loop.Add(new Vector3(0.02f * Mathf.Cos(a), -0.016f * Mathf.Sin(a), 0f));
                }
                for (int i = 0; i < loop.Count; i++) loop[i] = c + tilt * loop[i];
                fm.Sub = 6;
                int n = loop.Count;
                fm.Tube(t => { float f = t * n; int i = Mathf.FloorToInt(f) % n; return Vector3.Lerp(loop[i], loop[(i + 1) % n], f - Mathf.Floor(f)); },
                    t => Vector2.one * 0.0011f, n * 2, 5, Vector3.forward, false, false, true);
                fm.Sub = 7;
                fm.Fan(loop, c + tilt * new Vector3(0f, -0.007f, 0f), tilt * Vector3.forward);
                lensTop[s] = c;
                outer[s] = c + tilt * new Vector3(sx * 0.02f, 0f, 0f);
                inner[s] = c + tilt * new Vector3(-sx * 0.02f, 0f, 0f);
            }
            fm.Sub = 6;
            var bridgeTop = H + OnFace(0f, -0.012f, 0f) + Vector3.forward * (NoseHeight((0.02f + 0.012f) / 0.058f) + 0.0025f);
            fm.Tube(t => Bezier(inner[0], bridgeTop + new Vector3(-0.004f, 0f, 0f), bridgeTop + new Vector3(0.004f, 0f, 0f), inner[1], t), t => Vector2.one * 0.0011f, 6, 5, Vector3.up, false, false);
            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? -1f : 1f;
                var pts = new[] { outer[s], Around(sx * 0.062f, -0.004f, 0.075f, 0.005f), Around(sx * 0.077f, 0.006f, 0.04f, 0.0045f),
                    Around(sx * 0.082f, 0.02f, 0.008f, 0.0045f), Around(sx * 0.082f, 0.026f, -0.008f, 0.0042f), Around(sx * 0.08f, 0.012f, -0.022f, 0.004f) };
                fm.Tube(t => Path(pts, t), t => Vector2.one * 0.0011f, 10, 5, Vector3.up, false, false);
            }
            // A pencil behind his right ear, resting on the crease above it.
            var mid = Around(0.074f, 0.027f, -0.002f, 0.0038f);
            var along = new Vector3(0.05f, -0.2f, 1f).normalized;
            var p0 = mid + along * 0.045f;
            var p1 = mid - along * 0.045f;
            fm.Sub = 5;
            fm.Tube(t => Vector3.Lerp(p1, p0, t), t => Vector2.one * 0.0035f, 2, 6, Vector3.up, false, false);
            fm.Sub = 1;
            fm.Tube(t => p0 + along * (0.011f * t), t => Vector2.one * Mathf.Lerp(0.0035f, 0.0012f, t), 2, 6, Vector3.up, false, false);
            fm.Sub = 4;
            fm.Tube(t => p0 + along * (0.011f + 0.003f * t), t => Vector2.one * Mathf.Lerp(0.0012f, 0.0003f, t), 1, 4, Vector3.up, false, false);
            fm.Sub = 6;
            fm.Tube(t => p1 - along * (0.006f * t), t => Vector2.one * 0.0037f, 1, 6, Vector3.up, false, true);

            // A jeweller's loupe on a cord, hanging on his chest a little to the left.
            fm.Bone = Idx(m_Breath);
            var L = new Vector3(-0.045f, 1.275f, RobeZ(1.275f, -0.045f, 1f) + 0.012f);
            var axis = new Vector3(0f, -0.25f, 1f).normalized;
            fm.Sub = 4;
            fm.Tube(t => L + axis * Mathf.Lerp(-0.009f, 0.009f, t), t => Vector2.one * Mathf.Lerp(0.0115f, 0.0128f, t), 2, 12, Vector3.up, false, false);
            fm.Disc(L - axis * 0.009f, -axis, 0.0115f, 12);
            fm.Sub = 6;
            fm.Tube(t => { float a = t * Mathf.PI * 2f; var r = Vector3.Cross(axis, Vector3.up).normalized; var u2 = Vector3.Cross(r, axis); return L + axis * 0.009f + (r * Mathf.Cos(a) + u2 * Mathf.Sin(a)) * 0.0128f; },
                t => Vector2.one * 0.0016f, 12, 4, axis, false, false, true);
            fm.Sub = 7;
            fm.Disc(L + axis * 0.0095f, axis, 0.0118f, 12);
            fm.Sub = 4;
            var top = L + Vector3.up * 0.012f;
            var cordA = new[] { top, new Vector3(-0.03f, 1.33f, RobeZ(1.33f, -0.03f, 1f) + 0.007f), new Vector3(-0.046f, 1.4f, RobeZ(1.4f, -0.046f, 1f) + 0.006f), new Vector3(-0.05f, 1.46f, RobeZ(1.46f, -0.05f, 1f) - 0.002f) };
            var cordB = new[] { top, new Vector3(-0.012f, 1.34f, RobeZ(1.34f, -0.012f, 1f) + 0.007f), new Vector3(0.03f, 1.41f, RobeZ(1.41f, 0.03f, 1f) + 0.006f), new Vector3(0.05f, 1.465f, RobeZ(1.465f, 0.05f, 1f) - 0.002f) };
            fm.Tube(t => Path(cordA, t), t => Vector2.one * 0.0012f, 8, 4, Vector3.forward, false, false);
            fm.Tube(t => Path(cordB, t), t => Vector2.one * 0.0012f, 8, 4, Vector3.forward, false, false);
        }

        /// <summary>Head-local direction (x, y, z) projected onto the skull and lifted, in character space.</summary>
        static Vector3 Around(float x, float y, float z, float lift)
        {
            var d = new Vector3(x, y, z).normalized;
            return HeadCentre + Skull(d) + d * lift;
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return a * (u * u * u) + b * (3f * u * u * t) + c * (3f * u * t * t) + d * (t * t * t);
        }

        /// <summary>A smooth (Catmull-Rom) path through the points.</summary>
        static Vector3 Path(Vector3[] p, float t)
        {
            int n = p.Length - 1;
            float f = Mathf.Clamp01(t) * n;
            int i = Mathf.Min(n - 1, Mathf.FloorToInt(f));
            float k = f - i;
            var p0 = p[Mathf.Max(0, i - 1)]; var p1 = p[i]; var p2 = p[i + 1]; var p3 = p[Mathf.Min(n, i + 2)];
            return 0.5f * (2f * p1 + (-p0 + p2) * k + (2f * p0 - 5f * p1 + 4f * p2 - p3) * k * k + (-p0 + 3f * p1 - 3f * p2 + p3) * k * k * k);
        }

        static Quaternion BoneRot(Vector3 along, Vector3 hint)
        {
            var y = -along.normalized; // a bone's local -Y runs down the limb
            var z = Vector3.ProjectOnPlane(hint, y);
            if (z.sqrMagnitude < 1e-6f) z = Vector3.ProjectOnPlane(Vector3.forward, y);
            if (z.sqrMagnitude < 1e-6f) z = Vector3.ProjectOnPlane(Vector3.right, y);
            return Quaternion.LookRotation(z.normalized, y);
        }

        // ================================================================== animation
        void LateUpdate()
        {
            var rig = Game.Rig;
            var look = rig != null && rig.Head != null ? rig.Head.position : transform.TransformPoint(new Vector3(0f, 1.6f, 2.5f));
            Animate(Time.time, Time.deltaTime, look);
        }

        static float Clock(ref float t, float dt, float length)
        {
            if (t < 0f) return -1f;
            t += dt;
            if (t >= length) { t = -1f; return -1f; }
            return t / length;
        }

        static float Rise(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, (x - a) / (b - a));

        internal void Animate(float time, float dt, Vector3 lookWorld)
        {
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            float welcome = Clock(ref m_WelcomeT, dt, WelcomeSeconds);
            float nod = Clock(ref m_NodT, dt, NodSeconds);
            float offerBefore = m_OfferT;
            float offer = Clock(ref m_OfferT, dt, m_OfferLength > 0f ? m_OfferLength : OfferSeconds);
            if (offerBefore >= 0f && offer < 0f) RestoreItems();

            // ---- gesture weights
            float wWelcome = welcome < 0f ? 0f : Rise(0f, 0.3f, welcome) * (1f - Rise(0.72f, 1f, welcome));
            float a = offer < 0f ? -1f : offer * m_OfferLength; // seconds into the offer
            bool holding = m_Items != null && a >= 0f;
            float lean = 0f, turn = 0f, raise = wWelcome;
            float wReach = 0f, wGrip = 0f, wSupport = 0f, wPresent = 0f;
            float itemK = 0f; // 0 = on the counter (slid), 1 = held out
            if (a >= 0f)
            {
                if (holding)
                {
                    wReach = Rise(0f, 0.9f, a) * (1f - Rise(4.1f, 4.6f, a));
                    wGrip = Rise(0.8f, 1.15f, a) * (1f - Rise(4.05f, 4.35f, a));
                    itemK = Rise(1.2f, 2.1f, a) * (1f - Rise(3.3f, 4.1f, a));
                    wSupport = Rise(1.5f, 2.2f, a) * (1f - Rise(3.2f, 3.8f, a));
                    lean = 15f * wReach * (1f - itemK) + 3f * itemK;
                    turn = 13f * wReach * (1f - itemK);
                    raise = Mathf.Max(raise, itemK);
                    if (!m_OfferNodded && a > 2.35f) { m_OfferNodded = true; m_NodT = 0f; }
                }
                else
                {
                    wPresent = Rise(0f, 0.8f, a) * (1f - Rise(2.6f, 3.4f, a));
                    lean = 4f * wPresent;
                    raise = Mathf.Max(raise, wPresent);
                    if (!m_OfferNodded && a > 1.0f) { m_OfferNodded = true; m_NodT = 0f; }
                }
            }

            // ---- body: breathing, a slow weight shift, the lean over the counter
            float breath = Mathf.Sin(time * (2f * Mathf.PI / 4.4f));
            float sway = Mathf.Sin(time * (2f * Mathf.PI / 9.7f)), sway2 = Mathf.Sin(time * (2f * Mathf.PI / 6.3f) + 1.3f);
            m_Hips.localPosition = m_HipsHome + new Vector3(0.011f * sway, 0f, 0f);
            m_Hips.localRotation = Quaternion.Euler(0f, 1.2f * sway2, -1.1f * sway);
            m_Spine.localRotation = Quaternion.Euler(2f + lean, -turn + 0.8f * sway2 + 4f * wWelcome, 0.9f * sway);
            m_Chest.localRotation = Quaternion.Euler(0.6f * breath + 0.25f * lean, 0f, 0.3f * sway);
            m_Breath.localScale = new Vector3(1f + 0.007f * breath, 1f + 0.003f * breath, 1f + 0.012f * breath);
            foreach (var arm in m_Arms) arm.Shoulder.localPosition = arm.ShoulderHome + new Vector3(0f, 0.0025f * breath, 0f);

            // ---- head and eyes: the player, or a passing glance elsewhere
            var look = lookWorld;
            if (m_GlanceT < 0f && time > m_NextGlance && a < 0f && welcome < 0f) { m_GlanceT = 0f; m_Glance = m_Rnd.Next(GlanceAt.Length); }
            if (m_GlanceT >= 0f)
            {
                m_GlanceT += dt;
                float g = Rise(0f, 0.25f, m_GlanceT) * (1f - Rise(1.1f, 1.5f, m_GlanceT));
                look = Vector3.Lerp(lookWorld, transform.TransformPoint(GlanceAt[m_Glance]), g);
                if (m_GlanceT > 1.5f) { m_GlanceT = -1f; m_NextGlance = time + 7f + (float)m_Rnd.NextDouble() * 8f; }
            }
            if (holding && itemK < 0.5f && wReach > 0.3f) look = Vector3.Lerp(look, m_Items[0].position, 0.6f); // he looks at what he reaches for
            var rel = Quaternion.Inverse(m_Chest.rotation) * (look - m_Head.position);
            float yaw = Mathf.Clamp(Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg, -62f, 62f);
            float pitch = Mathf.Clamp(-Mathf.Atan2(rel.y, new Vector2(rel.x, rel.z).magnitude) * Mathf.Rad2Deg, -20f, 32f);
            if (dt <= 0f) { m_Yaw = yaw; m_Pitch = pitch; }
            else
            {
                m_Yaw = Mathf.SmoothDampAngle(m_Yaw, yaw, ref m_YawVel, 0.3f, 400f, dt);
                m_Pitch = Mathf.SmoothDampAngle(m_Pitch, pitch, ref m_PitchVel, 0.36f, 300f, dt);
            }
            float nodPitch = nod < 0f ? 0f : 12f * Mathf.Sin(2f * Mathf.PI * nod) * (1f - 0.55f * nod);
            float tilt = 5f * wWelcome + 2.5f * itemK;
            m_Neck.localRotation = Quaternion.Euler(4f + m_Pitch * 0.35f, m_Yaw * 0.4f, 0f);
            m_Head.localRotation = Quaternion.Euler(-4f + m_Pitch * 0.65f + nodPitch - 0.5f * breath, m_Yaw * 0.6f, tilt);
            float eyePitch = 0f;
            for (int s = 0; s < 2; s++)
            {
                var er = Quaternion.Inverse(m_Head.rotation) * (look - m_Eye[s].position);
                float ey = Mathf.Clamp(Mathf.Atan2(er.x, er.z) * Mathf.Rad2Deg, -28f, 28f);
                float ep = Mathf.Clamp(-Mathf.Atan2(er.y, new Vector2(er.x, er.z).magnitude) * Mathf.Rad2Deg, -22f, 24f);
                var target = new Vector2(ep, ey);
                if (s == 0) m_EyeAim = dt <= 0f ? target : Vector2.Lerp(m_EyeAim, target, 1f - Mathf.Exp(-dt * 18f));
                m_Eye[s].localRotation = Quaternion.Euler(m_EyeAim.x, m_EyeAim.y, 0f);
                eyePitch = m_EyeAim.x;
            }
            // Blinks every few seconds (sometimes twice); the lids follow the eyes down.
            if (m_BlinkT < 0f && time > m_NextBlink) m_BlinkT = 0f;
            float blink = 0f;
            if (m_BlinkT >= 0f)
            {
                m_BlinkT += dt;
                blink = m_BlinkT < 0.07f ? m_BlinkT / 0.07f : 1f - Mathf.Clamp01((m_BlinkT - 0.07f) / 0.13f);
                if (m_BlinkT > 0.2f) { m_BlinkT = -1f; m_NextBlink = time + (m_Rnd.NextDouble() < 0.2 ? 0.28f : 2.2f + (float)m_Rnd.NextDouble() * 3.3f); }
            }
            for (int s = 0; s < 2; s++)
            {
                m_Lid[s].localRotation = Quaternion.Euler(-4f + 0.45f * Mathf.Max(0f, eyePitch) + 0.2f * Mathf.Min(0f, eyePitch) + blink * 62f, 0f, 0f);
                m_Brow[s].localPosition = m_BrowHome[s] + new Vector3(0f, 0.0028f * raise, 0.0006f * raise);
            }

            // ---- arms: hands on the counter, the welcome, the reach and hold of an offer
            float idle = 0.04f * Mathf.Sin(time * 0.9f);
            var right = RestR; right.Curl += idle;
            var left = RestL; left.Curl -= idle;
            float sweep = Rise(0.3f, 0.72f, welcome < 0f ? 0f : welcome);
            right = HandPose.Lerp(right, new HandPose(WelcomeR.Pos + new Vector3(0.05f * sweep, 0.015f * sweep, 0f), WelcomeR.Palm, WelcomeR.Finger, WelcomeR.Curl, WelcomeR.Spread), wWelcome);
            left = HandPose.Lerp(left, WelcomeL, 0.4f * wWelcome);
            if (holding)
            {
                // The items follow their own path (slid within reach, lifted, held out, put back); the hands follow them.
                var home0 = m_ItemHome[0] + m_Slide;
                var held = home0 + m_PresentDelta + transform.forward * (0.015f * Rise(2.1f, 2.6f, a));
                var pos0 = Vector3.Lerp(home0, held, itemK) + Vector3.up * (0.04f * Mathf.Sin(Mathf.PI * itemK));
                var delta = pos0 - m_ItemHome[0];
                for (int i = 0; i < m_Items.Length; i++)
                    if (m_Items[i] != null) { m_Items[i].position = m_ItemHome[i] + delta; m_Items[i].rotation = m_ItemHomeRot[i]; }
                var grip = transform.InverseTransformPoint(pos0 + m_GripOffset);
                var gripPose = new HandPose(grip, new Vector3(0f, -1f, 0.15f), new Vector3(-0.35f, -0.25f, 0.9f), Mathf.Lerp(0.3f, 0.6f, wGrip), 0.1f);
                right = HandPose.Lerp(right, gripPose, wReach);
                var support = transform.InverseTransformPoint(pos0 + m_SupportOffset);
                left = HandPose.Lerp(left, new HandPose(support, new Vector3(0.1f, 1f, 0.1f), new Vector3(0.4f, 0.05f, 0.9f), 0.3f, 0.15f), wSupport);
            }
            else if (wPresent > 0f)
            {
                right = HandPose.Lerp(right, PresentR, wPresent);
                left = HandPose.Lerp(left, PresentL, wPresent);
            }
            SolveArm(m_Arms[0], left);
            SolveArm(m_Arms[1], right);
            // Out of reach: at the grip, slide the items along the counter to where the hand got to (and back at the end).
            if (holding)
            {
                if (a > 0.85f && a < 1.2f)
                {
                    var gap = transform.TransformPoint(m_Arms[1].Reached) - transform.TransformPoint(right.Pos);
                    gap.y = 0f;
                    m_Slide = Vector3.Lerp(m_Slide, gap * Rise(0.85f, 1.15f, a), 0.5f);
                }
                else if (a > 4.1f) m_Slide = Vector3.Lerp(m_Slide, Vector3.zero, Rise(4.1f, 4.4f, a));
            }
        }

        void SolveArm(Arm arm, HandPose pose)
        {
            var handRot = transform.rotation * Quaternion.LookRotation(pose.Palm, -pose.Finger);
            var palm = transform.TransformPoint(pose.Pos);
            var wristTarget = palm - handRot * new Vector3(0f, PalmCentre, 0f);
            var S = arm.Shoulder.position;
            var d = wristTarget - S;
            float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(UpperArm - Forearm) + 0.02f, UpperArm + Forearm - 0.002f);
            var dir = d.sqrMagnitude > 1e-8f ? d.normalized : transform.forward;
            var pole = transform.TransformDirection(new Vector3(arm.Side * 0.75f, -1f, -0.5f)).normalized;
            var bend = Vector3.ProjectOnPlane(pole, dir);
            bend = bend.sqrMagnitude > 1e-6f ? bend.normalized : transform.TransformDirection(Vector3.down);
            float cosA = Mathf.Clamp((UpperArm * UpperArm + dist * dist - Forearm * Forearm) / (2f * UpperArm * dist), -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
            var elbow = S + (dir * cosA + bend * sinA) * UpperArm;
            var wrist = S + dir * dist;
            arm.Shoulder.rotation = BoneRot(elbow - S, -bend);
            arm.Elbow.rotation = BoneRot(wrist - elbow, -bend);
            arm.Hand.rotation = handRot;
            arm.Reached = transform.InverseTransformPoint(wrist + handRot * new Vector3(0f, PalmCentre, 0f));
            // Fingers: curl toward the palm; spread a little apart; the thumb curls less.
            float[] spread = { 8f, 2f, -3f, -9f, 10f };
            for (int f = 0; f < 5; f++)
            {
                float c = Mathf.Clamp01(pose.Curl) * (f == 4 ? 0.5f : 1f);
                arm.Prox[f].localRotation = Quaternion.Euler(-c * 62f, 0f, arm.Side * spread[f] * pose.Spread);
                arm.Dist[f].localRotation = Quaternion.Euler(-c * 78f, 0f, 0f);
            }
        }
    }

    /// <summary>
    /// Builds the shopkeeper's skinned mesh: parametric surfaces (smooth normals from the surface itself), tubes, discs
    /// and fans, each vertex bound to one or two bones, one submesh per material. Parts can be built in a bone's own frame
    /// (<see cref="Xf"/>) and mirrored in x for the left side (<see cref="Mirror"/>).
    /// </summary>
    sealed class FigureMesh
    {
        readonly List<Vector3> m_V = new List<Vector3>(), m_N = new List<Vector3>();
        readonly List<Vector2> m_UV = new List<Vector2>();
        readonly List<BoneWeight> m_W = new List<BoneWeight>();
        readonly List<int>[] m_T;
        public Matrix4x4 Xf = Matrix4x4.identity;
        public bool Mirror;
        public int Sub, Bone;
        public Func<Vector3, BoneWeight> Weights;

        public FigureMesh(int submeshes)
        {
            m_T = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) m_T[i] = new List<int>();
        }

        public int TriangleCount { get { int n = 0; foreach (var t in m_T) n += t.Count; return n / 3; } }
        public int VertexCount => m_V.Count;

        /// <summary>
        /// Unit vector without Unity's cut-off. Vector3.normalized returns zero for anything shorter than 1e-5, and the
        /// finite differences on a figure this small (eyes, fingers, the head itself) are far shorter than that. A zero
        /// normal shades as NaN in URP: black, with a bloom halo, and on the normal-mapped robe, stripes.
        /// </summary>
        internal static Vector3 Unit(Vector3 v)
        {
            double m = Math.Sqrt((double)v.x * v.x + (double)v.y * v.y + (double)v.z * v.z);
            return m > 1e-30 && m < 1e30 ? new Vector3((float)(v.x / m), (float)(v.y / m), (float)(v.z / m)) : Vector3.zero;
        }

        int Add(Vector3 p, Vector3 n, Vector2 uv)
        {
            if (Mirror) { p.x = -p.x; n.x = -n.x; }
            var cp = Xf.MultiplyPoint3x4(p);
            m_V.Add(cp);
            m_N.Add(Unit(Xf.MultiplyVector(n)));
            m_UV.Add(uv);
            m_W.Add(Weights != null ? Weights(cp) : new BoneWeight { boneIndex0 = Bone, weight0 = 1f });
            return m_V.Count - 1;
        }

        void Tri(int a, int b, int c, bool reverse)
        {
            var t = m_T[Sub];
            if (reverse ^ Mirror) { t.Add(a); t.Add(c); t.Add(b); }
            else { t.Add(a); t.Add(b); t.Add(c); }
        }

        /// <summary>
        /// A grid p = f(u, v) over 0..1 x 0..1 (nu x nv cells). closedU joins u = 1 to u = 0. Normals come from the surface
        /// (finite differences) and face away from inside(u, v). UVs follow the surface's own lengths (tile metres a repeat).
        /// </summary>
        public void Grid(int nu, int nv, Func<float, float, Vector3> f, Func<float, float, Vector3> inside, bool closedU, float tile)
        {
            const float e = 1e-3f;
            Vector3 Normal(float u, float v)
            {
                for (int k = 0; k < 4; k++)
                {
                    float vv = Mathf.Clamp(v + (k == 0 ? 0f : (v < 0.5f ? 1f : -1f) * e * 3f * k), 0f, 1f);
                    float u0 = closedU ? u - e : Mathf.Max(0f, u - e), u1 = closedU ? u + e : Mathf.Min(1f, u + e);
                    float v0 = Mathf.Max(0f, vv - e), v1 = Mathf.Min(1f, vv + e);
                    // Unit tangents before the cross product: on a 1 cm part the raw differences are ~1e-5 m and their
                    // cross product ~1e-10, which Vector3.normalized silently turns into a zero normal.
                    var n = Vector3.Cross(Unit(f(u1, vv) - f(u0, vv)), Unit(f(u, v1) - f(u, v0)));
                    if (n.sqrMagnitude > 1e-6f) return Unit(n);
                }
                return Vector3.zero; // degenerate here: ToMesh rebuilds it from the faces around
            }
            float Facing(float u, float v) => Vector3.Dot(Normal(u, v), Unit(f(u, v) - inside(u, v)));
            float facing = Facing(0.5f, 0.5f);
            if (Mathf.Abs(facing) < 0.2f) facing += Facing(0.25f, 0.5f) + Facing(0.75f, 0.5f) + Facing(0.5f, 0.3f) + Facing(0.5f, 0.7f);
            bool flip = facing < 0f;
            Sheet(nu, nv, f, (u, v) => flip ? -Normal(u, v) : Normal(u, v), flip, tile);
        }

        /// <summary>
        /// The vertices and triangles of a grid p = f(u, v) with the given (outward) normals. Triangles wind with
        /// cross(dp/du, dp/dv) as their front, or the other way when <paramref name="reverse"/> is set. UVs follow the
        /// surface's own lengths (tile metres a repeat).
        /// </summary>
        void Sheet(int nu, int nv, Func<float, float, Vector3> f, Func<float, float, Vector3> normal, bool reverse, float tile)
        {
            var pos = new Vector3[nu + 1, nv + 1];
            for (int j = 0; j <= nv; j++) for (int i = 0; i <= nu; i++) pos[i, j] = f(i / (float)nu, j / (float)nv);
            var uvAcc = new Vector2[nu + 1, nv + 1];
            for (int j = 0; j <= nv; j++) for (int i = 1; i <= nu; i++) uvAcc[i, j].x = uvAcc[i - 1, j].x + Vector3.Distance(pos[i, j], pos[i - 1, j]);
            for (int i = 0; i <= nu; i++) for (int j = 1; j <= nv; j++) uvAcc[i, j].y = uvAcc[i, j - 1].y + Vector3.Distance(pos[i, j], pos[i, j - 1]);
            int start = m_V.Count;
            for (int j = 0; j <= nv; j++)
                for (int i = 0; i <= nu; i++)
                    Add(pos[i, j], normal(i / (float)nu, j / (float)nv), uvAcc[i, j] / tile);
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    int a = start + j * (nu + 1) + i, b = a + 1, c = a + nu + 2, d = a + nu + 1;
                    Tri(a, b, c, reverse);
                    Tri(a, c, d, reverse);
                }
        }

        /// <summary>An ellipsoid with exact normals (its parametrisation winds inward, hence reverse).</summary>
        public void Ellipsoid(Vector3 c, Vector3 r, int nu, int nv)
        {
            Vector3 S(float u, float v)
            {
                float th = u * Mathf.PI * 2f, ph = v * Mathf.PI;
                return new Vector3(Mathf.Sin(ph) * Mathf.Sin(th), Mathf.Cos(ph), Mathf.Sin(ph) * Mathf.Cos(th));
            }
            Sheet(nu, nv, (u, v) => c + Vector3.Scale(S(u, v), r),
                (u, v) => { var s = S(u, v); return Unit(new Vector3(s.x / r.x, s.y / r.y, s.z / r.z)); }, true, 0.05f);
        }

        /// <summary>
        /// A tube along path(t) with an elliptic section (x across, y along <paramref name="up"/>), optionally capped or
        /// closed in a loop. Normals are exact for the section (tilted for any taper), not finite differences: on short
        /// tubes the float noise in the path twists neighbouring rings more than they advance.
        /// </summary>
        public void Tube(Func<float, Vector3> path, Func<float, Vector2> radius, int segs, int sides, Vector3 up, bool capStart, bool capEnd, bool loop = false)
        {
            const float e = 0.01f; // tangent baseline, as a fraction of the path
            float Wrap(float t) => loop ? t - Mathf.Floor(t) : t;
            float T0(float t) => loop ? t - e : Mathf.Max(0f, t - e);
            float T1(float t) => loop ? t + e : Mathf.Min(1f, t + e);
            Vector3 Tangent(float t)
            {
                var d = Unit(path(Wrap(T1(t))) - path(Wrap(T0(t))));
                return d.sqrMagnitude > 0.5f ? d : Vector3.forward;
            }
            void Frame(float t, out Vector3 tan, out Vector3 n, out Vector3 b)
            {
                tan = Tangent(t);
                n = Vector3.ProjectOnPlane(up, tan);
                if (n.sqrMagnitude < 1e-8f) n = Vector3.ProjectOnPlane(Mathf.Abs(tan.y) < 0.9f ? Vector3.up : Vector3.right, tan);
                n = Unit(n);
                b = Vector3.Cross(tan, n);
            }
            Vector3 P(float u, float t)
            {
                Frame(t, out _, out var n, out var b);
                var r = radius(Mathf.Clamp01(t));
                float a = u * Mathf.PI * 2f;
                return path(Wrap(t)) + b * (Mathf.Cos(a) * r.x) + n * (Mathf.Sin(a) * r.y);
            }
            Vector3 N(float u, float t)
            {
                Frame(t, out var tan, out var n, out var b);
                float a = u * Mathf.PI * 2f, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                var r = radius(Mathf.Clamp01(t));
                var radial = Unit(b * (ca / Mathf.Max(1e-6f, r.x)) + n * (sa / Mathf.Max(1e-6f, r.y)));
                // Taper: the section's radius at this angle, changing along the tube, tilts the normal back.
                float t0 = T0(t), t1 = T1(t);
                var r0 = radius(Mathf.Clamp01(t0));
                var r1 = radius(Mathf.Clamp01(t1));
                float rho0 = new Vector2(r0.x * ca, r0.y * sa).magnitude, rho1 = new Vector2(r1.x * ca, r1.y * sa).magnitude;
                float ds = (path(Wrap(t1)) - path(Wrap(t0))).magnitude;
                float slope = ds > 1e-6f ? Mathf.Clamp((rho1 - rho0) / ds, -20f, 20f) : 0f;
                return Unit(radial - tan * slope);
            }
            // The rings run from b towards n, so cross(dP/du, dP/dt) points into the tube: wind the other way.
            Sheet(sides, segs, P, N, true, 0.05f);
            if (capStart) Disc(path(0f), -Tangent(0f), radius(0f).x, sides, radius(0f).y, up);
            if (capEnd) Disc(path(1f), Tangent(1f), radius(1f).x, sides, radius(1f).y, up);
        }

        /// <summary>A flat disc (ellipse when ry is given) facing <paramref name="normal"/>.</summary>
        public void Disc(Vector3 c, Vector3 normal, float rx, int sides, float ry = -1f, Vector3? up = null)
        {
            normal = Unit(normal);
            if (ry < 0f) ry = rx;
            var n = Vector3.ProjectOnPlane(up ?? Vector3.up, normal);
            if (n.sqrMagnitude < 1e-8f) n = Vector3.ProjectOnPlane(Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right, normal);
            n = Unit(n);
            var b = Vector3.Cross(normal, n);
            var ring = new List<Vector3>(sides);
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                ring.Add(c + b * (Mathf.Cos(a) * rx) + n * (Mathf.Sin(a) * ry));
            }
            Fan(ring, c, normal);
        }

        /// <summary>A triangle fan over a closed outline, facing <paramref name="normal"/>.</summary>
        public void Fan(IList<Vector3> loop, Vector3 centre, Vector3 normal)
        {
            // Planar UVs (5 cm a repeat) rather than one shared UV, so the tangents are defined everywhere.
            var nn = Unit(normal);
            var ax = Unit(Vector3.Cross(nn, Mathf.Abs(nn.y) < 0.9f ? Vector3.up : Vector3.right));
            var ay = Vector3.Cross(nn, ax);
            Vector2 Uv(Vector3 p) => new Vector2(0.5f + Vector3.Dot(p - centre, ax) / 0.05f, 0.5f + Vector3.Dot(p - centre, ay) / 0.05f);
            int c = Add(centre, normal, Uv(centre));
            int first = m_V.Count;
            foreach (var p in loop) Add(p, normal, Uv(p));
            int n = loop.Count;
            for (int i = 0; i < n; i++)
            {
                var a = loop[i]; var b = loop[(i + 1) % n];
                bool reverse = Vector3.Dot(Vector3.Cross(a - centre, b - centre), normal) < 0f;
                Tri(c, first + i, first + (i + 1) % n, reverse);
            }
        }

        public Mesh ToMesh(string name, Matrix4x4[] bindposes)
        {
            RepairNormals();
            var mesh = new Mesh { name = name };
            if (m_V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(m_V);
            mesh.SetNormals(m_N);
            mesh.SetUVs(0, m_UV);
            mesh.SetTangents(Tangents());
            mesh.boneWeights = m_W.ToArray();
            mesh.bindposes = bindposes;
            mesh.subMeshCount = m_T.Length;
            for (int i = 0; i < m_T.Length; i++) mesh.SetTriangles(m_T[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Any normal still zero or NaN (a degenerate point of a surface) takes the faces around it instead.</summary>
        void RepairNormals()
        {
            bool any = false;
            foreach (var n in m_N) if (!(n.sqrMagnitude > 0.5f)) { any = true; break; }
            if (!any) return;
            var acc = new Vector3[m_N.Count];
            foreach (var t in m_T)
                for (int k = 0; k < t.Count; k += 3)
                {
                    var fn = Vector3.Cross(m_V[t[k + 1]] - m_V[t[k]], m_V[t[k + 2]] - m_V[t[k]]);
                    acc[t[k]] += fn; acc[t[k + 1]] += fn; acc[t[k + 2]] += fn;
                }
            for (int i = 0; i < m_N.Count; i++)
                if (!(m_N[i].sqrMagnitude > 0.5f))
                {
                    var n = Unit(acc[i]);
                    m_N[i] = n.sqrMagnitude > 0.5f ? n : Vector3.up;
                }
        }

        /// <summary>
        /// Per-vertex tangents from the UVs, for the normal-mapped cloth. Triangles with no UV area (where a surface
        /// closes to a point) are skipped rather than dividing by zero, and a vertex no triangle gave a direction to
        /// gets any tangent perpendicular to its normal, so nothing can turn NaN.
        /// </summary>
        List<Vector4> Tangents()
        {
            int count = m_V.Count;
            var tan = new Vector3[count];
            var bit = new Vector3[count];
            foreach (var t in m_T)
                for (int k = 0; k < t.Count; k += 3)
                {
                    int a = t[k], b = t[k + 1], c = t[k + 2];
                    Vector3 e1 = m_V[b] - m_V[a], e2 = m_V[c] - m_V[a];
                    Vector2 d1 = m_UV[b] - m_UV[a], d2 = m_UV[c] - m_UV[a];
                    float det = d1.x * d2.y - d2.x * d1.y;
                    if (!(Mathf.Abs(det) > 1e-10f)) continue;
                    float r = 1f / det;
                    var s = (e1 * d2.y - e2 * d1.y) * r;
                    var w = (e2 * d1.x - e1 * d2.x) * r;
                    tan[a] += s; tan[b] += s; tan[c] += s;
                    bit[a] += w; bit[b] += w; bit[c] += w;
                }
            var result = new List<Vector4>(count);
            for (int i = 0; i < count; i++)
            {
                var n = m_N[i];
                var tt = Unit(tan[i] - n * Vector3.Dot(n, tan[i]));
                if (tt.sqrMagnitude < 0.5f) tt = Unit(Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right));
                float hand = Vector3.Dot(Vector3.Cross(n, tt), bit[i]) < 0f ? -1f : 1f;
                result.Add(new Vector4(tt.x, tt.y, tt.z, hand));
            }
            return result;
        }
    }
}
