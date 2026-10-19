using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// A small flock that makes a place feel alive and notices you: birds peck and hop about on the ground, then burst
    /// into the air (a clatter of wings) when the player comes close or runs nearby, or when the Passenger walks through.
    /// They climb away from whatever startled them, wheel round high up, and drift back down to the same patch once it
    /// has been quiet for a while. Low-poly, unlit by shadows, world motion only.
    /// </summary>
    public class AmbientBirds : MonoBehaviour
    {
        public enum Kind { Pigeon, Gull, Crow, Sparrow, Dove }

        enum S { Ground, Flee, Circle, Land }

        class Bird
        {
            public Transform Root, WingL, WingR, Head;
            public S State;
            public Vector3 Vel, Target;
            public float Timer, Flap, Peck, Hop, Speed;
            public float Orbit, OrbitR, OrbitH;
        }

        /// <summary>Every flock in a built cell.</summary>
        public static readonly List<AmbientBirds> All = new List<AmbientBirds>();

        public float Radius = 3f;
        public float StartleDistance = 4.2f, RunStartleDistance = 8f;
        /// <summary>How many times this flock has taken off (tests).</summary>
        public int TakeOffs { get; private set; }
        public bool Airborne { get; private set; }

        readonly List<Bird> m_Birds = new List<Bird>();
        Kind m_Kind;
        float m_Quiet;

        /// <summary>A flock of <paramref name="count"/> birds pecking about a local floor point within <paramref name="radius"/>.</summary>
        public static AmbientBirds Create(Transform parent, Vector3 localCenter, float radius, int count, Kind kind)
        {
            var go = new GameObject("Birds_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            var f = go.AddComponent<AmbientBirds>();
            f.Radius = radius;
            f.m_Kind = kind;
            f.Build(Mathf.Clamp(count, 1, 14));
            return f;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Build(int count)
        {
            Color body, wing, beak;
            float size;
            switch (m_Kind)
            {
                case Kind.Gull: body = new Color(0.9f, 0.9f, 0.88f); wing = new Color(0.62f, 0.64f, 0.66f); beak = new Color(0.95f, 0.75f, 0.2f); size = 1.25f; break;
                case Kind.Crow: body = new Color(0.07f, 0.07f, 0.08f); wing = new Color(0.05f, 0.05f, 0.06f); beak = new Color(0.08f, 0.08f, 0.08f); size = 1.15f; break;
                case Kind.Sparrow: body = new Color(0.52f, 0.4f, 0.28f); wing = new Color(0.38f, 0.28f, 0.18f); beak = new Color(0.3f, 0.25f, 0.2f); size = 0.62f; break;
                case Kind.Dove: body = new Color(0.92f, 0.92f, 0.9f); wing = new Color(0.86f, 0.86f, 0.84f); beak = new Color(0.8f, 0.6f, 0.5f); size = 0.95f; break;
                default: body = new Color(0.5f, 0.52f, 0.56f); wing = new Color(0.4f, 0.42f, 0.46f); beak = new Color(0.25f, 0.22f, 0.2f); size = 1f; break;
            }
            var bodyMat = Mats.Lit(body, 0.25f);
            var wingMat = Mats.Lit(wing, 0.2f);
            var beakMat = Mats.Lit(beak, 0.4f);
            for (int i = 0; i < count; i++)
            {
                var b = new Bird();
                b.Root = new GameObject("Bird").transform;
                b.Root.SetParent(transform, false);
                b.Root.localScale = Vector3.one * size * Random.Range(0.9f, 1.1f);
                var torso = MeshFactory.Sphere("Body", b.Root, new Vector3(0f, 0.09f, 0f), 0.07f, bodyMat, 10, 7);
                torso.transform.localScale = new Vector3(0.85f, 0.8f, 1.45f);
                b.Head = MeshFactory.Sphere("Head", b.Root, new Vector3(0f, 0.17f, 0.09f), 0.04f, bodyMat, 8, 6).transform;
                MeshFactory.Box("Beak", b.Head, new Vector3(0f, -0.005f, 0.045f), new Vector3(0.014f, 0.012f, 0.03f), beakMat, false);
                MeshFactory.Box("Tail", b.Root, new Vector3(0f, 0.1f, -0.13f), new Vector3(0.07f, 0.012f, 0.08f), wingMat, false, 1f, Quaternion.Euler(-12f, 0f, 0f));
                b.WingL = Wing(b.Root, wingMat, -1f);
                b.WingR = Wing(b.Root, wingMat, 1f);
                foreach (var r in b.Root.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
                b.Target = RandomGround();
                b.Root.localPosition = b.Target;
                b.Root.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                b.Timer = Random.Range(0.5f, 3f);
                b.Flap = Random.value * 10f;
                b.Speed = Random.Range(0.9f, 1.15f);
                m_Birds.Add(b);
            }
        }

        static Transform Wing(Transform root, Material mat, float side)
        {
            var pivot = new GameObject(side < 0 ? "WingL" : "WingR").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(side * 0.045f, 0.12f, 0.01f);
            // Double-sided feathered blade: two quads back to back, tip out along +/-X.
            MeshFactory.Quad("Top", pivot, 0.17f, 0.09f, mat, new Vector3(side * 0.085f, 0.001f, -0.01f), Quaternion.Euler(90f, 0f, 0f));
            MeshFactory.Quad("Under", pivot, 0.17f, 0.09f, mat, new Vector3(side * 0.085f, -0.001f, -0.01f), Quaternion.Euler(-90f, 0f, 0f));
            return pivot;
        }

        /// <summary>Tests only: every bird back on the ground at once, as if it had been quiet for a long while.</summary>
        public void SettleNow()
        {
            foreach (var b in m_Birds)
            {
                b.State = S.Ground;
                b.Target = RandomGround();
                b.Root.localPosition = b.Target;
                b.Vel = Vector3.zero;
                b.Hop = 0f;
                b.Timer = Random.Range(0.5f, 2f);
            }
            Airborne = false;
            m_Quiet = 0f;
        }

        Vector3 RandomGround()
        {
            var c = Random.insideUnitCircle * Radius;
            return new Vector3(c.x, 0f, c.y);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || m_Birds.Count == 0) return;
            var rig = Game.Rig;
            Vector3 startle = Vector3.zero;
            bool scare = false;
            if (rig != null)
            {
                var d = rig.Feet - transform.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist < StartleDistance || (rig.Sprinting && dist < RunStartleDistance)) { scare = true; startle = rig.Feet; }
            }
            var pc = Game.Passenger;
            if (!scare && pc != null && pc.Visible && !pc.InPocket)
            {
                var d = pc.transform.position - transform.position; d.y = 0f;
                if (d.magnitude < StartleDistance) { scare = true; startle = pc.transform.position; }
            }

            if (scare)
            {
                m_Quiet = 0f;
                if (!Airborne) TakeOff(startle);
            }
            else if (Airborne)
            {
                m_Quiet += dt;
                if (m_Quiet > 14f && (rig == null || Vector3.Distance(rig.Feet, transform.position) > StartleDistance + 3f))
                {
                    foreach (var b in m_Birds) if (b.State == S.Circle) { b.State = S.Land; b.Target = RandomGround(); }
                }
            }

            bool any = false;
            foreach (var b in m_Birds)
            {
                StepBird(b, dt);
                if (b.State != S.Ground) any = true;
            }
            Airborne = any;
        }

        void TakeOff(Vector3 cause)
        {
            TakeOffs++;
            Airborne = true;
            var away = transform.position - cause; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;
            away.Normalize();
            foreach (var b in m_Birds)
            {
                b.State = S.Flee;
                b.Timer = Random.Range(1.4f, 2.2f);
                var spread = Quaternion.Euler(0f, Random.Range(-55f, 55f), 0f) * away;
                b.Vel = transform.InverseTransformDirection(spread) * Random.Range(3f, 4.5f) + Vector3.up * Random.Range(3.5f, 5f);
                b.Orbit = Random.Range(0f, 360f);
                b.OrbitR = Random.Range(7f, 12f);
                b.OrbitH = Random.Range(7f, 11f);
            }
            AudioDirector.I?.Play("birds_flutter_" + Random.Range(1, 4), transform.position + Vector3.up * 0.4f, 0.85f, Random.Range(0.92f, 1.08f));
        }

        void StepBird(Bird b, float dt)
        {
            var t = b.Root;
            switch (b.State)
            {
                case S.Ground:
                {
                    // Peck, look about, hop to a new spot now and then.
                    b.Timer -= dt;
                    b.Peck += dt * 9f * b.Speed;
                    float peck = Mathf.Max(0f, Mathf.Sin(b.Peck)) * (Mathf.PerlinNoise(b.Peck * 0.1f, b.Speed) > 0.45f ? 1f : 0f);
                    b.Head.localRotation = Quaternion.Euler(peck * 55f, Mathf.Sin(b.Peck * 0.23f) * 25f, 0f);
                    if (b.Hop > 0f)
                    {
                        b.Hop -= dt;
                        var lp = Vector3.MoveTowards(t.localPosition, b.Target, dt * 0.9f);
                        lp.y = Mathf.Abs(Mathf.Sin(b.Hop * 18f)) * 0.04f;
                        t.localPosition = lp;
                    }
                    else if (b.Timer <= 0f)
                    {
                        b.Timer = Random.Range(1.2f, 4f);
                        b.Target = RandomGround();
                        var dir = b.Target - t.localPosition; dir.y = 0f;
                        if (dir.sqrMagnitude > 0.01f) t.localRotation = Quaternion.LookRotation(dir, Vector3.up);
                        b.Hop = Mathf.Min(1.2f, dir.magnitude / 0.9f);
                    }
                    Fold(b, dt);
                    break;
                }
                case S.Flee:
                {
                    b.Timer -= dt;
                    b.Vel += Vector3.down * 1.2f * dt;
                    t.localPosition += b.Vel * dt;
                    Face(b, b.Vel, dt, 10f);
                    Beat(b, dt, 15f, 70f);
                    if (b.Timer <= 0f) b.State = S.Circle;
                    break;
                }
                case S.Circle:
                {
                    // Wheel round high over the patch, gliding between bursts of beats.
                    b.Orbit += dt * (5f / Mathf.Max(4f, b.OrbitR)) * Mathf.Rad2Deg * b.Speed; // about 5 m/s round the circle
                    var goal = new Vector3(Mathf.Cos(b.Orbit * Mathf.Deg2Rad) * b.OrbitR, b.OrbitH, Mathf.Sin(b.Orbit * Mathf.Deg2Rad) * b.OrbitR);
                    var to = goal - t.localPosition;
                    b.Vel = Vector3.Lerp(b.Vel, to.normalized * 5f, dt * 1.5f);
                    t.localPosition += b.Vel * dt;
                    Face(b, b.Vel, dt, 4f);
                    bool glide = Mathf.Sin(Time.time * 0.9f + b.Speed * 7f) > 0.2f;
                    Beat(b, dt, glide ? 0f : 9f, glide ? 8f : 50f);
                    break;
                }
                case S.Land:
                {
                    var to = b.Target - t.localPosition;
                    float dist = to.magnitude;
                    var want = dist > 0.01f ? to / dist * Mathf.Clamp(dist * 1.2f, 0.8f, 5f) : Vector3.zero;
                    b.Vel = Vector3.Lerp(b.Vel, want, dt * 2f);
                    t.localPosition += b.Vel * dt;
                    Face(b, new Vector3(b.Vel.x, 0f, b.Vel.z), dt, 5f);
                    Beat(b, dt, dist < 2f ? 14f : 7f, dist < 2f ? 65f : 35f);
                    if (dist < 0.08f)
                    {
                        t.localPosition = b.Target;
                        b.State = S.Ground;
                        b.Timer = Random.Range(0.8f, 2.5f);
                        b.Vel = Vector3.zero;
                    }
                    break;
                }
            }
        }

        static void Face(Bird b, Vector3 vel, float dt, float rate)
        {
            if (vel.sqrMagnitude < 0.01f) return;
            var want = Quaternion.LookRotation(vel.normalized, Vector3.up);
            b.Root.localRotation = Quaternion.Slerp(b.Root.localRotation, want, 1f - Mathf.Exp(-rate * dt));
            b.Head.localRotation = Quaternion.Slerp(b.Head.localRotation, Quaternion.identity, dt * 6f);
        }

        static void Beat(Bird b, float dt, float hz, float amplitude)
        {
            b.Flap += dt * hz * Mathf.PI * 2f;
            float a = hz > 0f ? Mathf.Sin(b.Flap) * amplitude : Mathf.Lerp(0f, amplitude, 0.5f);
            b.WingL.localRotation = Quaternion.Euler(0f, 0f, a);
            b.WingR.localRotation = Quaternion.Euler(0f, 0f, -a);
        }

        static void Fold(Bird b, float dt)
        {
            b.WingL.localRotation = Quaternion.Slerp(b.WingL.localRotation, Quaternion.Euler(0f, -70f, -8f), dt * 8f);
            b.WingR.localRotation = Quaternion.Slerp(b.WingR.localRotation, Quaternion.Euler(0f, 70f, 8f), dt * 8f);
        }
    }
}
