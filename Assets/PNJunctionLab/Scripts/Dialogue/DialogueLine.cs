using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// ScriptableObject defining a single NPC dialogue beat, emotion, audio hook, and branching choices.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueLine", menuName = "PNJunctionLab/Dialogue Line")]
    public class DialogueLine : ScriptableObject
    {
        [SerializeField] private string lineId;
        [SerializeField] private string speakerOverride;
        [TextArea(2, 5)]
        [SerializeField] private string text;
        [SerializeField] private NPCEmotion emotion = NPCEmotion.Explaining;
        [SerializeField] private AudioClip voiceClip;
        [SerializeField] private DialogueLine nextLine;
        [SerializeField] private DialogueChoice[] choices;

        public string LineId => lineId;
        public string SpeakerOverride => speakerOverride;
        public string Text => text;
        public NPCEmotion Emotion => emotion;
        public AudioClip VoiceClip => voiceClip;
        public DialogueLine NextLine => nextLine;
        public DialogueChoice[] Choices => choices;

        public void Configure(
            string id,
            string bodyText,
            NPCEmotion npcEmotion = NPCEmotion.Explaining,
            DialogueLine next = null,
            DialogueChoice[] branchChoices = null,
            string speaker = "")
        {
            lineId = id;
            text = bodyText;
            emotion = npcEmotion;
            nextLine = next;
            choices = branchChoices;
            speakerOverride = speaker;
        }

        public void SetVoiceClip(AudioClip clip)
        {
            voiceClip = clip;
        }
    }
}
