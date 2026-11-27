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
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Builds Phase 4:
    /// 1. NPC Station Escort (TeleportationAnchor -> NPCStandPoint via NavMesh) and decoupled Click-to-Guide (Stations 1, 2, 3).
    /// 2. Station 2: N-Type Doping (Workbench 2, Group V vs III/IV dopant shelf, Diffusion Furnace, Lattice & Ed/EF Band Diagram, ND slider, N-Type output block).
    /// 3. Station 3: P-Type Doping (Workbench 3, Group III vs V/IV dopant shelf, Diffusion Furnace, Hole-Hopping Lattice & Ea/EF Band Diagram, NA slider, P-Type output block).
    /// 4. Phase 4 DialogueLine & LessonStep ScriptableObjects + NavMesh rebake + World-Space Canvas crisp audit.
    /// </summary>
    public static class Phase4DopingStationsBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string PrefabsFolder = "Assets/PNJunctionLab/Prefabs";
        private const string MaterialsFolder = "Assets/PNJunctionLab/Materials";
        private const string DataRoot = "Assets/PNJunctionLab/Data";
        private const string DialogueFolder = DataRoot + "/Dialogue";
        private const string LessonFolder = DataRoot + "/Lessons";
        private const string Station2PrefabPath = PrefabsFolder + "/NTypeDopingStation.prefab";
        private const string Station3PrefabPath = PrefabsFolder + "/PTypeDopingStation.prefab";
        private const string NpcPrefabPath = PrefabsFolder + "/NPC_Scientist.prefab";

        private struct VialSpec
        {
            public string Symbol;
            public string FullName;
            public int AtomicNumber;
            public DopantValenceGroup Group;
            public bool BackRow;
            public int ColumnIndex;
        }

        private struct Phase4MatSet
        {
            public Material TableTop;
            public Material TableFrame;
            public Material ScreenPanel;
            public Material AccentBase;
            public Material ButtonGreen;
            public Material ButtonAmber;
            public Material ButtonBlue;
            public Material ButtonRed;
            public Material WaferSi;
            public Material ChamberGlass;
            public Material AtomCore;
            public Material BondIntact;
            public Material BondBroken;
            public Material ElectronBlue;
            public Material HoleRed;
            public Material DonorOrange;
            public Material AcceptorTeal;
            public Material NeutralSlate;
            public Material BlockNType;
            public Material BlockPType;
        }

        [MenuItem("PNJunctionLab/Build Phase 4 (NPC Escort + Stations 2 & 3 Doping)")]
        public static void BuildPhase4Menu()
        {
            BuildPhase4();
        }

        public static string BuildPhase4()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                return "Failed to open scene at " + ScenePath;
            }

            LabTextStyleHelper.EnsureNativeGameViewDPI();
            Phase4MatSet mats = CreateOrLoadMaterials();
            Camera mainCam = FindMainCamera();

            GameObject stationsRoot = GameObject.Find("[Stations]");
            if (stationsRoot == null)
            {
                stationsRoot = new GameObject("[Stations]");
            }

            // Build Station 2 (N-Type Doping) replacing placeholder while preserving world placement.
            DopingStationController st2Ctrl = BuildOrReplaceDopingStation(
                stationsRoot.transform,
                "Station_2_NType",
                "2",
                "N-Type Doping",
                ExtrinsicDopingKind.NType,
                new Vector3(-5.6f, 0f, 2.8f),
                Quaternion.Euler(0f, 90f, 0f),
                new Vector3(1.36f, 0f, 1.05f),
                Station2PrefabPath,
                mats,
                mainCam);

            // Build Station 3 (P-Type Doping) replacing placeholder while preserving world placement.
            DopingStationController st3Ctrl = BuildOrReplaceDopingStation(
                stationsRoot.transform,
                "Station_3_PType",
                "3",
                "P-Type Doping",
                ExtrinsicDopingKind.PType,
                new Vector3(-2.2f, 0f, 4.0f),
                Quaternion.Euler(0f, 180f, 0f),
                new Vector3(-1.36f, 0f, 1.05f),
                Station3PrefabPath,
                mats,
                mainCam);

            // Ensure NPCScientist has decoupled Click/Ray interaction target + Station 1..3 guides + teleport escort bindings.
            NPCController npcCtrl = UpgradeNpcEscortAndClickTarget(mats, mainCam);

            // Wire Phase 4 DialogueLines, LessonSteps (10..15 + Step 16 Stage 4 stub), and Station Guides.
            WirePhase4LessonsAndGuides(st2Ctrl, st3Ctrl, npcCtrl);

            // Re-bake NavMeshData against updated benches and validate all stand points.
            string navReport = RebuildNavMeshAndValidateStandPoints();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return saved
                ? $"Phase 4 built & saved cleanly. {navReport}"
                : "Failed to save scene after Phase 4 build.";
        }

        private static Phase4MatSet CreateOrLoadMaterials()
        {
            return new Phase4MatSet
            {
                TableTop = LoadOrCreateLit(MaterialsFolder + "/Mat_TableTop.mat", new Color(0.88f, 0.90f, 0.94f, 1f), 0.05f, 0.35f, Color.black, false),
                TableFrame = LoadOrCreateLit(MaterialsFolder + "/Mat_TableFrame.mat", new Color(0.14f, 0.17f, 0.22f, 1f), 0.35f, 0.45f, Color.black, false),
                ScreenPanel = LoadOrCreateLit(MaterialsFolder + "/Mat_ScreenPanel.mat", new Color(0.08f, 0.11f, 0.16f, 1f), 0.1f, 0.3f, Color.black, false),
                AccentBase = LoadOrCreateLit(MaterialsFolder + "/Mat_AccentBase.mat", new Color(0.18f, 0.78f, 0.95f, 1f), 0.1f, 0.5f, new Color(0.06f, 0.30f, 0.42f), false),
                ButtonGreen = LoadOrCreateLit(MaterialsFolder + "/Mat_ButtonGreen.mat", new Color(0.16f, 0.78f, 0.38f, 1f), 0.1f, 0.4f, new Color(0.03f, 0.18f, 0.07f), false),
                ButtonAmber = LoadOrCreateLit(MaterialsFolder + "/Mat_ButtonAmber.mat", new Color(0.92f, 0.58f, 0.16f, 1f), 0.1f, 0.4f, new Color(0.22f, 0.12f, 0.02f), false),
                ButtonBlue = LoadOrCreateLit(MaterialsFolder + "/Mat_ButtonBlue.mat", new Color(0.16f, 0.58f, 0.92f, 1f), 0.1f, 0.45f, new Color(0.03f, 0.16f, 0.28f), false),
                ButtonRed = LoadOrCreateLit(MaterialsFolder + "/Mat_ButtonRed.mat", new Color(0.86f, 0.22f, 0.24f, 1f), 0.05f, 0.42f, new Color(0.24f, 0.04f, 0.04f), false),
                WaferSi = LoadOrCreateLit(MaterialsFolder + "/Mat_WaferSi.mat", new Color(0.24f, 0.30f, 0.39f, 1f), 0.65f, 0.72f, new Color(0.02f, 0.05f, 0.08f), false),
                ChamberGlass = LoadOrCreateLit(MaterialsFolder + "/Mat_ChamberGlass.mat", new Color(0.55f, 0.85f, 1.0f, 0.16f), 0.0f, 0.92f, new Color(0.02f, 0.08f, 0.14f), true),
                AtomCore = LoadOrCreateLit(MaterialsFolder + "/Mat_LatticeAtom.mat", new Color(0.84f, 0.90f, 0.96f, 1f), 0.25f, 0.60f, new Color(0.12f, 0.18f, 0.26f), false),
                BondIntact = LoadOrCreateLit(MaterialsFolder + "/Mat_BondIntact.mat", new Color(0.28f, 0.76f, 0.92f, 0.85f), 0.10f, 0.45f, new Color(0.06f, 0.28f, 0.38f), true),
                BondBroken = LoadOrCreateLit(MaterialsFolder + "/Mat_BondBroken.mat", new Color(0.95f, 0.38f, 0.24f, 0.65f), 0.0f, 0.35f, new Color(0.45f, 0.10f, 0.06f), true),
                ElectronBlue = LoadOrCreateLit(MaterialsFolder + "/Mat_ElectronBlue.mat", new Color(0.18f, 0.62f, 1.0f, 1f), 0.1f, 0.6f, new Color(0.18f, 0.50f, 0.98f), false),
                HoleRed = LoadOrCreateLit(MaterialsFolder + "/Mat_HoleRed.mat", new Color(1.0f, 0.30f, 0.34f, 1f), 0.1f, 0.6f, new Color(0.75f, 0.14f, 0.18f), false),
                DonorOrange = LoadOrCreateLit(MaterialsFolder + "/Mat_DonorIonOrange.mat", new Color(0.98f, 0.55f, 0.14f, 1f), 0.15f, 0.65f, new Color(0.55f, 0.24f, 0.02f), false),
                AcceptorTeal = LoadOrCreateLit(MaterialsFolder + "/Mat_AcceptorIonTeal.mat", new Color(0.12f, 0.80f, 0.78f, 1f), 0.15f, 0.65f, new Color(0.04f, 0.38f, 0.38f), false),
                NeutralSlate = LoadOrCreateLit(MaterialsFolder + "/Mat_NeutralGroupIV.mat", new Color(0.46f, 0.52f, 0.62f, 1f), 0.25f, 0.45f, Color.black, false),
                BlockNType = LoadOrCreateLit(MaterialsFolder + "/Mat_BlockNType.mat", new Color(0.16f, 0.54f, 0.94f, 1f), 0.2f, 0.55f, new Color(0.06f, 0.22f, 0.42f), false),
                BlockPType = LoadOrCreateLit(MaterialsFolder + "/Mat_BlockPType.mat", new Color(0.92f, 0.28f, 0.30f, 1f), 0.2f, 0.55f, new Color(0.38f, 0.08f, 0.10f), false)
            };
        }

        private static Material LoadOrCreateLit(string path, Color baseCol, float metallic, float smooth, Color emission, bool transparent)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                return mat;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", baseCol);
            mat.SetColor("_Color", baseCol);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smooth);
            if (emission != Color.black)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }

            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static DopingStationController BuildOrReplaceDopingStation(
            Transform stationsParent,
            string sceneObjName,
            string stationId,
            string stationTitle,
            ExtrinsicDopingKind mode,
            Vector3 defaultPos,
            Quaternion defaultRot,
            Vector3 standPointLocalPos,
            string prefabPath,
            Phase4MatSet mats,
            Camera mainCam)
        {
            GameObject existing = GameObject.Find(sceneObjName);
            Vector3 worldPos = defaultPos;
            Quaternion worldRot = defaultRot;
            if (existing != null)
            {
                worldPos = existing.transform.position;
                worldRot = existing.transform.rotation;
                Object.DestroyImmediate(existing);
            }

            GameObject root = new GameObject(sceneObjName);
            AudioSource stationAudio = root.AddComponent<AudioSource>();
            stationAudio.spatialBlend = 1f;
            stationAudio.playOnAwake = false;

            // Workbench (2.48 m wide x 0.94 m deep x 0.90 m high) gives spacious tabletop separation.
            BuildWorkbench(root.transform, 2.48f, 0.94f, 0.90f, mats);

            // Teleport Anchor + Hologram Title + Floor Pad.
            StationBeacon beacon = BuildBeaconParts(root.transform, stationId, stationTitle, mats, mainCam, out TeleportationAnchor anchor);

            // Dedicated NPC Stand Point beside workbench.
            GameObject npcStand = new GameObject("NPCStandPoint_" + stationId);
            npcStand.transform.SetParent(root.transform, false);
            npcStand.transform.localPosition = standPointLocalPos;
            npcStand.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Back Left Panel: Periodic Group Chart + Live SemiconductorModel Telemetry Board.
            BuildTelemetryBoard(
                root.transform, mode, mats, mainCam,
                out TMP_Text statusHeaderText,
                out TMP_Text concentrationReadoutText,
                out TMP_Text carrierReadoutText,
                out TMP_Text fermiNeutralityText,
                out TMP_Text conductivityReadoutText);

            // Back Right Panel: Live Extrinsic Energy Band Diagram (Ec, Ev, Ed/Ea, EF shift).
            ExtrinsicBandDiagramView bandView = BuildExtrinsicBandDiagram(root.transform, mode, mats, mainCam);

            // Tabletop Left: Chemical Dopant Shelf (8 Vials) + Grabbable Silicon Wafer + Return Props Button.
            GameObject shelfRoot = BuildDopantShelfAndWafer(
                root.transform, mode, mats, stationAudio, mainCam,
                out DopantVialItem[] shelfVials,
                out RespawnableItem waferRespawn,
                out List<RespawnableItem> allRespawnItems);

            PhysicalPushButton returnPropsBtn = BuildLabeledButton(
                root.transform, "ReturnPropsButton", new Vector3(1.08f, 0.90f, 0.34f),
                new Vector3(0.085f, 0.036f, 0.075f), mats.ButtonAmber,
                "RETURN PROPS", "Return Wafer & Vials to Shelf", mats, stationAudio, mainCam, out _, 0.19f);

            PhysicalPushButton resetBtn = BuildLabeledButton(
                root.transform, "ResetStationButton", new Vector3(-1.08f, 0.90f, 0.34f),
                new Vector3(0.085f, 0.036f, 0.075f), mats.ButtonRed,
                "RESET STATION", "Reset Doping Station", mats, stationAudio, mainCam, out _, 0.19f);

            // Tabletop Center: Diffusion Furnace (Wafer Socket + Dopant Socket) + 3D Extrinsic Lattice Visualizer.
            BuildDiffusionFurnace(
                root.transform, mode, mats, stationAudio, mainCam,
                out XRSocketInteractor waferSocket,
                out SocketRingFeedback waferFeedback,
                out XRSocketInteractor dopantSocket,
                out SocketRingFeedback dopantFeedback,
                out Renderer furnaceCoreRend);

            ExtrinsicLatticeVisualizer latticeVis = BuildExtrinsicLatticeStage(
                root.transform, mode, mats, stationAudio, mainCam);

            // Tabletop Right: Doping Concentration Slider Deck (10^14 - 10^18 cm^-3) + -/+ Decade Step Buttons.
            GameObject sliderDeck = BuildConcentrationDeck(
                root.transform, mode, mats, stationAudio, mainCam,
                out Slider dopingSlider,
                out PhysicalPushButton concDownBtn,
                out PhysicalPushButton concUpBtn);

            // Tabletop Far-Right: Output Block Fabrication Cradle + Grabbable ExtrinsicSemiconductorBlock.
            ExtrinsicSemiconductorBlock outBlock = BuildOutputBlockCradle(
                root.transform, mode, mats, stationAudio, mainCam,
                out TMP_Text outputTrayText,
                out RespawnableItem outBlockRespawn);
            allRespawnItems.Add(outBlockRespawn);

            // RespawnZone catches any dropped lab items below bench or offbounds.
            RespawnZone respawnZone = BuildStationRespawnZone(root.transform, allRespawnItems.ToArray());

            // Locked Dim Overlay shown cleanly while previous station is incomplete.
            GameObject lockedOverlay = BuildLockedOverlay(root.transform, stationTitle, mats, mainCam);

            // Wire DopingStationController component.
            DopingStationController ctrl = root.AddComponent<DopingStationController>();
            SerializedObject so = new SerializedObject(ctrl);
            LabTextStyleHelper.SetStr(so, "stationId", stationId);
            LabTextStyleHelper.SetStr(so, "stationTitle", stationTitle);
            LabTextStyleHelper.SetObjRef(so, "stationBeacon", beacon);
            LabTextStyleHelper.SetObjRef(so, "npcStandPoint", npcStand.transform);
            LabTextStyleHelper.SetObjRef(so, "lockedOverlay", lockedOverlay);
            LabTextStyleHelper.SetObjectArray(so, "hintTargets", new[]
            {
                anchor.transform,
                shelfRoot.transform,
                sliderDeck.transform
            });

            SerializedProperty modeProp = so.FindProperty("stationMode");
            if (modeProp != null) modeProp.intValue = (int)mode;
            LabTextStyleHelper.SetObjRef(so, "stationAnchor", anchor);
            LabTextStyleHelper.SetObjRef(so, "waferSocket", waferSocket);
            LabTextStyleHelper.SetObjRef(so, "waferSocketFeedback", waferFeedback);
            LabTextStyleHelper.SetObjRef(so, "dopantSocket", dopantSocket);
            LabTextStyleHelper.SetObjRef(so, "dopantSocketFeedback", dopantFeedback);
            LabTextStyleHelper.SetObjectArray(so, "shelfVials", shelfVials);
            LabTextStyleHelper.SetObjRef(so, "waferRespawnItem", waferRespawn);
            LabTextStyleHelper.SetObjRef(so, "furnaceCoreRenderer", furnaceCoreRend);
            LabTextStyleHelper.SetObjRef(so, "respawnZone", respawnZone);
            LabTextStyleHelper.SetObjRef(so, "audioSource", stationAudio);
            LabTextStyleHelper.SetObjRef(so, "dopingExponentSlider", dopingSlider);
            LabTextStyleHelper.SetObjRef(so, "concentrationDownButton", concDownBtn);
            LabTextStyleHelper.SetObjRef(so, "concentrationUpButton", concUpBtn);
            LabTextStyleHelper.SetObjRef(so, "resetStationButton", resetBtn);
            LabTextStyleHelper.SetObjRef(so, "returnPropsButton", returnPropsBtn);
            LabTextStyleHelper.SetObjRef(so, "latticeVisualizer", latticeVis);
            LabTextStyleHelper.SetObjRef(so, "bandDiagramView", bandView);
            LabTextStyleHelper.SetObjRef(so, "outputBlock", outBlock);
            LabTextStyleHelper.SetObjRef(so, "statusHeaderText", statusHeaderText);
            LabTextStyleHelper.SetObjRef(so, "concentrationReadoutText", concentrationReadoutText);
            LabTextStyleHelper.SetObjRef(so, "carrierReadoutText", carrierReadoutText);
            LabTextStyleHelper.SetObjRef(so, "fermiNeutralityText", fermiNeutralityText);
            LabTextStyleHelper.SetObjRef(so, "conductivityReadoutText", conductivityReadoutText);
            LabTextStyleHelper.SetObjRef(so, "outputTrayText", outputTrayText);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
            root.transform.SetParent(stationsParent, false);
            root.transform.SetPositionAndRotation(worldPos, worldRot);
            return ctrl;
        }

        private static void BuildTelemetryBoard(
            Transform parent,
            ExtrinsicDopingKind mode,
            Phase4MatSet mats,
            Camera mainCam,
            out TMP_Text statusHeaderText,
            out TMP_Text concentrationReadoutText,
            out TMP_Text carrierReadoutText,
            out TMP_Text fermiNeutralityText,
            out TMP_Text conductivityReadoutText)
        {
            GameObject panel = new GameObject("DopingTelemetryBoard");
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = new Vector3(0.56f, 1.72f, -0.38f);

            CreateBox("PostL", parent, new Vector3(0.94f, 1.15f, -0.39f), new Vector3(0.04f, 0.50f, 0.04f), mats.TableFrame, true);
            CreateBox("PostR", parent, new Vector3(0.18f, 1.15f, -0.39f), new Vector3(0.04f, 0.50f, 0.04f), mats.TableFrame, true);
            CreateBox("Frame", panel.transform, Vector3.zero, new Vector3(1.06f, 0.64f, 0.036f), mats.TableFrame, true);
            GameObject screen = CreateBox("Backing", panel.transform, new Vector3(0f, 0f, 0.014f), new Vector3(1.00f, 0.58f, 0.018f), mats.ScreenPanel, false);
            Object.DestroyImmediate(screen.GetComponent<Collider>());

            Canvas canvas = LabTextStyleHelper.CreateWorldCanvas("TelemetryCanvas", panel.transform,
                new Vector3(0f, 0f, 0.028f), new Vector3(0f, 180f, 0f),
                new Vector2(1.00f, 0.58f), 1000, 580, mainCam, false);

            bool isN = mode == ExtrinsicDopingKind.NType;
            Color accent = isN ? new Color(0.28f, 0.90f, 1.0f) : new Color(1.0f, 0.55f, 0.50f);

            LabTextStyleHelper.CreateTMPText(canvas.transform, "BoardTitle",
                isN ? "N-TYPE DOPING — GROUP V DONOR GUIDE" : "P-TYPE DOPING — GROUP III ACCEPTOR GUIDE",
                26f, new Vector2(0f, 250f), new Vector2(950f, 38f), accent, TextAlignmentOptions.Center, FontStyles.Bold);

            string periodicLine = isN
                ? "Grp III: B, Al, Ga (3e-) | Grp IV: C, Si, Ge (4e-) | [GRP V DONORS: P, As, Sb (5e-)]"
                : "[GRP III ACCEPTORS: B, Al, Ga, In (3e-)] | Grp IV: C, Si, Ge (4e-) | Grp V: P, As (5e-)";

            LabTextStyleHelper.CreateTMPText(canvas.transform, "PeriodicHighlight", periodicLine, 18.5f,
                new Vector2(0f, 206f), new Vector2(960f, 36f), new Color(1.0f, 0.88f, 0.42f), TextAlignmentOptions.Center, FontStyles.Bold);

            CreateUIImageRect(canvas.transform, "Divider", new Vector2(0f, 174f), new Vector2(920f, 3f), new Color(0.24f, 0.72f, 0.92f, 0.75f));

            statusHeaderText = LabTextStyleHelper.CreateTMPText(canvas.transform, "FurnaceStatus",
                "DIFFUSION FURNACE STANDBY", 22f,
                new Vector2(0f, 128f), new Vector2(940f, 64f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            concentrationReadoutText = LabTextStyleHelper.CreateTMPText(canvas.transform, "ConcReadout",
                "Doping: 1.00E+16 cm^-3", 23f,
                new Vector2(0f, 52f), new Vector2(940f, 44f), new Color(0.50f, 0.96f, 0.72f), TextAlignmentOptions.Center, FontStyles.Bold);

            carrierReadoutText = LabTextStyleHelper.CreateTMPText(canvas.transform, "CarrierReadout",
                "Majority & Minority Carriers", 21f,
                new Vector2(0f, -26f), new Vector2(940f, 76f), new Color(0.84f, 0.94f, 1.0f), TextAlignmentOptions.Center, FontStyles.Normal);

            fermiNeutralityText = LabTextStyleHelper.CreateTMPText(canvas.transform, "FermiNeutrality",
                "EF Shift & Charge Neutrality", 20f,
                new Vector2(0f, -124f), new Vector2(940f, 76f), new Color(1.0f, 0.85f, 0.45f), TextAlignmentOptions.Center, FontStyles.Normal);

            conductivityReadoutText = LabTextStyleHelper.CreateTMPText(canvas.transform, "ConductivityReadout",
                "Conductivity (sigma): pending", 20f,
                new Vector2(0f, -214f), new Vector2(940f, 52f), new Color(0.45f, 0.95f, 0.65f), TextAlignmentOptions.Center, FontStyles.Bold);
        }

        private static ExtrinsicBandDiagramView BuildExtrinsicBandDiagram(
            Transform parent,
            ExtrinsicDopingKind mode,
            Phase4MatSet mats,
            Camera mainCam)
        {
            GameObject panel = new GameObject("ExtrinsicBandDiagramPanel");
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = new Vector3(-0.56f, 1.72f, -0.38f);

            CreateBox("PostL", parent, new Vector3(-0.18f, 1.15f, -0.39f), new Vector3(0.04f, 0.50f, 0.04f), mats.TableFrame, true);
            CreateBox("PostR", parent, new Vector3(-0.94f, 1.15f, -0.39f), new Vector3(0.04f, 0.50f, 0.04f), mats.TableFrame, true);
            CreateBox("Frame", panel.transform, Vector3.zero, new Vector3(1.06f, 0.64f, 0.036f), mats.TableFrame, true);
            GameObject screen = CreateBox("Backing", panel.transform, new Vector3(0f, 0f, 0.014f), new Vector3(1.00f, 0.58f, 0.018f), mats.ScreenPanel, false);
            Object.DestroyImmediate(screen.GetComponent<Collider>());

            Canvas canvas = LabTextStyleHelper.CreateWorldCanvas("BandDiagramCanvas", panel.transform,
                new Vector3(0f, 0f, 0.028f), new Vector3(0f, 180f, 0f),
                new Vector2(1.00f, 0.58f), 1000, 580, mainCam, false);

            TMP_Text titleLbl = LabTextStyleHelper.CreateTMPText(canvas.transform, "Title",
                mode == ExtrinsicDopingKind.NType ? "ENERGY BAND DIAGRAM — N-TYPE (Not to scale)" : "ENERGY BAND DIAGRAM — P-TYPE (Not to scale)",
                24f, new Vector2(0f, 246f), new Vector2(950f, 40f),
                new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject plotArea = new GameObject("PlotArea");
            plotArea.transform.SetParent(canvas.transform, false);
            RectTransform plotRt = plotArea.AddComponent<RectTransform>();
            plotRt.anchoredPosition = new Vector2(0f, -4f);
            plotRt.sizeDelta = new Vector2(880f, 320f);

            RectTransform ecRect = CreateUIImageRect(plotArea.transform, "ConductionBand", new Vector2(0f, 110f), new Vector2(820f, 68f), new Color(0.11f, 0.34f, 0.56f, 0.94f));
            RectTransform evRect = CreateUIImageRect(plotArea.transform, "ValenceBand", new Vector2(0f, -110f), new Vector2(820f, 68f), new Color(0.46f, 0.14f, 0.18f, 0.94f));
            RectTransform eiLine = CreateUIImageRect(plotArea.transform, "IntrinsicEiLine", Vector2.zero, new Vector2(800f, 3f), new Color(0.75f, 0.80f, 0.86f, 0.60f));

            Color dopLineColor = mode == ExtrinsicDopingKind.NType
                ? new Color(1.0f, 0.65f, 0.18f, 0.95f)
                : new Color(0.16f, 0.88f, 0.84f, 0.95f);
            RectTransform dopLine = CreateUIImageRect(plotArea.transform, "DopantLevelLine", new Vector2(0f, 64f), new Vector2(800f, 4f), dopLineColor);
            RectTransform efLine = CreateUIImageRect(plotArea.transform, "FermiEFLine", new Vector2(0f, 44f), new Vector2(820f, 5f), new Color(0.98f, 0.92f, 0.25f, 1f));

            TMP_Text ecLbl = LabTextStyleHelper.CreateTMPText(plotArea.transform, "EcLabel", "Ec (Conduction Band)", 22f,
                new Vector2(0f, 110f), new Vector2(560f, 36f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            TMP_Text evLbl = LabTextStyleHelper.CreateTMPText(plotArea.transform, "EvLabel", "Ev (Valence Band)", 22f,
                new Vector2(0f, -110f), new Vector2(560f, 36f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Opaque dark pill badges rendered AFTER level lines so horizontal lines never slash through any text.
            Color pillBgColor = new Color(0.08f, 0.11f, 0.16f, 1f);
            RectTransform eiBadge = CreateUIImageRect(plotArea.transform, "EiBadge", Vector2.zero, new Vector2(150f, 24f), pillBgColor);
            TMP_Text eiLbl = LabTextStyleHelper.CreateTMPText(eiBadge.transform, "EiLabel", "Ei Midgap", 17.5f,
                Vector2.zero, new Vector2(142f, 22f), new Color(0.86f, 0.90f, 0.95f), TextAlignmentOptions.Center, FontStyles.Bold);

            RectTransform dopBadge = CreateUIImageRect(plotArea.transform, "DopantLevelBadge", new Vector2(-225f, 64f), new Vector2(330f, 28f), pillBgColor);
            TMP_Text dopLbl = LabTextStyleHelper.CreateTMPText(dopBadge.transform, "DopantLevelLabel",
                mode == ExtrinsicDopingKind.NType ? "Ed (0.045 eV below Ec)" : "Ea (0.045 eV above Ev)",
                18f, Vector2.zero, new Vector2(318f, 26f), dopLineColor, TextAlignmentOptions.Center, FontStyles.Bold);

            RectTransform efBadge = CreateUIImageRect(plotArea.transform, "FermiBadge", new Vector2(225f, 44f), new Vector2(250f, 28f), pillBgColor);
            TMP_Text efLbl = LabTextStyleHelper.CreateTMPText(efBadge.transform, "FermiLabel", "EF",
                18.5f, Vector2.zero, new Vector2(240f, 26f), new Color(1.0f, 0.94f, 0.32f), TextAlignmentOptions.Center, FontStyles.Bold);

            RectTransform[] eDots = new RectTransform[8];
            RectTransform[] hDots = new RectTransform[8];
            for (int i = 0; i < 8; i++)
            {
                eDots[i] = CreateUIImageRect(plotArea.transform, "ConductionDot_" + i, new Vector2(-120f + i * 32f, 88f), new Vector2(13f, 13f), new Color(0.24f, 0.78f, 1.0f, 1f));
                eDots[i].gameObject.SetActive(false);

                hDots[i] = CreateUIImageRect(plotArea.transform, "ValenceDot_" + i, new Vector2(-120f + i * 32f, -88f), new Vector2(13f, 13f), new Color(1.0f, 0.34f, 0.36f, 1f));
                hDots[i].gameObject.SetActive(false);
            }

            TMP_Text footerLbl = LabTextStyleHelper.CreateTMPText(canvas.transform, "Footer",
                "Insert Silicon Wafer & Dopant in Furnace to ionize!", 20f,
                new Vector2(0f, -228f), new Vector2(950f, 44f), new Color(0.92f, 0.96f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            ExtrinsicBandDiagramView view = panel.AddComponent<ExtrinsicBandDiagramView>();
            SerializedObject bSo = new SerializedObject(view);
            LabTextStyleHelper.SetObjRef(bSo, "conductionBandRect", ecRect);
            LabTextStyleHelper.SetObjRef(bSo, "valenceBandRect", evRect);
            LabTextStyleHelper.SetObjRef(bSo, "intrinsicEiLineRect", eiLine);
            LabTextStyleHelper.SetObjRef(bSo, "dopantLevelLineRect", dopLine);
            LabTextStyleHelper.SetObjRef(bSo, "fermiLevelLineRect", efLine);
            LabTextStyleHelper.SetObjRef(bSo, "dopantBadgeRect", dopBadge);
            LabTextStyleHelper.SetObjRef(bSo, "fermiBadgeRect", efBadge);
            LabTextStyleHelper.SetObjRef(bSo, "eiBadgeRect", eiBadge);
            LabTextStyleHelper.SetObjRef(bSo, "titleLabel", titleLbl);
            LabTextStyleHelper.SetObjRef(bSo, "ecLabel", ecLbl);
            LabTextStyleHelper.SetObjRef(bSo, "evLabel", evLbl);
            LabTextStyleHelper.SetObjRef(bSo, "dopantLevelLabel", dopLbl);
            LabTextStyleHelper.SetObjRef(bSo, "eiLabel", eiLbl);
            LabTextStyleHelper.SetObjRef(bSo, "fermiLabel", efLbl);
            LabTextStyleHelper.SetObjRef(bSo, "regimeFooterLabel", footerLbl);
            LabTextStyleHelper.SetObjectArray(bSo, "conductionDots", eDots);
            LabTextStyleHelper.SetObjectArray(bSo, "valenceDots", hDots);
            bSo.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static GameObject BuildDopantShelfAndWafer(
            Transform parent,
            ExtrinsicDopingKind mode,
            Phase4MatSet mats,
            AudioSource stationAudio,
            Camera mainCam,
            out DopantVialItem[] vialsOut,
            out RespawnableItem waferRespawn,
            out List<RespawnableItem> allRespawns)
        {
            allRespawns = new List<RespawnableItem>();

            GameObject shelfRoot = new GameObject("DopantShelfAndWaferZone");
            shelfRoot.transform.SetParent(parent, false);
            shelfRoot.transform.localPosition = new Vector3(0.84f, 0.90f, -0.04f);

            // Two-tier Chemical Rack on left side of workbench (+X side).
            CreateBox("RackBase", shelfRoot.transform, new Vector3(0f, 0.012f, 0f), new Vector3(0.62f, 0.024f, 0.42f), mats.TableFrame, true);
            CreateBox("RackUpperTier", shelfRoot.transform, new Vector3(0f, 0.055f, -0.105f), new Vector3(0.60f, 0.086f, 0.17f), mats.TableFrame, true);

            Canvas headerCanvas = LabTextStyleHelper.CreateWorldCanvas("ShelfTitle", shelfRoot.transform,
                new Vector3(0f, 0.028f, 0.175f), new Vector3(90f, 180f, 0f),
                new Vector2(0.58f, 0.06f), 580, 60, mainCam, false);
            LabTextStyleHelper.CreateTMPText(headerCanvas.transform, "Title", "DOPANT SHELF (GRAB & DROP IN FURNACE)", 24f,
                Vector2.zero, new Vector2(560f, 52f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            VialSpec[] specs = mode == ExtrinsicDopingKind.NType
                ? new[]
                {
                    new VialSpec { Symbol = "P",  FullName = "Phosphorus", AtomicNumber = 15, Group = DopantValenceGroup.GroupV_Donor,    BackRow = true,  ColumnIndex = 0 },
                    new VialSpec { Symbol = "As", FullName = "Arsenic",    AtomicNumber = 33, Group = DopantValenceGroup.GroupV_Donor,    BackRow = true,  ColumnIndex = 1 },
                    new VialSpec { Symbol = "Sb", FullName = "Antimony",   AtomicNumber = 51, Group = DopantValenceGroup.GroupV_Donor,    BackRow = true,  ColumnIndex = 2 },
                    new VialSpec { Symbol = "Ge", FullName = "Germanium",  AtomicNumber = 32, Group = DopantValenceGroup.GroupIV_Intrinsic,BackRow = true, ColumnIndex = 3 },
                    new VialSpec { Symbol = "B",  FullName = "Boron",      AtomicNumber = 5,  Group = DopantValenceGroup.GroupIII_Acceptor,BackRow = false, ColumnIndex = 0 },
                    new VialSpec { Symbol = "Al", FullName = "Aluminum",   AtomicNumber = 13, Group = DopantValenceGroup.GroupIII_Acceptor,BackRow = false, ColumnIndex = 1 },
                    new VialSpec { Symbol = "Ga", FullName = "Gallium",    AtomicNumber = 31, Group = DopantValenceGroup.GroupIII_Acceptor,BackRow = false, ColumnIndex = 2 },
                    new VialSpec { Symbol = "C",  FullName = "Carbon",     AtomicNumber = 6,  Group = DopantValenceGroup.GroupIV_Intrinsic,BackRow = false, ColumnIndex = 3 }
                }
                : new[]
                {
                    new VialSpec { Symbol = "B",  FullName = "Boron",      AtomicNumber = 5,  Group = DopantValenceGroup.GroupIII_Acceptor,BackRow = true,  ColumnIndex = 0 },
                    new VialSpec { Symbol = "Al", FullName = "Aluminum",   AtomicNumber = 13, Group = DopantValenceGroup.GroupIII_Acceptor,BackRow = true,  ColumnIndex = 1 },
                    new VialSpec { Symbol = "Ga", FullName = "Gallium",    AtomicNumber = 31, Group = DopantValenceGroup.GroupIII_Acceptor,BackRow = true,  ColumnIndex = 2 },
                    new VialSpec { Symbol = "In", FullName = "Indium",     AtomicNumber = 49, Group = DopantValenceGroup.GroupIII_Acceptor,BackRow = true,  ColumnIndex = 3 },
                    new VialSpec { Symbol = "P",  FullName = "Phosphorus", AtomicNumber = 15, Group = DopantValenceGroup.GroupV_Donor,    BackRow = false, ColumnIndex = 0 },
                    new VialSpec { Symbol = "As", FullName = "Arsenic",    AtomicNumber = 33, Group = DopantValenceGroup.GroupV_Donor,    BackRow = false, ColumnIndex = 1 },
                    new VialSpec { Symbol = "Ge", FullName = "Germanium",  AtomicNumber = 32, Group = DopantValenceGroup.GroupIV_Intrinsic,BackRow = false, ColumnIndex = 2 },
                    new VialSpec { Symbol = "C",  FullName = "Carbon",     AtomicNumber = 6,  Group = DopantValenceGroup.GroupIV_Intrinsic,BackRow = false, ColumnIndex = 3 }
                };

            vialsOut = new DopantVialItem[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                VialSpec sp = specs[i];
                // Player looks -Z so +X is left; place col 0..3 left-to-right (+0.21 down to -0.21).
                float x = 0.21f - sp.ColumnIndex * 0.14f;
                float z = sp.BackRow ? -0.105f : 0.065f;
                float yBase = sp.BackRow ? 0.098f : 0.024f;

                GameObject marker = new GameObject("VialHome_" + sp.Symbol);
                marker.transform.SetParent(shelfRoot.transform, false);
                marker.transform.localPosition = new Vector3(x, yBase + 0.056f, z);

                vialsOut[i] = BuildSingleDopantVial(
                    shelfRoot.transform, marker.transform, sp, mats, stationAudio, mainCam, out RespawnableItem vialResp);
                allRespawns.Add(vialResp);
            }

            // Grabbable Silicon Wafer resting on front pad (+0.72, 0.90, 0.30).
            GameObject waferTray = CreateBox("WaferTray", parent, new Vector3(0.72f, 0.906f, 0.30f), new Vector3(0.20f, 0.012f, 0.20f), mats.TableFrame, true);
            Object.DestroyImmediate(waferTray.GetComponent<Collider>());

            GameObject waferHome = new GameObject("WaferHomeMarker");
            waferHome.transform.SetParent(parent, false);
            waferHome.transform.localPosition = new Vector3(0.72f, 0.942f, 0.30f);

            GameObject waferGo = new GameObject("SiliconWafer_Grabbable");
            waferGo.transform.SetParent(parent, false);
            waferGo.transform.localPosition = waferHome.transform.localPosition;

            GameObject waferDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            waferDisc.name = "DiscMesh";
            waferDisc.transform.SetParent(waferGo.transform, false);
            waferDisc.transform.localScale = new Vector3(0.145f, 0.011f, 0.145f);
            Object.DestroyImmediate(waferDisc.GetComponent<Collider>());
            Renderer waferRend = waferDisc.GetComponent<Renderer>();
            waferRend.sharedMaterial = mats.WaferSi;

            Canvas wCanvas = LabTextStyleHelper.CreateWorldCanvas("WaferLabelCanvas", waferGo.transform,
                new Vector3(0f, 0.014f, 0f), new Vector3(90f, 180f, 0f),
                new Vector2(0.12f, 0.08f), 180, 120, mainCam, false);
            LabTextStyleHelper.CreateTMPText(wCanvas.transform, "Title", "Si\nWAFER", 34f,
                Vector2.zero, new Vector2(170f, 110f), new Color(0.88f, 0.96f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Collider >= 5 cm tall for reliable Quest grab + desktop physics.
            BoxCollider wCol = waferGo.AddComponent<BoxCollider>();
            wCol.size = new Vector3(0.15f, 0.056f, 0.15f);

            Rigidbody wRb = waferGo.AddComponent<Rigidbody>();
            wRb.mass = 0.25f;
            wRb.linearDamping = 1f;
            wRb.angularDamping = 2f;

            XRGrabInteractable wGrab = waferGo.AddComponent<XRGrabInteractable>();
            wGrab.colliders.Add(wCol);
            wGrab.useDynamicAttach = true;
            wGrab.throwOnDetach = false;

            AddInteractionFeedback(waferGo, wGrab, new[] { waferRend }, stationAudio,
                "Silicon Wafer (Insert in Furnace)", new Vector3(0f, 0.12f, 0f), mats, mainCam);

            waferRespawn = waferGo.AddComponent<RespawnableItem>();
            SerializedObject wrSo = new SerializedObject(waferRespawn);
            LabTextStyleHelper.SetObjRef(wrSo, "homeAnchor", waferHome.transform);
            LabTextStyleHelper.SetObjRef(wrSo, "audioSource", stationAudio);
            wrSo.ApplyModifiedPropertiesWithoutUndo();
            allRespawns.Add(waferRespawn);

            return shelfRoot;
        }

        private static DopantVialItem BuildSingleDopantVial(
            Transform parent,
            Transform homeMarker,
            VialSpec spec,
            Phase4MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            out RespawnableItem respawn)
        {
            GameObject go = new GameObject("Vial_" + spec.Symbol);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = homeMarker.localPosition;

            Material accentMat = spec.Group == DopantValenceGroup.GroupV_Donor
                ? mats.DonorOrange
                : (spec.Group == DopantValenceGroup.GroupIII_Acceptor ? mats.AcceptorTeal : mats.NeutralSlate);

            GameObject bodyCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bodyCyl.name = "Bottle";
            bodyCyl.transform.SetParent(go.transform, false);
            bodyCyl.transform.localScale = new Vector3(0.056f, 0.045f, 0.056f);
            Object.DestroyImmediate(bodyCyl.GetComponent<Collider>());
            Renderer bodyRend = bodyCyl.GetComponent<Renderer>();
            bodyRend.sharedMaterial = mats.ChamberGlass;

            GameObject capCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            capCyl.name = "Cap";
            capCyl.transform.SetParent(go.transform, false);
            capCyl.transform.localPosition = new Vector3(0f, 0.048f, 0f);
            capCyl.transform.localScale = new Vector3(0.060f, 0.012f, 0.060f);
            Object.DestroyImmediate(capCyl.GetComponent<Collider>());
            Renderer capRend = capCyl.GetComponent<Renderer>();
            capRend.sharedMaterial = accentMat;

            GameObject labelPlate = CreateBox("LabelBacking", go.transform, new Vector3(0f, 0f, 0.032f), new Vector3(0.054f, 0.056f, 0.006f), mats.ScreenPanel, false);
            Object.DestroyImmediate(labelPlate.GetComponent<Collider>());

            string groupShort = spec.Group == DopantValenceGroup.GroupV_Donor ? "V"
                : (spec.Group == DopantValenceGroup.GroupIII_Acceptor ? "III" : "IV");
            Canvas vCanvas = LabTextStyleHelper.CreateWorldCanvas("VialCanvas", go.transform,
                new Vector3(0f, 0f, 0.036f), new Vector3(0f, 180f, 0f),
                new Vector2(0.052f, 0.054f), 110, 114, mainCam, false);
            LabTextStyleHelper.CreateTMPText(vCanvas.transform, "Sym", $"{spec.Symbol}\n({groupShort})", 36f,
                Vector2.zero, new Vector2(104f, 108f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Collider >= 5 cm all axes for clean XR grab.
            BoxCollider col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.064f, 0.114f, 0.064f);

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.18f;
            rb.linearDamping = 1.2f;
            rb.angularDamping = 2.4f;

            XRGrabInteractable grab = go.AddComponent<XRGrabInteractable>();
            grab.colliders.Add(col);
            grab.useDynamicAttach = true;
            grab.throwOnDetach = false;

            string tip = $"{spec.Symbol} ({spec.FullName}) — Group {groupShort} ({(int)spec.Group} val. e-)";
            AddInteractionFeedback(go, grab, new[] { capRend, bodyRend }, audioSource, tip, new Vector3(0f, 0.11f, 0f), mats, mainCam);

            respawn = go.AddComponent<RespawnableItem>();
            SerializedObject rpSo = new SerializedObject(respawn);
            LabTextStyleHelper.SetObjRef(rpSo, "homeAnchor", homeMarker);
            LabTextStyleHelper.SetObjRef(rpSo, "audioSource", audioSource);
            rpSo.ApplyModifiedPropertiesWithoutUndo();

            DopantVialItem item = go.AddComponent<DopantVialItem>();
            item.Configure(spec.Symbol, spec.FullName, spec.AtomicNumber, spec.Group, grab, respawn);
            return item;
        }

        private static void BuildDiffusionFurnace(
            Transform parent,
            ExtrinsicDopingKind mode,
            Phase4MatSet mats,
            AudioSource stationAudio,
            Camera mainCam,
            out XRSocketInteractor waferSocket,
            out SocketRingFeedback waferFeedback,
            out XRSocketInteractor dopantSocket,
            out SocketRingFeedback dopantFeedback,
            out Renderer furnaceCoreRenderer)
        {
            GameObject furnaceRoot = new GameObject("DiffusionFurnace");
            furnaceRoot.transform.SetParent(parent, false);
            furnaceRoot.transform.localPosition = new Vector3(0.18f, 0.90f, 0.22f);

            CreateBox("FurnaceDeck", furnaceRoot.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.56f, 0.03f, 0.30f), mats.TableFrame, true);
            GameObject coreBox = CreateBox("FurnaceCoreChamber", furnaceRoot.transform, new Vector3(0f, 0.036f, -0.05f), new Vector3(0.24f, 0.044f, 0.14f), mats.TableTop, false);
            Object.DestroyImmediate(coreBox.GetComponent<Collider>());
            furnaceCoreRenderer = coreBox.GetComponent<Renderer>();

            Canvas titleCanvas = LabTextStyleHelper.CreateWorldCanvas("FurnaceTitleCanvas", furnaceRoot.transform,
                new Vector3(0f, 0.062f, -0.05f), new Vector3(90f, 180f, 0f),
                new Vector2(0.24f, 0.09f), 260, 100, mainCam, false);
            LabTextStyleHelper.CreateTMPText(titleCanvas.transform, "Title",
                mode == ExtrinsicDopingKind.NType ? "N-TYPE\nFURNACE" : "P-TYPE\nFURNACE", 28f,
                Vector2.zero, new Vector2(246f, 92f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Wafer Socket (left bay: +0.17f in local X).
            BuildFurnaceSocketBay(furnaceRoot.transform, "WaferSocketBay", new Vector3(0.17f, 0.03f, 0.03f),
                "1. SI WAFER", mats, stationAudio, mainCam, out waferSocket, out waferFeedback);

            // Dopant Socket (right bay: -0.17f in local X -> 0.34m center separation, zero overlap).
            BuildFurnaceSocketBay(furnaceRoot.transform, "DopantSocketBay", new Vector3(-0.17f, 0.03f, 0.03f),
                "2. DOPANT", mats, stationAudio, mainCam, out dopantSocket, out dopantFeedback);
        }

        private static void BuildFurnaceSocketBay(
            Transform parent,
            string bayName,
            Vector3 localPos,
            string bayLabel,
            Phase4MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            out XRSocketInteractor socket,
            out SocketRingFeedback feedback)
        {
            GameObject bay = new GameObject(bayName);
            bay.transform.SetParent(parent, false);
            bay.transform.localPosition = localPos;

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "SocketRing";
            ring.transform.SetParent(bay.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.006f, 0f);
            ring.transform.localScale = new Vector3(0.17f, 0.006f, 0.17f);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            Renderer ringRend = ring.GetComponent<Renderer>();
            ringRend.sharedMaterial = mats.AccentBase;

            Canvas lblCanvas = LabTextStyleHelper.CreateWorldCanvas("BayLabel", bay.transform,
                new Vector3(0f, 0.015f, 0.105f), new Vector3(90f, 180f, 0f),
                new Vector2(0.18f, 0.042f), 200, 48, mainCam, false);
            LabTextStyleHelper.CreateTMPText(lblCanvas.transform, "Text", bayLabel, 24f,
                Vector2.zero, new Vector2(190f, 44f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject attach = new GameObject("AttachPoint");
            attach.transform.SetParent(bay.transform, false);
            attach.transform.localPosition = new Vector3(0f, 0.055f, 0f);

            SphereCollider triggerCol = bay.AddComponent<SphereCollider>();
            triggerCol.isTrigger = true;
            triggerCol.center = new Vector3(0f, 0.055f, 0f);
            triggerCol.radius = 0.115f;

            socket = bay.AddComponent<XRSocketInteractor>();
            socket.attachTransform = attach.transform;
            socket.showInteractableHoverMeshes = true;

            feedback = bay.AddComponent<SocketRingFeedback>();
            SerializedObject fbSo = new SerializedObject(feedback);
            LabTextStyleHelper.SetObjRef(fbSo, "socket", socket);
            LabTextStyleHelper.SetObjRef(fbSo, "ringRenderer", ringRend);
            LabTextStyleHelper.SetObjRef(fbSo, "audioSource", audioSource);
            fbSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ExtrinsicLatticeVisualizer BuildExtrinsicLatticeStage(
            Transform parent,
            ExtrinsicDopingKind mode,
            Phase4MatSet mats,
            AudioSource audioSource,
            Camera mainCam)
        {
            GameObject stageRoot = new GameObject("ExtrinsicLatticeStage");
            stageRoot.transform.SetParent(parent, false);
            stageRoot.transform.localPosition = new Vector3(0.18f, 1.00f, -0.05f);

            CreateBox("PedestalBase", stageRoot.transform, new Vector3(0f, 0.014f, 0f), new Vector3(0.56f, 0.028f, 0.28f), mats.TableFrame, true);
            CreateBox("PillarL", stageRoot.transform, new Vector3(0.24f, 0.06f, -0.08f), new Vector3(0.025f, 0.10f, 0.025f), mats.TableFrame, true);
            CreateBox("PillarR", stageRoot.transform, new Vector3(-0.24f, 0.06f, -0.08f), new Vector3(0.025f, 0.10f, 0.025f), mats.TableFrame, true);

            // Top Banner & Hover Tooltip Display (always visible cleanly facing player).
            GameObject headerBacking = CreateBox("BannerBacking", stageRoot.transform, new Vector3(0f, 0.42f, -0.02f), new Vector3(0.58f, 0.082f, 0.016f), mats.ScreenPanel, false);
            Object.DestroyImmediate(headerBacking.GetComponent<Collider>());
            Canvas topCanvas = LabTextStyleHelper.CreateWorldCanvas("LatticeHeaderCanvas", stageRoot.transform,
                new Vector3(0f, 0.42f, -0.010f), new Vector3(0f, 180f, 0f),
                new Vector2(0.56f, 0.076f), 680, 92, mainCam, false);
            TMP_Text scaleHeader = LabTextStyleHelper.CreateTMPText(topCanvas.transform, "ScaleHeader",
                mode == ExtrinsicDopingKind.NType ? "N-Type Si Lattice — Not to scale" : "P-Type Si Lattice — Not to scale",
                25f, new Vector2(0f, 18f), new Vector2(660f, 38f), new Color(1.0f, 0.86f, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);
            TMP_Text mechCaption = LabTextStyleHelper.CreateTMPText(topCanvas.transform, "MechanismCaption",
                "Ray-point atoms, ions, covalent bonds, or mobile carriers",
                18f, new Vector2(0f, -20f), new Vector2(660f, 34f), Color.white, TextAlignmentOptions.Center, FontStyles.Normal);

            // Front Lower Hover Inspection Strip.
            GameObject hoverStrip = new GameObject("HoverInfoStrip");
            hoverStrip.transform.SetParent(stageRoot.transform, false);
            hoverStrip.transform.localPosition = new Vector3(0f, 0.036f, 0.145f);
            hoverStrip.transform.localRotation = Quaternion.Euler(52f, 180f, 0f);
            GameObject hBack = CreateBox("Backing", hoverStrip.transform, Vector3.zero, new Vector3(0.54f, 0.072f, 0.014f), mats.ScreenPanel, false);
            Object.DestroyImmediate(hBack.GetComponent<Collider>());
            Canvas hCanvas = LabTextStyleHelper.CreateWorldCanvas("HoverInfoCanvas", hoverStrip.transform,
                new Vector3(0f, 0f, -0.009f), Vector3.zero,
                new Vector2(0.52f, 0.068f), 620, 82, mainCam, false);
            TMP_Text hoverTitle = LabTextStyleHelper.CreateTMPText(hCanvas.transform, "HoverTitle",
                "Ray-Point Crystal Elements", 20f, new Vector2(0f, 18f), new Vector2(600f, 30f),
                new Color(0.28f, 0.92f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);
            TMP_Text hoverDetail = LabTextStyleHelper.CreateTMPText(hCanvas.transform, "HoverDetail",
                "Dope Silicon Wafer in Furnace to reveal atomic lattice.", 16f, new Vector2(0f, -18f), new Vector2(600f, 34f),
                Color.white, TextAlignmentOptions.Center, FontStyles.Normal);

            // Standby Placeholder (shown before wafer + valid dopant).
            GameObject standbyRoot = new GameObject("StandbyPlaceholder");
            standbyRoot.transform.SetParent(stageRoot.transform, false);
            standbyRoot.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            GameObject glassCube = CreateBox("StandbyGlass", standbyRoot.transform, Vector3.zero, new Vector3(0.46f, 0.24f, 0.22f), mats.ChamberGlass, false);
            Object.DestroyImmediate(glassCube.GetComponent<Collider>());

            // Active 3D Crystal Hologram Root.
            GameObject visRoot = new GameObject("LatticeVisualRoot");
            visRoot.transform.SetParent(stageRoot.transform, false);
            visRoot.transform.localPosition = new Vector3(0f, 0.22f, 0f);

            // Central Dopant + 2 extra active Ion sites (enabled cleanly at higher ND/NA).
            Vector3[] ionPos =
            {
                new Vector3(0f, 0.02f, 0f),
                new Vector3(0.18f, 0.06f, 0.02f),
                new Vector3(-0.18f, 0.06f, -0.02f)
            };
            LatticeElementTarget[] ionTargets = new LatticeElementTarget[ionPos.Length];
            Material ionMat = mode == ExtrinsicDopingKind.NType ? mats.DonorOrange : mats.AcceptorTeal;
            for (int i = 0; i < ionPos.Length; i++)
            {
                ionTargets[i] = CreateLatticeNodeSphere(
                    "DopantIon_" + i, visRoot.transform, ionPos[i], 0.056f, ionMat,
                    mode == ExtrinsicDopingKind.NType ? LatticeElementKind.DonorIon : LatticeElementKind.AcceptorIon, i);
            }

            // 8 Host Silicon atoms forming clean tetrahedral neighborhood + crystal chain.
            Vector3[] hostPos =
            {
                new Vector3(0.11f,  0.095f,  0.055f),
                new Vector3(-0.11f, -0.045f,  0.055f),
                new Vector3(0.11f, -0.045f, -0.055f),
                new Vector3(-0.11f,  0.095f, -0.055f),
                new Vector3(0.21f, -0.045f,  0.02f),
                new Vector3(-0.21f,  0.02f,   0.04f),
                new Vector3(0.0f,   0.13f,   0.00f),
                new Vector3(0.0f,  -0.085f, -0.04f)
            };
            LatticeElementTarget[] hostTargets = new LatticeElementTarget[hostPos.Length];
            for (int i = 0; i < hostPos.Length; i++)
            {
                hostTargets[i] = CreateLatticeNodeSphere(
                    "HostSi_" + i, visRoot.transform, hostPos[i], 0.042f, mats.AtomCore, LatticeElementKind.HostAtom, i);
            }

            // Covalent bonds around center Dopant atom (bonds 0,1,2 intact; bond 3 incomplete for P-Type!).
            List<LatticeElementTarget> bondTargets = new List<LatticeElementTarget>();
            LatticeElementTarget incompleteBondTarget = null;
            GameObject incompleteGapMarker = null;

            int[,] edges =
            {
                { 0, 0 }, // center -> host 0
                { 0, 1 }, // center -> host 1
                { 0, 2 }, // center -> host 2
                { 0, 3 }  // center -> host 3 (incomplete in P-Type!)
            };

            for (int e = 0; e < 4; e++)
            {
                Vector3 a = ionPos[0];
                Vector3 b = hostPos[edges[e, 1]];
                if (e == 3 && mode == ExtrinsicDopingKind.PType)
                {
                    // Build incomplete covalent bond (dashed short stub + glowing coral gap marker).
                    incompleteBondTarget = CreateLatticeBond("IncompleteCovalentBond", visRoot.transform, a, Vector3.Lerp(a, b, 0.45f), mats.BondBroken, LatticeElementKind.IncompleteBond, e);
                    incompleteGapMarker = CreateBox("IncompleteBondGap", visRoot.transform, Vector3.Lerp(a, b, 0.68f), new Vector3(0.018f, 0.018f, 0.018f), mats.HoleRed, false);
                    Object.DestroyImmediate(incompleteGapMarker.GetComponent<Collider>());
                }
                else
                {
                    bondTargets.Add(CreateLatticeBond("CovalentBond_" + e, visRoot.transform, a, b, mats.BondIntact, LatticeElementKind.CovalentBond, e));
                }
            }

            // Extra outer crystal bonds cleanly framing host lattice.
            bondTargets.Add(CreateLatticeBond("OuterBond_A", visRoot.transform, hostPos[0], hostPos[6], mats.BondIntact, LatticeElementKind.CovalentBond, 4));
            bondTargets.Add(CreateLatticeBond("OuterBond_B", visRoot.transform, hostPos[3], hostPos[6], mats.BondIntact, LatticeElementKind.CovalentBond, 5));
            bondTargets.Add(CreateLatticeBond("OuterBond_C", visRoot.transform, hostPos[2], hostPos[4], mats.BondIntact, LatticeElementKind.CovalentBond, 6));
            bondTargets.Add(CreateLatticeBond("OuterBond_D", visRoot.transform, hostPos[1], hostPos[5], mats.BondIntact, LatticeElementKind.CovalentBond, 7));

            // Mobile Carrier Swarms (<= 16 e- and <= 16 h+ -> crisp Quest budget).
            int poolSize = 16;
            LatticeElementTarget[] eSwarm = new LatticeElementTarget[poolSize];
            LatticeElementTarget[] hSwarm = new LatticeElementTarget[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                eSwarm[i] = CreateLatticeNodeSphere("MobileElectron_" + i, visRoot.transform, Vector3.zero, 0.022f, mats.ElectronBlue, LatticeElementKind.FreeElectron, i);
                hSwarm[i] = CreateLatticeNodeSphere("MobileHole_" + i, visRoot.transform, Vector3.zero, 0.024f, mats.HoleRed, LatticeElementKind.Hole, i);
            }

            // P-Type dedicated visible valence-electron hopping track (e- hops left <-> hole shifts right).
            Vector3[] hopSites =
            {
                new Vector3( 0.16f, -0.105f, 0.068f),
                new Vector3( 0.08f, -0.105f, 0.068f),
                new Vector3( 0.00f, -0.105f, 0.068f),
                new Vector3(-0.08f, -0.105f, 0.068f),
                new Vector3(-0.16f, -0.105f, 0.068f)
            };
            Transform[] hopElectrons = new Transform[hopSites.Length];
            Transform hopHoleTf = null;
            LatticeElementTarget hopHoleTarget = null;
            LatticeElementTarget hopElecTarget = null;

            if (mode == ExtrinsicDopingKind.PType)
            {
                for (int i = 0; i < hopSites.Length; i++)
                {
                    GameObject pad = CreateBox("HopSiteMarker_" + i, visRoot.transform, hopSites[i] + new Vector3(0f, -0.016f, 0f),
                        new Vector3(0.028f, 0.005f, 0.028f), mats.TableFrame, false);
                    Object.DestroyImmediate(pad.GetComponent<Collider>());

                    LatticeElementTarget et = CreateLatticeNodeSphere("HopValenceElectron_" + i, visRoot.transform,
                        hopSites[i], 0.020f, mats.ElectronBlue, LatticeElementKind.FreeElectron, 100 + i);
                    hopElectrons[i] = et.transform;
                    if (i == 0) hopElecTarget = et;
                }

                hopHoleTarget = CreateLatticeNodeSphere("HoppingVacancyHole", visRoot.transform,
                    hopSites[0], 0.027f, mats.HoleRed, LatticeElementKind.Hole, 200);
                hopHoleTf = hopHoleTarget.transform;
            }

            ExtrinsicLatticeVisualizer vis = stageRoot.AddComponent<ExtrinsicLatticeVisualizer>();
            SerializedObject lSo = new SerializedObject(vis);
            SerializedProperty dmProp = lSo.FindProperty("dopingMode");
            if (dmProp != null) dmProp.intValue = (int)mode;
            LabTextStyleHelper.SetObjRef(lSo, "visualRoot", visRoot);
            LabTextStyleHelper.SetObjRef(lSo, "standbyPromptRoot", standbyRoot);
            LabTextStyleHelper.SetObjRef(lSo, "scaleHeaderText", scaleHeader);
            LabTextStyleHelper.SetObjRef(lSo, "mechanismCaptionText", mechCaption);
            LabTextStyleHelper.SetObjRef(lSo, "hoverTitleText", hoverTitle);
            LabTextStyleHelper.SetObjRef(lSo, "hoverDetailText", hoverDetail);
            LabTextStyleHelper.SetObjRef(lSo, "audioSource", audioSource);
            LabTextStyleHelper.SetObjectArray(lSo, "hostAtoms", hostTargets);
            LabTextStyleHelper.SetObjectArray(lSo, "donorOrAcceptorIons", ionTargets);
            LabTextStyleHelper.SetObjectArray(lSo, "covalentBonds", bondTargets.ToArray());
            LabTextStyleHelper.SetObjRef(lSo, "incompleteBondTarget", incompleteBondTarget);
            LabTextStyleHelper.SetObjRef(lSo, "incompleteBondGapMarker", incompleteGapMarker);
            LabTextStyleHelper.SetObjectArray(lSo, "freeElectrons", eSwarm);
            LabTextStyleHelper.SetObjectArray(lSo, "mobileHoles", hSwarm);

            SerializedProperty hsProp = lSo.FindProperty("hopSitePositions");
            if (hsProp != null)
            {
                hsProp.arraySize = hopSites.Length;
                for (int i = 0; i < hopSites.Length; i++)
                {
                    hsProp.GetArrayElementAtIndex(i).vector3Value = hopSites[i];
                }
            }

            LabTextStyleHelper.SetObjectArray(lSo, "hoppingValenceElectrons", hopElectrons);
            LabTextStyleHelper.SetObjRef(lSo, "hoppingVacancyHole", hopHoleTf);
            LabTextStyleHelper.SetObjRef(lSo, "hoppingHoleTarget", hopHoleTarget);
            LabTextStyleHelper.SetObjRef(lSo, "hoppingElectronTarget", hopElecTarget);
            lSo.ApplyModifiedPropertiesWithoutUndo();

            return vis;
        }

        private static LatticeElementTarget CreateLatticeNodeSphere(
            string name, Transform parent, Vector3 localPos, float diameter, Material mat, LatticeElementKind kind, int index)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * diameter;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            SphereCollider col = go.GetComponent<SphereCollider>();
            col.radius = Mathf.Max(0.65f, 0.016f / Mathf.Max(0.01f, diameter));

            XRSimpleInteractable xr = go.AddComponent<XRSimpleInteractable>();
            xr.colliders.Add(col);
            LatticeElementTarget t = go.AddComponent<LatticeElementTarget>();
            t.Configure(xr, r, kind, index);
            return t;
        }

        private static LatticeElementTarget CreateLatticeBond(
            string name, Transform parent, Vector3 pA, Vector3 pB, Material mat, LatticeElementKind kind, int index)
        {
            Vector3 mid = (pA + pB) * 0.5f;
            Vector3 dir = pB - pA;
            float dist = Mathf.Max(0.02f, dir.magnitude);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = mid;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);

            CapsuleCollider col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.015f;
            col.height = dist * 0.88f;
            col.direction = 1;

            GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = "Rod";
            rod.transform.SetParent(go.transform, false);
            rod.transform.localScale = new Vector3(0.008f, dist * 0.48f, 0.008f);
            Object.DestroyImmediate(rod.GetComponent<Collider>());
            Renderer r = rod.GetComponent<Renderer>();
            r.sharedMaterial = mat;

            XRSimpleInteractable xr = go.AddComponent<XRSimpleInteractable>();
            xr.colliders.Add(col);
            LatticeElementTarget t = go.AddComponent<LatticeElementTarget>();
            t.Configure(xr, r, kind, index);
            return t;
        }

        private static GameObject BuildConcentrationDeck(
            Transform parent,
            ExtrinsicDopingKind mode,
            Phase4MatSet mats,
            AudioSource stationAudio,
            Camera mainCam,
            out Slider expSlider,
            out PhysicalPushButton downBtn,
            out PhysicalPushButton upBtn)
        {
            GameObject deck = new GameObject("DopingConcentrationDeck");
            deck.transform.SetParent(parent, false);
            deck.transform.localPosition = new Vector3(-0.44f, 0.90f, 0.16f);

            CreateBox("Plinth", deck.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.46f, 0.04f, 0.42f), mats.TableFrame, true);

            string sym = mode == ExtrinsicDopingKind.NType ? "ND (DONOR)" : "NA (ACCEPTOR)";
            Canvas headerCanvas = LabTextStyleHelper.CreateWorldCanvas("ConcHeaderCanvas", deck.transform,
                new Vector3(0f, 0.044f, -0.14f), new Vector3(90f, 180f, 0f),
                new Vector2(0.44f, 0.10f), 440, 100, mainCam, false);
            LabTextStyleHelper.CreateTMPText(headerCanvas.transform, "Title",
                $"{sym}\n10^14 to 10^18 cm^-3", 22f, Vector2.zero, new Vector2(420f, 92f),
                new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Slanted interactive World-Space Slider Bar.
            GameObject sliderPedestal = new GameObject("ConcentrationSliderBar");
            sliderPedestal.transform.SetParent(deck.transform, false);
            sliderPedestal.transform.localPosition = new Vector3(0f, 0.072f, -0.02f);
            sliderPedestal.transform.localRotation = Quaternion.Euler(58f, 180f, 0f);
            GameObject sBack = CreateBox("Backing", sliderPedestal.transform, Vector3.zero, new Vector3(0.42f, 0.086f, 0.018f), mats.ScreenPanel, false);
            Object.DestroyImmediate(sBack.GetComponent<Collider>());

            Canvas sliderCanvas = LabTextStyleHelper.CreateWorldCanvas("ConcSliderCanvas", sliderPedestal.transform,
                new Vector3(0f, 0f, -0.012f), Vector3.zero,
                new Vector2(0.40f, 0.082f), 400, 82, mainCam, true);
            LabTextStyleHelper.CreateTMPText(sliderCanvas.transform, "Min14", "10^14", 16f, new Vector2(-150f, 25f), new Vector2(80f, 24f), Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            LabTextStyleHelper.CreateTMPText(sliderCanvas.transform, "Mid16", "10^16", 16f, new Vector2(0f, 25f), new Vector2(80f, 24f), new Color(0.55f, 0.96f, 0.72f), TextAlignmentOptions.Center, FontStyles.Bold);
            LabTextStyleHelper.CreateTMPText(sliderCanvas.transform, "Max18", "10^18", 16f, new Vector2(150f, 25f), new Vector2(80f, 24f), new Color(1.0f, 0.72f, 0.36f), TextAlignmentOptions.Right, FontStyles.Bold);

            expSlider = CreateUISlider(sliderCanvas.transform, "DopingExponentSlider", new Vector2(0f, -10f), new Vector2(356f, 40f));
            expSlider.minValue = 14f;
            expSlider.maxValue = 18f;
            expSlider.value = 16f;

            downBtn = BuildLabeledButton(deck.transform, "DecreaseDopingBtn",
                new Vector3(0.115f, 0.04f, 0.125f), new Vector3(0.095f, 0.036f, 0.07f),
                mats.ButtonAmber, "-0.5 DEC", "Lower Concentration (x0.32)", mats, stationAudio, mainCam, out _, 0.16f);

            upBtn = BuildLabeledButton(deck.transform, "IncreaseDopingBtn",
                new Vector3(-0.115f, 0.04f, 0.125f), new Vector3(0.095f, 0.036f, 0.07f),
                mats.ButtonGreen, "+0.5 DEC", "Raise Concentration (x3.16)", mats, stationAudio, mainCam, out _, 0.16f);

            return deck;
        }

        private static ExtrinsicSemiconductorBlock BuildOutputBlockCradle(
            Transform parent,
            ExtrinsicDopingKind mode,
            Phase4MatSet mats,
            AudioSource stationAudio,
            Camera mainCam,
            out TMP_Text outputTrayText,
            out RespawnableItem blockRespawn)
        {
            GameObject cradle = new GameObject("OutputBlockCradle");
            cradle.transform.SetParent(parent, false);
            cradle.transform.localPosition = new Vector3(-0.94f, 0.90f, 0.04f);

            CreateBox("Tray", cradle.transform, new Vector3(0f, 0.014f, 0f), new Vector3(0.28f, 0.028f, 0.34f), mats.TableFrame, true);
            GameObject screenBack = CreateBox("TrayLabelBack", cradle.transform, new Vector3(0f, 0.030f, -0.105f), new Vector3(0.26f, 0.012f, 0.095f), mats.ScreenPanel, false);
            Object.DestroyImmediate(screenBack.GetComponent<Collider>());
            Canvas trayCanvas = LabTextStyleHelper.CreateWorldCanvas("TrayStatusCanvas", cradle.transform,
                new Vector3(0f, 0.038f, -0.105f), new Vector3(90f, 180f, 0f),
                new Vector2(0.25f, 0.09f), 320, 115, mainCam, false);
            outputTrayText = LabTextStyleHelper.CreateTMPText(trayCanvas.transform, "TrayText",
                "OUTPUT TRAY: Empty", 20f, Vector2.zero, new Vector2(304f, 106f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject blockHome = new GameObject("OutputBlockHome");
            blockHome.transform.SetParent(cradle.transform, false);
            blockHome.transform.localPosition = new Vector3(0f, 0.074f, 0.055f);

            GameObject blockGo = new GameObject(mode == ExtrinsicDopingKind.NType ? "NTypeSiliconBlock" : "PTypeSiliconBlock");
            blockGo.transform.SetParent(cradle.transform, false);
            blockGo.transform.localPosition = blockHome.transform.localPosition;

            GameObject mesh = CreateBox("BlockMesh", blockGo.transform, Vector3.zero, new Vector3(0.14f, 0.08f, 0.14f),
                mode == ExtrinsicDopingKind.NType ? mats.BlockNType : mats.BlockPType, false);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            Renderer blockRend = mesh.GetComponent<Renderer>();

            Canvas blkCanvas = LabTextStyleHelper.CreateWorldCanvas("BlockLabelCanvas", blockGo.transform,
                new Vector3(0f, 0.043f, 0f), new Vector3(90f, 180f, 0f),
                new Vector2(0.132f, 0.132f), 200, 200, mainCam, false);
            TMP_Text blockTmp = LabTextStyleHelper.CreateTMPText(blkCanvas.transform, "BlockLabel",
                mode == ExtrinsicDopingKind.NType ? "N-TYPE Si" : "P-TYPE Si", 26f,
                Vector2.zero, new Vector2(186f, 186f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            BoxCollider blkCol = blockGo.AddComponent<BoxCollider>();
            blkCol.size = new Vector3(0.14f, 0.08f, 0.14f);

            Rigidbody blkRb = blockGo.AddComponent<Rigidbody>();
            blkRb.mass = 0.35f;
            blkRb.linearDamping = 1f;
            blkRb.angularDamping = 2f;

            XRGrabInteractable blkGrab = blockGo.AddComponent<XRGrabInteractable>();
            blkGrab.colliders.Add(blkCol);
            blkGrab.useDynamicAttach = true;
            blkGrab.throwOnDetach = false;

            AddInteractionFeedback(blockGo, blkGrab, new[] { blockRend }, stationAudio,
                mode == ExtrinsicDopingKind.NType ? "N-Type Silicon Block (Use in Station 4)" : "P-Type Silicon Block (Use in Station 4)",
                new Vector3(0f, 0.12f, 0f), mats, mainCam);

            blockRespawn = blockGo.AddComponent<RespawnableItem>();
            SerializedObject brSo = new SerializedObject(blockRespawn);
            LabTextStyleHelper.SetObjRef(brSo, "homeAnchor", blockHome.transform);
            LabTextStyleHelper.SetObjRef(brSo, "audioSource", stationAudio);
            brSo.ApplyModifiedPropertiesWithoutUndo();

            ExtrinsicSemiconductorBlock blockComp = blockGo.AddComponent<ExtrinsicSemiconductorBlock>();
            SerializedObject bSo = new SerializedObject(blockComp);
            SerializedProperty dkProp = bSo.FindProperty("dopingKind");
            if (dkProp != null) dkProp.intValue = (int)mode;
            LabTextStyleHelper.SetObjRef(bSo, "grabInteractable", blkGrab);
            LabTextStyleHelper.SetObjRef(bSo, "respawnableItem", blockRespawn);
            LabTextStyleHelper.SetObjRef(bSo, "blockRenderer", blockRend);
            LabTextStyleHelper.SetObjRef(bSo, "labelText", blockTmp);
            bSo.ApplyModifiedPropertiesWithoutUndo();

            return blockComp;
        }

        private static NPCController UpgradeNpcEscortAndClickTarget(Phase4MatSet mats, Camera mainCam)
        {
            GameObject npcGo = GameObject.Find("NPCScientist");
            if (npcGo == null)
            {
                return Object.FindAnyObjectByType<NPCController>();
            }

            NPCController npcCtrl = npcGo.GetComponent<NPCController>();
            AudioSource npcAudio = npcGo.GetComponent<AudioSource>();

            // Remove old NPCInteractionTarget if rebuilding idempotently; keep NPCModelSlot 100% untouched!
            Transform existingTarget = npcGo.transform.Find("NPCInteractionTarget");
            if (existingTarget != null)
            {
                Object.DestroyImmediate(existingTarget.gameObject);
            }

            GameObject target = new GameObject("NPCInteractionTarget");
            target.transform.SetParent(npcGo.transform, false);
            target.transform.localPosition = Vector3.zero;

            CapsuleCollider targetCol = target.AddComponent<CapsuleCollider>();
            targetCol.center = new Vector3(0f, 0.96f, 0f);
            targetCol.radius = 0.34f;
            targetCol.height = 1.72f;
            targetCol.isTrigger = false;

            Rigidbody targetRb = target.AddComponent<Rigidbody>();
            targetRb.isKinematic = true;
            targetRb.useGravity = false;

            XRSimpleInteractable npcSimple = target.AddComponent<XRSimpleInteractable>();
            npcSimple.colliders.Add(targetCol);

            // Highlight placeholder body if present (safely optional when user drops custom 3D model into NPCModelSlot).
            Renderer[] fallbackRenderers = System.Array.Empty<Renderer>();
            Transform bodyRendTf = npcGo.transform.Find("NPCModelSlot/PlaceholderRig/CapsuleBody");
            if (bodyRendTf != null)
            {
                Collider legacyBodyCol = bodyRendTf.GetComponent<Collider>();
                if (legacyBodyCol != null) Object.DestroyImmediate(legacyBodyCol);
                Renderer r = bodyRendTf.GetComponent<Renderer>();
                if (r != null) fallbackRenderers = new[] { r };
            }

            AddInteractionFeedback(target, npcSimple, fallbackRenderers, npcAudio,
                "Click / Select Dr. Vera: Station Guide", new Vector3(0f, 1.24f, 0.34f), mats, mainCam);

            // World-space invisible chest UI button facing +Z (Euler(0, 180, 0)) for Left-Click & Trigger (T).
            Canvas clickCanvas = LabTextStyleHelper.CreateWorldCanvas("NPCClickUiCanvas", target.transform,
                new Vector3(0f, 1.02f, 0.25f), new Vector3(0f, 180f, 0f),
                new Vector2(0.52f, 1.38f), 260, 690, mainCam, true);
            Button uiClickBtn = CreateInvisibleClickButton(clickCanvas.transform, "GuideClickButton", new Vector2(260f, 690f));

            SerializedObject npcSo = new SerializedObject(npcCtrl);
            LabTextStyleHelper.SetObjRef(npcSo, "npcInteractable", npcSimple);
            LabTextStyleHelper.SetObjRef(npcSo, "npcClickButton", uiClickBtn);
            LabTextStyleHelper.SetObjRef(npcSo, "audioSource", npcAudio);
            npcSo.ApplyModifiedPropertiesWithoutUndo();

            return npcCtrl;
        }

        private static void WirePhase4LessonsAndGuides(
            DopingStationController st2Ctrl,
            DopingStationController st3Ctrl,
            NPCController npcCtrl)
        {
            LessonManager lessonManager = Object.FindAnyObjectByType<LessonManager>();
            if (lessonManager == null)
            {
                return;
            }

            // Update Station 1 final completion dialogue so it invites student cleanly into Phase 4's Station 2.
            DialogueLine dlSt1Done = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_St1_Ge_Done.asset");
            if (dlSt1Done != null)
            {
                dlSt1Done.Configure("st1_ge_done",
                    "Station 1 complete! Ge's narrower 0.67 eV gap lets thermal energy break far more bonds (ni ~ 2.4e13 cm^-3 at 300 K) than Si. Teleport to Station 2 (N-Type Doping)!",
                    NPCEmotion.Happy);
                EditorUtility.SetDirty(dlSt1Done);
            }

            // --- NPC Click-to-Guide Sequences (2-line chains: Physics -> Steps) for Stations T, 1, 2, 3 ---
            DialogueLine dlGuideTSteps = CreateDialogue("DL_Guide_T_Steps", "guide_t_steps",
                "Tutorial Steps: 1) Teleport onto Pad T, 2) Grab the blue Test Cube (G), 3) Drop it into the center Socket, 4) Press the Green Button (T/N), and 5) Point your ray at the Tutorial Screen!",
                NPCEmotion.Pointing);
            DialogueLine dlGuideTPhys = CreateDialogue("DL_Guide_T_Physics", "guide_t_physics",
                "Welcome! This Tutorial Pad warms up your VR hands: Grip (G/M) grabs props, Trigger (T) or Left-Click activates UI & buttons, and Thumbstick-Up (I) teleports between benches.",
                NPCEmotion.Explaining, dlGuideTSteps);

            DialogueLine dlGuide1Steps = CreateDialogue("DL_Guide_St1_Steps", "guide_1_steps",
                "Station 1 Steps: 1) Click the Microscope Eyepiece or LOOK IN MICROSCOPE button, 2) Raise Temperature (>= 250 K) to free e-/h+ pairs, and 3) Press MAT: Si -> Ge to compare Ge's 0.67 eV bandgap!",
                NPCEmotion.Pointing);
            DialogueLine dlGuide1Phys = CreateDialogue("DL_Guide_St1_Physics", "guide_1_physics",
                "Intrinsic Silicon (Group IV) has 4 valence electrons per atom locked in covalent bonds at 0 K (insulator). Heating gives electrons thermal energy across Eg (1.12 eV Si vs 0.67 eV Ge), generating equal n = p = ni!",
                NPCEmotion.Explaining, dlGuide1Steps);

            DialogueLine dlGuide2Steps = CreateDialogue("DL_Guide_St2_Steps", "guide_2_steps",
                "Station 2 Steps: 1) Grab the Silicon Wafer & drop it in the left Furnace bay, 2) Drop a Group V Donor vial (P, As, or Sb) into the right bay, and 3) Sweep the ND slider (10^14 to 10^18) then inspect your N-Type Si block!",
                NPCEmotion.Pointing);
            DialogueLine dlGuide2Phys = CreateDialogue("DL_Guide_St2_Physics", "guide_2_physics",
                "N-Type Doping replaces a Si atom with a Group V Donor (5 valence e-: P, As, Sb). Four electrons form covalent bonds; the 5th ionizes into the Conduction Band from Ed (~0.045 eV below Ec), leaving a fixed orange + donor ion and shifting EF UP!",
                NPCEmotion.Explaining, dlGuide2Steps);

            DialogueLine dlGuide3Steps = CreateDialogue("DL_Guide_St3_Steps", "guide_3_steps",
                "Station 3 Steps: 1) Put the Silicon Wafer in the left Furnace bay, 2) Drop a Group III Acceptor vial (B, Al, Ga, or In) into the right bay, and 3) Adjust the NA slider while watching valence electrons hop opposite to holes!",
                NPCEmotion.Pointing);
            DialogueLine dlGuide3Phys = CreateDialogue("DL_Guide_St3_Physics", "guide_3_physics",
                "P-Type Doping adds a Group III Acceptor (3 valence e-: B, Al, Ga, In), creating 1 incomplete covalent bond (hole h+). Neighboring valence electrons hop the OPPOSITE way to fill the vacancy, leaving a fixed teal - ion and shifting EF DOWN toward Ev!",
                NPCEmotion.Explaining, dlGuide3Steps);

            // --- Station 2 Lesson Steps Dialogue ---
            DialogueLine dlSt2Intro = CreateDialogue("DL_Station2_Intro", "st2_intro",
                "Welcome to Station 2: N-Type Doping! Aim your Teleport ray onto Pad 2 in front of the second workbench.",
                NPCEmotion.Encouraging);
            DialogueLine dlSt2TpH20 = CreateDialogue("DL_St2_Tp_H20", "st2_tp_h20",
                "Hint: Aim at floor Pad 2 beside the second table and release Teleport (I).", NPCEmotion.Thinking);
            DialogueLine dlSt2TpH45 = CreateDialogue("DL_St2_Tp_H45", "st2_tp_h45",
                "Teleport right where I am standing at Station 2 to begin N-Type doping!", NPCEmotion.Pointing);
            DialogueLine dlSt2TpDone = CreateDialogue("DL_St2_Tp_Done", "st2_tp_done",
                "Great! Place the Silicon Wafer into the left Furnace bay, then choose a Group V Donor vial (P, As, or Sb) from the shelf for the right bay.",
                NPCEmotion.Happy);

            DialogueLine dlSt2DopeIntro = CreateDialogue("DL_St2_Dope_Intro", "st2_dope_intro",
                "Step 2: Dope Silicon! Grab the Silicon Wafer + a Group V Donor vial (P, As, or Sb) and drop both into the Diffusion Furnace sockets.",
                NPCEmotion.Explaining);
            DialogueLine dlSt2DopeH20 = CreateDialogue("DL_St2_Dope_H20", "st2_dope_h20",
                "Hint: Group V elements (5 valence e-) donate free electrons. Grab Si Wafer + P, As, or Sb (top orange-cap row) and drop into the Furnace.",
                NPCEmotion.Thinking);
            DialogueLine dlSt2DopeH45 = CreateDialogue("DL_St2_Dope_H45", "st2_dope_h45",
                "I am pointing at the Dopant Shelf on the left — pick a Group V donor (P, As, Sb) along with the Si Wafer!",
                NPCEmotion.Pointing);
            DialogueLine dlSt2DopeDone = CreateDialogue("DL_St2_Dope_Done", "st2_dope_done",
                "N-Type Si fabricated! Watch the 3D crystal: the 5th valence e- breaks free (blue), leaving a fixed + donor ion (orange) while Ed sits 0.045 eV below Ec!",
                NPCEmotion.Happy);

            DialogueLine dlSt2NdIntro = CreateDialogue("DL_St2_Nd_Intro", "st2_nd_intro",
                "Step 3: Sweep the ND Doping Slider (or press -0.5/+0.5 DEC) from 10^14 to 10^18 cm^-3 to watch EF shift toward Ec and conductivity rise!",
                NPCEmotion.Explaining);
            DialogueLine dlSt2NdH20 = CreateDialogue("DL_St2_Nd_H20", "st2_nd_h20",
                "Hint: Drag the slider on the right deck or press +0.5 DEC / -0.5 DEC to change ND and observe p = ni^2 / ND.",
                NPCEmotion.Thinking);
            DialogueLine dlSt2NdH45 = CreateDialogue("DL_St2_Nd_H45", "st2_nd_h45",
                "Use the ND Doping Slider I am pointing to on the right of the bench!",
                NPCEmotion.Pointing);
            DialogueLine dlSt2NdDone = CreateDialogue("DL_St2_Nd_Done", "st2_nd_done",
                "Station 2 complete! Increasing ND raises n ~ ND, suppresses minority holes via np = ni^2, and shifts EF closer to Ec. Notice your N-Type Si block on the output tray — Station 3 awaits!",
                NPCEmotion.Happy);

            // --- Station 3 Lesson Steps Dialogue ---
            DialogueLine dlSt3Intro = CreateDialogue("DL_Station3_Intro", "st3_intro",
                "Welcome to Station 3: P-Type Doping! Teleport onto Pad 3 in front of the third workbench.",
                NPCEmotion.Encouraging);
            DialogueLine dlSt3TpH20 = CreateDialogue("DL_St3_Tp_H20", "st3_tp_h20",
                "Hint: Aim at floor Pad 3 in front of the P-Type workbench and release Teleport (I).", NPCEmotion.Thinking);
            DialogueLine dlSt3TpH45 = CreateDialogue("DL_St3_Tp_H45", "st3_tp_h45",
                "Join me at Station 3's Teleport Pad to fabricate P-Type Silicon!", NPCEmotion.Pointing);
            DialogueLine dlSt3TpDone = CreateDialogue("DL_St3_Tp_Done", "st3_tp_done",
                "Ready! Insert the Silicon Wafer in the left Furnace bay, then choose a Group III Acceptor vial (B, Al, Ga, or In).",
                NPCEmotion.Happy);

            DialogueLine dlSt3DopeIntro = CreateDialogue("DL_St3_Dope_Intro", "st3_dope_intro",
                "Step 2: Dope Silicon P-Type! Place the Si Wafer + a Group III Acceptor (B, Al, Ga, or In) into the Diffusion Furnace.",
                NPCEmotion.Explaining);
            DialogueLine dlSt3DopeH20 = CreateDialogue("DL_St3_Dope_H20", "st3_dope_h20",
                "Hint: Group III elements have 3 valence e- (teal caps on upper rack: B, Al, Ga, In). Pair one with the Si Wafer!",
                NPCEmotion.Thinking);
            DialogueLine dlSt3DopeH45 = CreateDialogue("DL_St3_Dope_H45", "st3_dope_h45",
                "Pick a Group III acceptor from the shelf I am pointing toward — avoid Group V donors here!",
                NPCEmotion.Pointing);
            DialogueLine dlSt3DopeDone = CreateDialogue("DL_St3_Dope_Done", "st3_dope_done",
                "P-Type Si fabricated! Look at the incomplete bond, fixed teal - acceptor ion, Ea just above Ev, and valence electrons hopping OPPOSITE to moving red holes!",
                NPCEmotion.Happy);

            DialogueLine dlSt3NaIntro = CreateDialogue("DL_St3_Na_Intro", "st3_na_intro",
                "Step 3: Adjust NA (10^14 to 10^18 cm^-3) on the right deck to watch p ~ NA increase, EF shift DOWN toward Ev, and hole hopping speed up!",
                NPCEmotion.Explaining);
            DialogueLine dlSt3NaH20 = CreateDialogue("DL_St3_Na_H20", "st3_na_h20",
                "Hint: Drag the NA slider or press +0.5 DEC / -0.5 DEC on the right deck.",
                NPCEmotion.Thinking);
            DialogueLine dlSt3NaH45 = CreateDialogue("DL_St3_Na_H45", "st3_na_h45",
                "Adjust the NA slider I am pointing to on the right of Station 3!",
                NPCEmotion.Pointing);
            DialogueLine dlSt3NaDone = CreateDialogue("DL_St3_Na_Done", "st3_na_done",
                "Station 3 complete! You now have both N-Type and P-Type Silicon ready for Phase 5 (P-N Junction Assembly at Station 4)!",
                NPCEmotion.Happy);

            DialogueLine dlSt4Ready = CreateDialogue("DL_Station4_Intro", "st4_intro",
                "Station 4 (P-N Junction & Depletion) unlocks next in Phase 5! Feel free to revisit Stations 1-3 or click me anytime for physics explanations.",
                NPCEmotion.Explaining);

            // Preserve Steps 01..09 (Tutorial Pad + Station 1), append Steps 10..15 + Step 16 Stage 4 stub.
            List<LessonStep> steps = new List<LessonStep>();
            string[] existingStepFiles =
            {
                "Step_01_Teleport",
                "Step_02_Grab",
                "Step_03_Socket",
                "Step_04_Button",
                "Step_05_Screen",
                "Step_06_St1_Teleport",
                "Step_07_St1_Lens",
                "Step_08_St1_Heat",
                "Step_09_St1_ToggleGe"
            };
            for (int i = 0; i < existingStepFiles.Length; i++)
            {
                LessonStep s = AssetDatabase.LoadAssetAtPath<LessonStep>(LessonFolder + "/" + existingStepFiles[i] + ".asset");
                if (s != null) steps.Add(s);
            }

            // Delete old Phase 3 Station 2 placeholder asset if superseded cleanly.
            string oldSt2Placeholder = LessonFolder + "/Step_10_Station2_Ready.asset";
            if (AssetDatabase.LoadAssetAtPath<LessonStep>(oldSt2Placeholder) != null)
            {
                AssetDatabase.DeleteAsset(oldSt2Placeholder);
            }

            steps.Add(CreateStep("Step_10_St2_Teleport", "s2_teleport", "2", "Teleport to Station 2",
                "Teleport onto Pad 2 in front of N-Type Doping.", "station2.teleport",
                dlSt2Intro, dlSt2TpDone, dlSt2TpH20, dlSt2TpH45, 0));

            steps.Add(CreateStep("Step_11_St2_Dope", "s2_dope_n", "2", "Dope Si N-Type (Group V)",
                "Insert Si Wafer + P, As, or Sb in Furnace.", "station2.dope_n",
                dlSt2DopeIntro, dlSt2DopeDone, dlSt2DopeH20, dlSt2DopeH45, 1));

            steps.Add(CreateStep("Step_12_St2_AdjustND", "s2_adjust_nd", "2", "Sweep ND Concentration",
                "Vary ND (10^14-10^18 cm^-3) and check EF & sigma.", "station2.adjust_nd",
                dlSt2NdIntro, dlSt2NdDone, dlSt2NdH20, dlSt2NdH45, 2));

            steps.Add(CreateStep("Step_13_St3_Teleport", "s3_teleport", "3", "Teleport to Station 3",
                "Teleport onto Pad 3 in front of P-Type Doping.", "station3.teleport",
                dlSt3Intro, dlSt3TpDone, dlSt3TpH20, dlSt3TpH45, 0));

            steps.Add(CreateStep("Step_14_St3_Dope", "s3_dope_p", "3", "Dope Si P-Type (Group III)",
                "Insert Si Wafer + B, Al, Ga, or In in Furnace.", "station3.dope_p",
                dlSt3DopeIntro, dlSt3DopeDone, dlSt3DopeH20, dlSt3DopeH45, 1));

            steps.Add(CreateStep("Step_15_St3_AdjustNA", "s3_adjust_na", "3", "Sweep NA Concentration",
                "Vary NA (10^14-10^18 cm^-3) and inspect hole hopping.", "station3.adjust_na",
                dlSt3NaIntro, dlSt3NaDone, dlSt3NaH20, dlSt3NaH45, 2));

            steps.Add(CreateStep("Step_16_Station4_Ready", "s4_ready", "4", "P-N Junction (Phase 5)",
                "Station 4 ready for Phase 5.", "station4.complete",
                dlSt4Ready, null, dlSt4Ready, dlSt4Ready, 0));

            // Rebuild ordered stations array on LessonManager.
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
                GameObject sGo = GameObject.Find(orderedNames[i]);
                if (sGo != null)
                {
                    StationController sc = sGo.GetComponent<StationController>();
                    if (sc != null) controllers.Add(sc);
                }
            }

            SerializedObject lmSo = new SerializedObject(lessonManager);
            LabTextStyleHelper.SetObjectList(lmSo, "steps", steps);
            LabTextStyleHelper.SetObjectArray(lmSo, "stations", controllers.ToArray());
            lmSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lessonManager);

            // Bind NPCController Station Escort + Click-to-Guide across Stations T, 1, 2, 3.
            if (npcCtrl != null)
            {
                StationController stT = GameObject.Find("Station_T_TutorialPad")?.GetComponent<StationController>();
                StationController st1 = GameObject.Find("Station_1_Intrinsic")?.GetComponent<StationController>();

                (string id, StationController sc, DialogueLine guide)[] bindings =
                {
                    ("T", stT, dlGuideTPhys),
                    ("1", st1, dlGuide1Phys),
                    ("2", st2Ctrl, dlGuide2Phys),
                    ("3", st3Ctrl, dlGuide3Phys)
                };

                SerializedObject npcSo = new SerializedObject(npcCtrl);
                LabTextStyleHelper.SetObjRef(npcSo, "lessonManager", lessonManager);

                SerializedProperty guidesProp = npcSo.FindProperty("stationGuides");
                if (guidesProp != null)
                {
                    guidesProp.arraySize = bindings.Length;
                    for (int i = 0; i < bindings.Length; i++)
                    {
                        SerializedProperty elem = guidesProp.GetArrayElementAtIndex(i);
                        StationController sc = bindings[i].sc;
                        TeleportationAnchor anch = sc != null ? sc.GetComponentInChildren<TeleportationAnchor>(true) : null;
                        Transform standPt = sc != null ? sc.NpcStandPoint : null;

                        elem.FindPropertyRelative("stationId").stringValue = bindings[i].id;
                        elem.FindPropertyRelative("teleportAnchor").objectReferenceValue = anch;
                        elem.FindPropertyRelative("standPoint").objectReferenceValue = standPt;
                        elem.FindPropertyRelative("guideDialogue").objectReferenceValue = bindings[i].guide;
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

        private static string RebuildNavMeshAndValidateStandPoints()
        {
            LabNavMeshSurface surface = Object.FindAnyObjectByType<LabNavMeshSurface>();
            if (surface == null)
            {
                return "No LabNavMeshSurface found.";
            }

            SerializedObject surfSo = new SerializedObject(surface);
            NavMeshData navData = surfSo.FindProperty("navMeshData")?.objectReferenceValue as NavMeshData;
            if (navData == null)
            {
                return "LabNavMeshSurface navMeshData missing.";
            }

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

            // Register NavMeshData temporarily in EditMode to sample and validate every Station NPCStandPoint.
            NavMeshDataInstance inst = NavMesh.AddNavMeshData(navData, surface.transform.position, surface.transform.rotation);
            int okCount = 0;
            int totalCount = 0;
            try
            {
                string[] checkStations = { "Station_T_TutorialPad", "Station_1_Intrinsic", "Station_2_NType", "Station_3_PType" };
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

        private static DialogueLine CreateDialogue(string fileName, string id, string text, NPCEmotion emotion,
            DialogueLine next = null, DialogueChoice[] choices = null)
        {
            string path = DialogueFolder + "/" + fileName + ".asset";
            DialogueLine asset = AssetDatabase.LoadAssetAtPath<DialogueLine>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DialogueLine>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Configure(id, text, emotion, next, choices);
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

        private static void BuildWorkbench(Transform parent, float width, float depth, float height, Phase4MatSet mats)
        {
            GameObject bench = new GameObject("Workbench");
            bench.transform.SetParent(parent, false);
            CreateBox("TableTop", bench.transform, new Vector3(0f, height - 0.025f, 0f), new Vector3(width, 0.05f, depth), mats.TableTop, true);

            float legH = height - 0.05f;
            float lx = width * 0.5f - 0.06f;
            float lz = depth * 0.5f - 0.06f;
            CreateBox("Leg_FL", bench.transform, new Vector3(-lx, legH * 0.5f, lz), new Vector3(0.06f, legH, 0.06f), mats.TableFrame, true);
            CreateBox("Leg_FR", bench.transform, new Vector3(lx, legH * 0.5f, lz), new Vector3(0.06f, legH, 0.06f), mats.TableFrame, true);
            CreateBox("Leg_BL", bench.transform, new Vector3(-lx, legH * 0.5f, -lz), new Vector3(0.06f, legH, 0.06f), mats.TableFrame, true);
            CreateBox("Leg_BR", bench.transform, new Vector3(lx, legH * 0.5f, -lz), new Vector3(0.06f, legH, 0.06f), mats.TableFrame, true);
            CreateBox("CrossBar", bench.transform, new Vector3(0f, 0.18f, -lz), new Vector3(width - 0.12f, 0.04f, 0.04f), mats.TableFrame, true);
        }

        private static StationBeacon BuildBeaconParts(Transform parent, string stationId, string title, Phase4MatSet mats, Camera mainCam, out TeleportationAnchor anchor)
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
            padDisc.GetComponent<Renderer>().sharedMaterial = mats.TableFrame;

            GameObject padRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            padRing.name = "AnchorRing";
            padRing.transform.SetParent(anchorGo.transform, false);
            padRing.transform.localPosition = new Vector3(0f, 0.004f, 0f);
            padRing.transform.localScale = new Vector3(0.86f, 0.004f, 0.86f);
            Object.DestroyImmediate(padRing.GetComponent<Collider>());
            Renderer ringRenderer = padRing.GetComponent<Renderer>();
            ringRenderer.sharedMaterial = mats.AccentBase;

            anchor = anchorGo.AddComponent<TeleportationAnchor>();
            anchor.teleportAnchorTransform = spawnPoint.transform;
            anchor.matchOrientation = MatchOrientation.TargetUpAndForward;
            int mask = InteractionLayerMask.GetMask("Teleport");
            anchor.interactionLayers = mask != 0 ? mask : unchecked((int)(1u << 31));

            GameObject floorSign = new GameObject("FloorNumberSign");
            floorSign.transform.SetParent(anchorGo.transform, false);
            floorSign.transform.localPosition = new Vector3(0.58f, 0.01f, 0f);
            GameObject floorTile = CreateBox("Tile", floorSign.transform, Vector3.zero, new Vector3(0.32f, 0.012f, 0.32f), mats.TableFrame, false);
            Object.DestroyImmediate(floorTile.GetComponent<Collider>());
            Canvas floorCanvas = LabTextStyleHelper.CreateWorldCanvas("FloorNumberUI", floorSign.transform,
                new Vector3(0f, 0.009f, 0f), new Vector3(90f, 0f, 0f), new Vector2(0.30f, 0.30f), 320, 320, mainCam, false);
            TMP_Text floorNumText = LabTextStyleHelper.CreateTMPText(floorCanvas.transform, "Number", stationId, 180f,
                Vector2.zero, new Vector2(300f, 300f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject holoRoot = new GameObject("HologramTitle");
            holoRoot.transform.SetParent(parent, false);
            holoRoot.transform.localPosition = new Vector3(0f, 2.38f, 0f);

            GameObject holoBacking = CreateBox("HoloBacking", holoRoot.transform, new Vector3(0f, 0f, -0.014f), new Vector3(1.68f, 0.36f, 0.014f), mats.ScreenPanel, false);
            Object.DestroyImmediate(holoBacking.GetComponent<Collider>());
            GameObject holoBar = CreateBox("HoloAccentBar", holoRoot.transform, new Vector3(0f, -0.195f, -0.010f), new Vector3(1.68f, 0.025f, 0.024f), mats.AccentBase, false);
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

        private static RespawnZone BuildStationRespawnZone(Transform parent, RespawnableItem[] items)
        {
            GameObject zoneGo = new GameObject("TableRespawnZone");
            zoneGo.transform.SetParent(parent, false);
            zoneGo.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            BoxCollider col = zoneGo.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(3.6f, 0.5f, 2.4f);

            RespawnZone zone = zoneGo.AddComponent<RespawnZone>();
            SerializedObject zSo = new SerializedObject(zone);
            LabTextStyleHelper.SetObjectArray(zSo, "managedItems", items);
            zSo.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }

        private static GameObject BuildLockedOverlay(Transform parent, string stationTitle, Phase4MatSet mats, Camera mainCam)
        {
            GameObject overlay = new GameObject("LockedOverlay");
            overlay.transform.SetParent(parent, false);
            overlay.transform.localPosition = new Vector3(0f, 1.24f, 0.50f);

            GameObject dimBox = CreateBox("DimScreen", overlay.transform, Vector3.zero, new Vector3(1.92f, 0.54f, 0.02f), mats.ScreenPanel, false);
            Object.DestroyImmediate(dimBox.GetComponent<Collider>());

            Canvas c = LabTextStyleHelper.CreateWorldCanvas("LockedCanvas", overlay.transform,
                new Vector3(0f, 0f, 0.015f), new Vector3(0f, 180f, 0f),
                new Vector2(1.84f, 0.48f), 920, 240, mainCam, false);
            LabTextStyleHelper.CreateTMPText(c.transform, "LockedText",
                $"LOCKED: Complete previous station to unlock {stationTitle}", 32f,
                Vector2.zero, new Vector2(880f, 210f), new Color(1.0f, 0.68f, 0.32f), TextAlignmentOptions.Center, FontStyles.Bold);
            return overlay;
        }

        private static PhysicalPushButton BuildLabeledButton(
            Transform parent,
            string name,
            Vector3 localPos,
            Vector3 capSize,
            Material capMat,
            string plateLabel,
            string tooltipLabel,
            Phase4MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            out TMP_Text plateText,
            float plateWidth = 0.20f)
        {
            GameObject btnRoot = new GameObject(name);
            btnRoot.transform.SetParent(parent, false);
            btnRoot.transform.localPosition = localPos;

            GameObject housing = CreateBox("Housing", btnRoot.transform,
                new Vector3(0f, 0.01f, 0f), new Vector3(capSize.x + 0.03f, 0.02f, capSize.z + 0.03f), mats.TableFrame, true);
            Object.DestroyImmediate(housing.GetComponent<Collider>());

            GameObject cap = CreateBox("ButtonCap", btnRoot.transform,
                new Vector3(0f, 0.022f + capSize.y * 0.5f, 0f), capSize, capMat, false);
            Renderer capRenderer = cap.GetComponent<Renderer>();
            BoxCollider capCol = cap.GetComponent<BoxCollider>();
            float minColY = Mathf.Max(1.0f, 0.052f / Mathf.Max(0.01f, capSize.y));
            capCol.size = new Vector3(1.1f, minColY, 1.1f);
            capCol.center = new Vector3(0f, -Mathf.Max(0f, (minColY - 1.0f) * 0.5f), 0f);

            Rigidbody capRb = btnRoot.AddComponent<Rigidbody>();
            capRb.isKinematic = true;
            capRb.useGravity = false;

            XRSimpleInteractable simple = btnRoot.AddComponent<XRSimpleInteractable>();
            simple.colliders.Add(capCol);

            var pokeFilter = btnRoot.AddComponent<UnityEngine.XR.Interaction.Toolkit.Filtering.XRPokeFilter>();
            SerializedObject pfSo = new SerializedObject(pokeFilter);
            LabTextStyleHelper.SetObjRef(pfSo, "m_Interactable", simple);
            LabTextStyleHelper.SetObjRef(pfSo, "m_PokeCollider", capCol);
            SerializedProperty cfgProp = pfSo.FindProperty("m_PokeConfiguration");
            if (cfgProp != null)
            {
                SerializedProperty useConst = cfgProp.FindPropertyRelative("m_UseConstant");
                if (useConst != null) useConst.boolValue = true;
                SerializedProperty constVal = cfgProp.FindPropertyRelative("m_ConstantValue");
                if (constVal != null)
                {
                    SerializedProperty dirP = constVal.FindPropertyRelative("m_PokeDirection");
                    if (dirP != null) dirP.intValue = (int)UnityEngine.XR.Interaction.Toolkit.Filtering.PokeAxis.NegativeY;
                    SerializedProperty enP = constVal.FindPropertyRelative("m_EnablePokeAngleThreshold");
                    if (enP != null) enP.boolValue = true;
                    SerializedProperty thP = constVal.FindPropertyRelative("m_PokeAngleThreshold");
                    if (thP != null) thP.floatValue = 45f;
                }
            }
            pfSo.ApplyModifiedPropertiesWithoutUndo();

            PhysicalPushButton pushBtn = btnRoot.AddComponent<PhysicalPushButton>();
            AddInteractionFeedback(btnRoot, simple, new[] { capRenderer }, audioSource, tooltipLabel, new Vector3(0f, 0.11f, 0f), mats, mainCam);

            Vector2 capFootprint = new Vector2(capSize.x + 0.03f, capSize.z + 0.03f);
            Canvas capUiCanvas = LabTextStyleHelper.CreateWorldCanvas("CapUiOverlay", btnRoot.transform,
                new Vector3(0f, 0.025f + capSize.y + 0.004f, 0f), new Vector3(90f, 180f, 0f),
                capFootprint, 180, 180, mainCam, true);
            Button capUiBtn = CreateInvisibleClickButton(capUiCanvas.transform, "ClickTarget", new Vector2(180f, 180f));

            int pxW = Mathf.RoundToInt(plateWidth * 1200f);
            GameObject plate = CreateBox("LabelPlate", btnRoot.transform,
                new Vector3(0f, 0.005f, capSize.z * 0.5f + 0.042f), new Vector3(plateWidth, 0.01f, 0.052f), mats.ScreenPanel, false);
            Object.DestroyImmediate(plate.GetComponent<Collider>());

            Canvas labelCanvas = LabTextStyleHelper.CreateWorldCanvas("Label", btnRoot.transform,
                new Vector3(0f, 0.012f, capSize.z * 0.5f + 0.042f), new Vector3(90f, 180f, 0f),
                new Vector2(plateWidth, 0.052f), pxW, 62, mainCam, true);
            Button labelUiBtn = CreateInvisibleClickButton(labelCanvas.transform, "ClickLabel", new Vector2(pxW, 62f));
            plateText = LabTextStyleHelper.CreateTMPText(labelCanvas.transform, "Text", plateLabel, 24f,
                Vector2.zero, new Vector2(pxW - 14f, 54f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            plateText.enableAutoSizing = true;
            plateText.fontSizeMin = 15f;
            plateText.fontSizeMax = 24f;

            SerializedObject pbSo = new SerializedObject(pushBtn);
            LabTextStyleHelper.SetObjRef(pbSo, "interactable", simple);
            LabTextStyleHelper.SetObjRef(pbSo, "uiRelayButton", capUiBtn);
            LabTextStyleHelper.SetObjRef(pbSo, "uiLabelRelayButton", labelUiBtn);
            LabTextStyleHelper.SetObjRef(pbSo, "capTransform", cap.transform);
            LabTextStyleHelper.SetObjRef(pbSo, "audioSource", audioSource);
            pbSo.ApplyModifiedPropertiesWithoutUndo();

            return pushBtn;
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
            haRt.offsetMin = new Vector2(12f, 0f);
            haRt.offsetMax = new Vector2(-12f, 0f);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform hRt = handle.AddComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(26f, 0f);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;

            Slider slider = go.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = hRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Button CreateInvisibleClickButton(Transform parent, string name, Vector2 sizeDelta)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;
            Image img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.01f);
            img.raycastTarget = true;
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            return btn;
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

        private static void AddInteractionFeedback(
            GameObject target,
            XRBaseInteractable interactable,
            Renderer[] renderers,
            AudioSource audioSource,
            string tooltipLabel,
            Vector3 tooltipLocalPos,
            Phase4MatSet mats,
            Camera mainCam)
        {
            Vector3 parentScale = target.transform.localScale;
            Vector3 invScale = new Vector3(
                Mathf.Abs(parentScale.x) > 0.0001f ? 1f / parentScale.x : 1f,
                Mathf.Abs(parentScale.y) > 0.0001f ? 1f / parentScale.y : 1f,
                Mathf.Abs(parentScale.z) > 0.0001f ? 1f / parentScale.z : 1f);

            GameObject tipRoot = new GameObject("Tooltip");
            tipRoot.transform.SetParent(target.transform, false);
            tipRoot.transform.localPosition = new Vector3(
                tooltipLocalPos.x * invScale.x,
                tooltipLocalPos.y * invScale.y,
                tooltipLocalPos.z * invScale.z);
            tipRoot.transform.localScale = invScale;

            GameObject tipBg = CreateBox("Bg", tipRoot.transform, Vector3.zero, new Vector3(0.42f, 0.075f, 0.008f), mats.ScreenPanel, false);
            Object.DestroyImmediate(tipBg.GetComponent<Collider>());

            // InteractionFeedback.LateUpdate points +Z away from camera, so front (-Z) Canvas sits at -0.006 with zero rotation.
            Canvas tipCanvas = LabTextStyleHelper.CreateWorldCanvas("TipCanvas", tipRoot.transform,
                new Vector3(0f, 0f, -0.006f), Vector3.zero,
                new Vector2(0.40f, 0.068f), 500, 86, mainCam, false);
            TMP_Text tipText = LabTextStyleHelper.CreateTMPText(tipCanvas.transform, "Label", tooltipLabel, 24f,
                Vector2.zero, new Vector2(480f, 76f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            tipText.enableAutoSizing = true;
            tipText.fontSizeMin = 15f;
            tipText.fontSizeMax = 24f;

            InteractionFeedback fb = target.AddComponent<InteractionFeedback>();
            SerializedObject so = new SerializedObject(fb);
            LabTextStyleHelper.SetObjRef(so, "interactable", interactable);
            LabTextStyleHelper.SetObjRef(so, "audioSource", audioSource);
            LabTextStyleHelper.SetObjRef(so, "tooltipObject", tipRoot);
            LabTextStyleHelper.SetObjRef(so, "tooltipText", tipText);
            LabTextStyleHelper.SetStr(so, "tooltipLabel", tooltipLabel);
            LabTextStyleHelper.SetObjectArray(so, "highlightRenderers", renderers);
            so.ApplyModifiedPropertiesWithoutUndo();
            tipRoot.SetActive(false);
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
            if (r != null && mat != null)
            {
                r.sharedMaterial = mat;
            }
            return go;
        }

        private static Camera FindMainCamera()
        {
            Camera cam = Camera.main;
            return cam != null ? cam : Object.FindAnyObjectByType<Camera>();
        }
    }
}
