using System.Collections.Generic;
using System.IO;
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
    /// Phase 6 Editor builder: builds Station 5 (Diode Biasing) and Station 6 (Diode I-V Characteristics),
    /// replaces scene stubs cleanly, authors LessonStep & DialogueLine assets for Stations 5 & 6,
    /// wires NPCController (Stations T, 1..6), and rebakes LabNavMeshData.
    /// </summary>
    public static class Phase6BiasAndIVStationsBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string PrefabsFolder = "Assets/PNJunctionLab/Prefabs";
        private const string StationsPrefabsFolder = "Assets/PNJunctionLab/Prefabs/Stations";
        private const string Station5PrefabPath = StationsPrefabsFolder + "/BiasingStation.prefab";
        private const string Station6PrefabPath = StationsPrefabsFolder + "/IVCharacteristicsStation.prefab";
        private const string MaterialsFolder = "Assets/PNJunctionLab/Materials";
        private const string DataFolder = "Assets/PNJunctionLab/Data";
        private const string LessonFolder = DataFolder + "/Lessons";
        private const string DialogueFolder = DataFolder + "/Dialogue";

        private sealed class Phase6MatSet
        {
            public Material TableTop;
            public Material TableFrame;
            public Material ScreenPanel;
            public Material AccentBase;
            public Material ButtonGreen;
            public Material ButtonAmber;
            public Material ButtonRed;
            public Material ButtonBlue;
            public Material HoleRed;
            public Material ElectronBlue;
            public Material DonorOrange;
            public Material AcceptorTeal;
            public Material BlockPType;
            public Material BlockNType;
            public Material DepletionTint;
            public Material LeadRed;
            public Material LeadBlack;
            public Material MetalContact;
            public Material LedOff;
            public Material DiodeSi;
            public Material DiodeGe;
            public Material DiodeLed;
            public Material DiodeZener;
            public Material BreakdownGlow;
        }

        [MenuItem("PNJunctionLab/Build Phase 6 (Stations 5 & 6: Biasing & I-V)")]
        public static void BuildPhase6Menu()
        {
            string summary = BuildPhase6();
            Debug.Log(summary);
        }

        /// <summary>
        /// Programmatic Phase 6 entry point callable from Unity MCP execute_code.
        /// </summary>
        public static string BuildPhase6()
        {
            LabTextStyleHelper.EnsureNativeGameViewDPI();

            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.EndsWith("PNJunctionLab.unity"))
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            EnsureFolders();
            LabTextStyleHelper.GetOrCreateCrispFontAsset();
            Phase6MatSet mats = EnsureMaterials();
            Camera mainCam = FindMainCamera();

            GameObject stationsParentGo = GameObject.Find("Stations");
            Transform stationsParent = stationsParentGo != null ? stationsParentGo.transform : null;

            // Remove existing Station 5 & Station 6 roots cleanly (preserve exact transforms).
            (Vector3 pos5, Quaternion rot5) = RemoveExistingStation("Station_5_Biasing",
                new Vector3(5.60f, 0f, 2.20f), Quaternion.Euler(0f, 270f, 0f));
            (Vector3 pos6, Quaternion rot6) = RemoveExistingStation("Station_6_IV",
                new Vector3(5.60f, 0f, -0.80f), Quaternion.Euler(0f, 270f, 0f));

            BiasingStationController st5Ctrl = BuildStation5(stationsParent, pos5, rot5, mats, mainCam);
            IVStationController st6Ctrl = BuildStation6(stationsParent, pos6, rot6, mats, mainCam);

            WirePhase6LessonsAndNpc(st5Ctrl, st6Ctrl);
            string navReport = RebuildNavMeshAndValidateStandPoints();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"Phase 6 build complete! Station_5_Biasing + Station_6_IV wired. {navReport}";
        }

        private static (Vector3 pos, Quaternion rot) RemoveExistingStation(string objName, Vector3 fallbackPos, Quaternion fallbackRot)
        {
            GameObject existing = GameObject.Find(objName);
            if (existing == null)
            {
                return (fallbackPos, fallbackRot);
            }

            Vector3 p = existing.transform.position;
            Quaternion r = existing.transform.rotation;
            Object.DestroyImmediate(existing);
            return (p, r);
        }

        // =====================================================================
        // STATION 5: DIODE BIASING CIRCUIT
        // =====================================================================
        private static BiasingStationController BuildStation5(
            Transform stationsParent,
            Vector3 worldPos,
            Quaternion worldRot,
            Phase6MatSet mats,
            Camera mainCam)
        {
            GameObject root = new GameObject("Station_5_Biasing");
            AudioSource stationAudio = root.AddComponent<AudioSource>();
            stationAudio.spatialBlend = 1f;
            stationAudio.playOnAwake = false;

            // Workbench (2.44 m wide x 0.98 m deep x 0.90 m high).
            BuildWorkbench(root.transform, 2.44f, 0.98f, 0.90f, mats);

            StationBeacon beacon = BuildBeaconParts(root.transform, "5", "Diode Biasing", mats, mainCam, out TeleportationAnchor anchor);

            // Dedicated NPCStandPoint in clear NE floor aisle (local (+1.34, 0, 1.05) -> world ~(4.55, 0, 3.54)).
            GameObject npcStand = new GameObject("NPCStandPoint_5");
            npcStand.transform.SetParent(root.transform, false);
            npcStand.transform.localPosition = new Vector3(1.34f, 0f, 1.05f);
            npcStand.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Back Screens (Left/Center: Meters & Status, Right: Physics Theory).
            BuildStation5BackScreens(
                root.transform, mats, mainCam,
                out TMP_Text circuitStatusText,
                out TMP_Text voltmeterText,
                out TMP_Text ammeterText,
                out GameObject breakdownBanner,
                out TMP_Text breakdownWarningText);

            // Tabletop Left: Variable DC Battery (0..60 V), Knob, Slider, Buttons, and Grabbable Red(+)/Black(-) Leads.
            GameObject batteryRoot = BuildStation5Battery(
                root.transform, mats, stationAudio, mainCam,
                out RotaryKnobControl voltageKnob,
                out Slider voltageSlider,
                out PhysicalPushButton voltDownBtn,
                out PhysicalPushButton voltUpBtn,
                out PhysicalPushButton voltStep10Btn,
                out TMP_Text supplyVoltageText,
                out BiasLeadPlug redLead,
                out BiasLeadPlug blackLead,
                out RespawnableItem redRespawn,
                out RespawnableItem blackRespawn);

            // Tabletop Center: Live Biased P-N Junction Cutaway + Circuit Board (220 ohm R, P/N Sockets, Indicator LED).
            BiasedJunctionVisual junctionVisual = BuildStation5CutawayAndCircuit(
                root.transform, mats, stationAudio, mainCam,
                out XRSocketInteractor pSocket,
                out SocketRingFeedback pFeedback,
                out XRSocketInteractor nSocket,
                out SocketRingFeedback nFeedback);

            // Tabletop Right: RETURN LEADS & RESET STATION buttons.
            PhysicalPushButton returnLeadsBtn = BuildLabeledButton(
                root.transform, "ReturnLeadsButton", new Vector3(-0.76f, 0.90f, 0.24f),
                new Vector3(0.088f, 0.036f, 0.072f), mats.ButtonAmber,
                "RETURN LEADS", "Unplug & Return Battery Leads", mats, stationAudio, mainCam, out _, 0.21f);

            PhysicalPushButton resetBtn = BuildLabeledButton(
                root.transform, "ResetStationButton", new Vector3(-1.02f, 0.90f, 0.24f),
                new Vector3(0.088f, 0.036f, 0.072f), mats.ButtonRed,
                "RESET STATION", "Reset Biasing & Clear Breakdown", mats, stationAudio, mainCam, out _, 0.21f);

            RespawnZone respawnZone = BuildStationRespawnZone(root.transform, new[] { redRespawn, blackRespawn });
            GameObject lockedOverlay = BuildLockedOverlay(root.transform, "Diode Biasing", mats, mainCam);

            BiasingStationController ctrl = root.AddComponent<BiasingStationController>();
            SerializedObject so = new SerializedObject(ctrl);
            LabTextStyleHelper.SetObjRef(so, "stationBeacon", beacon);
            LabTextStyleHelper.SetObjRef(so, "npcStandPoint", npcStand.transform);
            LabTextStyleHelper.SetObjRef(so, "lockedOverlay", lockedOverlay);
            LabTextStyleHelper.SetObjectArray(so, "hintTargets", new[]
            {
                anchor.transform,
                batteryRoot.transform,
                pSocket.transform,
                resetBtn.transform
            });

            LabTextStyleHelper.SetObjRef(so, "stationAnchor", anchor);
            LabTextStyleHelper.SetObjRef(so, "pTerminalSocket", pSocket);
            LabTextStyleHelper.SetObjRef(so, "pSocketFeedback", pFeedback);
            LabTextStyleHelper.SetObjRef(so, "nTerminalSocket", nSocket);
            LabTextStyleHelper.SetObjRef(so, "nSocketFeedback", nFeedback);
            LabTextStyleHelper.SetObjRef(so, "redPositiveLead", redLead);
            LabTextStyleHelper.SetObjRef(so, "blackNegativeLead", blackLead);
            LabTextStyleHelper.SetObjRef(so, "respawnZone", respawnZone);
            LabTextStyleHelper.SetObjRef(so, "audioSource", stationAudio);

            LabTextStyleHelper.SetObjRef(so, "voltageKnob", voltageKnob);
            LabTextStyleHelper.SetObjRef(so, "voltageSlider", voltageSlider);
            LabTextStyleHelper.SetObjRef(so, "voltDownButton", voltDownBtn);
            LabTextStyleHelper.SetObjRef(so, "voltUpButton", voltUpBtn);
            LabTextStyleHelper.SetObjRef(so, "voltStep10Button", voltStep10Btn);
            LabTextStyleHelper.SetObjRef(so, "returnLeadsButton", returnLeadsBtn);
            LabTextStyleHelper.SetObjRef(so, "resetStationButton", resetBtn);

            LabTextStyleHelper.SetObjRef(so, "junctionVisual", junctionVisual);
            LabTextStyleHelper.SetObjRef(so, "circuitStatusText", circuitStatusText);
            LabTextStyleHelper.SetObjRef(so, "voltmeterText", voltmeterText);
            LabTextStyleHelper.SetObjRef(so, "ammeterText", ammeterText);
            LabTextStyleHelper.SetObjRef(so, "supplyVoltageText", supplyVoltageText);
            LabTextStyleHelper.SetObjRef(so, "breakdownWarningBanner", breakdownBanner);
            LabTextStyleHelper.SetObjRef(so, "breakdownWarningText", breakdownWarningText);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAssetAndConnect(root, Station5PrefabPath, InteractionMode.AutomatedAction);
            root.transform.SetParent(stationsParent, false);
            root.transform.SetPositionAndRotation(worldPos, worldRot);
            return ctrl;
        }

        private static void BuildStation5BackScreens(
            Transform parent,
            Phase6MatSet mats,
            Camera mainCam,
            out TMP_Text circuitStatusText,
            out TMP_Text voltmeterText,
            out TMP_Text ammeterText,
            out GameObject breakdownBanner,
            out TMP_Text breakdownWarningText)
        {
            GameObject screens = new GameObject("BackDisplays");
            screens.transform.SetParent(parent, false);

            // Main Readout Board (Left/Center).
            GameObject mainBoard = new GameObject("BiasingReadoutBoard");
            mainBoard.transform.SetParent(screens.transform, false);
            mainBoard.transform.localPosition = new Vector3(0.36f, 1.76f, -0.40f);

            CreateBox("PostL", screens.transform, new Vector3(0.95f, 1.15f, -0.41f), new Vector3(0.04f, 0.52f, 0.04f), mats.TableFrame, true);
            CreateBox("PostR", screens.transform, new Vector3(-0.22f, 1.15f, -0.41f), new Vector3(0.04f, 0.52f, 0.04f), mats.TableFrame, true);
            CreateBox("Frame", mainBoard.transform, Vector3.zero, new Vector3(1.38f, 0.68f, 0.036f), mats.TableFrame, true);
            GameObject backing = CreateBox("Backing", mainBoard.transform, new Vector3(0f, 0f, 0.014f), new Vector3(1.32f, 0.62f, 0.018f), mats.ScreenPanel, false);
            Object.DestroyImmediate(backing.GetComponent<Collider>());

            Canvas mainCanvas = LabTextStyleHelper.CreateWorldCanvas("ReadoutCanvas", mainBoard.transform,
                new Vector3(0f, 0f, 0.028f), new Vector3(0f, 180f, 0f),
                new Vector2(1.32f, 0.62f), 1320, 620, mainCam, false);

            LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "Title",
                "DIODE BIASING CIRCUIT  |  Si DIODE (V_knee = 0.70 V, V_br = -50 V) + 220 ohm SERIES R", 22f,
                new Vector2(0f, 276f), new Vector2(1260f, 44f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Digital Voltmeter Card (left) & Digital Ammeter Card (right).
            CreateUIImageRect(mainCanvas.transform, "VoltCard", new Vector2(-315f, 118f), new Vector2(580f, 188f), new Color(0.06f, 0.11f, 0.17f, 0.95f));
            voltmeterText = LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "VoltmeterReadout",
                "VOLTMETER (Vd)\n0.000 V", 44f,
                new Vector2(-315f, 118f), new Vector2(540f, 166f), new Color(0.35f, 0.92f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            CreateUIImageRect(mainCanvas.transform, "AmpCard", new Vector2(315f, 118f), new Vector2(580f, 188f), new Color(0.06f, 0.11f, 0.17f, 0.95f));
            ammeterText = LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "AmmeterReadout",
                "AMMETER (I)\n0.00 mA", 44f,
                new Vector2(315f, 118f), new Vector2(540f, 166f), new Color(0.32f, 1.0f, 0.62f), TextAlignmentOptions.Center, FontStyles.Bold);

            CreateUIImageRect(mainCanvas.transform, "StatusStrip", new Vector2(0f, -52f), new Vector2(1240f, 96f), new Color(0.09f, 0.13f, 0.19f, 0.95f));
            circuitStatusText = LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "StatusText",
                "Plug Red (+) and Black (-) leads into P & N terminals.", 24f,
                new Vector2(0f, -52f), new Vector2(1200f, 84f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Dedicated Breakdown Alert Banner in bottom slot of screen (never overlaps meters or status).
            breakdownBanner = CreateUIImageRect(mainCanvas.transform, "BreakdownBanner", new Vector2(0f, -206f), new Vector2(1240f, 148f), new Color(0.45f, 0.08f, 0.08f, 0.98f)).gameObject;
            CreateUIImageRect(breakdownBanner.transform, "WarningBorder", new Vector2(0f, 68f), new Vector2(1240f, 8f), new Color(1.0f, 0.25f, 0.20f, 1f));
            breakdownWarningText = LabTextStyleHelper.CreateTMPText(breakdownBanner.transform, "WarningText",
                "BREAKDOWN ALERT!", 28f,
                new Vector2(0f, -4f), new Vector2(1200f, 130f), new Color(1.0f, 0.88f, 0.72f), TextAlignmentOptions.Center, FontStyles.Bold);
            breakdownBanner.SetActive(false);

            // Right Reference Board: Biasing Physics Summary.
            GameObject guideBoard = new GameObject("BiasingPhysicsBoard");
            guideBoard.transform.SetParent(screens.transform, false);
            guideBoard.transform.localPosition = new Vector3(-0.74f, 1.76f, -0.40f);

            CreateBox("PostGR", screens.transform, new Vector3(-1.02f, 1.15f, -0.41f), new Vector3(0.04f, 0.52f, 0.04f), mats.TableFrame, true);
            CreateBox("PostGL", screens.transform, new Vector3(-0.46f, 1.15f, -0.41f), new Vector3(0.04f, 0.52f, 0.04f), mats.TableFrame, true);
            CreateBox("Frame", guideBoard.transform, Vector3.zero, new Vector3(0.76f, 0.68f, 0.036f), mats.TableFrame, true);
            GameObject gBack = CreateBox("Backing", guideBoard.transform, new Vector3(0f, 0f, 0.014f), new Vector3(0.70f, 0.62f, 0.018f), mats.ScreenPanel, false);
            Object.DestroyImmediate(gBack.GetComponent<Collider>());

            Canvas gCanvas = LabTextStyleHelper.CreateWorldCanvas("TheoryCanvas", guideBoard.transform,
                new Vector3(0f, 0f, 0.028f), new Vector3(0f, 180f, 0f),
                new Vector2(0.70f, 0.62f), 700, 620, mainCam, false);

            LabTextStyleHelper.CreateTMPText(gCanvas.transform, "Header",
                "BIASING GUIDE (Not to scale)", 24f,
                new Vector2(0f, 272f), new Vector2(650f, 44f), new Color(1.0f, 0.80f, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);

            string body =
                "1. FORWARD BIAS (Red -> P, Black -> N)\n" +
                "   - Barrier lowers: q(V0 - V)\n" +
                "   - Depletion width W NARROWS\n" +
                "   - Exponential current once V >= 0.70 V (LED ON)\n\n" +
                "2. REVERSE BIAS (Red -> N, Black -> P)\n" +
                "   - Barrier increases: q(V0 + V_rev)\n" +
                "   - Depletion width W WIDENS; only Is (~2.5 nA) leaks\n\n" +
                "3. BREAKDOWN & SERIES RESISTOR (220 ohm)\n" +
                "   - Above -50 V avalanche current surges!\n" +
                "   - Without R, diode burns out (thermal runaway).\n" +
                "   - Press RESET STATION after testing Breakdown.";
            LabTextStyleHelper.CreateTMPText(gCanvas.transform, "Body", body, 20f,
                new Vector2(0f, -24f), new Vector2(640f, 510f), Color.white, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        }

        private static GameObject BuildStation5Battery(
            Transform parent,
            Phase6MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            out RotaryKnobControl knob,
            out Slider voltageSlider,
            out PhysicalPushButton voltDownBtn,
            out PhysicalPushButton voltUpBtn,
            out PhysicalPushButton voltStep10Btn,
            out TMP_Text supplyVoltageText,
            out BiasLeadPlug redLead,
            out BiasLeadPlug blackLead,
            out RespawnableItem redRespawn,
            out RespawnableItem blackRespawn)
        {
            GameObject battery = new GameObject("VariableDCBattery");
            battery.transform.SetParent(parent, false);
            battery.transform.localPosition = new Vector3(0.84f, 0.90f, 0.08f);

            // Chassis + Slanted Control Console.
            CreateBox("BatteryHousing", battery.transform, new Vector3(0f, 0.085f, -0.05f), new Vector3(0.56f, 0.17f, 0.30f), mats.TableFrame, true);
            GameObject topStrip = CreateBox("TopAccent", battery.transform, new Vector3(0f, 0.172f, -0.05f), new Vector3(0.54f, 0.01f, 0.28f), mats.ScreenPanel, false);
            Object.DestroyImmediate(topStrip.GetComponent<Collider>());

            // Readout + Piecewise Voltage Slider face on top front bevel.
            GameObject panel = new GameObject("SupplyControlPanel");
            panel.transform.SetParent(battery.transform, false);
            panel.transform.localPosition = new Vector3(0.08f, 0.195f, -0.02f);
            panel.transform.localRotation = Quaternion.Euler(52f, 180f, 0f);
            GameObject pBack = CreateBox("Backing", panel.transform, Vector3.zero, new Vector3(0.36f, 0.17f, 0.016f), mats.ScreenPanel, false);
            Object.DestroyImmediate(pBack.GetComponent<Collider>());

            Canvas pCanvas = LabTextStyleHelper.CreateWorldCanvas("BatteryPanelCanvas", panel.transform,
                new Vector3(0f, 0f, -0.011f), Vector3.zero,
                new Vector2(0.35f, 0.16f), 420, 192, mainCam, true);

            supplyVoltageText = LabTextStyleHelper.CreateTMPText(pCanvas.transform, "SupplyReadout",
                "V_src: 0.00 V\nR_series: 220 ohm", 22f,
                new Vector2(0f, 44f), new Vector2(390f, 76f), new Color(1.0f, 0.85f, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);

            LabTextStyleHelper.CreateTMPText(pCanvas.transform, "Hint",
                "Sweep: 0..5V (fine) | 5..60V (br)", 15f,
                new Vector2(0f, -66f), new Vector2(390f, 24f), new Color(0.75f, 0.86f, 0.96f), TextAlignmentOptions.Center, FontStyles.Normal);

            voltageSlider = CreateUISlider(pCanvas.transform, "SupplyVoltageSlider", new Vector2(0f, -24f), new Vector2(360f, 40f), new Color(0.95f, 0.62f, 0.18f, 1f));
            voltageSlider.minValue = 0f;
            voltageSlider.maxValue = 1f;
            voltageSlider.value = 0f;

            // Rotary Knob on Battery Top-Right (-X local).
            knob = BuildRotaryKnob(battery.transform, "VoltageKnob", new Vector3(-0.18f, 0.178f, -0.05f),
                0f, 100f, 0f, "Turn DC Voltage Knob (0-60 V)", mats, audioSource, mainCam);

            // Fine (-0.2V, +0.2V) & Breakdown (+10V) Physical Push Buttons in front of battery.
            voltDownBtn = BuildLabeledButton(battery.transform, "VoltDownButton", new Vector3(0.18f, 0.0f, 0.24f),
                new Vector3(0.072f, 0.034f, 0.06f), mats.ButtonAmber, "-V", "Step Supply Voltage Down", mats, audioSource, mainCam, out _, 0.12f);
            voltUpBtn = BuildLabeledButton(battery.transform, "VoltUpButton", new Vector3(0.0f, 0.0f, 0.24f),
                new Vector3(0.072f, 0.034f, 0.06f), mats.ButtonGreen, "+V", "Step Supply Voltage Up", mats, audioSource, mainCam, out _, 0.12f);
            voltStep10Btn = BuildLabeledButton(battery.transform, "VoltStep10Button", new Vector3(-0.18f, 0.0f, 0.24f),
                new Vector3(0.078f, 0.034f, 0.06f), mats.ButtonBlue, "+10 V", "Jump +10 V Toward Breakdown", mats, audioSource, mainCam, out _, 0.14f);

            // Positive Red (+) and Negative Black (-) Binding Posts + Holsters + Grabbable Plugs.
            GameObject redPost = CreateBox("Post_PosRed", battery.transform, new Vector3(0.12f, 0.17f, -0.15f), new Vector3(0.042f, 0.042f, 0.042f), mats.LeadRed, false);
            Object.DestroyImmediate(redPost.GetComponent<Collider>());
            GameObject blackPost = CreateBox("Post_NegBlack", battery.transform, new Vector3(-0.12f, 0.17f, -0.15f), new Vector3(0.042f, 0.042f, 0.042f), mats.LeadBlack, false);
            Object.DestroyImmediate(blackPost.GetComponent<Collider>());

            (redLead, redRespawn) = BuildLeadPlug(
                battery.transform, "Lead_Red_Positive", BiasLeadPolarity.PositiveRed,
                new Vector3(0.14f, 0.042f, 0.08f), redPost.transform,
                "+ (+RED)", "Battery + Lead -> Drop in P (Fwd) or N (Rev)", mats.LeadRed, new Color(0.95f, 0.22f, 0.20f, 1f),
                mats, audioSource, mainCam);

            (blackLead, blackRespawn) = BuildLeadPlug(
                battery.transform, "Lead_Black_Negative", BiasLeadPolarity.NegativeBlack,
                new Vector3(-0.14f, 0.042f, 0.08f), blackPost.transform,
                "- (BLACK)", "Battery - Lead -> Drop in N (Fwd) or P (Rev)", mats.LeadBlack, new Color(0.22f, 0.25f, 0.30f, 1f),
                mats, audioSource, mainCam);

            return battery;
        }

        private static (BiasLeadPlug plug, RespawnableItem respawn) BuildLeadPlug(
            Transform parent,
            string name,
            BiasLeadPolarity polarity,
            Vector3 restLocalPos,
            Transform postOrigin,
            string badgeText,
            string tooltipText,
            Material bodyMat,
            Color cableColor,
            Phase6MatSet mats,
            AudioSource audioSource,
            Camera mainCam)
        {
            GameObject anchor = new GameObject(name + "_RestAnchor");
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = restLocalPos;
            anchor.transform.localRotation = Quaternion.identity;

            // Holster dock pad.
            GameObject holster = CreateBox(name + "_Holster", parent, new Vector3(restLocalPos.x, 0.008f, restLocalPos.z),
                new Vector3(0.11f, 0.016f, 0.11f), mats.TableFrame, true);
            Object.DestroyImmediate(holster.GetComponent<Collider>());

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = restLocalPos;

            // Plug body (>= 5 cm all axes via BoxCollider 0.068 x 0.062 x 0.090).
            GameObject handle = CreateBox("PlugHandle", go.transform, Vector3.zero, new Vector3(0.056f, 0.044f, 0.064f), bodyMat, false);
            Object.DestroyImmediate(handle.GetComponent<Collider>());
            GameObject tip = CreateBox("MetalTip", go.transform, new Vector3(0f, -0.015f, -0.025f), new Vector3(0.026f, 0.024f, 0.024f), mats.MetalContact, false);
            Object.DestroyImmediate(tip.GetComponent<Collider>());

            GameObject wirePin = new GameObject("WireAttach");
            wirePin.transform.SetParent(go.transform, false);
            wirePin.transform.localPosition = new Vector3(0f, 0.012f, 0.032f);

            Canvas badge = LabTextStyleHelper.CreateWorldCanvas("PlugBadge", go.transform,
                new Vector3(0f, 0.027f, 0.010f), new Vector3(58f, 180f, 0f),
                new Vector2(0.080f, 0.056f), 140, 96, mainCam, false);
            LabTextStyleHelper.CreateTMPText(badge.transform, "Txt", badgeText, 24f,
                Vector2.zero, new Vector2(132f, 88f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            BoxCollider col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.072f, 0.060f, 0.090f);

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.25f;
            rb.linearDamping = 2f;
            rb.angularDamping = 2.5f;

            XRGrabInteractable grab = go.AddComponent<XRGrabInteractable>();
            grab.colliders.Add(col);
            grab.useDynamicAttach = true;
            grab.throwOnDetach = false;

            AddInteractionFeedback(go, grab, new[] { handle.GetComponent<Renderer>(), tip.GetComponent<Renderer>() },
                audioSource, tooltipText, new Vector3(0f, 0.095f, 0f), mats, mainCam);

            RespawnableItem resp = go.AddComponent<RespawnableItem>();
            SerializedObject rSo = new SerializedObject(resp);
            LabTextStyleHelper.SetObjRef(rSo, "homeAnchor", anchor.transform);
            LabTextStyleHelper.SetObjRef(rSo, "audioSource", audioSource);
            rSo.ApplyModifiedPropertiesWithoutUndo();

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.startWidth = 0.015f;
            lr.endWidth = 0.015f;
            lr.numCapVertices = 3;
            lr.sharedMaterial = bodyMat;
            lr.startColor = cableColor;
            lr.endColor = cableColor;

            BiasLeadPlug plug = go.AddComponent<BiasLeadPlug>();
            SerializedObject pSo = new SerializedObject(plug);
            SerializedProperty polProp = pSo.FindProperty("polarity");
            if (polProp != null) polProp.intValue = (int)polarity;
            LabTextStyleHelper.SetObjRef(pSo, "batteryPostOrigin", postOrigin);
            LabTextStyleHelper.SetObjRef(pSo, "wireAttachPoint", wirePin.transform);
            LabTextStyleHelper.SetObjRef(pSo, "cableRenderer", lr);
            LabTextStyleHelper.SetObjRef(pSo, "grabInteractable", grab);
            LabTextStyleHelper.SetObjRef(pSo, "respawnableItem", resp);
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return (plug, resp);
        }

        private static BiasedJunctionVisual BuildStation5CutawayAndCircuit(
            Transform parent,
            Phase6MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            out XRSocketInteractor pSocket,
            out SocketRingFeedback pFeedback,
            out XRSocketInteractor nSocket,
            out SocketRingFeedback nFeedback)
        {
            GameObject boardRoot = new GameObject("DiodeCircuitAndCutaway");
            boardRoot.transform.SetParent(parent, false);
            boardRoot.transform.localPosition = new Vector3(0.06f, 0.90f, 0.02f);

            // Base Circuit PCB Deck.
            CreateBox("CircuitDeck", boardRoot.transform, new Vector3(0f, 0.014f, 0.06f), new Vector3(0.96f, 0.028f, 0.56f), mats.TableFrame, true);

            // P-Terminal (Left: +0.26 X) & N-Terminal (Right: -0.14 X) Sockets + 220 ohm Resistor + Indicator LED (-0.34 X).
            BuildJigSocketBay(boardRoot.transform, "P_TerminalSocket", new Vector3(0.28f, 0.028f, 0.18f),
                "P-TERMINAL", mats.BlockPType, mats, audioSource, mainCam, 0.14f, out pSocket, out pFeedback);
            BuildJigSocketBay(boardRoot.transform, "N_TerminalSocket", new Vector3(-0.08f, 0.028f, 0.18f),
                "N-TERMINAL", mats.BlockNType, mats, audioSource, mainCam, 0.14f, out nSocket, out nFeedback);

            // Diode Symbol Block between P & N sockets.
            GameObject diodeBody = CreateBox("DiodePackage", boardRoot.transform, new Vector3(0.10f, 0.048f, 0.18f), new Vector3(0.11f, 0.04f, 0.055f), mats.DiodeSi, false);
            Object.DestroyImmediate(diodeBody.GetComponent<Collider>());
            GameObject cathodeBand = CreateBox("CathodeBand", boardRoot.transform, new Vector3(0.055f, 0.05f, 0.18f), new Vector3(0.016f, 0.043f, 0.057f), mats.MetalContact, false);
            Object.DestroyImmediate(cathodeBand.GetComponent<Collider>());

            // Series 220 ohm Resistor on left side (+0.39 X -> top trace).
            GameObject resistor = CreateBox("SeriesResistor220Ohm", boardRoot.transform, new Vector3(0.39f, 0.048f, 0.06f), new Vector3(0.045f, 0.036f, 0.11f), mats.DonorOrange, false);
            Object.DestroyImmediate(resistor.GetComponent<Collider>());
            Canvas rLblCanvas = LabTextStyleHelper.CreateWorldCanvas("ResistorTag", boardRoot.transform,
                new Vector3(0.39f, 0.072f, 0.06f), new Vector3(90f, 180f, 0f),
                new Vector2(0.15f, 0.045f), 180, 54, mainCam, false);
            LabTextStyleHelper.CreateTMPText(rLblCanvas.transform, "Txt", "R = 220 ohm", 20f,
                Vector2.zero, new Vector2(170f, 48f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Indicator LED Dome & Halo on right side (-0.34 X).
            GameObject ledPedestal = CreateBox("LedPedestal", boardRoot.transform, new Vector3(-0.33f, 0.044f, 0.18f), new Vector3(0.11f, 0.034f, 0.11f), mats.ScreenPanel, false);
            Object.DestroyImmediate(ledPedestal.GetComponent<Collider>());

            GameObject ledBulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ledBulb.name = "IndicatorLED_Bulb";
            ledBulb.transform.SetParent(boardRoot.transform, false);
            ledBulb.transform.localPosition = new Vector3(-0.33f, 0.088f, 0.18f);
            ledBulb.transform.localScale = new Vector3(0.058f, 0.072f, 0.058f);
            Object.DestroyImmediate(ledBulb.GetComponent<Collider>());
            Renderer ledRend = ledBulb.GetComponent<Renderer>();
            ledRend.sharedMaterial = mats.LedOff;

            GameObject ledHalo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ledHalo.name = "IndicatorLED_Halo";
            ledHalo.transform.SetParent(boardRoot.transform, false);
            ledHalo.transform.localPosition = new Vector3(-0.33f, 0.063f, 0.18f);
            ledHalo.transform.localScale = new Vector3(0.095f, 0.006f, 0.095f);
            Object.DestroyImmediate(ledHalo.GetComponent<Collider>());
            Renderer ledHaloRend = ledHalo.GetComponent<Renderer>();
            ledHaloRend.sharedMaterial = mats.LedOff;

            Canvas ledLblCanvas = LabTextStyleHelper.CreateWorldCanvas("LedLabel", boardRoot.transform,
                new Vector3(-0.33f, 0.032f, 0.255f), new Vector3(90f, 180f, 0f),
                new Vector2(0.14f, 0.042f), 160, 48, mainCam, false);
            LabTextStyleHelper.CreateTMPText(ledLblCanvas.transform, "Txt", "LED STATUS", 19f,
                Vector2.zero, new Vector2(152f, 44f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Live 3D Biased Junction Cutaway Stage elevated clearly above deck & components (y = 0.235).
            GameObject stage = new GameObject("BiasedJunctionCutawayStage");
            stage.transform.SetParent(boardRoot.transform, false);
            stage.transform.localPosition = new Vector3(-0.02f, 0.235f, -0.09f);

            CreateBox("StagePostL", stage.transform, new Vector3(0.39f, -0.105f, -0.04f), new Vector3(0.024f, 0.21f, 0.024f), mats.TableFrame, true);
            CreateBox("StagePostR", stage.transform, new Vector3(-0.39f, -0.105f, -0.04f), new Vector3(0.024f, 0.21f, 0.024f), mats.TableFrame, true);

            GameObject topHdr = CreateBox("TopBanner", stage.transform, new Vector3(0f, 0.178f, -0.02f), new Vector3(0.88f, 0.072f, 0.016f), mats.ScreenPanel, false);
            Object.DestroyImmediate(topHdr.GetComponent<Collider>());
            Canvas topHdrCanvas = LabTextStyleHelper.CreateWorldCanvas("TopBannerCanvas", stage.transform,
                new Vector3(0f, 0.178f, -0.008f), new Vector3(0f, 180f, 0f),
                new Vector2(0.86f, 0.068f), 860, 68, mainCam, false);
            TMP_Text headerTxt = LabTextStyleHelper.CreateTMPText(topHdrCanvas.transform, "Header",
                "P-N JUNCTION CUTAWAY (Not to scale) — OPEN CIRCUIT", 20f,
                Vector2.zero, new Vector2(840f, 62f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject botStrip = CreateBox("BottomBanner", stage.transform, new Vector3(0f, -0.145f, 0.025f), new Vector3(0.86f, 0.072f, 0.016f), mats.ScreenPanel, false);
            botStrip.transform.localRotation = Quaternion.Euler(28f, 180f, 0f);
            Object.DestroyImmediate(botStrip.GetComponent<Collider>());
            Canvas botCanvas = LabTextStyleHelper.CreateWorldCanvas("BottomBannerCanvas", stage.transform,
                new Vector3(0f, -0.140f, 0.036f), new Vector3(28f, 180f, 0f),
                new Vector2(0.84f, 0.068f), 840, 70, mainCam, false);
            TMP_Text barrierTxt = LabTextStyleHelper.CreateTMPText(botCanvas.transform, "Barrier",
                "Barrier qV_eff = --", 18f, new Vector2(0f, 16f), new Vector2(810f, 28f), new Color(1.0f, 0.85f, 0.38f), TextAlignmentOptions.Center, FontStyles.Bold);
            TMP_Text widthTxt = LabTextStyleHelper.CreateTMPText(botCanvas.transform, "Width",
                "W = -- um", 18f, new Vector2(0f, -16f), new Vector2(810f, 28f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Bulk P (+X left, aligned with P-Terminal) & N (-X right, aligned with N-Terminal) + Depletion Box.
            GameObject pBulk = CreateBox("P_Bulk", stage.transform, new Vector3(0.20f, 0f, 0f), new Vector3(0.28f, 0.21f, 0.13f), mats.BlockPType, false);
            Object.DestroyImmediate(pBulk.GetComponent<Collider>());
            GameObject nBulk = CreateBox("N_Bulk", stage.transform, new Vector3(-0.20f, 0f, 0f), new Vector3(0.28f, 0.21f, 0.13f), mats.BlockNType, false);
            Object.DestroyImmediate(nBulk.GetComponent<Collider>());
            GameObject depBox = CreateBox("DepletionBox", stage.transform, Vector3.zero, new Vector3(0.15f, 0.212f, 0.132f), mats.DepletionTint, false);
            Object.DestroyImmediate(depBox.GetComponent<Collider>());

            GameObject pBound = CreateBox("Boundary_P", stage.transform, new Vector3(0.075f, 0f, -0.068f), new Vector3(0.008f, 0.22f, 0.008f), mats.HoleRed, false);
            Object.DestroyImmediate(pBound.GetComponent<Collider>());
            GameObject nBound = CreateBox("Boundary_N", stage.transform, new Vector3(-0.075f, 0f, -0.068f), new Vector3(0.008f, 0.22f, 0.008f), mats.ElectronBlue, false);
            Object.DestroyImmediate(nBound.GetComponent<Collider>());

            GameObject brGlow = CreateBox("BreakdownAvalancheGlow", stage.transform, Vector3.zero, new Vector3(0.75f, 0.225f, 0.145f), mats.BreakdownGlow, false);
            Object.DestroyImmediate(brGlow.GetComponent<Collider>());
            Renderer brGlowRend = brGlow.GetComponent<Renderer>();
            brGlowRend.enabled = false;

            // Pooled Ion & Carrier primitives (8 + 8 + 16 + 16 = 48 spheres << 300 cap).
            Transform[] negIons = new Transform[8];
            Transform[] posIons = new Transform[8];
            for (int i = 0; i < 8; i++)
            {
                GameObject neg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                neg.name = "NegIon_" + i;
                neg.transform.SetParent(stage.transform, false);
                neg.transform.localScale = Vector3.one * 0.019f;
                Object.DestroyImmediate(neg.GetComponent<Collider>());
                neg.GetComponent<Renderer>().sharedMaterial = mats.AcceptorTeal;
                negIons[i] = neg.transform;

                GameObject pos = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pos.name = "PosIon_" + i;
                pos.transform.SetParent(stage.transform, false);
                pos.transform.localScale = Vector3.one * 0.019f;
                Object.DestroyImmediate(pos.GetComponent<Collider>());
                pos.GetComponent<Renderer>().sharedMaterial = mats.DonorOrange;
                posIons[i] = pos.transform;
            }

            Transform[] holes = new Transform[16];
            Transform[] electrons = new Transform[16];
            for (int i = 0; i < 16; i++)
            {
                GameObject h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                h.name = "Hole_" + i;
                h.transform.SetParent(stage.transform, false);
                h.transform.localScale = Vector3.one * 0.016f;
                Object.DestroyImmediate(h.GetComponent<Collider>());
                h.GetComponent<Renderer>().sharedMaterial = mats.HoleRed;
                holes[i] = h.transform;

                GameObject e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                e.name = "Electron_" + i;
                e.transform.SetParent(stage.transform, false);
                e.transform.localScale = Vector3.one * 0.015f;
                Object.DestroyImmediate(e.GetComponent<Collider>());
                e.GetComponent<Renderer>().sharedMaterial = mats.ElectronBlue;
                electrons[i] = e.transform;
            }

            BiasedJunctionVisual visual = stage.AddComponent<BiasedJunctionVisual>();
            SerializedObject vSo = new SerializedObject(visual);
            LabTextStyleHelper.SetObjRef(vSo, "pBulkBlock", pBulk.transform);
            LabTextStyleHelper.SetObjRef(vSo, "nBulkBlock", nBulk.transform);
            LabTextStyleHelper.SetObjRef(vSo, "depletionBox", depBox.transform);
            LabTextStyleHelper.SetObjRef(vSo, "depletionBoxRenderer", depBox.GetComponent<Renderer>());
            LabTextStyleHelper.SetObjRef(vSo, "pBoundaryBar", pBound.transform);
            LabTextStyleHelper.SetObjRef(vSo, "nBoundaryBar", nBound.transform);
            LabTextStyleHelper.SetObjRef(vSo, "breakdownGlowRenderer", brGlowRend);
            LabTextStyleHelper.SetObjectArray(vSo, "negativeIonMarkers", negIons);
            LabTextStyleHelper.SetObjectArray(vSo, "positiveIonMarkers", posIons);
            LabTextStyleHelper.SetObjectArray(vSo, "holeMarkers", holes);
            LabTextStyleHelper.SetObjectArray(vSo, "electronMarkers", electrons);
            LabTextStyleHelper.SetObjRef(vSo, "ledBulbRenderer", ledRend);
            LabTextStyleHelper.SetObjRef(vSo, "ledHaloRenderer", ledHaloRend);
            LabTextStyleHelper.SetObjRef(vSo, "cutawayHeaderText", headerTxt);
            LabTextStyleHelper.SetObjRef(vSo, "barrierInfoText", barrierTxt);
            LabTextStyleHelper.SetObjRef(vSo, "depletionWidthText", widthTxt);
            vSo.ApplyModifiedPropertiesWithoutUndo();

            return visual;
        }

        // =====================================================================
        // STATION 6: I-V CHARACTERISTICS ANALYZER
        // =====================================================================
        private static IVStationController BuildStation6(
            Transform stationsParent,
            Vector3 worldPos,
            Quaternion worldRot,
            Phase6MatSet mats,
            Camera mainCam)
        {
            GameObject root = new GameObject("Station_6_IV");
            AudioSource stationAudio = root.AddComponent<AudioSource>();
            stationAudio.spatialBlend = 1f;
            stationAudio.playOnAwake = false;

            // Workbench (2.36 m wide x 0.98 m deep x 0.90 m high).
            BuildWorkbench(root.transform, 2.36f, 0.98f, 0.90f, mats);

            StationBeacon beacon = BuildBeaconParts(root.transform, "6", "I-V Characteristics", mats, mainCam, out TeleportationAnchor anchor);

            // Dedicated NPCStandPoint at clear S aisle edge (local (-1.32, 0, 1.05) -> world ~(4.55, 0, -2.12)).
            GameObject npcStand = new GameObject("NPCStandPoint_6");
            npcStand.transform.SetParent(root.transform, false);
            npcStand.transform.localPosition = new Vector3(-1.32f, 0f, 1.05f);
            npcStand.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Dual Upper Displays: Main I-V Curve Console (left/center) + Reverse Inset Zoom & Data Table (right).
            DiodeIVPlotterView plotterView = BuildStation6PlotterScreens(
                root.transform, mats, mainCam,
                out TMP_Text analyzerStatusText,
                out TMP_Text diodeSpecsText,
                out TMP_Text liveReadingText);

            // Tabletop Left: 4 Grabbable Diode Sample Modules + Analyzer Socket.
            GameObject trayRoot = BuildDiodeSamplesAndSocket(
                root.transform, mats, stationAudio, mainCam,
                out XRSocketInteractor diodeSocket,
                out SocketRingFeedback diodeSocketFeedback,
                out DiodeSampleModule[] sampleModules,
                out RespawnableItem[] sampleRespawns);

            // Tabletop Center-Right: Sweep & Temperature Console (Knobs, Sliders, -/+ Buttons, Auto-Sweep).
            GameObject sweepDeck = BuildSweepAndTempDeck(
                root.transform, mats, stationAudio, mainCam,
                out RotaryKnobControl voltageKnob, out Slider voltageSlider,
                out PhysicalPushButton voltDownBtn, out PhysicalPushButton voltUpBtn,
                out PhysicalPushButton autoSweepBtn, out TMP_Text autoSweepLbl,
                out RotaryKnobControl tempKnob, out Slider tempSlider,
                out PhysicalPushButton tempDownBtn, out PhysicalPushButton tempUpBtn,
                out TMP_Text voltageValTxt, out TMP_Text tempValTxt);

            // Tabletop Right: Ideal Overlay, Record Point, Clear Table, Return Samples, Reset Station.
            BuildStation6ActionDeck(
                root.transform, mats, stationAudio, mainCam,
                out PhysicalPushButton idealBtn, out TMP_Text idealLbl,
                out PhysicalPushButton recordBtn,
                out PhysicalPushButton clearTableBtn,
                out PhysicalPushButton returnSamplesBtn,
                out PhysicalPushButton resetBtn);

            RespawnZone respawnZone = BuildStationRespawnZone(root.transform, sampleRespawns);
            GameObject lockedOverlay = BuildLockedOverlay(root.transform, "I-V Characteristics", mats, mainCam);

            IVStationController ctrl = root.AddComponent<IVStationController>();
            SerializedObject so = new SerializedObject(ctrl);
            LabTextStyleHelper.SetObjRef(so, "stationBeacon", beacon);
            LabTextStyleHelper.SetObjRef(so, "npcStandPoint", npcStand.transform);
            LabTextStyleHelper.SetObjRef(so, "lockedOverlay", lockedOverlay);
            LabTextStyleHelper.SetObjectArray(so, "hintTargets", new[]
            {
                anchor.transform,
                trayRoot.transform,
                sweepDeck.transform,
                recordBtn.transform
            });

            LabTextStyleHelper.SetObjRef(so, "stationAnchor", anchor);
            LabTextStyleHelper.SetObjRef(so, "diodeSocket", diodeSocket);
            LabTextStyleHelper.SetObjRef(so, "diodeSocketFeedback", diodeSocketFeedback);
            LabTextStyleHelper.SetObjectArray(so, "sampleModules", sampleModules);
            LabTextStyleHelper.SetObjRef(so, "respawnZone", respawnZone);
            LabTextStyleHelper.SetObjRef(so, "audioSource", stationAudio);

            LabTextStyleHelper.SetObjRef(so, "voltageKnob", voltageKnob);
            LabTextStyleHelper.SetObjRef(so, "voltageSlider", voltageSlider);
            LabTextStyleHelper.SetObjRef(so, "voltDownButton", voltDownBtn);
            LabTextStyleHelper.SetObjRef(so, "voltUpButton", voltUpBtn);
            LabTextStyleHelper.SetObjRef(so, "autoSweepButton", autoSweepBtn);
            LabTextStyleHelper.SetObjRef(so, "autoSweepButtonLabel", autoSweepLbl);

            LabTextStyleHelper.SetObjRef(so, "temperatureKnob", tempKnob);
            LabTextStyleHelper.SetObjRef(so, "temperatureSlider", tempSlider);
            LabTextStyleHelper.SetObjRef(so, "tempDownButton", tempDownBtn);
            LabTextStyleHelper.SetObjRef(so, "tempUpButton", tempUpBtn);

            LabTextStyleHelper.SetObjRef(so, "idealOverlayButton", idealBtn);
            LabTextStyleHelper.SetObjRef(so, "idealOverlayButtonLabel", idealLbl);
            LabTextStyleHelper.SetObjRef(so, "recordPointButton", recordBtn);
            LabTextStyleHelper.SetObjRef(so, "clearTableButton", clearTableBtn);
            LabTextStyleHelper.SetObjRef(so, "returnSamplesButton", returnSamplesBtn);
            LabTextStyleHelper.SetObjRef(so, "resetStationButton", resetBtn);

            LabTextStyleHelper.SetObjRef(so, "plotterView", plotterView);
            LabTextStyleHelper.SetObjRef(so, "analyzerStatusText", analyzerStatusText);
            LabTextStyleHelper.SetObjRef(so, "diodeSpecsText", diodeSpecsText);
            LabTextStyleHelper.SetObjRef(so, "liveReadingText", liveReadingText);
            LabTextStyleHelper.SetObjRef(so, "voltageValueText", voltageValTxt);
            LabTextStyleHelper.SetObjRef(so, "tempValueText", tempValTxt);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAssetAndConnect(root, Station6PrefabPath, InteractionMode.AutomatedAction);
            root.transform.SetParent(stationsParent, false);
            root.transform.SetPositionAndRotation(worldPos, worldRot);
            return ctrl;
        }

        private static DiodeIVPlotterView BuildStation6PlotterScreens(
            Transform parent,
            Phase6MatSet mats,
            Camera mainCam,
            out TMP_Text analyzerStatusText,
            out TMP_Text diodeSpecsText,
            out TMP_Text liveReadingText)
        {
            GameObject screens = new GameObject("AnalyzerDisplays");
            screens.transform.SetParent(parent, false);

            // LEFT SCREEN: Main Live I-V Curve Console (width 1.24 m, height 0.72 m).
            GameObject leftPanel = new GameObject("MainIVGraphPanel");
            leftPanel.transform.SetParent(screens.transform, false);
            leftPanel.transform.localPosition = new Vector3(0.46f, 1.75f, -0.39f);

            CreateBox("PostLL", screens.transform, new Vector3(0.95f, 1.14f, -0.40f), new Vector3(0.04f, 0.52f, 0.04f), mats.TableFrame, true);
            CreateBox("PostLR", screens.transform, new Vector3(-0.04f, 1.14f, -0.40f), new Vector3(0.04f, 0.52f, 0.04f), mats.TableFrame, true);
            CreateBox("Frame", leftPanel.transform, Vector3.zero, new Vector3(1.24f, 0.72f, 0.036f), mats.TableFrame, true);
            GameObject lBack = CreateBox("Backing", leftPanel.transform, new Vector3(0f, 0f, 0.014f), new Vector3(1.18f, 0.66f, 0.018f), mats.ScreenPanel, false);
            Object.DestroyImmediate(lBack.GetComponent<Collider>());

            Canvas mainCanvas = LabTextStyleHelper.CreateWorldCanvas("IVMainCanvas", leftPanel.transform,
                new Vector3(0f, 0f, 0.028f), new Vector3(0f, 180f, 0f),
                new Vector2(1.18f, 0.66f), 1180, 660, mainCam, false);

            LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "Title",
                "DIODE I-V CHARACTERISTIC ANALYZER  (R_series = 100 ohm  |  Range: -6.5 V to +3.5 V)", 20f,
                new Vector2(0f, 304f), new Vector2(1140f, 34f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            diodeSpecsText = LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "DiodeSpecs",
                "ACTIVE DIODE: None", 18f,
                new Vector2(0f, 266f), new Vector2(1140f, 30f), new Color(1.0f, 0.85f, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Graph Plot Box (width 680 px, height 340 px centered at (-65, 18) -> x in [-405 .. +275]).
            const float plotW = 680f;
            const float plotH = 340f;
            RectTransform plotArea = CreateUIImageRect(mainCanvas.transform, "MainPlotArea", new Vector2(-65f, 18f), new Vector2(plotW, plotH), new Color(0.05f, 0.08f, 0.13f, 0.96f));

            // X-axis (I = 0) and Y-axis (V = 0 at (-6.5..+3.5 -> 0 is at 0.65 from left = +0.15 * plotW))
            CreateUIImageRect(plotArea, "AxisI0", Vector2.zero, new Vector2(plotW - 12f, 2f), new Color(0.55f, 0.64f, 0.74f, 0.75f));
            float vZeroX = (0.65f - 0.5f) * plotW;
            CreateUIImageRect(plotArea, "AxisV0", new Vector2(vZeroX, 0f), new Vector2(2f, plotH - 12f), new Color(0.55f, 0.64f, 0.74f, 0.75f));

            // Light reference grid & axis labels completely clear of MainPlotArea & LegendBg.
            LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "YMaxLbl", "+35 mA", 16f,
                new Vector2(316f, 168f), new Vector2(74f, 26f), Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "YMinLbl", "-35 mA", 16f,
                new Vector2(316f, -134f), new Vector2(74f, 26f), Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "XMinLbl", "-6.5 V (Rev)", 16f,
                new Vector2(-335f, -172f), new Vector2(140f, 26f), new Color(1.0f, 0.65f, 0.45f), TextAlignmentOptions.Left, FontStyles.Bold);
            LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "XZeroLbl", "0 V", 16f,
                new Vector2(-65f + vZeroX, -172f), new Vector2(70f, 26f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "XMaxLbl", "+3.5 V (Fwd)", 16f,
                new Vector2(205f, -172f), new Vector2(140f, 26f), new Color(0.45f, 0.95f, 0.65f), TextAlignmentOptions.Right, FontStyles.Bold);

            // Legend box on right side of MainPlotArea.
            CreateUIImageRect(mainCanvas.transform, "LegendBg", new Vector2(468f, 22f), new Vector2(216f, 210f), new Color(0.08f, 0.12f, 0.18f, 0.92f));
            LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "LegendText",
                "LEGEND:\n" +
                "- Cyan: Circuit (100 ohm)\n" +
                "- Gold: Ideal Shockley\n" +
                "- Green Dot: Live (V, I)\n" +
                "- Orange Line: V_knee(T)", 15f,
                new Vector2(468f, 22f), new Vector2(198f, 190f), Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

            RectTransform[] actualSegs = CreatePolylineSegments(plotArea, "ActualSeg", DiodeIVPlotterView.MainSampleCount - 1, 4.5f, new Color(0.24f, 0.92f, 1.0f, 1f));
            RectTransform[] idealSegs = CreatePolylineSegments(plotArea, "IdealSeg", DiodeIVPlotterView.MainSampleCount - 1, 3.0f, new Color(1.0f, 0.78f, 0.22f, 0.90f));
            RectTransform kneeLine = CreateUIImageRect(plotArea, "KneeMarker", Vector2.zero, new Vector2(2.5f, plotH * 0.86f), new Color(1.0f, 0.52f, 0.18f, 0.85f));
            TMP_Text kneeLabel = LabTextStyleHelper.CreateTMPText(plotArea, "KneeLbl", "V_knee", 15f,
                new Vector2(80f, -142f), new Vector2(210f, 24f), new Color(1.0f, 0.78f, 0.42f), TextAlignmentOptions.Center, FontStyles.Bold);
            RectTransform opDot = CreateUIImageRect(plotArea, "OperatingDot", Vector2.zero, new Vector2(15f, 15f), new Color(0.25f, 1.0f, 0.45f, 1f));

            liveReadingText = LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "LiveReading",
                "V_src = +0.90 V  |  Vd = --  |  I = --", 22f,
                new Vector2(0f, -228f), new Vector2(1140f, 36f), new Color(0.35f, 1.0f, 0.65f), TextAlignmentOptions.Center, FontStyles.Bold);
            analyzerStatusText = LabTextStyleHelper.CreateTMPText(mainCanvas.transform, "AnalyzerStatus",
                "Drop a diode module into Test Socket to begin.", 19f,
                new Vector2(0f, -276f), new Vector2(1140f, 36f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // RIGHT SCREEN: Reverse Leakage Inset Zoom + 10-Row Data Table (width 0.92 m, height 0.72 m).
            GameObject rightPanel = new GameObject("InsetAndDataTablePanel");
            rightPanel.transform.SetParent(screens.transform, false);
            rightPanel.transform.localPosition = new Vector3(-0.68f, 1.75f, -0.39f);

            CreateBox("PostRL", screens.transform, new Vector3(-0.24f, 1.14f, -0.40f), new Vector3(0.04f, 0.52f, 0.04f), mats.TableFrame, true);
            CreateBox("PostRR", screens.transform, new Vector3(-1.08f, 1.14f, -0.40f), new Vector3(0.04f, 0.52f, 0.04f), mats.TableFrame, true);
            CreateBox("Frame", rightPanel.transform, Vector3.zero, new Vector3(0.92f, 0.72f, 0.036f), mats.TableFrame, true);
            GameObject rBack = CreateBox("Backing", rightPanel.transform, new Vector3(0f, 0f, 0.014f), new Vector3(0.86f, 0.66f, 0.018f), mats.ScreenPanel, false);
            Object.DestroyImmediate(rBack.GetComponent<Collider>());

            Canvas rightCanvas = LabTextStyleHelper.CreateWorldCanvas("IVTableCanvas", rightPanel.transform,
                new Vector3(0f, 0f, 0.028f), new Vector3(0f, 180f, 0f),
                new Vector2(0.86f, 0.66f), 860, 660, mainCam, false);

            // Top Half: Reverse Leakage Inset Zoom.
            TMP_Text zoomTitle = LabTextStyleHelper.CreateTMPText(rightCanvas.transform, "ZoomTitle",
                "REVERSE LEAKAGE INSET [-6.5..0 V]", 19f,
                new Vector2(0f, 304f), new Vector2(820f, 30f), new Color(1.0f, 0.72f, 0.34f), TextAlignmentOptions.Center, FontStyles.Bold);

            RectTransform zoomPlotArea = CreateUIImageRect(rightCanvas.transform, "ZoomPlotArea", new Vector2(0f, 206f), new Vector2(780f, 148f), new Color(0.05f, 0.08f, 0.13f, 0.96f));
            CreateUIImageRect(zoomPlotArea, "ZeroLeakLine", new Vector2(0f, 52f), new Vector2(760f, 2f), new Color(0.55f, 0.64f, 0.74f, 0.7f));
            RectTransform[] zoomSegs = CreatePolylineSegments(zoomPlotArea, "ZoomSeg", DiodeIVPlotterView.InsetSampleCount - 1, 3.8f, new Color(1.0f, 0.42f, 0.62f, 1f));

            TMP_Text zoomReadout = LabTextStyleHelper.CreateTMPText(rightCanvas.transform, "ZoomReadout",
                "Is(300 K) = --", 18f,
                new Vector2(0f, 112f), new Vector2(820f, 28f), new Color(0.65f, 0.92f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Bottom Half: Data Table Card (max 10 rows).
            CreateUIImageRect(rightCanvas.transform, "TableBg", new Vector2(0f, -118f), new Vector2(820f, 384f), new Color(0.06f, 0.09f, 0.14f, 0.98f));
            TMP_Text tblHeader = LabTextStyleHelper.CreateTMPText(rightCanvas.transform, "TableHeader",
                "RECORDED DATA TABLE (0/8+ pts)   # |Type| T(K)| Vsrc  |  Vd   |   I", 17f,
                new Vector2(0f, 52f), new Vector2(800f, 30f), new Color(0.32f, 0.95f, 1.0f), TextAlignmentOptions.Left, FontStyles.Bold);
            TMP_Text tblRows = LabTextStyleHelper.CreateTMPText(rightCanvas.transform, "TableRows",
                "No points recorded yet.", 16f,
                new Vector2(0f, -135f), new Vector2(800f, 330f), Color.white, TextAlignmentOptions.TopLeft, FontStyles.Normal);

            DiodeIVPlotterView plotter = screens.AddComponent<DiodeIVPlotterView>();
            SerializedObject pSo = new SerializedObject(plotter);
            LabTextStyleHelper.SetObjectArray(pSo, "actualCurveSegments", actualSegs);
            LabTextStyleHelper.SetObjectArray(pSo, "idealOverlaySegments", idealSegs);
            LabTextStyleHelper.SetObjRef(pSo, "operatingDot", opDot);
            LabTextStyleHelper.SetObjRef(pSo, "kneeMarkerLine", kneeLine);
            LabTextStyleHelper.SetObjRef(pSo, "kneeMarkerLabel", kneeLabel);
            LabTextStyleHelper.SetFloat(pSo, "mainPlotWidth", plotW);
            LabTextStyleHelper.SetFloat(pSo, "mainPlotHalfHeight", plotH * 0.5f);
            LabTextStyleHelper.SetObjectArray(pSo, "zoomCurveSegments", zoomSegs);
            LabTextStyleHelper.SetObjRef(pSo, "zoomTitleLabel", zoomTitle);
            LabTextStyleHelper.SetObjRef(pSo, "zoomReadoutLabel", zoomReadout);
            LabTextStyleHelper.SetFloat(pSo, "zoomPlotWidth", 740f);
            LabTextStyleHelper.SetFloat(pSo, "zoomPlotHalfHeight", 66f);
            LabTextStyleHelper.SetObjRef(pSo, "tableHeaderText", tblHeader);
            LabTextStyleHelper.SetObjRef(pSo, "tableRowsText", tblRows);
            pSo.ApplyModifiedPropertiesWithoutUndo();

            return plotter;
        }

        private static GameObject BuildDiodeSamplesAndSocket(
            Transform parent,
            Phase6MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            out XRSocketInteractor diodeSocket,
            out SocketRingFeedback socketFeedback,
            out DiodeSampleModule[] modules,
            out RespawnableItem[] respawns)
        {
            GameObject tray = new GameObject("DiodeSampleTrayAndSocket");
            tray.transform.SetParent(parent, false);
            tray.transform.localPosition = new Vector3(0.68f, 0.90f, 0.04f);

            // Left sample tray deck (+0.20 X) + right Test Socket Deck (-0.28 X).
            CreateBox("SamplesTray", tray.transform, new Vector3(0.18f, 0.012f, 0f), new Vector3(0.56f, 0.024f, 0.44f), mats.TableFrame, true);
            Canvas trayTitle = LabTextStyleHelper.CreateWorldCanvas("TrayTitle", tray.transform,
                new Vector3(0.18f, 0.028f, -0.18f), new Vector3(90f, 180f, 0f),
                new Vector2(0.52f, 0.048f), 520, 48, mainCam, false);
            LabTextStyleHelper.CreateTMPText(trayTitle.transform, "Title", "DIODE SAMPLES (GRAB & INSERT)", 20f,
                Vector2.zero, new Vector2(500f, 44f), new Color(0.32f, 0.95f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Socket bay on right half of left zone.
            BuildJigSocketBay(tray.transform, "AnalyzerDiodeSocket", new Vector3(-0.30f, 0.024f, -0.02f),
                "TEST SOCKET", mats.AccentBase, mats, audioSource, mainCam, 0.16f, out diodeSocket, out socketFeedback);

            var specs = new (DiodeType type, string shortLbl, string fullName, Vector3 pos, Material mat, string tip)[]
            {
                (DiodeType.Silicon, "Si", "Silicon Diode (Vk 0.7V)", new Vector3(0.32f, 0.056f, -0.06f), mats.DiodeSi, "Si Diode: Eg=1.12 eV, Knee ~0.70V, Vbr=-50V"),
                (DiodeType.Germanium, "Ge", "Germanium Diode (Vk 0.3V)", new Vector3(0.05f, 0.056f, -0.06f), mats.DiodeGe, "Ge Diode: Eg=0.67 eV, Knee ~0.30V, High Is"),
                (DiodeType.LED, "LED", "Green LED (Vk 2.0V)", new Vector3(0.32f, 0.056f, 0.11f), mats.DiodeLed, "GaP LED: Eg=2.10 eV, Knee ~2.0V, Vbr=-5.0V"),
                (DiodeType.Zener, "Znr", "Zener 5.1V Diode (Vbr -5.1V)", new Vector3(0.05f, 0.056f, 0.11f), mats.DiodeZener, "Zener Diode: Knee 0.7V, Sharp Zener -5.1V")
            };

            modules = new DiodeSampleModule[specs.Length];
            respawns = new RespawnableItem[specs.Length];

            for (int i = 0; i < specs.Length; i++)
            {
                var sp = specs[i];
                GameObject anchor = new GameObject("Home_" + sp.shortLbl);
                anchor.transform.SetParent(tray.transform, false);
                anchor.transform.localPosition = sp.pos;

                GameObject go = new GameObject("DiodeSample_" + sp.shortLbl);
                go.transform.SetParent(tray.transform, false);
                go.transform.localPosition = sp.pos;

                GameObject body = CreateBox("Package", go.transform, Vector3.zero, new Vector3(0.11f, 0.054f, 0.082f), sp.mat, false);
                Object.DestroyImmediate(body.GetComponent<Collider>());
                GameObject leadL = CreateBox("PinP", go.transform, new Vector3(0.062f, -0.008f, 0f), new Vector3(0.016f, 0.018f, 0.022f), mats.MetalContact, false);
                Object.DestroyImmediate(leadL.GetComponent<Collider>());
                GameObject leadR = CreateBox("PinN", go.transform, new Vector3(-0.062f, -0.008f, 0f), new Vector3(0.016f, 0.018f, 0.022f), mats.MetalContact, false);
                Object.DestroyImmediate(leadR.GetComponent<Collider>());

                Canvas c = LabTextStyleHelper.CreateWorldCanvas("SampleLbl", go.transform,
                    new Vector3(0f, 0.030f, 0f), new Vector3(90f, 180f, 0f),
                    new Vector2(0.10f, 0.074f), 140, 100, mainCam, false);
                LabTextStyleHelper.CreateTMPText(c.transform, "Txt", sp.shortLbl + "\nDIODE", 22f,
                    Vector2.zero, new Vector2(132f, 92f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

                BoxCollider col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(0.128f, 0.058f, 0.086f);

                Rigidbody rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.28f;
                rb.linearDamping = 1.5f;
                rb.angularDamping = 2f;

                XRGrabInteractable grab = go.AddComponent<XRGrabInteractable>();
                grab.colliders.Add(col);
                grab.useDynamicAttach = true;
                grab.throwOnDetach = false;

                AddInteractionFeedback(go, grab, new[] { body.GetComponent<Renderer>() },
                    audioSource, sp.tip, new Vector3(0f, 0.11f, 0f), mats, mainCam);

                RespawnableItem resp = go.AddComponent<RespawnableItem>();
                SerializedObject rSo = new SerializedObject(resp);
                LabTextStyleHelper.SetObjRef(rSo, "homeAnchor", anchor.transform);
                LabTextStyleHelper.SetObjRef(rSo, "audioSource", audioSource);
                rSo.ApplyModifiedPropertiesWithoutUndo();

                DiodeSampleModule mod = go.AddComponent<DiodeSampleModule>();
                SerializedObject mSo = new SerializedObject(mod);
                SerializedProperty tpProp = mSo.FindProperty("diodeType");
                if (tpProp != null) tpProp.intValue = (int)sp.type;
                LabTextStyleHelper.SetStr(mSo, "shortLabel", sp.shortLbl);
                LabTextStyleHelper.SetStr(mSo, "displayName", sp.fullName);
                LabTextStyleHelper.SetObjRef(mSo, "grabInteractable", grab);
                LabTextStyleHelper.SetObjRef(mSo, "respawnableItem", resp);
                mSo.ApplyModifiedPropertiesWithoutUndo();

                modules[i] = mod;
                respawns[i] = resp;
            }

            return tray;
        }

        private static GameObject BuildSweepAndTempDeck(
            Transform parent,
            Phase6MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            out RotaryKnobControl voltageKnob,
            out Slider voltageSlider,
            out PhysicalPushButton voltDownBtn,
            out PhysicalPushButton voltUpBtn,
            out PhysicalPushButton autoSweepBtn,
            out TMP_Text autoSweepLbl,
            out RotaryKnobControl tempKnob,
            out Slider tempSlider,
            out PhysicalPushButton tempDownBtn,
            out PhysicalPushButton tempUpBtn,
            out TMP_Text voltageValTxt,
            out TMP_Text tempValTxt)
        {
            GameObject deck = new GameObject("SweepAndTemperatureDeck");
            deck.transform.SetParent(parent, false);
            deck.transform.localPosition = new Vector3(-0.26f, 0.90f, 0.02f);

            CreateBox("Plinth", deck.transform, new Vector3(0f, 0.014f, 0f), new Vector3(0.68f, 0.028f, 0.56f), mats.TableFrame, true);

            // Slanted Dual Sliders Console on back edge of deck.
            GameObject pedestal = new GameObject("DualSlidersConsole");
            pedestal.transform.SetParent(deck.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, 0.115f, -0.15f);
            pedestal.transform.localRotation = Quaternion.Euler(46f, 180f, 0f);
            GameObject pBack = CreateBox("Backing", pedestal.transform, Vector3.zero, new Vector3(0.62f, 0.21f, 0.018f), mats.ScreenPanel, false);
            Object.DestroyImmediate(pBack.GetComponent<Collider>());

            Canvas slCanvas = LabTextStyleHelper.CreateWorldCanvas("IVSlidersCanvas", pedestal.transform,
                new Vector3(0f, 0f, -0.012f), Vector3.zero,
                new Vector2(0.60f, 0.20f), 600, 200, mainCam, true);

            voltageValTxt = LabTextStyleHelper.CreateTMPText(slCanvas.transform, "VoltVal", "Sweep V_src: +0.90 V", 20f,
                new Vector2(0f, 72f), new Vector2(560f, 28f), new Color(0.32f, 0.95f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);
            voltageSlider = CreateUISlider(slCanvas.transform, "VoltageSweepSlider", new Vector2(0f, 38f), new Vector2(520f, 36f), new Color(0.22f, 0.78f, 0.98f, 1f));
            voltageSlider.minValue = 0f; voltageSlider.maxValue = 1f; voltageSlider.value = Mathf.InverseLerp(-6.5f, 3.5f, 0.9f);

            tempValTxt = LabTextStyleHelper.CreateTMPText(slCanvas.transform, "TempVal", "Temp: 300 K (+27 C)", 20f,
                new Vector2(0f, -14f), new Vector2(560f, 28f), new Color(1.0f, 0.72f, 0.28f), TextAlignmentOptions.Center, FontStyles.Bold);
            tempSlider = CreateUISlider(slCanvas.transform, "TemperatureSlider", new Vector2(0f, -50f), new Vector2(520f, 36f), new Color(0.95f, 0.56f, 0.18f, 1f));
            tempSlider.minValue = 220f; tempSlider.maxValue = 420f; tempSlider.value = 300f;

            // Dual Knobs + Buttons below slanted pedestal with generous horizontal separation.
            float initVoltPct = Mathf.InverseLerp(-6.5f, 3.5f, 0.9f) * 100f;
            voltageKnob = BuildRotaryKnob(deck.transform, "SweepVoltageKnob", new Vector3(0.24f, 0.028f, 0.04f),
                0f, 100f, initVoltPct, "Drag Voltage Sweep (-6.5..+3.5 V)", mats, audioSource, mainCam);
            tempKnob = BuildRotaryKnob(deck.transform, "ChamberTempKnob", new Vector3(-0.24f, 0.028f, 0.04f),
                220f, 420f, 300f, "Drag Chamber Temp (220..420 K)", mats, audioSource, mainCam);

            voltDownBtn = BuildLabeledButton(deck.transform, "VoltDownButton", new Vector3(0.27f, 0.028f, 0.19f),
                new Vector3(0.068f, 0.032f, 0.056f), mats.ButtonBlue, "-0.5 V", "Sweep Voltage Down", mats, audioSource, mainCam, out _, 0.12f);
            voltUpBtn = BuildLabeledButton(deck.transform, "VoltUpButton", new Vector3(0.12f, 0.028f, 0.19f),
                new Vector3(0.068f, 0.032f, 0.056f), mats.ButtonBlue, "+0.5 V", "Sweep Voltage Up", mats, audioSource, mainCam, out _, 0.12f);

            autoSweepBtn = BuildLabeledButton(deck.transform, "AutoSweepButton", new Vector3(0.0f, 0.028f, 0.04f),
                new Vector3(0.095f, 0.036f, 0.065f), mats.ButtonGreen, "AUTO-SWEEP: OFF", "Toggle Continuous Auto-Sweep", mats, audioSource, mainCam, out autoSweepLbl, 0.19f);

            tempDownBtn = BuildLabeledButton(deck.transform, "TempDownButton", new Vector3(-0.12f, 0.028f, 0.19f),
                new Vector3(0.068f, 0.032f, 0.056f), mats.ButtonAmber, "-25 K", "Cool Diode 25 K", mats, audioSource, mainCam, out _, 0.12f);
            tempUpBtn = BuildLabeledButton(deck.transform, "TempUpButton", new Vector3(-0.27f, 0.028f, 0.19f),
                new Vector3(0.068f, 0.032f, 0.056f), mats.ButtonAmber, "+25 K", "Heat Diode 25 K", mats, audioSource, mainCam, out _, 0.12f);

            return deck;
        }

        private static void BuildStation6ActionDeck(
            Transform parent,
            Phase6MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            out PhysicalPushButton idealBtn,
            out TMP_Text idealLbl,
            out PhysicalPushButton recordBtn,
            out PhysicalPushButton clearTableBtn,
            out PhysicalPushButton returnSamplesBtn,
            out PhysicalPushButton resetBtn)
        {
            GameObject actions = new GameObject("AnalyzerActionsDeck");
            actions.transform.SetParent(parent, false);
            actions.transform.localPosition = new Vector3(-0.88f, 0.90f, 0.08f);

            CreateBox("ActionsPlinth", actions.transform, new Vector3(0f, 0.012f, 0f), new Vector3(0.46f, 0.024f, 0.56f), mats.TableFrame, true);

            recordBtn = BuildLabeledButton(actions.transform, "RecordPointButton", new Vector3(0.10f, 0.024f, -0.14f),
                new Vector3(0.12f, 0.040f, 0.075f), mats.ButtonGreen,
                "RECORD POINT", "Record Current (V, I) to Data Table", mats, audioSource, mainCam, out _, 0.21f);

            idealBtn = BuildLabeledButton(actions.transform, "IdealOverlayButton", new Vector3(-0.12f, 0.024f, -0.14f),
                new Vector3(0.11f, 0.036f, 0.075f), mats.ButtonBlue,
                "SHOCKLEY: OFF", "Toggle Ideal Zero-R Shockley Overlay", mats, audioSource, mainCam, out idealLbl, 0.21f);

            clearTableBtn = BuildLabeledButton(actions.transform, "ClearTableButton", new Vector3(0.10f, 0.024f, 0.06f),
                new Vector3(0.092f, 0.034f, 0.065f), mats.ButtonAmber,
                "CLEAR TABLE", "Clear Recorded Data Table", mats, audioSource, mainCam, out _, 0.19f);

            returnSamplesBtn = BuildLabeledButton(actions.transform, "ReturnSamplesButton", new Vector3(-0.12f, 0.024f, 0.06f),
                new Vector3(0.092f, 0.034f, 0.065f), mats.ButtonAmber,
                "RETURN DIODES", "Return All Diode Samples to Tray", mats, audioSource, mainCam, out _, 0.19f);

            resetBtn = BuildLabeledButton(actions.transform, "ResetStationButton", new Vector3(0f, 0.024f, 0.21f),
                new Vector3(0.11f, 0.036f, 0.068f), mats.ButtonRed,
                "RESET STATION", "Reset I-V Analyzer & Clear Table", mats, audioSource, mainCam, out _, 0.22f);
        }

        // =====================================================================
        // LESSONS, NPC DIALOGUE & NAVMESH WIRING (STATIONS T, 1..6)
        // =====================================================================
        private static void WirePhase6LessonsAndNpc(BiasingStationController st5Ctrl, IVStationController st6Ctrl)
        {
            LessonManager lessonManager = Object.FindAnyObjectByType<LessonManager>();
            if (lessonManager == null)
            {
                return;
            }

            // Smooth handoff line at end of Station 4.
            DialogueLine dlSt4Done = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_St4_Asym_Done.asset");
            if (dlSt4Done != null)
            {
                dlSt4Done.Configure("st4_asym_done",
                    "Station 4 complete! Notice xn = W * NA/(NA+ND) extends deeper into the lighter-doped side. Let's move to Station 5: Diode Biasing!",
                    NPCEmotion.Happy);
                EditorUtility.SetDirty(dlSt4Done);
            }

            // Click-to-Guide Sequences for Station 5 and Station 6.
            DialogueLine dlGuide5Steps = CreateDialogue("DL_Guide_St5_Steps", "guide_5_steps",
                "Station 5 Steps: 1) Plug Red (+) to P-Terminal & Black (-) to N-Terminal and raise V above 0.7V to light the LED (Forward Bias), 2) Swap Red->N & Black->P to watch W widen (Reverse Bias), 3) Sweep past -50V for Breakdown, then press RESET STATION!",
                NPCEmotion.Pointing);
            DialogueLine dlGuide5Phys = CreateDialogue("DL_Guide_St5_Physics", "guide_5_physics",
                "Forward Bias subtracts applied V from V0 (q(V0-V)), narrowing depletion so majority carriers flood exponentially across the junction! Reverse Bias adds V (q(V0+V)), widening W so only tiny Is (~2.5 nA) leaks until avalanche breakdown hits at -50V.",
                NPCEmotion.Explaining, dlGuide5Steps);

            DialogueLine dlGuide6Steps = CreateDialogue("DL_Guide_St6_Steps", "guide_6_steps",
                "Station 6 Steps: 1) Drop Si, Ge, LED, or Zener into the Test Socket, 2) Toggle AUTO-SWEEP & adjust Temperature (220-420K) plus Ideal Shockley overlay, and 3) Press RECORD POINT at least 8 times to populate the Data Table!",
                NPCEmotion.Pointing);
            DialogueLine dlGuide6Phys = CreateDialogue("DL_Guide_St6_Physics", "guide_6_physics",
                "Every diode follows Shockley's law I = Is*(exp(qVd/nkT) - 1) through the 100 ohm load resistor! Larger bandgap gives higher knee (Ge ~0.3V, Si ~0.7V, LED ~2.0V), heating shifts Vk down (~ -2 mV/C) while growing Is, and Zener diodes break down cleanly at -5.1V.",
                NPCEmotion.Explaining, dlGuide6Steps);

            // Station 5 Lesson Dialogue.
            DialogueLine dlSt5Intro = CreateDialogue("DL_Station5_Intro", "st5_intro",
                "Welcome to Station 5: Diode Biasing! Teleport onto Pad 5 beside the Variable Battery bench.",
                NPCEmotion.Encouraging);
            DialogueLine dlSt5TpH20 = CreateDialogue("DL_St5_Tp_H20", "st5_tp_h20",
                "Hint: Aim at floor Pad 5 beside the Biasing workbench and release Teleport (I).", NPCEmotion.Thinking);
            DialogueLine dlSt5TpH45 = CreateDialogue("DL_St5_Tp_H45", "st5_tp_h45",
                "Walk or teleport onto Station 5 right beside me!", NPCEmotion.Pointing);
            DialogueLine dlSt5TpDone = CreateDialogue("DL_St5_Tp_Done", "st5_tp_done",
                "Step 2: Grab Red (+) -> P-Terminal and Black (-) -> N-Terminal, then raise Battery Voltage above 0.8 V until the LED lights!",
                NPCEmotion.Happy);

            DialogueLine dlSt5FwdIntro = CreateDialogue("DL_St5_Fwd_Intro", "st5_fwd_intro",
                "Plug Red (+) into P-Terminal and Black (-) into N-Terminal, then turn up voltage (>0.8V) for Forward Bias!",
                NPCEmotion.Explaining);
            DialogueLine dlSt5FwdH20 = CreateDialogue("DL_St5_Fwd_H20", "st5_fwd_h20",
                "Hint: Drop Red (+) in P-Terminal, Black (-) in N-Terminal, and raise V above 0.85 V using +V or the slider.",
                NPCEmotion.Thinking);
            DialogueLine dlSt5FwdH45 = CreateDialogue("DL_St5_Fwd_H45", "st5_fwd_h45",
                "Connect Red->P and Black->N at the sockets I am pointing to, then increase Voltage!",
                NPCEmotion.Pointing);
            DialogueLine dlSt5FwdDone = CreateDialogue("DL_St5_Fwd_Done", "st5_fwd_done",
                "Forward Bias active! W narrowed, majority carriers flooded across, and the LED lit above silicon's 0.70 V knee! Now swap Red->N and Black->P.",
                NPCEmotion.Happy);

            DialogueLine dlSt5RevIntro = CreateDialogue("DL_St5_Rev_Intro", "st5_rev_intro",
                "Step 3: Reverse Bias! Swap leads (Red -> N-Terminal, Black -> P-Terminal) with V >= 2.0 V to observe depletion widening.",
                NPCEmotion.Explaining);
            DialogueLine dlSt5RevH20 = CreateDialogue("DL_St5_Rev_H20", "st5_rev_h20",
                "Hint: Press RETURN LEADS, plug Red (+) into N-Terminal and Black (-) into P-Terminal, and set V >= 2 V.",
                NPCEmotion.Thinking);
            DialogueLine dlSt5RevH45 = CreateDialogue("DL_St5_Rev_H45", "st5_rev_h45",
                "Swap the Red and Black plugs at the sockets I am pointing to so battery polarity opposes the diode!",
                NPCEmotion.Pointing);
            DialogueLine dlSt5RevDone = CreateDialogue("DL_St5_Rev_Done", "st5_rev_done",
                "Reverse Bias confirmed! Barrier increased, W widened, and only ~2.5 nA saturation current leaks! Now push reverse voltage past -50 V.",
                NPCEmotion.Happy);

            DialogueLine dlSt5BrIntro = CreateDialogue("DL_St5_Br_Intro", "st5_br_intro",
                "Step 4: Keep Reverse Bias active and press +10 V (or drag slider) past -50 V to trigger Avalanche Breakdown, then press RESET STATION.",
                NPCEmotion.Explaining);
            DialogueLine dlSt5BrH20 = CreateDialogue("DL_St5_Br_H20", "st5_br_h20",
                "Hint: In Reverse Bias, tap +10 V until |V| reaches 50 V, read the protective resistor alert, then press RESET STATION.",
                NPCEmotion.Thinking);
            DialogueLine dlSt5BrH45 = CreateDialogue("DL_St5_Br_H45", "st5_br_h45",
                "Increase reverse voltage past 50 V with the +10 V button, then clear the latch using the red RESET STATION button I am pointing to!",
                NPCEmotion.Pointing);
            DialogueLine dlSt5BrDone = CreateDialogue("DL_St5_Br_Done", "st5_br_done",
                "Station 5 complete! Notice how a 220 ohm series resistor prevents thermal destruction at breakdown! Let's inspect I-V curves at Station 6!",
                NPCEmotion.Happy);

            // Station 6 Lesson Dialogue.
            DialogueLine dlSt6Intro = CreateDialogue("DL_Station6_Intro", "st6_intro",
                "Welcome to Station 6: I-V Characteristics! Teleport onto Pad 6 in front of the Diode Analyzer bench.",
                NPCEmotion.Encouraging);
            DialogueLine dlSt6TpH20 = CreateDialogue("DL_St6_Tp_H20", "st6_tp_h20",
                "Hint: Aim at floor Pad 6 beside the I-V Analyzer bench and release Teleport (I).", NPCEmotion.Thinking);
            DialogueLine dlSt6TpH45 = CreateDialogue("DL_St6_Tp_H45", "st6_tp_h45",
                "Teleport or move over to Station 6 right beside me!", NPCEmotion.Pointing);
            DialogueLine dlSt6TpDone = CreateDialogue("DL_St6_Tp_Done", "st6_tp_done",
                "Step 2: Pick up any diode sample module (Si, Ge, LED, or Zener) and drop it into the Test Socket!",
                NPCEmotion.Happy);

            DialogueLine dlSt6SockIntro = CreateDialogue("DL_St6_Sock_Intro", "st6_sock_intro",
                "Grab Si, Ge, LED, or Zener from the sample tray and place it in the glowing Test Socket.",
                NPCEmotion.Explaining);
            DialogueLine dlSt6SockH20 = CreateDialogue("DL_St6_Sock_H20", "st6_sock_h20",
                "Hint: Grab one of the 4 diode blocks on the left tray and snap it into the TEST SOCKET ring.",
                NPCEmotion.Thinking);
            DialogueLine dlSt6SockH45 = CreateDialogue("DL_St6_Sock_H45", "st6_sock_h45",
                "Drop a diode sample into the Test Socket I am pointing toward!",
                NPCEmotion.Pointing);
            DialogueLine dlSt6SockDone = CreateDialogue("DL_St6_Sock_Done", "st6_sock_done",
                "Diode inserted! Look at its I-V curve, V_knee marker, and Reverse Inset zoom! Now sweep voltage AND change Temperature.",
                NPCEmotion.Happy);

            DialogueLine dlSt6SweepIntro = CreateDialogue("DL_St6_Sweep_Intro", "st6_sweep_intro",
                "Step 3: Sweep Voltage (Knob / Slider / AUTO-SWEEP) AND shift Temperature (>= 20 K away from 300 K) to observe knee shift and Is growth!",
                NPCEmotion.Explaining);
            DialogueLine dlSt6SweepH20 = CreateDialogue("DL_St6_Sweep_H20", "st6_sweep_h20",
                "Hint: Press AUTO-SWEEP or adjust the Voltage slider, and click +25 K / -25 K on the Temperature controls!",
                NPCEmotion.Thinking);
            DialogueLine dlSt6SweepH45 = CreateDialogue("DL_St6_Sweep_H45", "st6_sweep_h45",
                "Adjust both Sweep Voltage and Temperature on the console I am pointing toward!",
                NPCEmotion.Pointing);
            DialogueLine dlSt6SweepDone = CreateDialogue("DL_St6_Sweep_Done", "st6_sweep_done",
                "Notice V_knee shifts ~ -2 mV/C warmer while reverse Is surges! Toggle SHOCKLEY OVERLAY to compare ideal vs 100 ohm load, then record 8+ points!",
                NPCEmotion.Happy);

            DialogueLine dlSt6RecIntro = CreateDialogue("DL_St6_Rec_Intro", "st6_rec_intro",
                "Step 4: Sweep across different voltages & diodes (Si, Ge, LED, Zener) and press RECORD POINT at least 8 times!",
                NPCEmotion.Explaining);
            DialogueLine dlSt6RecH20 = CreateDialogue("DL_St6_Rec_H20", "st6_rec_h20",
                "Hint: Vary Sweep Voltage (forward + reverse) or swap diodes and click green RECORD POINT until Data Table shows 8/8+ pts.",
                NPCEmotion.Thinking);
            DialogueLine dlSt6RecH45 = CreateDialogue("DL_St6_Rec_H45", "st6_rec_h45",
                "Press RECORD POINT on the right console I am pointing toward until 8 points are logged!",
                NPCEmotion.Pointing);
            DialogueLine dlSt6RecDone = CreateDialogue("DL_St6_Rec_Done", "st6_rec_done",
                "Station 6 complete! You plotted and recorded real forward & reverse diode curves across Si, Ge, LED, and Zener! Assessment awaits in Phase 7!",
                NPCEmotion.Happy);

            DialogueLine dlAssessReady = CreateDialogue("DL_StationA_Intro", "stA_intro",
                "Station A (Assessment) unlocks next in Phase 7! Feel free to compare Si, Ge, LED, and Zener curves or click me anytime.",
                NPCEmotion.Explaining);

            // Preserve Steps 01..19; delete old Step_20_Station5_Ready placeholder.
            List<LessonStep> steps = new List<LessonStep>();
            string[] prevStepFiles =
            {
                "Step_01_Teleport", "Step_02_Grab", "Step_03_Socket", "Step_04_Button", "Step_05_Screen",
                "Step_06_St1_Teleport", "Step_07_St1_Lens", "Step_08_St1_Heat", "Step_09_St1_ToggleGe",
                "Step_10_St2_Teleport", "Step_11_St2_Dope", "Step_12_St2_AdjustND",
                "Step_13_St3_Teleport", "Step_14_St3_Dope", "Step_15_St3_AdjustNA",
                "Step_16_St4_Teleport", "Step_17_St4_Join", "Step_18_St4_TimeLapse", "Step_19_St4_Asymmetric"
            };
            for (int i = 0; i < prevStepFiles.Length; i++)
            {
                LessonStep s = AssetDatabase.LoadAssetAtPath<LessonStep>(LessonFolder + "/" + prevStepFiles[i] + ".asset");
                if (s != null) steps.Add(s);
            }

            string oldSt5Placeholder = LessonFolder + "/Step_20_Station5_Ready.asset";
            if (AssetDatabase.LoadAssetAtPath<LessonStep>(oldSt5Placeholder) != null)
            {
                AssetDatabase.DeleteAsset(oldSt5Placeholder);
            }

            steps.Add(CreateStep("Step_20_St5_Teleport", "s5_teleport", "5", "Teleport to Station 5",
                "Teleport onto Pad 5 at Diode Biasing bench.", "station5.teleport",
                dlSt5Intro, dlSt5TpDone, dlSt5TpH20, dlSt5TpH45, 0));

            steps.Add(CreateStep("Step_21_St5_ForwardBias", "s5_forward", "5", "Forward Bias & Light LED",
                "Plug Red->P & Black->N and raise V above 0.8 V.", "station5.forward_bias",
                dlSt5FwdIntro, dlSt5FwdDone, dlSt5FwdH20, dlSt5FwdH45, 1));

            steps.Add(CreateStep("Step_22_St5_ReverseBias", "s5_reverse", "5", "Test Reverse Bias Blocking",
                "Swap leads (Red->N, Black->P) at >= 2 V.", "station5.reverse_bias",
                dlSt5RevIntro, dlSt5RevDone, dlSt5RevH20, dlSt5RevH45, 2));

            steps.Add(CreateStep("Step_23_St5_BreakdownReset", "s5_breakdown", "5", "Trigger Breakdown & Reset",
                "Push Reverse V >= 50 V, then press RESET.", "station5.breakdown_reset",
                dlSt5BrIntro, dlSt5BrDone, dlSt5BrH20, dlSt5BrH45, 3));

            steps.Add(CreateStep("Step_24_St6_Teleport", "s6_teleport", "6", "Teleport to Station 6",
                "Teleport onto Pad 6 at I-V Characteristics.", "station6.teleport",
                dlSt6Intro, dlSt6TpDone, dlSt6TpH20, dlSt6TpH45, 0));

            steps.Add(CreateStep("Step_25_St6_SocketDiode", "s6_socket", "6", "Insert a Diode Sample",
                "Grab Si, Ge, LED, or Zener and plug into Test Socket.", "station6.socket_diode",
                dlSt6SockIntro, dlSt6SockDone, dlSt6SockH20, dlSt6SockH45, 1));

            steps.Add(CreateStep("Step_26_St6_SweepAndTemp", "s6_sweep_temp", "6", "Sweep Voltage & Temperature",
                "Vary sweep voltage and shift T (>= 20 K from 300 K).", "station6.sweep_temp",
                dlSt6SweepIntro, dlSt6SweepDone, dlSt6SweepH20, dlSt6SweepH45, 2));

            steps.Add(CreateStep("Step_27_St6_RecordPoints", "s6_record", "6", "Record 8+ I-V Data Points",
                "Press RECORD POINT at least 8 times across sweeps/diodes.", "station6.record_points",
                dlSt6RecIntro, dlSt6RecDone, dlSt6RecH20, dlSt6RecH45, 3));

            steps.Add(CreateStep("Step_28_Assessment_Ready", "sA_ready", "A", "Assessment (Phase 7)",
                "Station A ready for Phase 7.", "assessment.complete",
                dlAssessReady, null, dlAssessReady, dlAssessReady, 0));

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

            // Wire Stations T, 1, 2, 3, 4, 5, 6 into NPCController.stationGuides.
            NPCController npcCtrl = Object.FindAnyObjectByType<NPCController>();
            if (npcCtrl != null)
            {
                StationController stT = GameObject.Find("Station_T_TutorialPad")?.GetComponent<StationController>();
                StationController st1 = GameObject.Find("Station_1_Intrinsic")?.GetComponent<StationController>();
                StationController st2 = GameObject.Find("Station_2_NType")?.GetComponent<StationController>();
                StationController st3 = GameObject.Find("Station_3_PType")?.GetComponent<StationController>();
                StationController st4 = GameObject.Find("Station_4_Junction")?.GetComponent<StationController>();

                DialogueLine dlGT = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Guide_T_Physics.asset");
                DialogueLine dlG1 = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Guide_St1_Physics.asset");
                DialogueLine dlG2 = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Guide_St2_Physics.asset");
                DialogueLine dlG3 = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Guide_St3_Physics.asset");
                DialogueLine dlG4 = AssetDatabase.LoadAssetAtPath<DialogueLine>(DialogueFolder + "/DL_Guide_St4_Physics.asset");

                (string id, StationController sc, DialogueLine guide)[] bindings =
                {
                    ("T", stT, dlGT),
                    ("1", st1, dlG1),
                    ("2", st2, dlG2),
                    ("3", st3, dlG3),
                    ("4", st4, dlG4),
                    ("5", st5Ctrl, dlGuide5Phys),
                    ("6", st6Ctrl, dlGuide6Phys)
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
                    "Station_3_PType", "Station_4_Junction", "Station_5_Biasing", "Station_6_IV"
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

        // =====================================================================
        // SHARED BUILDING PRIMITIVES & ASSET HELPERS
        // =====================================================================
        private static void EnsureFolders()
        {
            EnsureFolder("Assets/PNJunctionLab", "Prefabs");
            EnsureFolder(PrefabsFolder, "Stations");
            EnsureFolder("Assets/PNJunctionLab", "Materials");
            EnsureFolder("Assets/PNJunctionLab", "Data");
            EnsureFolder(DataFolder, "Lessons");
            EnsureFolder(DataFolder, "Dialogue");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full)) AssetDatabase.CreateFolder(parent, child);
        }

        private static Phase6MatSet EnsureMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Phase6MatSet
            {
                TableTop = GetOrCreateMat("Mat_TableTop", urpLit, new Color(0.84f, 0.86f, 0.89f), 0.1f, 0.45f, false),
                TableFrame = GetOrCreateMat("Mat_TableFrame", urpLit, new Color(0.14f, 0.17f, 0.22f), 0.45f, 0.55f, false),
                ScreenPanel = GetOrCreateMat("Mat_ScreenPanel", urpLit, new Color(0.08f, 0.12f, 0.18f), 0.1f, 0.35f, false),
                AccentBase = GetOrCreateMat("Mat_AccentBase", urpLit, new Color(0.15f, 0.72f, 0.95f), 0.2f, 0.6f, true, new Color(0.15f, 0.72f, 0.95f) * 0.45f),
                ButtonGreen = GetOrCreateMat("Mat_ButtonGreen", urpLit, new Color(0.18f, 0.76f, 0.34f), 0.15f, 0.55f, false),
                ButtonAmber = GetOrCreateMat("Mat_ButtonAmber", urpLit, new Color(0.92f, 0.62f, 0.16f), 0.15f, 0.55f, false),
                ButtonRed = GetOrCreateMat("Mat_ButtonRed", urpLit, new Color(0.84f, 0.22f, 0.20f), 0.15f, 0.55f, false),
                ButtonBlue = GetOrCreateMat("Mat_ButtonBlue", urpLit, new Color(0.20f, 0.60f, 0.94f), 0.15f, 0.55f, false),
                HoleRed = GetOrCreateMat("Mat_HoleRed", urpLit, new Color(0.96f, 0.24f, 0.28f), 0.1f, 0.65f, true, new Color(0.8f, 0.16f, 0.18f) * 0.65f),
                ElectronBlue = GetOrCreateMat("Mat_ElectronBlue", urpLit, new Color(0.22f, 0.74f, 1.0f), 0.1f, 0.7f, true, new Color(0.16f, 0.60f, 0.95f) * 0.65f),
                DonorOrange = GetOrCreateMat("Mat_DonorOrange", urpLit, new Color(0.98f, 0.56f, 0.14f), 0.15f, 0.55f, true, new Color(0.8f, 0.38f, 0.08f) * 0.45f),
                AcceptorTeal = GetOrCreateMat("Mat_AcceptorTeal", urpLit, new Color(0.14f, 0.80f, 0.76f), 0.15f, 0.55f, true, new Color(0.10f, 0.65f, 0.60f) * 0.45f),
                BlockPType = GetOrCreateMat("Mat_BlockPType", urpLit, new Color(0.70f, 0.22f, 0.28f), 0.15f, 0.5f, false),
                BlockNType = GetOrCreateMat("Mat_BlockNType", urpLit, new Color(0.16f, 0.48f, 0.78f), 0.15f, 0.5f, false),
                DepletionTint = GetOrCreateTransparentMat("Mat_DepletionTint", urpLit, new Color(0.55f, 0.62f, 0.74f, 0.36f)),
                LeadRed = GetOrCreateMat("Mat_LeadRed", urpLit, new Color(0.88f, 0.16f, 0.16f), 0.1f, 0.4f, false),
                LeadBlack = GetOrCreateMat("Mat_LeadBlack", urpLit, new Color(0.12f, 0.13f, 0.16f), 0.15f, 0.4f, false),
                MetalContact = GetOrCreateMat("Mat_MetalContact", urpLit, new Color(0.76f, 0.79f, 0.84f), 0.85f, 0.78f, false),
                LedOff = GetOrCreateMat("Mat_LedOff", urpLit, new Color(0.18f, 0.22f, 0.26f), 0.1f, 0.82f, false),
                DiodeSi = GetOrCreateMat("Mat_DiodeSi", urpLit, new Color(0.24f, 0.34f, 0.48f), 0.2f, 0.5f, false),
                DiodeGe = GetOrCreateMat("Mat_DiodeGe", urpLit, new Color(0.72f, 0.48f, 0.26f), 0.2f, 0.55f, false),
                DiodeLed = GetOrCreateMat("Mat_DiodeLed", urpLit, new Color(0.18f, 0.78f, 0.44f), 0.1f, 0.75f, true, new Color(0.10f, 0.45f, 0.24f) * 0.4f),
                DiodeZener = GetOrCreateMat("Mat_DiodeZener", urpLit, new Color(0.88f, 0.44f, 0.14f), 0.2f, 0.5f, false),
                BreakdownGlow = GetOrCreateTransparentMat("Mat_BreakdownGlow", urpLit, new Color(1.0f, 0.24f, 0.14f, 0.42f))
            };
        }

        private static Material GetOrCreateMat(string name, Shader shader, Color baseColor, float metallic, float smoothness,
            bool emission = false, Color emissionColor = default)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Material mat = new Material(shader);
            mat.name = name;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (emission && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material GetOrCreateTransparentMat(string name, Shader shader, Color baseColor)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Material mat = new Material(shader);
            mat.name = name;
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
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

        private static void BuildWorkbench(Transform parent, float width, float depth, float height, Phase6MatSet mats)
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

        private static StationBeacon BuildBeaconParts(Transform parent, string stationId, string title, Phase6MatSet mats, Camera mainCam, out TeleportationAnchor anchor)
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

        private static void BuildJigSocketBay(
            Transform parent,
            string bayName,
            Vector3 localPos,
            string bayLabel,
            Material accentMat,
            Phase6MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            float ringDiameter,
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
            ring.transform.localScale = new Vector3(ringDiameter, 0.006f, ringDiameter);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            Renderer ringRend = ring.GetComponent<Renderer>();
            ringRend.sharedMaterial = accentMat;

            Canvas lblCanvas = LabTextStyleHelper.CreateWorldCanvas("BayLabel", bay.transform,
                new Vector3(0f, 0.015f, -ringDiameter * 0.60f), new Vector3(90f, 180f, 0f),
                new Vector2(0.18f, 0.042f), 200, 48, mainCam, false);
            LabTextStyleHelper.CreateTMPText(lblCanvas.transform, "Text", bayLabel, 22f,
                Vector2.zero, new Vector2(190f, 44f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject attach = new GameObject("AttachPoint");
            attach.transform.SetParent(bay.transform, false);
            attach.transform.localPosition = new Vector3(0f, 0.042f, 0f);

            SphereCollider triggerCol = bay.AddComponent<SphereCollider>();
            triggerCol.isTrigger = true;
            triggerCol.center = new Vector3(0f, 0.045f, 0f);
            triggerCol.radius = ringDiameter * 0.62f;

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

        private static RotaryKnobControl BuildRotaryKnob(
            Transform parent,
            string name,
            Vector3 localPos,
            float minValue,
            float maxValue,
            float initialValue,
            string tooltipLabel,
            Phase6MatSet mats,
            AudioSource audioSource,
            Camera mainCam)
        {
            GameObject knobGo = new GameObject(name);
            knobGo.transform.SetParent(parent, false);
            knobGo.transform.localPosition = localPos;

            GameObject dialBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dialBase.name = "DialBody";
            dialBase.transform.SetParent(knobGo.transform, false);
            dialBase.transform.localPosition = new Vector3(0f, 0.022f, 0f);
            dialBase.transform.localScale = new Vector3(0.095f, 0.022f, 0.095f);
            Object.DestroyImmediate(dialBase.GetComponent<Collider>());
            dialBase.GetComponent<Renderer>().sharedMaterial = mats.ScreenPanel;

            GameObject dialBasis = new GameObject("DialDragBasis");
            dialBasis.transform.SetParent(parent, false);
            dialBasis.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject pointer = CreateBox("PointerBar", dialBase.transform, new Vector3(0f, 0.85f, -0.25f), new Vector3(0.16f, 0.22f, 0.50f), mats.AccentBase, false);
            Object.DestroyImmediate(pointer.GetComponent<Collider>());

            BoxCollider knobCol = knobGo.AddComponent<BoxCollider>();
            knobCol.center = new Vector3(0f, 0.028f, 0f);
            knobCol.size = new Vector3(0.105f, 0.056f, 0.105f);

            Rigidbody knobRb = knobGo.AddComponent<Rigidbody>();
            knobRb.isKinematic = true;
            knobRb.useGravity = false;

            XRSimpleInteractable xr = knobGo.AddComponent<XRSimpleInteractable>();
            xr.colliders.Add(knobCol);
            AddInteractionFeedback(knobGo, xr, new[] { pointer.GetComponent<Renderer>(), dialBase.GetComponent<Renderer>() },
                audioSource, tooltipLabel, new Vector3(0f, 0.11f, 0f), mats, mainCam);

            RotaryKnobControl knob = knobGo.AddComponent<RotaryKnobControl>();
            SerializedObject kSo = new SerializedObject(knob);
            LabTextStyleHelper.SetObjRef(kSo, "interactable", xr);
            LabTextStyleHelper.SetObjRef(kSo, "dialVisual", dialBase.transform);
            LabTextStyleHelper.SetObjRef(kSo, "orientationReference", dialBasis.transform);
            LabTextStyleHelper.SetObjRef(kSo, "audioSource", audioSource);
            LabTextStyleHelper.SetFloat(kSo, "minValue", minValue);
            LabTextStyleHelper.SetFloat(kSo, "maxValue", maxValue);
            LabTextStyleHelper.SetFloat(kSo, "initialValue", initialValue);
            kSo.ApplyModifiedPropertiesWithoutUndo();
            return knob;
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

        private static GameObject BuildLockedOverlay(Transform parent, string stationTitle, Phase6MatSet mats, Camera mainCam)
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
            Phase6MatSet mats,
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

        private static RectTransform[] CreatePolylineSegments(Transform parent, string prefix, int count, float thickness, Color color)
        {
            RectTransform[] segs = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                segs[i] = CreateUIImageRect(parent, prefix + "_" + i, Vector2.zero, new Vector2(14f, thickness), color);
            }
            return segs;
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
            Phase6MatSet mats,
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
            if (r != null && mat != null) r.sharedMaterial = mat;
            return go;
        }

        private static Camera FindMainCamera()
        {
            Camera cam = Camera.main;
            return cam != null ? cam : Object.FindAnyObjectByType<Camera>();
        }
    }
}
