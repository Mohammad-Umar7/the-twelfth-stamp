using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Makes a light hit on the Passenger unmistakable (playtest: "I put the light at the enemy and I'm just stuck there"):
    /// the moment light catches it, a burst of ash and a hiss as it flinches back a step; smoke curls off it for as long as
    /// it stays lit; the first time in each place a caption says what happened. World motion only: the camera is never
    /// moved, and nothing flashes.
    /// </summary>
    [RequireComponent(typeof(PassengerController))]
    public class PassengerLightFx : MonoBehaviour
    {
        const float KnockDistance = 0.4f, KnockSpeed = 2.4f;

        static readonly HashSet<CellId> s_Captioned = new HashSet<CellId>();

        PassengerController m_Pc;
        ParticleSystem m_Ash, m_Smoke;
        float m_Knock, m_LastFlinch = -10f;
        Vector3 m_KnockDir;

        /// <summary>Flinches so far (tests).</summary>
        public int Flinches { get; private set; }

        void Awake()
        {
            m_Pc = GetComponent<PassengerController>();
            m_Pc.StateChanged += OnState;
            m_Ash = MakeAsh();
            m_Smoke = MakeSmoke();
        }

        void OnDestroy()
        {
            if (m_Pc != null) m_Pc.StateChanged -= OnState;
        }

        void OnState(PassengerState s)
        {
            if (s == PassengerState.Repelled) Flinch();
        }

        void Flinch()
        {
            if (Time.time - m_LastFlinch < 0.6f) return;
            m_LastFlinch = Time.time;
            Flinches++;
            var chest = m_Pc.ChestPosition;
            m_Ash.transform.position = chest;
            m_Ash.Emit(46);
            AudioDirector.I?.Play("pass_recoil", chest, 0.7f, Random.Range(1.08f, 1.22f));
            // Back a step, away from the player: the light that hits it is nearly always theirs (a tear, the beam, a lamp
            // they switched on).
            var rig = Game.Rig;
            var away = rig != null ? transform.position - rig.Feet : -transform.forward;
            away.y = 0f;
            m_KnockDir = away.sqrMagnitude > 1e-4f ? away.normalized : -transform.forward;
            m_Knock = KnockDistance;
            var cell = Game.Cells != null ? Game.Cells.Active : null;
            if (cell != null && s_Captioned.Add(cell.Id)) Hud.I?.ShowCaption("it flinches from the light", 2.6f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (m_Knock > 0f)
            {
                float step = Mathf.Min(m_Knock, KnockSpeed * dt);
                m_Knock -= step;
                // Never through a wall: stop at anything solid within reach of the step.
                if (!Physics.Raycast(m_Pc.ChestPosition, m_KnockDir, 0.45f, ~((1 << Layers.Passenger) | (1 << Layers.PlayerBody) | (1 << Layers.MirrorOnly) | (1 << Layers.UI3D)), QueryTriggerInteraction.Ignore))
                    transform.position += m_KnockDir * step;
                else m_Knock = 0f;
            }
            bool lit = m_Pc.Visible && m_Pc.Exposure > m_Pc.RepelThreshold * 0.8f;
            var em = m_Smoke.emission;
            em.rateOverTime = lit ? Mathf.Lerp(8f, 34f, Mathf.InverseLerp(m_Pc.RepelThreshold, 1f, m_Pc.Exposure)) : 0f;
            if (lit) m_Smoke.transform.position = m_Pc.ChestPosition + Vector3.up * 0.1f;
        }

        ParticleSystem MakeAsh()
        {
            var go = new GameObject("LightAsh");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.16f, 0.15f, 0.9f), new Color(0.42f, 0.4f, 0.36f, 0.8f));
            main.gravityModifier = -0.08f;
            main.maxParticles = 160;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.28f;
            var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 2.2f; drag.multiplyDragByParticleSize = false;
            Fade(ps);
            Finish(go, false);
            return ps;
        }

        ParticleSystem MakeSmoke()
        {
            var go = new GameObject("LightSmoke");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.56f, 0.55f, 0.28f), new Color(0.75f, 0.74f, 0.7f, 0.2f));
            main.maxParticles = 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(0.45f, 0.9f, 0.3f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            vel.y = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            Fade(ps);
            Finish(go, false);
            ps.Play();
            return ps;
        }

        static void Fade(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        static void Finish(GameObject go, bool additive)
        {
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, additive, Color.white, 1f, Mats.StencilNotEqual);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 0.5f;
            go.layer = Layers.Passenger;
        }
    }
}
