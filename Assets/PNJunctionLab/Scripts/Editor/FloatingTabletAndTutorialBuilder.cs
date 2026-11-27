using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Converts LabTablet from its pedestal dock into a hidden-at-start, button-summoned floating air tablet
    /// and adds "Summon Floating Lab Tablet (Press Y)" to the Tutorial Pad curriculum & Dr. Vera's dialogue
    /// without modifying Stations 1-6, Assessment Board, or SemiconductorModel physics.
    /// </summary>
    public static class FloatingTabletAndTutorialBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string TabletPrefabPath = "Assets/PNJunctionLab/Prefabs/LabTablet.prefab";
        private const string TutorialPrefabPath = "Assets/PNJunctionLab/Prefabs/TutorialPadStation.prefab";
        private const string InputActionsAssetPath = "Assets/PNJunctionLab/Data/LabTabletControls.inputactions";
        private const string LessonFolder = "Assets/PNJunctionLab/Data/Lessons";
        private const string DialogueFolder = "Assets/PNJunctionLab/Data/Dialogue";

        [MenuItem("PNJunctionLab/Apply Floating Tablet & Tutorial Step")]
        public static void ApplyMenu()
        {
            Debug.Log(ApplyFloatingTabletAndTutorialUpgrade());
        }

        /// <summary>
        /// Applies the floating LabTablet conversion, Tutorial Pad curriculum addition, and dialogue updates cleanly.
        /// </summary>
        public static string ApplyFloatingTabletAndTutorialUpgrade()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.EndsWith("PNJunctionLab.unity", StringComparison.OrdinalIgnoreCase))
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            InputActionReference toggleActionRef = EnsureTabletInputActionsAsset();
            AttachInputAssetToManager(toggleActionRef);

            RemovePedestalAndUpgradeTablet(toggleActionRef);
            UpdateTutorialPadStationAndPrefab();
            UpdateTutorialDialogueAndSteps();

            int badScalers = AuditWorldCanvasDPU();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"FloatingTabletAndTutorialBuilder Complete: InputAsset={(toggleActionRef != null)}, BadCanvasDPU={badScalers}.";
        }

        private static InputActionReference EnsureTabletInputActionsAsset()
        {
            const string json = @"{
    ""name"": ""LabTabletControls"",
    ""maps"": [
        {
            ""name"": ""Tablet"",
            ""id"": ""d7d96c5a-2a19-4db1-9e12-55a4021cb101"",
            ""actions"": [
                {
                    ""name"": ""ToggleTablet"",
                    ""type"": ""Button"",
                    ""id"": ""3f4e2a0b-76a8-4b75-8f8a-03c5fa7d2202"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": """",
                    ""id"": ""82ef49cc-0d23-4ef8-b55c-844c5db53303"",
                    ""path"": ""<XRController>{LeftHand}/secondaryButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""ToggleTablet"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""9f4ba312-9cd4-47f5-82bb-74d8e5f14404"",
                    ""path"": ""<Keyboard>/f"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""ToggleTablet"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": []
}";
            File.WriteAllText(InputActionsAssetPath, json);
            AssetDatabase.ImportAsset(InputActionsAssetPath, ImportAssetOptions.ForceUpdate);

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(InputActionsAssetPath);
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is InputActionReference iar && iar.name.Contains("ToggleTablet"))
                {
                    return iar;
                }
            }

            InputActionAsset loadedAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAssetPath);
            if (loadedAsset != null)
            {
                InputAction action = loadedAsset.FindAction("Tablet/ToggleTablet", true);
                if (action != null)
                {
                    return InputActionReference.Create(action);
                }
            }

            return null;
        }

        private static void AttachInputAssetToManager(InputActionReference toggleActionRef)
        {
            if (toggleActionRef == null || toggleActionRef.asset == null)
            {
                return;
            }

            InputActionManager manager = Object.FindAnyObjectByType<InputActionManager>();
            if (manager == null)
            {
                return;
            }

            List<InputActionAsset> assets = manager.actionAssets != null
                ? new List<InputActionAsset>(manager.actionAssets)
                : new List<InputActionAsset>();

            bool alreadyAdded = false;
            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i] == toggleActionRef.asset)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                assets.Add(toggleActionRef.asset);
                manager.actionAssets = assets;
                EditorUtility.SetDirty(manager);
            }
        }

        private static void RemovePedestalAndUpgradeTablet(InputActionReference toggleActionRef)
        {
            GameObject dock = GameObject.Find("LabTabletStationDock");
            while (dock != null)
            {
                Object.DestroyImmediate(dock);
                dock = GameObject.Find("LabTabletStationDock");
            }

            GameObject tabletGo = GameObject.Find("LabTablet");
            if (tabletGo == null)
            {
                throw new InvalidOperationException("LabTablet GameObject not found in scene.");
            }

            tabletGo.SetActive(true);
            tabletGo.transform.SetParent(null, true);
            tabletGo.transform.localScale = Vector3.one;
            tabletGo.transform.SetPositionAndRotation(new Vector3(0.04f, 1.16f, -3.43f), Quaternion.Euler(25f, 0f, 0f));

            RespawnableItem oldRespawn = tabletGo.GetComponent<RespawnableItem>();
            if (oldRespawn != null)
            {
                Object.DestroyImmediate(oldRespawn);
            }

            // Ensure a clean ContentRoot container holds visual geometry, collider, and World-Space Canvas
            // so scale animations never scale the root Rigidbody/Collider to near-zero and root LabTablet stays listening.
            Transform contentRoot = tabletGo.transform.Find("ContentRoot");
            if (contentRoot == null)
            {
                GameObject crGo = new GameObject("ContentRoot");
                crGo.transform.SetParent(tabletGo.transform, false);
                crGo.transform.localPosition = Vector3.zero;
                crGo.transform.localRotation = Quaternion.identity;
                crGo.transform.localScale = Vector3.one;
                contentRoot = crGo.transform;
            }
            contentRoot.gameObject.SetActive(true);
            contentRoot.localScale = Vector3.one;

            List<Transform> moveChildren = new List<Transform>();
            for (int i = 0; i < tabletGo.transform.childCount; i++)
            {
                Transform ch = tabletGo.transform.GetChild(i);
                if (ch == contentRoot || ch.name == "GrabAttachPoint" || ch.name == "InteractionTooltip")
                {
                    continue;
                }
                moveChildren.Add(ch);
            }

            for (int i = 0; i < moveChildren.Count; i++)
            {
                moveChildren[i].SetParent(contentRoot, false);
            }

            // Ensure Rigidbody stays floating/kinematic with zero gravity.
            Rigidbody rb = tabletGo.GetComponent<Rigidbody>();
            if (rb == null) rb = tabletGo.AddComponent<Rigidbody>();
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            EditorUtility.SetDirty(rb);

            // Configure XRGrabInteractable for mid-air repositioning with ThrowOnDetach disabled.
            XRGrabInteractable grab = tabletGo.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                grab.throwOnDetach = false;
                grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
                Transform chassis = contentRoot.Find("Chassis");
                BoxCollider chassisCol = chassis != null ? chassis.GetComponent<BoxCollider>() : null;
                if (chassisCol != null)
                {
                    grab.colliders.Clear();
                    grab.colliders.Add(chassisCol);
                }
                EditorUtility.SetDirty(grab);
            }

            InteractionFeedback fb = tabletGo.GetComponent<InteractionFeedback>();
            if (fb != null)
            {
                SerializedObject fbSo = new SerializedObject(fb);
                LabTextStyleHelper.SetStr(fbSo, "tooltipLabel", "Lab Tablet (Grab G / Toggle Y or F)");
                Transform rim = contentRoot.Find("RimAccent");
                if (rim != null && rim.GetComponent<Renderer>() != null)
                {
                    LabTextStyleHelper.SetObjectArray(fbSo, "highlightRenderers", new Object[] { rim.GetComponent<Renderer>() });
                }
                fbSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fb);
            }

            // Ensure CanvasScaler DynamicPixelsPerUnit = 15 and balanced ObjectivePanel bounds.
            Canvas tabletCanvas = contentRoot.GetComponentInChildren<Canvas>(true);
            if (tabletCanvas != null)
            {
                CanvasScaler scaler = tabletCanvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.dynamicPixelsPerUnit = 15f;
                    scaler.referencePixelsPerUnit = 100f;
                    EditorUtility.SetDirty(scaler);
                }

                Transform objPanel = tabletCanvas.transform.Find("ObjectivePanel");
                if (objPanel != null)
                {
                    TMP_Text objTxt = objPanel.Find("CurrentObjective")?.GetComponent<TMP_Text>();
                    if (objTxt != null)
                    {
                        RectTransform rt = objTxt.rectTransform;
                        rt.anchoredPosition = new Vector2(0f, 102f);
                        rt.sizeDelta = new Vector2(920f, 102f);
                        objTxt.enableAutoSizing = true;
                        objTxt.fontSizeMin = 20f;
                        objTxt.fontSizeMax = 28f;
                        EditorUtility.SetDirty(objTxt);
                    }

                    TMP_Text stepTxt = objPanel.Find("StepList")?.GetComponent<TMP_Text>();
                    if (stepTxt != null)
                    {
                        RectTransform rt = stepTxt.rectTransform;
                        rt.anchoredPosition = new Vector2(0f, -62f);
                        rt.sizeDelta = new Vector2(920f, 224f);
                        stepTxt.enableAutoSizing = true;
                        stepTxt.fontSizeMin = 18f;
                        stepTxt.fontSizeMax = 25f;
                        EditorUtility.SetDirty(stepTxt);
                    }
                }
                Transform ctlPanel = tabletCanvas.transform.Find("ControlsPanel");
                if (ctlPanel != null)
                {
                    TMP_Text ctlTxt = ctlPanel.Find("ControlsBody")?.GetComponent<TMP_Text>();
                    if (ctlTxt != null)
                    {
                        RectTransform rt = ctlTxt.rectTransform;
                        rt.anchoredPosition = new Vector2(0f, -20f);
                        rt.sizeDelta = new Vector2(920f, 380f);
                        ctlTxt.enableAutoSizing = true;
                        ctlTxt.fontSizeMin = 15f;
                        ctlTxt.fontSizeMax = 22f;
                        EditorUtility.SetDirty(ctlTxt);
                    }
                }
            }

            // Wire LabTablet serialized fields.
            LabTablet tablet = tabletGo.GetComponent<LabTablet>();
            if (tablet != null)
            {
                Camera mainCam = Camera.main;
                SerializedObject tabSo = new SerializedObject(tablet);
                LabTextStyleHelper.SetObjRef(tabSo, "grabInteractable", grab);
                LabTextStyleHelper.SetObjRef(tabSo, "interactionFeedback", fb);
                LabTextStyleHelper.SetObjRef(tabSo, "tabletBody", rb);
                LabTextStyleHelper.SetObjRef(tabSo, "visualRoot", contentRoot);
                LabTextStyleHelper.SetObjRef(tabSo, "playerHead", mainCam != null ? mainCam.transform : null);
                LabTextStyleHelper.SetFloat(tabSo, "summonDistanceMeters", 0.65f);
                LabTextStyleHelper.SetFloat(tabSo, "summonVerticalOffsetMeters", -0.05f);
                LabTextStyleHelper.SetFloat(tabSo, "summonUpwardTiltDegrees", 20f);
                LabTextStyleHelper.SetFloat(tabSo, "toggleAnimDurationSeconds", 0.15f);
                LabTextStyleHelper.SetBool(tabSo, "hideOnStart", true);

                if (toggleActionRef != null)
                {
                    SerializedProperty actProp = tabSo.FindProperty("toggleTabletAction");
                    if (actProp != null)
                    {
                        SerializedProperty useRef = actProp.FindPropertyRelative("m_UseReference");
                        SerializedProperty refProp = actProp.FindPropertyRelative("m_Reference");
                        if (useRef != null) useRef.boolValue = true;
                        if (refProp != null) refProp.objectReferenceValue = toggleActionRef;
                    }
                }

                tabSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tablet);
            }

            PrefabUtility.SaveAsPrefabAssetAndConnect(tabletGo, TabletPrefabPath, InteractionMode.AutomatedAction);
        }

        private static void UpdateTutorialPadStationAndPrefab()
        {
            GameObject tutorialGo = GameObject.Find("Station_T_TutorialPad");
            if (tutorialGo == null)
            {
                throw new InvalidOperationException("Station_T_TutorialPad not found.");
            }

            Transform screenCanvas = tutorialGo.transform.Find("TutorialScreen/ScreenCanvas");
            if (screenCanvas != null)
            {
                CanvasScaler scaler = screenCanvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.dynamicPixelsPerUnit = 15f;
                    EditorUtility.SetDirty(scaler);
                }

                TMP_Text statusBanner = screenCanvas.Find("StatusBanner")?.GetComponent<TMP_Text>();
                if (statusBanner != null)
                {
                    statusBanner.text = "SKILLS VERIFIED: 0 / 6";
                    EditorUtility.SetDirty(statusBanner);
                }

                TMP_Text checklist = screenCanvas.Find("Checklist")?.GetComponent<TMP_Text>();
                if (checklist != null)
                {
                    RectTransform rt = checklist.rectTransform;
                    rt.anchoredPosition = new Vector2(0f, -54f);
                    rt.sizeDelta = new Vector2(935f, 324f);
                    checklist.enableAutoSizing = true;
                    checklist.fontSize = 29f;
                    checklist.fontSizeMin = 21f;
                    checklist.fontSizeMax = 29f;
                    checklist.text =
                        "<color=#D0DCE8>[ ] 1. Teleport onto Tutorial Anchor</color>\n" +
                        "<color=#D0DCE8>[ ] 2. Grab the Test Cube</color>\n" +
                        "<color=#D0DCE8>[ ] 3. Drop Cube into Socket</color>\n" +
                        "<color=#D0DCE8>[ ] 4. Press the Green Lab Button</color>\n" +
                        "<color=#D0DCE8>[ ] 5. Ray-Point at this Screen</color>\n" +
                        "<color=#D0DCE8>[ ] 6. Summon Floating Lab Tablet (Y / F)</color>";
                    EditorUtility.SetDirty(checklist);
                }
            }

            // Save UI/prefab updates cleanly first, then assign scene LabTablet reference cleanly on scene instance.
            if (PrefabUtility.IsPartOfPrefabInstance(tutorialGo))
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(tutorialGo, TutorialPrefabPath, InteractionMode.AutomatedAction);
            }

            TutorialPadController controller = tutorialGo.GetComponent<TutorialPadController>();
            LabTablet tablet = Object.FindAnyObjectByType<LabTablet>(FindObjectsInactive.Include);
            if (controller != null && tablet != null)
            {
                SerializedObject ctrlSo = new SerializedObject(controller);
                LabTextStyleHelper.SetObjRef(ctrlSo, "labTablet", tablet);
                ctrlSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }
        }

        private static void UpdateTutorialDialogueAndSteps()
        {
            // 1. Update existing Tutorial DialogueLines cleanly.
            DialogueLine dlTeleportGo = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Tutorial_TeleportPrompt.asset");

            DialogueLine dlControlsHint = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Tutorial_ControlsExplain.asset");
            if (dlControlsHint != null)
            {
                dlControlsHint.Configure(
                    "tut_controls",
                    "On Quest 3: Grip grabs, Trigger selects UI, Left Thumbstick teleports, and Left Y summons or dismisses your floating Lab Tablet. On Desktop Simulator: W/A/S/D moves, Right-Drag looks, I teleports, G grabs, T/Click selects UI, and F (or Shift+2) toggles the Lab Tablet.",
                    NPCEmotion.Explaining,
                    dlTeleportGo);
                EditorUtility.SetDirty(dlControlsHint);
            }

            DialogueLine dlWelcome = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Tutorial_Welcome.asset");
            if (dlWelcome != null)
            {
                DialogueChoice c1 = new DialogueChoice();
                c1.Configure("Explain Controls", dlControlsHint);
                DialogueChoice c2 = new DialogueChoice();
                c2.Configure("Let's Start!", dlTeleportGo);
                dlWelcome.Configure(
                    "tut_welcome",
                    "Welcome to the Semiconductor Lab! I am Dr. Vera. Let's quickly check all six VR skills on the Tutorial Pad — including your floating Lab Tablet — before we explore silicon.",
                    NPCEmotion.Happy,
                    null,
                    new[] { c1, c2 });
                EditorUtility.SetDirty(dlWelcome);
            }

            DialogueLine dlStep5Done = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Step5_Done.asset");
            if (dlStep5Done != null)
            {
                dlStep5Done.Configure(
                    "s5_done",
                    "Ray pointing verified! One final skill: press Y on your Left controller (or F on Desktop / Shift+2) to summon the floating Lab Tablet, check your station steps, then press it again to dismiss.",
                    NPCEmotion.Happy);
                EditorUtility.SetDirty(dlStep5Done);
            }

            DialogueLine dlGuideTSteps = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Guide_T_Steps.asset");
            if (dlGuideTSteps != null)
            {
                dlGuideTSteps.Configure(
                    "guide_t_steps",
                    "Tutorial Steps: 1) Teleport onto Pad T, 2) Grab the blue Test Cube (G), 3) Drop it into the center Socket, 4) Press the Green Button (T/N), 5) Ray-point at the Tutorial Screen, and 6) Press Y (or F on Desktop) to summon and dismiss your Floating Lab Tablet!",
                    NPCEmotion.Pointing);
                EditorUtility.SetDirty(dlGuideTSteps);
            }

            DialogueLine dlGuideTPhys = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Guide_T_Physics.asset");
            if (dlGuideTPhys != null)
            {
                dlGuideTPhys.Configure(
                    "guide_t_physics",
                    "Welcome! This Tutorial Pad warms up your VR hands: Grip (G/M) grabs props, Trigger (T) or Left-Click activates UI & buttons, Thumbstick-Up (I) teleports, and Left Y (Desktop F / Shift+2) summons your floating Lab Tablet anytime.",
                    NPCEmotion.Explaining,
                    dlGuideTSteps);
                EditorUtility.SetDirty(dlGuideTPhys);
            }

            // 2. Author Step 6 (Floating Lab Tablet) DialogueLines & LessonStep.
            DialogueLine dlTabIntro = CreateOrUpdateDialogue(
                "DL_Step6_Tablet_Intro",
                "s6_tab_intro",
                "Step 6: Press Y on your Left controller (or F on Desktop) to summon your floating Lab Tablet, read your current station steps, then press Y / F again to dismiss it.",
                NPCEmotion.Explaining);

            DialogueLine dlTabDone = CreateOrUpdateDialogue(
                "DL_Step6_Tablet_Done",
                "s6_tab_done",
                "All 6 Tutorial skills verified! You can summon your floating Lab Tablet anytime with Left Y (Desktop F). Station 1 (Intrinsic Silicon) is now unlocked — walk or teleport there when ready!",
                NPCEmotion.Happy);

            DialogueLine dlTabH20 = CreateOrUpdateDialogue(
                "DL_Step6_Tablet_H20",
                "s6_tab_h20",
                "Hint: Press Left Controller Y (or press F / Shift+2 on Desktop) to summon the floating Lab Tablet in front of you, read your Objective steps, then press Y / F again to dismiss it.",
                NPCEmotion.Thinking);

            DialogueLine dlTabH45 = CreateOrUpdateDialogue(
                "DL_Step6_Tablet_H45",
                "s6_tab_h45",
                "Press Y on your Left controller (or F on keyboard) to open the floating Lab Tablet right here at the Tutorial Pad, then press it a second time to dismiss!",
                NPCEmotion.Pointing);

            LessonStep stepTablet = CreateOrUpdateLessonStep(
                "Step_05b_Tablet",
                "t_tablet",
                "T",
                "Summon Floating Lab Tablet (Press Y)",
                "Press Y (Left controller) or F (Desktop) to summon the floating Lab Tablet, read your station steps, then press Y / F again to dismiss it.",
                "tutorial.tablet",
                dlTabIntro,
                dlTabDone,
                dlTabH20,
                dlTabH45,
                5);

            // 3. Splice Step_05b_Tablet cleanly into LessonManager.steps right after Step_05_Screen
            // without disturbing Stations 1-6 or Assessment Board step assets.
            LessonManager lessonManager = Object.FindAnyObjectByType<LessonManager>();
            if (lessonManager == null)
            {
                throw new InvalidOperationException("LessonManager not found in scene.");
            }

            SerializedObject lmSo = new SerializedObject(lessonManager);
            SerializedProperty stepsProp = lmSo.FindProperty("steps");
            if (stepsProp != null)
            {
                List<LessonStep> orderedSteps = new List<LessonStep>();
                for (int i = 0; i < stepsProp.arraySize; i++)
                {
                    LessonStep existing = stepsProp.GetArrayElementAtIndex(i).objectReferenceValue as LessonStep;
                    if (existing != null && existing != stepTablet)
                    {
                        orderedSteps.Add(existing);
                    }
                }

                int insertIdx = 0;
                bool inserted = false;
                for (int i = 0; i < orderedSteps.Count; i++)
                {
                    if (orderedSteps[i].StepId == "t_screen")
                    {
                        orderedSteps.Insert(i + 1, stepTablet);
                        inserted = true;
                        break;
                    }
                }

                if (!inserted)
                {
                    orderedSteps.Insert(insertIdx, stepTablet);
                }

                LabTextStyleHelper.SetObjectList(lmSo, "steps", orderedSteps);
                lmSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(lessonManager);
            }
        }

        private static DialogueLine CreateOrUpdateDialogue(
            string fileName,
            string lineId,
            string text,
            NPCEmotion emotion,
            DialogueLine nextLine = null,
            DialogueChoice[] choices = null)
        {
            string path = DialogueFolder + "/" + fileName + ".asset";
            DialogueLine asset = AssetDatabase.LoadAssetAtPath<DialogueLine>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DialogueLine>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Configure(lineId, text, emotion, nextLine, choices);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static LessonStep CreateOrUpdateLessonStep(
            string fileName,
            string stepId,
            string stationId,
            string title,
            string objective,
            string actionId,
            DialogueLine intro,
            DialogueLine completion,
            DialogueLine h20,
            DialogueLine h45,
            int hintTargetIndex)
        {
            string path = LessonFolder + "/" + fileName + ".asset";
            LessonStep step = AssetDatabase.LoadAssetAtPath<LessonStep>(path);
            if (step == null)
            {
                step = ScriptableObject.CreateInstance<LessonStep>();
                AssetDatabase.CreateAsset(step, path);
            }

            step.Configure(stepId, stationId, title, objective, actionId, intro, completion, h20, h45, hintTargetIndex);
            EditorUtility.SetDirty(step);
            return step;
        }

        private static int AuditWorldCanvasDPU()
        {
            int bad = 0;
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].renderMode != RenderMode.WorldSpace) continue;
                CanvasScaler scaler = canvases[i].GetComponent<CanvasScaler>();
                if (scaler == null || Mathf.Abs(scaler.dynamicPixelsPerUnit - 15f) > 0.1f)
                {
                    bad++;
                }
            }
            return bad;
        }
    }
}
