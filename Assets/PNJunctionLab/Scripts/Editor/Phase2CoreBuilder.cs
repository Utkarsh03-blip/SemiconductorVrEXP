using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Creates Phase 2 ScriptableObjects, NavMesh data, NPC scientist placeholder prefab,
    /// Lab Tablet prefab, Debug Panel prefab, and wires LessonManager + DialogueSystem in PNJunctionLab.unity.
    /// </summary>
    public static class Phase2CoreBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string DataRoot = "Assets/PNJunctionLab/Data";
        private const string DialogueFolder = DataRoot + "/Dialogue";
        private const string GlossaryFolder = DataRoot + "/Glossary";
        private const string LessonFolder = DataRoot + "/Lessons";
        private const string NavMeshAssetPath = DataRoot + "/LabNavMeshData.asset";

        private const string PrefabsFolder = "Assets/PNJunctionLab/Prefabs";
        private const string MaterialsFolder = "Assets/PNJunctionLab/Materials";
        private const string NpcFolder = "Assets/PNJunctionLab/NPC";

        private const string NpcPrefabPath = PrefabsFolder + "/NPCScientist.prefab";
        private const string TabletPrefabPath = PrefabsFolder + "/LabTablet.prefab";
        private const string DebugPrefabPath = PrefabsFolder + "/DebugPanel.prefab";

        [MenuItem("PNJunctionLab/Build Phase 2 Core Systems")]
        public static void BuildPhase2Menu()
        {
            BuildPhase2();
        }

        public static string BuildPhase2()
        {
            EnsureFolders();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                return "Failed to open scene at " + ScenePath;
            }

            Camera mainCam = FindMainCamera();

            GlossaryTopic[] topics = CreateGlossaryAssets();
            List<LessonStep> steps = CreateLessonAndDialogueAssets();

            NavMeshData navData = BakeLabNavMesh();
            AttachNavMeshSurface(navData);

            // Clean any prior Phase 2 roots so re-runs stay idempotent.
            DestroyIfExists("[CoreSystems]");
            DestroyIfExists("NPCScientist");
            DestroyIfExists("LabTablet");
            DestroyIfExists("LabTabletStationDock");
            DestroyIfExists("DebugPanelStand");

            GameObject coreRoot = new GameObject("[CoreSystems]");

            // Wire StationControllers on all U-shape stations and add NPC stand points.
            StationController[] stations = SetupStationsAndStandPoints();

            // Create DialogueSystem on CoreSystems.
            DialogueSystem dialogueSystem = coreRoot.AddComponent<DialogueSystem>();
            SerializedObject dsSo = new SerializedObject(dialogueSystem);
            SetStr(dsSo, "defaultSpeakerName", "Dr. Vera");
            SetObjectArray(dsSo, "askMeTopics", topics);
            dsSo.ApplyModifiedPropertiesWithoutUndo();

            // Create LessonManager on CoreSystems (wired after NPCController is built).
            LessonManager lessonManager = coreRoot.AddComponent<LessonManager>();

            // Build NPC Scientist prefab & instance.
            GameObject npcGo = BuildNpcScientist(dialogueSystem, mainCam);
            NPCController npcController = npcGo.GetComponent<NPCController>();
            NPCSpeechBubble speechBubble = npcGo.GetComponentInChildren<NPCSpeechBubble>(true);

            // Wire LessonManager.
            SerializedObject lmSo = new SerializedObject(lessonManager);
            SetObjectList(lmSo, "steps", steps);
            SetObjectArray(lmSo, "stations", stations);
            SetObjRef(lmSo, "dialogueSystem", dialogueSystem);
            SetObjRef(lmSo, "npcController", npcController);
            SetFloat(lmSo, "firstHintDelaySeconds", 20f);
            SetFloat(lmSo, "strongHintDelaySeconds", 45f);
            lmSo.ApplyModifiedPropertiesWithoutUndo();

            // Build Grabbable Lab Tablet + Stand Dock.
            LocomotionModeController locoController = Object.FindAnyObjectByType<LocomotionModeController>();
            BuildLabTabletAndDock(lessonManager, dialogueSystem, locoController, speechBubble, topics, mainCam);

            // Build Dev/Editor-only Debug Panel.
            BuildDebugPanel(lessonManager, dialogueSystem, mainCam);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return saved
                ? "Phase 2 core systems, ScriptableObjects, NavMesh, NPC, LabTablet, and DebugPanel built successfully."
                : "Failed to save scene after Phase 2 build.";
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing("Assets/PNJunctionLab", "Data");
            CreateFolderIfMissing(DataRoot, "Dialogue");
            CreateFolderIfMissing(DataRoot, "Glossary");
            CreateFolderIfMissing(DataRoot, "Lessons");
            CreateFolderIfMissing("Assets/PNJunctionLab", "NPC");
        }

        private static void CreateFolderIfMissing(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static GlossaryTopic[] CreateGlossaryAssets()
        {
            GlossaryTopic[] result = new GlossaryTopic[6];

            result[0] = CreateGlossaryAsset("Glossary_Intrinsic", "intrinsic", "Intrinsic Si/Ge",
                "Pure Group IV crystal (4 valence electrons). At 0 K it has no free carriers (insulator). Heat breaks covalent bonds so n = p = ni (Si: 1.5e10 cm^-3, Eg = 1.12 eV at 300 K).",
                "Pure silicon has 4 valence electrons per atom. At 0 K all bonds stay intact; heating creates free electron-hole pairs with n = p = ni.");

            result[1] = CreateGlossaryAsset("Glossary_NType", "ntype", "N-Type Doping",
                "Pentavalent Group V donors (P, As, Sb) supply a 5th electron (~0.045 eV below Ec for P in Si). Majority = electrons (n ~ ND), minority = holes (p = ni^2 / n). Stays electrically neutral.",
                "Adding pentavalent donors like Phosphorus donates free electrons and leaves fixed positive donor ions, shifting the Fermi level toward the conduction band.");

            result[2] = CreateGlossaryAsset("Glossary_PType", "ptype", "P-Type Doping",
                "Trivalent Group III acceptors (B, Al, Ga, In) leave an incomplete bond (a hole). Majority = holes (p ~ NA), minority = electrons (n = ni^2 / p). Stays electrically neutral.",
                "Trivalent acceptors like Boron create holes just above the valence band plus fixed negative ions, shifting the Fermi level toward the valence band.");

            result[3] = CreateGlossaryAsset("Glossary_Depletion", "depletion", "Depletion & V0",
                "Diffusion and recombination uncover fixed + donor ions (N) and - acceptor ions (P), creating a depletion field N->P and built-in potential V0 = VT * ln(NA*ND/ni^2) (~0.69 V for Si).",
                "At the junction, diffusion uncovers fixed ions that form an N-to-P electric field. Drift balances diffusion at equilibrium with built-in potential V0.");

            result[4] = CreateGlossaryAsset("Glossary_Biasing", "biasing", "Forward/Reverse Bias",
                "Forward (P to +): barrier drops to V0 - V, depletion narrows, current rises above knee (~0.7 V Si, ~0.3 V Ge, 1.8-3 V LED). Reverse (P to -): barrier rises to V0 + VR, only Is flows until breakdown.",
                "Forward bias lowers the barrier so majority carriers cross above the knee voltage. Reverse bias widens the depletion region until Zener or avalanche breakdown.");

            result[5] = CreateGlossaryAsset("Glossary_Shockley", "shockley", "Shockley I-V Law",
                "I = Is * (exp(V / (n * VT)) - 1), with VT = kT/q (~25.9 mV at 300 K) and n = 1..2. Forward voltage drops ~2 mV/C as temperature rises; reverse Is grows with temperature.",
                "The Shockley equation models exponential forward current above the knee and tiny saturation current Is in reverse bias. A series resistor limits forward current.");

            return result;
        }

        private static GlossaryTopic CreateGlossaryAsset(string fileName, string id, string title, string summary, string spokenText)
        {
            DialogueLine spokenLine = CreateDialogueAsset("DL_Glossary_" + fileName, "glossary_" + id, spokenText, NPCEmotion.Explaining);
            string assetPath = GlossaryFolder + "/" + fileName + ".asset";
            GlossaryTopic topic = AssetDatabase.LoadAssetAtPath<GlossaryTopic>(assetPath);
            if (topic == null)
            {
                topic = ScriptableObject.CreateInstance<GlossaryTopic>();
                AssetDatabase.CreateAsset(topic, assetPath);
            }

            topic.Configure(id, title, summary, spokenLine);
            EditorUtility.SetDirty(topic);
            return topic;
        }

        private static List<LessonStep> CreateLessonAndDialogueAssets()
        {
            // Welcome branching dialogue for Tutorial Step 1.
            DialogueLine dlControlsHint = CreateDialogueAsset(
                "DL_Tutorial_ControlsExplain",
                "tut_controls",
                "On desktop Simulator: Right-Mouse drag looks, W/A/S/D moves, [ switches to Left hand (hold Shift for Left buttons/stick), I teleports, G/Click grabs (J/L rotates held tablet), and T/Click selects UI.",
                NPCEmotion.Explaining);

            DialogueLine dlTeleportGo = CreateDialogueAsset(
                "DL_Tutorial_TeleportPrompt",
                "tut_teleport_go",
                "Step 1: Aim your controller ray onto the glowing Tutorial Anchor pad in front of the bench and release to teleport there.",
                NPCEmotion.Encouraging);

            DialogueChoice choiceControls = new DialogueChoice();
            choiceControls.Configure("Explain Controls", dlControlsHint);
            DialogueChoice choiceStart = new DialogueChoice();
            choiceStart.Configure("Let's Start!", dlTeleportGo);

            // Wire dlControlsHint -> dlTeleportGo.
            dlControlsHint.Configure(
                "tut_controls",
                "On desktop Simulator: Right-Mouse drag looks, W/A/S/D moves, [ switches to Left hand (hold Shift for Left buttons/stick), I teleports, G/Click grabs (J/L rotates held tablet), and T/Click selects UI.",
                NPCEmotion.Explaining,
                dlTeleportGo);
            EditorUtility.SetDirty(dlControlsHint);

            DialogueLine dlWelcome = CreateDialogueAsset(
                "DL_Tutorial_Welcome",
                "tut_welcome",
                "Welcome to the Semiconductor Lab! I am Dr. Vera. Let's quickly check the five VR skills on the Tutorial Pad before we explore silicon.",
                NPCEmotion.Happy,
                null,
                new[] { choiceControls, choiceStart });

            DialogueLine dlStep1Done = CreateDialogueAsset("DL_Step1_Done", "s1_done",
                "Great teleport! Next, grab the blue Test Cube on the left side of the workbench.", NPCEmotion.Happy);
            DialogueLine dlStep1H20 = CreateDialogueAsset("DL_Step1_H20", "s1_h20",
                "Hint: Aim at the glowing cyan circle in front of the Tutorial Pad table using Teleport (thumbstick / I).", NPCEmotion.Thinking);
            DialogueLine dlStep1H45 = CreateDialogueAsset("DL_Step1_H45", "s1_h45",
                "Look right where I am pointing — aim your ray at that floor anchor ring and release to snap onto the pad.", NPCEmotion.Pointing);

            DialogueLine dlStep2Intro = CreateDialogueAsset("DL_Step2_Intro", "s2_intro",
                "Step 2: Reach for the blue Test Cube (or aim with your ray) and press Grip (G) to pick it up.", NPCEmotion.Explaining);
            DialogueLine dlStep2Done = CreateDialogueAsset("DL_Step2_Done", "s2_done",
                "Nice grip! Now place that cube into the glowing cyan Socket at the center of the bench.", NPCEmotion.Happy);
            DialogueLine dlStep2H20 = CreateDialogueAsset("DL_Step2_H20", "s2_h20",
                "Hint: Hover over the blue Test Cube on the left pad and hold/press Grip (G in Simulator).", NPCEmotion.Thinking);
            DialogueLine dlStep2H45 = CreateDialogueAsset("DL_Step2_H45", "s2_h45",
                "Here is the Test Cube I am pointing at! If it falls off the room, press the amber Return Cube button.", NPCEmotion.Pointing);

            DialogueLine dlStep3Intro = CreateDialogueAsset("DL_Step3_Intro", "s3_intro",
                "Step 3: Move the Test Cube into the center Socket ring until it turns green and snaps in.", NPCEmotion.Explaining);
            DialogueLine dlStep3Done = CreateDialogueAsset("DL_Step3_Done", "s3_done",
                "Clean snap! Notice how sockets glow green for valid items. Next, press the Green Lab Button.", NPCEmotion.Happy);
            DialogueLine dlStep3H20 = CreateDialogueAsset("DL_Step3_H20", "s3_h20",
                "Hint: Hold the Test Cube over the glowing blue ring in the middle of the table and release Grip.", NPCEmotion.Thinking);
            DialogueLine dlStep3H45 = CreateDialogueAsset("DL_Step3_H45", "s3_h45",
                "Drop the cube right onto the center socket pad I am pointing toward.", NPCEmotion.Pointing);

            DialogueLine dlStep4Intro = CreateDialogueAsset("DL_Step4_Intro", "s4_intro",
                "Step 4: Poke the Green Lab Button directly or aim your ray at it and press Select / Trigger (T).", NPCEmotion.Explaining);
            DialogueLine dlStep4Done = CreateDialogueAsset("DL_Step4_Done", "s4_done",
                "Button registered! One last skill: aim your controller ray at the Tutorial Screen above the bench.", NPCEmotion.Happy);
            DialogueLine dlStep4H20 = CreateDialogueAsset("DL_Step4_H20", "s4_h20",
                "Hint: Aim at the green square button on the right side of the table and press Trigger (T) or poke it (N).", NPCEmotion.Thinking);
            DialogueLine dlStep4H45 = CreateDialogueAsset("DL_Step4_H45", "s4_h45",
                "Look at the green button I am pointing to on the right of the bench — poke or ray-select it!", NPCEmotion.Pointing);

            DialogueLine dlStep5Intro = CreateDialogueAsset("DL_Step5_Intro", "s5_intro",
                "Step 5: Point your controller ray at the Tutorial Screen to verify ray hover and UI pointing.", NPCEmotion.Explaining);
            DialogueLine dlStep5Done = CreateDialogueAsset("DL_Step5_Done", "s5_done",
                "All 5 Tutorial skills verified! Station 1 (Intrinsic Silicon) is now unlocked — follow me when Phase 3 begins.", NPCEmotion.Happy);
            DialogueLine dlStep5H20 = CreateDialogueAsset("DL_Step5_H20", "s5_h20",
                "Hint: Aim either controller ray directly at the dark Tutorial Screen panel above the table.", NPCEmotion.Thinking);
            DialogueLine dlStep5H45 = CreateDialogueAsset("DL_Step5_H45", "s5_h45",
                "Point your ray straight at the Tutorial Screen I am indicating!", NPCEmotion.Pointing);

            DialogueLine dlStation1Intro = CreateDialogueAsset("DL_Station1_Intro", "st1_intro",
                "Welcome to Station 1: Intrinsic Silicon! Station 1 apparatus will be assembled in Phase 3. Meanwhile, feel free to ask me glossary questions or test the Lab Tablet.", NPCEmotion.Explaining);

            List<LessonStep> steps = new List<LessonStep>
            {
                CreateLessonStepAsset("Step_01_Teleport", "t_teleport", "T", "Teleport to Tutorial Pad",
                    "Teleport onto the glowing anchor pad in front of the Tutorial workbench.", "tutorial.teleport",
                    dlWelcome, dlStep1Done, dlStep1H20, dlStep1H45, 0),

                CreateLessonStepAsset("Step_02_Grab", "t_grab", "T", "Grab the Test Cube",
                    "Pick up the blue silicon test cube from the left side of the workbench.", "tutorial.grab",
                    dlStep2Intro, dlStep2Done, dlStep2H20, dlStep2H45, 1),

                CreateLessonStepAsset("Step_03_Socket", "t_socket", "T", "Drop Cube in Socket",
                    "Place the grabbed cube into the glowing center socket ring.", "tutorial.socket",
                    dlStep3Intro, dlStep3Done, dlStep3H20, dlStep3H45, 2),

                CreateLessonStepAsset("Step_04_Button", "t_button", "T", "Press the Green Button",
                    "Poke or ray-select the green lab push button on the workbench.", "tutorial.button",
                    dlStep4Intro, dlStep4Done, dlStep4H20, dlStep4H45, 3),

                CreateLessonStepAsset("Step_05_Screen", "t_screen", "T", "Ray-Point at Screen",
                    "Aim your controller ray onto the Tutorial Screen to verify UI pointing.", "tutorial.screen",
                    dlStep5Intro, dlStep5Done, dlStep5H20, dlStep5H45, 4),

                CreateLessonStepAsset("Step_06_Station1_Ready", "s1_ready", "1", "Intrinsic Silicon (Phase 3)",
                    "Station 1 unlocked! Ready for Phase 3 apparatus build.", "station1.complete",
                    dlStation1Intro, null, dlStation1Intro, dlStation1Intro, 0)
            };

            return steps;
        }

        private static DialogueLine CreateDialogueAsset(
            string fileName,
            string id,
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

            asset.Configure(id, text, emotion, nextLine, choices);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static LessonStep CreateLessonStepAsset(
            string fileName,
            string stepId,
            string stationId,
            string title,
            string objective,
            string requiredActionId,
            DialogueLine intro,
            DialogueLine completion,
            DialogueLine hint20,
            DialogueLine hint45,
            int hintTargetIndex)
        {
            string path = LessonFolder + "/" + fileName + ".asset";
            LessonStep step = AssetDatabase.LoadAssetAtPath<LessonStep>(path);
            if (step == null)
            {
                step = ScriptableObject.CreateInstance<LessonStep>();
                AssetDatabase.CreateAsset(step, path);
            }

            step.Configure(stepId, stationId, title, objective, requiredActionId, intro, completion, hint20, hint45, hintTargetIndex);
            EditorUtility.SetDirty(step);
            return step;
        }

        private static NavMeshData BakeLabNavMesh()
        {
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();

            // Walkable lab floor (16m x 11m at y = 0).
            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(15.6f, 0.2f, 10.6f),
                transform = Matrix4x4.TRS(new Vector3(0f, -0.1f, 0f), Quaternion.identity, Vector3.one),
                area = 0
            });

            // Non-walkable obstacles: workbenches and decor so NavMeshAgent routes smoothly around them.
            GameObject stationsRoot = GameObject.Find("[Stations]");
            if (stationsRoot != null)
            {
                foreach (Transform station in stationsRoot.transform)
                {
                    Transform bench = station.Find("Workbench");
                    if (bench != null)
                    {
                        Transform top = bench.Find("TableTop");
                        Vector3 worldPos = top != null ? top.position : station.position + Vector3.up * 0.45f;
                        sources.Add(new NavMeshBuildSource
                        {
                            shape = NavMeshBuildSourceShape.Box,
                            size = new Vector3(2.15f, 1.2f, 0.95f),
                            transform = Matrix4x4.TRS(new Vector3(worldPos.x, 0.6f, worldPos.z), station.rotation, Vector3.one),
                            area = 1 // Not walkable
                        });
                    }
                    else
                    {
                        // Assessment board stand
                        sources.Add(new NavMeshBuildSource
                        {
                            shape = NavMeshBuildSourceShape.Box,
                            size = new Vector3(1.75f, 1.5f, 0.6f),
                            transform = Matrix4x4.TRS(new Vector3(station.position.x, 0.75f, station.position.z), station.rotation, Vector3.one),
                            area = 1
                        });
                    }
                }
            }

            // Fume hood & shelves obstacle boxes.
            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(1.05f, 2.0f, 1.9f),
                transform = Matrix4x4.TRS(new Vector3(-7.45f, 1.0f, -2.6f), Quaternion.identity, Vector3.one),
                area = 1
            });

            NavMeshBuildSettings buildSettings = NavMesh.GetSettingsByID(0);
            buildSettings.agentRadius = 0.35f;
            buildSettings.agentHeight = 1.75f;
            buildSettings.agentClimb = 0.25f;
            buildSettings.agentSlope = 35f;

            Bounds bounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(18f, 4f, 13f));
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(buildSettings, sources, bounds, Vector3.zero, Quaternion.identity);
            data.name = "LabNavMeshData";

            NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(NavMeshAssetPath);
            }

            AssetDatabase.CreateAsset(data, NavMeshAssetPath);
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void AttachNavMeshSurface(NavMeshData navData)
        {
            GameObject labRoom = GameObject.Find("[LabRoom]");
            if (labRoom == null)
            {
                labRoom = new GameObject("[LabRoom]");
            }

            LabNavMeshSurface surface = labRoom.GetComponent<LabNavMeshSurface>();
            if (surface == null)
            {
                surface = labRoom.AddComponent<LabNavMeshSurface>();
            }

            surface.SetNavMeshData(navData);
            EditorUtility.SetDirty(surface);
        }

        private static StationController[] SetupStationsAndStandPoints()
        {
            string[] orderedNames =
            {
                "Station_T_TutorialPad",
                "Station_1_Intrinsic",
                "Station_2_NType",
                "Station_3_PType",
                "Station_4_Junction",
                "Station_5_Biasing",
                "Station_6_IV",
                "Station_A_Assessment"
            };

            List<StationController> controllers = new List<StationController>();
            for (int i = 0; i < orderedNames.Length; i++)
            {
                GameObject go = GameObject.Find(orderedNames[i]);
                if (go == null) continue;

                // Ensure NpcStandPoint child exists comfortably beside the station anchor.
                Transform stand = go.transform.Find("NpcStandPoint");
                if (stand == null)
                {
                    GameObject standGo = new GameObject("NpcStandPoint");
                    standGo.transform.SetParent(go.transform, false);
                    standGo.transform.localPosition = new Vector3(-1.15f, 0f, 1.05f);
                    stand = standGo.transform;
                }

                StationController ctrl = go.GetComponent<StationController>();
                if (ctrl == null)
                {
                    ctrl = go.AddComponent<StationStubController>();
                }

                StationBeacon beacon = go.GetComponent<StationBeacon>();
                SerializedObject ctrlSo = new SerializedObject(ctrl);
                SetObjRef(ctrlSo, "stationBeacon", beacon);
                SetObjRef(ctrlSo, "npcStandPoint", stand);
                ctrlSo.ApplyModifiedPropertiesWithoutUndo();

                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
                }

                controllers.Add(ctrl);
            }

            return controllers.ToArray();
        }

        private static GameObject BuildNpcScientist(DialogueSystem dialogueSystem, Camera mainCam)
        {
            Material coatMat = GetOrCreateLitMat(NpcFolder + "/Mat_NpcLabCoat.mat", new Color(0.94f, 0.96f, 0.99f, 1f), 0.25f);
            Material skinMat = GetOrCreateLitMat(NpcFolder + "/Mat_NpcSkin.mat", new Color(0.86f, 0.72f, 0.62f, 1f), 0.2f);
            Material trimMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_AccentBase.mat");
            Material visorMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_TableFrame.mat");
            Material panelMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_ScreenPanel.mat");

            GameObject npcRoot = new GameObject("NPCScientist");
            npcRoot.transform.position = new Vector3(-3.3f, 0f, -4.1f);
            npcRoot.transform.rotation = Quaternion.Euler(0f, 20f, 0f);

            NavMeshAgent agent = npcRoot.AddComponent<NavMeshAgent>();
            agent.enabled = false; // Enabled in NPCController.Start after LabNavMeshSurface registers NavMeshData.
            agent.radius = 0.35f;
            agent.height = 1.75f;
            agent.speed = 2.4f;
            agent.angularSpeed = 360f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.15f;

            AudioSource npcAudio = npcRoot.AddComponent<AudioSource>();
            npcAudio.spatialBlend = 0.8f;
            npcAudio.playOnAwake = false;

            // NPCModelSlot: user-supplied 3D model drops right under this slot later.
            GameObject modelSlot = new GameObject("NPCModelSlot");
            modelSlot.transform.SetParent(npcRoot.transform, false);

            GameObject placeholder = new GameObject("PlaceholderRig");
            placeholder.transform.SetParent(modelSlot.transform, false);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "CapsuleBody";
            body.transform.SetParent(placeholder.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            body.transform.localScale = new Vector3(0.48f, 0.56f, 0.48f);
            body.GetComponent<Renderer>().sharedMaterial = coatMat;

            GameObject badge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            badge.name = "LabBadge";
            badge.transform.SetParent(placeholder.transform, false);
            badge.transform.localPosition = new Vector3(0.11f, 1.05f, 0.23f);
            badge.transform.localScale = new Vector3(0.10f, 0.07f, 0.02f);
            Object.DestroyImmediate(badge.GetComponent<Collider>());
            badge.GetComponent<Renderer>().sharedMaterial = trimMat;

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(placeholder.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.52f, 0f);
            head.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = skinMat;

            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "SafetyGlasses";
            visor.transform.SetParent(head.transform, false);
            visor.transform.localPosition = new Vector3(0f, 0.08f, 0.46f);
            visor.transform.localScale = new Vector3(0.75f, 0.24f, 0.22f);
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            visor.GetComponent<Renderer>().sharedMaterial = visorMat;

            // Pointer arm pivot so 45s strong hint points cleanly toward hint target even with placeholder capsule.
            GameObject armPivot = new GameObject("PointerArmPivot");
            armPivot.transform.SetParent(placeholder.transform, false);
            armPivot.transform.localPosition = new Vector3(0.28f, 1.15f, 0f);

            GameObject armMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            armMesh.name = "RightArm";
            armMesh.transform.SetParent(armPivot.transform, false);
            armMesh.transform.localPosition = new Vector3(0f, -0.18f, 0.06f);
            armMesh.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            armMesh.transform.localScale = new Vector3(0.11f, 0.24f, 0.11f);
            Object.DestroyImmediate(armMesh.GetComponent<Collider>());
            armMesh.GetComponent<Renderer>().sharedMaterial = coatMat;

            // World-space billboarded Speech Bubble above head.
            GameObject bubbleRoot = new GameObject("NPCSpeechBubble");
            bubbleRoot.transform.SetParent(npcRoot.transform, false);
            bubbleRoot.transform.localPosition = new Vector3(0f, 2.22f, 0f);

            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(bubbleRoot.transform, false);

            GameObject bgBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bgBox.name = "BubbleBacking";
            bgBox.transform.SetParent(visualRoot.transform, false);
            bgBox.transform.localScale = new Vector3(1.56f, 0.84f, 0.03f);
            bgBox.GetComponent<Renderer>().sharedMaterial = panelMat;
            BoxCollider bubbleCol = bgBox.GetComponent<BoxCollider>();

            GameObject topBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topBar.name = "AccentBar";
            topBar.transform.SetParent(visualRoot.transform, false);
            topBar.transform.localPosition = new Vector3(0f, 0.405f, -0.01f);
            topBar.transform.localScale = new Vector3(1.54f, 0.03f, 0.02f);
            Object.DestroyImmediate(topBar.GetComponent<Collider>());
            topBar.GetComponent<Renderer>().sharedMaterial = trimMat;

            // Canvas faces -Z so when NPCSpeechBubble rotates +Z away from Camera.main, front faces player.
            Canvas bubbleCanvas = CreateWorldCanvas("SpeechCanvas", visualRoot.transform,
                new Vector3(0f, 0f, -0.022f), Vector3.zero,
                new Vector2(1.50f, 0.78f), 1000, 520, mainCam, true);

            TMP_Text speakerTmp = CreateTMPText(bubbleCanvas.transform, "Speaker", "Dr. Vera", 34f,
                new Vector2(0f, 210f), new Vector2(900f, 48f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Left, FontStyles.Bold);

            TMP_Text bodyTmp = CreateTMPText(bubbleCanvas.transform, "SubtitleBody", "", 40f,
                new Vector2(0f, 28f), new Vector2(900f, 270f), new Color(0.95f, 0.98f, 1.0f), TextAlignmentOptions.TopLeft, FontStyles.Normal);
            bodyTmp.enableAutoSizing = true;
            bodyTmp.fontSizeMin = 34f;
            bodyTmp.fontSizeMax = 48f;

            Button nextBtn = CreateUIButton(bubbleCanvas.transform, "NextButton", "Next",
                new Vector2(355f, -190f), new Vector2(220f, 84f), new Color(0.14f, 0.62f, 0.90f, 1f), out TMP_Text nextLabel);
            nextLabel.fontSize = 30f;

            Button[] optBtns = new Button[3];
            TMP_Text[] optLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                optBtns[i] = CreateUIButton(bubbleCanvas.transform, "OptionButton_" + i, "Topic " + i,
                    new Vector2(-355f + (i * 234f), -190f), new Vector2(222f, 84f), new Color(0.19f, 0.27f, 0.36f, 1f), out optLabels[i]);
                optLabels[i].enableAutoSizing = true;
                optLabels[i].fontSizeMin = 18f;
                optLabels[i].fontSizeMax = 24f;
                optLabels[i].margin = new Vector4(8f, 4f, 8f, 4f);
            }

            XRSimpleInteractable bubbleSimple = bubbleRoot.AddComponent<XRSimpleInteractable>();
            bubbleSimple.colliders.Add(bubbleCol);

            NPCSpeechBubble bubbleComp = bubbleRoot.AddComponent<NPCSpeechBubble>();
            SerializedObject sbSo = new SerializedObject(bubbleComp);
            SetObjRef(sbSo, "dialogueSystem", dialogueSystem);
            SetObjRef(sbSo, "visualRoot", visualRoot);
            SetObjRef(sbSo, "speakerText", speakerTmp);
            SetObjRef(sbSo, "bodyText", bodyTmp);
            SetObjRef(sbSo, "nextButton", nextBtn);
            SetObjRef(sbSo, "nextButtonLabel", nextLabel);
            SetObjRef(sbSo, "bubbleInteractable", bubbleSimple);
            SetObjRef(sbSo, "audioSource", npcAudio);
            SetObjectArray(sbSo, "optionButtons", optBtns);
            SetObjectArray(sbSo, "optionLabels", optLabels);
            sbSo.ApplyModifiedPropertiesWithoutUndo();

            NPCController npcCtrl = npcRoot.AddComponent<NPCController>();
            SerializedObject npcSo = new SerializedObject(npcCtrl);
            SetStr(npcSo, "npcName", "Dr. Vera");
            SetObjRef(npcSo, "npcModelSlot", modelSlot.transform);
            SetObjRef(npcSo, "placeholderRig", placeholder);
            SetObjRef(npcSo, "pointerArmPivot", armPivot.transform);
            SetObjRef(npcSo, "navMeshAgent", agent);
            SetObjRef(npcSo, "dialogueSystem", dialogueSystem);
            SetObjRef(npcSo, "speechBubble", bubbleComp);
            SetObjRef(npcSo, "playerHead", mainCam != null ? mainCam.transform : null);
            npcSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAssetAndConnect(npcRoot, NpcPrefabPath, InteractionMode.AutomatedAction);
            return npcRoot;
        }

        private static void BuildLabTabletAndDock(
            LessonManager lessonManager,
            DialogueSystem dialogueSystem,
            LocomotionModeController locoController,
            NPCSpeechBubble speechBubble,
            GlossaryTopic[] topics,
            Camera mainCam)
        {
            Material tableTopMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_TableTop.mat");
            Material frameMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_TableFrame.mat");
            Material screenMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_ScreenPanel.mat");
            Material accentMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_AccentBase.mat");

            // Pedestal dock near Entrance Spawn (0, 0, -4.2) -> front-right at (0.35, 0, -3.25), clear of DebugPanel.
            GameObject dock = new GameObject("LabTabletStationDock");
            dock.transform.position = new Vector3(0.35f, 0f, -3.25f);
            dock.transform.rotation = Quaternion.Euler(0f, 20f, 0f);

            CreateBox("PedestalBase", dock.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.46f, 0.90f, 0.42f), frameMat, true);
            CreateBox("PedestalTop", dock.transform, new Vector3(0f, 0.91f, 0f), new Vector3(0.52f, 0.03f, 0.48f), tableTopMat, true);

            // Slanted dock backrest and front lip so tablet rests face-up (+36 deg) cleanly under gravity.
            GameObject cradleBack = CreateBox("DockBackrest", dock.transform, new Vector3(0f, 1.085f, 0.032f), new Vector3(0.46f, 0.28f, 0.025f), frameMat, true);
            cradleBack.transform.localRotation = Quaternion.Euler(36f, 0f, 0f);
            GameObject cradleLip = CreateBox("DockLip", dock.transform, new Vector3(0f, 0.949f, -0.132f), new Vector3(0.46f, 0.038f, 0.045f), frameMat, true);
            cradleLip.transform.localRotation = Quaternion.Euler(36f, 0f, 0f);

            GameObject tabletHome = new GameObject("TabletHomeMarker");
            tabletHome.transform.SetParent(dock.transform, false);
            tabletHome.transform.localPosition = new Vector3(0f, 1.11f, -0.015f);
            // +36 deg X tilts local -Z screen UPWARD toward player eyes instead of toward floor.
            tabletHome.transform.localRotation = Quaternion.Euler(36f, 0f, 0f);

            // Grabbable Lab Tablet (>= 5 cm all meaningful dims: 0.50 x 0.35 x 0.05 m).
            GameObject tabletRoot = new GameObject("LabTablet");
            tabletRoot.transform.SetPositionAndRotation(tabletHome.transform.position, tabletHome.transform.rotation);

            AudioSource tabletAudio = tabletRoot.AddComponent<AudioSource>();
            tabletAudio.spatialBlend = 0.85f;
            tabletAudio.playOnAwake = false;

            GameObject chassis = CreateBox("Chassis", tabletRoot.transform, Vector3.zero, new Vector3(0.50f, 0.35f, 0.05f), frameMat, false);
            BoxCollider grabCol = chassis.GetComponent<BoxCollider>();
            GameObject screenPlate = CreateBox("ScreenPlate", tabletRoot.transform, new Vector3(0f, 0f, -0.018f), new Vector3(0.47f, 0.32f, 0.02f), screenMat, false);
            Object.DestroyImmediate(screenPlate.GetComponent<Collider>());
            GameObject rimLed = CreateBox("RimAccent", tabletRoot.transform, new Vector3(0f, 0.17f, -0.02f), new Vector3(0.44f, 0.012f, 0.012f), accentMat, false);
            Object.DestroyImmediate(rimLed.GetComponent<Collider>());

            // Dedicated attach transform so picking up the tablet immediately faces its -Z screen upright toward the holder.
            GameObject grabAttach = new GameObject("GrabAttachPoint");
            grabAttach.transform.SetParent(tabletRoot.transform, false);
            grabAttach.transform.localPosition = new Vector3(0f, -0.04f, 0.025f);
            grabAttach.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);

            Rigidbody rb = tabletRoot.AddComponent<Rigidbody>();
            rb.mass = 0.6f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            XRGrabInteractable grab = tabletRoot.AddComponent<XRGrabInteractable>();
            grab.colliders.Add(grabCol);
            grab.attachTransform = grabAttach.transform;
            grab.useDynamicAttach = false;
            grab.matchAttachPosition = true;
            grab.matchAttachRotation = true;
            grab.attachEaseInTime = 0.12f;
            grab.throwOnDetach = true;

            InteractionFeedback fb = tabletRoot.AddComponent<InteractionFeedback>();
            SerializedObject fbSo = new SerializedObject(fb);
            SetObjRef(fbSo, "interactable", grab);
            SetObjRef(fbSo, "audioSource", tabletAudio);
            SetStr(fbSo, "tooltipLabel", "Lab Tablet (Grab + J/L Rotate)");
            SetObjectArray(fbSo, "highlightRenderers", new Object[] { rimLed.GetComponent<Renderer>() });
            fbSo.ApplyModifiedPropertiesWithoutUndo();

            RespawnableItem respawn = tabletRoot.AddComponent<RespawnableItem>();
            SerializedObject rpSo = new SerializedObject(respawn);
            SetObjRef(rpSo, "homeAnchor", tabletHome.transform);
            SetObjRef(rpSo, "audioSource", tabletAudio);
            rpSo.ApplyModifiedPropertiesWithoutUndo();

            // World-space Canvas on -Z face so camera looking +Z reads left-to-right.
            Canvas canvas = CreateWorldCanvas("TabletCanvas", tabletRoot.transform,
                new Vector3(0f, 0f, -0.032f), Vector3.zero,
                new Vector2(0.46f, 0.31f), 1000, 675, mainCam, true);

            // Header + ProgressHUD strip.
            TMP_Text headerStation = CreateTMPText(canvas.transform, "StationHeader", "T. Tutorial Pad", 31f,
                new Vector2(-225f, 296f), new Vector2(510f, 44f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Left, FontStyles.Bold);
            TMP_Text stepCounter = CreateTMPText(canvas.transform, "StepCounter", "PROGRESS: 0 / 6 STEPS", 26f,
                new Vector2(250f, 296f), new Vector2(440f, 42f), Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
            TMP_Text stationStrip = CreateTMPText(canvas.transform, "StationStrip", "[T] 1 2 3 4 5 6 A", 24f,
                new Vector2(0f, 254f), new Vector2(940f, 36f), Color.white, TextAlignmentOptions.Center, FontStyles.Normal);

            // 4 Tab Buttons.
            Button objTabBtn = CreateUIButton(canvas.transform, "Tab_Objective", "Objective", new Vector2(-351f, 200f), new Vector2(224f, 52f), new Color(0.16f, 0.42f, 0.62f, 1f), out TMP_Text objTabLbl);
            objTabLbl.fontSize = 25f;
            Button gloTabBtn = CreateUIButton(canvas.transform, "Tab_Glossary", "Glossary", new Vector2(-117f, 200f), new Vector2(224f, 52f), new Color(0.16f, 0.25f, 0.34f, 1f), out TMP_Text gloTabLbl);
            gloTabLbl.fontSize = 25f;
            Button ctlTabBtn = CreateUIButton(canvas.transform, "Tab_Controls", "Controls", new Vector2(117f, 200f), new Vector2(224f, 52f), new Color(0.16f, 0.25f, 0.34f, 1f), out TMP_Text ctlTabLbl);
            ctlTabLbl.fontSize = 25f;
            Button setTabBtn = CreateUIButton(canvas.transform, "Tab_Settings", "Settings", new Vector2(351f, 200f), new Vector2(224f, 52f), new Color(0.16f, 0.25f, 0.34f, 1f), out TMP_Text setTabLbl);
            setTabLbl.fontSize = 25f;

            List<TMP_Text> scalable = new List<TMP_Text>();

            // Objective Panel.
            GameObject objPanel = CreatePanel(canvas.transform, "ObjectivePanel");
            TMP_Text objText = CreateTMPText(objPanel.transform, "CurrentObjective", "Objective...", 30f,
                new Vector2(0f, 94f), new Vector2(920f, 116f), Color.white, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            TMP_Text stepList = CreateTMPText(objPanel.transform, "StepList", "", 26f,
                new Vector2(0f, -68f), new Vector2(920f, 210f), new Color(0.90f, 0.95f, 0.99f), TextAlignmentOptions.TopLeft, FontStyles.Normal);
            Button hintBtn = CreateUIButton(objPanel.transform, "HintButton", "Ask Hint", new Vector2(330f, -242f), new Vector2(260f, 64f), new Color(0.86f, 0.56f, 0.14f, 1f), out TMP_Text hintLbl);
            hintLbl.fontSize = 26f;
            scalable.Add(objText);
            scalable.Add(stepList);

            // Glossary Panel.
            GameObject gloPanel = CreatePanel(canvas.transform, "GlossaryPanel");
            Button[] topicBtns = new Button[6];
            TMP_Text[] topicLbls = new TMP_Text[6];
            for (int i = 0; i < 6; i++)
            {
                int row = i / 3;
                int col = i % 3;
                topicBtns[i] = CreateUIButton(gloPanel.transform, "TopicBtn_" + i, "Topic",
                    new Vector2(-308f + col * 308f, 122f - row * 64f), new Vector2(292f, 54f),
                    new Color(0.18f, 0.28f, 0.38f, 1f), out topicLbls[i]);
                topicLbls[i].enableAutoSizing = true;
                topicLbls[i].fontSizeMin = 18f;
                topicLbls[i].fontSizeMax = 24f;
                topicLbls[i].margin = new Vector4(8f, 4f, 8f, 4f);
            }
            TMP_Text gloDetail = CreateTMPText(gloPanel.transform, "GlossaryDetail", "", 28f,
                new Vector2(0f, -72f), new Vector2(920f, 195f), Color.white, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            Button askNpcBtn = CreateUIButton(gloPanel.transform, "AskNpcButton", "Ask Dr. Vera About This",
                new Vector2(0f, -242f), new Vector2(410f, 64f), new Color(0.14f, 0.62f, 0.88f, 1f), out TMP_Text askNpcLbl);
            askNpcLbl.fontSize = 25f;
            scalable.Add(gloDetail);

            // Controls Panel.
            GameObject ctlPanel = CreatePanel(canvas.transform, "ControlsPanel");
            TMP_Text ctlBody = CreateTMPText(ctlPanel.transform, "ControlsBody", "", 24f,
                new Vector2(0f, -24f), new Vector2(935f, 380f), Color.white, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            scalable.Add(ctlBody);

            // Settings Panel.
            GameObject setPanel = CreatePanel(canvas.transform, "SettingsPanel");
            gloPanel.SetActive(false);
            ctlPanel.SetActive(false);
            setPanel.SetActive(false);
            Button textBtn = CreateUIButton(setPanel.transform, "TextSizeButton", "Text Size: Medium",
                new Vector2(0f, 96f), new Vector2(680f, 68f), new Color(0.18f, 0.32f, 0.44f, 1f), out TMP_Text textLbl);
            textLbl.fontSize = 26f;
            Button locoBtn = CreateUIButton(setPanel.transform, "LocomotionButton", "Locomotion: Teleport (Default)",
                new Vector2(0f, 12f), new Vector2(680f, 68f), new Color(0.18f, 0.32f, 0.44f, 1f), out TMP_Text locoLbl);
            locoLbl.fontSize = 26f;
            Button postBtn = CreateUIButton(setPanel.transform, "PostureButton", "Posture: Standing",
                new Vector2(0f, -72f), new Vector2(680f, 68f), new Color(0.18f, 0.32f, 0.44f, 1f), out TMP_Text postLbl);
            postLbl.fontSize = 26f;
            Button turnBtn = CreateUIButton(setPanel.transform, "TurnModeButton", "Turn: Snap (Default)",
                new Vector2(0f, -156f), new Vector2(680f, 68f), new Color(0.18f, 0.32f, 0.44f, 1f), out TMP_Text turnLbl);
            turnLbl.fontSize = 26f;

            ProgressHUD progressHud = tabletRoot.AddComponent<ProgressHUD>();
            SerializedObject hudSo = new SerializedObject(progressHud);
            SetObjRef(hudSo, "lessonManager", lessonManager);
            SetObjRef(hudSo, "stationTitleText", headerStation);
            SetObjRef(hudSo, "objectiveText", objText);
            SetObjRef(hudSo, "stepCounterText", stepCounter);
            SetObjRef(hudSo, "stationStripText", stationStrip);
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            LabTablet tablet = tabletRoot.AddComponent<LabTablet>();
            SerializedObject tabSo = new SerializedObject(tablet);
            SetObjRef(tabSo, "lessonManager", lessonManager);
            SetObjRef(tabSo, "dialogueSystem", dialogueSystem);
            SetObjRef(tabSo, "locomotionController", locoController);
            SetObjRef(tabSo, "speechBubble", speechBubble);
            SetObjRef(tabSo, "progressHud", progressHud);
            SetObjRef(tabSo, "audioSource", tabletAudio);

            SetObjRef(tabSo, "objectiveTabButton", objTabBtn);
            SetObjRef(tabSo, "glossaryTabButton", gloTabBtn);
            SetObjRef(tabSo, "controlsTabButton", ctlTabBtn);
            SetObjRef(tabSo, "settingsTabButton", setTabBtn);
            SetObjRef(tabSo, "objectivePanel", objPanel);
            SetObjRef(tabSo, "glossaryPanel", gloPanel);
            SetObjRef(tabSo, "controlsPanel", ctlPanel);
            SetObjRef(tabSo, "settingsPanel", setPanel);

            SetObjRef(tabSo, "stepListText", stepList);
            SetObjRef(tabSo, "hintButton", hintBtn);

            SetObjectArray(tabSo, "glossaryTopics", topics);
            SetObjectArray(tabSo, "glossaryButtons", topicBtns);
            SetObjectArray(tabSo, "glossaryButtonLabels", topicLbls);
            SetObjRef(tabSo, "glossaryDetailText", gloDetail);
            SetObjRef(tabSo, "askNpcTopicButton", askNpcBtn);

            SetObjRef(tabSo, "controlsBodyText", ctlBody);
            SetObjRef(tabSo, "textSizeButton", textBtn);
            SetObjRef(tabSo, "textSizeLabel", textLbl);
            SetObjRef(tabSo, "locomotionToggleButton", locoBtn);
            SetObjRef(tabSo, "locomotionLabel", locoLbl);
            SetObjRef(tabSo, "postureToggleButton", postBtn);
            SetObjRef(tabSo, "postureLabel", postLbl);
            SetObjRef(tabSo, "snapTurnToggleButton", turnBtn);
            SetObjRef(tabSo, "snapTurnLabel", turnLbl);
            SetObjectArray(tabSo, "scalableTexts", scalable.ToArray());
            tabSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAssetAndConnect(tabletRoot, TabletPrefabPath, InteractionMode.AutomatedAction);
        }

        private static void BuildDebugPanel(LessonManager lessonManager, DialogueSystem dialogueSystem, Camera mainCam)
        {
            Material frameMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_TableFrame.mat");
            Material screenMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_ScreenPanel.mat");

            GameObject stand = new GameObject("DebugPanelStand");
            stand.transform.position = new Vector3(2.05f, 0f, -2.40f);
            stand.transform.rotation = Quaternion.Euler(0f, 36f, 0f);

            CreateBox("Post", stand.transform, new Vector3(0f, 0.70f, 0.05f), new Vector3(0.08f, 1.40f, 0.08f), frameMat, true);
            CreateBox("Base", stand.transform, new Vector3(0f, 0.02f, 0.05f), new Vector3(0.45f, 0.04f, 0.40f), frameMat, true);

            GameObject panelRoot = new GameObject("DebugPanel");
            panelRoot.transform.SetParent(stand.transform, false);
            panelRoot.transform.localPosition = new Vector3(0f, 1.42f, 0f);

            CreateBox("Frame", panelRoot.transform, Vector3.zero, new Vector3(1.28f, 0.80f, 0.04f), frameMat, false);
            CreateBox("Back", panelRoot.transform, new Vector3(0f, 0f, -0.015f), new Vector3(1.22f, 0.74f, 0.02f), screenMat, false);

            Canvas canvas = CreateWorldCanvas("DebugCanvas", panelRoot.transform,
                new Vector3(0f, 0f, -0.032f), Vector3.zero,
                new Vector2(1.20f, 0.72f), 1050, 630, mainCam, true);

            CreateTMPText(canvas.transform, "Title", "DEV / EDITOR DEBUG PANEL", 34f,
                new Vector2(0f, 276f), new Vector2(980f, 48f), new Color(1.0f, 0.72f, 0.25f), TextAlignmentOptions.Center, FontStyles.Bold);

            string[] ids = { "T", "1", "2", "3", "4", "5", "6", "A" };
            Button[] jumpBtns = new Button[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                jumpBtns[i] = CreateUIButton(canvas.transform, "Jump_" + ids[i], "Go " + ids[i],
                    new Vector2(-428f + i * 122f, 212f), new Vector2(112f, 54f),
                    new Color(0.16f, 0.34f, 0.50f, 1f), out TMP_Text jLbl);
                jLbl.fontSize = 25f;
            }

            Button nextStepBtn = CreateUIButton(canvas.transform, "NextStep", "Skip Step", new Vector2(-320f, 138f), new Vector2(285f, 56f), new Color(0.20f, 0.52f, 0.32f, 1f), out TMP_Text nsLbl);
            nsLbl.fontSize = 26f;
            Button skipDlgBtn = CreateUIButton(canvas.transform, "SkipDialogue", "Skip Dialogue", new Vector2(0f, 138f), new Vector2(285f, 56f), new Color(0.24f, 0.42f, 0.62f, 1f), out TMP_Text sdLbl);
            sdLbl.fontSize = 26f;
            Button resetStBtn = CreateUIButton(canvas.transform, "ResetStation", "Reset Station", new Vector2(320f, 138f), new Vector2(285f, 56f), new Color(0.58f, 0.26f, 0.24f, 1f), out TMP_Text rsLbl);
            rsLbl.fontSize = 26f;

            TMP_Text timeLbl = CreateTMPText(canvas.transform, "TimeScaleLabel", "Time Scale: 1.00x", 25f,
                new Vector2(-306f, 64f), new Vector2(340f, 44f), Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            Slider timeSlider = CreateUISlider(canvas.transform, "TimeScaleSlider", new Vector2(65f, 64f), new Vector2(385f, 38f));
            timeSlider.minValue = 0.25f;
            timeSlider.maxValue = 3f;
            timeSlider.value = 1f;
            Button resetTimeBtn = CreateUIButton(canvas.transform, "ResetTime", "1.0x", new Vector2(372f, 64f), new Vector2(130f, 48f), new Color(0.26f, 0.32f, 0.40f, 1f), out TMP_Text rtLbl);
            rtLbl.fontSize = 25f;

            TMP_Text readout = CreateTMPText(canvas.transform, "ModelReadout", "Telemetry...", 25f,
                new Vector2(0f, -132f), new Vector2(980f, 305f), new Color(0.91f, 0.97f, 1.0f), TextAlignmentOptions.TopLeft, FontStyles.Normal);

            DebugPanel dbg = panelRoot.AddComponent<DebugPanel>();
            SerializedObject dbgSo = new SerializedObject(dbg);
            SetObjRef(dbgSo, "lessonManager", lessonManager);
            SetObjRef(dbgSo, "dialogueSystem", dialogueSystem);
            SetObjectArray(dbgSo, "jumpStationButtons", jumpBtns);
            SetObjRef(dbgSo, "nextStepButton", nextStepBtn);
            SetObjRef(dbgSo, "skipDialogueButton", skipDlgBtn);
            SetObjRef(dbgSo, "resetStationButton", resetStBtn);
            SetObjRef(dbgSo, "timeScaleSlider", timeSlider);
            SetObjRef(dbgSo, "resetTimeScaleButton", resetTimeBtn);
            SetObjRef(dbgSo, "timeScaleText", timeLbl);
            SetObjRef(dbgSo, "modelReadoutText", readout);
            dbgSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAssetAndConnect(panelRoot, DebugPrefabPath, InteractionMode.AutomatedAction);
        }

        private static Material GetOrCreateLitMat(string path, Color color, float smoothness)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Canvas CreateWorldCanvas(
            string name,
            Transform parent,
            Vector3 localPos,
            Vector3 localEuler,
            Vector2 worldSize,
            int pxWidth,
            int pxHeight,
            Camera eventCamera,
            bool interactive)
        {
            return LabTextStyleHelper.CreateWorldCanvas(
                name, parent, localPos, localEuler, worldSize, pxWidth, pxHeight, eventCamera, interactive);
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        private static TMP_Text CreateTMPText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Color color,
            TextAlignmentOptions alignment,
            FontStyles style)
        {
            return LabTextStyleHelper.CreateTMPText(
                parent, name, text, fontSize, anchoredPos, sizeDelta, color, alignment, style, false);
        }

        private static Button CreateUIButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Color bgColor,
            out TMP_Text labelTmp)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Image img = go.AddComponent<Image>();
            img.color = bgColor;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            btn.colors = cb;

            labelTmp = CreateTMPText(go.transform, "Label", label, 28f,
                Vector2.zero, sizeDelta - new Vector2(14f, 8f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            return btn;
        }

        private static Slider CreateUISlider(Transform parent, string name, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            RectTransform bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.25f);
            bgRt.anchorMax = new Vector2(1f, 0.75f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.18f, 0.22f, 0.28f, 1f);

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            RectTransform faRt = fillArea.AddComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.25f);
            faRt.anchorMax = new Vector2(1f, 0.75f);
            faRt.offsetMin = Vector2.zero;
            faRt.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRt = fill.AddComponent<RectTransform>();
            fillRt.sizeDelta = Vector2.zero;
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.20f, 0.72f, 0.96f, 1f);

            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            RectTransform haRt = handleArea.AddComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(10f, 0f);
            haRt.offsetMax = new Vector2(-10f, 0f);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform hRt = handle.AddComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(24f, 0f);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;

            Slider slider = go.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = hRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool isStatic)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.isStatic = isStatic;
            if (mat != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
            return go;
        }

        private static Camera FindMainCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;
            return Object.FindAnyObjectByType<Camera>();
        }

        private static void DestroyIfExists(string objectName)
        {
            GameObject existing;
            while ((existing = GameObject.Find(objectName)) != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        private static void SetObjRef(SerializedObject so, string propName, Object value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetStr(SerializedObject so, string propName, string value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null) p.stringValue = value;
        }

        private static void SetFloat(SerializedObject so, string propName, float value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null) p.floatValue = value;
        }

        private static void SetObjectArray<T>(SerializedObject so, string propName, T[] items) where T : Object
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop == null) return;
            prop.arraySize = items != null ? items.Length : 0;
            if (items == null) return;
            for (int i = 0; i < items.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }

        private static void SetObjectList<T>(SerializedObject so, string propName, List<T> items) where T : Object
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop == null) return;
            prop.arraySize = items != null ? items.Count : 0;
            if (items == null) return;
            for (int i = 0; i < items.Count; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }
    }
}
