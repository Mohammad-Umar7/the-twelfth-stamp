using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// The player's body. Breathing follows the rig: quiet nasal breaths at a walk; while sprinting, rhythmic 'hmf'
    /// exhales and quick inhales loosely locked to the footsteps (each breath snaps to a step that lands near its
    /// time), turning ragged as Exertion builds (voiced, rasping breaths, uneven timing, a catch now and then);
    /// after a run, panting that slows and fades over ~10 s. With the Passenger close the breath turns sharp and
    /// trembling; very close and still, the player holds it, then lets it out shakily. A gasp lands with a chase
    /// start, the lunge, a world-shift arrival and being caught. Running strides add cloth rustle and a heel-heavy
    /// layer on the surface; wet stone (Paris) adds a splash. 2D at the head, a little room reverb, ducked under
    /// narration, silent while Saeed speaks. Three pooled breath voices; nothing allocates per frame.
    /// </summary>
    public class PlayerAudio : MonoBehaviour
    {
        public static PlayerAudio I { get; private set; }

        /// <summary>0..1 breathlessness: rises with exertion at once, eases off over ~10 s (ThreatAudio's heartbeat uses it).</summary>
        public float Breathless { get; private set; }

        enum Mode { Off, Calm, Run, Pant, Fear, Hold }

        /// <summary>A family of clip variants that never plays the same one twice in a row.</summary>
        class ClipSet
        {
            readonly string[] m_Ids;
            int m_Last = -1;

            public ClipSet(string prefix, int count)
            {
                m_Ids = new string[count];
                for (int i = 0; i < count; i++) m_Ids[i] = prefix + "_" + (i + 1);
            }

            public string Next()
            {
                int n = m_Ids.Length;
                int k = Random.Range(0, m_Last >= 0 && n > 1 ? n - 1 : n);
                if (m_Last >= 0 && n > 1 && k >= m_Last) k++;
                m_Last = k;
                return m_Ids[k];
            }
        }

        static readonly ClipSet CalmIn = new ClipSet("breath_calm_in", 3), CalmOut = new ClipSet("breath_calm_out", 3);
        static readonly ClipSet RunIn = new ClipSet("breath_run_in", 6), RunOut = new ClipSet("breath_run_out", 6);
        static readonly ClipSet HardIn = new ClipSet("breath_hard_in", 4), HardOut = new ClipSet("breath_hard_out", 4);
        static readonly ClipSet PantIn = new ClipSet("breath_pant_in", 4), PantOut = new ClipSet("breath_pant_out", 4);
        static readonly ClipSet FearIn = new ClipSet("breath_fear_in", 4), FearOut = new ClipSet("breath_fear_out", 3);
        static readonly ClipSet Gasps = new ClipSet("breath_gasp", 3), Reliefs = new ClipSet("breath_relief", 2);
        static readonly ClipSet Cloth = new ClipSet("cloth_run", 6);
        static readonly string[] Surfaces = { "stone", "tile", "wood", "sand", "grass", "metal" };
        static readonly string[] RunLayer = { "foot_run_stone_1", "foot_run_tile_1", "foot_run_wood_1", "foot_run_sand_1", "foot_run_grass_1", "foot_run_metal_1" };

        readonly AudioSource[] m_Breath = new AudioSource[3];
        AudioSource m_Cloth;
        int m_Voice;

        PlayerRig m_Rig;
        Cell m_LastCell;
        Mode m_Mode = Mode.Off;
        bool m_NextIsIn = true;
        float m_Next, m_Gap = 1f, m_StepTime = -1f, m_HoldUntil, m_HoldCooldown, m_GaspAt = -1f, m_CatchAt = -1f;
        bool m_ReliefPending, m_WasFailing;

        void Awake() => I = this;

        void Start()
        {
            var ad = AudioDirector.I;
            if (ad == null) return;
            // Breath sits at the head (2D) with a little of the room on it; narration pulls it down 5 dB.
            for (int i = 0; i < m_Breath.Length; i++) m_Breath[i] = ad.CreateVoice("Breath" + i, transform, Bus.SFX, 0f, 0.8f, 0.35f);
            m_Cloth = ad.CreateVoice("Cloth", transform, Bus.SFX, 0f, 0.6f, 0.3f);
        }

        void OnDestroy()
        {
            if (m_Rig != null) m_Rig.Footstep -= OnStep;
            if (I == this) I = null;
        }

        void Rebind(PlayerRig rig)
        {
            if (m_Rig != null) m_Rig.Footstep -= OnStep;
            m_Rig = rig;
            if (m_Rig != null) m_Rig.Footstep += OnStep;
        }

        static bool Gentle => Game.Settings != null && Game.Settings.scareIntensity == 0;
        static bool Rdr => Game.Settings != null && Game.Settings.reducedDynamicRange;

        static bool Active(PlayerRig rig) =>
            rig != null && !Game.InMenu && (Game.Story == null || Game.Story.InCampaign || Game.IsDemo);

        // ------------------------------------------------------------------ requests (ThreatAudio, story beats)
        /// <summary>A sharp gasp after delay seconds (a chase starts, the lunge, landing after a shift).</summary>
        public void Gasp(float delay = 0f)
        {
            m_GaspAt = Time.time + Mathf.Max(0f, delay);
            m_HoldUntil = 0f;
        }

        /// <summary>Hold the breath (silence before the scare); it comes out shakily afterwards.</summary>
        public void HoldBreath(float seconds) => m_HoldUntil = Mathf.Max(m_HoldUntil, Time.time + seconds);

        /// <summary>The danger has passed: the next exhale is a long shaky one (ignored while running).</summary>
        public void Relief()
        {
            if (m_Mode != Mode.Run) m_ReliefPending = true;
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            var rig = Game.Rig;
            if (rig != m_Rig) Rebind(rig);
            float dt = Time.deltaTime;
            float now = Time.time;
            bool failing = Game.Story != null && Game.Story.Failing;
            if (failing && !m_WasFailing && rig != null) Play(Gasps.Next(), Gentle ? 0.55f : 0.85f, Random.Range(0.92f, 1.0f)); // caught
            m_WasFailing = failing;
            if (failing || !Active(rig))
            {
                m_Mode = Mode.Off;
                m_GaspAt = -1f; m_CatchAt = -1f; m_HoldUntil = 0f;
                Breathless = Mathf.MoveTowards(Breathless, 0f, dt / 3f);
                m_LastCell = Game.Cells != null ? Game.Cells.Active : null;
                return;
            }
            if (dt <= 0f) return; // paused

            float speed = rig.MoveSpeed;
            bool running = rig.Sprinting && speed > 2.0f;
            float fear = ThreatAudio.I != null ? ThreatAudio.I.Fear : 0f;
            if (DimensionShift.Running) fear = Mathf.Max(fear, 0.55f);

            // Breathlessness: straight up with effort, back down over ~10 s (never below the rig's exertion).
            float effort = running ? Mathf.Max(rig.Exertion, 0.3f) : rig.Exertion;
            Breathless = effort >= Breathless ? Mathf.MoveTowards(Breathless, effort, dt * 2f)
                                              : Mathf.Max(effort, Breathless - Breathless * dt / 4.5f - dt * 0.01f);

            // Landing after a world shift: the cell swaps behind the fade, he gasps as it lifts.
            var cell = Game.Cells != null ? Game.Cells.Active : null;
            if (cell != m_LastCell)
            {
                if (DimensionShift.Running && m_LastCell != null) Gasp(0.35f);
                m_LastCell = cell;
            }

            // The Passenger right there and the player keeping still: hold the breath.
            var p = Game.Passenger;
            if (now > m_HoldCooldown && !running && speed < 0.4f && p != null && p.Visible && !p.InPocket && !p.Chasing && fear > 0.55f
                && Vector3.Distance(rig.Head.position, p.ChestPosition) < 4.5f)
            {
                HoldBreath(Random.Range(3.5f, 5.5f));
                m_HoldCooldown = now + 18f;
            }

            if (m_GaspAt >= 0f && now >= m_GaspAt)
            {
                m_GaspAt = -1f;
                Play(Gasps.Next(), (Gentle ? 0.55f : 0.85f) * (Rdr ? 0.75f : 1f), Random.Range(0.95f, 1.04f));
                m_NextIsIn = false;
                m_Next = now + 0.5f;
                m_Gap = 0.5f;
            }
            if (m_CatchAt >= 0f && now >= m_CatchAt)
            {
                m_CatchAt = -1f;
                Play(HardIn.Next(), 0.32f, Random.Range(1.02f, 1.1f)); // the breath catches
            }

            var mode = now < m_HoldUntil ? Mode.Hold : running ? Mode.Run : fear >= 0.4f ? Mode.Fear : Breathless >= 0.12f ? Mode.Pant : Mode.Calm;
            if (mode != m_Mode)
            {
                if (mode == Mode.Run && m_Mode != Mode.Run) { m_NextIsIn = true; m_Next = Mathf.Min(m_Next, now + 0.12f); }
                else if (m_Mode == Mode.Hold) { m_NextIsIn = false; m_Next = now; } // let it out
                else if (m_Mode == Mode.Run) m_Next = Mathf.Min(m_Next, now + 0.3f);
                else if (m_Mode == Mode.Off || m_Mode == Mode.Calm) m_Next = Mathf.Min(m_Next, now + 0.4f);
                m_Mode = mode;
            }
            if (mode == Mode.Hold || m_GaspAt >= 0f) return;
            if (SaeedSpeaking()) { m_Next = Mathf.Max(m_Next, now + 0.25f); return; }

            bool due;
            if (mode == Mode.Run)
            {
                // Loosely locked to the stride: a step landing close to the breath's time takes it (window
                // -18 % / +15 % of the gap, so fast strides do not hurry the breathing).
                bool stepNow = m_StepTime >= now - 0.02f;
                due = (stepNow && now >= m_Next - m_Gap * 0.18f) || now >= m_Next + m_Gap * 0.15f;
            }
            else due = now >= m_Next;
            if (due) Breathe(mode, fear, speed, now);
        }

        void Breathe(Mode mode, float fear, float speed, float now)
        {
            bool inhale = m_NextIsIn;
            float e = Breathless, period, toNext, vol;
            string id;
            float pitch = 1f + Random.Range(-0.03f, 0.03f);
            float ragged = 0f;
            switch (mode)
            {
                case Mode.Run:
                {
                    float hard = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.9f, e));
                    ragged = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, e));
                    if (inhale)
                    {
                        id = fear > 0.5f && Random.value < 0.35f ? FearIn.Next() : Random.value < hard ? HardIn.Next() : RunIn.Next();
                        vol = Mathf.Lerp(0.42f, 0.62f, e);
                    }
                    else
                    {
                        id = Random.value < hard ? HardOut.Next() : RunOut.Next();
                        vol = Mathf.Lerp(0.5f, 0.76f, e);
                        if (ragged > 0.3f && Random.value < 0.1f * ragged) m_CatchAt = now + Random.Range(0.16f, 0.22f);
                    }
                    if (Rdr) vol *= 0.85f;
                    pitch += 0.02f * e;
                    period = Mathf.Lerp(1.15f, 0.75f, e); // ~52 -> 80 breaths a minute
                    toNext = period * (inhale ? 0.42f : 0.58f);
                    break;
                }
                case Mode.Pant:
                {
                    bool heavy = Breathless > 0.75f && Random.value < 0.3f;
                    id = inhale ? (heavy ? HardIn.Next() : PantIn.Next()) : (heavy ? HardOut.Next() : PantOut.Next());
                    vol = Mathf.Lerp(0.3f, 0.68f, Breathless);
                    period = Mathf.Lerp(2.6f, 0.7f, Breathless);
                    toNext = period * (inhale ? 0.45f : 0.55f);
                    break;
                }
                case Mode.Fear:
                    id = inhale ? FearIn.Next() : FearOut.Next();
                    vol = Mathf.Lerp(0.38f, 0.72f, Mathf.InverseLerp(0.4f, 1f, fear)) * (Gentle ? 0.75f : 1f) * (Rdr ? 0.85f : 1f);
                    period = Mathf.Lerp(3.0f, 1.5f, fear);
                    toNext = period * (inhale ? 0.42f : 0.58f);
                    break;
                default: // calm
                {
                    bool moving = speed > 0.6f;
                    id = inhale ? CalmIn.Next() : CalmOut.Next();
                    vol = moving ? 0.5f : 0.3f;
                    period = moving ? 3.4f : 4.6f;
                    toNext = period * (inhale ? 0.42f : 0.58f);
                    break;
                }
            }
            if (!inhale && m_ReliefPending && mode != Mode.Run)
            {
                id = Reliefs.Next();
                vol = 0.55f;
                toNext = 1.6f;
                m_ReliefPending = false;
            }
            Play(id, vol, pitch);
            m_NextIsIn = !inhale;
            m_Gap = toNext * (1f + Random.Range(-1f, 1f) * (0.05f + 0.12f * ragged));
            m_Next = now + m_Gap;
        }

        void Play(string id, float volume, float pitch)
        {
            var ad = AudioDirector.I;
            if (ad == null || m_Breath[0] == null) return;
            var clip = ad.Clip(id);
            if (clip == null) return;
            var voice = m_Breath[m_Voice];
            m_Voice = (m_Voice + 1) % m_Breath.Length;
            ad.PlayOn(voice, clip, volume, pitch);
        }

        bool SaeedSpeaking()
        {
            var n = Narration.I;
            if (n == null || !n.IsSpeaking || AudioDirector.I == null) return false;
            var info = AudioDirector.I.Info(n.CurrentId);
            return info != null && info.speaker == "saeed";
        }

        // ------------------------------------------------------------------ strides
        void OnStep(string surface, bool running)
        {
            var rig = m_Rig;
            var ad = AudioDirector.I;
            if (ad == null || !Active(rig) || (Game.Story != null && Game.Story.Failing)) return;
            m_StepTime = Time.time;
            var feet = rig.Feet;
            if (running)
            {
                // The body's weight lands and the foot drives off: a heel-heavy layer under the rig's own step.
                ad.Play(RunLayerFor(surface), feet, Mathf.Lerp(0.38f, 0.5f, Breathless), Random.Range(0.95f, 1.05f));
                if (Random.value < 0.7f) PlayCloth(Mathf.Lerp(0.32f, 0.45f, Breathless));
            }
            else if (rig.MoveSpeed > 1.2f && Random.value < 0.18f) PlayCloth(0.14f);
            var cell = Game.Cells != null ? Game.Cells.Active : null;
            if (cell != null && cell.Id == CellId.Paris && surface == "stone")
                ad.Play("foot_wet_1", feet, running ? 0.45f : 0.32f, Random.Range(0.94f, 1.06f));
        }

        static string RunLayerFor(string surface)
        {
            for (int i = 0; i < Surfaces.Length; i++) if (Surfaces[i] == surface) return RunLayer[i];
            return RunLayer[0];
        }

        void PlayCloth(float volume)
        {
            var ad = AudioDirector.I;
            if (ad == null || m_Cloth == null) return;
            var clip = ad.Clip(Cloth.Next());
            if (clip != null) ad.PlayOn(m_Cloth, clip, volume, Random.Range(0.94f, 1.08f));
        }
    }
}
