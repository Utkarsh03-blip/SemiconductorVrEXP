using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Exports DialogueLine texts for offline speech synthesis, configures imported AudioClips
    /// as mono CompressedInMemory clips and wires them to DialogueLine ScriptableObjects,
    /// sets up 3D spatial NPCNarratorAudio on NPCScientist (Y = 1.65m head emitter), and adds
    /// synchronized Voice Mute toggles to NPCSpeechBubble and Floating LabTablet Settings.
    /// Does not modify Stations 1-6, Assessment Board, or SemiconductorModel physics.
    /// </summary>
    public static class NarrationAudioSetupBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string NarrationFolder = "Assets/PNJunctionLab/Audio/Narration";
        private const string DataDialogueRoot = "Assets/PNJunctionLab/Data";
        private const string NpcPrefabPath = "Assets/PNJunctionLab/Prefabs/NPCScientist.prefab";
        private const string TabletPrefabPath = "Assets/PNJunctionLab/Prefabs/LabTablet.prefab";

        [MenuItem("PNJunctionLab/Import & Wire NPC Narration Audio")]
        public static void ImportAndWireMenu()
        {
            Debug.Log(ImportConfigureAndWireAll());
        }

        /// <summary>
        /// Exports all DialogueLine asset names and sanitized speech strings to a UTF-8 TSV manifest
        /// so offline PowerShell System.Speech.Synthesis can render all WAV clips accurately.
        /// </summary>
        public static string ExportDialogueManifest(string manifestOutputPath)
        {
            EnsureFolders();
            string[] guids = AssetDatabase.FindAssets("t:DialogueLine", new[] { DataDialogueRoot });
            var lines = new List<string>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                DialogueLine dl = AssetDatabase.LoadAssetAtPath<DialogueLine>(assetPath);
                if (dl == null) continue;

                string baseName = Path.GetFileNameWithoutExtension(assetPath);
                string spoken = NPCNarratorAudio.SanitizeForSpeech(dl.Text);
                spoken = spoken.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
                lines.Add(baseName + "\t" + assetPath + "\t" + spoken);
            }

            lines.Sort(StringComparer.Ordinal);
            string dir = Path.GetDirectoryName(manifestOutputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllLines(manifestOutputPath, lines, new UTF8Encoding(false));
            return $"ExportedDialogueManifest: count={lines.Count} -> {manifestOutputPath}";
        }

        /// <summary>
        /// Refreshes and configures all WAV files in Assets/PNJunctionLab/Audio/Narration/,
        /// assigns AudioClips onto all matching DialogueLine ScriptableObjects, and configures
        /// NPCNarratorAudio + Mute UI on NPCScientist and LabTablet without modifying stations.
        /// </summary>
        public static string ImportConfigureAndWireAll()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            int configuredClips = ConfigureNarrationAudioImporters();
            int wiredLines = WireDialogueLineClips();

            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.EndsWith("PNJunctionLab.unity", StringComparison.OrdinalIgnoreCase))
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            NPCNarratorAudio narrator = SetupNpcNarratorAndBubbleToggle();
            SetupLabTabletMuteSetting(narrator);

            int badScalers = AuditWorldCanvasDPU();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"NarrationAudioSetup Complete: ClipsConfigured={configuredClips}, DialogueLinesWired={wiredLines}, Narrator={(narrator != null)}, BadCanvasDPU={badScalers}.";
        }

        private static int ConfigureNarrationAudioImporters()
        {
            if (!Directory.Exists(NarrationFolder))
            {
                return 0;
            }

            string[] wavFiles = Directory.GetFiles(NarrationFolder, "*.wav", SearchOption.TopDirectoryOnly);
            int configured = 0;

            for (int i = 0; i < wavFiles.Length; i++)
            {
                string assetPath = wavFiles[i].Replace('\\', '/');
                AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
                if (importer == null) continue;

                bool changed = false;
                if (!importer.forceToMono)
                {
                    importer.forceToMono = true;
                    changed = true;
                }

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.CompressedInMemory
                    || settings.compressionFormat != AudioCompressionFormat.Vorbis
                    || Mathf.Abs(settings.quality - 0.70f) > 0.02f)
                {
                    settings.loadType = AudioClipLoadType.CompressedInMemory;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = 0.70f;
                    settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                    importer.defaultSampleSettings = settings;
                    changed = true;
                }

                SerializedObject impSo = new SerializedObject(importer);
                SerializedProperty preloadProp = impSo.FindProperty("m_PreloadAudioData");
                if (preloadProp != null && !preloadProp.boolValue)
                {
                    preloadProp.boolValue = true;
                    impSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                }

                configured++;
            }

            return configured;
        }

        private static int WireDialogueLineClips()
        {
            string[] guids = AssetDatabase.FindAssets("t:DialogueLine", new[] { DataDialogueRoot });
            int wired = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                DialogueLine dl = AssetDatabase.LoadAssetAtPath<DialogueLine>(assetPath);
                if (dl == null) continue;

                string baseName = Path.GetFileNameWithoutExtension(assetPath);
                string clipPath = $"{NarrationFolder}/{baseName}.wav";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null)
                {
                    dl.SetVoiceClip(clip);
                    EditorUtility.SetDirty(dl);
                    wired++;
                }
            }

            return wired;
        }

        private static NPCNarratorAudio SetupNpcNarratorAndBubbleToggle()
        {
            NPCController npcCtrl = Object.FindAnyObjectByType<NPCController>();
            if (npcCtrl == null)
            {
                throw new InvalidOperationException("NPCController (NPCScientist) not found.");
            }

            GameObject npcGo = npcCtrl.gameObject;
            DialogueSystem dlgSys = Object.FindAnyObjectByType<DialogueSystem>();

            // Create/preserve dedicated 3D VoiceHeadEmitter at Y = 1.65m inside NPCScientist (separate from NPCModelSlot).
            Transform headEmitter = npcGo.transform.Find("VoiceHeadEmitter");
            if (headEmitter == null)
            {
                GameObject heGo = new GameObject("VoiceHeadEmitter");
                heGo.transform.SetParent(npcGo.transform, false);
                headEmitter = heGo.transform;
            }
            headEmitter.localPosition = new Vector3(0f, 1.65f, 0.04f);
            headEmitter.localRotation = Quaternion.identity;

            AudioSource voiceSource = headEmitter.GetComponent<AudioSource>();
            if (voiceSource == null)
            {
                voiceSource = headEmitter.gameObject.AddComponent<AudioSource>();
            }
            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
            voiceSource.spatialBlend = 1.0f;
            voiceSource.rolloffMode = AudioRolloffMode.Logarithmic;
            voiceSource.minDistance = 1.2f;
            voiceSource.maxDistance = 14.0f;
            voiceSource.dopplerLevel = 0f;
            voiceSource.volume = 0.95f;
            EditorUtility.SetDirty(voiceSource);

            NPCNarratorAudio narrator = npcGo.GetComponent<NPCNarratorAudio>();
            if (narrator == null)
            {
                narrator = npcGo.AddComponent<NPCNarratorAudio>();
            }

            SerializedObject narSo = new SerializedObject(narrator);
            LabTextStyleHelper.SetObjRef(narSo, "dialogueSystem", dlgSys);
            LabTextStyleHelper.SetObjRef(narSo, "voiceAudioSource", voiceSource);
            LabTextStyleHelper.SetObjRef(narSo, "headAnchor", headEmitter);
            LabTextStyleHelper.SetFloat(narSo, "spatialBlend", 1.0f);
            LabTextStyleHelper.SetFloat(narSo, "minDistance", 1.2f);
            LabTextStyleHelper.SetFloat(narSo, "maxDistance", 14.0f);
            LabTextStyleHelper.SetFloat(narSo, "dopplerLevel", 0f);
            LabTextStyleHelper.SetFloat(narSo, "voiceVolume", 0.95f);
            narSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(narrator);

            // Wire narrator onto NPCController and NPCProceduralAnimator so Talk gesture syncs cleanly with active audio.
            SerializedObject npcSo = new SerializedObject(npcCtrl);
            LabTextStyleHelper.SetObjRef(npcSo, "narratorAudio", narrator);
            npcSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(npcCtrl);

            NPCProceduralAnimator procAnim = npcGo.GetComponentInChildren<NPCProceduralAnimator>(true);
            if (procAnim != null)
            {
                SerializedObject paSo = new SerializedObject(procAnim);
                LabTextStyleHelper.SetObjRef(paSo, "narratorAudio", narrator);
                paSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(procAnim);
            }

            // Add header-bar Voice Mute Toggle button cleanly onto NPCSpeechBubble/SpeechCanvas (DPU = 15).
            NPCSpeechBubble bubble = npcGo.GetComponentInChildren<NPCSpeechBubble>(true);
            if (bubble != null)
            {
                Canvas speechCanvas = bubble.GetComponentInChildren<Canvas>(true);
                if (speechCanvas != null)
                {
                    CanvasScaler scaler = speechCanvas.GetComponent<CanvasScaler>();
                    if (scaler != null)
                    {
                        scaler.dynamicPixelsPerUnit = 15f;
                        EditorUtility.SetDirty(scaler);
                    }

                    Transform speakerTf = speechCanvas.transform.Find("Speaker");
                    if (speakerTf != null)
                    {
                        RectTransform srt = speakerTf.GetComponent<RectTransform>();
                        srt.anchoredPosition = new Vector2(-145f, 210f);
                        srt.sizeDelta = new Vector2(610f, 48f);
                        EditorUtility.SetDirty(speakerTf.gameObject);
                    }

                    Transform oldMute = speechCanvas.transform.Find("VoiceMuteButton");
                    if (oldMute != null)
                    {
                        Object.DestroyImmediate(oldMute.gameObject);
                    }

                    GameObject muteGo = new GameObject("VoiceMuteButton");
                    muteGo.transform.SetParent(speechCanvas.transform, false);
                    RectTransform mrt = muteGo.AddComponent<RectTransform>();
                    mrt.anchoredPosition = new Vector2(332f, 210f);
                    mrt.sizeDelta = new Vector2(236f, 52f);

                    Image muteImg = muteGo.AddComponent<Image>();
                    muteImg.color = new Color(0.14f, 0.52f, 0.46f, 1f);

                    Button muteBtn = muteGo.AddComponent<Button>();
                    muteBtn.targetGraphic = muteImg;
                    ColorBlock colors = muteBtn.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
                    colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                    muteBtn.colors = colors;

                    TMP_Text muteLbl = LabTextStyleHelper.CreateTMPText(
                        muteGo.transform, "Label", "Voice: ON", 24f,
                        Vector2.zero, new Vector2(220f, 44f), Color.white,
                        TextAlignmentOptions.Center, FontStyles.Bold);
                    muteLbl.enableAutoSizing = true;
                    muteLbl.fontSizeMin = 18f;
                    muteLbl.fontSizeMax = 25f;

                    AudioSource bubbleAudio = bubble.GetComponent<AudioSource>() ?? npcGo.GetComponent<AudioSource>();
                    InteractionFeedback muteFeedback = muteGo.AddComponent<InteractionFeedback>();
                    SerializedObject fbSo = new SerializedObject(muteFeedback);
                    LabTextStyleHelper.SetObjRef(fbSo, "audioSource", bubbleAudio);
                    LabTextStyleHelper.SetBool(fbSo, "playHoverSound", true);
                    LabTextStyleHelper.SetBool(fbSo, "playSelectSounds", true);
                    fbSo.ApplyModifiedPropertiesWithoutUndo();

                    SerializedObject sbSo = new SerializedObject(bubble);
                    LabTextStyleHelper.SetObjRef(sbSo, "narratorAudio", narrator);
                    LabTextStyleHelper.SetObjRef(sbSo, "voiceMuteButton", muteBtn);
                    LabTextStyleHelper.SetObjRef(sbSo, "voiceMuteLabel", muteLbl);
                    LabTextStyleHelper.SetObjRef(sbSo, "voiceMuteBackground", muteImg);
                    LabTextStyleHelper.SetObjRef(sbSo, "voiceMuteFeedback", muteFeedback);
                    sbSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(bubble);
                }
            }

            if (PrefabUtility.IsPartOfPrefabInstance(npcGo))
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(npcGo, NpcPrefabPath, InteractionMode.AutomatedAction);
            }

            return narrator;
        }

        private static void SetupLabTabletMuteSetting(NPCNarratorAudio narrator)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(TabletPrefabPath);
            if (prefabRoot != null)
            {
                try
                {
                    Transform contentRoot = prefabRoot.transform.Find("ContentRoot");
                    if (contentRoot == null)
                    {
                        GameObject crGo = new GameObject("ContentRoot");
                        crGo.transform.SetParent(prefabRoot.transform, false);
                        crGo.transform.localPosition = Vector3.zero;
                        crGo.transform.localRotation = Quaternion.identity;
                        crGo.transform.localScale = Vector3.one;
                        contentRoot = crGo.transform;
                    }
                    contentRoot.gameObject.SetActive(true);
                    contentRoot.localScale = Vector3.one;

                    // Ensure visual geometry and canvas live under ContentRoot inside the prefab itself.
                    var toMove = new List<Transform>();
                    for (int i = 0; i < prefabRoot.transform.childCount; i++)
                    {
                        Transform ch = prefabRoot.transform.GetChild(i);
                        if (ch == contentRoot || ch.name == "GrabAttachPoint" || ch.name == "InteractionTooltip")
                        {
                            continue;
                        }
                        toMove.Add(ch);
                    }

                    for (int i = 0; i < toMove.Count; i++)
                    {
                        toMove[i].SetParent(contentRoot, false);
                    }

                    LabTablet prefabTablet = prefabRoot.GetComponent<LabTablet>();
                    Transform settingsPanel = contentRoot.Find("TabletCanvas/SettingsPanel");
                    if (settingsPanel != null && prefabTablet != null)
                    {
                        (string name, float y)[] layout =
                        {
                            ("TextSizeButton", 118f),
                            ("LocomotionButton", 46f),
                            ("PostureButton", -26f),
                            ("TurnModeButton", -98f)
                        };

                        for (int i = 0; i < layout.Length; i++)
                        {
                            Transform btnTf = settingsPanel.Find(layout[i].name);
                            if (btnTf != null)
                            {
                                RectTransform brt = btnTf.GetComponent<RectTransform>();
                                brt.anchoredPosition = new Vector2(0f, layout[i].y);
                                brt.sizeDelta = new Vector2(680f, 58f);
                                TMP_Text lbl = btnTf.GetComponentInChildren<TMP_Text>(true);
                                if (lbl != null)
                                {
                                    lbl.fontSize = 24f;
                                    EditorUtility.SetDirty(lbl);
                                }
                                EditorUtility.SetDirty(btnTf.gameObject);
                            }
                        }

                        Transform oldVoiceBtn = settingsPanel.Find("NpcVoiceButton");
                        if (oldVoiceBtn != null)
                        {
                            Object.DestroyImmediate(oldVoiceBtn.gameObject);
                        }

                        GameObject voiceBtnGo = new GameObject("NpcVoiceButton");
                        voiceBtnGo.transform.SetParent(settingsPanel, false);
                        RectTransform vrt = voiceBtnGo.AddComponent<RectTransform>();
                        vrt.anchoredPosition = new Vector2(0f, -170f);
                        vrt.sizeDelta = new Vector2(680f, 58f);

                        Image vImg = voiceBtnGo.AddComponent<Image>();
                        vImg.color = new Color(0.14f, 0.45f, 0.44f, 1f);

                        Button vBtn = voiceBtnGo.AddComponent<Button>();
                        vBtn.targetGraphic = vImg;

                        TMP_Text vLbl = LabTextStyleHelper.CreateTMPText(
                            voiceBtnGo.transform, "Label", "NPC Voice Narration: ON", 24f,
                            Vector2.zero, new Vector2(650f, 48f), Color.white,
                            TextAlignmentOptions.Center, FontStyles.Bold);

                        SerializedObject ptSo = new SerializedObject(prefabTablet);
                        LabTextStyleHelper.SetObjRef(ptSo, "visualRoot", contentRoot);
                        LabTextStyleHelper.SetObjRef(ptSo, "npcVoiceToggleButton", vBtn);
                        LabTextStyleHelper.SetObjRef(ptSo, "npcVoiceLabel", vLbl);
                        LabTextStyleHelper.SetObjRef(ptSo, "npcVoiceButtonBackground", vImg);
                        ptSo.ApplyModifiedPropertiesWithoutUndo();
                    }

                    // Keep XRGrabInteractable collider & InteractionFeedback renderer aligned with ContentRoot children.
                    var pGrab = prefabRoot.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                    Transform chassis = contentRoot.Find("Chassis");
                    if (pGrab != null && chassis != null && chassis.GetComponent<BoxCollider>() != null)
                    {
                        pGrab.colliders.Clear();
                        pGrab.colliders.Add(chassis.GetComponent<BoxCollider>());
                        pGrab.throwOnDetach = false;
                    }

                    InteractionFeedback pFb = prefabRoot.GetComponent<InteractionFeedback>();
                    Transform rim = contentRoot.Find("RimAccent");
                    if (pFb != null && rim != null && rim.GetComponent<Renderer>() != null)
                    {
                        SerializedObject pFbSo = new SerializedObject(pFb);
                        LabTextStyleHelper.SetObjectArray(pFbSo, "highlightRenderers", new Object[] { rim.GetComponent<Renderer>() });
                        pFbSo.ApplyModifiedPropertiesWithoutUndo();
                    }

                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, TabletPrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            // Ensure the scene LabTablet instance binds its scene references + ContentRoot visuals cleanly.
            LabTablet sceneTablet = Object.FindAnyObjectByType<LabTablet>(FindObjectsInactive.Include);
            if (sceneTablet != null)
            {
                Transform cr = sceneTablet.transform.Find("ContentRoot");
                Transform chassis = cr != null ? cr.Find("Chassis") : null;
                var grab = sceneTablet.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (grab != null && chassis != null && chassis.GetComponent<BoxCollider>() != null)
                {
                    grab.colliders.Clear();
                    grab.colliders.Add(chassis.GetComponent<BoxCollider>());
                    EditorUtility.SetDirty(grab);
                }

                SerializedObject sceneTabSo = new SerializedObject(sceneTablet);
                if (cr != null) LabTextStyleHelper.SetObjRef(sceneTabSo, "visualRoot", cr);
                LabTextStyleHelper.SetObjRef(sceneTabSo, "narratorAudio", narrator);
                sceneTabSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(sceneTablet);
            }
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/PNJunctionLab/Audio"))
            {
                AssetDatabase.CreateFolder("Assets/PNJunctionLab", "Audio");
            }

            if (!AssetDatabase.IsValidFolder(NarrationFolder))
            {
                AssetDatabase.CreateFolder("Assets/PNJunctionLab/Audio", "Narration");
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
                    if (canvases[i].name.StartsWith("HW2_", StringComparison.Ordinal)) continue;
                    bad++;
                }
            }
            return bad;
        }
    }
}
