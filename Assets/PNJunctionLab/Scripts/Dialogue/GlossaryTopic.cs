using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// ScriptableObject representing a semiconductor glossary entry and optional NPC "Ask me about..." dialogue branch.
    /// </summary>
    [CreateAssetMenu(fileName = "GlossaryTopic", menuName = "PNJunctionLab/Glossary Topic")]
    public class GlossaryTopic : ScriptableObject
    {
        [SerializeField] private string topicId;
        [SerializeField] private string title;
        [TextArea(2, 5)]
        [SerializeField] private string summary;
        [SerializeField] private DialogueLine explanationLine;

        public string TopicId => topicId;
        public string Title => title;
        public string Summary => summary;
        public DialogueLine ExplanationLine => explanationLine;

        public void Configure(string id, string topicTitle, string topicSummary, DialogueLine dialogue)
        {
            topicId = id;
            title = topicTitle;
            summary = topicSummary;
            explanationLine = dialogue;
        }
    }
}
