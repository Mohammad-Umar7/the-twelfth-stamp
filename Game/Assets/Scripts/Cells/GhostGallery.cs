using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Germany: the Ghost Shooting Gallery, the finale's boss game. Once the eleven seals are validated a brass torch holding
    /// Egypt's sunlight rises on the registration plinth. With it in hand the Passenger starts flickering in at one place
    /// after another round the hall: each time it holds still a moment with a glowing target on its chest, then walks
    /// slowly toward you before it flickers away again. Aim and fire (desktop E or a click, VR trigger): each shot draws a
    /// beam of light from the torch to where it lands, with a sizzle and a flash. A miss costs nothing; five hits and it is
    /// spent: it staggers and kneels before the false doors (the strength pips over its head, and in the objective, run
    /// out). If it reaches you while it is out, that is the usual catch.
    /// </summary>
    public class GhostGallery : MonoBehaviour
    {
        public const int HitsToWin = 5;
        /// <summary>How far from the target's centre a shot still counts (a generous chest-sized target).</summary>
        public const float HitRadius = 0.42f;
        public enum Phase { Idle, Away, Holding, Walking, Struck, Weakening, Kneeling }

        public BerlinCell Cell;
        public Grabbable Torch;
        /// <summary>Where it can flicker in (hall local space), and where it kneels once it is spent.</summary>
        public readonly List<Vector3> Spots = new List<Vector3>();
        public Vector3 KneelLocal = new Vector3(6.9f, 0f, 0f);
        public Phase State { get; private set; } = Phase.Idle;
        public int Hits { get; private set; }
        public int Shots { get; private set; }
        public int Appearances { get; private set; }
        public int Strength => Mathf.Max(0, HitsToWin - Hits);
        /// <summary>The game is on (the torch was taken and it has not been worn down yet).</summary>
        public bool Running => State == Phase.Away || State == Phase.Holding || State == Phase.Walking || State == Phase.Struck;
        public bool Weakened => State == Phase.Weakening || State == Phase.Kneeling;
        public bool TorchOut => Torch != null && Torch.gameObject.activeInHierarchy;
        public bool TorchHeld => Torch != null && Torch.Holder != null;
        /// <summary>It is out with the target on its chest: a shot can hit it now.</summary>
        public bool TargetUp => (State == Phase.Holding || State == Phase.Walking) && PassengerHere;
        public event Action<GhostGallery> Hit;
        public event Action<GhostGallery> Beaten;

        public float HoldSecondsNow => Mathf.Lerp(2.6f, 1.8f, Hits / (float)(HitsToWin - 1));
        public const float WalkSeconds = 3.2f, WalkSpeed = 0.45f;

        Transform m_Target, m_Pips;
        Material m_TargetMat, m_TargetCoreMat;
        readonly List<Material> m_PipMats = new List<Material>();
        LineRenderer m_Beam, m_BeamCore;
        Material m_BeamMat, m_BeamCoreMat;
        Light m_Impact, m_Flash, m_TorchGlow;
        Vector3 m_LensLocal;
        float m_Timer, m_BeamTime, m_Cooldown, m_TargetPop, m_FlashTime;
        int m_Spot = -1, m_FlickerRun;
        Renderer[] m_Flickering;
        bool[] m_FlickerWasOn;

        bool PassengerHere
        {
            get
            {
                var pc = Game.Passenger;
                return pc != null && pc.Visible && Cell != null && pc.transform.parent == Cell.transform;
            }
        }

        /// <summary>Where to shoot: the glowing target on its chest (world space), a little in front of the glass body.</summary>
        public Vector3 TargetPoint
        {
            get
            {
                var pc = Game.Passenger;
                if (pc == null) return transform.position;
                var chest = pc.ChestPosition;
                var to = Game.Rig != null ? Game.Rig.Head.position - chest : Vector3.zero;
                to.y = 0f;
                return chest + (to.sqrMagnitude > 0.01f ? to.normalized * 0.22f : Vector3.zero);
            }
        }

        public static GhostGallery Build(BerlinCell cell, Vector3 torchLocal, float torchYaw)
        {
            var go = new GameObject("ShootingGallery");
            go.transform.SetParent(cell.transform, false);
            var g = go.AddComponent<GhostGallery>();
            g.Cell = cell;
            g.Construct(torchLocal, torchYaw);
            return g;
        }

        void Construct(Vector3 torchLocal, float torchYaw)
        {
            // Round the hall, each well away from the plinth and clear of the furniture: by the blank wall, the north-west
            // tree, the glass, the false doors (two), the south-west corner and the information desk.
            Spots.Add(new Vector3(-6.6f, 0f, 0.6f));
            Spots.Add(new Vector3(-5.8f, 0f, 6.2f));
            Spots.Add(new Vector3(0f, 0f, 7.4f));
            Spots.Add(new Vector3(6.8f, 0f, 2.0f));
            Spots.Add(new Vector3(6.9f, 0f, -2.4f));
            Spots.Add(new Vector3(-6.2f, 0f, -4.4f));
            Spots.Add(new Vector3(3.4f, 0f, -4.6f));
            Torch = BuildTorch(torchLocal, torchYaw);
            BuildMarkers();
            BuildBeam();
        }

        // ------------------------------------------------------------------ the sun torch
        Grabbable BuildTorch(Vector3 local, float yaw)
        {
            var go = new GameObject("SunTorch");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // The barrel runs along the torch's +Z (the lens end): built along +Y, turned forward.
            var barrel = new GameObject("Barrel").transform;
            barrel.SetParent(go.transform, false);
            barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var brass = Mats.Lit(new Color(0.8f, 0.62f, 0.32f), 0.7f, 0.9f, "metal_brushed");
            var grip = Mats.Lit(new Color(0.9f, 0.85f, 0.8f), 0.35f, 0f, "leather_navy");
            var sun = Mats.Emissive(new Color(1f, 0.85f, 0.5f), new Color(3.2f, 2.3f, 1f));
            MeshFactory.Cylinder("Grip", barrel, new Vector3(0f, -0.15f, 0f), 0.024f, 0.15f, grip, 14, false);
            MeshFactory.Cylinder("Collar", barrel, new Vector3(0f, -0.005f, 0f), 0.03f, 0.03f, brass, 16, false);
            MeshFactory.Cylinder("Body", barrel, new Vector3(0f, 0.02f, 0f), 0.028f, 0.08f, brass, 16, false);
            MeshFactory.Frustum("Head", barrel, new Vector3(0f, 0.1f, 0f), 0.03f, 0.05f, 0.05f, brass, 18);
            MeshFactory.Cylinder("Lens", barrel, new Vector3(0f, 0.15f, 0f), 0.044f, 0.008f, sun, 18, false);
            MeshFactory.Sphere("Cap", barrel, new Vector3(0f, -0.15f, 0f), 0.026f, brass, 12, 8);
            m_LensLocal = new Vector3(0f, 0f, 0.16f);
            var lg = new GameObject("TorchGlow");
            lg.transform.SetParent(go.transform, false);
            lg.transform.localPosition = new Vector3(0f, 0f, 0.22f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.82f, 0.5f); l.intensity = 0.7f; l.range = 1.3f; l.shadows = LightShadows.None;
            m_TorchGlow = l;
            MeshFactory.NoShadows(go);
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.12f, 0.12f, 0.36f);
            col.center = new Vector3(0f, 0f, 0f);
            var g = PropLibrary.MakeGrabbable(go, "sun_torch", "Take the sun torch (it holds Egypt's light)");
            g.ActivatePrompt = "Shoot a beam of light";
            g.OnActivate = (it, who) => Fire(who);
            g.UsePhysicsOnRelease = false; // put down, it stays where it is (it never rolls off the plinth)
            g.HoldLocalPosition = new Vector3(0f, 0f, 0.06f); // held by the grip, the lens forward
            g.HoldLocalEuler = Vector3.zero;
            g.DesktopHoldEuler = Vector3.zero;
            g.SetHome();
            go.SetActive(false);
            return g;
        }

        /// <summary>The torch rises on the plinth (the seals are validated).</summary>
        public void OfferTorch()
        {
            if (Torch == null) return;
            if (!Torch.gameObject.activeSelf)
            {
                Torch.gameObject.SetActive(true); // (built while the cell was inactive, it wakes up here)
                AudioDirector.I?.Play("pp_seam_shimmer", Torch.transform.position, 0.8f, 0.8f);
            }
            if (Torch.Holder == null && Torch.Body != null) Torch.ReturnHome();
            Torch.Usable = true;
        }

        /// <summary>The torch is spent (it is beaten) or not needed here: out of the hand and away.</summary>
        public void RetireTorch()
        {
            if (Torch == null) return;
            if (Torch.Holder != null) Torch.Holder.ForceRelease(false);
            if (Torch.Body != null) Torch.ReturnHome(); // (never woken up, it has not moved)
            Torch.gameObject.SetActive(false);
        }

        public Vector3 LensPosition => Torch != null ? Torch.transform.TransformPoint(m_LensLocal) : transform.position;

        // ------------------------------------------------------------------ the game
        /// <summary>Starts the gallery: the hits back at zero, the first appearance in a moment.</summary>
        public void Begin()
        {
            StopAllCoroutines();
            RestoreFlicker();
            Hits = 0;
            Shots = 0;
            Appearances = 0;
            m_Spot = -1;
            if (PassengerHere) Vanish(0.5f); // it was standing guard by the blank wall
            else { State = Phase.Away; m_Timer = 0.8f; }
            AudioDirector.I?.Play("pass_whisper", Game.Rig != null ? Game.Rig.Head.position + Game.Rig.Head.forward * 3f : (Vector3?)null, 0.7f, 0.8f);
        }

        /// <summary>No gallery (the player left, a retry, or the seals are not validated yet).</summary>
        public void Stop()
        {
            StopAllCoroutines();
            RestoreFlicker();
            State = Phase.Idle;
            m_BeamTime = 0f;
            SetBeam(false);
            if (m_Target != null) m_Target.gameObject.SetActive(false);
            if (m_Pips != null) m_Pips.gameObject.SetActive(false);
        }

        /// <summary>Already beaten on an earlier visit: it kneels by the false doors, its light spent.</summary>
        public void ShowKneeling()
        {
            Stop();
            Hits = HitsToWin;
            Kneel();
            State = Phase.Kneeling;
        }

        void Kneel()
        {
            var pc = Game.Passenger;
            if (pc == null || Cell == null) return;
            pc.Spawn(Cell, KneelLocal, 270f, PassengerState.Observe, Disguise.Mimic);
            pc.Threatening = false;
            pc.AllowChase = false;
            pc.RetreatPoint = Cell.transform.TransformPoint(KneelLocal);
            pc.Investigate(pc.transform.position, 1e6f); // arrives at once and crouches there: on its knees
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (m_Cooldown > 0f) m_Cooldown -= dt;
            UpdateBeam(dt);
            // On the plinth it glows to be found; in the hand only faintly (right by the eyes it lit the sleeve white).
            if (m_TorchGlow != null) m_TorchGlow.intensity = TorchHeld ? 0.15f : 0.7f + Mathf.Sin(Time.time * 3f) * 0.2f;
            if (!Running) return;
            if (Cell == null || Cell.Mode != CellMode.Active || Cell.Encounter == null || !Cell.Encounter.IsLive) return;
            var story = Game.Story;
            var pc = Game.Passenger;
            if (story == null || pc == null || Game.Rig == null || story.Busy || story.Failing || Game.Paused) return;
            if (Game.Campaign != null && Game.Campaign.vitality <= 0f) return; // caught: the Retry card is up
            m_Timer -= dt;
            switch (State)
            {
                case Phase.Away:
                    if (m_Timer <= 0f) Appear();
                    break;
                case Phase.Holding:
                    if (!PassengerHere) { State = Phase.Away; m_Timer = 0.8f; break; }
                    if (m_Timer <= 0f)
                    {
                        // Its moment is over: it comes for you, slowly, until it flickers away again.
                        pc.WalkSpeed = WalkSpeed;
                        pc.AllowChase = false;
                        pc.SetState(PassengerState.Stalk);
                        State = Phase.Walking;
                        m_Timer = WalkSeconds;
                    }
                    break;
                case Phase.Walking:
                    if (!PassengerHere) { State = Phase.Away; m_Timer = 0.8f; break; }
                    pc.WalkSpeed = WalkSpeed;
                    pc.AllowChase = false;
                    if (m_Timer <= 0f) Vanish(UnityEngine.Random.Range(0.6f, 1.0f));
                    break;
                case Phase.Struck:
                    if (m_Timer <= 0f)
                    {
                        if (Hits >= HitsToWin) StartCoroutine(Weaken());
                        else Vanish(0.5f);
                    }
                    break;
            }
        }

        void Appear()
        {
            var pc = Game.Passenger;
            int i = PickSpot();
            m_Spot = i;
            var local = Spots[i];
            var feet = Cell.transform.InverseTransformPoint(Game.Rig.Feet);
            var look = feet - local; look.y = 0f;
            float yaw = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look).eulerAngles.y : 0f;
            pc.Spawn(Cell, local, yaw, PassengerState.Observe, Disguise.Mimic);
            pc.Threatening = true;
            pc.AllowChase = false;
            pc.WalkSpeed = WalkSpeed;
            pc.RetreatPoint = Cell.transform.TransformPoint(local);
            pc.RetreatSeconds = 2f;
            Appearances++;
            StartCoroutine(Flicker(true));
            FlashAt(pc.ChestPosition, new Color(0.75f, 0.85f, 1f));
            AudioDirector.I?.Play("pass_whisper", pc.ChestPosition, 0.9f, 0.9f);
            AudioDirector.I?.Play("pp_seam_shimmer", pc.ChestPosition, 0.6f, 0.6f);
            if (Appearances == 1) Hud.I?.ShowCaption("it flickers in across the hall: shoot the glowing target on its chest", 3.5f);
            State = Phase.Holding;
            m_Timer = HoldSecondsNow;
        }

        /// <summary>Somewhere it was not last time, well away from the player, most likely in front of them.</summary>
        int PickSpot()
        {
            var feet = Cell.transform.InverseTransformPoint(Game.Rig.Feet);
            var fwd = Cell.transform.InverseTransformDirection(Game.Rig.Head.forward); fwd.y = 0f;
            float total = 0f;
            var w = new float[Spots.Count];
            int farthest = 0;
            float far = -1f;
            for (int i = 0; i < Spots.Count; i++)
            {
                var to = Spots[i] - feet; to.y = 0f;
                float d = to.magnitude;
                if (d > far && i != m_Spot) { far = d; farthest = i; }
                if (i == m_Spot || d < 4.5f) continue;
                w[i] = 1f + (fwd.sqrMagnitude > 0.01f && Vector3.Angle(fwd, to) < 70f ? 2f : 0f);
                total += w[i];
            }
            if (total <= 0f) return farthest;
            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < w.Length; i++) { r -= w[i]; if (w[i] > 0f && r <= 0f) return i; }
            return farthest;
        }

        void Vanish(float away)
        {
            State = Phase.Away;
            m_Timer = away + 0.35f;
            StartCoroutine(VanishRoutine());
        }

        IEnumerator VanishRoutine()
        {
            var pc = Game.Passenger;
            if (pc == null || !PassengerHere) yield break;
            pc.Threatening = false; // nothing catches you as it fades
            AudioDirector.I?.Play("pass_whisper", pc.ChestPosition, 0.45f, 1.3f);
            yield return Flicker(false);
            if (State == Phase.Away && PassengerHere) pc.Despawn();
        }

        IEnumerator Weaken()
        {
            State = Phase.Weakening;
            var pc = Game.Passenger;
            if (pc != null)
            {
                pc.Threatening = false;
                pc.SetState(PassengerState.Repelled); // it staggers, shielding itself
                AudioDirector.I?.Play("glasses_break", pc.ChestPosition, 0.8f, 0.6f);
                AudioDirector.I?.Play("pass_recoil", pc.ChestPosition, 1f, 0.7f);
            }
            PostFx.I?.Flash(0.7f);
            Hud.I?.ShowCaption("five hits: it staggers, its light spent", 3f);
            yield return new WaitForSeconds(1.1f);
            yield return Flicker(false);
            Kneel();
            if (Game.Passenger != null) FlashAt(Game.Passenger.ChestPosition, new Color(1f, 0.8f, 0.55f));
            yield return Flicker(true);
            State = Phase.Kneeling;
            Beaten?.Invoke(this);
        }

        /// <summary>Its body blinks in (or out) over a third of a second; the renderers are left as they were.</summary>
        IEnumerator Flicker(bool appearing)
        {
            var pc = Game.Passenger;
            if (pc == null) yield break;
            // A newer flicker takes over from one still running (it keeps the states that one recorded) and restores them.
            int run = ++m_FlickerRun;
            if (m_Flickering == null)
            {
                m_Flickering = pc.GetComponentsInChildren<Renderer>();
                m_FlickerWasOn = new bool[m_Flickering.Length];
                for (int i = 0; i < m_Flickering.Length; i++) m_FlickerWasOn[i] = m_Flickering[i].enabled;
            }
            var rs = m_Flickering;
            var was = m_FlickerWasOn;
            const float Seconds = 0.34f;
            for (float t = 0f; t < Seconds; t += Time.deltaTime)
            {
                if (run != m_FlickerRun) yield break;
                float k = t / Seconds;
                bool on = UnityEngine.Random.value < (appearing ? k : 1f - k);
                for (int i = 0; i < rs.Length; i++) if (rs[i] != null && was[i]) rs[i].enabled = on;
                yield return null;
            }
            if (run == m_FlickerRun) RestoreFlicker();
        }

        void RestoreFlicker()
        {
            m_FlickerRun++; // any flicker still running stops
            if (m_Flickering == null) return;
            for (int i = 0; i < m_Flickering.Length; i++) if (m_Flickering[i] != null) m_Flickering[i].enabled = m_FlickerWasOn[i];
            m_Flickering = null;
        }

        // ------------------------------------------------------------------ shooting
        /// <summary>One shot from the torch along the hand's aim (desktop: the view's centre; VR: the controller's ray).</summary>
        public void Fire(Interactor who)
        {
            if (who == null || who.RayOrigin == null || m_Cooldown > 0f) return;
            m_Cooldown = 0.22f;
            Shots++;
            var origin = who.RayOrigin.position;
            var dir = who.RayOrigin.forward;
            float reach = 40f;
            var end = origin + dir * reach;
            if (Physics.Raycast(origin, dir, out var hit, reach, (1 << Layers.Default) | (1 << Layers.Floor), QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                reach = hit.distance;
            }
            bool struck = false;
            if (TargetUp)
            {
                var c = TargetPoint;
                float t = Vector3.Dot(c - origin, dir);
                if (t > 0f && t < reach + 0.4f)
                {
                    var closest = origin + dir * t;
                    if ((closest - c).magnitude <= HitRadius) { struck = true; end = closest; }
                }
            }
            var from = Torch != null && Torch.Holder == who ? LensPosition : who.HoldPoint != null ? who.HoldPoint.position : origin;
            ShowBeam(from, end, struck);
            AudioDirector.I?.Play("sun_sizzle", end, struck ? 1f : 0.45f, struck ? 1f : 1.35f);
            AudioDirector.I?.Play("pp_seam_shimmer", from, 0.35f, 1.8f);
            PostFx.I?.Flash(struck ? 0.45f : 0.12f);
            AudioDirector.Haptic(who.Side, struck ? 0.8f : 0.35f, 0.06f);
            if (struck) OnStruck();
        }

        void OnStruck()
        {
            Hits++;
            m_TargetPop = 1f;
            var pc = Game.Passenger;
            if (pc != null)
            {
                pc.SetState(PassengerState.Repelled); // it flinches from the light: ash, a hiss, a step back
                AudioDirector.I?.Play("pass_recoil", pc.ChestPosition, 0.9f, 1.1f);
            }
            State = Phase.Struck;
            m_Timer = 0.55f;
            Hit?.Invoke(this);
        }

        // ------------------------------------------------------------------ the beam, the target, the strength pips
        void BuildBeam()
        {
            m_BeamMat = Mats.UnlitInstance(new Color(1f, 0.82f, 0.45f, 0f), Tex.SoftDot, true, 2.4f);
            m_BeamMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m_BeamCoreMat = Mats.UnlitInstance(new Color(1f, 0.97f, 0.85f, 0f), null, true, 3f);
            m_Beam = Line("Beam", m_BeamMat);
            m_BeamCore = Line("BeamCore", m_BeamCoreMat);
            m_Impact = MakeLight("BeamImpact", new Color(1f, 0.8f, 0.5f), 2.8f);
            m_Flash = MakeLight("AppearFlash", new Color(0.75f, 0.85f, 1f), 4f);
        }

        LineRenderer Line(string name, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.material = m;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        Light MakeLight(string name, Color c, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.range = range; l.intensity = 0f; l.shadows = LightShadows.None;
            l.enabled = false;
            return l;
        }

        void ShowBeam(Vector3 from, Vector3 to, bool struck)
        {
            m_Beam.SetPosition(0, from); m_Beam.SetPosition(1, to);
            m_BeamCore.SetPosition(0, from); m_BeamCore.SetPosition(1, to);
            m_Impact.transform.position = to - (to - from).normalized * 0.15f;
            m_Impact.intensity = struck ? 4f : 2f;
            m_BeamTime = 0.15f;
            SetBeam(true);
        }

        void SetBeam(bool on)
        {
            if (m_Beam != null) m_Beam.enabled = on;
            if (m_BeamCore != null) m_BeamCore.enabled = on;
            if (m_Impact != null) m_Impact.enabled = on;
        }

        /// <summary>Shown for the tests: the beam of the last shot is still drawn.</summary>
        public bool BeamShowing => m_Beam != null && m_Beam.enabled;

        void UpdateBeam(float dt)
        {
            if (m_FlashTime > 0f)
            {
                m_FlashTime -= dt;
                m_Flash.intensity = Mathf.Max(0f, m_FlashTime / 0.3f) * 5f;
                if (m_FlashTime <= 0f) m_Flash.enabled = false;
            }
            if (m_BeamTime <= 0f) return;
            m_BeamTime -= dt;
            float k = Mathf.Clamp01(m_BeamTime / 0.15f);
            m_Beam.widthMultiplier = 0.14f * (0.6f + 0.4f * k);
            m_BeamCore.widthMultiplier = 0.028f * (0.5f + 0.5f * k);
            m_BeamMat.SetColor("_Color", new Color(1f, 0.82f, 0.45f, 0.75f * k));
            m_BeamCoreMat.SetColor("_Color", new Color(1f, 0.97f, 0.85f, k));
            m_Impact.intensity = 4f * k;
            if (m_BeamTime <= 0f) SetBeam(false);
        }

        void FlashAt(Vector3 p, Color c)
        {
            m_Flash.transform.position = p;
            m_Flash.color = c;
            m_Flash.enabled = true;
            m_FlashTime = 0.3f;
        }

        void BuildMarkers()
        {
            m_Target = new GameObject("ChestTarget").transform;
            m_Target.SetParent(transform, false);
            m_TargetMat = Mats.UnlitInstance(new Color(1f, 0.5f, 0.2f, 0.95f), Tex.Ring, true, 2.4f);
            m_TargetMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m_TargetMat.SetFloat("_Cull", 0f);
            m_TargetCoreMat = Mats.UnlitInstance(new Color(1f, 0.85f, 0.5f, 0.9f), Tex.SoftDot, true, 2.6f);
            m_TargetCoreMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m_TargetCoreMat.SetFloat("_Cull", 0f);
            MeshFactory.Quad("Ring", m_Target, 0.46f, 0.46f, m_TargetMat).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MeshFactory.Quad("Ring2", m_Target, 0.26f, 0.26f, m_TargetMat, new Vector3(0f, 0f, -0.005f)).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MeshFactory.Quad("Bull", m_Target, 0.16f, 0.16f, m_TargetCoreMat, new Vector3(0f, 0f, -0.01f)).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Target.gameObject.SetActive(false);

            m_Pips = new GameObject("StrengthPips").transform;
            m_Pips.SetParent(transform, false);
            for (int i = 0; i < HitsToWin; i++)
            {
                var m = Mats.UnlitInstance(new Color(1f, 0.38f, 0.22f, 1f), Tex.SoftDot, true, 2f);
                m.SetFloat("_Cull", 0f);
                m_PipMats.Add(m);
                MeshFactory.Quad("Pip" + i, m_Pips, 0.11f, 0.11f, m, new Vector3((i - (HitsToWin - 1) * 0.5f) * 0.13f, 0f, 0f)).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            m_Pips.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            var head = Game.Rig != null ? Game.Rig.Head : null;
            var pc = Game.Passenger;
            bool here = PassengerHere && head != null;
            bool target = here && (TargetUp || State == Phase.Struck);
            m_Target.gameObject.SetActive(target);
            if (target)
            {
                var p = TargetPoint;
                m_Target.position = p;
                m_Target.rotation = Quaternion.LookRotation(p - head.position, Vector3.up);
                m_TargetPop = Mathf.MoveTowards(m_TargetPop, 0f, Time.deltaTime * 3f);
                float pulse = 1f + Mathf.Sin(Time.time * 7f) * 0.08f + m_TargetPop * 0.6f;
                m_Target.localScale = Vector3.one * pulse;
                var c = Color.Lerp(new Color(1f, 0.5f, 0.2f, 0.95f), new Color(1f, 1f, 0.9f, 1f), m_TargetPop);
                m_TargetMat.SetColor("_Color", c);
            }
            bool pips = here && (Running || State == Phase.Weakening);
            m_Pips.gameObject.SetActive(pips);
            if (pips)
            {
                m_Pips.position = pc.ChestPosition + Vector3.up * 0.75f;
                m_Pips.rotation = Quaternion.LookRotation(m_Pips.position - head.position, Vector3.up);
                for (int i = 0; i < m_PipMats.Count; i++)
                    m_PipMats[i].SetColor("_Color", i < Strength ? new Color(1f, 0.38f, 0.22f, 1f) : new Color(0.3f, 0.3f, 0.33f, 0.55f));
            }
        }

        /// <summary>Its strength as pips for the objective: lit for what is left, grey for what the light took.</summary>
        public string PipsText()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#FF6A4A>");
            for (int i = 0; i < Strength; i++) sb.Append("• ");
            sb.Append("</color><color=#6B6B70>");
            for (int i = Strength; i < HitsToWin; i++) sb.Append("• ");
            sb.Append("</color>");
            return sb.ToString().TrimEnd();
        }
    }
}
