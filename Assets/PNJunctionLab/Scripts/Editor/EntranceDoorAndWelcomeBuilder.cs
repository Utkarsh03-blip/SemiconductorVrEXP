using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Replaces the transparent south-wall doorway void with a detailed Cleanroom Double Entrance Door
    /// (with solid BoxCollider, metallic jambs, frosted vision windows, push bars, and airlock backing)
    /// and syncs LabTablet's startup welcome settings across scene & prefab.
    /// Does not modify Stations 1-6, Assessment Board, or SemiconductorModel physics.
    /// </summary>
    public static class EntranceDoorAndWelcomeBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string TabletPrefabPath = "Assets/PNJunctionLab/Prefabs/LabTablet.prefab";
        private const string MaterialsRoot = "Assets/PNJunctionLab/Materials";

        [MenuItem("PNJunctionLab/Build Entrance Door & Sync Tablet Welcome")]
        public static void ApplyMenu()
        {
            Debug.Log(ApplyEntranceDoorAndTabletWelcome());
        }

        public static string ApplyEntranceDoorAndTabletWelcome()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.EndsWith("PNJunctionLab.unity", StringComparison.OrdinalIgnoreCase))
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            int doorParts = BuildCleanroomEntranceDoor();
            bool tabletSynced = SyncLabTabletStartupSettings();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"EntranceDoorAndWelcome Complete: DoorVisualParts={doorParts}, TabletSynced={tabletSynced}.";
        }

        private static int BuildCleanroomEntranceDoor()
        {
            GameObject walls = GameObject.Find("[LabRoom]/Walls");
            if (walls == null)
            {
                throw new InvalidOperationException("[LabRoom]/Walls not found.");
            }

            Transform doorRoot = walls.transform.Find("EntranceDoorFrame");
            if (doorRoot == null)
            {
                GameObject go = new GameObject("EntranceDoorFrame");
                go.transform.SetParent(walls.transform, false);
                doorRoot = go.transform;
            }

            doorRoot.localPosition = new Vector3(0f, 1.15f, -5.55f);
            doorRoot.localRotation = Quaternion.identity;
            doorRoot.localScale = Vector3.one;

            // Remove legacy single glass-pane renderer from EntranceDoorFrame while keeping solid collider.
            MeshRenderer oldMr = doorRoot.GetComponent<MeshRenderer>();
            if (oldMr != null) Object.DestroyImmediate(oldMr);
            MeshFilter oldMf = doorRoot.GetComponent<MeshFilter>();
            if (oldMf != null) Object.DestroyImmediate(oldMf);

            BoxCollider col = doorRoot.GetComponent<BoxCollider>();
            if (col == null) col = doorRoot.gameObject.AddComponent<BoxCollider>();
            col.isTrigger = false;
            col.center = Vector3.zero;
            col.size = new Vector3(2.60f, 2.30f, 0.14f);

            Transform existingAssembly = doorRoot.Find("EntranceDoorAssembly");
            if (existingAssembly != null)
            {
                Object.DestroyImmediate(existingAssembly.gameObject);
            }

            GameObject assembly = new GameObject("EntranceDoorAssembly");
            assembly.transform.SetParent(doorRoot, false);

            Material wallMat = LoadMat("Mat_LabWall.mat");
            Material trimMat = LoadMat("Mat_LabTrim.mat");
            Material metalMat = LoadMat("Mat_MetalContact.mat") ?? trimMat;
            Material windowMat = LoadMat("Mat_LabWindow.mat");
            Material vestibuleMat = LoadMat("Mat_ScreenPanel.mat") ?? trimMat;
            Material accentMat = LoadMat("Hardware/Mat_HW_AccentCyanGlow.mat") ?? LoadMat("Mat_ButtonGreen.mat");

            int count = 0;

            // 1. Outer heavy architectural frame, transom, and floor threshold (opening is 2.60m x 2.30m).
            count += AddPart("DoorJamb_Left", assembly.transform, new Vector3(-1.26f, 0f, 0f), new Vector3(0.08f, 2.30f, 0.15f), trimMat);
            count += AddPart("DoorJamb_Right", assembly.transform, new Vector3(1.26f, 0f, 0f), new Vector3(0.08f, 2.30f, 0.15f), trimMat);
            count += AddPart("DoorJamb_Top", assembly.transform, new Vector3(0f, 1.11f, 0f), new Vector3(2.60f, 0.08f, 0.15f), trimMat);
            count += AddPart("DoorThreshold", assembly.transform, new Vector3(0f, -1.14f, 0f), new Vector3(2.60f, 0.02f, 0.18f), metalMat);
            count += AddPart("CenterAstragalSeam", assembly.transform, new Vector3(0f, -0.02f, 0.005f), new Vector3(0.022f, 2.18f, 0.075f), trimMat);

            // Sealed exterior vestibule backdrop behind the doors so vision windows never show empty skybox void.
            count += AddPart("AirlockVestibuleBackdrop", assembly.transform, new Vector3(0f, -0.02f, -0.06f), new Vector3(2.46f, 2.18f, 0.02f), vestibuleMat);

            // 2. Left & Right Double Cleanroom Door Leaves (each 1.19m wide x 2.18m tall).
            float[] leafCenters = { -0.615f, 0.615f };
            string[] leafNames = { "LeftDoorLeaf", "RightDoorLeaf" };
            for (int i = 0; i < 2; i++)
            {
                float cx = leafCenters[i];
                float sign = i == 0 ? -1f : 1f;
                GameObject leaf = new GameObject(leafNames[i]);
                leaf.transform.SetParent(assembly.transform, false);
                leaf.transform.localPosition = new Vector3(cx, -0.02f, 0f);

                // Solid lower panel, top rail, left & right stiles surrounding vision glass cutout (0.55m x 0.84m, Y = 0.06..0.90).
                count += AddPart("LowerPanel", leaf.transform, new Vector3(0f, -0.525f, 0f), new Vector3(1.19f, 1.17f, 0.055f), wallMat);
                count += AddPart("TopRail", leaf.transform, new Vector3(0f, 0.995f, 0f), new Vector3(1.19f, 0.19f, 0.055f), wallMat);
                count += AddPart("OuterStile", leaf.transform, new Vector3(sign * 0.435f, 0.48f, 0f), new Vector3(0.32f, 0.84f, 0.055f), wallMat);
                count += AddPart("InnerStile", leaf.transform, new Vector3(-sign * 0.435f, 0.48f, 0f), new Vector3(0.32f, 0.84f, 0.055f), wallMat);

                // Vision window frame bezel & cleanroom glass pane.
                count += AddPart("WindowGlass", leaf.transform, new Vector3(0f, 0.48f, 0f), new Vector3(0.55f, 0.84f, 0.018f), windowMat);
                count += AddPart("WindowTrimTop", leaf.transform, new Vector3(0f, 0.90f, 0f), new Vector3(0.59f, 0.03f, 0.068f), trimMat);
                count += AddPart("WindowTrimBottom", leaf.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.59f, 0.03f, 0.068f), trimMat);
                count += AddPart("WindowTrimL", leaf.transform, new Vector3(-0.28f, 0.48f, 0f), new Vector3(0.03f, 0.87f, 0.068f), trimMat);
                count += AddPart("WindowTrimR", leaf.transform, new Vector3(0.28f, 0.48f, 0f), new Vector3(0.03f, 0.87f, 0.068f), trimMat);

                // Bottom stainless kick plate (interior & exterior trim).
                count += AddPart("KickPlate", leaf.transform, new Vector3(0f, -0.95f, 0.004f), new Vector3(1.14f, 0.24f, 0.060f), trimMat);

                // Interior horizontal push bar + vertical pull handle near center seam.
                count += AddPart("PushBarRail", leaf.transform, new Vector3(0f, -0.14f, 0.052f), new Vector3(0.92f, 0.042f, 0.032f), metalMat);
                count += AddPart("PushBarMountOuter", leaf.transform, new Vector3(sign * 0.42f, -0.14f, 0.036f), new Vector3(0.045f, 0.065f, 0.025f), trimMat);
                count += AddPart("PushBarMountInner", leaf.transform, new Vector3(-sign * 0.42f, -0.14f, 0.036f), new Vector3(0.045f, 0.065f, 0.025f), trimMat);
                count += AddPart("VerticalHandle", leaf.transform, new Vector3(-sign * 0.44f, 0.03f, 0.058f), new Vector3(0.028f, 0.28f, 0.028f), metalMat);
                count += AddPart("HandleBaseTop", leaf.transform, new Vector3(-sign * 0.44f, 0.14f, 0.038f), new Vector3(0.04f, 0.04f, 0.025f), trimMat);
                count += AddPart("HandleBaseBot", leaf.transform, new Vector3(-sign * 0.44f, -0.08f, 0.038f), new Vector3(0.04f, 0.04f, 0.025f), trimMat);
            }

            // Center header badge + airlock status LED bar.
            count += AddPart("CleanroomLockBadge", assembly.transform, new Vector3(0f, 0.96f, 0.042f), new Vector3(0.28f, 0.065f, 0.02f), trimMat);
            count += AddPart("CleanroomStatusLed", assembly.transform, new Vector3(0f, 0.96f, 0.054f), new Vector3(0.18f, 0.016f, 0.008f), accentMat);

            EditorUtility.SetDirty(doorRoot.gameObject);
            return count;
        }

        private static bool SyncLabTabletStartupSettings()
        {
            LabTablet sceneTablet = Object.FindAnyObjectByType<LabTablet>(FindObjectsInactive.Include);
            if (sceneTablet == null)
            {
                return false;
            }

            SerializedObject so = new SerializedObject(sceneTablet);
            LabTextStyleHelper.SetBool(so, "hideOnStart", true);
            LabTextStyleHelper.SetBool(so, "showWelcomeOnStart", true);
            SerializedProperty titleProp = so.FindProperty("startupWelcomeTitle");
            if (titleProp != null) titleProp.stringValue = "Welcome to Semiconductor Lab Simulation";
            SerializedProperty subProp = so.FindProperty("startupWelcomeSubtitle");
            if (subProp != null) subProp.stringValue = "Interactive P-N Junction Physics VR Lab";
            LabTextStyleHelper.SetFloat(so, "startupWelcomeSeconds", 3.5f);
            LabTextStyleHelper.SetFloat(so, "startupObjectivePreviewSeconds", 2.5f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sceneTablet);

            if (PrefabUtility.IsPartOfPrefabInstance(sceneTablet.gameObject))
            {
                PrefabUtility.ApplyPrefabInstance(sceneTablet.gameObject, InteractionMode.AutomatedAction);
            }
            else if (AssetDatabase.LoadAssetAtPath<GameObject>(TabletPrefabPath) != null)
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(sceneTablet.gameObject, TabletPrefabPath, InteractionMode.AutomatedAction);
            }

            return true;
        }

        private static int AddPart(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                Object.DestroyImmediate(c);
            }

            if (mat != null)
            {
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = mat;
                }
            }

            return 1;
        }

        private static Material LoadMat(string relativePath)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/" + relativePath);
        }
    }
}
