using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    public enum Disguise { None, Kandura, ExhibitLinen, RainCoat, LostLuggage, LanternPaper, MirageMirror, PetalWet, Confetti, ClockCoat, OpticianCoat, Mimic }

    public enum Pose { Idle, Walk, Shuffle, Reach, Recoil, Shield, HoldPassport, Rise, Crouch, Point, Run }

    /// <summary>
    /// The Passenger's body: rigid layered plates of charcoal smoked glass with faint sea-green fracture edges, an
    /// empty ID-photo oval for a face, a chest document slot and thin luggage-tag fingers. Animated procedurally
    /// (no skinning). A per-country disguise layer is worn over the same core so it always reads as one entity.
    /// </summary>
    public class PassengerBody : MonoBehaviour
    {
        public Transform Hips, Chest, Neck, Head, FaceRoot;
        public Transform UpperArmL, ForearmL, HandL, UpperArmR, ForearmR, HandR;
        public Transform ThighL, ShinL, ThighR, ShinR;
        public Transform PropHoldR;
        readonly List<Transform> m_FingersL = new List<Transform>(), m_FingersR = new List<Transform>();
        Material m_Glass, m_Edge, m_Face, m_FaceMimic;
        GameObject m_DisguiseRoot;
        GameObject m_PassportProp;
        Disguise m_Current = Disguise.None;
        readonly List<Renderer> m_Renderers = new List<Renderer>();

        public Pose Pose = Pose.Idle;
        public float Speed01;          // 0..1 walking intensity
        public float ReachAmount;      // 0..1
        public float RiseAmount = 1f;  // 0 = under ground, 1 = standing
        float m_Phase, m_Twitch, m_TwitchTarget, m_TwitchTimer;
        float m_PoseBlend;
        Pose m_PrevPose;

        public static PassengerBody Create(Transform parent)
        {
            var go = new GameObject("PassengerBody");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<PassengerBody>();
            b.Build();
            return b;
        }

        void Build()
        {
            // Smoked glass: dark, glossy, mostly opaque. Its edges catch light through the rim layer (TwelfthStamp/Rim), so the
            // silhouette reads in the dark without the body glowing.
            m_Glass = Glass();
            // Bronze seams (approved reference C), darkened so they read as fracture edges rather than panel lines.
            m_Edge = Seam();
            m_Face = FaceMaterial(Tex.Art("passenger_face"));
            m_FaceMimic = FaceMaterial(Tex.Art("passenger_face_mimic"));

            Hips = Bone("Hips", transform, new Vector3(0, 1.08f, 0));
            Chest = Bone("Chest", Hips, new Vector3(0, 0.3f, 0));
            Neck = Bone("Neck", Chest, new Vector3(0, 0.42f, 0));
            Head = Bone("Head", Neck, new Vector3(0, 0.1f, 0));

            // Torso: stacked tapering shards with slight offsets (layered glass).
            Shard(Hips, new Vector3(0, 0.05f, 0), new Vector3(0.3f, 0.2f, 0.17f), new Vector3(0, 0, 2));
            Shard(Hips, new Vector3(0, 0.22f, 0.005f), new Vector3(0.26f, 0.2f, 0.15f), new Vector3(3, 0, -2));
            Shard(Chest, new Vector3(0, 0.1f, 0), new Vector3(0.34f, 0.22f, 0.18f), new Vector3(-2, 0, 1));
            Shard(Chest, new Vector3(0, 0.3f, -0.01f), new Vector3(0.4f, 0.18f, 0.17f), new Vector3(2, 0, -1));
            // Document slot cavity in the chest.
            MeshFactory.Box("DocumentSlot", Chest, new Vector3(0, 0.27f, 0.087f), new Vector3(0.17f, 0.018f, 0.02f), Mats.Lit(new Color(0.01f, 0.01f, 0.012f), 0.2f), false);
            Edge(Chest, new Vector3(0, 0.285f, 0.092f), new Vector3(0.18f, 0.003f, 0.004f));
            Edge(Chest, new Vector3(0.16f, 0.2f, 0.07f), new Vector3(0.004f, 0.32f, 0.004f), new Vector3(0, 0, 8));
            Edge(Chest, new Vector3(-0.15f, 0.12f, 0.07f), new Vector3(0.004f, 0.26f, 0.004f), new Vector3(0, 0, -6));

            // Neck + head shell with the empty photo oval.
            Shard(Neck, new Vector3(0, 0.03f, 0), new Vector3(0.07f, 0.12f, 0.07f), Vector3.zero);
            var shell = MeshFactory.Sphere("HeadShell", Head, new Vector3(0, 0.13f, -0.01f), 1f, m_Glass, 18, 14);
            shell.transform.localScale = new Vector3(0.105f, 0.15f, 0.12f);
            FaceRoot = Bone("Face", Head, new Vector3(0, 0.13f, 0.098f));
            var oval = new MeshBuilder();
            oval.AddDisc(Vector3.zero, Vector3.forward, 1f, 36);
            // Disc UVs come out rotated 180 degrees for a forward-facing disc; turn the mesh to read upright.
            var face = MeshFactory.Make("FaceOval", oval.ToMesh("FaceOval"), m_Face, FaceRoot, Vector3.zero, Quaternion.Euler(0, 0, 180));
            face.transform.localScale = new Vector3(0.078f, 0.108f, 1f);
            var ring = new MeshBuilder();
            ring.AddTorus(Vector3.zero, 1f, 0.06f, 36, 6, Quaternion.Euler(90, 0, 0));
            var rim = MeshFactory.Make("FaceRim", ring.ToMesh("FaceRim"), m_Edge, FaceRoot, new Vector3(0, 0, -0.002f));
            rim.transform.localScale = new Vector3(0.082f, 0.112f, 0.08f);

            // Arms.
            BuildArm(true);
            BuildArm(false);
            // Legs.
            ThighL = Bone("ThighL", Hips, new Vector3(-0.09f, 0f, 0));
            ShinL = Bone("ShinL", ThighL, new Vector3(0, -0.5f, 0));
            ThighR = Bone("ThighR", Hips, new Vector3(0.09f, 0f, 0));
            ShinR = Bone("ShinR", ThighR, new Vector3(0, -0.5f, 0));
            foreach (var (thigh, shin) in new[] { (ThighL, ShinL), (ThighR, ShinR) })
            {
                Limb(thigh, 0.5f, 0.06f, 0.045f);
                Limb(shin, 0.52f, 0.045f, 0.03f);
                Shard(shin, new Vector3(0, -0.55f, 0.04f), new Vector3(0.07f, 0.03f, 0.17f), Vector3.zero);
            }

            UsesModel = ApplyModel();
            foreach (var r in GetComponentsInChildren<Renderer>(true)) m_Renderers.Add(r);
            MakePassportProp();
        }

        /// <summary>True when the Blender-modelled body is in use (false: procedural shards).</summary>
        public bool UsesModel { get; private set; }

        public static Material Glass() => Mats.Lit(new Color(0.042f, 0.047f, 0.052f), 0.86f, 0.12f);
        public static Material Seam() => Mats.Lit(new Color(0.19f, 0.145f, 0.095f), 0.6f, 0.85f, null, false, new Color(0.01f, 0.026f, 0.022f));
        public static Material RimLayer() => Mats.Rim(new Color(0.62f, 0.78f, 0.74f), 3.2f, 0.42f);

        /// <summary>The empty ID photo: a printed, lit oval set inside the face recess (a faint self-light keeps the blank
        /// silhouette legible in the dark without turning it into a lamp).</summary>
        static Material FaceMaterial(Texture2D tex)
        {
            var m = new Material(Mats.Template("Lit_Emissive")) { name = "PassengerFace" };
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.SetTexture("_EmissionMap", tex); }
            m.SetColor("_BaseColor", new Color(0.34f, 0.33f, 0.31f));
            m.SetFloat("_Smoothness", 0.3f);
            m.SetFloat("_Metallic", 0f);
            m.SetColor("_EmissionColor", new Color(0.035f, 0.036f, 0.034f));
            return m;
        }

        /// <summary>
        /// Adds the edge-light layer: a copy of each glass mesh drawn with TwelfthStamp/Rim on every submesh. It lives on the
        /// same transform, so it animates, wears disguises and moves into pockets with the part it lights.
        /// </summary>
        public static void AddRimLayer(GameObject part, Material rim)
        {
            foreach (var mf in part.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.name == "RimLayer") continue;
                var go = new GameObject("RimLayer");
                go.transform.SetParent(mf.transform, false);
                go.layer = mf.gameObject.layer;
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                var mats = new Material[Mathf.Max(1, mf.sharedMesh.subMeshCount)];
                for (int i = 0; i < mats.Length; i++) mats[i] = rim;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        /// <summary>
        /// Swaps the procedural shards for the Blender-modelled parts (Resources/Models/Passenger_Parts, described in
        /// Art/Models/MODELS.md) when that file is present. Every part's origin is its joint, so each hangs off the same
        /// bone the procedural limb used: poses, animation and disguises are unchanged. The face oval and its rim stay
        /// procedural because they carry the photo texture and the mimic swap.
        /// </summary>
        bool ApplyModel()
        {
            var prefab = Resources.Load<GameObject>("Models/Passenger_Parts");
            if (prefab == null) return false;
            var parts = new Dictionary<string, GameObject>();
            foreach (Transform t in prefab.transform) parts[t.name] = t.gameObject;
            foreach (var n in new[] { "Part_Hips", "Part_Chest", "Part_Head", "Part_FacePlate", "Part_UpperArm", "Part_Forearm", "Part_Hand", "Part_FingerTag", "Part_Thigh", "Part_Shin", "Part_Foot" })
                if (!parts.ContainsKey(n)) { Debug.LogWarning("[TS-Passenger] Model is missing " + n + "; keeping the procedural body"); return false; }

            // Remove the procedural meshes, keeping the bones and the face oval + rim.
            var doomed = new List<GameObject>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.name != "FaceOval" && r.name != "FaceRim") doomed.Add(r.gameObject);
            foreach (var g in doomed) DestroyImmediate(g);

            var slotGlass = m_Glass;
            var slotEdge = m_Edge;
            var slotFace = Mats.Lit(new Color(0.02f, 0.022f, 0.026f), 0.9f, 0.25f);
            void Attach(string part, Transform bone, Vector3 offset)
            {
                var go = Instantiate(parts[part], bone, false);
                go.name = part;
                go.transform.localPosition = offset;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        string s = mats[i] != null ? mats[i].name : "";
                        mats[i] = s.Contains("TS_BronzeSeam") ? slotEdge : s.Contains("TS_FacePlate") ? slotFace : slotGlass;
                    }
                    r.sharedMaterials = mats;
                }
            }

            // Head: the model's head origin is the neck top, 5 cm below our head pivot.
            var headOrigin = new Vector3(0f, -0.05f, 0f);
            Attach("Part_Hips", Hips, Vector3.zero);
            Attach("Part_Chest", Chest, Vector3.zero);
            Attach("Part_Head", Head, headOrigin);
            Attach("Part_FacePlate", Head, headOrigin + new Vector3(0f, 0.1552f, 0.0834f));
            // The missing photo sits inside the recess, just over the face plate (the plate is about 1 cm below the head's
            // surface) and slightly smaller than the opening: an empty frame set into the glass, not a lamp.
            FaceRoot.localPosition = headOrigin + new Vector3(0f, 0.1552f, 0.0834f + 0.0045f);
            var oval = FaceRoot.Find("FaceOval");
            if (oval != null) oval.localScale = new Vector3(0.066f, 0.093f, 1f);
            var ring = FaceRoot.Find("FaceRim");
            if (ring != null) ring.gameObject.SetActive(false); // the head shell already has its own recess edge
            // Deliberate asymmetry: a lifted, cracked plate on the left shoulder (added live in Blender, see MODELS.md).
            if (parts.ContainsKey("Part_ShoulderShard")) Attach("Part_ShoulderShard", UpperArmL.parent, new Vector3(0f, 0.015f, 0f));
            else Debug.LogWarning("[TS-Passenger] Passenger_Parts has no Part_ShoulderShard (older export)");
            foreach (var (upper, fore, hand, fingers) in new[] { (UpperArmL, ForearmL, HandL, m_FingersL), (UpperArmR, ForearmR, HandR, m_FingersR) })
            {
                Attach("Part_UpperArm", upper, Vector3.zero);
                Attach("Part_Forearm", fore, Vector3.zero);
                Attach("Part_Hand", hand, Vector3.zero);
                foreach (var f in fingers) Attach("Part_FingerTag", f, Vector3.zero);
            }
            foreach (var (thigh, shin) in new[] { (ThighL, ShinL), (ThighR, ShinR) })
            {
                Attach("Part_Thigh", thigh, Vector3.zero);
                Attach("Part_Shin", shin, Vector3.zero);
                Attach("Part_Foot", shin, new Vector3(0f, -0.52f, 0f));
            }
            // Edge light on every glass part (not the face plate or the photo).
            var rim = RimLayer();
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
                if (r.name.StartsWith("Part_") && r.name != "Part_FacePlate") AddRimLayer(r.gameObject, rim);
            // A slightly dropped left shoulder: it stands like something that learned posture from a photograph.
            UpperArmL.parent.localPosition += new Vector3(0f, -0.022f, 0f);
            return true;
        }

        void BuildArm(bool left)
        {
            float s = left ? -1f : 1f;
            var shoulder = Bone(left ? "ShoulderL" : "ShoulderR", Chest, new Vector3(0.22f * s, 0.36f, 0));
            var upper = Bone(left ? "UpperArmL" : "UpperArmR", shoulder, Vector3.zero);
            var fore = Bone(left ? "ForearmL" : "ForearmR", upper, new Vector3(0, -0.36f, 0));
            var hand = Bone(left ? "HandL" : "HandR", fore, new Vector3(0, -0.36f, 0));
            Limb(upper, 0.36f, 0.045f, 0.035f);
            Limb(fore, 0.36f, 0.035f, 0.025f);
            Shard(hand, new Vector3(0, -0.04f, 0), new Vector3(0.07f, 0.08f, 0.025f), Vector3.zero);
            var fingers = left ? m_FingersL : m_FingersR;
            for (int i = 0; i < 4; i++)
            {
                var f = Bone("Finger" + i, hand, new Vector3((-0.027f + i * 0.018f), -0.08f, 0));
                // Luggage-tag finger: long thin plate with a rounded end and a punched hole.
                var tag = new MeshBuilder();
                tag.AddRoundedPlate(new Vector3(0, -0.075f, 0), 0.014f, 0.15f, 0.006f, 0.004f, Quaternion.identity, 3);
                MeshFactory.FromBuilder("Tag", tag, m_Glass, f);
                MeshFactory.Box("TagHole", f, new Vector3(0, -0.135f, 0), new Vector3(0.006f, 0.006f, 0.006f), m_Edge, false);
                fingers.Add(f);
            }
            if (left) { UpperArmL = upper; ForearmL = fore; HandL = hand; }
            else
            {
                UpperArmR = upper; ForearmR = fore; HandR = hand;
                PropHoldR = Bone("PropHold", hand, new Vector3(0, -0.09f, 0.03f));
            }
        }

        Transform Bone(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        void Shard(Transform parent, Vector3 pos, Vector3 size, Vector3 euler)
        {
            MeshFactory.Box("Shard", parent, pos, size, m_Glass, false, 1f, Quaternion.Euler(euler));
            // Fracture edge highlight along one vertical edge.
            Edge(parent, pos + new Vector3(size.x * 0.5f, 0, size.z * 0.5f), new Vector3(0.004f, size.y * 0.95f, 0.004f), euler);
        }

        void Edge(Transform parent, Vector3 pos, Vector3 size, Vector3 euler = default)
        {
            var e = MeshFactory.Box("Edge", parent, pos, size, m_Edge, false, 1f, Quaternion.Euler(euler));
            e.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Limb(Transform bone, float length, float r0, float r1)
        {
            var b = new MeshBuilder();
            b.AddFrustum(Vector3.zero, r1, r0, length, 6, true, true, 1f, Quaternion.identity);
            var go = MeshFactory.FromBuilder("Limb", b, m_Glass, bone);
            go.transform.localPosition = new Vector3(0, -length, 0);
            Edge(bone, new Vector3(r0 * 0.8f, -length * 0.5f, r0 * 0.4f), new Vector3(0.003f, length * 0.9f, 0.003f));
        }

        void MakePassportProp()
        {
            m_PassportProp = new GameObject("ImitationPassport");
            m_PassportProp.transform.SetParent(PropHoldR, false);
            var dark = Mats.Lit(new Color(0.04f, 0.05f, 0.08f), 0.6f, 0.2f);
            MeshFactory.Box("CoverL", m_PassportProp.transform, new Vector3(-0.055f, 0, 0), new Vector3(0.105f, 0.15f, 0.006f), dark, false, 1f, Quaternion.Euler(0, 18, 0));
            MeshFactory.Box("CoverR", m_PassportProp.transform, new Vector3(0.055f, 0, 0), new Vector3(0.105f, 0.15f, 0.006f), dark, false, 1f, Quaternion.Euler(0, -18, 0));
            var blank = Mats.Lit(new Color(0.55f, 0.55f, 0.52f), 0.2f);
            MeshFactory.Box("PageL", m_PassportProp.transform, new Vector3(-0.053f, 0, -0.005f), new Vector3(0.098f, 0.142f, 0.003f), blank, false, 1f, Quaternion.Euler(0, 18, 0));
            MeshFactory.Box("PageR", m_PassportProp.transform, new Vector3(0.053f, 0, -0.005f), new Vector3(0.098f, 0.142f, 0.003f), blank, false, 1f, Quaternion.Euler(0, -18, 0));
            m_PassportProp.transform.localRotation = Quaternion.Euler(-70, 0, 0);
            m_PassportProp.SetActive(false);
            foreach (var r in m_PassportProp.GetComponentsInChildren<Renderer>(true)) m_Renderers.Add(r);
        }

        public IEnumerable<Renderer> Renderers => m_Renderers;

        // ------------------------------------------------------------------ disguises
        readonly List<GameObject> m_DisguiseParts = new List<GameObject>();

        public void SetDisguise(Disguise d)
        {
            if (d == m_Current && m_DisguiseRoot != null) return;
            m_Current = d;
            // Pieces hung directly on bones (wraps, umbrella, hair...) belong to the old disguise too.
            foreach (var g in m_DisguiseParts)
            {
                if (g == null) continue;
                foreach (var r in g.GetComponentsInChildren<Renderer>(true)) m_Renderers.Remove(r);
                Destroy(g);
            }
            m_DisguiseParts.Clear();
            if (m_DisguiseRoot != null)
            {
                foreach (var r in m_DisguiseRoot.GetComponentsInChildren<Renderer>(true)) m_Renderers.Remove(r);
                Destroy(m_DisguiseRoot);
            }
            var before = new HashSet<Transform>(GetComponentsInChildren<Transform>(true));
            m_DisguiseRoot = new GameObject("Disguise_" + d);
            m_DisguiseRoot.transform.SetParent(transform, false);
            DisguiseKit.Build(this, d, m_DisguiseRoot.transform);
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (!before.Contains(t) && !t.IsChildOf(m_DisguiseRoot.transform) && t.parent != null && before.Contains(t.parent))
                    m_DisguiseParts.Add(t.gameObject);
            foreach (var r in m_DisguiseRoot.GetComponentsInChildren<Renderer>(true)) m_Renderers.Add(r);
            foreach (var g in m_DisguiseParts) foreach (var r in g.GetComponentsInChildren<Renderer>(true)) m_Renderers.Add(r);
            var face = FaceRoot.Find("FaceOval")?.GetComponent<Renderer>();
            if (face != null) face.sharedMaterial = d == Disguise.Mimic ? m_FaceMimic : d == Disguise.ClockCoat ? Mats.Unlit(new Color(0.85f, 0.82f, 0.76f), Tex.Art("stamp_uk"), false, 0.7f) : m_Face;
            MeshFactory.NoShadows(m_DisguiseRoot);
        }

        public Disguise CurrentDisguise => m_Current;

        public void ShowImitationPassport(bool on) => m_PassportProp.SetActive(on);

        // ------------------------------------------------------------------ procedural animation
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            m_Phase += dt * (Pose == Pose.Shuffle ? 2.2f : Pose == Pose.Run ? 8.2f : 3.6f) * Mathf.Max(0.15f, Speed01);
            if (Pose != m_PrevPose) { m_PoseBlend = 0f; m_PrevPose = Pose; }
            m_PoseBlend = Mathf.MoveTowards(m_PoseBlend, 1f, dt * 3.5f);

            // Uncanny stop-motion twitches of the head.
            m_TwitchTimer -= dt;
            if (m_TwitchTimer <= 0f)
            {
                m_TwitchTimer = Random.Range(0.4f, 2.2f);
                m_TwitchTarget = Random.Range(-14f, 14f);
            }
            m_Twitch = Mathf.MoveTowards(m_Twitch, m_TwitchTarget, dt * 220f);

            float walk = Mathf.Sin(m_Phase);
            float breathe = Mathf.Sin(Time.time * 1.3f) * 0.012f;
            float swing = (Pose == Pose.Walk ? 26f : Pose == Pose.Shuffle ? 10f : 0f) * Speed01;

            Quaternion upperL = Quaternion.Euler(walk * swing * 0.8f, 0, 6f), upperR = Quaternion.Euler(-walk * swing * 0.8f, 0, -6f);
            Quaternion foreL = Quaternion.Euler(-10, 0, 0), foreR = Quaternion.Euler(-10, 0, 0);
            Quaternion thighL = Quaternion.Euler(-walk * swing, 0, 0), thighR = Quaternion.Euler(walk * swing, 0, 0);
            Quaternion shinL = Quaternion.Euler(Mathf.Max(0, walk) * swing * 1.2f, 0, 0), shinR = Quaternion.Euler(Mathf.Max(0, -walk) * swing * 1.2f, 0, 0);
            Quaternion chest = Quaternion.Euler(2f + breathe * 100f, walk * swing * 0.15f, 0);
            Quaternion head = Quaternion.Euler(6f, 0, m_Twitch + 7f); // a constant tilt: deliberate asymmetry
            float fingerCurl = 8f;
            float hipsY = 1.08f + Mathf.Abs(walk) * 0.02f * Speed01;

            switch (Pose)
            {
                case Pose.Shuffle:
                    upperL = Quaternion.Euler(-78f + walk * 6f, 0, 8f);
                    upperR = Quaternion.Euler(-82f - walk * 6f, 0, -8f);
                    foreL = foreR = Quaternion.Euler(-8f, 0, 0);
                    chest = Quaternion.Euler(14f, walk * 6f, walk * 3f);
                    head = Quaternion.Euler(18f, 0, 22f + m_Twitch * 0.5f);
                    break;
                case Pose.Reach:
                    upperR = Quaternion.Euler(Mathf.Lerp(-10f, -88f, ReachAmount), 0, -4f);
                    foreR = Quaternion.Euler(Mathf.Lerp(-30f, -2f, ReachAmount), 0, 0);
                    chest = Quaternion.Euler(Mathf.Lerp(2f, 16f, ReachAmount), -12f * ReachAmount, 0);
                    head = Quaternion.Euler(10f, 0, m_Twitch * 0.3f);
                    fingerCurl = Mathf.Lerp(30f, -12f, ReachAmount);
                    break;
                case Pose.Recoil:
                case Pose.Shield:
                    upperL = Quaternion.Euler(-120f, 20f, 25f);
                    upperR = Quaternion.Euler(-115f, -20f, -25f);
                    foreL = Quaternion.Euler(-95f, 0, 0);
                    foreR = Quaternion.Euler(-100f, 0, 0);
                    chest = Quaternion.Euler(-14f, 18f, 0);
                    head = Quaternion.Euler(-12f, 40f, 10f + m_Twitch * 0.3f);
                    fingerCurl = 55f;
                    break;
                case Pose.HoldPassport:
                    upperL = Quaternion.Euler(-40f, 12f, 18f);
                    upperR = Quaternion.Euler(-42f, -12f, -18f);
                    foreL = Quaternion.Euler(-70f, 0, 0);
                    foreR = Quaternion.Euler(-70f, 0, 0);
                    head = Quaternion.Euler(28f, 0, m_Twitch * 0.2f);
                    break;
                case Pose.Crouch:
                    hipsY = 0.75f;
                    thighL = thighR = Quaternion.Euler(-70f, 0, 0);
                    shinL = shinR = Quaternion.Euler(95f, 0, 0);
                    chest = Quaternion.Euler(35f, 0, 0);
                    head = Quaternion.Euler(-30f, 0, m_Twitch);
                    break;
                case Pose.Point:
                    upperR = Quaternion.Euler(-85f, 15f, 0);
                    foreR = Quaternion.Euler(-5f, 0, 0);
                    break;
                case Pose.Run:
                    // A loping sprint, leaning in, both arms reaching ahead with splayed fingers: what you see behind you.
                    upperL = Quaternion.Euler(-70f + walk * 16f, 0, 10f);
                    upperR = Quaternion.Euler(-76f - walk * 16f, 0, -10f);
                    foreL = foreR = Quaternion.Euler(-14f, 0, 0);
                    thighL = Quaternion.Euler(-walk * 48f * Speed01 - 8f, 0, 0);
                    thighR = Quaternion.Euler(walk * 48f * Speed01 - 8f, 0, 0);
                    shinL = Quaternion.Euler(Mathf.Max(0, walk) * 85f * Speed01 + 10f, 0, 0);
                    shinR = Quaternion.Euler(Mathf.Max(0, -walk) * 85f * Speed01 + 10f, 0, 0);
                    chest = Quaternion.Euler(24f, walk * 9f, 0);
                    head = Quaternion.Euler(-16f, 0, m_Twitch * 0.6f + 7f); // chin up: it keeps its face on you
                    fingerCurl = -12f;
                    hipsY = 1.0f + Mathf.Abs(walk) * 0.07f * Speed01;
                    break;
            }

            float k = 1f - Mathf.Pow(1f - m_PoseBlend, 3f);
            Blend(UpperArmL, upperL, k); Blend(UpperArmR, upperR, k);
            Blend(ForearmL, foreL, k); Blend(ForearmR, foreR, k);
            Blend(ThighL, thighL, k); Blend(ThighR, thighR, k);
            Blend(ShinL, shinL, k); Blend(ShinR, shinR, k);
            Blend(Chest, chest, k); Blend(Head, head, k);
            foreach (var f in m_FingersL) f.localRotation = Quaternion.Slerp(f.localRotation, Quaternion.Euler(fingerCurl, 0, 0), dt * 8f);
            foreach (var f in m_FingersR) f.localRotation = Quaternion.Slerp(f.localRotation, Quaternion.Euler(fingerCurl, 0, 0), dt * 8f);

            float rise = Mathf.Clamp01(RiseAmount);
            Hips.localPosition = new Vector3(0, Mathf.Lerp(-1.3f, hipsY, rise), 0);
            Chest.localScale = new Vector3(1f, 1f + breathe, 1f);
        }

        static void Blend(Transform t, Quaternion target, float k)
        {
            t.localRotation = Quaternion.Slerp(t.localRotation, target, Mathf.Clamp01(k * 0.35f + Time.deltaTime * 6f));
        }
    }
}
