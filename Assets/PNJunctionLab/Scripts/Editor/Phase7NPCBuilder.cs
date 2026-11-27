using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Upgrades ONLY the visual character rig inside NPCModelSlot on NPCScientist (Dr. Vera)
    /// into an articulated, stylized 3D Lab Scientist with custom URP/Lit materials and
    /// NPCProceduralAnimator without modifying any stations, physics, or lesson data.
    /// </summary>
    public static class Phase7NPCBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string NpcMaterialsFolder = "Assets/PNJunctionLab/Materials/NPC";

        private sealed class NpcMatSet
        {
            public Material LabCoat;
            public Material ShirtSlate;
            public Material Trousers;
            public Material Shoes;
            public Material Skin;
            public Material Hair;
            public Material GlassesFrame;
            public Material GlassesLens;
            public Material EyeDark;
            public Material EyeWhite;
            public Material BadgeCyan;
            public Material SilverMetal;
            public Material PenBlue;
            public Material PenAmber;
        }

        [MenuItem("PNJunctionLab/Upgrade Dr. Vera 3D Model (NPCModelSlot)")]
        public static void BuildStylizedNpcMenu()
        {
            Debug.Log(BuildStylizedNpc());
        }

        /// <summary>
        /// Programmatic entry point callable via Unity MCP execute_code.
        /// Replaces only PlaceholderRig inside NPCModelSlot while preserving NPCController,
        /// root collider, XRSimpleInteractable, NPCSpeechBubble, and NPCModelSlot drop-in compatibility.
        /// </summary>
        public static string BuildStylizedNpc()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.EndsWith("PNJunctionLab.unity"))
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            EnsureFolders();
            NpcMatSet mats = CreateNpcMaterials();

            NPCController npcCtrl = Object.FindAnyObjectByType<NPCController>();
            if (npcCtrl == null)
            {
                return "Error: No NPCController found in scene.";
            }

            Transform modelSlot = npcCtrl.NpcModelSlot != null
                ? npcCtrl.NpcModelSlot
                : npcCtrl.transform.Find("NPCModelSlot");
            if (modelSlot == null)
            {
                return "Error: NPCModelSlot missing on NPCScientist.";
            }

            // Clear only old placeholder children inside NPCModelSlot cleanly.
            for (int i = modelSlot.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(modelSlot.GetChild(i).gameObject);
            }

            modelSlot.localPosition = Vector3.zero;
            modelSlot.localRotation = Quaternion.identity;
            modelSlot.localScale = Vector3.one;

            GameObject rig = new GameObject("DrVeraStylizedRig");
            rig.transform.SetParent(modelSlot, false);
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;

            List<Renderer> highlightRenderers = new List<Renderer>(16);

            // -----------------------------------------------------------------
            // 1. LOWER BODY (Trousers & Lab Shoes grounded cleanly at Y = 0.00)
            // -----------------------------------------------------------------
            Transform leftLegPivot = CreatePivot("LeftLegPivot", rig.transform, new Vector3(-0.095f, 0.76f, 0f));
            BuildLegAssembly(leftLegPivot, "L", mats);

            Transform rightLegPivot = CreatePivot("RightLegPivot", rig.transform, new Vector3(0.095f, 0.76f, 0f));
            BuildLegAssembly(rightLegPivot, "R", mats);

            // -----------------------------------------------------------------
            // 2. UPPER TORSO / CHEST PIVOT (Pelvis, Dark Shirt, Tailored Lab Coat, Pens & Badge)
            // -----------------------------------------------------------------
            Transform chestPivot = CreatePivot("ChestPivot", rig.transform, new Vector3(0f, 0.76f, 0f));

            // Pelvis / Trouser waistband.
            CreatePart("Pelvis", PrimitiveType.Cylinder, chestPivot,
                new Vector3(0f, 0.035f, 0f), Vector3.zero, new Vector3(0.285f, 0.09f, 0.195f), mats.Trousers);

            // Inner dark navy/slate dress shirt visible through front coat opening.
            CreatePart("InnerShirtTorso", PrimitiveType.Cube, chestPivot,
                new Vector3(0f, 0.305f, 0.015f), Vector3.zero, new Vector3(0.275f, 0.42f, 0.165f), mats.ShirtSlate);
            CreatePart("ShirtCollarRing", PrimitiveType.Cylinder, chestPivot,
                new Vector3(0f, 0.53f, 0.012f), Vector3.zero, new Vector3(0.125f, 0.025f, 0.125f), mats.ShirtSlate);

            // Tailored White Lab Coat (Back, Open Left & Right Panels, Waist Coat-Tails, Lapels).
            highlightRenderers.Add(CreatePart("CoatBackShell", PrimitiveType.Cube, chestPivot,
                new Vector3(0f, 0.305f, -0.042f), Vector3.zero, new Vector3(0.355f, 0.435f, 0.125f), mats.LabCoat).GetComponent<Renderer>());
            highlightRenderers.Add(CreatePart("CoatShoulderYoke", PrimitiveType.Capsule, chestPivot,
                new Vector3(0f, 0.478f, -0.005f), new Vector3(0f, 0f, 90f), new Vector3(0.180f, 0.215f, 0.195f), mats.LabCoat).GetComponent<Renderer>());
            highlightRenderers.Add(CreatePart("CoatFrontPanelL", PrimitiveType.Cube, chestPivot,
                new Vector3(-0.122f, 0.295f, 0.038f), Vector3.zero, new Vector3(0.125f, 0.42f, 0.125f), mats.LabCoat).GetComponent<Renderer>());
            highlightRenderers.Add(CreatePart("CoatFrontPanelR", PrimitiveType.Cube, chestPivot,
                new Vector3(0.122f, 0.295f, 0.038f), Vector3.zero, new Vector3(0.125f, 0.42f, 0.125f), mats.LabCoat).GetComponent<Renderer>());

            // Crisp Collar & Front Lapels framing shirt V neatly flush.
            CreatePart("CoatLapelL", PrimitiveType.Cube, chestPivot,
                new Vector3(-0.088f, 0.430f, 0.098f), new Vector3(0f, -8f, 15f), new Vector3(0.052f, 0.185f, 0.015f), mats.LabCoat);
            CreatePart("CoatLapelR", PrimitiveType.Cube, chestPivot,
                new Vector3(0.088f, 0.430f, 0.098f), new Vector3(0f, 8f, -15f), new Vector3(0.052f, 0.185f, 0.015f), mats.LabCoat);
            CreatePart("CoatNeckCollarBack", PrimitiveType.Cylinder, chestPivot,
                new Vector3(0f, 0.542f, -0.025f), Vector3.zero, new Vector3(0.155f, 0.028f, 0.135f), mats.LabCoat);

            // Waist Coat-Tails (open skirt hem reaching mid-thigh neatly flush).
            highlightRenderers.Add(CreatePart("CoatTailBack", PrimitiveType.Cube, chestPivot,
                new Vector3(0f, -0.025f, -0.048f), new Vector3(-3f, 0f, 0f), new Vector3(0.365f, 0.26f, 0.120f), mats.LabCoat).GetComponent<Renderer>());
            highlightRenderers.Add(CreatePart("CoatTailLeft", PrimitiveType.Cube, chestPivot,
                new Vector3(-0.125f, -0.025f, 0.024f), new Vector3(0f, 0f, -2f), new Vector3(0.128f, 0.255f, 0.138f), mats.LabCoat).GetComponent<Renderer>());
            highlightRenderers.Add(CreatePart("CoatTailRight", PrimitiveType.Cube, chestPivot,
                new Vector3(0.125f, -0.025f, 0.024f), new Vector3(0f, 0f, 2f), new Vector3(0.128f, 0.255f, 0.138f), mats.LabCoat).GetComponent<Renderer>());

            // Chest Pocket + Two Colored Lab Pens on Left Breast (-X local).
            CreatePart("ChestPocket", PrimitiveType.Cube, chestPivot,
                new Vector3(-0.112f, 0.375f, 0.106f), Vector3.zero, new Vector3(0.068f, 0.072f, 0.014f), mats.LabCoat);
            CreatePart("PocketTrim", PrimitiveType.Cube, chestPivot,
                new Vector3(-0.112f, 0.408f, 0.109f), Vector3.zero, new Vector3(0.068f, 0.008f, 0.015f), mats.ShirtSlate);
            CreatePart("PenBlue", PrimitiveType.Cylinder, chestPivot,
                new Vector3(-0.124f, 0.422f, 0.108f), new Vector3(0f, 0f, 6f), new Vector3(0.010f, 0.036f, 0.010f), mats.PenBlue);
            CreatePart("PenAmber", PrimitiveType.Cylinder, chestPivot,
                new Vector3(-0.098f, 0.420f, 0.108f), new Vector3(0f, 0f, -4f), new Vector3(0.010f, 0.034f, 0.010f), mats.PenAmber);

            // Metallic & Cyan Lab ID Badge on Right Breast (+X local) + Lapel Mic.
            CreatePart("BadgeClip", PrimitiveType.Cube, chestPivot,
                new Vector3(0.115f, 0.415f, 0.109f), Vector3.zero, new Vector3(0.022f, 0.018f, 0.012f), mats.SilverMetal);
            CreatePart("IDBadgeCard", PrimitiveType.Cube, chestPivot,
                new Vector3(0.115f, 0.375f, 0.108f), Vector3.zero, new Vector3(0.064f, 0.058f, 0.009f), mats.LabCoat);
            Renderer badgeAccent = CreatePart("IDBadgeCyanStrip", PrimitiveType.Cube, chestPivot,
                new Vector3(0.115f, 0.388f, 0.113f), Vector3.zero, new Vector3(0.058f, 0.018f, 0.004f), mats.BadgeCyan).GetComponent<Renderer>();
            highlightRenderers.Add(badgeAccent);
            CreatePart("LapelMicClip", PrimitiveType.Sphere, chestPivot,
                new Vector3(0.072f, 0.465f, 0.115f), Vector3.zero, new Vector3(0.018f, 0.022f, 0.014f), mats.GlassesFrame);

            // -----------------------------------------------------------------
            // 3. NECK, HEAD PIVOT, HAIR, FACE & GLASSES
            // -----------------------------------------------------------------
            CreatePart("Neck", PrimitiveType.Cylinder, chestPivot,
                new Vector3(0f, 0.585f, 0.008f), Vector3.zero, new Vector3(0.090f, 0.075f, 0.090f), mats.Skin);

            Transform headPivot = CreatePivot("HeadPivot", chestPivot, new Vector3(0f, 0.645f, 0.010f));

            // Sculpted Head & Jaw + Ears + Nose.
            highlightRenderers.Add(CreatePart("HeadSkull", PrimitiveType.Sphere, headPivot,
                new Vector3(0f, 0.145f, 0.012f), Vector3.zero, new Vector3(0.220f, 0.258f, 0.238f), mats.Skin).GetComponent<Renderer>());
            CreatePart("ChinJaw", PrimitiveType.Sphere, headPivot,
                new Vector3(0f, 0.065f, 0.028f), Vector3.zero, new Vector3(0.165f, 0.130f, 0.185f), mats.Skin);
            CreatePart("Nose", PrimitiveType.Sphere, headPivot,
                new Vector3(0f, 0.135f, 0.130f), new Vector3(12f, 0f, 0f), new Vector3(0.030f, 0.044f, 0.036f), mats.Skin);
            CreatePart("EarL", PrimitiveType.Sphere, headPivot,
                new Vector3(-0.106f, 0.142f, 0.005f), Vector3.zero, new Vector3(0.028f, 0.052f, 0.036f), mats.Skin);
            CreatePart("EarR", PrimitiveType.Sphere, headPivot,
                new Vector3(0.106f, 0.142f, 0.005f), Vector3.zero, new Vector3(0.028f, 0.052f, 0.036f), mats.Skin);

            // Expressive Eyes, Eyebrows & Subtle Warm Smile.
            CreatePart("EyeWhiteL", PrimitiveType.Sphere, headPivot,
                new Vector3(-0.044f, 0.162f, 0.114f), Vector3.zero, new Vector3(0.034f, 0.026f, 0.018f), mats.EyeWhite);
            CreatePart("EyeWhiteR", PrimitiveType.Sphere, headPivot,
                new Vector3(0.044f, 0.162f, 0.114f), Vector3.zero, new Vector3(0.034f, 0.026f, 0.018f), mats.EyeWhite);
            CreatePart("EyeIrisL", PrimitiveType.Sphere, headPivot,
                new Vector3(-0.044f, 0.162f, 0.121f), Vector3.zero, new Vector3(0.020f, 0.020f, 0.012f), mats.EyeDark);
            CreatePart("EyeIrisR", PrimitiveType.Sphere, headPivot,
                new Vector3(0.044f, 0.162f, 0.121f), Vector3.zero, new Vector3(0.020f, 0.020f, 0.012f), mats.EyeDark);
            CreatePart("EyebrowL", PrimitiveType.Cube, headPivot,
                new Vector3(-0.045f, 0.198f, 0.122f), new Vector3(0f, 0f, 6f), new Vector3(0.042f, 0.009f, 0.012f), mats.Hair);
            CreatePart("EyebrowR", PrimitiveType.Cube, headPivot,
                new Vector3(0.045f, 0.198f, 0.122f), new Vector3(0f, 0f, -6f), new Vector3(0.042f, 0.009f, 0.012f), mats.Hair);
            CreatePart("SmileCenter", PrimitiveType.Capsule, headPivot,
                new Vector3(0f, 0.088f, 0.122f), new Vector3(0f, 0f, 90f), new Vector3(0.010f, 0.024f, 0.008f), mats.Hair);

            // Styled Dark Hair (Top dome, sleek swept bangs, side locks, scientist chignon bun at back).
            CreatePart("HairCrown", PrimitiveType.Sphere, headPivot,
                new Vector3(0f, 0.205f, -0.012f), Vector3.zero, new Vector3(0.236f, 0.192f, 0.252f), mats.Hair);
            CreatePart("HairBackSweep", PrimitiveType.Sphere, headPivot,
                new Vector3(0f, 0.155f, -0.055f), Vector3.zero, new Vector3(0.228f, 0.210f, 0.205f), mats.Hair);
            CreatePart("HairBangsWave", PrimitiveType.Capsule, headPivot,
                new Vector3(0.026f, 0.228f, 0.058f), new Vector3(14f, 16f, 80f), new Vector3(0.072f, 0.100f, 0.058f), mats.Hair);
            CreatePart("HairSideLockL", PrimitiveType.Capsule, headPivot,
                new Vector3(-0.102f, 0.168f, -0.008f), new Vector3(-8f, 0f, -8f), new Vector3(0.052f, 0.072f, 0.072f), mats.Hair);
            CreatePart("HairSideLockR", PrimitiveType.Capsule, headPivot,
                new Vector3(0.102f, 0.168f, -0.008f), new Vector3(-8f, 0f, 8f), new Vector3(0.052f, 0.072f, 0.072f), mats.Hair);
            CreatePart("HairBunChignon", PrimitiveType.Sphere, headPivot,
                new Vector3(0f, 0.165f, -0.145f), Vector3.zero, new Vector3(0.115f, 0.108f, 0.105f), mats.Hair);

            // Dark-Rimmed Glasses with Translucent Lenses & Sleek Bridge/Temples.
            CreatePart("GlassesBridge", PrimitiveType.Cube, headPivot,
                new Vector3(0f, 0.168f, 0.136f), Vector3.zero, new Vector3(0.028f, 0.007f, 0.008f), mats.GlassesFrame);
            BuildGlassesEyeFrame(headPivot, -0.046f, "L", mats);
            BuildGlassesEyeFrame(headPivot, 0.046f, "R", mats);
            CreatePart("GlassesTempleL", PrimitiveType.Cube, headPivot,
                new Vector3(-0.102f, 0.168f, 0.068f), new Vector3(0f, -10f, 0f), new Vector3(0.008f, 0.008f, 0.132f), mats.GlassesFrame);
            CreatePart("GlassesTempleR", PrimitiveType.Cube, headPivot,
                new Vector3(0.102f, 0.168f, 0.068f), new Vector3(0f, 10f, 0f), new Vector3(0.008f, 0.008f, 0.132f), mats.GlassesFrame);

            // -----------------------------------------------------------------
            // 4. ARTICULATED ARMS & HANDS (Shoulders, Elbows, Coat Cuffs, Hands)
            // -----------------------------------------------------------------
            BuildArmAssembly(chestPivot, -1f, "Left", mats, highlightRenderers,
                out Transform leftShoulderPivot, out Transform leftElbowPivot, out _);
            BuildArmAssembly(chestPivot, 1f, "Right", mats, highlightRenderers,
                out Transform rightShoulderPivot, out Transform rightElbowPivot, out Transform rightPointRef);

            // -----------------------------------------------------------------
            // 5. WIRE NPCProceduralAnimator & PRESERVE NPCController + InteractionFeedback
            // -----------------------------------------------------------------
            DialogueSystem dlgSys = Object.FindAnyObjectByType<DialogueSystem>();
            NavMeshAgent agent = npcCtrl.GetComponent<NavMeshAgent>();
            Animator existingExternalAnim = null; // No external FBX Animator on procedural rig.

            NPCProceduralAnimator procAnim = rig.AddComponent<NPCProceduralAnimator>();
            SerializedObject paSo = new SerializedObject(procAnim);
            LabTextStyleHelper.SetObjRef(paSo, "npcController", npcCtrl);
            LabTextStyleHelper.SetObjRef(paSo, "dialogueSystem", dlgSys);
            LabTextStyleHelper.SetObjRef(paSo, "navMeshAgent", agent);
            LabTextStyleHelper.SetObjRef(paSo, "optionalExternalAnimator", existingExternalAnim);
            LabTextStyleHelper.SetObjRef(paSo, "chestPivot", chestPivot);
            LabTextStyleHelper.SetObjRef(paSo, "headPivot", headPivot);
            LabTextStyleHelper.SetObjRef(paSo, "leftShoulderPivot", leftShoulderPivot);
            LabTextStyleHelper.SetObjRef(paSo, "leftElbowPivot", leftElbowPivot);
            LabTextStyleHelper.SetObjRef(paSo, "rightShoulderPivot", rightShoulderPivot);
            LabTextStyleHelper.SetObjRef(paSo, "rightElbowPivot", rightElbowPivot);
            LabTextStyleHelper.SetObjRef(paSo, "leftLegPivot", leftLegPivot);
            LabTextStyleHelper.SetObjRef(paSo, "rightLegPivot", rightLegPivot);
            paSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire NPCController without altering collider, XRSimpleInteractable, or NPCSpeechBubble.
            SerializedObject npcSo = new SerializedObject(npcCtrl);
            LabTextStyleHelper.SetObjRef(npcSo, "proceduralAnimator", procAnim);
            LabTextStyleHelper.SetObjRef(npcSo, "placeholderRig", rig);
            LabTextStyleHelper.SetObjRef(npcSo, "pointerArmPivot", rightShoulderPivot);
            npcSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(npcCtrl);

            // Keep hover highlight renderers crisp on NPCInteractionTarget.
            InteractionFeedback npcFeedback = npcCtrl.GetComponentInChildren<InteractionFeedback>(true);
            if (npcFeedback != null)
            {
                SerializedObject fbSo = new SerializedObject(npcFeedback);
                LabTextStyleHelper.SetObjectArray(fbSo, "highlightRenderers", highlightRenderers.ToArray());
                fbSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(npcFeedback);
            }

            if (PrefabUtility.IsPartOfPrefabInstance(npcCtrl.gameObject))
            {
                PrefabUtility.ApplyPrefabInstance(npcCtrl.gameObject, InteractionMode.AutomatedAction);
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"Dr. Vera stylized 3D Lab Scientist built cleanly inside NPCModelSlot with {highlightRenderers.Count} highlight renderers and full NPCProceduralAnimator rig.";
        }

        private static void BuildLegAssembly(Transform legPivot, string sideTag, NpcMatSet mats)
        {
            // Leg length 0.76 m downward to Y = 0.00.
            CreatePart("Thigh_" + sideTag, PrimitiveType.Cylinder, legPivot,
                new Vector3(0f, -0.18f, 0f), Vector3.zero, new Vector3(0.125f, 0.185f, 0.125f), mats.Trousers);
            CreatePart("Knee_" + sideTag, PrimitiveType.Sphere, legPivot,
                new Vector3(0f, -0.365f, 0.005f), Vector3.zero, new Vector3(0.112f, 0.112f, 0.112f), mats.Trousers);
            CreatePart("Shin_" + sideTag, PrimitiveType.Cylinder, legPivot,
                new Vector3(0f, -0.535f, 0f), Vector3.zero, new Vector3(0.102f, 0.175f, 0.102f), mats.Trousers);

            // Clean Lab Shoe bottom strictly at -0.760 relative to Hip Y = 0.76 -> grounded at Y = 0.000!
            CreatePart("ShoeUpper_" + sideTag, PrimitiveType.Capsule, legPivot,
                new Vector3(0f, -0.708f, 0.042f), new Vector3(90f, 0f, 0f), new Vector3(0.104f, 0.115f, 0.072f), mats.Shoes);
            CreatePart("ShoeSole_" + sideTag, PrimitiveType.Cube, legPivot,
                new Vector3(0f, -0.745f, 0.042f), Vector3.zero, new Vector3(0.100f, 0.030f, 0.210f), mats.Shoes);
        }

        private static void BuildArmAssembly(
            Transform chestPivot,
            float sideSign, // -1 = Left (-X), +1 = Right (+X)
            string sideName,
            NpcMatSet mats,
            List<Renderer> highlightRenderers,
            out Transform shoulderPivot,
            out Transform elbowPivot,
            out Transform handOut)
        {
            shoulderPivot = CreatePivot(sideName + "ShoulderPivot", chestPivot, new Vector3(sideSign * 0.202f, 0.468f, 0f));

            highlightRenderers.Add(CreatePart(sideName + "ShoulderCap", PrimitiveType.Sphere, shoulderPivot,
                Vector3.zero, Vector3.zero, new Vector3(0.096f, 0.104f, 0.096f), mats.LabCoat).GetComponent<Renderer>());
            highlightRenderers.Add(CreatePart(sideName + "UpperArmSleeve", PrimitiveType.Cylinder, shoulderPivot,
                new Vector3(0f, -0.135f, 0f), Vector3.zero, new Vector3(0.088f, 0.135f, 0.088f), mats.LabCoat).GetComponent<Renderer>());

            elbowPivot = CreatePivot(sideName + "ElbowPivot", shoulderPivot, new Vector3(0f, -0.265f, 0f));
            highlightRenderers.Add(CreatePart(sideName + "ElbowJoint", PrimitiveType.Sphere, elbowPivot,
                Vector3.zero, Vector3.zero, new Vector3(0.084f, 0.084f, 0.084f), mats.LabCoat).GetComponent<Renderer>());
            highlightRenderers.Add(CreatePart(sideName + "ForearmSleeve", PrimitiveType.Cylinder, elbowPivot,
                new Vector3(0f, -0.105f, 0f), Vector3.zero, new Vector3(0.078f, 0.105f, 0.078f), mats.LabCoat).GetComponent<Renderer>());

            CreatePart(sideName + "CoatCuff", PrimitiveType.Cylinder, elbowPivot,
                new Vector3(0f, -0.205f, 0f), Vector3.zero, new Vector3(0.086f, 0.018f, 0.086f), mats.LabCoat);
            CreatePart(sideName + "Wrist", PrimitiveType.Cylinder, elbowPivot,
                new Vector3(0f, -0.228f, 0f), Vector3.zero, new Vector3(0.052f, 0.022f, 0.044f), mats.Skin);

            GameObject palm = CreatePart(sideName + "HandPalm", PrimitiveType.Cube, elbowPivot,
                new Vector3(0f, -0.262f, 0.006f), Vector3.zero, new Vector3(0.046f, 0.064f, 0.028f), mats.Skin);
            CreatePart(sideName + "Fingers", PrimitiveType.Capsule, elbowPivot,
                new Vector3(0f, -0.305f, 0.008f), Vector3.zero, new Vector3(0.040f, 0.028f, 0.024f), mats.Skin);
            CreatePart(sideName + "Thumb", PrimitiveType.Capsule, elbowPivot,
                new Vector3(-sideSign * 0.028f, -0.266f, 0.018f), new Vector3(22f, 0f, sideSign * 26f), new Vector3(0.018f, 0.024f, 0.018f), mats.Skin);

            handOut = palm.transform;
        }

        private static void BuildGlassesEyeFrame(Transform headPivot, float xPos, string sideTag, NpcMatSet mats)
        {
            const float zFront = 0.136f;
            const float yEye = 0.166f;
            CreatePart("GlassesLens_" + sideTag, PrimitiveType.Cube, headPivot,
                new Vector3(xPos, yEye, zFront), Vector3.zero, new Vector3(0.056f, 0.036f, 0.004f), mats.GlassesLens);
            CreatePart("FrameTop_" + sideTag, PrimitiveType.Cube, headPivot,
                new Vector3(xPos, yEye + 0.020f, zFront + 0.001f), Vector3.zero, new Vector3(0.064f, 0.007f, 0.009f), mats.GlassesFrame);
            CreatePart("FrameBot_" + sideTag, PrimitiveType.Cube, headPivot,
                new Vector3(xPos, yEye - 0.020f, zFront + 0.001f), Vector3.zero, new Vector3(0.062f, 0.006f, 0.008f), mats.GlassesFrame);
            CreatePart("FrameOuter_" + sideTag, PrimitiveType.Cube, headPivot,
                new Vector3(xPos + (xPos < 0 ? -0.029f : 0.029f), yEye, zFront + 0.001f), Vector3.zero, new Vector3(0.006f, 0.044f, 0.008f), mats.GlassesFrame);
            CreatePart("FrameInner_" + sideTag, PrimitiveType.Cube, headPivot,
                new Vector3(xPos + (xPos < 0 ? 0.029f : -0.029f), yEye, zFront + 0.001f), Vector3.zero, new Vector3(0.006f, 0.044f, 0.008f), mats.GlassesFrame);
        }

        private static Transform CreatePivot(string name, Transform parent, Vector3 localPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        private static GameObject CreatePart(
            string name,
            PrimitiveType primitive,
            Transform parent,
            Vector3 localPos,
            Vector3 localEuler,
            Vector3 localScale,
            Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }

            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null)
            {
                r.sharedMaterial = mat;
            }

            return go;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/PNJunctionLab/Materials"))
            {
                AssetDatabase.CreateFolder("Assets/PNJunctionLab", "Materials");
            }
            if (!AssetDatabase.IsValidFolder(NpcMaterialsFolder))
            {
                AssetDatabase.CreateFolder("Assets/PNJunctionLab/Materials", "NPC");
            }
        }

        private static NpcMatSet CreateNpcMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new NpcMatSet
            {
                LabCoat = MakeMat("Mat_NPC_LabCoat", urpLit, new Color(0.95f, 0.96f, 0.98f), 0.0f, 0.28f),
                ShirtSlate = MakeMat("Mat_NPC_ShirtSlate", urpLit, new Color(0.14f, 0.21f, 0.34f), 0.0f, 0.32f),
                Trousers = MakeMat("Mat_NPC_Trousers", urpLit, new Color(0.10f, 0.12f, 0.17f), 0.0f, 0.22f),
                Shoes = MakeMat("Mat_NPC_Shoes", urpLit, new Color(0.06f, 0.07f, 0.09f), 0.10f, 0.55f),
                Skin = MakeMat("Mat_NPC_Skin", urpLit, new Color(0.90f, 0.72f, 0.60f), 0.0f, 0.35f),
                Hair = MakeMat("Mat_NPC_Hair", urpLit, new Color(0.11f, 0.08f, 0.07f), 0.05f, 0.42f),
                GlassesFrame = MakeMat("Mat_NPC_GlassesFrame", urpLit, new Color(0.07f, 0.08f, 0.11f), 0.65f, 0.72f),
                GlassesLens = MakeTransparentMat("Mat_NPC_GlassesLens", urpLit, new Color(0.74f, 0.90f, 0.98f, 0.36f)),
                EyeDark = MakeMat("Mat_NPC_EyeDark", urpLit, new Color(0.05f, 0.05f, 0.07f), 0.0f, 0.78f),
                EyeWhite = MakeMat("Mat_NPC_EyeWhite", urpLit, new Color(0.98f, 0.98f, 0.99f), 0.0f, 0.50f),
                BadgeCyan = MakeMat("Mat_NPC_BadgeCyan", urpLit, new Color(0.18f, 0.84f, 0.98f), 0.55f, 0.80f, true, new Color(0.06f, 0.28f, 0.36f)),
                SilverMetal = MakeMat("Mat_NPC_SilverMetal", urpLit, new Color(0.80f, 0.83f, 0.87f), 0.85f, 0.78f),
                PenBlue = MakeMat("Mat_NPC_PenBlue", urpLit, new Color(0.16f, 0.48f, 0.92f), 0.40f, 0.70f),
                PenAmber = MakeMat("Mat_NPC_PenAmber", urpLit, new Color(0.94f, 0.62f, 0.14f), 0.40f, 0.70f)
            };
        }

        private static Material MakeMat(string name, Shader shader, Color baseColor, float metallic, float smoothness,
            bool emission = false, Color emissionColor = default)
        {
            string path = NpcMaterialsFolder + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (emission && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material MakeTransparentMat(string name, Shader shader, Color baseColor)
        {
            string path = NpcMaterialsFolder + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.90f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
