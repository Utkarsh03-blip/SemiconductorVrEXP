using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace PNJunctionLab
{
    /// <summary>
    /// Self-contained StationController for Station A: Theory Assessment Board.
    /// Runs a 10-question ScriptableObject MCQ exam across all 5 core semiconductor topics
    /// (2 questions each), provides immediate scientific feedback, computes a per-topic
    /// breakdown scoreboard with weak-area review guidance and missed-question recap,
    /// and supports randomized Retake Quiz cleanly.
    /// </summary>
    public class AssessmentStationController : StationController
    {
        private static readonly string[] OptionPrefixes = { "A", "B", "C", "D" };

        private static readonly string[] TopicDisplayNames =
        {
            "1. Intrinsic Semiconductors",
            "2. Extrinsic Doping (N & P)",
            "3. P-N Junction Equilibrium",
            "4. Forward & Reverse Bias",
            "5. Diode I-V Characteristics"
        };

        private static readonly string[] TopicStations =
        {
            "Station 1",
            "Stations 2 & 3",
            "Station 4",
            "Station 5",
            "Station 6"
        };

        private static readonly Color OptionIdleColor = new Color(0.11f, 0.16f, 0.24f, 0.96f);
        private static readonly Color OptionCorrectColor = new Color(0.14f, 0.56f, 0.28f, 0.98f);
        private static readonly Color OptionWrongColor = new Color(0.62f, 0.18f, 0.18f, 0.98f);
        private static readonly Color OptionDimColor = new Color(0.08f, 0.11f, 0.16f, 0.72f);

        private static readonly Color StrongGreenColor = new Color(0.24f, 0.88f, 0.45f, 1f);
        private static readonly Color FairAmberColor = new Color(0.96f, 0.68f, 0.20f, 1f);
        private static readonly Color WeakRedColor = new Color(0.95f, 0.30f, 0.26f, 1f);

        [Header("Navigation & Curriculum")]
        [SerializeField] private TeleportationAnchor stationAnchor;
        [SerializeField] private AssessmentQuestion[] questions;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private bool startUnlockedForStandalone;

        [Header("Board View Panels")]
        [SerializeField] private GameObject quizViewRoot;
        [SerializeField] private GameObject scoreboardViewRoot;

        [Header("Quiz View Controls & Displays")]
        [SerializeField] private TMP_Text questionProgressText;
        [SerializeField] private TMP_Text topicTagText;
        [SerializeField] private Image progressFillBar;
        [SerializeField] private TMP_Text questionPromptText;
        [SerializeField] private AssessmentBoardButton[] optionButtons;
        [SerializeField] private TMP_Text[] optionTexts;
        [SerializeField] private GameObject feedbackPanel;
        [SerializeField] private Image feedbackAccentBar;
        [SerializeField] private TMP_Text feedbackResultText;
        [SerializeField] private TMP_Text feedbackExplanationText;
        [SerializeField] private AssessmentBoardButton nextQuestionButton;
        [SerializeField] private TMP_Text nextQuestionLabel;

        [Header("Scoreboard & Topic Analysis Controls")]
        [SerializeField] private TMP_Text overallScoreText;
        [SerializeField] private TMP_Text masteryRatingText;
        [SerializeField] private TMP_Text[] topicBreakdownTexts;
        [SerializeField] private Image[] topicAccuracyFills;
        [SerializeField] private TMP_Text missedRecapHeaderText;
        [SerializeField] private TMP_Text missedRecapBodyText;
        [SerializeField] private AssessmentBoardButton retakeQuizButton;

        public int QuestionCount => questions != null ? questions.Length : 0;
        public int CurrentQuestionIndex { get; private set; }
        public bool IsShowingScoreboard { get; private set; }
        public bool HasAnsweredCurrent { get; private set; }
        public int LastScore { get; private set; }
        public float LastPercentage { get; private set; }

        private readonly List<int> activeOrder = new List<int>(16);
        private readonly List<int> selectedAnswers = new List<int>(16);
        private readonly List<bool> correctnessLog = new List<bool>(16);
        private readonly StringBuilder missedBuilder = new StringBuilder(1024);

        protected override void Awake()
        {
            base.Awake();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            InitializeCanonicalOrder();
        }

        private void Start()
        {
            if (startUnlockedForStandalone && !IsUnlocked)
            {
                SetUnlocked(true);
            }
            else
            {
                ApplyLockState();
            }

            RefreshQuizView();
        }

        private void OnEnable()
        {
            if (stationAnchor != null)
            {
                stationAnchor.teleporting.AddListener(HandlePlayerTeleportedHere);
            }

            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    if (optionButtons[i] != null)
                    {
                        optionButtons[i].Clicked += HandleOptionClicked;
                    }
                }
            }

            if (nextQuestionButton != null)
            {
                nextQuestionButton.Clicked += HandleNextClicked;
            }

            if (retakeQuizButton != null)
            {
                retakeQuizButton.Clicked += HandleRetakeClicked;
            }
        }

        private void OnDisable()
        {
            if (stationAnchor != null)
            {
                stationAnchor.teleporting.RemoveListener(HandlePlayerTeleportedHere);
            }

            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    if (optionButtons[i] != null)
                    {
                        optionButtons[i].Clicked -= HandleOptionClicked;
                    }
                }
            }

            if (nextQuestionButton != null)
            {
                nextQuestionButton.Clicked -= HandleNextClicked;
            }

            if (retakeQuizButton != null)
            {
                retakeQuizButton.Clicked -= HandleRetakeClicked;
            }
        }

        public override void SetUnlocked(bool unlocked)
        {
            base.SetUnlocked(unlocked);
            ApplyLockState();
        }

        public override void ResetStation()
        {
            base.ResetStation();
            StartFreshQuiz(shuffleOrder: false);
            ApplyLockState();
        }

        /// <summary>
        /// Submits choice index (0=A .. 3=D) for the active MCQ card and locks selection until Next Question.
        /// </summary>
        public void SelectOption(int choiceIndex)
        {
            if (!IsUnlocked || IsShowingScoreboard || HasAnsweredCurrent || activeOrder.Count == 0)
            {
                return;
            }

            AssessmentQuestion q = GetActiveQuestion();
            if (q == null || choiceIndex < 0 || choiceIndex >= 4)
            {
                return;
            }

            HasAnsweredCurrent = true;
            bool isCorrect = choiceIndex == q.CorrectIndex;

            if (CurrentQuestionIndex < selectedAnswers.Count)
            {
                selectedAnswers[CurrentQuestionIndex] = choiceIndex;
                correctnessLog[CurrentQuestionIndex] = isCorrect;
            }

            if (isCorrect)
            {
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.45f);
            }
            else
            {
                ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.SocketInvalid, 0.55f);
                NotifyWrongAction(q.QuestionId, $"Review {q.RecommendedStation} ({q.TopicTag}): {q.Explanation}");
            }

            UpdateOptionVisualStates(q, choiceIndex);
            ShowImmediateFeedback(q, choiceIndex, isCorrect);
            ApplyLockState();
        }

        /// <summary>
        /// Advances to the next MCQ or transitions to the Scoreboard & Topic Analysis screen after Q10.
        /// </summary>
        public void AdvanceToNextQuestion()
        {
            if (!IsUnlocked || IsShowingScoreboard || !HasAnsweredCurrent)
            {
                return;
            }

            if (CurrentQuestionIndex + 1 < activeOrder.Count)
            {
                CurrentQuestionIndex++;
                HasAnsweredCurrent = false;
                RefreshQuizView();
                ApplyLockState();
            }
            else
            {
                ShowScoreboard();
            }
        }

        /// <summary>
        /// Resets answers, randomizes question sequence, and restarts from Question 1 / 10.
        /// </summary>
        public void RetakeQuiz()
        {
            if (!IsUnlocked)
            {
                return;
            }

            StartFreshQuiz(shuffleOrder: true);
            ApplyLockState();
        }

        /// <summary>
        /// Returns the active AssessmentQuestion for inspection or testing.
        /// </summary>
        public AssessmentQuestion GetActiveQuestion()
        {
            if (questions == null || CurrentQuestionIndex < 0 || CurrentQuestionIndex >= activeOrder.Count)
            {
                return null;
            }

            int idx = activeOrder[CurrentQuestionIndex];
            return (idx >= 0 && idx < questions.Length) ? questions[idx] : null;
        }

        private void HandlePlayerTeleportedHere(TeleportingEventArgs args)
        {
            if (IsUnlocked)
            {
                NotifyStepAction("assessment.teleport");
            }
        }

        private void HandleOptionClicked(AssessmentBoardButton clicked)
        {
            if (optionButtons == null) return;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (optionButtons[i] == clicked)
                {
                    SelectOption(i);
                    return;
                }
            }
        }

        private void HandleNextClicked(AssessmentBoardButton _) => AdvanceToNextQuestion();
        private void HandleRetakeClicked(AssessmentBoardButton _) => RetakeQuiz();

        private void InitializeCanonicalOrder()
        {
            StartFreshQuiz(shuffleOrder: false);
        }

        private void StartFreshQuiz(bool shuffleOrder)
        {
            activeOrder.Clear();
            selectedAnswers.Clear();
            correctnessLog.Clear();

            int count = QuestionCount;
            for (int i = 0; i < count; i++)
            {
                activeOrder.Add(i);
                selectedAnswers.Add(-1);
                correctnessLog.Add(false);
            }

            if (shuffleOrder && count > 1)
            {
                for (int i = count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    int tmp = activeOrder[i];
                    activeOrder[i] = activeOrder[j];
                    activeOrder[j] = tmp;
                }
            }

            CurrentQuestionIndex = 0;
            IsShowingScoreboard = false;
            HasAnsweredCurrent = false;
            LastScore = 0;
            LastPercentage = 0f;
            RefreshQuizView();
        }

        private void RefreshQuizView()
        {
            if (quizViewRoot != null) quizViewRoot.SetActive(!IsShowingScoreboard);
            if (scoreboardViewRoot != null) scoreboardViewRoot.SetActive(IsShowingScoreboard);

            if (IsShowingScoreboard)
            {
                return;
            }

            int total = activeOrder.Count;
            AssessmentQuestion q = GetActiveQuestion();

            if (questionProgressText != null)
            {
                questionProgressText.text = total > 0
                    ? $"Question {CurrentQuestionIndex + 1} / {total}"
                    : "No Questions Configured";
            }

            if (progressFillBar != null)
            {
                float frac = total > 0 ? (CurrentQuestionIndex + 1f) / total : 0f;
                RectTransform rt = progressFillBar.rectTransform;
                rt.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            }

            if (q == null)
            {
                return;
            }

            if (topicTagText != null)
            {
                topicTagText.text = $"{q.TopicTag}  ({q.RecommendedStation})";
            }

            if (questionPromptText != null)
            {
                questionPromptText.text = q.PromptText;
            }

            if (optionTexts != null && q.Options != null)
            {
                for (int i = 0; i < optionTexts.Length; i++)
                {
                    if (optionTexts[i] == null) continue;
                    string optBody = i < q.Options.Length ? q.Options[i] : string.Empty;
                    optionTexts[i].text = $"{OptionPrefixes[i]}.  {optBody}";
                }
            }

            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    if (optionButtons[i] != null)
                    {
                        optionButtons[i].SetCardColor(OptionIdleColor);
                    }
                }
            }

            if (feedbackPanel != null)
            {
                feedbackPanel.SetActive(false);
            }

            if (nextQuestionButton != null)
            {
                nextQuestionButton.gameObject.SetActive(false);
            }
        }

        private void UpdateOptionVisualStates(AssessmentQuestion q, int chosenIndex)
        {
            if (optionButtons == null) return;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (optionButtons[i] == null) continue;
                if (i == q.CorrectIndex)
                {
                    optionButtons[i].SetCardColor(OptionCorrectColor);
                }
                else if (i == chosenIndex)
                {
                    optionButtons[i].SetCardColor(OptionWrongColor);
                }
                else
                {
                    optionButtons[i].SetCardColor(OptionDimColor);
                }
            }
        }

        private void ShowImmediateFeedback(AssessmentQuestion q, int chosenIndex, bool isCorrect)
        {
            if (feedbackPanel != null)
            {
                feedbackPanel.SetActive(true);
            }

            if (feedbackAccentBar != null)
            {
                feedbackAccentBar.color = isCorrect ? StrongGreenColor : WeakRedColor;
            }

            if (feedbackResultText != null)
            {
                string correctLetter = OptionPrefixes[Mathf.Clamp(q.CorrectIndex, 0, 3)];
                string chosenLetter = OptionPrefixes[Mathf.Clamp(chosenIndex, 0, 3)];
                feedbackResultText.text = isCorrect
                    ? $"CORRECT ({correctLetter})!"
                    : $"INCORRECT (Selected {chosenLetter} — Correct Answer: {correctLetter})";
                feedbackResultText.color = isCorrect ? StrongGreenColor : new Color(1.0f, 0.56f, 0.50f, 1f);
            }

            if (feedbackExplanationText != null)
            {
                feedbackExplanationText.text = q.Explanation;
            }

            if (nextQuestionButton != null)
            {
                nextQuestionButton.gameObject.SetActive(true);
            }

            if (nextQuestionLabel != null)
            {
                bool isLast = (CurrentQuestionIndex + 1) >= activeOrder.Count;
                nextQuestionLabel.text = isLast ? "VIEW SCOREBOARD" : "NEXT QUESTION";
            }
        }

        private void ShowScoreboard()
        {
            IsShowingScoreboard = true;
            if (quizViewRoot != null) quizViewRoot.SetActive(false);
            if (scoreboardViewRoot != null) scoreboardViewRoot.SetActive(true);

            int total = activeOrder.Count;
            int correctTotal = 0;

            int[] topicTotals = new int[5];
            int[] topicCorrects = new int[5];
            int missedCount = 0;
            missedBuilder.Clear();

            for (int i = 0; i < total; i++)
            {
                int qIdx = activeOrder[i];
                AssessmentQuestion q = (questions != null && qIdx >= 0 && qIdx < questions.Length) ? questions[qIdx] : null;
                if (q == null) continue;

                int t = Mathf.Clamp((int)q.Topic, 0, 4);
                topicTotals[t]++;

                bool ok = i < correctnessLog.Count && correctnessLog[i];
                if (ok)
                {
                    correctTotal++;
                    topicCorrects[t]++;
                }
                else
                {
                    missedCount++;
                    string correctLetter = OptionPrefixes[Mathf.Clamp(q.CorrectIndex, 0, 3)];
                    missedBuilder.AppendFormat("- [{0}] {1} (Ans {2}): {3}\n",
                        q.RecommendedStation, q.ShortSummaryLabel, correctLetter, q.Explanation);
                }
            }

            LastScore = correctTotal;
            LastPercentage = total > 0 ? (correctTotal * 100f) / total : 0f;

            if (overallScoreText != null)
            {
                overallScoreText.text = $"SCORE:  {correctTotal} / {total}   ({LastPercentage:F0}%)";
            }

            if (masteryRatingText != null)
            {
                if (LastPercentage >= 90f)
                {
                    masteryRatingText.text = "SIMULATION COMPLETE!  MASTERY: DISTINCTION — P-N Junction Expert!";
                    masteryRatingText.color = StrongGreenColor;
                }
                else if (LastPercentage >= 70f)
                {
                    masteryRatingText.text = "SIMULATION COMPLETE!  MASTERY: PROFICIENT — Strong Semiconductor Grasp!";
                    masteryRatingText.color = new Color(0.35f, 0.90f, 1.0f, 1f);
                }
                else if (LastPercentage >= 50f)
                {
                    masteryRatingText.text = "SIMULATION COMPLETE!  MASTERY: DEVELOPING — Revisit Flagged Stations Below";
                    masteryRatingText.color = FairAmberColor;
                }
                else
                {
                    masteryRatingText.text = "SIMULATION COMPLETE!  MASTERY: NEEDS REVIEW — Revisit Highlighted Lab Benches";
                    masteryRatingText.color = WeakRedColor;
                }
            }

            for (int t = 0; t < 5; t++)
            {
                int tot = topicTotals[t];
                int cor = topicCorrects[t];
                float frac = tot > 0 ? (cor / (float)tot) : 0f;

                if (topicAccuracyFills != null && t < topicAccuracyFills.Length && topicAccuracyFills[t] != null)
                {
                    RectTransform rt = topicAccuracyFills[t].rectTransform;
                    rt.anchorMax = new Vector2(Mathf.Max(0.04f, frac), 1f);
                    topicAccuracyFills[t].color = frac >= 0.99f
                        ? StrongGreenColor
                        : frac >= 0.5f
                            ? FairAmberColor
                            : WeakRedColor;
                }

                if (topicBreakdownTexts != null && t < topicBreakdownTexts.Length && topicBreakdownTexts[t] != null)
                {
                    string statusTag = frac >= 0.99f
                        ? "STRONG"
                        : frac >= 0.5f
                            ? $"FAIR — Review {TopicStations[t]}"
                            : $"WEAK — Revisit {TopicStations[t]}";

                    topicBreakdownTexts[t].text = $"{TopicDisplayNames[t]}:  {cor}/{tot}  ({frac * 100f:F0}%)  [{statusTag}]";
                    topicBreakdownTexts[t].color = frac >= 0.99f
                        ? new Color(0.75f, 1.0f, 0.82f, 1f)
                        : frac >= 0.5f
                            ? new Color(1.0f, 0.88f, 0.55f, 1f)
                            : new Color(1.0f, 0.65f, 0.62f, 1f);
                }
            }

            if (missedRecapHeaderText != null)
            {
                missedRecapHeaderText.text = missedCount == 0
                    ? "MISSED QUESTIONS RECAP (0 missed)"
                    : $"MISSED QUESTIONS RECAP ({missedCount} to review)";
            }

            if (missedRecapBodyText != null)
            {
                missedRecapBodyText.text = missedCount == 0
                    ? "Outstanding work! You answered every topic accurately across Intrinsic, Doping, Junction Equilibrium, Biasing, and I-V Characteristics."
                    : missedBuilder.ToString().TrimEnd('\n');
            }

            ApplyLockState();
            ProceduralAudio.PlayOneShot(audioSource, ProceduralAudio.SoundKind.Success, 0.65f);

            // Auto-complete assessment.teleport if the player walked up instead of using Pad A.
            NotifyStepAction("assessment.teleport");
            NotifyStepAction("assessment.complete");
            MarkCompleted();
        }

        private void ApplyLockState()
        {
            bool unlocked = IsUnlocked;

            if (optionButtons != null)
            {
                bool canSelectOption = unlocked && !IsShowingScoreboard && !HasAnsweredCurrent;
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    if (optionButtons[i] != null)
                    {
                        optionButtons[i].SetInteractable(canSelectOption);
                    }
                }
            }

            if (nextQuestionButton != null)
            {
                nextQuestionButton.SetInteractable(unlocked && !IsShowingScoreboard && HasAnsweredCurrent);
            }

            if (retakeQuizButton != null)
            {
                retakeQuizButton.SetInteractable(unlocked && IsShowingScoreboard);
            }
        }
    }
}
