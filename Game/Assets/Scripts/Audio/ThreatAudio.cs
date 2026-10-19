using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// The threat in sound. Fear (0..1) follows the Passenger: its distance to the head (2.5 m = full, 18 m = none),
    /// whether it is threatening, stalking, chasing or lunging (and the Between's runner); it rises in ~0.4 s and
    /// settles over ~5 s. A heartbeat is scheduled from single-beat clips: 62 bpm at rest up to 150 bpm (Gentle 125),
    /// audible from fear ~0.12, louder with fear (exertion alone gives a softer one). A chase start dips the world for
    /// ~0.3 s (the heart skips), then a stinger hits, the player gasps and mus_chase comes in from a phrase start while
    /// the cell's music and ambience duck under it; 2.5 s after the chase ends the layer fades over 4 s and the beds
    /// come back. A stalker nearby brings in mus_tension softly. Caught: everything cuts. Gentle: no dip, softer
    /// stinger and music; reduced dynamic range: lower peaks. Stays out of the way of a scene's own chase music and
    /// heartbeat (the dimension shift, the ending, the Between); during a shift's inhale the beds drain away.
    /// </summary>
    public class ThreatAudio : MonoBehaviour
    {
        public static ThreatAudio I { get; private set; }

        /// <summary>0..1: how frightened the player should be right now (smoothed).</summary>
        public float Fear { get; private set; }
        public bool ChaseActive => m_Chase;

        const float ChaseLevel = 1f, TensionLevel = 0.5f;
        static readonly string[] Beats = { "heart_beat_1", "heart_beat_2", "heart_beat_3", "heart_beat_4" };
        static readonly string[] Stings = { "sting_chase_1", "sting_chase_2" };
        static readonly float[] Phrases = { 0f, 15f, 30f }; // mus_chase phrase starts (8 bars at 128 bpm)

        AudioSource m_ChaseLayer, m_Tension;
        readonly AudioSource[] m_Heart = new AudioSource[2];
        PassengerController m_Passenger;
        bool m_Chase, m_ChaseLayerOn, m_ReliefDue;
        float m_ChaseVol, m_TensionVol, m_ChaseOffAt = -1f, m_DipStart = -1f, m_StingAt = -1f, m_DipRelease = -1f;
        float m_NextBeat, m_ChaseEndedAt = -1f, m_ShiftDip = 1f;
        int m_Beat = -1, m_Sting, m_Phrase, m_HeartVoice;

        void Awake() => I = this;

        void Start()
        {
            var ad = AudioDirector.I;
            if (ad == null) return;
            m_ChaseLayer = ad.CreateVoice("ChaseLayer", transform, Bus.Music, 0f, 0.6f, 0f);
            m_Tension = ad.CreateVoice("TensionLayer", transform, Bus.Music, 0f, 0.8f, 0f);
            m_ChaseLayer.loop = true;
            m_Tension.loop = true;
            for (int i = 0; i < m_Heart.Length; i++) m_Heart[i] = ad.CreateVoice("Heart" + i, transform, Bus.SFX, 0f, 0.8f, 0f);
        }

        void OnDestroy()
        {
            if (m_Passenger != null) m_Passenger.StateChanged -= OnState;
            if (I == this) I = null;
        }

        static bool Gentle => Game.Settings != null && Game.Settings.scareIntensity == 0;
        static bool Rdr => Game.Settings != null && Game.Settings.reducedDynamicRange;

        void OnState(PassengerState s)
        {
            if (s != PassengerState.Drain || m_Passenger == null || !m_Passenger.Threatening) return;
            // The lunge: the heart slams, the player gasps.
            Fear = 1f;
            m_NextBeat = Mathf.Min(m_NextBeat, Time.time);
            PlayerAudio.I?.Gasp(0f);
        }

        void Update()
        {
            var ad = AudioDirector.I;
            if (ad == null || m_ChaseLayer == null) return;
            if (Game.Passenger != m_Passenger)
            {
                if (m_Passenger != null) m_Passenger.StateChanged -= OnState;
                m_Passenger = Game.Passenger;
                if (m_Passenger != null) m_Passenger.StateChanged += OnState;
            }
            float dt = Time.deltaTime, udt = Time.unscaledDeltaTime, now = Time.time;
            var rig = Game.Rig;
            bool failing = Game.Story != null && Game.Story.Failing;
            bool live = rig != null && !Game.InMenu && !failing;

            // ---- fear
            float target = 0f, prox = 0f;
            bool chaseNow = false, stalking = false;
            var p = m_Passenger;
            if (live && p != null && p.Visible)
            {
                if (p.InPocket) target = 0.3f; // seen through a tear
                else
                {
                    float d = Vector3.Distance(rig.Head.position, p.ChestPosition);
                    prox = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.5f, 18f, d));
                    float w = p.Threatening ? 1f : 0.55f;
                    switch (p.State)
                    {
                        case PassengerState.Dormant: break;
                        case PassengerState.Chase: target = w * (0.68f + 0.32f * prox); break;
                        case PassengerState.Drain: target = w; break;
                        case PassengerState.Repelled:
                        case PassengerState.Retreat: target = w * 0.5f * prox; break;
                        default: target = w * 0.85f * Mathf.Pow(prox, 1.2f); break;
                    }
                    chaseNow = p.Threatening && p.Chasing;
                    stalking = !chaseNow && p.State != PassengerState.Retreat && p.State != PassengerState.Repelled && p.State != PassengerState.Dormant;
                }
            }
            if (dt > 0f) Fear += (target - Fear) * (1f - Mathf.Exp(-dt / (target > Fear ? 0.4f : 5f)));
            if (!live) Fear = Mathf.MoveTowards(Fear, 0f, udt / 0.5f);

            // ---- chase on / off (with hysteresis so a stutter in the pursuit does not restart it)
            string music = ad.CurrentMusic;
            bool sceneChase = music == "mus_chase"; // a scene that runs its own chase music
            if (chaseNow && !m_Chase) BeginChase(now, sceneChase);
            else if (chaseNow) m_ChaseOffAt = -1f;
            else if (m_Chase)
            {
                if (m_ChaseOffAt < 0f) m_ChaseOffAt = now + 2.5f;
                if (now >= m_ChaseOffAt || !live) { m_Chase = false; m_ChaseOffAt = -1f; m_ChaseEndedAt = now; m_ReliefDue = true; }
            }
            if (failing) { m_StingAt = -1f; m_DipStart = -1f; m_ReliefDue = false; }

            // ---- the dip before the stinger, then the hit
            float dip = 1f;
            if (m_StingAt >= 0f)
            {
                float depth = Gentle ? 0.6f : 0.12f;
                dip = Mathf.Lerp(1f, depth, Mathf.Clamp01((now - m_DipStart) / 0.06f));
                if (now >= m_StingAt)
                {
                    m_StingAt = -1f;
                    m_DipRelease = now;
                    var st = Stings[m_Sting++ % Stings.Length];
                    ad.Play(st, null, (Gentle ? 0.45f : 0.8f), 1f, Bus.SFX, true);
                    PlayerAudio.I?.Gasp(0f);
                    if (!sceneChase) StartLayer();
                    m_NextBeat = now; // the heart kicks back in, fast
                }
            }
            else if (m_DipRelease >= 0f)
            {
                float k = Mathf.Clamp01((now - m_DipRelease) / 0.7f);
                dip = Mathf.Lerp(Gentle ? 0.6f : 0.12f, 1f, k);
                if (k >= 1f) m_DipRelease = -1f;
            }

            // ---- chase layer
            float chaseTarget = m_Chase && m_ChaseLayerOn && !sceneChase
                ? ChaseLevel * (0.8f + 0.2f * prox) * (Gentle ? 0.75f : 1f) * (Rdr ? 0.85f : 1f) : 0f;
            if (failing) chaseTarget = 0f;
            float chaseRate = failing ? 4f : chaseTarget > m_ChaseVol ? 2.5f : ChaseLevel / 4f;
            m_ChaseVol = Mathf.MoveTowards(m_ChaseVol, chaseTarget, udt * chaseRate);
            if (m_ChaseVol <= 0.001f && chaseTarget <= 0f && m_ChaseLayer.isPlaying) { m_ChaseLayer.Stop(); m_ChaseLayerOn = false; }
            ad.SetBaseVolume(m_ChaseLayer, m_ChaseVol * (Game.Paused ? 0.35f : 1f));

            // ---- tension layer: a stalker in the middle distance. mus_tension carries its own slow pulse, so it gives
            // way to the player's racing heart as the thing closes in (fear 0.35 -> 0.65, roughly 11 m -> 6 m).
            bool storyMusic = music == "mus_tension" || music == "mus_chase" || music == "mus_reveal";
            float tensionTarget = live && stalking && !m_Chase && !storyMusic && prox > 0f
                ? TensionLevel * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.35f, prox))
                  * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, Fear)))
                  * (p.Threatening ? 1f : 0.6f) * (Gentle ? 0.8f : 1f) : 0f;
            m_TensionVol = Mathf.MoveTowards(m_TensionVol, tensionTarget, udt * (failing || m_Chase ? 2f : tensionTarget > m_TensionVol ? TensionLevel / 3f : TensionLevel / 5f));
            if (m_TensionVol > 0.001f && !m_Tension.isPlaying)
            {
                var clip = ad.Clip("mus_tension");
                if (clip != null) ad.PlayOn(m_Tension, clip, 0f, 1f, false, Random.Range(0f, clip.length * 0.9f));
            }
            else if (m_TensionVol <= 0.001f && tensionTarget <= 0f && m_Tension.isPlaying) m_Tension.Stop();
            ad.SetBaseVolume(m_Tension, m_TensionVol * (Game.Paused ? 0.35f : 1f));

            // ---- a world shift's inhale (0.4 s before the slam): the beds drain away, then come back with the new place
            bool inhale = DimensionShift.Phase == DimensionShift.Stage.Inhale;
            m_ShiftDip = Mathf.MoveTowards(m_ShiftDip, inhale ? 0.15f : 1f, udt * (inhale ? 8f : 1.5f));
            dip *= m_ShiftDip;

            // ---- the cell's own music and ambience make room
            float c = m_ChaseVol / ChaseLevel, t = m_TensionVol / TensionLevel;
            float musicDuck = Mathf.Min(Mathf.Lerp(1f, 0.25f, c), Mathf.Lerp(1f, 0.5f, t)) * dip;
            float ambDuck = Mathf.Min(Mathf.Lerp(1f, 0.6f, c), Mathf.Lerp(1f, 0.8f, t)) * dip;
            ad.SetThreatDuck(musicDuck, ambDuck);

            // ---- relief once it is really over
            if (m_ReliefDue && !m_Chase && Fear < 0.3f && now - m_ChaseEndedAt < 8f) { m_ReliefDue = false; PlayerAudio.I?.Relief(); }
            else if (m_ReliefDue && now - m_ChaseEndedAt >= 8f) m_ReliefDue = false;

            Heartbeat(ad, live, now, dt);
        }

        void BeginChase(float now, bool sceneChase)
        {
            m_Chase = true;
            m_ChaseOffAt = -1f;
            if (m_ChaseVol > 0.05f) return; // picked up again while the layer was still fading: no second stinger
            if (sceneChase) return;          // the scene is already playing its own chase music
            // Silence before the scare: the world drops away, the heart skips, then the hit.
            m_DipStart = now;
            m_StingAt = now + (Gentle ? 0.12f : 0.28f);
            m_NextBeat = m_StingAt + 0.05f;
        }

        void StartLayer()
        {
            var ad = AudioDirector.I;
            var clip = ad.Clip("mus_chase");
            if (clip == null) return;
            float start = Phrases[m_Phrase++ % Phrases.Length];
            ad.PlayOn(m_ChaseLayer, clip, 0f, 1f, false, Mathf.Min(start, clip.length - 1f));
            m_ChaseLayerOn = true;
            m_ChaseVol = Mathf.Max(m_ChaseVol, 0.25f); // lands with the stinger, then swells
        }

        void Heartbeat(AudioDirector ad, bool live, float now, float dt)
        {
            if (dt <= 0f || m_Heart[0] == null) return;
            float body = PlayerAudio.I != null ? PlayerAudio.I.Breathless : 0f;
            float h = Mathf.Max(Fear, 0.5f * body);
            float level = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.85f, Fear)),
                                    0.45f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, body)));
            level *= 0.75f * (Gentle ? 0.7f : 1f) * (Rdr ? 0.8f : 1f);
            bool quiet = !live || level < 0.02f || ad.ExternalHeartbeat || m_StingAt >= 0f;
            if (quiet)
            {
                if (now > m_NextBeat) m_NextBeat = now + 0.2f;
                return;
            }
            if (now < m_NextBeat) return;
            float bpm = Mathf.Lerp(62f, Gentle ? 125f : 150f, Mathf.Pow(h, 0.9f));
            m_NextBeat = now + 60f / bpm * (1f + Random.Range(-0.025f, 0.025f));
            int k = Random.Range(0, m_Beat >= 0 ? Beats.Length - 1 : Beats.Length);
            if (m_Beat >= 0 && k >= m_Beat) k++;
            m_Beat = k;
            var clip = ad.Clip(Beats[k]);
            if (clip == null) return;
            var voice = m_Heart[m_HeartVoice];
            m_HeartVoice = (m_HeartVoice + 1) % m_Heart.Length;
            ad.PlayOn(voice, clip, level * Random.Range(0.92f, 1f), Mathf.Lerp(0.98f, 1.08f, h) + Random.Range(-0.01f, 0.01f));
        }
    }
}
