using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using Object = UnityEngine.Object;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Applies Quest 3 Android rendering/OpenXR stability fixes (Station 2 & 3 crash prevention),
    /// streamlines LabTablet's Settings panel into a clean Menu (Resume, Restart, Volume, NPC Voice, Exit)
    /// removing unused Posture/Locomotion/Turn/TextSize clutter, and mounts a Meta Quest 3 Controls Guide
    /// board on Station_T_TutorialPad.
    /// </summary>
    public static class Quest3StabilityAndMenuBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string TabletPrefabPath = "Assets/PNJunctionLab/Prefabs/LabTablet.prefab";
        private const string TutorialPrefabPath = "Assets/PNJunctionLab/Prefabs/TutorialPadStation.prefab";
        private const string MobileRpPath = "Assets/Settings/Mobile_RPAsset.asset";
        private const string CrispFontPath = "Assets/PNJunctionLab/UI/LiberationSans_Crisp_SDF.asset";

        [MenuItem("PNJunctionLab/Apply Quest 3 Stability, Menu Tablet & Tutorial Controls Board")]
        public static void ApplyMenu()
        {
            Debug.Log(ApplyAll());
        }

        public static string ApplyAll()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.EndsWith("PNJunctionLab.unity", StringComparison.OrdinalIgnoreCase))
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            string questFixSummary = ApplyQuest3StabilityFixes();
            bool menuBuilt = UpgradeLabTabletToMenu();
            bool tutorialBoardBuilt = BuildTutorialControlsBoard();

            int badScalers = AuditWorldCanvasDPU();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"Quest3StabilityAndMenu Complete: {questFixSummary}, MenuBuilt={menuBuilt}, TutorialControlsBoard={tutorialBoardBuilt}, BadCanvasDPU={badScalers}.";
        }

        private static string ApplyQuest3StabilityFixes()
        {
            // 1. Mobile_RPAsset: disable HDR (avoids Quest 3 Vulkan HDR+MSAA tile overflow when transparent + glowing lattices activate on St 2 & 3)
            UniversalRenderPipelineAsset mobileRp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MobileRpPath);
            bool hdrDisabled = false;
            int msaa = -1;
            if (mobileRp != null)
            {
                mobileRp.supportsHDR = false;
                mobileRp.msaaSampleCount = 2;
                mobileRp.shadowDistance = 20f;
                EditorUtility.SetDirty(mobileRp);
                hdrDisabled = !mobileRp.supportsHDR;
                msaa = mobileRp.msaaSampleCount;
            }

            // Ensure Android uses Quality[0] (Mobile RP) by default.
            QualitySettings.SetQualityLevel(0, false);

            // 2. OpenXR Android: disable FoveatedRenderingFeature (useEyeTracking=True on Quest 3 without eye tracking)
            //    and set optimizeBufferDiscards=false on MetaQuestFeature for Vulkan safety.
            bool foveationDisabled = false;
            OpenXRSettings oxrAndroid = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (oxrAndroid != null)
            {
                foreach (OpenXRFeature f in oxrAndroid.GetFeatures<OpenXRFeature>())
                {
                    if (f == null) continue;
                    if (f is FoveatedRenderingFeature)
                    {
                        f.enabled = false;
                        EditorUtility.SetDirty(f);
                        foveationDisabled = true;
                    }
                    else if (f is UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature mq)
                    {
                        SerializedObject mqSo = new SerializedObject(mq);
                        SerializedProperty optDiscard = mqSo.FindProperty("m_optimizeBufferDiscards");
                        if (optDiscard != null)
                        {
                            optDiscard.boolValue = false;
                            mqSo.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(mq);
                        }
                    }
                }
                EditorUtility.SetDirty(oxrAndroid);
            }

            return $"MobileRP(HDR={!hdrDisabled}, MSAA={msaa}), FoveationDisabled={foveationDisabled}";
        }

        private static bool UpgradeLabTabletToMenu()
        {
            LabTablet tablet = Object.FindAnyObjectByType<LabTablet>(FindObjectsInactive.Include);
            if (tablet == null) return false;

            Transform canvasTf = tablet.transform.Find("ContentRoot/TabletCanvas");
            if (canvasTf == null) return false;

            // Relabel Tab_Settings -> "Menu"
            TMP_Text menuTabLabel = canvasTf.Find("Tab_Settings/Label")?.GetComponent<TMP_Text>();
            if (menuTabLabel != null)
            {
                menuTabLabel.text = "Menu";
                EditorUtility.SetDirty(menuTabLabel);
            }

            Transform settingsPanel = canvasTf.Find("SettingsPanel");
            if (settingsPanel == null) return false;

            // Remove clutter controls (TextSize, Locomotion, Posture, TurnMode).
            string[] obsoleteButtons = { "TextSizeButton", "LocomotionButton", "PostureButton", "TurnModeButton" };
            foreach (string ob in obsoleteButtons)
            {
                Transform child = settingsPanel.Find(ob);
                if (child != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            // Clean up any previous Menu buttons we're rebuilding idempotently.
            string[] rebuildNames = { "ResumeButton", "RestartButton", "VolDownButton", "VolumeReadout", "VolUpButton", "ExitButton" };
            foreach (string rb in rebuildNames)
            {
                Transform old = settingsPanel.Find(rb);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            // Create clean Menu buttons modeled after NpcVoiceButton style.
            Transform voiceBtnTf = settingsPanel.Find("NpcVoiceButton");
            TMP_FontAsset crispFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CrispFontPath);
            if (crispFont == null && voiceBtnTf != null)
            {
                crispFont = voiceBtnTf.GetComponentInChildren<TMP_Text>(true)?.font;
            }

            Button resumeBtn = CreateMenuButton("ResumeButton", settingsPanel, new Vector2(0f, 118f), new Vector2(680f, 58f),
                "Resume Simulation", new Color(0.12f, 0.48f, 0.30f, 1f), crispFont);
            Button restartBtn = CreateMenuButton("RestartButton", settingsPanel, new Vector2(0f, 46f), new Vector2(680f, 58f),
                "Restart Simulation", new Color(0.62f, 0.39f, 0.11f, 1f), crispFont);

            Button volDownBtn = CreateMenuButton("VolDownButton", settingsPanel, new Vector2(-240f, -26f), new Vector2(180f, 58f),
                "Vol -", new Color(0.16f, 0.25f, 0.38f, 1f), crispFont);
            TMP_Text volReadout = CreateMenuLabelBox("VolumeReadout", settingsPanel, new Vector2(0f, -26f), new Vector2(300f, 58f),
                "Volume: 100%", new Color(0.10f, 0.15f, 0.23f, 1f), crispFont);
            Button volUpBtn = CreateMenuButton("VolUpButton", settingsPanel, new Vector2(240f, -26f), new Vector2(180f, 58f),
                "Vol +", new Color(0.16f, 0.25f, 0.38f, 1f), crispFont);

            Button voiceBtn = null;
            TMP_Text voiceLbl = null;
            Image voiceBg = null;
            if (voiceBtnTf != null)
            {
                RectTransform vrt = voiceBtnTf.GetComponent<RectTransform>();
                vrt.anchoredPosition = new Vector2(0f, -98f);
                vrt.sizeDelta = new Vector2(680f, 58f);
                voiceBtn = voiceBtnTf.GetComponent<Button>();
                voiceLbl = voiceBtnTf.Find("Label")?.GetComponent<TMP_Text>();
                voiceBg = voiceBtnTf.GetComponent<Image>();
            }

            Button exitBtn = CreateMenuButton("ExitButton", settingsPanel, new Vector2(0f, -170f), new Vector2(680f, 58f),
                "Exit Simulation", new Color(0.58f, 0.15f, 0.16f, 1f), crispFont);

            SerializedObject tabSo = new SerializedObject(tablet);
            LabTextStyleHelper.SetObjRef(tabSo, "resumeButton", resumeBtn);
            LabTextStyleHelper.SetObjRef(tabSo, "restartButton", restartBtn);
            LabTextStyleHelper.SetObjRef(tabSo, "volumeDownButton", volDownBtn);
            LabTextStyleHelper.SetObjRef(tabSo, "volumeUpButton", volUpBtn);
            LabTextStyleHelper.SetObjRef(tabSo, "volumeLabel", volReadout);
            LabTextStyleHelper.SetObjRef(tabSo, "exitButton", exitBtn);
            LabTextStyleHelper.SetObjRef(tabSo, "npcVoiceToggleButton", voiceBtn);
            LabTextStyleHelper.SetObjRef(tabSo, "npcVoiceLabel", voiceLbl);
            LabTextStyleHelper.SetObjRef(tabSo, "npcVoiceButtonBackground", voiceBg);
            LabTextStyleHelper.SetObjRef(tabSo, "textSizeButton", null);
            LabTextStyleHelper.SetObjRef(tabSo, "textSizeLabel", null);
            LabTextStyleHelper.SetObjRef(tabSo, "locomotionToggleButton", null);
            LabTextStyleHelper.SetObjRef(tabSo, "locomotionLabel", null);
            LabTextStyleHelper.SetObjRef(tabSo, "postureToggleButton", null);
            LabTextStyleHelper.SetObjRef(tabSo, "postureLabel", null);
            LabTextStyleHelper.SetObjRef(tabSo, "snapTurnToggleButton", null);
            LabTextStyleHelper.SetObjRef(tabSo, "snapTurnLabel", null);
            tabSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tablet);

            if (PrefabUtility.IsPartOfPrefabInstance(tablet.gameObject))
            {
                PrefabUtility.ApplyPrefabInstance(tablet.gameObject, InteractionMode.AutomatedAction);
            }
            else if (AssetDatabase.LoadAssetAtPath<GameObject>(TabletPrefabPath) != null)
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(tablet.gameObject, TabletPrefabPath, InteractionMode.AutomatedAction);
            }

            return true;
        }

        private static bool BuildTutorialControlsBoard()
        {
            GameObject tutGo = GameObject.Find("Station_T_TutorialPad");
            if (tutGo == null) return false;

            Transform old = tutGo.transform.Find("TutorialControlsBoard");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // Placed cleanly on right side of Tutorial Pad bench facing inward toward TeleportAnchor_T (local 0, 0, +1.35).
            GameObject boardRoot = new GameObject("TutorialControlsBoard");
            boardRoot.transform.SetParent(tutGo.transform, false);
            boardRoot.transform.localPosition = new Vector3(-0.98f, 1.32f, -0.18f);
            Vector3 toAnchor = new Vector3(0f, 1.32f, 1.35f) - boardRoot.transform.localPosition;
            toAnchor.y = 0f;
            boardRoot.transform.localRotation = Quaternion.LookRotation(toAnchor.normalized, Vector3.up);

            Material trimMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PNJunctionLab/Materials/Mat_LabTrim.mat");
            Material panelMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PNJunctionLab/Materials/Mat_ScreenPanel.mat") ?? trimMat;
            TMP_FontAsset crispFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CrispFontPath);

            CreateBoardPart("BoardFrame", boardRoot.transform, Vector3.zero, new Vector3(0.68f, 0.56f, 0.03f), trimMat);
            CreateBoardPart("BoardBacking", boardRoot.transform, new Vector3(0f, 0f, 0.015f), new Vector3(0.64f, 0.52f, 0.015f), panelMat);
            CreateBoardPart("SupportArm", boardRoot.transform, new Vector3(0.18f, -0.44f, -0.02f), new Vector3(0.04f, 0.38f, 0.04f), trimMat);

            GameObject canvasGo = new GameObject("ControlsGuideCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            canvasGo.transform.SetParent(boardRoot.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 0f, 0.026f);
            canvasGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            RectTransform crt = canvasGo.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(620f, 500f);
            crt.localScale = Vector3.one * 0.001f;

            Canvas cv = canvasGo.GetComponent<Canvas>();
            cv.renderMode = RenderMode.WorldSpace;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 15f;
            scaler.referencePixelsPerUnit = 100f;

            GameObject hdrGo = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
            hdrGo.transform.SetParent(canvasGo.transform, false);
            RectTransform hrt = hdrGo.GetComponent<RectTransform>();
            hrt.anchoredPosition = new Vector2(0f, 195f);
            hrt.sizeDelta = new Vector2(580f, 54f);
            TextMeshProUGUI hdrTmp = hdrGo.GetComponent<TextMeshProUGUI>();
            if (crispFont != null) hdrTmp.font = crispFont;
            hdrTmp.text = "META QUEST 3 CONTROLS";
            hdrTmp.fontSize = 30f;
            hdrTmp.fontStyle = FontStyles.Bold;
            hdrTmp.alignment = TextAlignmentOptions.Center;
            hdrTmp.color = new Color(0.22f, 0.85f, 1.0f, 1f);

            GameObject bodyGo = new GameObject("ControlsList", typeof(RectTransform), typeof(TextMeshProUGUI));
            bodyGo.transform.SetParent(canvasGo.transform, false);
            RectTransform brt = bodyGo.GetComponent<RectTransform>();
            brt.anchoredPosition = new Vector2(0f, -25f);
            brt.sizeDelta = new Vector2(580f, 390f);
            TextMeshProUGUI bodyTmp = bodyGo.GetComponent<TextMeshProUGUI>();
            if (crispFont != null) bodyTmp.font = crispFont;
            bodyTmp.fontSize = 23f;
            bodyTmp.enableAutoSizing = true;
            bodyTmp.fontSizeMin = 18f;
            bodyTmp.fontSizeMax = 24f;
            bodyTmp.alignment = TextAlignmentOptions.TopLeft;
            bodyTmp.color = new Color(0.88f, 0.93f, 0.98f, 1f);
            bodyTmp.text =
                "<color=#FFE07D><b>MOVEMENT</b></color>\n" +
                "• <b>Walk / Strafe:</b> Left Thumbstick\n" +
                "• <b>Teleport:</b> Push Right Stick Forward & Release\n" +
                "• <b>Turn (45°):</b> Flick Right Stick Left / Right\n\n" +
                "<color=#5CFFA0><b>OBJECTS & UI</b></color>\n" +
                "• <b>Grab / Hold:</b> Left or Right Grip\n" +
                "• <b>Spin / Pull Held Item:</b> Stick while Ray-Grabbing\n" +
                "• <b>Buttons / Sliders / UI:</b> Trigger or Index Poke\n\n" +
                "<color=#38D8FF><b>TABLET & MENU</b></color>\n" +
                "• <b>Lab Tablet:</b> Left <b>Y</b>   |   <b>Menu (Exit/Restart):</b> Left <b>X</b>";

            EditorUtility.SetDirty(tutGo);
            if (PrefabUtility.IsPartOfPrefabInstance(tutGo) && AssetDatabase.LoadAssetAtPath<GameObject>(TutorialPrefabPath) != null)
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(tutGo, TutorialPrefabPath, InteractionMode.AutomatedAction);
            }

            return true;
        }

        private static Button CreateMenuButton(
            string name, Transform parent, Vector2 pos, Vector2 size, string text, Color bgColor, TMP_FontAsset font)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = bgColor;

            Button btn = go.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = cb;

            GameObject lblGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10f, 4f);
            lrt.offsetMax = new Vector2(-10f, -4f);

            TextMeshProUGUI tmp = lblGo.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = 25f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
        }

        private static TMP_Text CreateMenuLabelBox(
            string name, Transform parent, Vector2 pos, Vector2 size, string text, Color bgColor, TMP_FontAsset font)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = bgColor;

            GameObject lblGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lrt = lblGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = lblGo.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = 24f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.90f, 0.96f, 1.0f, 1f);
            return tmp;
        }

        private static void CreateBoardPart(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            if (mat != null)
            {
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;
            }
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
