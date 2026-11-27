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
    /// Builds Phase 5 (Station 4: Junction Formation) self-contained prefab, 3D time-lapse junction chamber,
    /// P-N Junction Jig (supporting Station 2/3 carried blocks + local spares), playback & speed controls,
    /// asymmetric NA/ND sliders, 5 layer toggles, 4 live spatial graphs + Energy Band-Bending diagram,
    /// Station 4 NPC escort/guide bindings, LessonSteps, NavMesh rebake, and crisp DPU=15 world UI.
    /// </summary>
    public static class Phase5JunctionStationBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string PrefabsFolder = "Assets/PNJunctionLab/Prefabs";
        private const string MaterialsFolder = "Assets/PNJunctionLab/Materials";
        private const string DataRoot = "Assets/PNJunctionLab/Data";
        private const string DialogueFolder = DataRoot + "/Dialogue";
        private const string LessonFolder = DataRoot + "/Lessons";
        private const string Station4PrefabPath = PrefabsFolder + "/JunctionFormationStation.prefab";

        private struct Phase5MatSet
        {
            public Material TableTop;
            public Material TableFrame;
            public Material ScreenPanel;
            public Material AccentBase;
            public Material ButtonGreen;
            public Material ButtonAmber;
            public Material ButtonBlue;
            public Material ButtonRed;
            public Material ChamberGlass;
            public Material ElectronBlue;
            public Material HoleRed;
            public Material DonorOrange;
            public Material AcceptorTeal;
            public Material DepletionGold;
            public Material FieldAmber;
            public Material BlockNType;
            public Material BlockPType;
        }

        [MenuItem("PNJunctionLab/Build Phase 5 (Station 4: Junction Formation)")]
        public static void BuildPhase5Menu()
        {
            BuildPhase5();
        }

        public static string BuildPhase5()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                return "Failed to open scene at " + ScenePath;
            }

            LabTextStyleHelper.EnsureNativeGameViewDPI();
            Phase5MatSet mats = CreateOrLoadMaterials();
            Camera mainCam = FindMainCamera();

            GameObject stationsRoot = GameObject.Find("[Stations]");
            if (stationsRoot == null)
            {
                stationsRoot = new GameObject("[Stations]");
            }

            JunctionStationController st4Ctrl = BuildOrReplaceStation4(stationsRoot.transform, mats, mainCam);
            WireStation4LessonsAndNpc(st4Ctrl);
            string navReport = RebuildNavMeshAndValidateStandPoints();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return saved
                ? $"Phase 5 (Station 4) built & saved cleanly. {navReport}"
                : "Failed to save scene after Phase 5 build.";
        }

        private static Phase5MatSet CreateOrLoadMaterials()
        {
            return new Phase5MatSet
            {
                TableTop = LoadOrCreateLit(MaterialsFolder + "/Mat_TableTop.mat", new Color(0.88f, 0.90f, 0.94f, 1f), 0.05f, 0.35f, Color.black, false),
                TableFrame = LoadOrCreateLit(MaterialsFolder + "/Mat_TableFrame.mat", new Color(0.14f, 0.17f, 0.22f, 1f), 0.35f, 0.45f, Color.black, false),
                ScreenPanel = LoadOrCreateLit(MaterialsFolder + "/Mat_ScreenPanel.mat", new Color(0.08f, 0.11f, 0.16f, 1f), 0.1f, 0.3f, Color.black, false),
                AccentBase = LoadOrCreateLit(MaterialsFolder + "/Mat_AccentBase.mat", new Color(0.18f, 0.78f, 0.95f, 1f), 0.1f, 0.5f, new Color(0.06f, 0.30f, 0.42f), false),
                ButtonGreen = LoadOrCreateLit(MaterialsFolder + "/Mat_ButtonGreen.mat", new Color(0.16f, 0.78f, 0.38f, 1f), 0.1f, 0.4f, new Color(0.03f, 0.18f, 0.07f), false),
                ButtonAmber = LoadOrCreateLit(MaterialsFolder + "/Mat_ButtonAmber.mat", new Color(0.92f, 0.58f, 0.16f, 1f), 0.1f, 0.4f, new Color(0.22f, 0.12f, 0.02f), false),
                ButtonBlue = LoadOrCreateLit(MaterialsFolder + "/Mat_ButtonBlue.mat", new Color(0.16f, 0.58f, 0.92f, 1f), 0.1f, 0.45f, new Color(0.03f, 0.16f, 0.28f), false),
                ButtonRed = LoadOrCreateLit(MaterialsFolder + "/Mat_ButtonRed.mat", new Color(0.86f, 0.22f, 0.24f, 1f), 0.05f, 0.42f, new Color(0.24f, 0.04f, 0.04f), false),
                ChamberGlass = LoadOrCreateLit(MaterialsFolder + "/Mat_ChamberGlass.mat", new Color(0.55f, 0.85f, 1.0f, 0.14f), 0.0f, 0.92f, new Color(0.02f, 0.08f, 0.14f), true),
                ElectronBlue = LoadOrCreateLit(MaterialsFolder + "/Mat_ElectronBlue.mat", new Color(0.18f, 0.62f, 1.0f, 1f), 0.1f, 0.6f, new Color(0.18f, 0.50f, 0.98f), false),
                HoleRed = LoadOrCreateLit(MaterialsFolder + "/Mat_HoleRed.mat", new Color(1.0f, 0.30f, 0.34f, 1f), 0.1f, 0.6f, new Color(0.75f, 0.14f, 0.18f), false),
                DonorOrange = LoadOrCreateLit(MaterialsFolder + "/Mat_DonorIonOrange.mat", new Color(0.98f, 0.55f, 0.14f, 1f), 0.15f, 0.65f, new Color(0.55f, 0.24f, 0.02f), false),
                AcceptorTeal = LoadOrCreateLit(MaterialsFolder + "/Mat_AcceptorIonTeal.mat", new Color(0.12f, 0.80f, 0.78f, 1f), 0.15f, 0.65f, new Color(0.04f, 0.38f, 0.38f), false),
                DepletionGold = LoadOrCreateLit(MaterialsFolder + "/Mat_DepletionZoneGlass.mat", new Color(0.98f, 0.78f, 0.24f, 0.18f), 0.0f, 0.40f, new Color(0.26f, 0.18f, 0.04f), true),
                FieldAmber = LoadOrCreateLit(MaterialsFolder + "/Mat_FieldArrowGold.mat", new Color(1.0f, 0.82f, 0.22f, 1f), 0.2f, 0.65f, new Color(0.45f, 0.32f, 0.04f), false),
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

        private static JunctionStationController BuildOrReplaceStation4(Transform stationsParent, Phase5MatSet mats, Camera mainCam)
        {
            GameObject existing = GameObject.Find("Station_4_Junction");
            Vector3 worldPos = new Vector3(1.80f, 0f, 4.00f);
            Quaternion worldRot = Quaternion.Euler(0f, 180f, 0f);
            if (existing != null)
            {
                worldPos = existing.transform.position;
                worldRot = existing.transform.rotation;
                Object.DestroyImmediate(existing);
            }

            GameObject root = new GameObject("Station_4_Junction");
            AudioSource stationAudio = root.AddComponent<AudioSource>();
            stationAudio.spatialBlend = 1f;
            stationAudio.playOnAwake = false;

            // Workbench (2.72 m wide x 0.98 m deep x 0.90 m high).
            BuildWorkbench(root.transform, 2.72f, 0.98f, 0.90f, mats);

            // Teleport Anchor + Hologram Title + Floor Pad.
            StationBeacon beacon = BuildBeaconParts(root.transform, "4", "Junction Formation", mats, mainCam, out TeleportationAnchor anchor);

            // Dedicated NPC Stand Point beside workbench right edge (world ~ (3.22, 0, 2.95)).
            GameObject npcStand = new GameObject("NPCStandPoint_4");
            npcStand.transform.SetParent(root.transform, false);
            npcStand.transform.localPosition = new Vector3(-1.42f, 0f, 1.05f);
            npcStand.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Upper Consoles: Left (4 Live Graphs) + Right (Energy Band-Bending Diagram & Live Electrostatics).
            JunctionGraphsAndBandsView graphsView = BuildGraphsAndBandsConsoles(
                root.transform, mats, mainCam,
                out TMP_Text jigStatusText,
                out TMP_Text electrostaticsText,
                out TMP_Text asymmetricText);

            // Tabletop Left: Spare P & N Blocks Tray + Return Props Button.
            GameObject spareTray = BuildSpareBlocksTray(
                root.transform, mats, stationAudio, mainCam,
                out RespawnableItem sparePRespawn,
                out RespawnableItem spareNRespawn);

            PhysicalPushButton returnPropsBtn = BuildLabeledButton(
                root.transform, "ReturnPropsButton", new Vector3(1.18f, 0.90f, 0.35f),
                new Vector3(0.085f, 0.036f, 0.075f), mats.ButtonAmber,
                "RETURN PROPS", "Return P/N Blocks to Tray", mats, stationAudio, mainCam, out _, 0.19f);

            PhysicalPushButton resetBtn = BuildLabeledButton(
                root.transform, "ResetStationButton", new Vector3(-1.20f, 0.90f, 0.35f),
                new Vector3(0.085f, 0.036f, 0.075f), mats.ButtonRed,
                "RESET STATION", "Reset Junction Formation", mats, stationAudio, mainCam, out _, 0.19f);

            // Tabletop Center: Junction Jig (Left=P Socket, Right=N Socket) + JOIN Button + 3D Junction Chamber.
            GameObject jigRoot = BuildJunctionJig(
                root.transform, mats, stationAudio, mainCam,
                out XRSocketInteractor pSocket,
                out SocketRingFeedback pFeedback,
                out XRSocketInteractor nSocket,
                out SocketRingFeedback nFeedback,
                out PhysicalPushButton joinBtn);

            JunctionSimulationView simView = BuildJunctionSimulationChamber(root.transform, mats, mainCam);

            // Tabletop Right: Time-Lapse Playback Controls + Asymmetric NA/ND Sliders + 5 Layer Toggles.
            GameObject playbackDeck = BuildPlaybackDeck(
                root.transform, mats, stationAudio, mainCam,
                out PhysicalPushButton playPauseBtn,
                out TMP_Text playPauseLbl,
                out PhysicalPushButton stepBtn,
                out PhysicalPushButton speedBtn,
                out TMP_Text speedLbl);

            GameObject slidersDeck = BuildAsymmetricDopingDeck(
                root.transform, mats, stationAudio, mainCam,
                out Slider naSlider,
                out Slider ndSlider,
                out PhysicalPushButton naDownBtn,
                out PhysicalPushButton naUpBtn,
                out PhysicalPushButton ndDownBtn,
                out PhysicalPushButton ndUpBtn,
                out TMP_Text naValTxt,
                out TMP_Text ndValTxt);

            BuildLayerToggleBar(
                root.transform, mats, stationAudio, mainCam,
                out PhysicalPushButton togHolesBtn, out TMP_Text togHolesLbl,
                out PhysicalPushButton togElecBtn, out TMP_Text togElecLbl,
                out PhysicalPushButton togIonsBtn, out TMP_Text togIonsLbl,
                out PhysicalPushButton togFieldBtn, out TMP_Text togFieldLbl,
                out PhysicalPushButton togCurrBtn, out TMP_Text togCurrLbl);

            // RespawnZone catches fallen P/N blocks.
            RespawnZone respawnZone = BuildStationRespawnZone(root.transform, new[] { sparePRespawn, spareNRespawn });
            GameObject lockedOverlay = BuildLockedOverlay(root.transform, "Junction Formation", mats, mainCam);

            // Wire JunctionStationController.
            JunctionStationController ctrl = root.AddComponent<JunctionStationController>();
            SerializedObject so = new SerializedObject(ctrl);
            LabTextStyleHelper.SetStr(so, "stationId", "4");
            LabTextStyleHelper.SetStr(so, "stationTitle", "Junction Formation");
            LabTextStyleHelper.SetObjRef(so, "stationBeacon", beacon);
            LabTextStyleHelper.SetObjRef(so, "npcStandPoint", npcStand.transform);
            LabTextStyleHelper.SetObjRef(so, "lockedOverlay", lockedOverlay);
            LabTextStyleHelper.SetObjectArray(so, "hintTargets", new[]
            {
                anchor.transform,
                jigRoot.transform,
                playbackDeck.transform,
                slidersDeck.transform
            });

            LabTextStyleHelper.SetObjRef(so, "stationAnchor", anchor);
            LabTextStyleHelper.SetObjRef(so, "pSideSocket", pSocket);
            LabTextStyleHelper.SetObjRef(so, "pSocketFeedback", pFeedback);
            LabTextStyleHelper.SetObjRef(so, "nSideSocket", nSocket);
            LabTextStyleHelper.SetObjRef(so, "nSocketFeedback", nFeedback);
            LabTextStyleHelper.SetObjRef(so, "sparePBlockRespawn", sparePRespawn);
            LabTextStyleHelper.SetObjRef(so, "spareNBlockRespawn", spareNRespawn);
            LabTextStyleHelper.SetObjRef(so, "joinButton", joinBtn);
            LabTextStyleHelper.SetObjRef(so, "respawnZone", respawnZone);
            LabTextStyleHelper.SetObjRef(so, "audioSource", stationAudio);

            LabTextStyleHelper.SetObjRef(so, "playPauseButton", playPauseBtn);
            LabTextStyleHelper.SetObjRef(so, "playPauseButtonLabel", playPauseLbl);
            LabTextStyleHelper.SetObjRef(so, "stepButton", stepBtn);
            LabTextStyleHelper.SetObjRef(so, "speedButton", speedBtn);
            LabTextStyleHelper.SetObjRef(so, "speedButtonLabel", speedLbl);
            LabTextStyleHelper.SetObjRef(so, "resetStationButton", resetBtn);
            LabTextStyleHelper.SetObjRef(so, "returnPropsButton", returnPropsBtn);

            LabTextStyleHelper.SetObjRef(so, "naExponentSlider", naSlider);
            LabTextStyleHelper.SetObjRef(so, "ndExponentSlider", ndSlider);
            LabTextStyleHelper.SetObjRef(so, "naDownButton", naDownBtn);
            LabTextStyleHelper.SetObjRef(so, "naUpButton", naUpBtn);
            LabTextStyleHelper.SetObjRef(so, "ndDownButton", ndDownBtn);
            LabTextStyleHelper.SetObjRef(so, "ndUpButton", ndUpBtn);

            LabTextStyleHelper.SetObjRef(so, "toggleHolesButton", togHolesBtn);
            LabTextStyleHelper.SetObjRef(so, "toggleHolesLabel", togHolesLbl);
            LabTextStyleHelper.SetObjRef(so, "toggleElectronsButton", togElecBtn);
            LabTextStyleHelper.SetObjRef(so, "toggleElectronsLabel", togElecLbl);
            LabTextStyleHelper.SetObjRef(so, "toggleIonsButton", togIonsBtn);
            LabTextStyleHelper.SetObjRef(so, "toggleIonsLabel", togIonsLbl);
            LabTextStyleHelper.SetObjRef(so, "toggleFieldButton", togFieldBtn);
            LabTextStyleHelper.SetObjRef(so, "toggleFieldLabel", togFieldLbl);
            LabTextStyleHelper.SetObjRef(so, "toggleCurrentsButton", togCurrBtn);
            LabTextStyleHelper.SetObjRef(so, "toggleCurrentsLabel", togCurrLbl);

            LabTextStyleHelper.SetObjRef(so, "simulationView", simView);
            LabTextStyleHelper.SetObjRef(so, "graphsAndBandsView", graphsView);
            LabTextStyleHelper.SetObjRef(so, "jigStatusText", jigStatusText);
            LabTextStyleHelper.SetObjRef(so, "electrostaticsReadoutText", electrostaticsText);
            LabTextStyleHelper.SetObjRef(so, "asymmetricSummaryText", asymmetricText);
            LabTextStyleHelper.SetObjRef(so, "naValueText", naValTxt);
            LabTextStyleHelper.SetObjRef(so, "ndValueText", ndValTxt);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAssetAndConnect(root, Station4PrefabPath, InteractionMode.AutomatedAction);
            root.transform.SetParent(stationsParent, false);
            root.transform.SetPositionAndRotation(worldPos, worldRot);
            return ctrl;
        }

        private static JunctionGraphsAndBandsView BuildGraphsAndBandsConsoles(
            Transform parent,
            Phase5MatSet mats,
            Camera mainCam,
            out TMP_Text jigStatusText,
            out TMP_Text electrostaticsText,
            out TMP_Text asymmetricText)
        {
            GameObject graphsRoot = new GameObject("GraphsAndBandsConsoles");
            graphsRoot.transform.SetParent(parent, false);

            // LEFT CONSOLE: 4 Live Graphs (Carrier n/p, Space Charge rho, E-Field, Potential V).
            GameObject leftPanel = new GameObject("FourLiveGraphsPanel");
            leftPanel.transform.SetParent(graphsRoot.transform, false);
            leftPanel.transform.localPosition = new Vector3(0.68f, 1.76f, -0.40f);

            CreateBox("PostL_L", graphsRoot.transform, new Vector3(1.16f, 1.15f, -0.41f), new Vector3(0.04f, 0.52f, 0.04f), mats.TableFrame, true);
            CreateBox("PostL_R", graphsRoot.transform, new Vector3(0.20f, 1.15f, -0.41f), new Vector3(0.04f, 0.52f, 0.04f), mats.TableFrame, true);
            CreateBox("Frame", leftPanel.transform, Vector3.zero, new Vector3(1.32f, 0.74f, 0.036f), mats.TableFrame, true);
            GameObject leftScreen = CreateBox("Backing", leftPanel.transform, new Vector3(0f, 0f, 0.014f), new Vector3(1.26f, 0.68f, 0.018f), mats.ScreenPanel, false);
            Object.DestroyImmediate(leftScreen.GetComponent<Collider>());

            Canvas leftCanvas = LabTextStyleHelper.CreateWorldCanvas("FourGraphsCanvas", leftPanel.transform,
                new Vector3(0f, 0f, 0.028f), new Vector3(0f, 180f, 0f),
                new Vector2(1.26f, 0.68f), 1260, 680, mainCam, false);

            LabTextStyleHelper.CreateTMPText(leftCanvas.transform, "MainTitle",
                "LIVE P-N JUNCTION SPATIAL PROFILES (P LEFT | N RIGHT)", 24f,
                new Vector2(0f, 310f), new Vector2(1200f, 36f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            int segCount = JunctionGraphsAndBandsView.SampleCount - 1;
            // Quadrant 1 (Top-Left): Carriers n(x), p(x).
            RectTransform q1 = BuildPlotBox(leftCanvas.transform, "Plot_Carriers", new Vector2(-305f, 145f), new Vector2(540f, 220f),
                "1. CARRIER CONCENTRATION n(x), p(x)", out TMP_Text carrierLbl);
            RectTransform[] eCurve = CreateCurveSegments(q1, "ElecSeg", segCount, 3.2f, new Color(0.24f, 0.74f, 1.0f, 1f));
            RectTransform[] hCurve = CreateCurveSegments(q1, "HoleSeg", segCount, 3.2f, new Color(1.0f, 0.34f, 0.36f, 1f));

            // Quadrant 2 (Top-Right): Space Charge Density rho(x).
            RectTransform q2 = BuildPlotBox(leftCanvas.transform, "Plot_ChargeDensity", new Vector2(305f, 145f), new Vector2(540f, 220f),
                "2. SPACE CHARGE DENSITY rho(x)", out TMP_Text chargeLbl);
            RectTransform[] rhoBars = CreateVerticalBars(q2, "RhoBar", JunctionGraphsAndBandsView.SampleCount, 386f, 11f, new Color(0.98f, 0.58f, 0.14f, 0.92f));

            // Quadrant 3 (Bottom-Left): Electric Field E(x).
            RectTransform q3 = BuildPlotBox(leftCanvas.transform, "Plot_ElectricField", new Vector2(-305f, -155f), new Vector2(540f, 220f),
                "3. ELECTRIC FIELD E(x)", out TMP_Text fieldLbl);
            RectTransform[] eFieldCurve = CreateCurveSegments(q3, "FieldSeg", segCount, 4.0f, new Color(1.0f, 0.84f, 0.25f, 1f));

            // Quadrant 4 (Bottom-Right): Electrostatic Potential V(x).
            RectTransform q4 = BuildPlotBox(leftCanvas.transform, "Plot_Potential", new Vector2(305f, -155f), new Vector2(540f, 220f),
                "4. ELECTROSTATIC POTENTIAL V(x)", out TMP_Text potLbl);
            RectTransform[] potCurve = CreateCurveSegments(q4, "PotSeg", segCount, 4.0f, new Color(0.32f, 0.95f, 0.62f, 1f));

            // RIGHT CONSOLE: Energy Band-Bending Diagram + Live Electrostatics Metrics.
            GameObject rightPanel = new GameObject("BandBendingAndMetricsPanel");
            rightPanel.transform.SetParent(graphsRoot.transform, false);
            rightPanel.transform.localPosition = new Vector3(-0.68f, 1.76f, -0.40f);

            CreateBox("PostR_L", graphsRoot.transform, new Vector3(-0.20f, 1.15f, -0.41f), new Vector3(0.04f, 0.52f, 0.04f), mats.TableFrame, true);
            CreateBox("PostR_R", graphsRoot.transform, new Vector3(-1.16f, 1.15f, -0.41f), new Vector3(0.04f, 0.52f, 0.04f), mats.TableFrame, true);
            CreateBox("Frame", rightPanel.transform, Vector3.zero, new Vector3(1.32f, 0.74f, 0.036f), mats.TableFrame, true);
            GameObject rightScreen = CreateBox("Backing", rightPanel.transform, new Vector3(0f, 0f, 0.014f), new Vector3(1.26f, 0.68f, 0.018f), mats.ScreenPanel, false);
            Object.DestroyImmediate(rightScreen.GetComponent<Collider>());

            Canvas rightCanvas = LabTextStyleHelper.CreateWorldCanvas("BandAndMetricsCanvas", rightPanel.transform,
                new Vector3(0f, 0f, 0.028f), new Vector3(0f, 180f, 0f),
                new Vector2(1.26f, 0.68f), 1260, 680, mainCam, false);

            LabTextStyleHelper.CreateTMPText(rightCanvas.transform, "MainTitle",
                "ENERGY BAND-BENDING DIAGRAM & ELECTROSTATICS (Not to scale)", 24f,
                new Vector2(0f, 308f), new Vector2(1200f, 36f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Band plot box (top half of right console).
            GameObject bandBox = new GameObject("BandPlotArea");
            bandBox.transform.SetParent(rightCanvas.transform, false);
            RectTransform bandRt = bandBox.AddComponent<RectTransform>();
            bandRt.anchoredPosition = new Vector2(0f, 145f);
            bandRt.sizeDelta = new Vector2(1140f, 244f);
            CreateUIImageRect(bandBox.transform, "Bg", Vector2.zero, new Vector2(1140f, 244f), new Color(0.05f, 0.08f, 0.13f, 0.96f));
            CreateUIImageRect(bandBox.transform, "CenterAxisX0", Vector2.zero, new Vector2(2f, 210f), new Color(1f, 1f, 1f, 0.20f));

            RectTransform[] ecSegs = CreateCurveSegments(bandRt, "EcSeg", segCount, 4.5f, new Color(0.24f, 0.78f, 1.0f, 1f));
            RectTransform[] evSegs = CreateCurveSegments(bandRt, "EvSeg", segCount, 4.5f, new Color(1.0f, 0.36f, 0.38f, 1f));
            RectTransform[] efSegs = CreateCurveSegments(bandRt, "EfSeg", segCount, 3.2f, new Color(1.0f, 0.92f, 0.25f, 0.95f));

            LabTextStyleHelper.CreateTMPText(bandBox.transform, "PLabel", "P-SIDE (Ev close to EF)", 17f,
                new Vector2(-385f, 102f), new Vector2(320f, 26f), new Color(1.0f, 0.58f, 0.58f), TextAlignmentOptions.Left, FontStyles.Bold);
            LabTextStyleHelper.CreateTMPText(bandBox.transform, "NLabel", "N-SIDE (Ec close to EF)", 17f,
                new Vector2(385f, -102f), new Vector2(320f, 26f), new Color(0.45f, 0.85f, 1.0f), TextAlignmentOptions.Right, FontStyles.Bold);
            TMP_Text bandBarrierLbl = LabTextStyleHelper.CreateTMPText(bandBox.transform, "BarrierLbl",
                "qV0 = 0.691 eV", 18f, new Vector2(290f, 100f), new Vector2(490f, 26f), new Color(1.0f, 0.88f, 0.42f), TextAlignmentOptions.Right, FontStyles.Bold);
            TMP_Text bandFooterLbl = LabTextStyleHelper.CreateTMPText(rightCanvas.transform, "BandFooter",
                "Equilibrium: EF is FLAT across P & N", 20f,
                new Vector2(0f, -2f), new Vector2(1180f, 34f), new Color(1.0f, 0.94f, 0.38f), TextAlignmentOptions.Center, FontStyles.Bold);

            CreateUIImageRect(rightCanvas.transform, "Divider", new Vector2(0f, -34f), new Vector2(1160f, 3f), new Color(0.24f, 0.72f, 0.92f, 0.7f));

            jigStatusText = LabTextStyleHelper.CreateTMPText(rightCanvas.transform, "JigStatus",
                "JUNCTION JIG: Insert P-Type & N-Type Blocks -> Press JOIN", 23f,
                new Vector2(0f, -78f), new Vector2(1180f, 52f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            electrostaticsText = LabTextStyleHelper.CreateTMPText(rightCanvas.transform, "ElectrostaticsReadout",
                "V0 = 0.691 V | W = 0.42 um", 22f,
                new Vector2(0f, -162f), new Vector2(1180f, 84f), new Color(0.55f, 0.96f, 0.74f), TextAlignmentOptions.Center, FontStyles.Bold);

            asymmetricText = LabTextStyleHelper.CreateTMPText(rightCanvas.transform, "AsymmetricSummary",
                "Symmetric Doping (NA ~ ND): xp ~ xn", 21f,
                new Vector2(0f, -256f), new Vector2(1180f, 54f), new Color(1.0f, 0.85f, 0.45f), TextAlignmentOptions.Center, FontStyles.Bold);

            JunctionGraphsAndBandsView view = graphsRoot.AddComponent<JunctionGraphsAndBandsView>();
            SerializedObject gSo = new SerializedObject(view);
            LabTextStyleHelper.SetObjectArray(gSo, "electronCurveSegments", eCurve);
            LabTextStyleHelper.SetObjectArray(gSo, "holeCurveSegments", hCurve);
            LabTextStyleHelper.SetObjRef(gSo, "carrierAxisLabel", carrierLbl);
            LabTextStyleHelper.SetObjectArray(gSo, "chargeDensityBars", rhoBars);
            LabTextStyleHelper.SetObjRef(gSo, "chargeAxisLabel", chargeLbl);
            LabTextStyleHelper.SetObjectArray(gSo, "fieldCurveSegments", eFieldCurve);
            LabTextStyleHelper.SetObjRef(gSo, "fieldAxisLabel", fieldLbl);
            LabTextStyleHelper.SetObjectArray(gSo, "potentialCurveSegments", potCurve);
            LabTextStyleHelper.SetObjRef(gSo, "potentialAxisLabel", potLbl);
            LabTextStyleHelper.SetObjectArray(gSo, "ecBandSegments", ecSegs);
            LabTextStyleHelper.SetObjectArray(gSo, "evBandSegments", evSegs);
            LabTextStyleHelper.SetObjectArray(gSo, "efBandSegments", efSegs);
            LabTextStyleHelper.SetObjRef(gSo, "bandDiagramFooterLabel", bandFooterLbl);
            LabTextStyleHelper.SetObjRef(gSo, "bandBarrierLabel", bandBarrierLbl);
            LabTextStyleHelper.SetFloat(gSo, "smallPlotWidth", 386f);
            LabTextStyleHelper.SetFloat(gSo, "smallPlotHalfHeight", 68f);
            LabTextStyleHelper.SetFloat(gSo, "bandPlotWidth", 880f);
            LabTextStyleHelper.SetFloat(gSo, "bandPlotHalfHeight", 96f);
            gSo.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static RectTransform BuildPlotBox(Transform parent, string name, Vector2 pos, Vector2 size, string title, out TMP_Text footerAxisLabel)
        {
            GameObject box = new GameObject(name);
            box.transform.SetParent(parent, false);
            RectTransform rt = box.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            CreateUIImageRect(box.transform, "Bg", Vector2.zero, size, new Color(0.05f, 0.08f, 0.13f, 0.95f));
            CreateUIImageRect(box.transform, "BaselineX", new Vector2(0f, -6f), new Vector2(size.x - 100f, 2f), new Color(1f, 1f, 1f, 0.22f));
            CreateUIImageRect(box.transform, "JunctionCenterY", new Vector2(0f, -6f), new Vector2(2f, 138f), new Color(1f, 1f, 1f, 0.22f));

            LabTextStyleHelper.CreateTMPText(box.transform, "Title", title, 18f,
                new Vector2(0f, 92f), new Vector2(size.x - 16f, 28f), new Color(0.86f, 0.94f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);
            LabTextStyleHelper.CreateTMPText(box.transform, "SideP", "P", 14.5f,
                new Vector2(-(size.x * 0.5f - 28f), -4f), new Vector2(36f, 24f), new Color(1.0f, 0.56f, 0.58f), TextAlignmentOptions.Center, FontStyles.Bold);
            LabTextStyleHelper.CreateTMPText(box.transform, "SideN", "N", 14.5f,
                new Vector2(size.x * 0.5f - 28f, -4f), new Vector2(36f, 24f), new Color(0.45f, 0.85f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            footerAxisLabel = LabTextStyleHelper.CreateTMPText(box.transform, "AxisFooter", "", 15.5f,
                new Vector2(0f, -92f), new Vector2(size.x - 16f, 26f), new Color(0.95f, 0.90f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject plotArea = new GameObject("PlotInner");
            plotArea.transform.SetParent(box.transform, false);
            RectTransform inner = plotArea.AddComponent<RectTransform>();
            inner.anchoredPosition = new Vector2(0f, -4f);
            inner.sizeDelta = new Vector2(400f, 144f);
            return inner;
        }

        private static RectTransform[] CreateCurveSegments(Transform parent, string prefix, int count, float thickness, Color color)
        {
            RectTransform[] arr = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = CreateUIImageRect(parent, prefix + "_" + i, Vector2.zero, new Vector2(14f, thickness), color);
            }
            return arr;
        }

        private static RectTransform[] CreateVerticalBars(Transform parent, string prefix, int count, float width, float barWidth, Color color)
        {
            RectTransform[] arr = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                float x = ((i / (float)Mathf.Max(1, count - 1)) - 0.5f) * width;
                arr[i] = CreateUIImageRect(parent, prefix + "_" + i, new Vector2(x, 0f), new Vector2(barWidth, 40f), color);
            }
            return arr;
        }

        private static GameObject BuildSpareBlocksTray(
            Transform parent,
            Phase5MatSet mats,
            AudioSource stationAudio,
            Camera mainCam,
            out RespawnableItem pBlockRespawn,
            out RespawnableItem nBlockRespawn)
        {
            GameObject tray = new GameObject("SpareBlocksTray");
            tray.transform.SetParent(parent, false);
            tray.transform.localPosition = new Vector3(0.96f, 0.90f, 0.02f);

            CreateBox("TrayDeck", tray.transform, new Vector3(0f, 0.014f, 0f), new Vector3(0.42f, 0.028f, 0.44f), mats.TableFrame, true);
            Canvas lblCanvas = LabTextStyleHelper.CreateWorldCanvas("TrayTitleCanvas", tray.transform,
                new Vector3(0f, 0.032f, -0.16f), new Vector3(90f, 180f, 0f),
                new Vector2(0.40f, 0.08f), 400, 80, mainCam, false);
            LabTextStyleHelper.CreateTMPText(lblCanvas.transform, "Title",
                "BLOCK TRAY\n(Station 2/3 or Local)", 20f, Vector2.zero, new Vector2(384f, 72f),
                new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            pBlockRespawn = BuildGrabbableSemiconductorBlock(
                tray.transform, "Spare_PTypeSiliconBlock", new Vector3(0.095f, 0.068f, 0.055f),
                ExtrinsicDopingKind.PType, "B", mats, stationAudio, mainCam);

            nBlockRespawn = BuildGrabbableSemiconductorBlock(
                tray.transform, "Spare_NTypeSiliconBlock", new Vector3(-0.095f, 0.068f, 0.055f),
                ExtrinsicDopingKind.NType, "P", mats, stationAudio, mainCam);

            return tray;
        }

        private static RespawnableItem BuildGrabbableSemiconductorBlock(
            Transform parent,
            string objName,
            Vector3 localPos,
            ExtrinsicDopingKind kind,
            string sym,
            Phase5MatSet mats,
            AudioSource audioSource,
            Camera mainCam)
        {
            GameObject marker = new GameObject(objName + "_Home");
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPos;

            GameObject go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            Material bodyMat = kind == ExtrinsicDopingKind.PType ? mats.BlockPType : mats.BlockNType;
            GameObject mesh = CreateBox("Mesh", go.transform, Vector3.zero, new Vector3(0.13f, 0.08f, 0.13f), bodyMat, false);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            Renderer rend = mesh.GetComponent<Renderer>();

            Canvas c = LabTextStyleHelper.CreateWorldCanvas("BlockLabelCanvas", go.transform,
                new Vector3(0f, 0.043f, 0f), new Vector3(90f, 180f, 0f),
                new Vector2(0.122f, 0.122f), 190, 190, mainCam, false);
            TMP_Text lbl = LabTextStyleHelper.CreateTMPText(c.transform, "Label",
                kind == ExtrinsicDopingKind.PType ? "P-TYPE Si" : "N-TYPE Si", 24f,
                Vector2.zero, new Vector2(176f, 176f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            BoxCollider col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.135f, 0.082f, 0.135f);

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.35f;
            rb.linearDamping = 1f;
            rb.angularDamping = 2f;

            XRGrabInteractable grab = go.AddComponent<XRGrabInteractable>();
            grab.colliders.Add(col);
            grab.useDynamicAttach = true;
            grab.throwOnDetach = false;

            string tip = kind == ExtrinsicDopingKind.PType
                ? "P-Type Si Block -> Place in Left Jig Bay"
                : "N-Type Si Block -> Place in Right Jig Bay";
            AddInteractionFeedback(go, grab, new[] { rend }, audioSource, tip, new Vector3(0f, 0.12f, 0f), mats, mainCam);

            RespawnableItem resp = go.AddComponent<RespawnableItem>();
            SerializedObject rpSo = new SerializedObject(resp);
            LabTextStyleHelper.SetObjRef(rpSo, "homeAnchor", marker.transform);
            LabTextStyleHelper.SetObjRef(rpSo, "audioSource", audioSource);
            rpSo.ApplyModifiedPropertiesWithoutUndo();

            ExtrinsicSemiconductorBlock blk = go.AddComponent<ExtrinsicSemiconductorBlock>();
            SerializedObject bSo = new SerializedObject(blk);
            SerializedProperty dkProp = bSo.FindProperty("dopingKind");
            if (dkProp != null) dkProp.intValue = (int)kind;
            LabTextStyleHelper.SetStr(bSo, "dopantSymbol", sym);
            LabTextStyleHelper.SetObjRef(bSo, "grabInteractable", grab);
            LabTextStyleHelper.SetObjRef(bSo, "respawnableItem", resp);
            LabTextStyleHelper.SetObjRef(bSo, "blockRenderer", rend);
            LabTextStyleHelper.SetObjRef(bSo, "labelText", lbl);
            bSo.ApplyModifiedPropertiesWithoutUndo();

            return resp;
        }

        private static GameObject BuildJunctionJig(
            Transform parent,
            Phase5MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            out XRSocketInteractor pSocket,
            out SocketRingFeedback pFeedback,
            out XRSocketInteractor nSocket,
            out SocketRingFeedback nFeedback,
            out PhysicalPushButton joinButton)
        {
            GameObject jig = new GameObject("PNJunctionJig");
            jig.transform.SetParent(parent, false);
            jig.transform.localPosition = new Vector3(0.26f, 0.90f, 0.22f);

            CreateBox("JigDeck", jig.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.58f, 0.03f, 0.32f), mats.TableFrame, true);

            // Left socket bay (+0.18 in local X -> player's LEFT) = P-Type.
            BuildJigSocketBay(jig.transform, "PSideSocketBay", new Vector3(0.18f, 0.03f, -0.04f),
                "1. P-TYPE", mats.BlockPType, mats, audioSource, mainCam, out pSocket, out pFeedback);

            // Right socket bay (-0.18 in local X -> player's RIGHT) = N-Type.
            BuildJigSocketBay(jig.transform, "NSideSocketBay", new Vector3(-0.18f, 0.03f, -0.04f),
                "2. N-TYPE", mats.BlockNType, mats, audioSource, mainCam, out nSocket, out nFeedback);

            // Prominent green JOIN push button centered in front between sockets.
            joinButton = BuildLabeledButton(
                jig.transform, "JoinButton", new Vector3(0f, 0.03f, 0.11f),
                new Vector3(0.14f, 0.042f, 0.075f), mats.ButtonGreen,
                "JOIN P-N", "Join P & N Blocks to Form Junction", mats, audioSource, mainCam, out _, 0.18f);

            return jig;
        }

        private static void BuildJigSocketBay(
            Transform parent,
            string bayName,
            Vector3 localPos,
            string bayLabel,
            Material accentMat,
            Phase5MatSet mats,
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
            ring.transform.localScale = new Vector3(0.18f, 0.006f, 0.18f);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            Renderer ringRend = ring.GetComponent<Renderer>();
            ringRend.sharedMaterial = accentMat;

            Canvas lblCanvas = LabTextStyleHelper.CreateWorldCanvas("BayLabel", bay.transform,
                new Vector3(0f, 0.015f, -0.11f), new Vector3(90f, 180f, 0f),
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

        private static JunctionSimulationView BuildJunctionSimulationChamber(Transform parent, Phase5MatSet mats, Camera mainCam)
        {
            GameObject stage = new GameObject("JunctionSimulationChamber");
            stage.transform.SetParent(parent, false);
            stage.transform.localPosition = new Vector3(0.24f, 0.96f, -0.10f);

            CreateBox("StageBase", stage.transform, new Vector3(0f, 0.012f, 0f), new Vector3(0.76f, 0.024f, 0.24f), mats.TableFrame, true);
            CreateBox("PostL", stage.transform, new Vector3(0.35f, 0.18f, -0.09f), new Vector3(0.024f, 0.34f, 0.024f), mats.TableFrame, true);
            CreateBox("PostR", stage.transform, new Vector3(-0.35f, 0.18f, -0.09f), new Vector3(0.024f, 0.34f, 0.024f), mats.TableFrame, true);

            // Top Banner ("Not to scale") + Bottom Narrative Strip raised cleanly above Diffusion tag.
            GameObject bannerBack = CreateBox("BannerBack", stage.transform, new Vector3(0f, 0.425f, -0.02f), new Vector3(0.76f, 0.088f, 0.016f), mats.ScreenPanel, false);
            Object.DestroyImmediate(bannerBack.GetComponent<Collider>());
            Canvas topCanvas = LabTextStyleHelper.CreateWorldCanvas("ChamberHeaderCanvas", stage.transform,
                new Vector3(0f, 0.425f, -0.010f), new Vector3(0f, 180f, 0f),
                new Vector2(0.74f, 0.082f), 840, 94, mainCam, false);
            TMP_Text scaleHdr = LabTextStyleHelper.CreateTMPText(topCanvas.transform, "ScaleHeader",
                "P-N JUNCTION SIMULATION (Not to scale)", 22f,
                new Vector2(0f, 20f), new Vector2(820f, 34f), new Color(1.0f, 0.86f, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);
            TMP_Text stageNarrative = LabTextStyleHelper.CreateTMPText(topCanvas.transform, "StageNarrative",
                "Socket P-Type & N-Type blocks in Jig below, then press JOIN!", 17.5f,
                new Vector2(0f, -20f), new Vector2(820f, 36f), Color.white, TextAlignmentOptions.Center, FontStyles.Normal);

            // Standby Empty Chamber Glass.
            GameObject standby = new GameObject("StandbyChamber");
            standby.transform.SetParent(stage.transform, false);
            standby.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            GameObject glass = CreateBox("GlassBox", standby.transform, Vector3.zero, new Vector3(0.68f, 0.22f, 0.16f), mats.ChamberGlass, false);
            Object.DestroyImmediate(glass.GetComponent<Collider>());

            // Active Visual Root.
            GameObject visRoot = new GameObject("ActiveJunctionVisual");
            visRoot.transform.SetParent(stage.transform, false);
            visRoot.transform.localPosition = new Vector3(0f, 0.18f, 0f);

            GameObject outerGlass = CreateBox("OuterGlass", visRoot.transform, Vector3.zero, new Vector3(0.68f, 0.22f, 0.16f), mats.ChamberGlass, false);
            Object.DestroyImmediate(outerGlass.GetComponent<Collider>());

            GameObject metLine = CreateBox("MetallurgicalJunctionLine", visRoot.transform, Vector3.zero, new Vector3(0.006f, 0.22f, 0.17f), mats.AccentBase, false);
            Object.DestroyImmediate(metLine.GetComponent<Collider>());

            GameObject depBox = CreateBox("DepletionRegionBox", visRoot.transform, Vector3.zero, new Vector3(0.14f, 0.22f, 0.16f), mats.DepletionGold, false);
            Object.DestroyImmediate(depBox.GetComponent<Collider>());
            GameObject depLineP = CreateBox("DepletionBoundaryP", visRoot.transform, Vector3.zero, new Vector3(0.005f, 0.22f, 0.168f), mats.AcceptorTeal, false);
            Object.DestroyImmediate(depLineP.GetComponent<Collider>());
            GameObject depLineN = CreateBox("DepletionBoundaryN", visRoot.transform, Vector3.zero, new Vector3(0.005f, 0.22f, 0.168f), mats.DonorOrange, false);
            Object.DestroyImmediate(depLineN.GetComponent<Collider>());

            // Layer Roots.
            GameObject holesLayer = new GameObject("Layer_Holes"); holesLayer.transform.SetParent(visRoot.transform, false);
            GameObject electronsLayer = new GameObject("Layer_Electrons"); electronsLayer.transform.SetParent(visRoot.transform, false);
            GameObject ionsLayer = new GameObject("Layer_FixedIons"); ionsLayer.transform.SetParent(visRoot.transform, false);
            GameObject fieldLayer = new GameObject("Layer_EFieldArrows"); fieldLayer.transform.SetParent(visRoot.transform, false);
            GameObject currentsLayer = new GameObject("Layer_CurrentArrows"); currentsLayer.transform.SetParent(visRoot.transform, false);

            // Fixed Ions (16 Teal - on P left +X, 16 Orange + on N right -X).
            Renderer[] accRends = new Renderer[16];
            Renderer[] donRends = new Renderer[16];
            for (int i = 0; i < 16; i++)
            {
                int row = i / 4;
                int col = i % 4;
                float y = -0.072f + row * 0.048f;
                float xFrac = (col + 0.45f) / 4.2f;

                GameObject acc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                acc.name = "AcceptorIon_" + i;
                acc.transform.SetParent(ionsLayer.transform, false);
                acc.transform.localPosition = new Vector3(xFrac * 0.31f, y, (col % 2 == 0 ? 0.025f : -0.025f));
                acc.transform.localScale = Vector3.one * 0.018f;
                Object.DestroyImmediate(acc.GetComponent<Collider>());
                accRends[i] = acc.GetComponent<Renderer>();
                accRends[i].sharedMaterial = mats.AcceptorTeal;

                GameObject don = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                don.name = "DonorIon_" + i;
                don.transform.SetParent(ionsLayer.transform, false);
                don.transform.localPosition = new Vector3(-xFrac * 0.31f, y, (col % 2 == 0 ? -0.025f : 0.025f));
                don.transform.localScale = Vector3.one * 0.018f;
                Object.DestroyImmediate(don.GetComponent<Collider>());
                donRends[i] = don.GetComponent<Renderer>();
                donRends[i].sharedMaterial = mats.DonorOrange;
            }

            // Pooled Holes (24 red) & Electrons (24 blue).
            Transform[] hTfs = new Transform[24];
            Transform[] eTfs = new Transform[24];
            for (int i = 0; i < 24; i++)
            {
                GameObject h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                h.name = "Hole_" + i;
                h.transform.SetParent(holesLayer.transform, false);
                h.transform.localScale = Vector3.one * 0.017f;
                Object.DestroyImmediate(h.GetComponent<Collider>());
                h.GetComponent<Renderer>().sharedMaterial = mats.HoleRed;
                hTfs[i] = h.transform;

                GameObject e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                e.name = "Electron_" + i;
                e.transform.SetParent(electronsLayer.transform, false);
                e.transform.localScale = Vector3.one * 0.016f;
                Object.DestroyImmediate(e.GetComponent<Collider>());
                e.GetComponent<Renderer>().sharedMaterial = mats.ElectronBlue;
                eTfs[i] = e.transform;
            }

            // 3D Electric Field Arrows (N -> P, pointing +X local left).
            Transform[] eFieldArrows = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                eFieldArrows[i] = Build3DArrow("EFieldArrow_" + i, fieldLayer.transform, Vector3.right, 0.12f, mats.FieldAmber);
            }

            // Top & Bottom Current Balance Arrows inside Currents Layer.
            Transform diffArrow = Build3DArrow("DiffusionArrow", currentsLayer.transform, Vector3.left, 0.24f, mats.ElectronBlue);
            diffArrow.localPosition = new Vector3(0f, 0.132f, 0.06f);
            Transform driftArrow = Build3DArrow("DriftArrow", currentsLayer.transform, Vector3.right, 0.24f, mats.DonorOrange);
            driftArrow.localPosition = new Vector3(0f, -0.132f, 0.06f);

            Canvas diffTagCanvas = LabTextStyleHelper.CreateWorldCanvas("DiffTagCanvas", currentsLayer.transform,
                new Vector3(0f, 0.150f, 0.06f), new Vector3(0f, 180f, 0f),
                new Vector2(0.46f, 0.036f), 460, 36, mainCam, false);
            LabTextStyleHelper.CreateTMPText(diffTagCanvas.transform, "DiffTxt", "DIFFUSION (I: P -> N)", 18f,
                Vector2.zero, new Vector2(440f, 32f), new Color(0.45f, 0.85f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject balStrip = CreateBox("BalanceStrip", stage.transform, new Vector3(0f, 0.036f, 0.125f), new Vector3(0.68f, 0.042f, 0.012f), mats.ScreenPanel, false);
            balStrip.transform.localRotation = Quaternion.Euler(48f, 180f, 0f);
            Object.DestroyImmediate(balStrip.GetComponent<Collider>());
            Canvas curLblCanvas = LabTextStyleHelper.CreateWorldCanvas("CurrentBalanceCanvas", currentsLayer.transform,
                new Vector3(0f, -0.136f, 0.133f), new Vector3(48f, 180f, 0f),
                new Vector2(0.66f, 0.040f), 660, 42, mainCam, false);
            TMP_Text curBalLbl = LabTextStyleHelper.CreateTMPText(curLblCanvas.transform, "Balance",
                "t=0: Sharp P|N Concentration Step", 20f, Vector2.zero, new Vector2(660f, 44f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            JunctionSimulationView view = stage.AddComponent<JunctionSimulationView>();
            SerializedObject vSo = new SerializedObject(view);
            LabTextStyleHelper.SetObjRef(vSo, "visualRoot", visRoot);
            LabTextStyleHelper.SetObjRef(vSo, "standbyRoot", standby);
            LabTextStyleHelper.SetObjRef(vSo, "scaleHeaderText", scaleHdr);
            LabTextStyleHelper.SetObjRef(vSo, "stageNarrativeText", stageNarrative);
            LabTextStyleHelper.SetObjRef(vSo, "depletionRegionBox", depBox.transform);
            LabTextStyleHelper.SetObjRef(vSo, "depletionBoundaryLineP", depLineP.transform);
            LabTextStyleHelper.SetObjRef(vSo, "depletionBoundaryLineN", depLineN.transform);
            LabTextStyleHelper.SetObjRef(vSo, "holesLayerRoot", holesLayer);
            LabTextStyleHelper.SetObjRef(vSo, "electronsLayerRoot", electronsLayer);
            LabTextStyleHelper.SetObjRef(vSo, "fixedIonsLayerRoot", ionsLayer);
            LabTextStyleHelper.SetObjRef(vSo, "eFieldLayerRoot", fieldLayer);
            LabTextStyleHelper.SetObjRef(vSo, "currentsLayerRoot", currentsLayer);
            LabTextStyleHelper.SetObjectArray(vSo, "holeTransforms", hTfs);
            LabTextStyleHelper.SetObjectArray(vSo, "electronTransforms", eTfs);
            LabTextStyleHelper.SetObjectArray(vSo, "acceptorIonRenderers", accRends);
            LabTextStyleHelper.SetObjectArray(vSo, "donorIonRenderers", donRends);
            LabTextStyleHelper.SetObjectArray(vSo, "eFieldArrowRoots", eFieldArrows);
            LabTextStyleHelper.SetObjRef(vSo, "diffusionArrowRoot", diffArrow);
            LabTextStyleHelper.SetObjRef(vSo, "driftArrowRoot", driftArrow);
            LabTextStyleHelper.SetObjRef(vSo, "currentBalanceLabel", curBalLbl);
            vSo.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static Transform Build3DArrow(string name, Transform parent, Vector3 localForwardDir, float length, Material mat)
        {
            GameObject arrow = new GameObject(name);
            arrow.transform.SetParent(parent, false);
            // Align local +Z along localForwardDir, then Build3DArrow callers scale Z or X cleanly;
            // rotate shaft+head relative to +X or left/right! Here keep localRotation=identity and orient child along dir:
            Quaternion rot = Quaternion.FromToRotation(Vector3.forward, localForwardDir.normalized);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(arrow.transform, false);
            shaft.transform.localRotation = rot * Quaternion.Euler(90f, 0f, 0f);
            shaft.transform.localPosition = localForwardDir.normalized * (-length * 0.1f);
            shaft.transform.localScale = new Vector3(0.009f, length * 0.42f, 0.009f);
            Object.DestroyImmediate(shaft.GetComponent<Collider>());
            shaft.GetComponent<Renderer>().sharedMaterial = mat;

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(arrow.transform, false);
            head.transform.localPosition = localForwardDir.normalized * (length * 0.34f);
            head.transform.localRotation = rot * Quaternion.Euler(0f, 45f, 45f);
            head.transform.localScale = Vector3.one * 0.022f;
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = mat;

            return arrow.transform;
        }

        private static GameObject BuildPlaybackDeck(
            Transform parent,
            Phase5MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            out PhysicalPushButton playPauseBtn,
            out TMP_Text playPauseLbl,
            out PhysicalPushButton stepBtn,
            out PhysicalPushButton speedBtn,
            out TMP_Text speedLbl)
        {
            GameObject deck = new GameObject("PlaybackControlDeck");
            deck.transform.SetParent(parent, false);
            deck.transform.localPosition = new Vector3(-0.38f, 0.90f, 0.06f);

            CreateBox("Plinth", deck.transform, new Vector3(0f, 0.014f, 0f), new Vector3(0.40f, 0.028f, 0.38f), mats.TableFrame, true);
            Canvas c = LabTextStyleHelper.CreateWorldCanvas("PlaybackTitle", deck.transform,
                new Vector3(0f, 0.032f, -0.145f), new Vector3(90f, 180f, 0f),
                new Vector2(0.38f, 0.06f), 380, 60, mainCam, false);
            LabTextStyleHelper.CreateTMPText(c.transform, "Title", "TIME-LAPSE PLAYBACK", 22f,
                Vector2.zero, new Vector2(364f, 52f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            playPauseBtn = BuildLabeledButton(deck.transform, "PlayPauseButton",
                new Vector3(0.105f, 0.028f, -0.02f), new Vector3(0.09f, 0.036f, 0.07f),
                mats.ButtonGreen, "PLAY", "Play / Pause Time-Lapse", mats, audioSource, mainCam, out playPauseLbl, 0.14f);

            stepBtn = BuildLabeledButton(deck.transform, "StepButton",
                new Vector3(-0.105f, 0.028f, -0.02f), new Vector3(0.09f, 0.036f, 0.07f),
                mats.ButtonBlue, "STEP", "Step Through 5 Stages", mats, audioSource, mainCam, out _, 0.14f);

            speedBtn = BuildLabeledButton(deck.transform, "SpeedButton",
                new Vector3(0f, 0.028f, 0.115f), new Vector3(0.14f, 0.036f, 0.065f),
                mats.ButtonAmber, "SPEED 1x", "Cycle Speed (0.5x / 1x / 2x)", mats, audioSource, mainCam, out speedLbl, 0.20f);

            return deck;
        }

        private static GameObject BuildAsymmetricDopingDeck(
            Transform parent,
            Phase5MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            out Slider naSlider,
            out Slider ndSlider,
            out PhysicalPushButton naDownBtn,
            out PhysicalPushButton naUpBtn,
            out PhysicalPushButton ndDownBtn,
            out PhysicalPushButton ndUpBtn,
            out TMP_Text naValueText,
            out TMP_Text ndValueText)
        {
            GameObject deck = new GameObject("AsymmetricDopingDeck");
            deck.transform.SetParent(parent, false);
            deck.transform.localPosition = new Vector3(-0.92f, 0.90f, 0.02f);

            CreateBox("Plinth", deck.transform, new Vector3(0f, 0.014f, 0f), new Vector3(0.54f, 0.028f, 0.44f), mats.TableFrame, true);

            // Slanted interactive dual slider face cleanly readable & draggable.
            GameObject sliderPedestal = new GameObject("SlidersConsole");
            sliderPedestal.transform.SetParent(deck.transform, false);
            sliderPedestal.transform.localPosition = new Vector3(0f, 0.105f, -0.08f);
            sliderPedestal.transform.localRotation = Quaternion.Euler(42f, 180f, 0f);
            GameObject sBack = CreateBox("Backing", sliderPedestal.transform, Vector3.zero, new Vector3(0.50f, 0.19f, 0.018f), mats.ScreenPanel, false);
            Object.DestroyImmediate(sBack.GetComponent<Collider>());

            Canvas sliderCanvas = LabTextStyleHelper.CreateWorldCanvas("DopingSlidersCanvas", sliderPedestal.transform,
                new Vector3(0f, 0f, -0.012f), Vector3.zero,
                new Vector2(0.48f, 0.18f), 480, 180, mainCam, true);

            naValueText = LabTextStyleHelper.CreateTMPText(sliderCanvas.transform, "NaVal", "P NA = 1.0E+16", 19f,
                new Vector2(0f, 66f), new Vector2(450f, 26f), new Color(1.0f, 0.56f, 0.58f), TextAlignmentOptions.Center, FontStyles.Bold);
            naSlider = CreateUISlider(sliderCanvas.transform, "NaExponentSlider", new Vector2(0f, 34f), new Vector2(420f, 34f), new Color(0.92f, 0.30f, 0.34f, 1f));
            naSlider.minValue = 14f; naSlider.maxValue = 18f; naSlider.value = 16f;

            ndValueText = LabTextStyleHelper.CreateTMPText(sliderCanvas.transform, "NdVal", "N ND = 1.0E+16", 19f,
                new Vector2(0f, -14f), new Vector2(450f, 26f), new Color(0.38f, 0.82f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);
            ndSlider = CreateUISlider(sliderCanvas.transform, "NdExponentSlider", new Vector2(0f, -46f), new Vector2(420f, 34f), new Color(0.20f, 0.70f, 0.96f, 1f));
            ndSlider.minValue = 14f; ndSlider.maxValue = 18f; ndSlider.value = 16f;

            // Physical +/- step buttons cleanly below pedestal on the front deck.
            naDownBtn = BuildLabeledButton(deck.transform, "NaDownBtn", new Vector3(0.19f, 0.028f, 0.11f), new Vector3(0.068f, 0.032f, 0.055f),
                mats.BlockPType, "NA-", "Decrease P-Side NA", mats, audioSource, mainCam, out _, 0.11f);
            naUpBtn = BuildLabeledButton(deck.transform, "NaUpBtn", new Vector3(0.065f, 0.028f, 0.11f), new Vector3(0.068f, 0.032f, 0.055f),
                mats.BlockPType, "NA+", "Increase P-Side NA", mats, audioSource, mainCam, out _, 0.11f);
            ndDownBtn = BuildLabeledButton(deck.transform, "NdDownBtn", new Vector3(-0.065f, 0.028f, 0.11f), new Vector3(0.068f, 0.032f, 0.055f),
                mats.BlockNType, "ND-", "Decrease N-Side ND", mats, audioSource, mainCam, out _, 0.11f);
            ndUpBtn = BuildLabeledButton(deck.transform, "NdUpBtn", new Vector3(-0.19f, 0.028f, 0.11f), new Vector3(0.068f, 0.032f, 0.055f),
                mats.BlockNType, "ND+", "Increase N-Side ND", mats, audioSource, mainCam, out _, 0.11f);

            return deck;
        }

        private static void BuildLayerToggleBar(
            Transform parent,
            Phase5MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            out PhysicalPushButton holesBtn, out TMP_Text holesLbl,
            out PhysicalPushButton elecBtn, out TMP_Text elecLbl,
            out PhysicalPushButton ionsBtn, out TMP_Text ionsLbl,
            out PhysicalPushButton fieldBtn, out TMP_Text fieldLbl,
            out PhysicalPushButton currBtn, out TMP_Text currLbl)
        {
            GameObject bar = new GameObject("LayerTogglesBar");
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = new Vector3(-0.62f, 0.90f, 0.35f);

            CreateBox("BarHousing", bar.transform, new Vector3(0f, 0.01f, 0f), new Vector3(0.86f, 0.02f, 0.14f), mats.TableFrame, true);

            holesBtn = BuildLabeledButton(bar.transform, "Toggle_Holes", new Vector3(0.32f, 0.02f, -0.01f), new Vector3(0.068f, 0.032f, 0.055f),
                mats.HoleRed, "HOLES\n[ON]", "Toggle Holes Layer", mats, audioSource, mainCam, out holesLbl, 0.13f);
            elecBtn = BuildLabeledButton(bar.transform, "Toggle_Electrons", new Vector3(0.16f, 0.02f, -0.01f), new Vector3(0.068f, 0.032f, 0.055f),
                mats.ElectronBlue, "e-\n[ON]", "Toggle Electrons Layer", mats, audioSource, mainCam, out elecLbl, 0.13f);
            ionsBtn = BuildLabeledButton(bar.transform, "Toggle_Ions", new Vector3(0.00f, 0.02f, -0.01f), new Vector3(0.068f, 0.032f, 0.055f),
                mats.AcceptorTeal, "IONS\n[ON]", "Toggle Fixed Ions Layer", mats, audioSource, mainCam, out ionsLbl, 0.13f);
            fieldBtn = BuildLabeledButton(bar.transform, "Toggle_EField", new Vector3(-0.16f, 0.02f, -0.01f), new Vector3(0.068f, 0.032f, 0.055f),
                mats.FieldAmber, "E-FIELD\n[ON]", "Toggle E-Field Arrows", mats, audioSource, mainCam, out fieldLbl, 0.13f);
            currBtn = BuildLabeledButton(bar.transform, "Toggle_Currents", new Vector3(-0.32f, 0.02f, -0.01f), new Vector3(0.068f, 0.032f, 0.055f),
                mats.ButtonGreen, "CURR\n[ON]", "Toggle Drift & Diffusion Arrows", mats, audioSource, mainCam, out currLbl, 0.13f);
        }

        private static void WireStation4LessonsAndNpc(JunctionStationController st4Ctrl)
        {
            LessonManager lessonManager = Object.FindAnyObjectByType<LessonManager>();
            if (lessonManager == null)
            {
                return;
            }

            DialogueLine dlSt3Done = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_St3_Na_Done.asset");
            if (dlSt3Done != null)
            {
                dlSt3Done.Configure("st3_na_done",
                    "Station 3 complete! Carry your P-Type & N-Type Silicon blocks (or use Station 4's tray) to Station 4: Junction Formation!",
                    NPCEmotion.Happy);
                EditorUtility.SetDirty(dlSt3Done);
            }

            // NPC Click-to-Guide Sequence for Station 4.
            DialogueLine dlGuide4Steps = CreateDialogue("DL_Guide_St4_Steps", "guide_4_steps",
                "Station 4 Steps: 1) Put a P-Type block (left bay) & N-Type block (right bay) into the Junction Jig and press JOIN, 2) Play or Step through Equilibrium, and 3) Sweep NA vs ND sliders & Layer Toggles!",
                NPCEmotion.Pointing);
            DialogueLine dlGuide4Phys = CreateDialogue("DL_Guide_St4_Physics", "guide_4_physics",
                "Joining P & N creates a steep gradient: electrons diffuse N->P and holes P->N, recombining to uncover fixed + orange donors & - teal acceptors! The N->P E-field causes Drift opposing Diffusion until net current = 0 (W expands deeper into the lighter side).",
                NPCEmotion.Explaining, dlGuide4Steps);

            // Station 4 Lesson Steps Dialogue.
            DialogueLine dlSt4Intro = CreateDialogue("DL_Station4_Intro", "st4_intro",
                "Welcome to Station 4: P-N Junction Formation! Teleport onto Pad 4 in front of Workbench 4.",
                NPCEmotion.Encouraging);
            DialogueLine dlSt4TpH20 = CreateDialogue("DL_St4_Tp_H20", "st4_tp_h20",
                "Hint: Aim at floor Pad 4 beside the Junction Formation bench and release Teleport (I).", NPCEmotion.Thinking);
            DialogueLine dlSt4TpH45 = CreateDialogue("DL_St4_Tp_H45", "st4_tp_h45",
                "Walk or teleport over to Station 4 right beside me!", NPCEmotion.Pointing);
            DialogueLine dlSt4TpDone = CreateDialogue("DL_St4_Tp_Done", "st4_tp_done",
                "Great! Place a P-Type block into the left socket and an N-Type block into the right socket (carried from Stations 2 & 3 or from the tray), then press JOIN!",
                NPCEmotion.Happy);

            DialogueLine dlSt4JoinIntro = CreateDialogue("DL_St4_Join_Intro", "st4_join_intro",
                "Step 2: Socket P-Type (left) and N-Type (right) into the Junction Jig and press the green JOIN button!",
                NPCEmotion.Explaining);
            DialogueLine dlSt4JoinH20 = CreateDialogue("DL_St4_Join_H20", "st4_join_h20",
                "Hint: Grab the P-Type block (left socket) and N-Type block (right socket), drop them into the glowing rings, then press JOIN.",
                NPCEmotion.Thinking);
            DialogueLine dlSt4JoinH45 = CreateDialogue("DL_St4_Join_H45", "st4_join_h45",
                "Place both P and N blocks into the Junction Jig I am pointing toward and press JOIN!",
                NPCEmotion.Pointing);
            DialogueLine dlSt4JoinDone = CreateDialogue("DL_St4_Join_Done", "st4_join_done",
                "Junction joined! Watch diffusion (e- N->P, h+ P->N) recombine near x=0, uncovering fixed + / - ions and building Ebi!",
                NPCEmotion.Happy);

            DialogueLine dlSt4EqIntro = CreateDialogue("DL_St4_Eq_Intro", "st4_eq_intro",
                "Step 3: Let the time-lapse play (or press STEP / SPEED) through Equilibrium, when Drift balances Diffusion (net current = 0) and EF aligns flat!",
                NPCEmotion.Explaining);
            DialogueLine dlSt4EqH20 = CreateDialogue("DL_St4_Eq_H20", "st4_eq_h20",
                "Hint: Wait for the playback to reach Equilibrium (or press STEP repeatedly on the Playback Deck).",
                NPCEmotion.Thinking);
            DialogueLine dlSt4EqH45 = CreateDialogue("DL_St4_Eq_H45", "st4_eq_h45",
                "Use the Time-Lapse Playback buttons I am pointing to until Stage 5 (Equilibrium) is reached!",
                NPCEmotion.Pointing);
            DialogueLine dlSt4EqDone = CreateDialogue("DL_St4_Eq_Done", "st4_eq_done",
                "Equilibrium achieved! Notice net current = 0, bands bend by qV0 (~0.69 eV at 1e16), EF is flat, and all 4 graphs (n/p, rho, E, V) stabilized!",
                NPCEmotion.Happy);

            DialogueLine dlSt4AsymIntro = CreateDialogue("DL_St4_Asym_Intro", "st4_asym_intro",
                "Step 4: Vary P-side NA vs N-side ND (e.g., NA=1e15 vs ND=1e17) on the sliders and toggle layers to see the depletion zone extend farther into the lighter-doped side!",
                NPCEmotion.Explaining);
            DialogueLine dlSt4AsymH20 = CreateDialogue("DL_St4_Asym_H20", "st4_asym_h20",
                "Hint: Drag the NA or ND slider (or press NA-/NA+/ND-/ND+) so NA and ND differ, and inspect xp vs xn!",
                NPCEmotion.Thinking);
            DialogueLine dlSt4AsymH45 = CreateDialogue("DL_St4_Asym_H45", "st4_asym_h45",
                "Adjust the NA vs ND doping sliders I am pointing toward to see asymmetric depletion penetration!",
                NPCEmotion.Pointing);
            DialogueLine dlSt4AsymDone = CreateDialogue("DL_St4_Asym_Done", "st4_asym_done",
                "Station 4 complete! Notice xn = W * NA/(NA+ND) extends deeper into the lighter-doped side! Station 5 (Forward & Reverse Biasing) awaits in Phase 6!",
                NPCEmotion.Happy);

            DialogueLine dlSt5Ready = CreateDialogue("DL_Station5_Intro", "st5_intro",
                "Station 5 (Biasing) unlocks next in Phase 6! Feel free to test Station 4 layer toggles or ask me anytime.",
                NPCEmotion.Explaining);

            // Preserve Steps 01..15, replace old Step_16_Station4_Ready placeholder cleanly.
            List<LessonStep> steps = new List<LessonStep>();
            string[] prevStepFiles =
            {
                "Step_01_Teleport", "Step_02_Grab", "Step_03_Socket", "Step_04_Button", "Step_05_Screen",
                "Step_06_St1_Teleport", "Step_07_St1_Lens", "Step_08_St1_Heat", "Step_09_St1_ToggleGe",
                "Step_10_St2_Teleport", "Step_11_St2_Dope", "Step_12_St2_AdjustND",
                "Step_13_St3_Teleport", "Step_14_St3_Dope", "Step_15_St3_AdjustNA"
            };
            for (int i = 0; i < prevStepFiles.Length; i++)
            {
                LessonStep s = AssetDatabase.LoadAssetAtPath<LessonStep>(LessonFolder + "/" + prevStepFiles[i] + ".asset");
                if (s != null) steps.Add(s);
            }

            string oldSt4Placeholder = LessonFolder + "/Step_16_Station4_Ready.asset";
            if (AssetDatabase.LoadAssetAtPath<LessonStep>(oldSt4Placeholder) != null)
            {
                AssetDatabase.DeleteAsset(oldSt4Placeholder);
            }

            steps.Add(CreateStep("Step_16_St4_Teleport", "s4_teleport", "4", "Teleport to Station 4",
                "Teleport onto Pad 4 in front of Junction Formation.", "station4.teleport",
                dlSt4Intro, dlSt4TpDone, dlSt4TpH20, dlSt4TpH45, 0));

            steps.Add(CreateStep("Step_17_St4_Join", "s4_join", "4", "Socket & Join P-N Blocks",
                "Place P & N blocks in Junction Jig and press JOIN.", "station4.join",
                dlSt4JoinIntro, dlSt4JoinDone, dlSt4JoinH20, dlSt4JoinH45, 1));

            steps.Add(CreateStep("Step_18_St4_TimeLapse", "s4_equilibrium", "4", "Reach Junction Equilibrium",
                "Play or Step time-lapse to Equilibrium (I_net = 0).", "station4.equilibrium",
                dlSt4EqIntro, dlSt4EqDone, dlSt4EqH20, dlSt4EqH45, 2));

            steps.Add(CreateStep("Step_19_St4_Asymmetric", "s4_asymmetric", "4", "Test Asymmetric NA vs ND",
                "Set NA != ND to compare xp vs xn and band bending.", "station4.asymmetric",
                dlSt4AsymIntro, dlSt4AsymDone, dlSt4AsymH20, dlSt4AsymH45, 3));

            steps.Add(CreateStep("Step_20_Station5_Ready", "s5_ready", "5", "Biasing (Phase 6)",
                "Station 5 ready for Phase 6.", "station5.complete",
                dlSt5Ready, null, dlSt5Ready, dlSt5Ready, 0));

            // Rebuild ordered StationController array cleanly.
            string[] orderedNames =
            {
                "Station_T_TutorialPad", "Station_1_Intrinsic", "Station_2_NType", "Station_3_PType",
                "Station_4_Junction", "Station_5_Biasing", "Station_6_IV", "Station_A_Assessment"
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

            // Bind Station 4 into NPCController.stationGuides alongside T, 1, 2, 3.
            NPCController npcCtrl = Object.FindAnyObjectByType<NPCController>();
            if (npcCtrl != null)
            {
                StationController stT = GameObject.Find("Station_T_TutorialPad")?.GetComponent<StationController>();
                StationController st1 = GameObject.Find("Station_1_Intrinsic")?.GetComponent<StationController>();
                StationController st2 = GameObject.Find("Station_2_NType")?.GetComponent<StationController>();
                StationController st3 = GameObject.Find("Station_3_PType")?.GetComponent<StationController>();

                DialogueLine dlGT = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Guide_T_Physics.asset");
                DialogueLine dlG1 = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Guide_St1_Physics.asset");
                DialogueLine dlG2 = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Guide_St2_Physics.asset");
                DialogueLine dlG3 = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Guide_St3_Physics.asset");

                (string id, StationController sc, DialogueLine guide)[] bindings =
                {
                    ("T", stT, dlGT),
                    ("1", st1, dlG1),
                    ("2", st2, dlG2),
                    ("3", st3, dlG3),
                    ("4", st4Ctrl, dlGuide4Phys)
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
                    "Station_3_PType", "Station_4_Junction"
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

        private static void BuildWorkbench(Transform parent, float width, float depth, float height, Phase5MatSet mats)
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

        private static StationBeacon BuildBeaconParts(Transform parent, string stationId, string title, Phase5MatSet mats, Camera mainCam, out TeleportationAnchor anchor)
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
            holoRoot.transform.localPosition = new Vector3(0f, 2.50f, 0f);

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
            col.size = new Vector3(3.8f, 0.5f, 2.5f);

            RespawnZone zone = zoneGo.AddComponent<RespawnZone>();
            SerializedObject zSo = new SerializedObject(zone);
            LabTextStyleHelper.SetObjectArray(zSo, "managedItems", items);
            zSo.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }

        private static GameObject BuildLockedOverlay(Transform parent, string stationTitle, Phase5MatSet mats, Camera mainCam)
        {
            GameObject overlay = new GameObject("LockedOverlay");
            overlay.transform.SetParent(parent, false);
            overlay.transform.localPosition = new Vector3(0f, 1.24f, 0.52f);

            GameObject dimBox = CreateBox("DimScreen", overlay.transform, Vector3.zero, new Vector3(2.04f, 0.54f, 0.02f), mats.ScreenPanel, false);
            Object.DestroyImmediate(dimBox.GetComponent<Collider>());

            Canvas c = LabTextStyleHelper.CreateWorldCanvas("LockedCanvas", overlay.transform,
                new Vector3(0f, 0f, 0.015f), new Vector3(0f, 180f, 0f),
                new Vector2(1.96f, 0.48f), 960, 240, mainCam, false);
            LabTextStyleHelper.CreateTMPText(c.transform, "LockedText",
                $"LOCKED: Complete previous station to unlock {stationTitle}", 32f,
                Vector2.zero, new Vector2(920f, 210f), new Color(1.0f, 0.68f, 0.32f), TextAlignmentOptions.Center, FontStyles.Bold);
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
            Phase5MatSet mats,
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
                new Vector3(0f, 0.005f, capSize.z * 0.5f + 0.042f), new Vector3(plateWidth, 0.01f, 0.056f), mats.ScreenPanel, false);
            Object.DestroyImmediate(plate.GetComponent<Collider>());

            Canvas labelCanvas = LabTextStyleHelper.CreateWorldCanvas("Label", btnRoot.transform,
                new Vector3(0f, 0.012f, capSize.z * 0.5f + 0.042f), new Vector3(90f, 180f, 0f),
                new Vector2(plateWidth, 0.056f), pxW, 68, mainCam, true);
            Button labelUiBtn = CreateInvisibleClickButton(labelCanvas.transform, "ClickLabel", new Vector2(pxW, 68f));
            plateText = LabTextStyleHelper.CreateTMPText(labelCanvas.transform, "Text", plateLabel, 22f,
                Vector2.zero, new Vector2(pxW - 12f, 60f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            plateText.enableAutoSizing = true;
            plateText.fontSizeMin = 14f;
            plateText.fontSizeMax = 22f;

            SerializedObject pbSo = new SerializedObject(pushBtn);
            LabTextStyleHelper.SetObjRef(pbSo, "interactable", simple);
            LabTextStyleHelper.SetObjRef(pbSo, "uiRelayButton", capUiBtn);
            LabTextStyleHelper.SetObjRef(pbSo, "uiLabelRelayButton", labelUiBtn);
            LabTextStyleHelper.SetObjRef(pbSo, "capTransform", cap.transform);
            LabTextStyleHelper.SetObjRef(pbSo, "audioSource", audioSource);
            pbSo.ApplyModifiedPropertiesWithoutUndo();

            return pushBtn;
        }

        private static Slider CreateUISlider(Transform parent, string name, Vector2 anchoredPos, Vector2 sizeDelta, Color fillColor)
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
            fillImg.color = fillColor;

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
            Phase5MatSet mats,
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
