using UnityEngine;

namespace PNJunctionLab
{
    /// <summary>
    /// ScriptableObject defining one guided step in the lab: dialogue, required station action, and 20s/45s hints.
    /// </summary>
    [CreateAssetMenu(fileName = "LessonStep", menuName = "PNJunctionLab/Lesson Step")]
    public class LessonStep : ScriptableObject
    {
        [SerializeField] private string stepId;
        [SerializeField] private string stationId = "T";
        [SerializeField] private string title;
        [TextArea(2, 4)]
        [SerializeField] private string objectiveText;
        [SerializeField] private string requiredActionId;
        [SerializeField] private DialogueLine introDialogue;
        [SerializeField] private DialogueLine completionDialogue;
        [SerializeField] private DialogueLine hint20sDialogue;
        [SerializeField] private DialogueLine hint45sDialogue;
        [SerializeField] private int hintTargetIndex;

        public string StepId => stepId;
        public string StationId => stationId;
        public string Title => title;
        public string ObjectiveText => objectiveText;
        public string RequiredActionId => requiredActionId;
        public DialogueLine IntroDialogue => introDialogue;
        public DialogueLine CompletionDialogue => completionDialogue;
        public DialogueLine Hint20sDialogue => hint20sDialogue;
        public DialogueLine Hint45sDialogue => hint45sDialogue;
        public int HintTargetIndex => hintTargetIndex;

        public void Configure(
            string id,
            string station,
            string stepTitle,
            string objective,
            string actionId,
            DialogueLine intro,
            DialogueLine completion,
            DialogueLine hint20,
            DialogueLine hint45,
            int targetIndex)
        {
            stepId = id;
            stationId = station;
            title = stepTitle;
            objectiveText = objective;
            requiredActionId = actionId;
            introDialogue = intro;
            completionDialogue = completion;
            hint20sDialogue = hint20;
            hint45sDialogue = hint45;
            hintTargetIndex = targetIndex;
        }
    }
}
