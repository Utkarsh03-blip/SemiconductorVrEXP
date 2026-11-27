using System;
using System.Collections.Generic;
using UnityEngine;

namespace PNJunctionLab
{
    public readonly struct DialogueState
    {
        public readonly bool IsActive;
        public readonly string Speaker;
        public readonly string Text;
        public readonly NPCEmotion Emotion;
        public readonly AudioClip VoiceClip;
        public readonly IReadOnlyList<DialogueChoice> Choices;
        public readonly bool HasNextLine;
        public readonly bool AllowAskTopics;

        public DialogueState(
            bool isActive,
            string speaker,
            string text,
            NPCEmotion emotion,
            AudioClip voiceClip,
            IReadOnlyList<DialogueChoice> choices,
            bool hasNextLine,
            bool allowAskTopics)
        {
            IsActive = isActive;
            Speaker = speaker;
            Text = text;
            Emotion = emotion;
            VoiceClip = voiceClip;
            Choices = choices;
            HasNextLine = hasNextLine;
            AllowAskTopics = allowAskTopics;
        }
    }

    /// <summary>
    /// Drives ScriptableObject DialogueLine sequences, branching choices, and "Ask me about..." glossary queries.
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        [SerializeField] private string defaultSpeakerName = "Dr. Vera";
        [SerializeField] private GlossaryTopic[] askMeTopics;
        [SerializeField] private GlossaryTopic[] stationAskTopics;

        public event Action<DialogueState> StateChanged;
        public event Action AskTopicsChanged;
        public event Action SequenceCompleted;
        public event Action<GlossaryTopic> TopicAsked;

        public DialogueLine CurrentLine { get; private set; }
        public bool IsDialogueActive { get; private set; }
        public IReadOnlyList<GlossaryTopic> AskMeTopics => askMeTopics;

        /// <summary>
        /// Returns the active station's contextual Ask topics when configured, falling back to general glossary topics.
        /// </summary>
        public IReadOnlyList<GlossaryTopic> GetEffectiveAskTopics()
        {
            return (stationAskTopics != null && stationAskTopics.Length > 0)
                ? (IReadOnlyList<GlossaryTopic>)stationAskTopics
                : askMeTopics;
        }

        /// <summary>
        /// Replaces the active station quick-question options shown on Dr. Vera's speech bubble without interrupting speech.
        /// </summary>
        public void SetStationAskTopics(IReadOnlyList<GlossaryTopic> topics)
        {
            if (topics == null || topics.Count == 0)
            {
                return;
            }

            if (SameTopics(stationAskTopics, topics))
            {
                return;
            }

            GlossaryTopic[] next = new GlossaryTopic[topics.Count];
            for (int i = 0; i < topics.Count; i++)
            {
                next[i] = topics[i];
            }

            stationAskTopics = next;
            AskTopicsChanged?.Invoke();
        }

        private static bool SameTopics(GlossaryTopic[] current, IReadOnlyList<GlossaryTopic> candidate)
        {
            if (current == null || current.Length != candidate.Count)
            {
                return false;
            }

            for (int i = 0; i < candidate.Count; i++)
            {
                if (current[i] != candidate[i])
                {
                    return false;
                }
            }

            return true;
        }

        public void SetSpeakerName(string speakerName)
        {
            if (!string.IsNullOrEmpty(speakerName))
            {
                defaultSpeakerName = speakerName;
            }
        }

        /// <summary>
        /// Begins or replaces the active dialogue sequence from the provided DialogueLine asset.
        /// </summary>
        public void ShowLine(DialogueLine line)
        {
            if (line == null)
            {
                ClearDialogue();
                return;
            }

            CurrentLine = line;
            IsDialogueActive = true;
            EmitCurrentLineState();
        }

        /// <summary>
        /// Shows a short one-off reaction line (for dynamic physics/wrong-action feedback).
        /// </summary>
        public void ShowAdHoc(string text, NPCEmotion emotion = NPCEmotion.Explaining, AudioClip clip = null)
        {
            CurrentLine = null;
            IsDialogueActive = true;
            DialogueState state = new DialogueState(
                true,
                defaultSpeakerName,
                text,
                emotion,
                clip,
                Array.Empty<DialogueChoice>(),
                false,
                true);
            StateChanged?.Invoke(state);
        }

        /// <summary>
        /// Advances to NextLine if present, or finishes the active sequence.
        /// </summary>
        public void Advance()
        {
            if (!IsDialogueActive)
            {
                return;
            }

            if (CurrentLine != null && CurrentLine.NextLine != null)
            {
                ShowLine(CurrentLine.NextLine);
                return;
            }

            ClearDialogue();
            SequenceCompleted?.Invoke();
        }

        /// <summary>
        /// Follows a branching choice on the active DialogueLine.
        /// </summary>
        public void SelectChoice(int index)
        {
            if (CurrentLine == null || CurrentLine.Choices == null || index < 0 || index >= CurrentLine.Choices.Length)
            {
                return;
            }

            DialogueChoice choice = CurrentLine.Choices[index];
            if (choice != null && choice.NextLine != null)
            {
                ShowLine(choice.NextLine);
            }
            else
            {
                Advance();
            }
        }

        /// <summary>
        /// Branches the NPC into explaining a glossary topic via its DialogueLine or Summary.
        /// </summary>
        public void AskAboutTopic(GlossaryTopic topic)
        {
            if (topic == null)
            {
                return;
            }

            TopicAsked?.Invoke(topic);
            if (topic.ExplanationLine != null)
            {
                ShowLine(topic.ExplanationLine);
            }
            else
            {
                ShowAdHoc(topic.Title + ": " + topic.Summary, NPCEmotion.Explaining);
            }
        }

        /// <summary>
        /// Skips the remaining dialogue sequence (used by DebugPanel).
        /// </summary>
        public void SkipSequence()
        {
            if (!IsDialogueActive)
            {
                return;
            }

            ClearDialogue();
            SequenceCompleted?.Invoke();
        }

        private void ClearDialogue()
        {
            CurrentLine = null;
            IsDialogueActive = false;
            StateChanged?.Invoke(new DialogueState(
                false,
                defaultSpeakerName,
                string.Empty,
                NPCEmotion.Neutral,
                null,
                Array.Empty<DialogueChoice>(),
                false,
                true));
        }

        private void EmitCurrentLineState()
        {
            string speaker = string.IsNullOrEmpty(CurrentLine.SpeakerOverride)
                ? defaultSpeakerName
                : CurrentLine.SpeakerOverride;
            IReadOnlyList<DialogueChoice> choices = CurrentLine.Choices != null
                ? (IReadOnlyList<DialogueChoice>)CurrentLine.Choices
                : Array.Empty<DialogueChoice>();

            StateChanged?.Invoke(new DialogueState(
                true,
                speaker,
                CurrentLine.Text,
                CurrentLine.Emotion,
                CurrentLine.VoiceClip,
                choices,
                CurrentLine.NextLine != null,
                true));
        }
    }
}
