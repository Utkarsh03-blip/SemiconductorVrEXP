using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Phase 7 Editor builder: upgrades ONLY Station_A_Assessment into a self-contained pure-theory
    /// 10-Question MCQ Assessment Board & Scoreboard Analysis station (without modifying Stations 1–6),
    /// authors all 10 AssessmentQuestion assets, LessonSteps, NPC Wrap-Up/Guide dialogues,
    /// wires NPCController & LessonManager cleanly, and validates LabNavMeshData.
    /// </summary>
    public static class Phase7AssessmentBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string StationsPrefabsFolder = "Assets/PNJunctionLab/Prefabs/Stations";
        private const string AssessmentPrefabPath = StationsPrefabsFolder + "/AssessmentStation.prefab";
        private const string MaterialsFolder = "Assets/PNJunctionLab/Materials";
        private const string DataFolder = "Assets/PNJunctionLab/Data";
        private const string AssessmentFolder = DataFolder + "/Assessment";
        private const string QuestionsFolder = AssessmentFolder + "/Questions";
        private const string LessonFolder = DataFolder + "/Lessons";
        private const string DialogueFolder = DataFolder + "/Dialogue";

        [MenuItem("PNJunctionLab/Build Phase 7 (Assessment Board)")]
        public static void BuildPhase7Menu()
        {
            Debug.Log(BuildPhase7());
        }

        /// <summary>
        /// Programmatic Phase 7 entry point callable from Unity MCP execute_code.
        /// Explicitly leaves Stations 1 through 6 untouched.
        /// </summary>
        public static string BuildPhase7()
        {
            LabTextStyleHelper.EnsureNativeGameViewDPI();

            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.EndsWith("PNJunctionLab.unity"))
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            EnsureFolders();
            LabTextStyleHelper.GetOrCreateCrispFontAsset();

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material matFrame = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_TableFrame.mat")
                ?? GetOrCreateMat("Mat_TableFrame", urpLit, new Color(0.14f, 0.17f, 0.22f));
            Material matScreen = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_ScreenPanel.mat")
                ?? GetOrCreateMat("Mat_ScreenPanel", urpLit, new Color(0.08f, 0.12f, 0.18f));
            Material matAccent = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_AccentBase.mat")
                ?? GetOrCreateMat("Mat_AccentBase", urpLit, new Color(0.15f, 0.72f, 0.95f));

            Camera mainCam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            GameObject stationsParentGo = GameObject.Find("Stations");
            Transform stationsParent = stationsParentGo != null ? stationsParentGo.transform : null;

            // Preserve Station_A_Assessment exact transform while replacing only Station_A_Assessment.
            GameObject existing = GameObject.Find("Station_A_Assessment");
            Vector3 posA = existing != null ? existing.transform.position : new Vector3(5.60f, 0f, -3.40f);
            Quaternion rotA = existing != null ? existing.transform.rotation : Quaternion.Euler(0f, 270f, 0f);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            AssessmentQuestion[] questions = CreateAllTenQuestions();
            AssessmentStationController ctrl = BuildAssessmentStation(stationsParent, posA, rotA, questions, matFrame, matScreen, matAccent, mainCam);

            WirePhase7LessonsAndNpc(ctrl);
            string navReport = RebakeAndVerifyNavMeshStandPoints();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"Phase 7 Assessment Board built cleanly (Stations 1–6 untouched). {navReport}";
        }

        private static AssessmentStationController BuildAssessmentStation(
            Transform stationsParent,
            Vector3 worldPos,
            Quaternion worldRot,
            AssessmentQuestion[] questions,
            Material matFrame,
            Material matScreen,
            Material matAccent,
            Camera mainCam)
        {
            GameObject root = new GameObject("Station_A_Assessment");
            AudioSource stationAudio = root.AddComponent<AudioSource>();
            stationAudio.spatialBlend = 1f;
            stationAudio.playOnAwake = false;

            // Standalone large Whiteboard Stand (no workbench/battery/wires/sockets — pure Theory Assessment Board).
            // Posts sit outside the screen frame with compact floor feet so angled views never occlude the board.
            CreateBox("BoardStandL", root.transform, new Vector3(1.07f, 1.04f, -0.01f), new Vector3(0.055f, 2.08f, 0.08f), matFrame, true);
            CreateBox("BoardStandR", root.transform, new Vector3(-1.07f, 1.04f, -0.01f), new Vector3(0.055f, 2.08f, 0.08f), matFrame, true);
            CreateBox("BoardFootL", root.transform, new Vector3(1.07f, 0.025f, 0f), new Vector3(0.09f, 0.05f, 0.42f), matFrame, true);
            CreateBox("BoardFootR", root.transform, new Vector3(-1.07f, 0.025f, 0f), new Vector3(0.09f, 0.05f, 0.42f), matFrame, true);
            CreateBox("BoardFrame", root.transform, new Vector3(0f, 1.44f, 0f), new Vector3(2.12f, 1.26f, 0.055f), matFrame, true);
            GameObject backing = CreateBox("WhiteboardScreenBacking", root.transform, new Vector3(0f, 1.44f, 0.018f), new Vector3(2.04f, 1.18f, 0.025f), matScreen, false);
            Object.DestroyImmediate(backing.GetComponent<Collider>());

            StationBeacon beacon = BuildBeaconParts(root.transform, "A", "Assessment Board", matFrame, matScreen, matAccent, mainCam, out TeleportationAnchor anchor);

            // NPCStandPoint beside south edge of board (local (-1.22, 0, 1.05) -> world ~(4.55, 0, -4.62), clear floor).
            GameObject npcStand = new GameObject("NpcStandPoint");
            npcStand.transform.SetParent(root.transform, false);
            npcStand.transform.localPosition = new Vector3(-1.22f, 0f, 1.05f);
            npcStand.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Crisp World-Space Interactive Canvas (DPU=15, Reference=100, 2040 x 1180 px -> 2.04 x 1.18 m).
            Canvas boardCanvas = LabTextStyleHelper.CreateWorldCanvas(
                "AssessmentBoardCanvas",
                root.transform,
                new Vector3(0f, 1.44f, 0.036f),
                new Vector3(0f, 180f, 0f),
                new Vector2(2.04f, 1.18f),
                2040,
                1180,
                mainCam,
                true);

            // -----------------------------------------------------------------
            // PANEL 1: 10-QUESTION MCQ INTERFACE
            // -----------------------------------------------------------------
            GameObject quizRoot = new GameObject("QuizView");
            quizRoot.transform.SetParent(boardCanvas.transform, false);
            RectTransform quizRt = quizRoot.AddComponent<RectTransform>();
            quizRt.anchorMin = Vector2.zero; quizRt.anchorMax = Vector2.one;
            quizRt.offsetMin = Vector2.zero; quizRt.offsetMax = Vector2.zero;

            LabTextStyleHelper.CreateTMPText(quizRoot.transform, "HeaderTitle",
                "FINAL THEORY ASSESSMENT  —  SEMICONDUCTOR & P-N JUNCTION LAB", 30f,
                new Vector2(0f, 540f), new Vector2(1940f, 44f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            TMP_Text progressTxt = LabTextStyleHelper.CreateTMPText(quizRoot.transform, "QuestionProgress",
                "Question 1 / 10", 28f,
                new Vector2(-620f, 482f), new Vector2(680f, 40f), Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

            TMP_Text topicTagTxt = LabTextStyleHelper.CreateTMPText(quizRoot.transform, "TopicTag",
                "1. Intrinsic Semiconductors (Station 1)", 26f,
                new Vector2(280f, 482f), new Vector2(1240f, 40f), new Color(1.0f, 0.84f, 0.36f), TextAlignmentOptions.Right, FontStyles.Bold);

            // Progress Bar Track & Fill.
            RectTransform trackRt = CreateUIImageRect(quizRoot.transform, "ProgressTrack",
                new Vector2(0f, 440f), new Vector2(1920f, 20f), new Color(0.16f, 0.22f, 0.30f, 1f));
            RectTransform fillRt = CreateStretchChild(trackRt, "ProgressFill", new Color(0.22f, 0.84f, 1.0f, 1f));
            Image progressFillImg = fillRt.GetComponent<Image>();

            // Question Prompt Card.
            CreateUIImageRect(quizRoot.transform, "PromptCard",
                new Vector2(0f, 336f), new Vector2(1920f, 152f), new Color(0.06f, 0.10f, 0.15f, 0.96f));
            TMP_Text promptTxt = LabTextStyleHelper.CreateTMPText(quizRoot.transform, "PromptText",
                "Question text appears here.", 31f,
                new Vector2(0f, 336f), new Vector2(1860f, 134f), Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            promptTxt.enableAutoSizing = true;
            promptTxt.fontSizeMin = 22f;
            promptTxt.fontSizeMax = 32f;

            // 4 Option Buttons (2x2 grid or 4 crisp full-width stacked cards — 4 stacked cards guarantees effortless reading of equations & text!).
            AssessmentBoardButton[] optBtns = new AssessmentBoardButton[4];
            TMP_Text[] optTxts = new TMP_Text[4];
            float[] optYs = { 188f, 76f, -36f, -148f };

            for (int i = 0; i < 4; i++)
            {
                optBtns[i] = BuildBoardCardButton(
                    quizRoot.transform,
                    "OptionCard_" + i,
                    new Vector2(0f, optYs[i]),
                    new Vector2(1920f, 96f),
                    new Color(0.11f, 0.16f, 0.24f, 0.96f),
                    "Select Choice",
                    27f,
                    TextAlignmentOptions.MidlineLeft,
                    stationAudio,
                    matScreen,
                    mainCam,
                    out optTxts[i]);
            }

            // Explanation Feedback Panel + Next Question Button at bottom.
            RectTransform fbRt = CreateUIImageRect(quizRoot.transform, "FeedbackPanel",
                new Vector2(-210f, -342f), new Vector2(1500f, 244f), new Color(0.06f, 0.10f, 0.16f, 0.98f));
            RectTransform fbAccentRt = CreateUIImageRect(fbRt, "Accent", new Vector2(-742f, 0f), new Vector2(14f, 244f), new Color(0.24f, 0.88f, 0.45f, 1f));
            Image fbAccentImg = fbAccentRt.GetComponent<Image>();

            TMP_Text fbResultTxt = LabTextStyleHelper.CreateTMPText(fbRt, "ResultHeader",
                "CORRECT!", 30f,
                new Vector2(12f, 84f), new Vector2(1430f, 44f), Color.green, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            TMP_Text fbExpTxt = LabTextStyleHelper.CreateTMPText(fbRt, "ExplanationBody",
                "Explanation...", 25f,
                new Vector2(12f, -24f), new Vector2(1430f, 154f), new Color(0.92f, 0.96f, 1.0f), TextAlignmentOptions.TopLeft, FontStyles.Normal);
            fbExpTxt.enableAutoSizing = true;
            fbExpTxt.fontSizeMin = 19f;
            fbExpTxt.fontSizeMax = 26f;

            AssessmentBoardButton nextBtn = BuildBoardCardButton(
                quizRoot.transform,
                "NextQuestionButton",
                new Vector2(760f, -342f),
                new Vector2(390f, 150f),
                new Color(0.16f, 0.60f, 0.92f, 0.98f),
                "NEXT QUESTION",
                30f,
                TextAlignmentOptions.Center,
                stationAudio,
                matScreen,
                mainCam,
                out TMP_Text nextLbl);

            // -----------------------------------------------------------------
            // PANEL 2: SCOREBOARD & TOPIC ANALYSIS SCREEN
            // -----------------------------------------------------------------
            GameObject scoreRoot = new GameObject("ScoreboardView");
            scoreRoot.transform.SetParent(boardCanvas.transform, false);
            RectTransform scoreRt = scoreRoot.AddComponent<RectTransform>();
            scoreRt.anchorMin = Vector2.zero; scoreRt.anchorMax = Vector2.one;
            scoreRt.offsetMin = Vector2.zero; scoreRt.offsetMax = Vector2.zero;

            LabTextStyleHelper.CreateTMPText(scoreRoot.transform, "ScoreTitle",
                "ASSESSMENT SCOREBOARD & TOPIC MASTERY ANALYSIS", 32f,
                new Vector2(0f, 540f), new Vector2(1940f, 46f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            CreateUIImageRect(scoreRoot.transform, "SummaryBanner",
                new Vector2(0f, 444f), new Vector2(1920f, 114f), new Color(0.06f, 0.11f, 0.18f, 0.98f));

            TMP_Text overallScoreTxt = LabTextStyleHelper.CreateTMPText(scoreRoot.transform, "OverallScore",
                "SCORE:  10 / 10  (100%)", 44f,
                new Vector2(-520f, 444f), new Vector2(820f, 76f), Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            TMP_Text masteryTxt = LabTextStyleHelper.CreateTMPText(scoreRoot.transform, "MasteryRating",
                "MASTERY TIER:  DISTINCTION", 30f,
                new Vector2(410f, 444f), new Vector2(1040f, 76f), new Color(0.24f, 0.88f, 0.45f), TextAlignmentOptions.MidlineRight, FontStyles.Bold);

            // Left Half: 5 Topic Accuracy Breakdowns.
            CreateUIImageRect(scoreRoot.transform, "TopicsBg",
                new Vector2(-470f, 80f), new Vector2(960f, 570f), new Color(0.06f, 0.09f, 0.14f, 0.96f));
            LabTextStyleHelper.CreateTMPText(scoreRoot.transform, "TopicsHdr",
                "TOPIC-BY-TOPIC BREAKDOWN & LAB REVIEW GUIDE", 24f,
                new Vector2(-470f, 336f), new Vector2(910f, 36f), new Color(1.0f, 0.84f, 0.36f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            TMP_Text[] topicTxts = new TMP_Text[5];
            Image[] topicFills = new Image[5];
            float[] topicRowYs = { 268f, 166f, 64f, -38f, -140f };

            for (int t = 0; t < 5; t++)
            {
                topicTxts[t] = LabTextStyleHelper.CreateTMPText(scoreRoot.transform, "TopicLabel_" + t,
                    $"Topic {t + 1}: 2/2 (100%)", 22f,
                    new Vector2(-470f, topicRowYs[t] + 18f), new Vector2(900f, 38f), Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

                RectTransform tTrack = CreateUIImageRect(scoreRoot.transform, "TopicTrack_" + t,
                    new Vector2(-470f, topicRowYs[t] - 20f), new Vector2(900f, 22f), new Color(0.16f, 0.21f, 0.28f, 1f));
                RectTransform tFill = CreateStretchChild(tTrack, "Fill", new Color(0.24f, 0.88f, 0.45f, 1f));
                topicFills[t] = tFill.GetComponent<Image>();
            }

            // Right Half: Missed Questions Recap & Correct Principles.
            CreateUIImageRect(scoreRoot.transform, "MissedBg",
                new Vector2(490f, 80f), new Vector2(920f, 570f), new Color(0.06f, 0.09f, 0.14f, 0.96f));
            TMP_Text missedHdrTxt = LabTextStyleHelper.CreateTMPText(scoreRoot.transform, "MissedHdr",
                "MISSED QUESTIONS RECAP", 24f,
                new Vector2(490f, 336f), new Vector2(870f, 36f), new Color(1.0f, 0.62f, 0.48f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            TMP_Text missedBodyTxt = LabTextStyleHelper.CreateTMPText(scoreRoot.transform, "MissedBody",
                "Summary appears here.", 21f,
                new Vector2(490f, 44f), new Vector2(870f, 494f), new Color(0.92f, 0.96f, 1.0f), TextAlignmentOptions.TopLeft, FontStyles.Normal);
            missedBodyTxt.enableAutoSizing = true;
            missedBodyTxt.fontSizeMin = 15f;
            missedBodyTxt.fontSizeMax = 22f;

            // Retake Quiz Button centered cleanly at bottom.
            AssessmentBoardButton retakeBtn = BuildBoardCardButton(
                scoreRoot.transform,
                "RetakeQuizButton",
                new Vector2(0f, -356f),
                new Vector2(620f, 134f),
                new Color(0.18f, 0.62f, 0.34f, 0.98f),
                "RETAKE QUIZ (SHUFFLED)",
                32f,
                TextAlignmentOptions.Center,
                stationAudio,
                matScreen,
                mainCam,
                out _);

            scoreRoot.SetActive(false);

            GameObject lockedOverlay = BuildLockedOverlay(root.transform, "Assessment Board", matScreen, mainCam);

            AssessmentStationController ctrl = root.AddComponent<AssessmentStationController>();
            SerializedObject so = new SerializedObject(ctrl);
            LabTextStyleHelper.SetObjRef(so, "stationBeacon", beacon);
            LabTextStyleHelper.SetObjRef(so, "npcStandPoint", npcStand.transform);
            LabTextStyleHelper.SetObjRef(so, "lockedOverlay", lockedOverlay);
            LabTextStyleHelper.SetObjectArray(so, "hintTargets", new[] { anchor.transform, boardCanvas.transform });

            LabTextStyleHelper.SetObjRef(so, "stationAnchor", anchor);
            LabTextStyleHelper.SetObjectArray(so, "questions", questions);
            LabTextStyleHelper.SetObjRef(so, "audioSource", stationAudio);

            LabTextStyleHelper.SetObjRef(so, "quizViewRoot", quizRoot);
            LabTextStyleHelper.SetObjRef(so, "scoreboardViewRoot", scoreRoot);

            LabTextStyleHelper.SetObjRef(so, "questionProgressText", progressTxt);
            LabTextStyleHelper.SetObjRef(so, "topicTagText", topicTagTxt);
            LabTextStyleHelper.SetObjRef(so, "progressFillBar", progressFillImg);
            LabTextStyleHelper.SetObjRef(so, "questionPromptText", promptTxt);
            LabTextStyleHelper.SetObjectArray(so, "optionButtons", optBtns);
            LabTextStyleHelper.SetObjectArray(so, "optionTexts", optTxts);
            LabTextStyleHelper.SetObjRef(so, "feedbackPanel", fbRt.gameObject);
            LabTextStyleHelper.SetObjRef(so, "feedbackAccentBar", fbAccentImg);
            LabTextStyleHelper.SetObjRef(so, "feedbackResultText", fbResultTxt);
            LabTextStyleHelper.SetObjRef(so, "feedbackExplanationText", fbExpTxt);
            LabTextStyleHelper.SetObjRef(so, "nextQuestionButton", nextBtn);
            LabTextStyleHelper.SetObjRef(so, "nextQuestionLabel", nextLbl);

            LabTextStyleHelper.SetObjRef(so, "overallScoreText", overallScoreTxt);
            LabTextStyleHelper.SetObjRef(so, "masteryRatingText", masteryTxt);
            LabTextStyleHelper.SetObjectArray(so, "topicBreakdownTexts", topicTxts);
            LabTextStyleHelper.SetObjectArray(so, "topicAccuracyFills", topicFills);
            LabTextStyleHelper.SetObjRef(so, "missedRecapHeaderText", missedHdrTxt);
            LabTextStyleHelper.SetObjRef(so, "missedRecapBodyText", missedBodyTxt);
            LabTextStyleHelper.SetObjRef(so, "retakeQuizButton", retakeBtn);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAssetAndConnect(root, AssessmentPrefabPath, InteractionMode.AutomatedAction);
            root.transform.SetParent(stationsParent, false);
            root.transform.SetPositionAndRotation(worldPos, worldRot);
            return ctrl;
        }

        private static AssessmentBoardButton BuildBoardCardButton(
            Transform canvasParent,
            string name,
            Vector2 anchoredPos,
            Vector2 sizeDeltaPx,
            Color initialColor,
            string labelText,
            float fontSize,
            TextAlignmentOptions alignment,
            AudioSource audioSource,
            Material matColliderHelper,
            Camera mainCam,
            out TMP_Text labelTmp)
        {
            GameObject cardGo = new GameObject(name);
            cardGo.transform.SetParent(canvasParent, false);
            RectTransform rt = cardGo.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDeltaPx;

            Image bg = cardGo.AddComponent<Image>();
            bg.color = initialColor;
            bg.raycastTarget = true;

            Button uiBtn = cardGo.AddComponent<Button>();
            uiBtn.targetGraphic = bg;
            ColorBlock cb = uiBtn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            uiBtn.colors = cb;

            labelTmp = LabTextStyleHelper.CreateTMPText(cardGo.transform, "Label", labelText, fontSize,
                Vector2.zero, new Vector2(sizeDeltaPx.x - 44f, sizeDeltaPx.y - 16f), Color.white, alignment, FontStyles.Bold);
            labelTmp.enableAutoSizing = true;
            labelTmp.fontSizeMin = 17f;
            labelTmp.fontSizeMax = fontSize;

            // Add 3D BoxCollider (>= 5 cm depth on Z; Canvas localScale.z is 1.0 so specify Z in world meters!).
            BoxCollider col = cardGo.AddComponent<BoxCollider>();
            col.size = new Vector3(sizeDeltaPx.x, sizeDeltaPx.y, 0.056f);
            col.center = new Vector3(0f, 0f, 0.030f);

            XRSimpleInteractable xr = cardGo.AddComponent<XRSimpleInteractable>();
            xr.colliders.Add(col);

            InteractionFeedback fb = cardGo.AddComponent<InteractionFeedback>();
            SerializedObject fbSo = new SerializedObject(fb);
            LabTextStyleHelper.SetObjRef(fbSo, "interactable", xr);
            LabTextStyleHelper.SetObjRef(fbSo, "audioSource", audioSource);
            fbSo.ApplyModifiedPropertiesWithoutUndo();

            AssessmentBoardButton boardBtn = cardGo.AddComponent<AssessmentBoardButton>();
            SerializedObject bSo = new SerializedObject(boardBtn);
            LabTextStyleHelper.SetObjRef(bSo, "interactable", xr);
            LabTextStyleHelper.SetObjRef(bSo, "uiRelayButton", uiBtn);
            LabTextStyleHelper.SetObjRef(bSo, "cardBackground", bg);
            LabTextStyleHelper.SetObjRef(bSo, "audioSource", audioSource);
            bSo.ApplyModifiedPropertiesWithoutUndo();

            return boardBtn;
        }

        private static AssessmentQuestion[] CreateAllTenQuestions()
        {
            var specs = new (string file, string id, AssessmentTopic topic, string tag, string shortLbl,
                string prompt, string a, string b, string c, string d, int ans, string exp, string station)[]
            {
                ("Q01_Intrinsic_ZeroKelvin", "q01_0k", AssessmentTopic.IntrinsicSemiconductors,
                    "1. Intrinsic Semiconductors", "0 K Insulator",
                    "At 0 K, how does pure intrinsic Silicon behave electrically, and why?",
                    "Metallic conductor, because all 4 valence electrons are delocalized",
                    "Insulator, because all covalent bonds stay intact with zero free carriers",
                    "N-type semiconductor, because midgap Ei supplies free electrons",
                    "Superconductor, because lattice vibration scattering vanishes",
                    1,
                    "At 0 K thermal energy is zero, so all 4 covalent bonds per Group IV Silicon atom remain intact (n = p = ni = 0), making pure Si an insulator.",
                    "Station 1"),

                ("Q02_Intrinsic_Heating", "q02_temp_ni", AssessmentTopic.IntrinsicSemiconductors,
                    "1. Intrinsic Semiconductors", "Thermal e-/h+ & Eg",
                    "Germanium (Eg = 0.67 eV, ni ~ 2.4e13 cm^-3) conducts more than Silicon (Eg = 1.12 eV, ni ~ 1.5e10 cm^-3) at 300 K. When pure Si is heated, what occurs?",
                    "Conductivity drops like a metal while ni stays frozen at 1.5e10 cm^-3",
                    "Only free electrons are created so n becomes 100x greater than p",
                    "Thermal bond breaking raises n = p = ni exponentially, boosting conductivity",
                    "The bandgap widens from 1.12 eV to 2.0 eV to stop carrier generation",
                    2,
                    "Thermal energy breaks covalent bonds in equal electron-hole pairs (n = p = ni). Because ni(T) rises exponentially with T, conductivity increases strongly.",
                    "Station 1"),

                ("Q03_Extrinsic_NType", "q03_ntype", AssessmentTopic.ExtrinsicDoping,
                    "2. Extrinsic Doping (N & P)", "N-Type Charge Neutrality",
                    "Doping Silicon with Group V Phosphorus (P) at ND = 1e16 cm^-3 creates N-type Silicon. Which statement is true at room temperature?",
                    "The crystal holds net negative charge and Fermi level EF drops toward Ev",
                    "The crystal stays neutral (mobile e- balance fixed + donor ions) and EF shifts toward Ec",
                    "Holes become the majority carriers with p ~ ND",
                    "Donor energy level ED lies 0.045 eV above the valence band Ev",
                    1,
                    "Pentavalent donors (P, As, Sb) contribute a free electron (~0.045 eV below Ec) and leave a fixed + donor ion locked in the crystal, keeping total charge neutral while shifting EF up toward Ec.",
                    "Stations 2 & 3"),

                ("Q04_Extrinsic_PType", "q04_ptype", AssessmentTopic.ExtrinsicDoping,
                    "2. Extrinsic Doping (N & P)", "Mass-Action & P-Type",
                    "Silicon (ni = 1.5e10 cm^-3 at 300 K) is doped with Boron (Group III) to NA = 1e16 cm^-3. What are the equilibrium carrier densities?",
                    "n ~ 1e16 cm^-3 (majority) and p ~ 2.25e4 cm^-3 (minority)",
                    "n = p = 1e16 cm^-3 from charge neutrality",
                    "p ~ 1e16 cm^-3 (majority holes) and n = ni^2 / p ~ 2.25e4 cm^-3 (minority e-)",
                    "p ~ 1.5e10 cm^-3 and n = 0",
                    2,
                    "Trivalent acceptors (B, Al, Ga, In) form majority holes (p ~ NA) and fixed - acceptor ions. Mass-action n*p = ni^2 reduces minority electrons to (1.5e10)^2 / 1e16 = 2.25e4 cm^-3.",
                    "Stations 2 & 3"),

                ("Q05_Junction_Equilibrium", "q05_eq_junc", AssessmentTopic.JunctionEquilibrium,
                    "3. P-N Junction Equilibrium", "Drift = Diffusion & Flat EF",
                    "At thermal equilibrium (V = 0 V) across an unbiased P-N junction, how are the electric field, net current, and Fermi level configured?",
                    "Field points P -> N, diffusion exceeds drift, and EF steps vertically by qV0",
                    "Uncovered + donors (N) and - acceptors (P) form an N -> P field; drift balances diffusion (Inet = 0) and EF is flat",
                    "Mobile carriers vanish throughout both bulk P and N regions",
                    "Energy bands remain completely flat across the metallurgical boundary",
                    1,
                    "Diffusion uncovers fixed + donor ions (N side) and - acceptor ions (P side), creating an N -> P field. Drift cancels diffusion (Inet = 0), EF aligns flat, and bands bend by qV0.",
                    "Station 4"),

                ("Q06_Junction_Asymmetry", "q06_asym_w", AssessmentTopic.JunctionEquilibrium,
                    "3. P-N Junction Equilibrium", "Asymmetric Depletion W",
                    "In a Silicon P-N junction with lightly doped P side (NA = 1e15 cm^-3) and heavily doped N side (ND = 1e17 cm^-3), how does depletion width W divide?",
                    "Depletion extends 100x farther into the lightly doped P side (xp = 100 * xn)",
                    "Both sides always share identical depletion penetration (xp = xn = W / 2)",
                    "Depletion extends 100x farther into the heavily doped N side (xn = 100 * xp)",
                    "Built-in potential V0 drops to 0 V whenever NA != ND",
                    0,
                    "Charge balance NA * xp = ND * xn forces the depletion region farther into the lighter-doped side: xp = W * ND / (NA + ND).",
                    "Station 4"),

                ("Q07_Bias_Forward", "q07_fwd_bias", AssessmentTopic.BiasingBehavior,
                    "4. Forward & Reverse Bias", "Forward Bias Mechanism",
                    "Connecting + to the P terminal and - to N applies forward bias (V > 0). How do barrier height, depletion width W, and current respond?",
                    "Barrier rises to V0 + V, W widens, and current remains zero",
                    "Barrier drops to V0 - V, W widens, blocking majority diffusion",
                    "Barrier drops to V0 - V, W narrows, and majority carriers cross exponentially",
                    "Built-in field flips instantly once V > 0.05 V",
                    2,
                    "Forward bias opposes the built-in field, lowering the barrier to V0 - V and narrowing W so majority carriers surge exponentially once past the diode knee (limited by a series resistor).",
                    "Station 5"),

                ("Q08_Bias_Reverse", "q08_rev_bias", AssessmentTopic.BiasingBehavior,
                    "4. Forward & Reverse Bias", "Reverse Bias & Is Leakage",
                    "Connecting + to the N terminal and - to P applies reverse bias (V = -VR < 0, below breakdown). What occurs?",
                    "Barrier rises to V0 + VR, W widens, blocking majority carriers; tiny minority leakage Is flows",
                    "Depletion width shrinks to zero and illuminates a series indicator LED",
                    "Majority holes pour rapidly across the junction from P to N",
                    "Reverse saturation current Is drops to zero when temperature rises",
                    0,
                    "Reverse bias reinforces the internal barrier (V0 + VR) and widens W, suppressing majority diffusion while allowing only tiny thermally generated minority leakage Is (which rises with T).",
                    "Station 5"),

                ("Q09_IV_KneeAndTemp", "q09_iv_knee", AssessmentTopic.DiodeIVCharacteristics,
                    "5. Diode I-V Characteristics", "Knee Voltages & Temp Shift",
                    "Comparing Ge, Si, and LED forward knees plus temperature shifts on an I-V curve, which statement is accurate?",
                    "Ge ~ 2.0 V, Si ~ 0.7 V, LED ~ 0.3 V; heating shifts forward voltage up +2 mV/C",
                    "Ge ~ 0.3 V, Si ~ 0.7 V, LED ~ 1.8-3.0 V; heating drops forward voltage by ~2 mV/C",
                    "All diodes turn on identically at 0.70 V regardless of bandgap",
                    "Heating sharply reduces saturation current Is while blocking conduction",
                    1,
                    "Larger bandgaps raise the forward knee (Ge ~0.3 V < Si ~0.7 V < LED ~1.8-3.0 V). Heating exponentially grows Is(T), lowering forward voltage at a fixed current by roughly -2 mV/C.",
                    "Station 6"),

                ("Q10_IV_Breakdown", "q10_breakdown", AssessmentTopic.DiodeIVCharacteristics,
                    "5. Diode I-V Characteristics", "Zener vs Avalanche Breakdown",
                    "How do Zener breakdown and Avalanche breakdown differ in reverse bias?",
                    "Zener occurs in heavily doped junctions at low reverse V via tunnelling; Avalanche occurs at higher V via impact ionisation",
                    "Both mechanisms occur only in forward bias above +5 V",
                    "Zener relies on impact ionisation while Avalanche requires quantum tunnelling",
                    "Series resistors should never be used during diode breakdown tests",
                    0,
                    "Heavily doped Zener junctions break down at lower reverse voltages via quantum tunnelling, whereas lighter-doped junctions undergo Avalanche multiplication by impact ionisation at higher voltages.",
                    "Station 6")
            };

            AssessmentQuestion[] arr = new AssessmentQuestion[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                var sp = specs[i];
                string path = QuestionsFolder + "/" + sp.file + ".asset";
                AssessmentQuestion q = AssetDatabase.LoadAssetAtPath<AssessmentQuestion>(path);
                if (q == null)
                {
                    q = ScriptableObject.CreateInstance<AssessmentQuestion>();
                    AssetDatabase.CreateAsset(q, path);
                }

                q.Configure(sp.id, sp.topic, sp.tag, sp.shortLbl, sp.prompt,
                    sp.a, sp.b, sp.c, sp.d, sp.ans, sp.exp, sp.station);
                EditorUtility.SetDirty(q);
                arr[i] = q;
            }

            return arr;
        }

        private static void WirePhase7LessonsAndNpc(AssessmentStationController ctrl)
        {
            LessonManager lessonManager = Object.FindAnyObjectByType<LessonManager>();
            if (lessonManager == null)
            {
                return;
            }

            DialogueLine dlSt6RecDone = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_St6_Rec_Done.asset");
            if (dlSt6RecDone != null)
            {
                dlSt6RecDone.Configure("st6_rec_done",
                    "Station 6 complete! Head over to Station A: Final Assessment Board to test your mastery of the entire lab!",
                    NPCEmotion.Happy);
                EditorUtility.SetDirty(dlSt6RecDone);
            }

            // Click-to-Guide sequence for Station A (pure theory MCQ exam + scoreboard explanation).
            DialogueLine dlGuideASteps = CreateDialogue("DL_Guide_StA_Steps", "guide_A_steps",
                "Assessment Steps: 1) Ray-click option A, B, C, or D for each of the 10 theory questions, 2) Read the instant explanation & press NEXT QUESTION, and 3) Inspect your Topic Breakdown Scoreboard or press RETAKE QUIZ!",
                NPCEmotion.Pointing);
            DialogueLine dlGuideAPhys = CreateDialogue("DL_Guide_StA_Physics", "guide_A_physics",
                "This final Assessment tests 10 theory concepts grounded in our lab (2 each across Intrinsic Si, N/P Doping, P-N Junction Equilibrium, Forward/Reverse Biasing, and Diode I-V Curves) and pinpoints topics to review!",
                NPCEmotion.Explaining, dlGuideASteps);

            // Step 28 & Step 29 Lesson Dialogue Assets.
            DialogueLine dlStAIntro = CreateDialogue("DL_StationA_Intro", "stA_intro",
                "Welcome to Station A: Final Theory Assessment! Teleport onto Pad A in front of the Assessment Whiteboard.",
                NPCEmotion.Encouraging);
            DialogueLine dlStATpH20 = CreateDialogue("DL_StA_Tp_H20", "stA_tp_h20",
                "Hint: Aim at floor Pad A in front of the Assessment Whiteboard and release Teleport (I).", NPCEmotion.Thinking);
            DialogueLine dlStATpH45 = CreateDialogue("DL_StA_Tp_H45", "stA_tp_h45",
                "Walk or teleport over to Pad A beside me to begin your 10-question theory assessment!", NPCEmotion.Pointing);
            DialogueLine dlStATpDone = CreateDialogue("DL_StA_Tp_Done", "stA_tp_done",
                "Step 2: Complete all 10 theory MCQs by pointing your ray and clicking option A, B, C, or D, then inspect your Scoreboard & Topic Analysis!",
                NPCEmotion.Happy);

            DialogueLine dlStAQuizIntro = CreateDialogue("DL_StA_Quiz_Intro", "stA_quiz_intro",
                "Select option A, B, C, or D on each card and press NEXT QUESTION after reading the explanation (no hardware circuit required here!).",
                NPCEmotion.Explaining);
            DialogueLine dlStAQuizH20 = CreateDialogue("DL_StA_Quiz_H20", "stA_quiz_h20",
                "Hint: Point your ray onto choice A, B, C, or D and press Trigger (T) / Left Click, then click NEXT QUESTION.", NPCEmotion.Thinking);
            DialogueLine dlStAQuizH45 = CreateDialogue("DL_StA_Quiz_H45", "stA_quiz_h45",
                "Answer the MCQ on the Assessment Whiteboard I am pointing toward to finish the lab!", NPCEmotion.Pointing);

            // Final Lab Completion Wrap-Up Message.
            DialogueLine dlWrapUp = CreateDialogue("DL_Assessment_Complete", "assessment_wrapup",
                "Congratulations — you have completed the entire Semiconductor & P-N Junction VR Lab! Review your Topic Breakdown on the scoreboard, revisit any flagged bench anytime, or press RETAKE QUIZ!",
                NPCEmotion.Happy);

            // Preserve Steps 01..27 (Phase 1–6); replace old Step_28_Assessment_Ready placeholder with real Steps 28 & 29.
            List<LessonStep> steps = new List<LessonStep>();
            string[] prevStepFiles =
            {
                "Step_01_Teleport", "Step_02_Grab", "Step_03_Socket", "Step_04_Button", "Step_05_Screen",
                "Step_06_St1_Teleport", "Step_07_St1_Lens", "Step_08_St1_Heat", "Step_09_St1_ToggleGe",
                "Step_10_St2_Teleport", "Step_11_St2_Dope", "Step_12_St2_AdjustND",
                "Step_13_St3_Teleport", "Step_14_St3_Dope", "Step_15_St3_AdjustNA",
                "Step_16_St4_Teleport", "Step_17_St4_Join", "Step_18_St4_TimeLapse", "Step_19_St4_Asymmetric",
                "Step_20_St5_Teleport", "Step_21_St5_ForwardBias", "Step_22_St5_ReverseBias", "Step_23_St5_BreakdownReset",
                "Step_24_St6_Teleport", "Step_25_St6_SocketDiode", "Step_26_St6_SweepAndTemp", "Step_27_St6_RecordPoints"
            };
            for (int i = 0; i < prevStepFiles.Length; i++)
            {
                LessonStep s = AssetDatabase.LoadAssetAtPath<LessonStep>(LessonFolder + "/" + prevStepFiles[i] + ".asset");
                if (s != null) steps.Add(s);
            }

            string oldPlaceholder = LessonFolder + "/Step_28_Assessment_Ready.asset";
            if (AssetDatabase.LoadAssetAtPath<LessonStep>(oldPlaceholder) != null)
            {
                AssetDatabase.DeleteAsset(oldPlaceholder);
            }

            steps.Add(CreateStep("Step_28_StA_Teleport", "sA_teleport", "A", "Teleport to Assessment Board",
                "Teleport onto Pad A in front of the Assessment Whiteboard.", "assessment.teleport",
                dlStAIntro, dlStATpDone, dlStATpH20, dlStATpH45, 0));

            steps.Add(CreateStep("Step_29_StA_CompleteQuiz", "sA_quiz", "A", "Complete 10-Question Theory Quiz",
                "Answer all 10 theory MCQs and inspect your Scoreboard & Topic Breakdown.", "assessment.complete",
                dlStAQuizIntro, dlWrapUp, dlStAQuizH20, dlStAQuizH45, 1));

            string[] orderedNames =
            {
                "Station_T_TutorialPad", "Station_1_Intrinsic", "Station_2_NType", "Station_3_PType",
                "Station_4_Junction", "Station_5_Biasing", "Station_6_IV", "Station_A_Assessment"
            };
            List<StationController> controllers = new List<StationController>();
            for (int i = 0; i < orderedNames.Length; i++)
            {
                GameObject sGo = GameObject.Find(orderedNames[i]);
                StationController sc = sGo != null ? sGo.GetComponent<StationController>() : null;
                if (sc != null) controllers.Add(sc);
            }

            SerializedObject lmSo = new SerializedObject(lessonManager);
            LabTextStyleHelper.SetObjectList(lmSo, "steps", steps);
            LabTextStyleHelper.SetObjectArray(lmSo, "stations", controllers.ToArray());
            lmSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lessonManager);

            // Rebind Station A entry into NPCController cleanly (preserving all T, 1..6 guides and askTopics!)
            NPCController npcCtrl = Object.FindAnyObjectByType<NPCController>();
            if (npcCtrl != null)
            {
                TeleportationAnchor anchA = ctrl.GetComponentInChildren<TeleportationAnchor>(true);
                Transform standA = ctrl.NpcStandPoint;

                SerializedObject npcSo = new SerializedObject(npcCtrl);
                SerializedProperty guidesProp = npcSo.FindProperty("stationGuides");
                if (guidesProp != null)
                {
                    bool foundA = false;
                    for (int i = 0; i < guidesProp.arraySize; i++)
                    {
                        SerializedProperty elem = guidesProp.GetArrayElementAtIndex(i);
                        if (elem.FindPropertyRelative("stationId").stringValue == "A")
                        {
                            elem.FindPropertyRelative("teleportAnchor").objectReferenceValue = anchA;
                            elem.FindPropertyRelative("standPoint").objectReferenceValue = standA;
                            elem.FindPropertyRelative("guideDialogue").objectReferenceValue = dlGuideAPhys;
                            foundA = true;
                            break;
                        }
                    }

                    if (!foundA)
                    {
                        int idx = guidesProp.arraySize;
                        guidesProp.arraySize = idx + 1;
                        SerializedProperty elem = guidesProp.GetArrayElementAtIndex(idx);
                        elem.FindPropertyRelative("stationId").stringValue = "A";
                        elem.FindPropertyRelative("teleportAnchor").objectReferenceValue = anchA;
                        elem.FindPropertyRelative("standPoint").objectReferenceValue = standA;
                        elem.FindPropertyRelative("guideDialogue").objectReferenceValue = dlGuideAPhys;
                    }
                }

                npcSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(npcCtrl);
                if (PrefabUtility.IsPartOfPrefabInstance(npcCtrl.gameObject))
                {
                    PrefabUtility.ApplyPrefabInstance(npcCtrl.gameObject, InteractionMode.AutomatedAction);
                }
            }
        }

        private static string RebakeAndVerifyNavMeshStandPoints()
        {
            LabNavMeshSurface surface = Object.FindAnyObjectByType<LabNavMeshSurface>();
            if (surface == null) return "No LabNavMeshSurface found.";

            SerializedObject surfSo = new SerializedObject(surface);
            NavMeshData navData = surfSo.FindProperty("navMeshData")?.objectReferenceValue as NavMeshData;
            if (navData == null) return "LabNavMeshSurface navMeshData missing.";

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.35f;
            settings.agentHeight = 1.75f;
            settings.agentSlope = 35f;
            settings.agentClimb = 0.25f;

            Bounds bounds = new Bounds(Vector3.zero, new Vector3(18f, 4f, 14f));
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            NavMeshBuilder.UpdateNavMeshData(navData, settings, sources, bounds);
            EditorUtility.SetDirty(navData);

            NavMeshDataInstance inst = NavMesh.AddNavMeshData(navData, surface.transform.position, surface.transform.rotation);
            int okCount = 0;
            int totalCount = 0;
            try
            {
                string[] checkStations =
                {
                    "Station_T_TutorialPad", "Station_1_Intrinsic", "Station_2_NType",
                    "Station_3_PType", "Station_4_Junction", "Station_5_Biasing", "Station_6_IV", "Station_A_Assessment"
                };
                for (int i = 0; i < checkStations.Length; i++)
                {
                    GameObject sGo = GameObject.Find(checkStations[i]);
                    StationController sc = sGo != null ? sGo.GetComponent<StationController>() : null;
                    if (sc != null && sc.NpcStandPoint != null)
                    {
                        totalCount++;
                        if (NavMesh.SamplePosition(sc.NpcStandPoint.position, out NavMeshHit hit, 0.85f, NavMesh.AllAreas))
                        {
                            okCount++;
                            sc.NpcStandPoint.position = hit.position;
                            EditorUtility.SetDirty(sc.NpcStandPoint);
                        }
                    }
                }
            }
            finally
            {
                if (inst.valid) inst.Remove();
            }

            return $"NavMesh rebaked; {okCount}/{totalCount} NPCStandPoints verified on NavMesh.";
        }

        private static StationBeacon BuildBeaconParts(Transform parent, string stationId, string title,
            Material matFrame, Material matScreen, Material matAccent, Camera mainCam, out TeleportationAnchor anchor)
        {
            GameObject anchorGo = new GameObject("TeleportAnchor_" + stationId);
            anchorGo.transform.SetParent(parent, false);
            anchorGo.transform.localPosition = new Vector3(0f, 0f, 1.35f);
            anchorGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject spawnPoint = new GameObject("SpawnTransform");
            spawnPoint.transform.SetParent(anchorGo.transform, false);

            GameObject padDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            padDisc.name = "AnchorPad";
            padDisc.transform.SetParent(anchorGo.transform, false);
            padDisc.transform.localPosition = new Vector3(0f, 0.006f, 0f);
            padDisc.transform.localScale = new Vector3(0.75f, 0.006f, 0.75f);
            padDisc.GetComponent<Renderer>().sharedMaterial = matFrame;

            GameObject padRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            padRing.name = "AnchorRing";
            padRing.transform.SetParent(anchorGo.transform, false);
            padRing.transform.localPosition = new Vector3(0f, 0.004f, 0f);
            padRing.transform.localScale = new Vector3(0.86f, 0.004f, 0.86f);
            Object.DestroyImmediate(padRing.GetComponent<Collider>());
            Renderer ringRenderer = padRing.GetComponent<Renderer>();
            ringRenderer.sharedMaterial = matAccent;

            anchor = anchorGo.AddComponent<TeleportationAnchor>();
            anchor.teleportAnchorTransform = spawnPoint.transform;
            anchor.matchOrientation = MatchOrientation.TargetUpAndForward;
            int mask = InteractionLayerMask.GetMask("Teleport");
            anchor.interactionLayers = mask != 0 ? mask : unchecked((int)(1u << 31));

            GameObject floorSign = new GameObject("FloorNumberSign");
            floorSign.transform.SetParent(anchorGo.transform, false);
            floorSign.transform.localPosition = new Vector3(0.58f, 0.01f, 0f);
            GameObject floorTile = CreateBox("Tile", floorSign.transform, Vector3.zero, new Vector3(0.32f, 0.012f, 0.32f), matFrame, false);
            Object.DestroyImmediate(floorTile.GetComponent<Collider>());
            Canvas floorCanvas = LabTextStyleHelper.CreateWorldCanvas("FloorNumberUI", floorSign.transform,
                new Vector3(0f, 0.009f, 0f), new Vector3(90f, 0f, 0f), new Vector2(0.30f, 0.30f), 320, 320, mainCam, false);
            TMP_Text floorNumText = LabTextStyleHelper.CreateTMPText(floorCanvas.transform, "Number", stationId, 180f,
                Vector2.zero, new Vector2(300f, 300f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject holoRoot = new GameObject("HologramTitle");
            holoRoot.transform.SetParent(parent, false);
            holoRoot.transform.localPosition = new Vector3(0f, 2.32f, 0f);

            GameObject holoBacking = CreateBox("HoloBacking", holoRoot.transform, new Vector3(0f, 0f, -0.014f), new Vector3(1.68f, 0.36f, 0.014f), matScreen, false);
            Object.DestroyImmediate(holoBacking.GetComponent<Collider>());
            GameObject holoBar = CreateBox("HoloAccentBar", holoRoot.transform, new Vector3(0f, -0.195f, -0.010f), new Vector3(1.68f, 0.025f, 0.024f), matAccent, false);
            Object.DestroyImmediate(holoBar.GetComponent<Collider>());
            Renderer holoBarRenderer = holoBar.GetComponent<Renderer>();

            Canvas holoCanvas = LabTextStyleHelper.CreateWorldCanvas("HoloCanvas", holoRoot.transform,
                new Vector3(0f, 0f, 0.002f), new Vector3(0f, 180f, 0f),
                new Vector2(1.62f, 0.32f), 960, 190, mainCam, false);
            TMP_Text holoText = LabTextStyleHelper.CreateTMPText(holoCanvas.transform, "Title", stationId + ". " + title, 64f,
                Vector2.zero, new Vector2(920f, 175f), new Color(0.92f, 0.98f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            StationBeacon beacon = parent.gameObject.AddComponent<StationBeacon>();
            SerializedObject bSo = new SerializedObject(beacon);
            LabTextStyleHelper.SetStr(bSo, "stationId", stationId);
            LabTextStyleHelper.SetStr(bSo, "stationTitle", title);
            LabTextStyleHelper.SetObjRef(bSo, "floorNumberText", floorNumText);
            LabTextStyleHelper.SetObjRef(bSo, "hologramTitleText", holoText);
            LabTextStyleHelper.SetObjRef(bSo, "teleportAnchor", anchor);
            LabTextStyleHelper.SetBool(bSo, "startUnlocked", false);
            LabTextStyleHelper.SetObjectArray(bSo, "accentRenderers", new[] { ringRenderer, holoBarRenderer });
            bSo.ApplyModifiedPropertiesWithoutUndo();

            return beacon;
        }

        private static GameObject BuildLockedOverlay(Transform parent, string stationTitle, Material matScreen, Camera mainCam)
        {
            GameObject overlay = new GameObject("LockedOverlay");
            overlay.transform.SetParent(parent, false);
            overlay.transform.localPosition = new Vector3(0f, 1.44f, 0.14f);

            GameObject dimBox = CreateBox("DimScreen", overlay.transform, Vector3.zero, new Vector3(2.08f, 0.62f, 0.025f), matScreen, false);
            Object.DestroyImmediate(dimBox.GetComponent<Collider>());

            Canvas c = LabTextStyleHelper.CreateWorldCanvas("LockedCanvas", overlay.transform,
                new Vector3(0f, 0f, 0.018f), new Vector3(0f, 180f, 0f),
                new Vector2(2.00f, 0.54f), 960, 260, mainCam, false);
            LabTextStyleHelper.CreateTMPText(c.transform, "LockedText",
                $"LOCKED: Complete Station 6 (I-V Analyzer) to unlock {stationTitle}", 34f,
                Vector2.zero, new Vector2(920f, 220f), new Color(1.0f, 0.68f, 0.32f), TextAlignmentOptions.Center, FontStyles.Bold);
            return overlay;
        }

        private static RectTransform CreateUIImageRect(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private static RectTransform CreateStretchChild(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool isStatic)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.isStatic = isStatic;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            return go;
        }

        private static DialogueLine CreateDialogue(string fileName, string id, string text, NPCEmotion emotion, DialogueLine next = null)
        {
            string path = DialogueFolder + "/" + fileName + ".asset";
            DialogueLine asset = AssetDatabase.LoadAssetAtPath<DialogueLine>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DialogueLine>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Configure(id, text, emotion, next, null);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static LessonStep CreateStep(string fileName, string id, string stationId, string title, string objective,
            string requiredActionId, DialogueLine intro, DialogueLine completion, DialogueLine h20, DialogueLine h45, int targetIndex)
        {
            string path = LessonFolder + "/" + fileName + ".asset";
            LessonStep step = AssetDatabase.LoadAssetAtPath<LessonStep>(path);
            if (step == null)
            {
                step = ScriptableObject.CreateInstance<LessonStep>();
                AssetDatabase.CreateAsset(step, path);
            }

            step.Configure(id, stationId, title, objective, requiredActionId, intro, completion, h20, h45, targetIndex);
            EditorUtility.SetDirty(step);
            return step;
        }

        private static Material GetOrCreateMat(string name, Shader shader, Color baseColor)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Material mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/PNJunctionLab", "Prefabs");
            EnsureFolder("Assets/PNJunctionLab/Prefabs", "Stations");
            EnsureFolder("Assets/PNJunctionLab", "Data");
            EnsureFolder(DataFolder, "Assessment");
            EnsureFolder(AssessmentFolder, "Questions");
            EnsureFolder(DataFolder, "Lessons");
            EnsureFolder(DataFolder, "Dialogue");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
