using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Voice lines with subtitles. One line at a time; tutorial lines never overlap and can be cleared when they go
    /// stale (action done, checkpoint change, skip). Missing audio still shows the subtitle for a reading-time
    /// estimate, so the story stays understandable.
    /// </summary>
    public class Narration : MonoBehaviour
    {
        public static Narration I { get; private set; }

        [Serializable] class ScriptLine { public string id; public string speaker; public string text; }
        [Serializable] class ScriptFile { public List<ScriptLine> lines = new List<ScriptLine>(); }

        class Line
        {
            public string Id;
            public Transform Speaker;
            public bool Tutorial;
            public Action Done;
        }

        readonly Queue<Line> m_Queue = new Queue<Line>();
        readonly Dictionary<string, ScriptLine> m_Script = new Dictionary<string, ScriptLine>();
        Line m_Current;
        AudioSource m_Source;
        float m_Remaining;

        static readonly Dictionary<string, string> s_SpeakerNames = new Dictionary<string, string>
        {
            { "saeed", "Saeed" },
            { "tutorial", "" },
            { "mother", "Mum (voicemail)" },
            { "shopkeeper", "Shopkeeper" },
            { "optician", "Optician's note" },
        };

        public bool IsSpeaking => m_Current != null;
        public string CurrentId => m_Current?.Id;

        public static void Create(Transform parent)
        {
            if (I != null) return;
            var go = new GameObject("Narration");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<Narration>();
            I.LoadScript();
        }

        void LoadScript()
        {
            var ta = Resources.Load<TextAsset>("Narration/narration");
            if (ta == null) return;
            try
            {
                var f = JsonUtility.FromJson<ScriptFile>(ta.text);
                foreach (var l in f.lines) m_Script[l.id] = l;
            }
            catch (Exception e) { Debug.LogWarning("[TS-Narration] " + e.Message); }
        }

        public string TextOf(string id)
        {
            var info = AudioDirector.I != null ? AudioDirector.I.Info(id) : null;
            if (info != null && !string.IsNullOrEmpty(info.transcript)) return info.transcript;
            return m_Script.TryGetValue(id, out var l) ? l.text : id;
        }

        string SpeakerOf(string id)
        {
            string key = m_Script.TryGetValue(id, out var l) ? l.speaker : AudioDirector.I?.Info(id)?.speaker;
            if (key == null) return "";
            return s_SpeakerNames.TryGetValue(key, out var n) ? n : key;
        }

        /// <summary>Queue a line. oncePerSave lines are remembered in the campaign flags ("vo:&lt;id&gt;").</summary>
        public void Say(string id, Transform speaker = null, bool tutorial = false, Action onDone = null, bool interrupt = false, bool oncePerSave = false)
        {
            if (oncePerSave && Game.Campaign != null)
            {
                if (Game.Campaign.HasFlag("vo:" + id)) { onDone?.Invoke(); return; }
                Game.Campaign.SetFlag("vo:" + id);
            }
            var line = new Line { Id = id, Speaker = speaker, Tutorial = tutorial, Done = onDone };
            if (tutorial)
            {
                // A new instruction replaces any stale instruction (never two tutorial speeches at once).
                ClearTutorial(keepCurrentIfSame: id);
            }
            if (interrupt) StopCurrent(invokeDone: true);
            m_Queue.Enqueue(line);
        }

        public void ClearTutorial(string keepCurrentIfSame = null)
        {
            var keep = new Queue<Line>();
            while (m_Queue.Count > 0)
            {
                var l = m_Queue.Dequeue();
                if (!l.Tutorial) keep.Enqueue(l); else l.Done?.Invoke();
            }
            while (keep.Count > 0) m_Queue.Enqueue(keep.Dequeue());
            if (m_Current != null && m_Current.Tutorial && m_Current.Id != keepCurrentIfSame) StopCurrent(invokeDone: true);
        }

        public void StopAll()
        {
            m_Queue.Clear();
            StopCurrent(invokeDone: false);
        }

        void StopCurrent(bool invokeDone)
        {
            if (m_Current == null) return;
            var done = m_Current.Done;
            if (m_Source != null) m_Source.Stop();
            m_Source = null;
            m_Current = null;
            Hud.I?.ClearSubtitle();
            if (invokeDone) done?.Invoke();
        }

        void Update()
        {
            if (m_Current == null)
            {
                if (m_Queue.Count == 0) return;
                Begin(m_Queue.Dequeue());
                return;
            }
            // Voice audio pauses with the game (AudioListener.pause); the timer follows game time.
            m_Remaining -= Time.deltaTime;
            bool audioDone = m_Source == null || (!m_Source.isPlaying && !AudioListener.pause);
            if (m_Remaining <= 0f && audioDone)
            {
                var done = m_Current.Done;
                m_Current = null;
                m_Source = null;
                done?.Invoke();
            }
        }

        void Begin(Line line)
        {
            m_Current = line;
            var clip = AudioDirector.I != null ? AudioDirector.I.Clip(line.Id) : null;
            string text = TextOf(line.Id);
            float duration = clip != null ? clip.length : Mathf.Clamp(text.Length * 0.065f, 1.2f, 8f);
            m_Remaining = duration + 0.25f;
            m_Source = clip != null ? AudioDirector.I.PlayVoice(clip, line.Speaker) : null;
            Hud.I?.ShowSubtitle(SpeakerOf(line.Id), text, duration + 0.6f, line.Tutorial);
        }
    }
}
