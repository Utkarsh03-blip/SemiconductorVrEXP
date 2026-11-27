using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Extensions.PerformanceSettings;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Object = UnityEngine.Object;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Disables the XR Device Simulator, configures Meta Quest 3 controller bindings
    /// (Left Stick Walk, Right Stick Teleport/Snap Turn, Grip Grab + Stick Spin/Pull,
    /// Trigger/Poke UI, Left Y Lab Tablet), and initializes Android OpenXR + PlayerSettings for APK builds.
    /// </summary>
    public static class Quest3BuildAndControlsConfigurator
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string SimulatorSettingsPath = "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset";
        private const string XRSettingsAssetPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        private const string OpenXRPackageSettingsPath = "Assets/XR/Settings/OpenXR Package Settings.asset";
        private const string OpenXRLoaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";

        [MenuItem("PNJunctionLab/Configure Meta Quest 3 Controls & Android APK Settings")]
        public static void ApplyMenu()
        {
            Debug.Log(ConfigureAll());
        }

        public static string ConfigureAll()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.EndsWith("PNJunctionLab.unity", StringComparison.OrdinalIgnoreCase))
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            bool simDisabled = DisableDeviceSimulator();
            int attachTuned = ConfigureQuest3ControlsInScene();
            string xrSummary = ConfigureXRManagementAndOpenXR();
            ConfigureAndroidPlayerSettings();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool androidInstalled = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
            return $"Quest3Config Complete: SimulatorDisabled={simDisabled}, AttachTuned={attachTuned}, {xrSummary}, AndroidModuleInstalled={androidInstalled}.";
        }

        private static bool DisableDeviceSimulator()
        {
            bool updated = false;
            ScriptableObject simSettings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SimulatorSettingsPath);
            if (simSettings != null)
            {
                SerializedObject so = new SerializedObject(simSettings);
                SerializedProperty autoProp = so.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab");
                if (autoProp != null)
                {
                    autoProp.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(simSettings);
                    updated = true;
                }
            }

            foreach (XRInteractionSimulator sim in Resources.FindObjectsOfTypeAll<XRInteractionSimulator>())
            {
                if (sim != null && sim.gameObject.scene.IsValid())
                {
                    sim.gameObject.SetActive(false);
                    EditorUtility.SetDirty(sim.gameObject);
                }
            }

            return updated;
        }

        private static int ConfigureQuest3ControlsInScene()
        {
            // Enable Left-Stick Continuous Move (Walking) alongside Right-Stick Teleport + Snap Turn.
            LocomotionModeController loco = Object.FindAnyObjectByType<LocomotionModeController>(FindObjectsInactive.Include);
            if (loco != null)
            {
                SerializedObject locoSo = new SerializedObject(loco);
                LabTextStyleHelper.SetBool(locoSo, "continuousMoveEnabled", true);
                LabTextStyleHelper.SetBool(locoSo, "smoothTurnEnabled", false);
                locoSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(loco);
            }

            int tuned = 0;
            foreach (InteractionAttachController iac in Object.FindObjectsByType<InteractionAttachController>(FindObjectsInactive.Include))
            {
                SerializedObject iacSo = new SerializedObject(iac);
                LabTextStyleHelper.SetBool(iacSo, "m_UseManipulationInput", true);
                SerializedProperty rotSpeed = iacSo.FindProperty("m_ManipulationRotateSpeed");
                if (rotSpeed != null) rotSpeed.floatValue = 180f;
                SerializedProperty transSpeed = iacSo.FindProperty("m_ManipulationTranslateSpeed");
                if (transSpeed != null) transSpeed.floatValue = 1.5f;
                iacSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(iac);
                tuned++;
            }

            return tuned;
        }

        private static string ConfigureXRManagementAndOpenXR()
        {
            if (!EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(
                    XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perBuildTarget)
                || perBuildTarget == null)
            {
                perBuildTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XRSettingsAssetPath);
                if (perBuildTarget == null)
                {
                    perBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    Directory.CreateDirectory(Path.GetDirectoryName(XRSettingsAssetPath));
                    AssetDatabase.CreateAsset(perBuildTarget, XRSettingsAssetPath);
                }

                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perBuildTarget, true);
            }

            EnsureOpenXRLoaderForGroup(perBuildTarget, BuildTargetGroup.Android);
            EnsureOpenXRLoaderForGroup(perBuildTarget, BuildTargetGroup.Standalone);

            // Ensure OpenXRSettings exists for Android & Standalone and enable Quest 3 profiles.
            EnsureAndroidOpenXRSettingsEntry();

            int androidFeaturesEnabled = EnableQuestFeaturesForGroup(BuildTargetGroup.Android);
            int standaloneFeaturesEnabled = EnableQuestFeaturesForGroup(BuildTargetGroup.Standalone);

            EditorUtility.SetDirty(perBuildTarget);
            return $"OpenXR(AndroidFeatures={androidFeaturesEnabled}, StandaloneFeatures={standaloneFeaturesEnabled})";
        }

        private static void EnsureOpenXRLoaderForGroup(XRGeneralSettingsPerBuildTarget perBuildTarget, BuildTargetGroup group)
        {
            XRGeneralSettings settings = perBuildTarget.SettingsForBuildTarget(group);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                settings.name = $"XRGeneralSettings_{group}";
                AssetDatabase.AddObjectToAsset(settings, perBuildTarget);
                perBuildTarget.SetSettingsForBuildTarget(group, settings);
            }

            settings.InitManagerOnStart = true;

            if (settings.Manager == null)
            {
                XRManagerSettings manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                manager.name = $"XRManagerSettings_{group}";
                manager.automaticLoading = true;
                manager.automaticRunning = true;
                AssetDatabase.AddObjectToAsset(manager, settings);
                settings.Manager = manager;
            }

            bool hasOpenXR = false;
            var loaders = settings.Manager.activeLoaders;
            for (int i = 0; i < loaders.Count; i++)
            {
                if (loaders[i] is OpenXRLoader)
                {
                    hasOpenXR = true;
                    break;
                }
            }

            if (!hasOpenXR)
            {
                XRPackageMetadataStore.AssignLoader(settings.Manager, OpenXRLoaderTypeName, group);
            }

            EditorUtility.SetDirty(settings.Manager);
            EditorUtility.SetDirty(settings);
        }

        private static void EnsureAndroidOpenXRSettingsEntry()
        {
            if (OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android) != null)
            {
                return;
            }

            Object pkgSettings = AssetDatabase.LoadMainAssetAtPath(OpenXRPackageSettingsPath);
            if (pkgSettings == null)
            {
                return;
            }

            // Use OpenXRPackageSettings reflection to create the Android OpenXRSettings + features cleanly.
            var method = pkgSettings.GetType().GetMethod(
                "GetSettingsForBuildTargetGroup",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(pkgSettings, new object[] { BuildTargetGroup.Android });
                EditorUtility.SetDirty(pkgSettings);
            }
        }

        private static int EnableQuestFeaturesForGroup(BuildTargetGroup group)
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings == null)
            {
                return 0;
            }

            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;

            int enabledCount = 0;
            foreach (OpenXRFeature feature in settings.GetFeatures<OpenXRFeature>())
            {
                if (feature == null)
                {
                    continue;
                }

                bool shouldEnable =
                    feature is MetaQuestTouchPlusControllerProfile
                    || feature is MetaQuestTouchProControllerProfile
                    || feature is OculusTouchControllerProfile
                    || feature is HandInteractionProfile
                    || feature is HandCommonPosesInteraction
                    || feature is XrPerformanceSettingsFeature
                    || feature is FoveatedRenderingFeature;

                if (shouldEnable)
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                    enabledCount++;
                }
            }

            EditorUtility.SetDirty(settings);
            return enabledCount;
        }

        private static void ConfigureAndroidPlayerSettings()
        {
            PlayerSettings.companyName = "PNJunctionLab";
            PlayerSettings.productName = "P-N Junction VR Lab";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.PNJunctionLab.VRLab");
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, true);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
        }
    }
}
