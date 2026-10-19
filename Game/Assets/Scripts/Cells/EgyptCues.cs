using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// World cues for Egypt's temple run (Cells/EgyptRun.cs): a burst of tomb dust when a door slab falls or a mummy
    /// crumbles, a gold sparkle where a passport coin is taken, and shreds of linen round the player when a mummy's grip
    /// lands. One particle system each, built once on the run.
    /// </summary>
    public class EgyptCues : MonoBehaviour
    {
        ParticleSystem m_Dust, m_Sparkle;

        ParticleSystem Dusty => m_Dust != null ? m_Dust : m_Dust = Make("TombDust", false, new Color(0.86f, 0.74f, 0.56f, 0.8f), new Color(0.7f, 0.6f, 0.45f, 0.6f), 0.12f, 0.4f, 0.3f, 1.2f, 0.9f, 1.8f, -0.05f);
        ParticleSystem Sparkly => m_Sparkle != null ? m_Sparkle : m_Sparkle = Make("CoinSparkle", true, new Color(1f, 0.9f, 0.45f, 1f), new Color(1f, 0.75f, 0.3f, 1f), 0.02f, 0.06f, 0.8f, 2.2f, 0.3f, 0.6f, 0.4f);

        /// <summary>Builds the particle systems with the run (the cell caches its renderers once).</summary>
        public void Init()
        {
            _ = Dusty;
            _ = Sparkly;
        }

        public void Dust(Vector3 world, int count)
        {
            var ps = Dusty;
            ps.transform.position = world;
            ps.Emit(count);
        }

        public void Sparkle(Vector3 world)
        {
            var ps = Sparkly;
            ps.transform.position = world;
            ps.Emit(22);
        }

        public void HitFlash()
        {
            var rig = Game.Rig;
            if (rig == null) return;
            var ps = Dusty;
            ps.transform.position = rig.Head.position + rig.Forward * 0.6f - Vector3.up * 0.4f;
            ps.Emit(18);
        }

        ParticleSystem Make(string name, bool additive, Color a, Color b, float size0, float size1, float speed0, float speed1, float life0, float life1, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life0, life1);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed0, speed1);
            main.startSize = new ParticleSystem.MinMaxCurve(size0, size1);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = additive ? 0.12f : 0.5f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, additive ? 1f : 0.6f, 1f, additive ? 0.2f : 2.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, additive, Color.white, additive ? 1.8f : 1f, Mats.StencilNotEqual);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }
    }
}
