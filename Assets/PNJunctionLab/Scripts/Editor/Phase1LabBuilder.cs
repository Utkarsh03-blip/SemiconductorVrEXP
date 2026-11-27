using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Builds and wires Phase 1 assets, project XR settings, materials, Tutorial Pad prefab,
    /// and the PNJunctionLab scene cleanly in Unity.
    /// </summary>
    public static class Phase1LabBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string MaterialsFolder = "Assets/PNJunctionLab/Materials";
        private const string PrefabsFolder = "Assets/PNJunctionLab/Prefabs";
        private const string TutorialPrefabPath = PrefabsFolder + "/TutorialPadStation.prefab";

        private const string XrOriginPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string VignettePrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets/TunnelingVignette/TunnelingVignette.prefab";
        private const string SimulatorPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.7.0/XR Interaction Simulator/XR Interaction Simulator.prefab";
        private const string DefaultInputActionsPath =
            "Assets/Samples/XR Interaction Toolkit/3.7.0/Starter Assets/XRI Default Input Actions.inputactions";
        private const string VolumeProfilePath = "Assets/Settings/SampleSceneProfile.asset";

        private struct MatSet
        {
            public Material Floor;
            public Material Wall;
            public Material Trim;
            public Material Window;
            public Material CeilingLight;
            public Material TableTop;
            public Material TableFrame;
            public Material AccentBase;
            public Material SiliconCube;
            public Material ButtonGreen;
            public Material ButtonAmber;
            public Material ScreenPanel;
        }

        [MenuItem("PNJunctionLab/Build Phase 1 Scene")]
        public static void BuildPhase1Menu()
        {
            BuildPhase1();
        }

        public static string BuildPhase1()
        {
            ConfigureXRProjectSettings();
            MatSet mats = CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "PNJunctionLab";

            SetupLightingAndVolume();
            Camera mainCam = SetupXRAndInput(out GameObject xrOriginGo);
            BuildLabRoom(mats, mainCam);
            BuildStations(mats, mainCam);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildSettingsScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return saved
                ? "Phase 1 scene and prefabs built successfully at " + ScenePath
                : "Failed to save scene at " + ScenePath;
        }

        private static void ConfigureXRProjectSettings()
        {
            Object layerSettingsObj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/XRI/Settings/Resources/InteractionLayerSettings.asset");
            if (layerSettingsObj != null)
            {
                SerializedObject layerSo = new SerializedObject(layerSettingsObj);
                SerializedProperty namesProp = layerSo.FindProperty("m_LayerNames");
                if (namesProp != null && namesProp.arraySize > 31)
                {
                    namesProp.GetArrayElementAtIndex(31).stringValue = "Teleport";
                }
                layerSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(layerSettingsObj);
            }

            Object simSettingsObj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset");
            GameObject simPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefabPath);
            if (simSettingsObj != null && simPrefab != null)
            {
                SerializedObject so = new SerializedObject(simSettingsObj);
                SetBool(so, "m_AutomaticallyInstantiateSimulatorPrefab", true);
                SetBool(so, "m_AutomaticallyInstantiateInEditorOnly", true);
                SetBool(so, "m_UseClassic", false);
                SerializedProperty prefabProp = so.FindProperty("m_SimulatorPrefab");
                if (prefabProp != null)
                {
                    prefabProp.objectReferenceValue = simPrefab;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(simSettingsObj);
            }
        }

        private static MatSet CreateMaterials()
        {
            Texture2D floorTex = CreateOrLoadFloorGridTexture();

            MatSet m = new MatSet
            {
                Floor = CreateOrUpdateLitMaterial(MaterialsFolder + "/Mat_LabFloor.mat",
                    new Color(0.88f, 0.91f, 0.94f, 1f), 0f, 0.25f, Color.black, false, floorTex, new Vector2(16f, 11f)),
                Wall = CreateOrUpdateLitMaterial(MaterialsFolder + "/Mat_LabWall.mat",
                    new Color(0.92f, 0.94f, 0.96f, 1f), 0f, 0.14f, Color.black, false),
                Trim = CreateOrUpdateLitMaterial(MaterialsFolder + "/Mat_LabTrim.mat",
                    new Color(0.16f, 0.21f, 0.28f, 1f), 0.15f, 0.35f, Color.black, false),
                Window = CreateOrUpdateLitMaterial(MaterialsFolder + "/Mat_LabWindow.mat",
                    new Color(0.55f, 0.82f, 0.96f, 0.32f), 0.05f, 0.85f, new Color(0.10f, 0.22f, 0.32f), true),
                CeilingLight = CreateOrUpdateLitMaterial(MaterialsFolder + "/Mat_CeilingLight.mat",
                    Color.white, 0f, 0.2f, new Color(1.1f, 1.25f, 1.4f), false),
                TableTop = CreateOrUpdateLitMaterial(MaterialsFolder + "/Mat_TableTop.mat",
                    new Color(0.86f, 0.89f, 0.93f, 1f), 0f, 0.30f, Color.black, false),
                TableFrame = CreateOrUpdateLitMaterial(MaterialsFolder + "/Mat_TableFrame.mat",
                    new Color(0.14f, 0.17f, 0.22f, 1f), 0.35f, 0.42f, Color.black, false),
                AccentBase = CreateOrUpdateLitMaterial(MaterialsFolder + "/Mat_AccentBase.mat",
                    new Color(0.15f, 0.78f, 0.98f, 1f), 0f, 0.45f, new Color(0.10f, 0.55f, 0.75f), false),
                SiliconCube = CreateOrUpdateLitMaterial(MaterialsFolder + "/Mat_SiliconCube.mat",
                    new Color(0.16f, 0.48f, 0.86f, 1f), 0.35f, 0.65f, new Color(0.03f, 0.10f, 0.20f), false),
                ButtonGreen = CreateOrUpdateLitMaterial(MaterialsFolder + "/Mat_ButtonGreen.mat",
                    new Color(0.18f, 0.78f, 0.42f, 1f), 0f, 0.50f, new Color(0.04f, 0.24f, 0.10f), false),
                ButtonAmber = CreateOrUpdateLitMaterial(MaterialsFolder + "/Mat_ButtonAmber.mat",
                    new Color(0.90f, 0.60f, 0.14f, 1f), 0f, 0.50f, new Color(0.22f, 0.14f, 0.02f), false),
                ScreenPanel = CreateOrUpdateLitMaterial(MaterialsFolder + "/Mat_ScreenPanel.mat",
                    new Color(0.038f, 0.058f, 0.095f, 1f), 0f, 0.06f, new Color(0.006f, 0.012f, 0.022f), false)
            };

            return m;
        }

        private static Texture2D CreateOrLoadFloorGridTexture()
        {
            string texPath = MaterialsFolder + "/Tex_LabFloorGrid.png";
            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color tileBase = new Color(0.84f, 0.87f, 0.91f, 1f);
            Color tileSpeck = new Color(0.81f, 0.85f, 0.89f, 1f);
            Color grout = new Color(0.62f, 0.67f, 0.74f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x < 4 || y < 4 || x >= size - 4 || y >= size - 4;
                    bool isSubGrid = Mathf.Abs(x - size / 2) < 2 || Mathf.Abs(y - size / 2) < 2;
                    if (isBorder)
                    {
                        tex.SetPixel(x, y, grout);
                    }
                    else if (isSubGrid)
                    {
                        tex.SetPixel(x, y, Color.Lerp(tileBase, grout, 0.45f));
                    }
                    else
                    {
                        bool checker = ((x / 64) + (y / 64)) % 2 == 0;
                        tex.SetPixel(x, y, checker ? tileBase : tileSpeck);
                    }
                }
            }

            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        }

        private static Material CreateOrUpdateLitMaterial(
            string path,
            Color baseColor,
            float metallic,
            float smoothness,
            Color emission,
            bool transparent,
            Texture2D mainTex = null,
            Vector2? tiling = null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != urpLit)
            {
                mat.shader = urpLit;
            }

            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            if (mainTex != null)
            {
                mat.SetTexture("_BaseMap", mainTex);
                if (tiling.HasValue)
                {
                    mat.SetTextureScale("_BaseMap", tiling.Value);
                }
            }

            if (emission.maxColorComponent > 0.001f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }

            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_ZWrite", 1f);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Geometry;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetupLightingAndVolume()
        {
            GameObject root = new GameObject("[Lighting & Environment]");

            // Single realtime Directional Light (Quest 2 budget: max 1 realtime light).
            GameObject lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1.0f, 0.97f, 0.92f, 1f);
            dirLight.intensity = 1.15f;
            dirLight.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.84f, 0.90f, 0.96f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.78f, 0.85f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.47f, 0.52f, 1f);

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile != null)
            {
                GameObject volumeGo = new GameObject("Global Volume");
                volumeGo.transform.SetParent(root.transform, false);
                Volume vol = volumeGo.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = profile;
            }
        }

        private static Camera SetupXRAndInput(out GameObject xrOriginGo)
        {
            GameObject xrRoot = new GameObject("[XR & Input]");

            GameObject managerGo = new GameObject("XR Interaction Manager");
            managerGo.transform.SetParent(xrRoot.transform, false);
            managerGo.AddComponent<XRInteractionManager>();

            GameObject eventGo = new GameObject("EventSystem");
            eventGo.transform.SetParent(xrRoot.transform, false);
            eventGo.AddComponent<EventSystem>();
            XRUIInputModule uiModule = eventGo.AddComponent<XRUIInputModule>();
            ConfigureXRUIInputModule(uiModule);

            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefabPath);
            xrOriginGo = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            xrOriginGo.name = "XR Origin (XR Rig)";
            xrOriginGo.transform.SetParent(xrRoot.transform, false);
            xrOriginGo.transform.SetPositionAndRotation(new Vector3(0f, 0f, -4.2f), Quaternion.identity);

            Transform cameraOffset = xrOriginGo.transform.Find("Camera Offset");
            Camera mainCam = xrOriginGo.GetComponentInChildren<Camera>(true);
            if (mainCam != null)
            {
                mainCam.tag = "MainCamera";
                mainCam.allowMSAA = true;
                UniversalAdditionalCameraData camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
                if (camData == null)
                {
                    camData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }
                camData.antialiasing = AntialiasingMode.None;
                camData.renderPostProcessing = true;
                camData.allowXRRendering = true;
                EditorUtility.SetDirty(camData);
            }

            // Attach TunnelingVignette under Main Camera for comfort during continuous move.
            TunnelingVignetteController vignetteController = null;
            GameObject vignettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VignettePrefabPath);
            if (vignettePrefab != null && mainCam != null)
            {
                GameObject vignetteGo = (GameObject)PrefabUtility.InstantiatePrefab(vignettePrefab);
                vignetteGo.name = "TunnelingVignette";
                vignetteGo.transform.SetParent(mainCam.transform, false);
                vignetteGo.transform.localPosition = Vector3.zero;
                vignetteGo.transform.localRotation = Quaternion.identity;
                vignetteController = vignetteGo.GetComponent<TunnelingVignetteController>();
            }

            ControllerInputActionManager leftActions = xrOriginGo.transform.Find("Camera Offset/Left Controller")
                ?.GetComponent<ControllerInputActionManager>();
            ControllerInputActionManager rightActions = xrOriginGo.transform.Find("Camera Offset/Right Controller")
                ?.GetComponent<ControllerInputActionManager>();

            if (leftActions != null)
            {
                leftActions.smoothMotionEnabled = false;
                leftActions.smoothTurnEnabled = false;
                EditorUtility.SetDirty(leftActions);
            }

            if (rightActions != null)
            {
                rightActions.smoothMotionEnabled = false;
                rightActions.smoothTurnEnabled = false;
                EditorUtility.SetDirty(rightActions);
            }

            DynamicMoveProvider moveProvider = xrOriginGo.GetComponentInChildren<DynamicMoveProvider>(true);
            SnapTurnProvider snapTurnProvider = xrOriginGo.GetComponentInChildren<SnapTurnProvider>(true);
            TeleportationProvider teleportProvider = xrOriginGo.GetComponentInChildren<TeleportationProvider>(true);

            if (vignetteController != null)
            {
                vignetteController.locomotionVignetteProviders = new List<LocomotionVignetteProvider>();
                if (moveProvider != null)
                {
                    vignetteController.locomotionVignetteProviders.Add(new LocomotionVignetteProvider
                    {
                        enabled = true,
                        locomotionProvider = moveProvider
                    });
                }

                if (snapTurnProvider != null)
                {
                    vignetteController.locomotionVignetteProviders.Add(new LocomotionVignetteProvider
                    {
                        enabled = true,
                        locomotionProvider = snapTurnProvider
                    });
                }

                if (teleportProvider != null)
                {
                    vignetteController.locomotionVignetteProviders.Add(new LocomotionVignetteProvider
                    {
                        enabled = true,
                        locomotionProvider = teleportProvider
                    });
                }

                EditorUtility.SetDirty(vignetteController);
            }

            LocomotionModeController locoController = xrOriginGo.AddComponent<LocomotionModeController>();
            SerializedObject locoSo = new SerializedObject(locoController);
            SetObjRef(locoSo, "leftControllerActions", leftActions);
            SetObjRef(locoSo, "rightControllerActions", rightActions);
            SetObjRef(locoSo, "continuousMoveProvider", moveProvider);
            SetObjRef(locoSo, "snapTurnProvider", snapTurnProvider);
            SetObjRef(locoSo, "teleportProvider", teleportProvider);
            SetObjRef(locoSo, "vignetteController", vignetteController);
            SetObjRef(locoSo, "cameraOffset", cameraOffset);
            SetBool(locoSo, "continuousMoveEnabled", false);
            SetBool(locoSo, "smoothTurnEnabled", false);
            locoSo.ApplyModifiedPropertiesWithoutUndo();

            return mainCam;
        }

        private static void ConfigureXRUIInputModule(XRUIInputModule uiModule)
        {
            uiModule.enableXRInput = true;
            uiModule.enableMouseInput = true;
            uiModule.enableTouchInput = true;
            uiModule.enableGamepadInput = true;
            uiModule.enableJoystickInput = true;
            uiModule.enableBuiltinActionsAsFallback = true;

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(DefaultInputActionsPath);
            Dictionary<string, InputActionReference> refs = new Dictionary<string, InputActionReference>();
            foreach (Object obj in subAssets)
            {
                InputActionReference actionRef = obj as InputActionReference;
                if (actionRef != null && actionRef.action != null && actionRef.action.actionMap != null)
                {
                    string key = actionRef.action.actionMap.name + "/" + actionRef.action.name;
                    refs[key] = actionRef;
                }
            }

            if (refs.TryGetValue("XRI UI/Point", out InputActionReference pt)) uiModule.pointAction = pt;
            if (refs.TryGetValue("XRI UI/Click", out InputActionReference clk)) uiModule.leftClickAction = clk;
            if (refs.TryGetValue("XRI UI/MiddleClick", out InputActionReference mid)) uiModule.middleClickAction = mid;
            if (refs.TryGetValue("XRI UI/RightClick", out InputActionReference rgt)) uiModule.rightClickAction = rgt;
            if (refs.TryGetValue("XRI UI/ScrollWheel", out InputActionReference scr)) uiModule.scrollWheelAction = scr;
            if (refs.TryGetValue("XRI UI/Navigate", out InputActionReference nav)) uiModule.navigateAction = nav;
            if (refs.TryGetValue("XRI UI/Submit", out InputActionReference sub)) uiModule.submitAction = sub;
            if (refs.TryGetValue("XRI UI/Cancel", out InputActionReference can)) uiModule.cancelAction = can;

            EditorUtility.SetDirty(uiModule);
        }

        private static void BuildLabRoom(MatSet mats, Camera mainCam)
        {
            GameObject room = new GameObject("[LabRoom]");

            // 16m x 11m floor with top surface exactly at y = 0.
            GameObject floor = CreatePrimitiveBox("LabFloor", room.transform,
                new Vector3(0f, -0.05f, 0f), new Vector3(16f, 0.1f, 11f), mats.Floor, true);
            TeleportationArea teleportArea = floor.AddComponent<TeleportationArea>();
            teleportArea.interactionLayers = GetTeleportLayerMask();
            teleportArea.matchDirectionalInput = true;

            // Out-of-bounds respawn trigger under the room.
            GameObject catchZone = new GameObject("RespawnCatchZone");
            catchZone.transform.SetParent(room.transform, false);
            catchZone.transform.position = new Vector3(0f, -1.5f, 0f);
            BoxCollider catchCol = catchZone.AddComponent<BoxCollider>();
            catchCol.isTrigger = true;
            catchCol.size = new Vector3(28f, 1.5f, 22f);
            catchZone.AddComponent<RespawnZone>();

            // Perimeter walls (16m x 11m x 3.2m).
            GameObject walls = new GameObject("Walls");
            walls.transform.SetParent(room.transform, false);

            CreatePrimitiveBox("Wall_North", walls.transform, new Vector3(0f, 1.6f, 5.55f), new Vector3(16.2f, 3.2f, 0.1f), mats.Wall, true);
            CreatePrimitiveBox("Wall_West", walls.transform, new Vector3(-8.05f, 1.6f, 0f), new Vector3(0.1f, 3.2f, 11f), mats.Wall, true);
            CreatePrimitiveBox("Wall_East", walls.transform, new Vector3(8.05f, 1.6f, 0f), new Vector3(0.1f, 3.2f, 11f), mats.Wall, true);

            // South wall with central double-door entrance opening.
            CreatePrimitiveBox("Wall_South_Left", walls.transform, new Vector3(-4.7f, 1.6f, -5.55f), new Vector3(6.8f, 3.2f, 0.1f), mats.Wall, true);
            CreatePrimitiveBox("Wall_South_Right", walls.transform, new Vector3(4.7f, 1.6f, -5.55f), new Vector3(6.8f, 3.2f, 0.1f), mats.Wall, true);
            CreatePrimitiveBox("Wall_South_Header", walls.transform, new Vector3(0f, 2.75f, -5.55f), new Vector3(2.6f, 0.9f, 0.1f), mats.Wall, true);
            CreatePrimitiveBox("EntranceDoorFrame", walls.transform, new Vector3(0f, 1.15f, -5.53f), new Vector3(2.6f, 2.3f, 0.06f), mats.Window, false);

            // Baseboard trim.
            CreatePrimitiveBox("Trim_North", walls.transform, new Vector3(0f, 0.06f, 5.49f), new Vector3(16f, 0.12f, 0.03f), mats.Trim, true);
            CreatePrimitiveBox("Trim_West", walls.transform, new Vector3(-7.99f, 0.06f, 0f), new Vector3(0.03f, 0.12f, 11f), mats.Trim, true);
            CreatePrimitiveBox("Trim_East", walls.transform, new Vector3(7.99f, 0.06f, 0f), new Vector3(0.03f, 0.12f, 11f), mats.Trim, true);

            // Ceiling + 6 emissive light panels (no extra realtime lights).
            GameObject ceiling = new GameObject("Ceiling");
            ceiling.transform.SetParent(room.transform, false);
            CreatePrimitiveBox("CeilingSlab", ceiling.transform, new Vector3(0f, 3.25f, 0f), new Vector3(16.2f, 0.1f, 11.2f), mats.Wall, true);

            float[] lightXs = { -4.4f, 0f, 4.4f };
            float[] lightZs = { -2.4f, 2.4f };
            for (int xi = 0; xi < lightXs.Length; xi++)
            {
                for (int zi = 0; zi < lightZs.Length; zi++)
                {
                    CreatePrimitiveBox("CeilingLightPanel_" + xi + "_" + zi, ceiling.transform,
                        new Vector3(lightXs[xi], 3.19f, lightZs[zi]), new Vector3(2.4f, 0.03f, 0.75f), mats.CeilingLight, true);
                }
            }

            // Windows (North + East).
            BuildWindow(room.transform, "Window_North_Left", new Vector3(-4.2f, 1.8f, 5.48f), Vector3.zero, 2.8f, 1.3f, mats);
            BuildWindow(room.transform, "Window_North_Right", new Vector3(4.2f, 1.8f, 5.48f), Vector3.zero, 2.8f, 1.3f, mats);
            BuildWindow(room.transform, "Window_East", new Vector3(7.98f, 1.8f, 0f), new Vector3(0f, 90f, 0f), 3.6f, 1.3f, mats);

            // Decor: Fume Hood, Storage Shelves, Periodic Table Poster, Safety Signs, Entrance Plaque, U-Path Floor Guides.
            GameObject decor = new GameObject("LabDecor");
            decor.transform.SetParent(room.transform, false);

            BuildFumeHood(decor.transform, new Vector3(-7.45f, 0f, -2.6f), mats);
            BuildStorageShelf(decor.transform, "StorageShelf_West", new Vector3(-7.55f, 0f, 4.35f), 0f, mats);
            BuildStorageShelf(decor.transform, "StorageShelf_East", new Vector3(7.55f, 0f, 4.35f), 180f, mats);

            BuildWallSignBoard(decor.transform, "EntranceBanner",
                new Vector3(0f, 2.65f, -5.45f), new Vector3(0f, 180f, 0f),
                new Vector2(3.6f, 0.60f),
                "P-N JUNCTION VR LAB",
                "Interactive Semiconductor Physics Laboratory",
                mats, mainCam, 32f, 19f);

            BuildPeriodicTablePoster(decor.transform, new Vector3(0f, 1.85f, 5.47f), mats, mainCam);

            BuildWallSignBoard(decor.transform, "SafetySign_Cleanroom",
                new Vector3(-7.95f, 1.95f, -4.15f), new Vector3(0f, -90f, 0f),
                new Vector2(1.70f, 0.90f),
                "LAB SAFETY",
                "1. Handle Si/Ge wafers cleanly\n2. Return props to bench sockets\n3. Use series resistor in bias tests",
                mats, mainCam, 26f, 19f);

            BuildWallSignBoard(decor.transform, "SafetySign_Comfort",
                new Vector3(7.95f, 2.0f, -3.4f), new Vector3(0f, 90f, 0f),
                new Vector2(1.70f, 0.90f),
                "VR COMFORT & CONTROLS",
                "Teleport + Snap Turn enabled by default\nUse Left/Right Rays to select & point\nCheck Docs/CONTROLS.md for Simulator keys",
                mats, mainCam, 26f, 18f);

            BuildFloorUPathGuides(decor.transform, mats);
        }

        private static void BuildWindow(Transform parent, string name, Vector3 pos, Vector3 euler, float width, float height, MatSet mats)
        {
            GameObject win = new GameObject(name);
            win.transform.SetParent(parent, false);
            win.transform.position = pos;
            win.transform.rotation = Quaternion.Euler(euler);

            CreatePrimitiveBox("Glass", win.transform, Vector3.zero, new Vector3(width, height, 0.02f), mats.Window, false);
            CreatePrimitiveBox("Frame_Top", win.transform, new Vector3(0f, height * 0.5f, 0f), new Vector3(width + 0.08f, 0.06f, 0.05f), mats.Trim, true);
            CreatePrimitiveBox("Frame_Bottom", win.transform, new Vector3(0f, -height * 0.5f, 0f), new Vector3(width + 0.08f, 0.06f, 0.05f), mats.Trim, true);
            CreatePrimitiveBox("Frame_Left", win.transform, new Vector3(-width * 0.5f, 0f, 0f), new Vector3(0.06f, height, 0.05f), mats.Trim, true);
            CreatePrimitiveBox("Frame_Right", win.transform, new Vector3(width * 0.5f, 0f, 0f), new Vector3(0.06f, height, 0.05f), mats.Trim, true);
        }

        private static void BuildFumeHood(Transform parent, Vector3 pos, MatSet mats)
        {
            GameObject hood = new GameObject("FumeHood");
            hood.transform.SetParent(parent, false);
            hood.transform.position = pos;

            CreatePrimitiveBox("BaseCabinet", hood.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.95f, 0.90f, 1.8f), mats.TableFrame, true);
            CreatePrimitiveBox("WorkDeck", hood.transform, new Vector3(0f, 0.92f, 0f), new Vector3(0.98f, 0.04f, 1.84f), mats.TableTop, true);
            CreatePrimitiveBox("BackPanel", hood.transform, new Vector3(-0.43f, 1.65f, 0f), new Vector3(0.08f, 1.44f, 1.8f), mats.Wall, true);
            CreatePrimitiveBox("Side_Left", hood.transform, new Vector3(0f, 1.65f, -0.86f), new Vector3(0.92f, 1.44f, 0.08f), mats.Trim, true);
            CreatePrimitiveBox("Side_Right", hood.transform, new Vector3(0f, 1.65f, 0.86f), new Vector3(0.92f, 1.44f, 0.08f), mats.Trim, true);
            CreatePrimitiveBox("TopCanopy", hood.transform, new Vector3(0f, 2.40f, 0f), new Vector3(0.96f, 0.14f, 1.84f), mats.Trim, true);
            CreatePrimitiveBox("ExhaustDuct", hood.transform, new Vector3(-0.15f, 2.82f, 0f), new Vector3(0.36f, 0.72f, 0.42f), mats.TableFrame, true);
            CreatePrimitiveBox("GlassSash", hood.transform, new Vector3(0.42f, 1.65f, 0f), new Vector3(0.02f, 1.05f, 1.62f), mats.Window, false);
        }

        private static void BuildStorageShelf(Transform parent, string name, Vector3 pos, float rotY, MatSet mats)
        {
            GameObject shelf = new GameObject(name);
            shelf.transform.SetParent(parent, false);
            shelf.transform.position = pos;
            shelf.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            float[] heights = { 0.18f, 0.85f, 1.52f, 2.15f };
            for (int i = 0; i < heights.Length; i++)
            {
                CreatePrimitiveBox("Shelf_" + i, shelf.transform, new Vector3(0f, heights[i], 0f), new Vector3(0.52f, 0.04f, 1.4f), mats.TableTop, true);
            }

            CreatePrimitiveBox("Post_FL", shelf.transform, new Vector3(0.22f, 1.1f, -0.66f), new Vector3(0.05f, 2.2f, 0.05f), mats.TableFrame, true);
            CreatePrimitiveBox("Post_FR", shelf.transform, new Vector3(0.22f, 1.1f, 0.66f), new Vector3(0.05f, 2.2f, 0.05f), mats.TableFrame, true);
            CreatePrimitiveBox("Post_BL", shelf.transform, new Vector3(-0.22f, 1.1f, -0.66f), new Vector3(0.05f, 2.2f, 0.05f), mats.TableFrame, true);
            CreatePrimitiveBox("Post_BR", shelf.transform, new Vector3(-0.22f, 1.1f, 0.66f), new Vector3(0.05f, 2.2f, 0.05f), mats.TableFrame, true);

            // Decorative wafer boxes on shelves.
            CreatePrimitiveBox("WaferBox_A", shelf.transform, new Vector3(0f, 0.99f, -0.35f), new Vector3(0.24f, 0.24f, 0.32f), mats.SiliconCube, true);
            CreatePrimitiveBox("WaferBox_B", shelf.transform, new Vector3(0f, 1.64f, 0.25f), new Vector3(0.22f, 0.20f, 0.28f), mats.ButtonGreen, true);
        }

        private static void BuildWallSignBoard(
            Transform parent,
            string name,
            Vector3 pos,
            Vector3 euler,
            Vector2 size,
            string header,
            string body,
            MatSet mats,
            Camera mainCam,
            float headerSize,
            float bodySize)
        {
            GameObject board = new GameObject(name);
            board.transform.SetParent(parent, false);
            board.transform.position = pos;
            board.transform.rotation = Quaternion.Euler(euler);

            CreatePrimitiveBox("Frame", board.transform, Vector3.zero, new Vector3(size.x, size.y, 0.04f), mats.Trim, true);
            CreatePrimitiveBox("Panel", board.transform, new Vector3(0f, 0f, -0.015f), new Vector3(size.x - 0.06f, size.y - 0.06f, 0.02f), mats.ScreenPanel, true);

            // Uniform pixels-per-meter (600 px/m -> scale 0.00167) keeps glyphs sharp across distance.
            Vector2 worldSize = new Vector2(size.x - 0.1f, size.y - 0.1f);
            int pxW = Mathf.RoundToInt(worldSize.x * 600f);
            int pxH = Mathf.RoundToInt(worldSize.y * 600f);
            Canvas canvas = CreateWorldCanvas("UI", board.transform, new Vector3(0f, 0f, -0.03f), Vector3.zero,
                worldSize, pxW, pxH, mainCam, false);

            CreateTMPText(canvas.transform, "Header", header, headerSize * 2.15f,
                new Vector2(0f, pxH * 0.27f), new Vector2(pxW - 64f, pxH * 0.28f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);
            CreateTMPText(canvas.transform, "Body", body, bodySize * 1.95f,
                new Vector2(0f, -pxH * 0.14f), new Vector2(pxW - 64f, pxH * 0.56f), Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
        }

        private static void BuildPeriodicTablePoster(Transform parent, Vector3 pos, MatSet mats, Camera mainCam)
        {
            GameObject poster = new GameObject("PeriodicTablePoster");
            poster.transform.SetParent(parent, false);
            poster.transform.position = pos;
            poster.transform.rotation = Quaternion.identity;

            CreatePrimitiveBox("Frame", poster.transform, Vector3.zero, new Vector3(3.6f, 1.75f, 0.04f), mats.Trim, true);
            CreatePrimitiveBox("Backing", poster.transform, new Vector3(0f, 0f, -0.015f), new Vector3(3.5f, 1.65f, 0.02f), mats.ScreenPanel, true);

            Canvas canvas = CreateWorldCanvas("PosterUI", poster.transform, new Vector3(0f, 0f, -0.03f), Vector3.zero,
                new Vector2(3.4f, 1.56f), 1700, 780, mainCam, false);

            CreateTMPText(canvas.transform, "Title", "PERIODIC TABLE — SEMICONDUCTOR GROUPS", 58f,
                new Vector2(0f, 305f), new Vector2(1600f, 90f), new Color(0.28f, 0.92f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            CreateTMPText(canvas.transform, "Col_GroupIII",
                "<color=#FF9966><b>GROUP III</b>\n(Acceptors, 3 valence e-)</color>\n\n<b>B (5)   Al (13)\nGa (31)   In (49)</b>\n\nCreates P-Type holes",
                36f, new Vector2(-545f, -55f), new Vector2(510f, 520f), Color.white, TextAlignmentOptions.Center, FontStyles.Normal);

            CreateTMPText(canvas.transform, "Col_GroupIV",
                "<color=#66CCFF><b>GROUP IV</b>\n(Intrinsic, 4 valence e-)</color>\n\n<b>C (6)   Si (14)   Ge (32)</b>\n\n4 covalent bonds in\ndiamond lattice",
                36f, new Vector2(0f, -55f), new Vector2(520f, 520f), Color.white, TextAlignmentOptions.Center, FontStyles.Normal);

            CreateTMPText(canvas.transform, "Col_GroupV",
                "<color=#70FFB0><b>GROUP V</b>\n(Donors, 5 valence e-)</color>\n\n<b>P (15)   As (33)   Sb (51)</b>\n\nDonates free\nN-Type electrons",
                36f, new Vector2(545f, -55f), new Vector2(510f, 520f), Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
        }

        private static void BuildFloorUPathGuides(Transform parent, MatSet mats)
        {
            GameObject guides = new GameObject("FloorUPathGuides");
            guides.transform.SetParent(parent, false);

            Vector3[] points =
            {
                new Vector3(0f, 0.004f, -3.7f),
                new Vector3(-3.2f, 0.004f, -3.0f),
                new Vector3(-4.2f, 0.004f, -0.2f),
                new Vector3(-4.2f, 0.004f, 2.6f),
                new Vector3(-2.2f, 0.004f, 2.6f),
                new Vector3(1.8f, 0.004f, 2.6f),
                new Vector3(4.2f, 0.004f, 2.2f),
                new Vector3(4.2f, 0.004f, -0.8f),
                new Vector3(4.2f, 0.004f, -3.4f)
            };

            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[i + 1];
                Vector3 mid = (a + b) * 0.5f;
                Vector3 dir = b - a;
                float len = dir.magnitude;
                GameObject strip = CreatePrimitiveBox("GuideStrip_" + i, guides.transform,
                    mid, new Vector3(0.06f, 0.004f, Mathf.Max(0.1f, len - 0.8f)), mats.AccentBase, false);
                strip.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                Collider c = strip.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
        }

        private static void BuildStations(MatSet mats, Camera mainCam)
        {
            GameObject stationsRoot = new GameObject("[Stations]");

            // T. Tutorial Pad (built as self-contained prefab and placed in scene).
            GameObject tutorialRoot = BuildTutorialPadStation(mats, mainCam);
            PrefabUtility.SaveAsPrefabAssetAndConnect(tutorialRoot, TutorialPrefabPath, InteractionMode.AutomatedAction);
            tutorialRoot.transform.SetParent(stationsRoot.transform, false);
            tutorialRoot.transform.SetPositionAndRotation(new Vector3(-4.6f, 0f, -3.0f), Quaternion.Euler(0f, 90f, 0f));

            // Stations 1..6 and Assessment Whiteboard (Phase 1 U-shape anchors, signs & dimmed benches).
            CreatePlaceholderStation(stationsRoot.transform, "Station_1_Intrinsic", "1", "Intrinsic Silicon",
                new Vector3(-5.6f, 0f, -0.2f), 90f, false, mats, mainCam);
            CreatePlaceholderStation(stationsRoot.transform, "Station_2_NType", "2", "N-Type Doping",
                new Vector3(-5.6f, 0f, 2.8f), 90f, false, mats, mainCam);
            CreatePlaceholderStation(stationsRoot.transform, "Station_3_PType", "3", "P-Type Doping",
                new Vector3(-2.2f, 0f, 4.0f), 180f, false, mats, mainCam);
            CreatePlaceholderStation(stationsRoot.transform, "Station_4_Junction", "4", "Junction Formation",
                new Vector3(1.8f, 0f, 4.0f), 180f, false, mats, mainCam);
            CreatePlaceholderStation(stationsRoot.transform, "Station_5_Biasing", "5", "Biasing",
                new Vector3(5.6f, 0f, 2.2f), -90f, false, mats, mainCam);
            CreatePlaceholderStation(stationsRoot.transform, "Station_6_IV", "6", "I-V Characteristics",
                new Vector3(5.6f, 0f, -0.8f), -90f, false, mats, mainCam);
            CreatePlaceholderStation(stationsRoot.transform, "Station_A_Assessment", "A", "Assessment Board",
                new Vector3(5.6f, 0f, -3.4f), -90f, true, mats, mainCam);
        }

        private static GameObject BuildTutorialPadStation(MatSet mats, Camera mainCam)
        {
            GameObject root = new GameObject("Station_T_TutorialPad");
            AudioSource stationAudio = root.AddComponent<AudioSource>();
            stationAudio.spatialBlend = 0.85f;
            stationAudio.playOnAwake = false;

            // 0.9m high workbench facing +Z in local space.
            BuildWorkbench(root.transform, 1.6f, 0.8f, 0.9f, mats);

            // Teleport Anchor + Floor Sign + Hologram Title.
            StationBeacon beacon = BuildStationBeaconParts(
                root.transform, "T", "Tutorial Pad", new Vector3(0f, 0f, 1.35f), true, mats, mainCam, 2.05f,
                out TeleportationAnchor anchor);

            // 1. Test Cube (>= 5 cm: 14 cm) with XRGrabInteractable + InteractionFeedback + RespawnableItem.
            GameObject homeMarker = new GameObject("CubeHomeMarker");
            homeMarker.transform.SetParent(root.transform, false);
            homeMarker.transform.localPosition = new Vector3(-0.48f, 0.985f, 0.04f);

            GameObject cubePad = CreatePrimitiveBox("CubeHomePad", root.transform,
                new Vector3(-0.48f, 0.905f, 0.04f), new Vector3(0.22f, 0.01f, 0.22f), mats.TableFrame, false);
            Collider padCol = cubePad.GetComponent<Collider>();
            if (padCol != null) Object.DestroyImmediate(padCol);

            GameObject testCubeGo = CreatePrimitiveBox("TestSiliconCube", root.transform,
                new Vector3(-0.48f, 0.985f, 0.04f), new Vector3(0.14f, 0.14f, 0.14f), mats.SiliconCube, false);
            Rigidbody cubeRb = testCubeGo.AddComponent<Rigidbody>();
            cubeRb.mass = 0.4f;
            cubeRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            XRGrabInteractable cubeGrab = testCubeGo.AddComponent<XRGrabInteractable>();
            cubeGrab.useDynamicAttach = true;
            cubeGrab.throwOnDetach = true;

            InteractionFeedback cubeFeedback = AddInteractionFeedback(
                testCubeGo, cubeGrab, new[] { testCubeGo.GetComponent<Renderer>() }, stationAudio,
                "Grab Test Cube", new Vector3(0f, 0.16f, 0f), mats, mainCam);

            RespawnableItem cubeRespawn = testCubeGo.AddComponent<RespawnableItem>();
            SerializedObject respawnSo = new SerializedObject(cubeRespawn);
            SetObjRef(respawnSo, "homeAnchor", homeMarker.transform);
            SetObjRef(respawnSo, "audioSource", stationAudio);
            respawnSo.ApplyModifiedPropertiesWithoutUndo();

            // 2. Socket with glowing ring & XRSocketInteractor.
            GameObject socketRoot = new GameObject("TutorialDropSocket");
            socketRoot.transform.SetParent(root.transform, false);
            socketRoot.transform.localPosition = new Vector3(-0.04f, 0.91f, 0.04f);

            GameObject socketBase = CreatePrimitiveBox("SocketPad", socketRoot.transform,
                new Vector3(0f, 0f, 0f), new Vector3(0.24f, 0.02f, 0.24f), mats.TableFrame, false);
            Object.DestroyImmediate(socketBase.GetComponent<Collider>());

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "SocketRing";
            ring.transform.SetParent(socketRoot.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.008f, 0f);
            ring.transform.localScale = new Vector3(0.24f, 0.008f, 0.24f);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            Renderer ringRenderer = ring.GetComponent<Renderer>();
            ringRenderer.sharedMaterial = mats.AccentBase;

            GameObject attachPt = new GameObject("AttachPoint");
            attachPt.transform.SetParent(socketRoot.transform, false);
            attachPt.transform.localPosition = new Vector3(0f, 0.08f, 0f);

            SphereCollider socketTrigger = socketRoot.AddComponent<SphereCollider>();
            socketTrigger.isTrigger = true;
            socketTrigger.center = new Vector3(0f, 0.08f, 0f);
            socketTrigger.radius = 0.15f;

            XRSocketInteractor socket = socketRoot.AddComponent<XRSocketInteractor>();
            socket.attachTransform = attachPt.transform;
            socket.showInteractableHoverMeshes = true;

            SocketRingFeedback socketFeedback = socketRoot.AddComponent<SocketRingFeedback>();
            SerializedObject sockFbSo = new SerializedObject(socketFeedback);
            SetObjRef(sockFbSo, "socket", socket);
            SetObjRef(sockFbSo, "ringRenderer", ringRenderer);
            SetObjRef(sockFbSo, "audioSource", stationAudio);
            sockFbSo.ApplyModifiedPropertiesWithoutUndo();

            // 3. Green Lab Push Button (supports poke + ray select).
            PhysicalPushButton testButton = BuildPushButton(
                root.transform, "LabPushButton", new Vector3(0.44f, 0.90f, 0.08f),
                new Vector3(0.11f, 0.045f, 0.11f), mats.ButtonGreen, mats, stationAudio,
                "Press / Poke Button", mainCam);

            // 4. Return Cube Push Button on front-left table edge.
            PhysicalPushButton returnButton = BuildPushButton(
                root.transform, "ReturnCubeButton", new Vector3(-0.48f, 0.90f, 0.27f),
                new Vector3(0.085f, 0.036f, 0.085f), mats.ButtonAmber, mats, stationAudio,
                "Return Cube", mainCam);

            // Register RespawnZone managed items on Tutorial Pad.
            RespawnZone tableZone = root.AddComponent<RespawnZone>();
            SerializedObject tzSo = new SerializedObject(tableZone);
            SerializedProperty arrProp = tzSo.FindProperty("managedItems");
            if (arrProp != null)
            {
                arrProp.arraySize = 1;
                arrProp.GetArrayElementAtIndex(0).objectReferenceValue = cubeRespawn;
            }
            tzSo.ApplyModifiedPropertiesWithoutUndo();

            // 5. Interactive Tutorial Screen at back edge of bench.
            GameObject screenRoot = new GameObject("TutorialScreen");
            screenRoot.transform.SetParent(root.transform, false);
            screenRoot.transform.localPosition = new Vector3(0f, 1.38f, -0.32f);
            screenRoot.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);

            CreatePrimitiveBox("StandPostL", root.transform, new Vector3(-0.36f, 1.05f, -0.34f), new Vector3(0.04f, 0.30f, 0.04f), mats.TableFrame, true);
            CreatePrimitiveBox("StandPostR", root.transform, new Vector3(0.36f, 1.05f, -0.34f), new Vector3(0.04f, 0.30f, 0.04f), mats.TableFrame, true);

            GameObject screenFrame = CreatePrimitiveBox("ScreenFrame", screenRoot.transform,
                Vector3.zero, new Vector3(1.18f, 0.64f, 0.04f), mats.TableFrame, false);
            CreatePrimitiveBox("ScreenBacking", screenRoot.transform,
                new Vector3(0f, 0f, 0.015f), new Vector3(1.12f, 0.58f, 0.02f), mats.ScreenPanel, false);

            GameObject statusLed = CreatePrimitiveBox("StatusLedBar", screenRoot.transform,
                new Vector3(0f, 0.285f, 0.024f), new Vector3(0.95f, 0.022f, 0.015f), mats.AccentBase, false);
            Object.DestroyImmediate(statusLed.GetComponent<Collider>());
            Renderer ledRenderer = statusLed.GetComponent<Renderer>();

            BoxCollider screenCollider = screenFrame.GetComponent<BoxCollider>();
            screenCollider.size = new Vector3(1.05f, 1.05f, 1.4f);

            XRSimpleInteractable screenInteractable = screenRoot.AddComponent<XRSimpleInteractable>();
            screenInteractable.colliders.Add(screenCollider);
            AddInteractionFeedback(screenRoot, screenInteractable, new[] { ledRenderer }, stationAudio,
                "Ray-Point Screen", new Vector3(0f, 0.40f, 0f), mats, mainCam);

            Canvas screenCanvas = CreateWorldCanvas("ScreenCanvas", screenRoot.transform,
                new Vector3(0f, 0f, 0.032f), new Vector3(0f, 180f, 0f),
                new Vector2(1.18f, 0.59f), 1000, 500, mainCam, true);

            CreateTMPText(screenCanvas.transform, "Header", "TUTORIAL PAD — VR INTERACTION CHECK", 38f,
                new Vector2(0f, 200f), new Vector2(940f, 54f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            TMP_Text statusBanner = CreateTMPText(screenCanvas.transform, "StatusBanner", "SKILLS VERIFIED: 0 / 5", 32f,
                new Vector2(0f, 142f), new Vector2(940f, 46f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            TMP_Text checklistText = CreateTMPText(screenCanvas.transform, "Checklist",
                "[ ] 1. Teleport onto Tutorial Anchor\n[ ] 2. Grab the Test Cube\n[ ] 3. Drop Cube into Socket\n[ ] 4. Press the Green Lab Button\n[ ] 5. Ray-Point at this Screen",
                34f,
                new Vector2(0f, -45f), new Vector2(920f, 290f), new Color(0.92f, 0.96f, 1.0f), TextAlignmentOptions.Left, FontStyles.Normal);

            // Wire TutorialPadController.
            TutorialPadController controller = root.AddComponent<TutorialPadController>();
            SerializedObject ctrlSo = new SerializedObject(controller);
            SetObjRef(ctrlSo, "stationBeacon", beacon);
            SetObjRef(ctrlSo, "tutorialAnchor", anchor);
            SetObjRef(ctrlSo, "testCube", cubeGrab);
            SetObjRef(ctrlSo, "testCubeRespawn", cubeRespawn);
            SetObjRef(ctrlSo, "testSocket", socket);
            SetObjRef(ctrlSo, "testButton", testButton);
            SetObjRef(ctrlSo, "returnCubeButton", returnButton);
            SetObjRef(ctrlSo, "screenInteractable", screenInteractable);
            SetObjRef(ctrlSo, "checklistText", checklistText);
            SetObjRef(ctrlSo, "statusBannerText", statusBanner);
            SetObjRef(ctrlSo, "screenStatusLed", ledRenderer);
            SetObjRef(ctrlSo, "audioSource", stationAudio);
            ctrlSo.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void CreatePlaceholderStation(
            Transform parent,
            string objName,
            string stationId,
            string title,
            Vector3 position,
            float rotY,
            bool isWhiteboard,
            MatSet mats,
            Camera mainCam)
        {
            GameObject root = new GameObject(objName);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotY, 0f));

            if (isWhiteboard)
            {
                CreatePrimitiveBox("BoardStandL", root.transform, new Vector3(-0.78f, 1.0f, 0f), new Vector3(0.06f, 2.0f, 0.50f), mats.TableFrame, true);
                CreatePrimitiveBox("BoardStandR", root.transform, new Vector3(0.78f, 1.0f, 0f), new Vector3(0.06f, 2.0f, 0.50f), mats.TableFrame, true);
                CreatePrimitiveBox("Whiteboard", root.transform, new Vector3(0f, 1.45f, 0f), new Vector3(1.65f, 1.15f, 0.06f), mats.ScreenPanel, true);
            }
            else
            {
                BuildWorkbench(root.transform, 1.5f, 0.8f, 0.9f, mats);
            }

            float holoHeight = isWhiteboard ? 2.28f : 2.05f;
            BuildStationBeaconParts(root.transform, stationId, title, new Vector3(0f, 0f, 1.35f), false, mats, mainCam, holoHeight, out _);
        }

        private static StationBeacon BuildStationBeaconParts(
            Transform parent,
            string stationId,
            string title,
            Vector3 anchorLocalPos,
            bool startUnlocked,
            MatSet mats,
            Camera mainCam,
            float holoHeight,
            out TeleportationAnchor anchor)
        {
            // TeleportationAnchor pad in front of bench (facing bench: local rotY = 180 since bench faces +Z).
            GameObject anchorGo = new GameObject("TeleportAnchor_" + stationId);
            anchorGo.transform.SetParent(parent, false);
            anchorGo.transform.localPosition = anchorLocalPos;
            anchorGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject spawnPoint = new GameObject("SpawnTransform");
            spawnPoint.transform.SetParent(anchorGo.transform, false);
            spawnPoint.transform.localPosition = Vector3.zero;
            spawnPoint.transform.localRotation = Quaternion.identity;

            GameObject padDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            padDisc.name = "AnchorPad";
            padDisc.transform.SetParent(anchorGo.transform, false);
            padDisc.transform.localPosition = new Vector3(0f, 0.006f, 0f);
            padDisc.transform.localScale = new Vector3(0.75f, 0.006f, 0.75f);
            Renderer padRenderer = padDisc.GetComponent<Renderer>();
            padRenderer.sharedMaterial = mats.TableFrame;

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
            anchor.interactionLayers = GetTeleportLayerMask();

            // Floor Number Sign next to pad.
            GameObject floorSign = CreatePrimitiveBox("FloorNumberSign", anchorGo.transform,
                new Vector3(0.58f, 0.01f, 0f), new Vector3(0.32f, 0.012f, 0.32f), mats.TableFrame, false);
            Object.DestroyImmediate(floorSign.GetComponent<Collider>());
            Canvas floorCanvas = CreateWorldCanvas("FloorNumberUI", floorSign.transform,
                new Vector3(0f, 0.008f, 0f), new Vector3(90f, 0f, 0f),
                new Vector2(0.30f, 0.30f), 320, 320, mainCam, false);
            TMP_Text floorNumText = CreateTMPText(floorCanvas.transform, "Number", stationId, 180f,
                Vector2.zero, new Vector2(300f, 300f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            // Floating Hologram Title above station with dark matte backing plaque for wall-free legibility.
            GameObject holoRoot = new GameObject("HologramTitle");
            holoRoot.transform.SetParent(parent, false);
            holoRoot.transform.localPosition = new Vector3(0f, holoHeight, 0f);

            GameObject holoBacking = CreatePrimitiveBox("HoloBacking", holoRoot.transform,
                new Vector3(0f, 0f, -0.014f), new Vector3(1.68f, 0.36f, 0.014f), mats.ScreenPanel, false);
            Object.DestroyImmediate(holoBacking.GetComponent<Collider>());

            GameObject holoBar = CreatePrimitiveBox("HoloAccentBar", holoRoot.transform,
                new Vector3(0f, -0.195f, -0.010f), new Vector3(1.68f, 0.025f, 0.024f), mats.AccentBase, false);
            Object.DestroyImmediate(holoBar.GetComponent<Collider>());
            Renderer holoBarRenderer = holoBar.GetComponent<Renderer>();

            Canvas holoCanvas = CreateWorldCanvas("HoloCanvas", holoRoot.transform,
                new Vector3(0f, 0f, 0.002f), new Vector3(0f, 180f, 0f),
                new Vector2(1.62f, 0.32f), 960, 190, mainCam, false);
            TMP_Text holoText = CreateTMPText(holoCanvas.transform, "Title", stationId + ". " + title, 64f,
                Vector2.zero, new Vector2(920f, 175f), new Color(0.92f, 0.98f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            StationBeacon beacon = parent.gameObject.AddComponent<StationBeacon>();
            SerializedObject bSo = new SerializedObject(beacon);
            SetStr(bSo, "stationId", stationId);
            SetStr(bSo, "stationTitle", title);
            SetObjRef(bSo, "floorNumberText", floorNumText);
            SetObjRef(bSo, "hologramTitleText", holoText);
            SetObjRef(bSo, "teleportAnchor", anchor);
            SetBool(bSo, "startUnlocked", startUnlocked);

            SerializedProperty renderersProp = bSo.FindProperty("accentRenderers");
            if (renderersProp != null)
            {
                renderersProp.arraySize = 2;
                renderersProp.GetArrayElementAtIndex(0).objectReferenceValue = ringRenderer;
                renderersProp.GetArrayElementAtIndex(1).objectReferenceValue = holoBarRenderer;
            }
            bSo.ApplyModifiedPropertiesWithoutUndo();

            return beacon;
        }

        private static PhysicalPushButton BuildPushButton(
            Transform parent,
            string name,
            Vector3 localPos,
            Vector3 capSize,
            Material capMat,
            MatSet mats,
            AudioSource audioSource,
            string tooltipLabel,
            Camera mainCam)
        {
            GameObject btnRoot = new GameObject(name);
            btnRoot.transform.SetParent(parent, false);
            btnRoot.transform.localPosition = localPos;

            CreatePrimitiveBox("Housing", btnRoot.transform,
                new Vector3(0f, 0.015f, 0f), new Vector3(capSize.x + 0.035f, 0.03f, capSize.z + 0.035f), mats.TableFrame, false);

            GameObject cap = CreatePrimitiveBox("ButtonCap", btnRoot.transform,
                new Vector3(0f, 0.032f + capSize.y * 0.5f, 0f), capSize, capMat, false);
            BoxCollider capCollider = cap.GetComponent<BoxCollider>();
            Renderer capRenderer = cap.GetComponent<Renderer>();

            XRSimpleInteractable simple = btnRoot.AddComponent<XRSimpleInteractable>();
            simple.colliders.Add(capCollider);

            XRPokeFilter pokeFilter = btnRoot.AddComponent<XRPokeFilter>();
            SerializedObject pokeSo = new SerializedObject(pokeFilter);
            SetObjRef(pokeSo, "m_Interactable", simple);
            SetObjRef(pokeSo, "m_PokeCollider", capCollider);
            SerializedProperty cfgProp = pokeSo.FindProperty("m_PokeConfiguration");
            if (cfgProp != null)
            {
                SerializedProperty useConst = cfgProp.FindPropertyRelative("m_UseConstant");
                if (useConst != null) useConst.boolValue = true;
                SerializedProperty constVal = cfgProp.FindPropertyRelative("m_ConstantValue");
                if (constVal != null)
                {
                    SetInt(constVal, "m_PokeDirection", (int)PokeAxis.NegativeY);
                    SetBool(constVal, "m_EnablePokeAngleThreshold", true);
                    SetFloat(constVal, "m_PokeAngleThreshold", 45f);
                }
            }
            pokeSo.ApplyModifiedPropertiesWithoutUndo();

            PhysicalPushButton pushBtn = btnRoot.AddComponent<PhysicalPushButton>();

            AddInteractionFeedback(btnRoot, simple, new[] { capRenderer }, audioSource,
                tooltipLabel, new Vector3(0f, 0.11f, 0f), mats, mainCam);

            // World-space UI Trigger/Click relay flush above ButtonCap top so T / Left Click work seamlessly alongside Grip/Pinch/Poke.
            Vector2 capFootprint = new Vector2(capSize.x + 0.03f, capSize.z + 0.03f);
            Canvas capUiCanvas = CreateWorldCanvas("CapUiOverlay", btnRoot.transform,
                new Vector3(0f, 0.034f + capSize.y + 0.003f, 0f), new Vector3(90f, 180f, 0f),
                capFootprint, 180, 180, mainCam, true);
            Button capUiBtn = CreateInvisibleClickButton(capUiCanvas.transform, "ClickTarget", new Vector2(180f, 180f));

            GameObject labelPlate = CreatePrimitiveBox("LabelPlate", btnRoot.transform,
                new Vector3(0f, 0.004f, capSize.z * 0.5f + 0.055f), new Vector3(0.24f, 0.008f, 0.065f), mats.TableFrame, false);
            Object.DestroyImmediate(labelPlate.GetComponent<Collider>());

            Canvas labelCanvas = CreateWorldCanvas("Label", btnRoot.transform,
                new Vector3(0f, 0.011f, capSize.z * 0.5f + 0.055f), new Vector3(90f, 180f, 0f),
                new Vector2(0.24f, 0.065f), 360, 98, mainCam, true);
            Button labelUiBtn = CreateInvisibleClickButton(labelCanvas.transform, "ClickLabel", new Vector2(360f, 98f));
            CreateTMPText(labelCanvas.transform, "Text", tooltipLabel.ToUpperInvariant(), 32f,
                Vector2.zero, new Vector2(340f, 88f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            SerializedObject pbSo = new SerializedObject(pushBtn);
            SetObjRef(pbSo, "interactable", simple);
            SetObjRef(pbSo, "uiRelayButton", capUiBtn);
            SetObjRef(pbSo, "uiLabelRelayButton", labelUiBtn);
            SetObjRef(pbSo, "capTransform", cap.transform);
            SetObjRef(pbSo, "audioSource", audioSource);
            pbSo.ApplyModifiedPropertiesWithoutUndo();

            return pushBtn;
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

        private static InteractionFeedback AddInteractionFeedback(
            GameObject target,
            XRBaseInteractable interactable,
            Renderer[] renderers,
            AudioSource audioSource,
            string tooltipLabel,
            Vector3 tooltipLocalPos,
            MatSet mats,
            Camera mainCam)
        {
            // Compensate parent scale (e.g. 14 cm Silicon Cube) so tooltip stays full world size.
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

            GameObject tipBg = CreatePrimitiveBox("Bg", tipRoot.transform, Vector3.zero, new Vector3(0.32f, 0.075f, 0.008f), mats.ScreenPanel, false);
            Object.DestroyImmediate(tipBg.GetComponent<Collider>());

            Canvas tipCanvas = CreateWorldCanvas("TipCanvas", tipRoot.transform,
                new Vector3(0f, 0f, 0.006f), new Vector3(0f, 180f, 0f),
                new Vector2(0.30f, 0.068f), 450, 102, mainCam, false);
            TMP_Text tipText = CreateTMPText(tipCanvas.transform, "Label", tooltipLabel, 38f,
                Vector2.zero, new Vector2(420f, 92f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            InteractionFeedback fb = target.AddComponent<InteractionFeedback>();
            SerializedObject so = new SerializedObject(fb);
            SetObjRef(so, "interactable", interactable);
            SetObjRef(so, "audioSource", audioSource);
            SetObjRef(so, "tooltipObject", tipRoot);
            SetObjRef(so, "tooltipText", tipText);
            SetStr(so, "tooltipLabel", tooltipLabel);

            SerializedProperty rendProp = so.FindProperty("highlightRenderers");
            if (rendProp != null && renderers != null)
            {
                rendProp.arraySize = renderers.Length;
                for (int i = 0; i < renderers.Length; i++)
                {
                    rendProp.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            tipRoot.SetActive(false);

            return fb;
        }

        private static void BuildWorkbench(Transform parent, float width, float depth, float height, MatSet mats)
        {
            GameObject bench = new GameObject("Workbench");
            bench.transform.SetParent(parent, false);

            // Top surface at y = height (0.90m).
            CreatePrimitiveBox("TableTop", bench.transform,
                new Vector3(0f, height - 0.025f, 0f), new Vector3(width, 0.05f, depth), mats.TableTop, true);

            float legH = height - 0.05f;
            float lx = width * 0.5f - 0.06f;
            float lz = depth * 0.5f - 0.06f;
            CreatePrimitiveBox("Leg_FL", bench.transform, new Vector3(-lx, legH * 0.5f, lz), new Vector3(0.06f, legH, 0.06f), mats.TableFrame, true);
            CreatePrimitiveBox("Leg_FR", bench.transform, new Vector3(lx, legH * 0.5f, lz), new Vector3(0.06f, legH, 0.06f), mats.TableFrame, true);
            CreatePrimitiveBox("Leg_BL", bench.transform, new Vector3(-lx, legH * 0.5f, -lz), new Vector3(0.06f, legH, 0.06f), mats.TableFrame, true);
            CreatePrimitiveBox("Leg_BR", bench.transform, new Vector3(lx, legH * 0.5f, -lz), new Vector3(0.06f, legH, 0.06f), mats.TableFrame, true);
            CreatePrimitiveBox("CrossBar", bench.transform, new Vector3(0f, 0.18f, -lz), new Vector3(width - 0.12f, 0.04f, 0.04f), mats.TableFrame, true);
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

        private static GameObject CreatePrimitiveBox(
            string name,
            Transform parent,
            Vector3 localPos,
            Vector3 scale,
            Material material,
            bool isStatic)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.isStatic = isStatic;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && material != null)
            {
                r.sharedMaterial = material;
            }
            return go;
        }

        private static InteractionLayerMask GetTeleportLayerMask()
        {
            int mask = InteractionLayerMask.GetMask("Teleport");
            return mask != 0 ? mask : unchecked((int)(1u << 31));
        }

        private static void SetBuildSettingsScene(string path)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(path, true)
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SetObjRef(SerializedObject so, string propName, Object value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetBool(SerializedObject so, string propName, bool value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null) p.boolValue = value;
        }

        private static void SetStr(SerializedObject so, string propName, string value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null) p.stringValue = value;
        }

        private static void SetInt(SerializedProperty parent, string propName, int value)
        {
            SerializedProperty p = parent.FindPropertyRelative(propName);
            if (p != null) p.intValue = value;
        }

        private static void SetBool(SerializedProperty parent, string propName, bool value)
        {
            SerializedProperty p = parent.FindPropertyRelative(propName);
            if (p != null) p.boolValue = value;
        }

        private static void SetFloat(SerializedProperty parent, string propName, float value)
        {
            SerializedProperty p = parent.FindPropertyRelative(propName);
            if (p != null) p.floatValue = value;
        }
    }
}
