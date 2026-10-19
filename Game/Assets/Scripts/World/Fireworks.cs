using System.Collections;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Celebration effects for the Victory Ground: rockets that climb on a sparkling trail and burst into trailed stars
    /// with a flash of light and a falling glitter, and confetti that flutters down around the player. All one-shot
    /// particle systems that clean themselves up.
    /// </summary>
    public static class Fireworks
    {
        static Material Additive => Mats.Particle(Tex.SoftDot, true, Color.white, 1.6f, Mats.StencilNotEqual);
        static Material Paper => Mats.Particle(Tex.White, false, Color.white, 1f, Mats.StencilNotEqual);

        /// <summary>A rocket from <paramref name="from"/> that bursts at <paramref name="at"/> (world space).</summary>
        public static IEnumerator Rocket(Transform parent, Vector3 from, Vector3 at, Color color, float scale = 1f)
        {
            var go = new GameObject("Rocket");
            go.transform.SetParent(parent, true);
            go.transform.position = from;
            var trail = go.AddComponent<ParticleSystem>();
            {
                var main = trail.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
                main.startColor = new Color(1f, 0.85f, 0.6f, 0.9f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 0.15f;
                main.maxParticles = 200;
                var em = trail.emission;
                em.rateOverTime = 0f;
                em.rateOverDistance = 7f;
                var shape = trail.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.03f;
                Fade(trail, Color.white);
                Render(go, Additive, 0.6f);
            }
            AudioDirector.I?.Play("shift_whoosh", from, 0.22f * scale, 1.7f + Random.value * 0.3f, Bus.SFX, false, null, 12f);
            float t = 0f, dur = 1.0f + Random.value * 0.35f;
            var wobble = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.6f;
            while (t < 1f && go != null)
            {
                t += Time.deltaTime / dur;
                float e = 1f - (1f - t) * (1f - t); // decelerates as it climbs
                go.transform.position = Vector3.Lerp(from, at, e) + wobble * Mathf.Sin(t * Mathf.PI);
                yield return null;
            }
            if (go == null) yield break;
            trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(go, 1.2f);
            Burst(parent, at, color, scale);
        }

        /// <summary>A burst of trailed stars in <paramref name="color"/>, a flash of light and a falling golden glitter.</summary>
        public static void Burst(Transform parent, Vector3 at, Color color, float scale = 1f)
        {
            var go = new GameObject("Burst");
            go.transform.SetParent(parent, true);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6.5f * scale, 9.5f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f * scale, 0.32f * scale);
            main.startColor = Color.white;
            main.gravityModifier = 0.32f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(130 * scale), (short)(170 * scale)) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.6f;
            drag.multiplyDragByParticleSize = false;
            drag.multiplyDragByParticleVelocity = true;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
            Fade(ps, color);
            var trails = ps.trails;
            trails.enabled = true;
            trails.ratio = 0.7f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.22f);
            trails.minVertexDistance = 0.15f;
            trails.dieWithParticles = true;
            trails.inheritParticleColor = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 0f));
            var r = Render(go, Additive, 1f);
            r.trailMaterial = Additive;

            // Glitter: a slower gold shower that twinkles as it falls.
            var gl = new GameObject("Glitter");
            gl.transform.SetParent(go.transform, false);
            var gp = gl.AddComponent<ParticleSystem>();
            var gm = gp.main;
            gm.loop = false;
            gm.duration = 0.2f;
            gm.startDelay = 0.55f;
            gm.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            gm.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.2f * scale);
            gm.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
            gm.startColor = new Color(1f, 0.86f, 0.5f);
            gm.gravityModifier = 0.12f;
            gm.simulationSpace = ParticleSystemSimulationSpace.World;
            var gem = gp.emission;
            gem.rateOverTime = 0f;
            gem.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(60 * scale)) });
            var gs = gp.shape;
            gs.shapeType = ParticleSystemShapeType.Sphere;
            gs.radius = 3.5f * scale;
            var noise = gp.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 1.5f;
            var twinkle = gp.colorOverLifetime;
            twinkle.enabled = true;
            var tg = new Gradient();
            tg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.75f, 0.35f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.2f, 0.3f), new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0.3f, 0.65f), new GradientAlphaKey(0.9f, 0.8f), new GradientAlphaKey(0f, 1f) });
            twinkle.color = tg;
            Render(gl, Additive, 0.4f);
            ps.Play(true);

            // The flash lights the ground and the faces below it.
            var lightGo = new GameObject("Flash");
            lightGo.transform.SetParent(go.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Color.Lerp(color, Color.white, 0.35f);
            l.range = 45f * scale;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            lightGo.AddComponent<FlashFade>().Peak = 5f * scale;

            AudioDirector.I?.Play("stamp_hit", at, 0.75f * scale, 0.45f + Random.value * 0.12f, Bus.SFX, false, null, 25f);
            AudioDirector.I?.Play("drum_hit", at, 0.5f * scale, 0.55f + Random.value * 0.1f, Bus.SFX, false, null, 25f);
        }

        /// <summary>Paper confetti falling around <paramref name="center"/> (world space), fluttering as it drops.</summary>
        public static void Confetti(Transform parent, Vector3 center, float radius = 4f, int count = 260, float height = 5f)
        {
            var go = new GameObject("Confetti");
            go.transform.SetParent(parent, true);
            go.transform.position = center + Vector3.up * height;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.duration = 1.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.035f, 0.06f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.06f, 0.1f);
            main.startSizeZ = 1f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var palette = new Gradient();
            palette.mode = GradientMode.Fixed;
            palette.SetKeys(new[]
            {
                new GradientColorKey(Levels.Red, 0.2f), new GradientColorKey(new Color(1f, 0.8f, 0.25f), 0.4f), new GradientColorKey(Levels.Green, 0.6f),
                new GradientColorKey(Levels.Blue, 0.8f), new GradientColorKey(new Color(0.95f, 0.95f, 0.92f), 1f),
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(palette) { mode = ParticleSystemGradientMode.RandomColor };
            main.gravityModifier = 0.09f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count + 20;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(count * 0.6f)), new ParticleSystem.Burst(0.5f, (short)(count * 0.4f)) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 0.8f;
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.9f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.4f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            Render(go, Paper, 0.2f);
            ps.Play();
        }

        static void Fade(ParticleSystem ps, Color color)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.18f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        static ParticleSystemRenderer Render(GameObject go, Material m, float maxSize)
        {
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = maxSize;
            return r;
        }
    }

    /// <summary>A light that flares to <see cref="Peak"/> and dies away (a firework's flash).</summary>
    public class FlashFade : MonoBehaviour
    {
        public float Peak = 5f;
        Light m_L;
        float m_T;

        void Awake() => m_L = GetComponent<Light>();

        void Update()
        {
            m_T += Time.deltaTime;
            if (m_L == null) return;
            m_L.intensity = Peak * (m_T < 0.06f ? m_T / 0.06f : Mathf.Exp(-(m_T - 0.06f) * 4f));
            if (m_T > 1.6f) { m_L.enabled = false; enabled = false; }
        }
    }
}
