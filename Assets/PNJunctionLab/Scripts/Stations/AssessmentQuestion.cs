using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// Core lab syllabus topics evaluated on the Station A Assessment Board.
    /// </summary>
    public enum AssessmentTopic
    {
        IntrinsicSemiconductors = 0,
        ExtrinsicDoping = 1,
        JunctionEquilibrium = 2,
        BiasingBehavior = 3,
        DiodeIVCharacteristics = 4
    }

    /// <summary>
    /// ScriptableObject defining one academically accurate MCQ item grounded in Docs/SCIENCE.md.
    /// </summary>
    [CreateAssetMenu(fileName = "AssessmentQuestion", menuName = "PNJunctionLab/Assessment Question")]
    public class AssessmentQuestion : ScriptableObject
    {
        [SerializeField] private string questionId;
        [SerializeField] private AssessmentTopic topic;
        [SerializeField] private string topicTag;
        [SerializeField] private string shortSummaryLabel;
        [TextArea(2, 4)]
        [SerializeField] private string promptText;
        [SerializeField] private string[] options = new string[4];
        [SerializeField] private int correctIndex;
        [TextArea(2, 4)]
        [SerializeField] private string explanation;
        [SerializeField] private string recommendedStation;

        public string QuestionId => questionId;
        public AssessmentTopic Topic => topic;
        public string TopicTag => topicTag;
        public string ShortSummaryLabel => shortSummaryLabel;
        public string PromptText => promptText;
        public string[] Options => options;
        public int CorrectIndex => correctIndex;
        public string Explanation => explanation;
        public string RecommendedStation => recommendedStation;

        public void Configure(
            string id,
            AssessmentTopic questionTopic,
            string tag,
            string summaryLabel,
            string prompt,
            string optionA,
            string optionB,
            string optionC,
            string optionD,
            int correctChoiceIndex,
            string scientificExplanation,
            string stationRecommendation)
        {
            questionId = id;
            topic = questionTopic;
            topicTag = tag;
            shortSummaryLabel = summaryLabel;
            promptText = prompt;
            options = new[] { optionA, optionB, optionC, optionD };
            correctIndex = Mathf.Clamp(correctChoiceIndex, 0, 3);
            explanation = scientificExplanation;
            recommendedStation = stationRecommendation;
        }
    }
}
