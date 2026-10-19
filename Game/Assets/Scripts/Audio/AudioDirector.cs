using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    public enum Bus { Music, Ambience, SFX, Voice, UI }

    [Serializable]
    public class AudioClipInfo
    {
        public string id;
        public string file;
        public string bus;
        public float duration;
        public bool loop;
        public string source;
        public string license;
        public string transcript;
        public string speaker;
        /// <summary>Seconds into the clip of its dramatic peak (transitions, stingers); 0 when not marked.</summary>
        public float peak;
    }

    [Serializable]
    class AudioManifest
    {
        public int version;
        public List<AudioClipInfo> clips = new List<AudioClipInfo>();
    }

    /// <summary>
    /// Software audio buses (Master, Music, Ambience, SFX, Voice, UI). Every source the game creates is registered
    /// with a bus; volume sliders change bus gains that are applied to all registered sources each frame. Narration
    /// ducks music, ambience, SFX (lightly) and the player's body sounds (breath, heartbeat). "Reduced dynamic range"
    /// lowers marked scare peaks and lifts quiet beds. One-shots come from a pool (no allocation per sound); 3D
    /// sources fall off with distance (inverse distance, easing to silence at max range). Spatialisation is Unity's
    /// built-in 3D panning (no HRTF plug-in is installed). Hosts PlayerAudio (breathing, cloth, running steps),
    /// ThreatAudio (heartbeat, chase/tension music, stingers) and Acoustics (per-place reverb).
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector I { get; private set; }

        class Tracked
        {
            public AudioSource Source;
            public Bus Bus;
            public float BaseVolume;
            public bool Peak;
            public float Fade = 1f;
            public float FadeTarget = 1f;
            public float FadeSpeed;
            public bool DestroyWhenFaded;
            public float DuckDepth;      // 0..1: how far narration pulls this source down
            public bool ThreatDucked;    // base music / ambience beds: ThreatAudio lowers them under a chase
            public bool Pooled;          // one-shot voice: returned to the pool instead of destroyed
            public bool Persistent;      // owned by a component: never recycled or destroyed by the director
            public bool HoldsVoice;      // a Voice-bus one-shot: counts as narration for ducking while it plays
            public float BusyUntil;      // pooled: clock time before which the voice is not reused
            public int CurveKey = -1;    // rolloff curve last applied (skip re-sending the same curve)
        }

        class VariantGroup
        {
            public string[] Ids;
            public int Last = -1;
        }

        const int MaxPool = 48;
        const float DefaultMax = 30f;

        readonly List<Tracked> m_Tracked = new List<Tracked>();
        readonly Dictionary<AudioSource, Tracked> m_Index = new Dictionary<AudioSource, Tracked>();
        readonly List<Tracked> m_Pool = new List<Tracked>();
        readonly Dictionary<string, AudioClipInfo> m_Info = new Dictionary<string, AudioClipInfo>();
        readonly Dictionary<string, AudioClip> m_Clips = new Dictionary<string, AudioClip>();
        readonly HashSet<string> m_Missing = new HashSet<string>();
        readonly Dictionary<string, VariantGroup> m_VariantOf = new Dictionary<string, VariantGroup>();
        readonly List<Tracked> m_HeartLoops = new List<Tracked>();
        readonly float[] m_Gain = new float[5];

        Tracked m_AmbA, m_MusA;
        string m_AmbId, m_MusId;
        float m_Duck = 1f;
        int m_VoiceActive;
        float m_ThreatMusic = 1f, m_ThreatAmbience = 1f;

        /// <summary>Far-carrying one-shots: (min, max) metres. Everything else: min as asked, max = max(30, 20 x min).</summary>
        static readonly Dictionary<string, Vector2> s_Range = new Dictionary<string, Vector2>
        {
            { "clock_chime", new Vector2(8f, 140f) },
            { "drum_hit", new Vector2(3f, 60f) },
            { "tear_breach", new Vector2(2f, 45f) },
            { "booth_power", new Vector2(2f, 35f) },
            { "seal_counterfeit", new Vector2(2f, 35f) },
        };

        static readonly Dictionary<int, AnimationCurve> s_Curves = new Dictionary<int, AnimationCurve>();

        public static void Create(Transform parent)
        {
            if (I != null) return;
            var go = new GameObject("AudioDirector");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<AudioDirector>();
            I.LoadManifest();
            I.RefreshGains();
            // The body, the threat and the room: persistent like the director, created with it.
            NewChild<PlayerAudio>(go.transform, "PlayerAudio");
            NewChild<ThreatAudio>(go.transform, "ThreatAudio");
            NewChild<AmbientScares>(go.transform, "AmbientScares");
            NewChild<Acoustics>(go.transform, "Acoustics");
        }

        static T NewChild<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        void LoadManifest()
        {
            var json = Resources.Load<TextAsset>("Audio/audio_manifest");
            if (json == null) { Debug.LogWarning("[TS-Audio] audio_manifest.json not found; clips load by convention"); return; }
            try
            {
                var m = JsonUtility.FromJson<AudioManifest>(json.text);
                foreach (var c in m.clips) if (!string.IsNullOrEmpty(c.id)) m_Info[c.id] = c;
                BuildVariantGroups();
                Debug.Log("[TS-Audio] Manifest clips: " + m_Info.Count);
            }
            catch (Exception e) { Debug.LogWarning("[TS-Audio] Bad manifest: " + e.Message); }
        }

        /// <summary>
        /// Footstep ids ("foot_stone_3") ask for a random variant; the director picks from the surface's full set
        /// without repeating the last one, so a fixed caller range still gets every variant (and no doubles).
        /// </summary>
        void BuildVariantGroups()
        {
            var groups = new Dictionary<string, List<string>>();
            foreach (var id in m_Info.Keys)
            {
                if (!id.StartsWith("foot_", StringComparison.Ordinal) && !id.StartsWith("pass_step_", StringComparison.Ordinal)) continue;
                int u = id.LastIndexOf('_');
                if (u <= 0 || !int.TryParse(id.Substring(u + 1), out _)) continue;
                string key = id.Substring(0, u);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<string>();
                list.Add(id);
            }
            foreach (var kv in groups)
            {
                if (kv.Value.Count < 2) continue;
                var g = new VariantGroup { Ids = kv.Value.ToArray() };
                foreach (var id in g.Ids) m_VariantOf[id] = g;
            }
        }

        string Variant(string id)
        {
            if (!m_VariantOf.TryGetValue(id, out var g)) return id;
            int n = g.Ids.Length;
            int k = UnityEngine.Random.Range(0, g.Last >= 0 ? n - 1 : n);
            if (g.Last >= 0 && k >= g.Last) k++;
            g.Last = k;
            return g.Ids[k];
        }

        public AudioClipInfo Info(string id) => id != null && m_Info.TryGetValue(id, out var i) ? i : null;

        public AudioClip Clip(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (m_Clips.TryGetValue(id, out var c)) return c;
            if (m_Missing.Contains(id)) return null;
            string path = Info(id)?.file;
            if (string.IsNullOrEmpty(path))
            {
                foreach (var folder in new[] { "SFX", "Voice", "Ambience", "Music" })
                {
                    c = Resources.Load<AudioClip>("Audio/" + folder + "/" + id);
                    if (c != null) break;
                }
            }
            else c = Resources.Load<AudioClip>(path);
            if (c == null)
            {
                m_Missing.Add(id);
                Debug.LogWarning("[TS-Audio] Missing clip: " + id);
                return null;
            }
            m_Clips[id] = c;
            return c;
        }

        public bool Has(string id) => Clip(id) != null;

        // ------------------------------------------------------------------ gains
        float SettingsGain(Bus bus)
        {
            var s = Game.Settings ?? new GameSettings();
            float g = s.master;
            switch (bus)
            {
                case Bus.Music: g *= s.music * (s.reducedDynamicRange ? 0.9f : 1f); break;
                case Bus.Ambience: g *= s.ambience * (s.reducedDynamicRange ? 1.25f : 1f); break;
                case Bus.SFX: g *= s.sfx; break;
                case Bus.Voice: g *= s.voice; break;
                case Bus.UI: g *= s.ui; break;
            }
            return g;
        }

        /// <summary>How far narration pulls a bus down by default (music 100 %, ambience 60 %, SFX 30 % of the duck).</summary>
        static float DefaultDuck(Bus bus) => bus == Bus.Music ? 1f : bus == Bus.Ambience ? 0.6f : bus == Bus.SFX ? 0.3f : 0f;

        public float BusGain(Bus bus) => SettingsGain(bus) * Mathf.Lerp(1f, m_Duck, DefaultDuck(bus));

        float Volume(Tracked t, bool rdr)
        {
            float v = t.BaseVolume * t.Fade * (t.Peak && rdr ? 0.6f : 1f) * m_Gain[(int)t.Bus] * Mathf.Lerp(1f, m_Duck, t.DuckDepth);
            if (t.ThreatDucked) v *= t.Bus == Bus.Music ? m_ThreatMusic : t.Bus == Bus.Ambience ? m_ThreatAmbience : 1f;
            return v;
        }

        /// <summary>ThreatAudio: multipliers for the base music and ambience beds (a chase, a stalker nearby, a scare dip).</summary>
        public void SetThreatDuck(float music, float ambience)
        {
            m_ThreatMusic = Mathf.Clamp01(music);
            m_ThreatAmbience = Mathf.Clamp01(ambience);
        }

        static float Clock(AudioSource s) => s.ignoreListenerPause ? Time.unscaledTime : Time.time;

        void RefreshGains()
        {
            for (int b = 0; b < m_Gain.Length; b++) m_Gain[b] = SettingsGain((Bus)b);
        }

        void Update()
        {
            float duckTarget = m_VoiceActive > 0 ? 0.45f : 1f;
            // Duck in quickly when a line starts, come back up gently after it.
            m_Duck = Mathf.MoveTowards(m_Duck, duckTarget, Time.unscaledDeltaTime * (duckTarget < m_Duck ? 2.4f : 0.8f));
            bool rdr = Game.Settings != null && Game.Settings.reducedDynamicRange;
            RefreshGains();
            for (int i = m_Tracked.Count - 1; i >= 0; i--)
            {
                var t = m_Tracked[i];
                if (t.Source == null)
                {
                    if (t.HoldsVoice) m_VoiceActive = Mathf.Max(0, m_VoiceActive - 1);
                    if ((object)t.Source != null) m_Index.Remove(t.Source);
                    m_Tracked.RemoveAt(i);
                    continue;
                }
                if (t.HoldsVoice && Clock(t.Source) >= t.BusyUntil && !t.Source.isPlaying)
                {
                    t.HoldsVoice = false;
                    m_VoiceActive = Mathf.Max(0, m_VoiceActive - 1);
                }
                if (t.FadeSpeed > 0f)
                {
                    t.Fade = Mathf.MoveTowards(t.Fade, t.FadeTarget, t.FadeSpeed * Time.unscaledDeltaTime);
                    if (Mathf.Approximately(t.Fade, t.FadeTarget)) t.FadeSpeed = 0f;
                    if (t.DestroyWhenFaded && t.Fade <= 0.0001f)
                    {
                        t.Source.Stop();
                        t.DestroyWhenFaded = false;
                        if (t.Pooled || t.Persistent) { t.Fade = 1f; t.FadeTarget = 1f; t.BusyUntil = 0f; continue; }
                        m_Index.Remove(t.Source);
                        Destroy(t.Source.gameObject);
                        m_Tracked.RemoveAt(i);
                        continue;
                    }
                }
                if (t.Pooled && !t.Source.isPlaying) continue; // idle pool voice
                t.Source.volume = Volume(t, rdr);
            }
        }

        Tracked Track(AudioSource src, Bus bus, float vol, bool peak)
        {
            var t = new Tracked { Source = src, Bus = bus, BaseVolume = vol, Peak = peak, DuckDepth = DefaultDuck(bus) };
            m_Tracked.Add(t);
            m_Index[src] = t;
            src.volume = vol * BusGain(bus);
            src.ignoreListenerPause = bus == Bus.Music || bus == Bus.UI;
            return t;
        }

        public void SetBaseVolume(AudioSource src, float vol)
        {
            if (src != null && m_Index.TryGetValue(src, out var t)) t.BaseVolume = vol;
        }

        public void FadeOutAndStop(AudioSource src, float seconds)
        {
            if (src == null) return;
            if (m_Index.TryGetValue(src, out var t))
            {
                t.FadeTarget = 0f; t.FadeSpeed = 1f / Mathf.Max(0.01f, seconds); t.DestroyWhenFaded = true;
                return;
            }
            Destroy(src.gameObject);
        }

        // ------------------------------------------------------------------ sources
        AudioSource NewSource(string name, Transform parent, Vector3? pos, float spatial)
        {
            var go = new GameObject("Audio_" + name);
            if (parent != null) { go.transform.SetParent(parent, false); go.transform.localPosition = Vector3.zero; }
            else go.transform.SetParent(transform, false);
            if (pos.HasValue) go.transform.position = pos.Value;
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.dopplerLevel = 0f;
            src.spread = 30f;
            Configure(null, src, Bus.SFX, spatial, 1.2f, DefaultMax);
            return src;
        }

        /// <summary>Spatial blend, distance rolloff and reverb send for a source.</summary>
        void Configure(Tracked t, AudioSource src, Bus bus, float spatial, float minDistance, float maxDistance, float reverbMix = 1f)
        {
            src.spatialBlend = spatial;
            minDistance = Mathf.Max(0.1f, minDistance);
            maxDistance = Mathf.Max(minDistance * 2f, maxDistance);
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            if (spatial > 0f)
            {
                int key = Mathf.RoundToInt(minDistance * 10f) * 10000 + Mathf.RoundToInt(maxDistance);
                if (t == null || t.CurveKey != key)
                {
                    src.rolloffMode = AudioRolloffMode.Custom;
                    src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff(minDistance, maxDistance, key));
                    if (t != null) t.CurveKey = key;
                }
            }
            // Music, UI, beds and inner-voice narration stay out of the room's reverb; things in the world go in.
            bool dry = bus == Bus.Music || bus == Bus.UI || (spatial < 0.5f && reverbMix >= 1f);
            src.bypassReverbZones = dry || reverbMix <= 0f;
            src.reverbZoneMix = Mathf.Clamp(reverbMix, 0f, 1.1f);
        }

        /// <summary>Inverse distance (-6 dB per doubling) from min, eased to silence over the last 30 % of the range.</summary>
        static AnimationCurve Rolloff(float min, float max, int key)
        {
            if (s_Curves.TryGetValue(key, out var c)) return c;
            var xs = new List<float> { 0f };
            for (float d = min; d < max * 0.8f; d *= 1.3f) xs.Add(d / max);
            xs.Add(0.85f); xs.Add(0.93f); xs.Add(1f);
            xs.Sort();
            for (int i = xs.Count - 1; i > 0; i--) if (xs[i] - xs[i - 1] < 0.01f) xs.RemoveAt(i - 1); // no near-duplicate keys
            var keys = new Keyframe[xs.Count];
            for (int i = 0; i < xs.Count; i++) keys[i] = new Keyframe(xs[i], RolloffGain(xs[i] * max, min, max));
            for (int i = 0; i < keys.Length; i++)
            {
                int a = Mathf.Max(0, i - 1), b = Mathf.Min(keys.Length - 1, i + 1);
                float slope = b > a ? (keys[b].value - keys[a].value) / Mathf.Max(1e-4f, keys[b].time - keys[a].time) : 0f;
                keys[i].inTangent = keys[i].outTangent = keys[i].time <= min / max ? 0f : slope;
            }
            c = new AnimationCurve(keys);
            s_Curves[key] = c;
            return c;
        }

        static float RolloffGain(float d, float min, float max)
        {
            float g = d <= min ? 1f : min / d;
            float f = Mathf.Clamp01(Mathf.InverseLerp(max, max * 0.7f, d));
            return g * f * f * (3f - 2f * f);
        }

        Tracked Rent(Transform follow)
        {
            // Reuse the voice that has been idle longest, so a caller still holding an old Play() result is unlikely to
            // touch a newer sound.
            Tracked best = null;
            for (int i = m_Pool.Count - 1; i >= 0; i--)
            {
                var v = m_Pool[i];
                if (v.Source == null) { m_Pool.RemoveAt(i); continue; }
                if (Clock(v.Source) < v.BusyUntil || v.Source.isPlaying) continue;
                if (best == null || v.BusyUntil < best.BusyUntil) best = v;
            }
            if (best == null && m_Pool.Count >= MaxPool)
            {
                // Everything is busy: take the voice closest to finishing (never a marked scare peak if avoidable).
                float soonest = float.MaxValue;
                foreach (var v in m_Pool)
                {
                    float left = v.BusyUntil - Clock(v.Source) + (v.Peak ? 100f : 0f);
                    if (left < soonest) { soonest = left; best = v; }
                }
                if (best != null)
                {
                    best.Source.Stop();
                    if (best.HoldsVoice) { best.HoldsVoice = false; m_VoiceActive = Mathf.Max(0, m_VoiceActive - 1); }
                }
            }
            if (best == null)
            {
                var src = NewSource("oneshot", null, null, 0f);
                best = Track(src, Bus.SFX, 0f, false);
                best.Pooled = true;
                m_Pool.Add(best);
            }
            var tr = best.Source.transform;
            var parent = follow != null ? follow : transform;
            if (tr.parent != parent) tr.SetParent(parent, false);
            tr.localPosition = Vector3.zero;
            best.Source.mute = false; // a cell shown as a tear pocket mutes the sources under it; a reused voice starts clean
            return best;
        }

        // ------------------------------------------------------------------ playback
        /// <summary>Fire-and-forget sound. Returns the source (or null if the clip is missing).</summary>
        public AudioSource Play(string id, Vector3? position = null, float volume = 1f, float pitch = 1f, Bus bus = Bus.SFX, bool peak = false, Transform follow = null, float minDistance = 1.2f)
            => PlayFrom(id, 0f, 0f, position, volume, pitch, bus, peak, follow, minDistance);

        public AudioSource PlayUI(string id, float volume = 1f) => Play(id, null, volume, 1f, Bus.UI);

        /// <summary>
        /// Plays a clip so that its marked peak (manifest "peak", e.g. shift_rumble 4.0 s) lands secondsToPeak from now:
        /// started part-way in when there is less time than its build-up, delayed when there is more. 2D. Returns the
        /// source so a skipped transition can FadeOutAndStop it.
        /// </summary>
        public AudioSource PlayToPeak(string id, float secondsToPeak, float volume = 1f, Bus bus = Bus.SFX, bool peak = true)
        {
            float p = Info(id)?.peak ?? 0f;
            return PlayFrom(id, Mathf.Max(0f, p - secondsToPeak), Mathf.Max(0f, secondsToPeak - p), null, volume, 1f, bus, peak, null, 1.2f);
        }

        AudioSource PlayFrom(string id, float startTime, float delay, Vector3? position, float volume, float pitch, Bus bus, bool peak, Transform follow, float minDistance)
        {
            if (string.IsNullOrEmpty(id)) return null;
            id = Variant(id);
            var clip = Clip(id);
            if (clip == null) return null;
            bool spatial = position.HasValue || follow != null;
            var t = Rent(follow);
            var src = t.Source;
            if (position.HasValue && follow == null) src.transform.position = position.Value;
            float max = Mathf.Max(DefaultMax, minDistance * 20f);
            if (s_Range.TryGetValue(id, out var r)) { minDistance = Mathf.Max(minDistance, r.x); max = r.y; }
            else if (id.StartsWith("pass_", StringComparison.Ordinal)) max = 40f;          // the Passenger's cues carry
            else if (id.StartsWith("scare_", StringComparison.Ordinal)) max = 60f;         // AmbientScares: a street away still reaches you
            else if (id.StartsWith("foot_", StringComparison.Ordinal)) minDistance = Mathf.Max(minDistance, 1.8f); // own feet: no head-to-floor loss
            Configure(t, src, bus, spatial ? 1f : 0f, minDistance, max);
            src.clip = clip;
            src.loop = false;
            src.pitch = pitch;
            src.ignoreListenerPause = bus == Bus.Music || bus == Bus.UI;
            t.Bus = bus; t.BaseVolume = volume; t.Peak = peak;
            t.Fade = 1f; t.FadeTarget = 1f; t.FadeSpeed = 0f; t.DestroyWhenFaded = false;
            t.DuckDepth = DefaultDuck(bus); t.ThreatDucked = false;
            src.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.02f));
            src.volume = Volume(t, Game.Settings != null && Game.Settings.reducedDynamicRange);
            if (delay > 0.001f) src.PlayDelayed(delay); else src.Play();
            t.BusyUntil = Clock(src) + delay + (clip.length - src.time) / Mathf.Max(0.05f, Mathf.Abs(pitch)) + 0.05f;
            if (bus == Bus.Voice && !t.HoldsVoice) { t.HoldsVoice = true; m_VoiceActive++; }
            return src;
        }

        /// <summary>Looping source that the caller controls (volume via SetBaseVolume, stop via FadeOutAndStop).</summary>
        public AudioSource Loop(string id, Transform parent, float volume, Bus bus = Bus.SFX, float spatial = 1f, float minDistance = 1.5f, float maxDistance = 25f)
        {
            var clip = Clip(id);
            if (clip == null) return null;
            var src = NewSource(id, parent, null, spatial);
            src.clip = clip;
            src.loop = true;
            var t = Track(src, bus, volume, false);
            Configure(t, src, bus, spatial, minDistance, maxDistance);
            if (id == "heartbeat_loop") m_HeartLoops.Add(t);
            src.time = UnityEngine.Random.Range(0f, clip.length * 0.9f);
            src.Play();
            return src;
        }

        /// <summary>A heartbeat loop someone else is playing (the dimension shift, the ending): ThreatAudio stays quiet.</summary>
        public bool ExternalHeartbeat
        {
            get
            {
                for (int i = m_HeartLoops.Count - 1; i >= 0; i--)
                {
                    var t = m_HeartLoops[i];
                    if (t.Source == null) { m_HeartLoops.RemoveAt(i); continue; }
                    if (t.Source.isPlaying && t.BaseVolume * t.Fade > 0.02f) return true;
                }
                return false;
            }
        }

        public void SetAmbience(string id, float fadeSeconds = 2.5f, float volume = 0.8f)
        {
            if (id == m_AmbId) return;
            m_AmbId = id;
            if (m_AmbA != null && m_AmbA.Source != null) FadeOutAndStop(m_AmbA.Source, fadeSeconds);
            m_AmbA = null;
            if (string.IsNullOrEmpty(id)) return;
            var src = Loop(id, transform, volume, Bus.Ambience, 0f);
            if (src == null || !m_Index.TryGetValue(src, out var t)) return;
            t.Fade = 0f; t.FadeTarget = 1f; t.FadeSpeed = 1f / Mathf.Max(0.01f, fadeSeconds); t.ThreatDucked = true;
            m_AmbA = t;
        }

        public void SetMusic(string id, float fadeSeconds = 3f, float volume = 0.8f, bool loop = true)
        {
            if (id == m_MusId) return;
            m_MusId = id;
            if (m_MusA != null && m_MusA.Source != null) FadeOutAndStop(m_MusA.Source, fadeSeconds);
            m_MusA = null;
            if (string.IsNullOrEmpty(id)) return;
            var src = Loop(id, transform, volume, Bus.Music, 0f);
            if (src == null || !m_Index.TryGetValue(src, out var t)) return;
            src.loop = loop;
            src.time = 0f;
            t.Fade = 0f; t.FadeTarget = 1f; t.FadeSpeed = 1f / Mathf.Max(0.01f, fadeSeconds); t.ThreatDucked = true;
            m_MusA = t;
        }

        public string CurrentAmbience => m_AmbId;
        /// <summary>The base music cue playing now (null when none, or when a one-shot cue such as mus_reveal has ended).</summary>
        public string CurrentMusic => m_MusA != null && m_MusA.Source != null && m_MusA.Source.isPlaying ? m_MusId : null;

        // ------------------------------------------------------------------ component voices
        /// <summary>
        /// A source owned by a component (breathing, heartbeat, music layers): created once, tracked on its bus, never
        /// recycled. duckDepth: share of the narration duck (default for the bus when negative). reverbMix: send into
        /// the room's reverb (0 = dry).
        /// </summary>
        public AudioSource CreateVoice(string name, Transform parent, Bus bus, float spatial = 0f, float duckDepth = -1f, float reverbMix = 0f)
        {
            var src = NewSource(name, parent != null ? parent : transform, null, spatial);
            var t = Track(src, bus, 0f, false);
            t.Persistent = true;
            if (duckDepth >= 0f) t.DuckDepth = duckDepth;
            Configure(t, src, bus, spatial, 1.2f, DefaultMax, reverbMix);
            return src;
        }

        /// <summary>Play a clip on a component voice at a base volume (bus gains, ducking and fades still apply).</summary>
        public void PlayOn(AudioSource voice, AudioClip clip, float volume, float pitch = 1f, bool peak = false, float startTime = 0f)
        {
            if (voice == null || clip == null || !m_Index.TryGetValue(voice, out var t)) return;
            t.BaseVolume = volume; t.Peak = peak;
            t.Fade = 1f; t.FadeTarget = 1f; t.FadeSpeed = 0f; t.DestroyWhenFaded = false;
            voice.clip = clip;
            voice.pitch = pitch;
            voice.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.02f));
            voice.volume = Volume(t, Game.Settings != null && Game.Settings.reducedDynamicRange);
            voice.Play();
        }

        /// <summary>Fade a component voice to a level over seconds (it keeps playing; 0 does not stop it).</summary>
        public void FadeVoice(AudioSource voice, float target, float seconds)
        {
            if (voice == null || !m_Index.TryGetValue(voice, out var t)) return;
            t.FadeTarget = Mathf.Clamp01(target);
            t.FadeSpeed = Mathf.Abs(t.Fade - t.FadeTarget) / Mathf.Max(0.01f, seconds);
            if (t.FadeSpeed <= 0f) t.Fade = t.FadeTarget;
        }

        // ------------------------------------------------------------------ narration
        /// <summary>Voice playback used by Narration (keeps ducking accurate).</summary>
        public AudioSource PlayVoice(AudioClip clip, Transform speaker)
        {
            var src = NewSource("voice", speaker, null, speaker != null ? 0.85f : 0f);
            src.clip = clip;
            var t = Track(src, Bus.Voice, 1f, false);
            Configure(t, src, Bus.Voice, speaker != null ? 0.85f : 0f, 2.5f, DefaultMax, speaker != null ? 0.8f : 1f);
            src.Play();
            m_VoiceActive++;
            StartCoroutine(VoiceDone(src, clip.length));
            return src;
        }

        System.Collections.IEnumerator VoiceDone(AudioSource src, float length)
        {
            float t = 0f;
            while (src != null && (src.isPlaying || AudioListener.pause) && t < length + 30f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            m_VoiceActive = Mathf.Max(0, m_VoiceActive - 1);
            if (src != null) Destroy(src.gameObject);
        }

        public void StopVoice(AudioSource src)
        {
            if (src != null) src.Stop();
        }

        /// <summary>True while a narration line (or another Voice-bus clip) is playing.</summary>
        public bool VoiceActive => m_VoiceActive > 0;

        public static void Haptic(HandSide hand, float amplitude, float duration)
        {
            if (Game.Mode != GameMode.VR || hand == HandSide.Desktop) return;
            float a = Mathf.Clamp01(amplitude * (Game.Settings?.haptics ?? 1f));
            if (a <= 0f) return;
            var node = hand == HandSide.Left ? UnityEngine.XR.XRNode.LeftHand : UnityEngine.XR.XRNode.RightHand;
            var dev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
            if (dev.isValid) dev.SendHapticImpulse(0, a, duration);
        }
    }
}
