using System.Collections;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// The Passenger's arm pushing through an open tear from the other side ("open tears work both ways"). Built from the
    /// same Blender parts as its body (Resources/Models/Passenger_Parts: upper arm, forearm, hand, four luggage-tag
    /// fingers) with the same smoked glass and edge light, so the hand that comes through is visibly the same creature.
    /// Rendered as ordinary world geometry: the part behind the tear plane is hidden by the opening's stencil clear, so the
    /// fingers appear to emerge from the hole. Sealing in time catches it (near miss); otherwise it breaches.
    /// </summary>
    public class PassengerHand : MonoBehaviour
    {
        Transform m_Arm;
        readonly Transform[] m_Fingers = new Transform[4];
        Tear m_Tear;
        float m_Progress, m_Attached;
        bool m_Recoiling;
        AudioSource m_Tap;

        public float Progress => m_Progress;
        public bool Active => m_Tear != null && !m_Recoiling;
        /// <summary>True when the modelled parts are in use (false: the procedural fallback, logged as a warning).</summary>
        public bool UsesModel { get; private set; }
        /// <summary>How far the fingertips are in front of the tear plane (metres; negative = still behind it).</summary>
        public float FingertipsInFront
        {
            get
            {
                if (m_Tear == null || m_Fingers[1] == null) return -1f;
                var tip = m_Fingers[1].TransformPoint(new Vector3(0f, 0f, 0.15f));
                return Vector3.Dot(tip - m_Tear.OpeningCenter, m_Tear.Anchor.Forward);
            }
        }

        public static PassengerHand Create(Transform parent)
        {
            var go = new GameObject("PassengerHand");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<PassengerHand>();
            h.Build();
            go.SetActive(false);
            return h;
        }

        void Build()
        {
            var glass = PassengerBody.Glass();
            var edge = PassengerBody.Seam();
            m_Arm = new GameObject("Arm").transform;
            m_Arm.SetParent(transform, false);
            UsesModel = BuildFromModel(glass, edge);
            if (!UsesModel)
            {
                Debug.LogWarning("[TS-Passenger] Passenger_Parts is missing: the hand through the tear uses its procedural fallback");
                BuildProcedural(glass, edge);
            }
            MeshFactory.NoShadows(gameObject);
        }

        bool BuildFromModel(Material glass, Material edge)
        {
            var prefab = Resources.Load<GameObject>("Models/Passenger_Parts");
            if (prefab == null) return false;
            GameObject Find(string n)
            {
                foreach (Transform t in prefab.transform) if (t.name == n) return t.gameObject;
                return null;
            }
            var upper = Find("Part_UpperArm");
            var fore = Find("Part_Forearm");
            var hand = Find("Part_Hand");
            var tag = Find("Part_FingerTag");
            if (upper == null || fore == null || hand == null || tag == null) return false;
            var rim = PassengerBody.RimLayer();
            // The parts hang along -Y from their joints; the reaching arm points along +Z (out of the tear).
            var along = Quaternion.Euler(-90f, 0f, 0f);
            Part(upper, m_Arm, new Vector3(0f, 0f, -0.72f), along, glass, edge, rim);
            Part(fore, m_Arm, new Vector3(0f, 0f, -0.36f), along, glass, edge, rim);
            Part(hand, m_Arm, Vector3.zero, along, glass, edge, rim);
            float[] knuckles = { -0.0285f, -0.0095f, 0.0095f, 0.0285f };
            for (int i = 0; i < 4; i++)
            {
                var f = new GameObject("Finger" + i).transform;
                f.SetParent(m_Arm, false);
                f.localPosition = new Vector3(knuckles[i], 0f, 0.09f);
                Part(tag, f, Vector3.zero, along, glass, edge, rim);
                m_Fingers[i] = f;
            }
            return true;
        }

        static void Part(GameObject src, Transform parent, Vector3 pos, Quaternion rot, Material glass, Material edge, Material rim)
        {
            var go = Instantiate(src, parent, false);
            go.name = src.name;
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = Vector3.one;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = mats[i] != null && mats[i].name.Contains("TS_BronzeSeam") ? edge : glass;
                r.sharedMaterials = mats;
            }
            PassengerBody.AddRimLayer(go, rim);
        }

        void BuildProcedural(Material glass, Material edge)
        {
            var fore = new MeshBuilder();
            fore.AddFrustum(Vector3.zero, 0.04f, 0.032f, 0.75f, 7, true, true, 1f, Quaternion.Euler(90, 0, 0));
            MeshFactory.FromBuilder("Forearm", fore, glass, m_Arm).transform.localPosition = new Vector3(0, 0, -0.75f);
            MeshFactory.Box("Palm", m_Arm, new Vector3(0, 0, 0.04f), new Vector3(0.08f, 0.025f, 0.09f), glass, false);
            for (int i = 0; i < 4; i++)
            {
                var f = new GameObject("Finger" + i).transform;
                f.SetParent(m_Arm, false);
                f.localPosition = new Vector3(-0.03f + i * 0.02f, 0, 0.085f);
                var tag = new MeshBuilder();
                tag.AddRoundedPlate(Vector3.zero, 0.014f, 0.15f, 0.006f, 0.004f, Quaternion.Euler(90, 0, 0), 3);
                MeshFactory.FromBuilder("Tag", tag, glass, f).transform.localPosition = new Vector3(0, 0, 0.075f);
                MeshFactory.Box("Hole", f, new Vector3(0, 0, 0.135f), new Vector3(0.006f, 0.006f, 0.006f), edge, false);
                m_Fingers[i] = f;
            }
        }

        public void Attach(Tear tear)
        {
            m_Tear = tear;
            m_Recoiling = false;
            m_Progress = 0f;
            m_Attached = 0f;
            gameObject.SetActive(true);
            m_Tap = AudioDirector.I?.Loop("pass_tap", transform, 0.7f, Bus.SFX, 1f, 1.2f, 12f);
            Hud.I?.ShowCaption("glass fingertips tap at the edge of the tear", 3f);
        }

        /// <summary>0 = just behind the opening, 1 = fully through and gripping the edge. Within about a second of
        /// appearing the hand is through the opening whatever the contamination, so the player sees what is coming.</summary>
        public void SetProgress(float p) => m_Progress = Mathf.Clamp01(Mathf.Max(p, Mathf.Min(0.6f, m_Attached / 1.2f)));

        void LateUpdate()
        {
            if (m_Tear == null) return;
            if (!m_Recoiling) m_Attached += Time.deltaTime;
            var a = m_Tear.Anchor;
            float w = m_Tear.Width;
            // Enter low on one side of the opening, reaching outwards along the seam's forward; anticipation (a pause
            // behind the edge) is in the progress curve, the settle is the fingers curling round the rim.
            var basePos = m_Tear.OpeningCenter + a.transform.right * Mathf.Min(0.12f, w * 0.15f) + Vector3.down * 0.15f;
            float depth = Mathf.Lerp(-0.35f, 0.55f, Mathf.SmoothStep(0f, 1f, m_Progress));
            transform.position = basePos + a.Forward * depth;
            transform.rotation = Quaternion.LookRotation(a.Forward, Vector3.up) * Quaternion.Euler(-12f + Mathf.Sin(Time.time * 7f) * 2f, Mathf.Sin(Time.time * 2.3f) * 6f, 0);
            float curl = Mathf.Lerp(-10f, 70f, Mathf.InverseLerp(0.6f, 1f, m_Progress)) + Mathf.Sin(Time.time * 9f) * 6f;
            for (int i = 0; i < 4; i++) m_Fingers[i].localRotation = Quaternion.Euler(curl + i * 4f, (i - 1.5f) * 6f, 0);
        }

        public void Recoil()
        {
            if (m_Tear == null || m_Recoiling) return;
            m_Recoiling = true;
            StartCoroutine(RecoilRoutine());
        }

        IEnumerator RecoilRoutine()
        {
            AudioDirector.I?.Play("pass_recoil", transform.position, 1f, 1.1f, Bus.SFX, true);
            if (m_Tap != null) { AudioDirector.I.FadeOutAndStop(m_Tap, 0.2f); m_Tap = null; }
            float start = m_Progress;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.3f;
                m_Progress = Mathf.Lerp(start, 0f, t);
                yield return null;
            }
            Detach();
        }

        public void Detach()
        {
            if (m_Tap != null) { AudioDirector.I?.FadeOutAndStop(m_Tap, 0.2f); m_Tap = null; }
            m_Tear = null;
            m_Recoiling = false;
            gameObject.SetActive(false);
        }
    }
}
