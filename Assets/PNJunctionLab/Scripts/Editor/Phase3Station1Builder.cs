using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Builds Phase 3 (Station 1: Intrinsic Silicon) self-contained prefab, materials, ScriptableObject dialogue
    /// and lesson steps, and wires Station 1 cleanly into PNJunctionLab.unity and LessonManager.
    /// </summary>
    public static class Phase3Station1Builder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string PrefabsFolder = "Assets/PNJunctionLab/Prefabs";
        private const string MaterialsFolder = "Assets/PNJunctionLab/Materials";
        private const string ModelsFolder = "Assets/PNJunctionLab/Models";
        private const string DataRoot = "Assets/PNJunctionLab/Data";
        private const string DialogueFolder = DataRoot + "/Dialogue";
        private const string LessonFolder = DataRoot + "/Lessons";
        private const string Station1PrefabPath = PrefabsFolder + "/IntrinsicSiliconStation.prefab";

        private struct Station1MatSet
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
            public Material LensGlass;
            public Material ChamberGlass;
            public Material MicroscopeMetal;
            public Material MicroscopeVoid;
            public Material MicroscopeBackdrop;
            public Material MicroscopeLensFeed;
            public RenderTexture MicroscopeRT;
            public Material AtomCore;
            public Material BondIntact;
            public Material BondBroken;
            public Material ElectronBlue;
            public Material HoleRed;
        }

        [MenuItem("PNJunctionLab/Build Phase 3 Station 1 (Intrinsic Silicon)")]
        public static void BuildPhase3Menu()
        {
            BuildPhase3();
        }

        public static string BuildPhase3()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                return "Failed to open scene at " + ScenePath;
            }

            LabTextStyleHelper.EnsureNativeGameViewDPI();
            Station1MatSet mats = EnsureMicroscopeRenderFeed(CreateOrLoadMaterials());
            Camera mainCam = FindMainCamera();

            // Replace placeholder Station_1_Intrinsic cleanly while preserving exact world pose.
            GameObject stationsRoot = GameObject.Find("[Stations]");
            if (stationsRoot == null)
            {
                stationsRoot = new GameObject("[Stations]");
            }

            Vector3 worldPos = new Vector3(-5.6f, 0f, -0.2f);
            Quaternion worldRot = Quaternion.Euler(0f, 90f, 0f);
            GameObject existingSt1 = GameObject.Find("Station_1_Intrinsic");
            if (existingSt1 != null)
            {
                worldPos = existingSt1.transform.position;
                worldRot = existingSt1.transform.rotation;
                Object.DestroyImmediate(existingSt1);
            }

            GameObject station1Go = BuildStation1PrefabAndInstance(mats, mainCam);
            station1Go.transform.SetParent(stationsRoot.transform, false);
            station1Go.transform.SetPositionAndRotation(worldPos, worldRot);

            // Re-wire LessonManager steps & Station 1 DialogueLine ScriptableObjects.
            WireStation1LessonsAndScene(station1Go.GetComponent<IntrinsicSiliconStationController>());

            // Refresh RespawnZone list if Tutorial Pad only manages its own or keep Station 1's RespawnZone self-contained.
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return saved
                ? "Phase 3 (Station 1: Intrinsic Silicon) built and wired cleanly."
                : "Failed to save scene after Phase 3 build.";
        }

        private static Station1MatSet CreateOrLoadMaterials()
        {
            return new Station1MatSet
            {
                TableTop = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_TableTop.mat"),
                TableFrame = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_TableFrame.mat"),
                ScreenPanel = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_ScreenPanel.mat"),
                AccentBase = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_AccentBase.mat"),
                ButtonGreen = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_ButtonGreen.mat"),
                ButtonAmber = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_ButtonAmber.mat"),
                ButtonBlue = CreateLitMat(MaterialsFolder + "/Mat_ButtonBlue.mat", new Color(0.16f, 0.58f, 0.92f, 1f), 0.1f, 0.45f, new Color(0.03f, 0.16f, 0.28f), false),
                ButtonRed = CreateLitMat(MaterialsFolder + "/Mat_ButtonRed.mat", new Color(0.86f, 0.22f, 0.24f, 1f), 0.05f, 0.42f, new Color(0.24f, 0.04f, 0.04f), false),
                WaferSi = CreateLitMat(MaterialsFolder + "/Mat_WaferSi.mat", new Color(0.24f, 0.30f, 0.39f, 1f), 0.65f, 0.72f, new Color(0.02f, 0.05f, 0.08f), false),
                LensGlass = CreateLitMat(MaterialsFolder + "/Mat_LensGlass.mat", new Color(0.32f, 0.85f, 1.0f, 0.42f), 0.0f, 0.88f, new Color(0.06f, 0.24f, 0.34f), true),
                ChamberGlass = CreateLitMat(MaterialsFolder + "/Mat_ChamberGlass.mat", new Color(0.55f, 0.85f, 1.0f, 0.14f), 0.0f, 0.92f, new Color(0.02f, 0.08f, 0.14f), true),
                MicroscopeMetal = CreateLitMat(MaterialsFolder + "/Mat_MicroscopeMetal.mat", new Color(0.76f, 0.80f, 0.86f, 1f), 0.78f, 0.66f, new Color(0.02f, 0.03f, 0.04f), false),
                MicroscopeVoid = CreateUnlitMat(MaterialsFolder + "/Mat_MicroscopeVoid.mat", new Color(0.012f, 0.015f, 0.020f, 1f)),
                MicroscopeBackdrop = CreateUnlitMat(MaterialsFolder + "/Mat_MicroscopeBackdrop.mat", new Color(0.025f, 0.065f, 0.14f, 1f)),
                AtomCore = CreateLitMat(MaterialsFolder + "/Mat_LatticeAtom.mat", new Color(0.84f, 0.90f, 0.96f, 1f), 0.25f, 0.60f, new Color(0.18f, 0.25f, 0.34f), false),
                BondIntact = CreateLitMat(MaterialsFolder + "/Mat_BondIntact.mat", new Color(0.28f, 0.76f, 0.92f, 0.82f), 0.10f, 0.45f, new Color(0.06f, 0.28f, 0.38f), true),
                BondBroken = CreateLitMat(MaterialsFolder + "/Mat_BondBroken.mat", new Color(0.95f, 0.45f, 0.18f, 0.55f), 0.0f, 0.30f, new Color(0.45f, 0.14f, 0.04f), true),
                ElectronBlue = CreateLitMat(MaterialsFolder + "/Mat_ElectronBlue.mat", new Color(0.18f, 0.62f, 1.0f, 1f), 0.1f, 0.6f, new Color(0.18f, 0.50f, 0.98f), false),
                HoleRed = CreateLitMat(MaterialsFolder + "/Mat_HoleRed.mat", new Color(1.0f, 0.30f, 0.34f, 1f), 0.1f, 0.6f, new Color(0.75f, 0.14f, 0.18f), false)
            };
        }

        private static Station1MatSet EnsureMicroscopeRenderFeed(Station1MatSet m)
        {
            string rtPath = MaterialsFolder + "/RT_MicroscopeView.renderTexture";
            RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
            if (rt == null)
            {
                rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32)
                {
                    name = "RT_MicroscopeView",
                    antiAliasing = 4,
                    filterMode = FilterMode.Bilinear
                };
                AssetDatabase.CreateAsset(rt, rtPath);
            }
            EditorUtility.SetDirty(rt);
            m.MicroscopeRT = rt;

            string matPath = MaterialsFolder + "/Mat_MicroscopeLensFeed.mat";
            Material feedMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            if (feedMat == null)
            {
                feedMat = new Material(unlit);
                AssetDatabase.CreateAsset(feedMat, matPath);
            }
            else if (unlit != null && feedMat.shader != unlit)
            {
                feedMat.shader = unlit;
            }
            feedMat.SetColor("_BaseColor", Color.white);
            feedMat.SetColor("_Color", Color.white);
            feedMat.SetTexture("_BaseMap", rt);
            feedMat.SetTexture("_MainTex", rt);
            EditorUtility.SetDirty(feedMat);
            m.MicroscopeLensFeed = feedMat;
            return m;
        }

        private static Mesh CreateOrLoadCircleMesh(string assetPath, float radius, int segments)
        {
            if (!AssetDatabase.IsValidFolder(ModelsFolder))
            {
                AssetDatabase.CreateFolder("Assets/PNJunctionLab", "Models");
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, assetPath);
            }

            mesh.Clear();
            Vector3[] verts = new Vector3[segments + 1];
            Vector3[] norms = new Vector3[segments + 1];
            Vector2[] uvs = new Vector2[segments + 1];
            int[] tris = new int[segments * 3];

            verts[0] = Vector3.zero;
            norms[0] = Vector3.back;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);
                verts[i + 1] = new Vector3(c * radius, s * radius, 0f);
                norms[i + 1] = Vector3.back;
                uvs[i + 1] = new Vector2(0.5f + 0.5f * c, 0.5f + 0.5f * s);

                int next = ((i + 1) % segments) + 1;
                int t = i * 3;
                // Winding faces -Z (toward the viewer).
                tris[t] = 0;
                tris[t + 1] = next;
                tris[t + 2] = i + 1;
            }

            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material CreateUnlitMat(string path, Color color)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (mat == null)
            {
                mat = new Material(unlit);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (unlit != null && mat.shader != unlit)
            {
                mat.shader = unlit;
            }

            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Mesh CreateOrLoadAnnulusMesh(string assetPath, float innerRadius, float outerRadius, int segments)
        {
            if (!AssetDatabase.IsValidFolder(ModelsFolder))
            {
                AssetDatabase.CreateFolder("Assets/PNJunctionLab", "Models");
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, assetPath);
            }

            mesh.Clear();
            int ringVerts = segments * 2;
            Vector3[] verts = new Vector3[ringVerts];
            Vector3[] norms = new Vector3[ringVerts];
            Vector2[] uvs = new Vector2[ringVerts];
            int[] tris = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);
                int iVert = i * 2;
                verts[iVert] = new Vector3(c * innerRadius, s * innerRadius, 0f);
                verts[iVert + 1] = new Vector3(c * outerRadius, s * outerRadius, 0f);
                norms[iVert] = Vector3.back;
                norms[iVert + 1] = Vector3.back;
                uvs[iVert] = new Vector2(0f, i / (float)segments);
                uvs[iVert + 1] = new Vector2(1f, i / (float)segments);

                int next = ((i + 1) % segments) * 2;
                int t = i * 6;
                // Winding faces -Z (back toward the viewer at PodEyeSpawn z=0).
                tris[t] = iVert;
                tris[t + 1] = next + 1;
                tris[t + 2] = iVert + 1;
                tris[t + 3] = iVert;
                tris[t + 4] = next;
                tris[t + 5] = next + 1;
            }

            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material CreateLitMat(string path, Color color, float metallic, float smoothness, Color emission, bool transparent)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0.001f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
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

        private static GameObject BuildStation1PrefabAndInstance(Station1MatSet mats, Camera mainCam)
        {
            GameObject root = new GameObject("Station_1_Intrinsic");
            AudioSource stationAudio = root.AddComponent<AudioSource>();
            stationAudio.spatialBlend = 0.85f;
            stationAudio.playOnAwake = false;

            // NPC standing marker next to Station 1 anchor (player-left side near approach from Tutorial Pad).
            GameObject npcStand = new GameObject("NpcStandPoint");
            npcStand.transform.SetParent(root.transform, false);
            npcStand.transform.localPosition = new Vector3(1.25f, 0f, 1.05f);

            // 2.08 m wide x 0.88 m deep x 0.90 m high workbench facing +Z.
            BuildWorkbench(root.transform, 2.08f, 0.88f, 0.90f, mats);

            // Teleport Anchor, Floor Sign "1", and Hologram Title above console displays.
            StationBeacon beacon = BuildBeaconParts(root.transform, "1", "Intrinsic Silicon", mats, mainCam, out TeleportationAnchor anchor);

            // Console Displays (mounted at z = -0.34, raised at y = 1.66 so center Thermal Cube & Hologram never occlude text).
            BuildConsolePosts(root.transform, mats);
            BuildLeftMetersAndGroupIVConsole(root.transform, mats, mainCam,
                out TMP_Text materialHeaderText,
                out TMP_Text tempText,
                out TMP_Text carrierText,
                out TMP_Text gapText,
                out TMP_Text sigmaText,
                out TMP_Text latticeSummaryText,
                out TMP_Text groupIvText);

            EnergyBandDiagramView bandView = BuildRightEnergyBandConsole(root.transform, mats, mainCam);

            // Interactive Optical Bench Microscope (with Silicon/Germanium wafer loaded beneath the objective lens).
            MicroscopeViewController microscopeCtrl = BuildLabMicroscope(
                root.transform, mats, stationAudio, mainCam,
                out GameObject scanRing,
                out Renderer stageWaferRenderer,
                out LatticeHologramController latticeCtrl);

            // Angled prompt placard cleanly in front of Microscope LOOK IN button.
            GameObject waferPromptBoard = new GameObject("MicroscopePromptBoard");
            waferPromptBoard.transform.SetParent(root.transform, false);
            waferPromptBoard.transform.localPosition = new Vector3(0.58f, 0.932f, 0.372f);
            waferPromptBoard.transform.localRotation = Quaternion.Euler(58f, 180f, 0f);
            GameObject waferPromptBack = CreateBox("Backing", waferPromptBoard.transform, Vector3.zero, new Vector3(0.36f, 0.052f, 0.014f), mats.ScreenPanel, false);
            Object.DestroyImmediate(waferPromptBack.GetComponent<Collider>());
            Canvas waferPromptCanvas = LabTextStyleHelper.CreateWorldCanvas("WaferPromptCanvas", waferPromptBoard.transform,
                new Vector3(0f, 0f, -0.010f), Vector3.zero,
                new Vector2(0.34f, 0.046f), 360, 48, mainCam, false);
            TMP_Text waferPromptText = LabTextStyleHelper.CreateTMPText(waferPromptCanvas.transform, "Prompt",
                "Interact With Microscope To View Atomic Lattice", 15f, Vector2.zero, new Vector2(342f, 40f),
                new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Center 3D Thermal Chamber Cube showing live temperature-driven lattice vibration & carrier speed/density.
            ThermalChamberCubeController thermalCubeCtrl = BuildThermalChamberCube(root.transform, mats, mainCam);

            // Temperature Rotary Knob + Angled Interactive Slider + +/-50 K step buttons + Si/Ge Toggle + Reset button.
            RotaryKnobControl tempKnob = BuildTemperatureConsole(root.transform, mats, stationAudio, mainCam,
                out Slider tempSlider, out PhysicalPushButton tempDownBtn, out PhysicalPushButton tempUpBtn);

            PhysicalPushButton matToggleBtn = BuildLabeledButton(root.transform, "MaterialToggleButton",
                new Vector3(-0.88f, 0.90f, -0.04f), new Vector3(0.14f, 0.045f, 0.11f),
                mats.ButtonBlue, "MAT: Si -> Ge", "Toggle Si / Ge", mats, stationAudio, mainCam, out TMP_Text matToggleLabel, 0.17f);

            PhysicalPushButton resetBtn = BuildLabeledButton(root.transform, "ResetStationButton",
                new Vector3(-0.88f, 0.90f, 0.26f), new Vector3(0.11f, 0.042f, 0.095f),
                mats.ButtonAmber, "RESET STATION", "Reset Station 1", mats, stationAudio, mainCam, out _, 0.18f);

            // Wire IntrinsicSiliconStationController.
            IntrinsicSiliconStationController ctrl = root.AddComponent<IntrinsicSiliconStationController>();
            SerializedObject cSo = new SerializedObject(ctrl);
            LabTextStyleHelper.SetObjRef(cSo, "stationBeacon", beacon);
            LabTextStyleHelper.SetObjRef(cSo, "npcStandPoint", npcStand.transform);
            LabTextStyleHelper.SetObjectArray(cSo, "hintTargets", new Transform[]
            {
                anchor.transform,
                microscopeCtrl.transform,
                tempKnob.transform,
                matToggleBtn.transform
            });
            LabTextStyleHelper.SetObjRef(cSo, "stationAnchor", anchor);
            LabTextStyleHelper.SetObjRef(cSo, "stageWaferRenderer", stageWaferRenderer);
            LabTextStyleHelper.SetObjRef(cSo, "microscopeController", microscopeCtrl);
            LabTextStyleHelper.SetObjRef(cSo, "temperatureKnob", tempKnob);
            LabTextStyleHelper.SetObjRef(cSo, "temperatureSlider", tempSlider);
            LabTextStyleHelper.SetObjRef(cSo, "tempUpButton", tempUpBtn);
            LabTextStyleHelper.SetObjRef(cSo, "tempDownButton", tempDownBtn);
            LabTextStyleHelper.SetObjRef(cSo, "materialToggleButton", matToggleBtn);
            LabTextStyleHelper.SetObjRef(cSo, "resetStationButton", resetBtn);
            LabTextStyleHelper.SetObjRef(cSo, "scanTargetRing", scanRing);
            LabTextStyleHelper.SetObjRef(cSo, "latticeHologram", latticeCtrl);
            LabTextStyleHelper.SetObjRef(cSo, "thermalChamberCube", thermalCubeCtrl);
            LabTextStyleHelper.SetObjRef(cSo, "bandDiagramView", bandView);
            LabTextStyleHelper.SetObjRef(cSo, "materialHeaderText", materialHeaderText);
            LabTextStyleHelper.SetObjRef(cSo, "temperatureReadoutText", tempText);
            LabTextStyleHelper.SetObjRef(cSo, "carrierReadoutText", carrierText);
            LabTextStyleHelper.SetObjRef(cSo, "bandGapReadoutText", gapText);
            LabTextStyleHelper.SetObjRef(cSo, "conductivityReadoutText", sigmaText);
            LabTextStyleHelper.SetObjRef(cSo, "latticeSummaryText", latticeSummaryText);
            LabTextStyleHelper.SetObjRef(cSo, "waferPromptText", waferPromptText);
            LabTextStyleHelper.SetObjRef(cSo, "materialToggleLabel", matToggleLabel);
            LabTextStyleHelper.SetObjRef(cSo, "groupIvChartText", groupIvText);
            LabTextStyleHelper.SetObjRef(cSo, "audioSource", stationAudio);
            LabTextStyleHelper.SetBool(cSo, "startUnlockedForStandalone", false);
            cSo.ApplyModifiedPropertiesWithoutUndo();

            // Save self-contained Station 1 prefab and connect instance.
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, Station1PrefabPath, InteractionMode.AutomatedAction);
            return root;
        }

        private static MicroscopeViewController BuildLabMicroscope(
            Transform stationRoot,
            Station1MatSet mats,
            AudioSource stationAudio,
            Camera mainCam,
            out GameObject scanRing,
            out Renderer stageWaferRenderer,
            out LatticeHologramController latticeCtrl)
        {
            GameObject scopeRoot = new GameObject("LabMicroscope");
            scopeRoot.transform.SetParent(stationRoot, false);
            scopeRoot.transform.localPosition = new Vector3(0.58f, 0.90f, -0.02f);

            List<Renderer> stageLightRends = new List<Renderer>();

            // Glowing attention ring on tabletop around Microscope base until first inspection.
            scanRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            scanRing.name = "ScanTargetRing";
            scanRing.transform.SetParent(scopeRoot.transform, false);
            scanRing.transform.localPosition = new Vector3(0f, 0.004f, 0.02f);
            scanRing.transform.localScale = new Vector3(0.28f, 0.004f, 0.28f);
            Object.DestroyImmediate(scanRing.GetComponent<Collider>());
            scanRing.GetComponent<Renderer>().sharedMaterial = mats.AccentBase;

            // Cast base + subtle recessed condenser illuminator port.
            CreateBox("Base", scopeRoot.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.22f, 0.03f, 0.27f), mats.TableFrame, true);
            GameObject condenserDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            condenserDisc.name = "BaseCondenserLight";
            condenserDisc.transform.SetParent(scopeRoot.transform, false);
            condenserDisc.transform.localPosition = new Vector3(0f, 0.031f, 0.035f);
            condenserDisc.transform.localScale = new Vector3(0.068f, 0.003f, 0.068f);
            Object.DestroyImmediate(condenserDisc.GetComponent<Collider>());
            Renderer condenserRend = condenserDisc.GetComponent<Renderer>();
            condenserRend.sharedMaterial = mats.AccentBase;
            stageLightRends.Add(condenserRend);

            // Pillar arm & mechanical side focus knobs.
            CreateBox("PillarColumn", scopeRoot.transform, new Vector3(0f, 0.16f, -0.095f), new Vector3(0.06f, 0.28f, 0.065f), mats.TableFrame, true);
            CreateBox("PillarNeck", scopeRoot.transform, new Vector3(0f, 0.295f, -0.03f), new Vector3(0.065f, 0.055f, 0.165f), mats.TableFrame, true);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject coarse = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                coarse.name = side < 0 ? "FocusKnob_R" : "FocusKnob_L";
                coarse.transform.SetParent(scopeRoot.transform, false);
                coarse.transform.localPosition = new Vector3(side * 0.048f, 0.12f, -0.095f);
                coarse.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                coarse.transform.localScale = new Vector3(0.048f, 0.018f, 0.048f);
                Object.DestroyImmediate(coarse.GetComponent<Collider>());
                coarse.GetComponent<Renderer>().sharedMaterial = mats.MicroscopeMetal;
            }

            // Specimen Stage + Clips + Active Wafer Slide beneath objective lens.
            CreateBox("SpecimenStage", scopeRoot.transform, new Vector3(0f, 0.112f, 0.035f), new Vector3(0.18f, 0.014f, 0.16f), mats.TableFrame, true);
            CreateBox("StageClip_L", scopeRoot.transform, new Vector3(0.058f, 0.123f, 0.035f), new Vector3(0.025f, 0.005f, 0.05f), mats.MicroscopeMetal, true);
            CreateBox("StageClip_R", scopeRoot.transform, new Vector3(-0.058f, 0.123f, 0.035f), new Vector3(0.025f, 0.005f, 0.05f), mats.MicroscopeMetal, true);

            GameObject stageWafer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stageWafer.name = "StageWaferSpecimen";
            stageWafer.transform.SetParent(scopeRoot.transform, false);
            stageWafer.transform.localPosition = new Vector3(0f, 0.122f, 0.035f);
            stageWafer.transform.localScale = new Vector3(0.105f, 0.005f, 0.105f);
            Object.DestroyImmediate(stageWafer.GetComponent<Collider>());
            stageWaferRenderer = stageWafer.GetComponent<Renderer>();
            stageWaferRenderer.sharedMaterial = mats.WaferSi;

            // Turret Nosepiece + Objective Barrels + Soft Optical Light Beam.
            GameObject turret = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            turret.name = "ObjectiveTurret";
            turret.transform.SetParent(scopeRoot.transform, false);
            turret.transform.localPosition = new Vector3(0f, 0.258f, 0.035f);
            turret.transform.localScale = new Vector3(0.088f, 0.015f, 0.088f);
            Object.DestroyImmediate(turret.GetComponent<Collider>());
            turret.GetComponent<Renderer>().sharedMaterial = mats.MicroscopeMetal;

            GameObject objMain = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            objMain.name = "Objective_100x";
            objMain.transform.SetParent(scopeRoot.transform, false);
            objMain.transform.localPosition = new Vector3(0f, 0.212f, 0.035f);
            objMain.transform.localScale = new Vector3(0.030f, 0.035f, 0.030f);
            Object.DestroyImmediate(objMain.GetComponent<Collider>());
            objMain.GetComponent<Renderer>().sharedMaterial = mats.MicroscopeMetal;

            GameObject objSide = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            objSide.name = "Objective_40x";
            objSide.transform.SetParent(scopeRoot.transform, false);
            objSide.transform.localPosition = new Vector3(0.032f, 0.222f, 0.048f);
            objSide.transform.localRotation = Quaternion.Euler(12f, 0f, -28f);
            objSide.transform.localScale = new Vector3(0.022f, 0.028f, 0.022f);
            Object.DestroyImmediate(objSide.GetComponent<Collider>());
            objSide.GetComponent<Renderer>().sharedMaterial = mats.MicroscopeMetal;

            GameObject lightCone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lightCone.name = "OpticalStageBeam";
            lightCone.transform.SetParent(scopeRoot.transform, false);
            lightCone.transform.localPosition = new Vector3(0f, 0.155f, 0.035f);
            lightCone.transform.localScale = new Vector3(0.026f, 0.030f, 0.026f);
            Object.DestroyImmediate(lightCone.GetComponent<Collider>());
            lightCone.GetComponent<Renderer>().sharedMaterial = mats.LensGlass;

            // Angled Clickable/Select-able Eyepiece Tube leaning forward toward viewer (+Z) with recessed optical lens.
            GameObject eyepieceGo = new GameObject("MicroscopeEyepiece");
            eyepieceGo.transform.SetParent(scopeRoot.transform, false);
            eyepieceGo.transform.localPosition = new Vector3(0f, 0.305f, -0.005f);
            eyepieceGo.transform.localRotation = Quaternion.Euler(36f, 0f, 0f);

            GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = "EyepieceTube";
            tube.transform.SetParent(eyepieceGo.transform, false);
            tube.transform.localPosition = new Vector3(0f, 0.068f, 0f);
            tube.transform.localScale = new Vector3(0.044f, 0.072f, 0.044f);
            Object.DestroyImmediate(tube.GetComponent<Collider>());
            Renderer tubeRend = tube.GetComponent<Renderer>();
            tubeRend.sharedMaterial = mats.MicroscopeMetal;

            GameObject eyeCup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            eyeCup.name = "EyeCupCollar";
            eyeCup.transform.SetParent(eyepieceGo.transform, false);
            eyeCup.transform.localPosition = new Vector3(0f, 0.142f, 0f);
            eyeCup.transform.localScale = new Vector3(0.062f, 0.016f, 0.062f);
            Object.DestroyImmediate(eyeCup.GetComponent<Collider>());
            Renderer eyeCupRend = eyeCup.GetComponent<Renderer>();
            eyeCupRend.sharedMaterial = mats.TableFrame;

            GameObject eyeGlass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            eyeGlass.name = "EyeLensGlow";
            eyeGlass.transform.SetParent(eyepieceGo.transform, false);
            eyeGlass.transform.localPosition = new Vector3(0f, 0.149f, 0f);
            eyeGlass.transform.localScale = new Vector3(0.036f, 0.002f, 0.036f);
            Object.DestroyImmediate(eyeGlass.GetComponent<Collider>());
            Renderer eyeGlassRend = eyeGlass.GetComponent<Renderer>();
            eyeGlassRend.sharedMaterial = mats.LensGlass;
            stageLightRends.Add(eyeGlassRend);

            BoxCollider eyeCol = eyepieceGo.AddComponent<BoxCollider>();
            eyeCol.center = new Vector3(0f, 0.072f, 0f);
            eyeCol.size = new Vector3(0.11f, 0.16f, 0.11f);

            Rigidbody eyeRb = eyepieceGo.AddComponent<Rigidbody>();
            eyeRb.isKinematic = true;
            eyeRb.useGravity = false;

            XRSimpleInteractable eyeXr = eyepieceGo.AddComponent<XRSimpleInteractable>();
            eyeXr.colliders.Add(eyeCol);
            AddInteractionFeedback(eyepieceGo, eyeXr, new[] { eyeCupRend, tubeRend, eyeGlassRend }, stationAudio,
                "Trigger / Click: Look In Microscope", new Vector3(0f, 0.22f, 0.06f), mats, mainCam);

            Canvas eyeUiCanvas = LabTextStyleHelper.CreateWorldCanvas("EyepieceUiOverlay", eyepieceGo.transform,
                new Vector3(0f, 0.166f, 0f), new Vector3(90f, 180f, 0f),
                new Vector2(0.095f, 0.095f), 140, 140, mainCam, true);
            Button eyeUiBtn = CreateInvisibleClickButton(eyeUiCanvas.transform, "ClickEyepiece", new Vector2(140f, 140f));

            PhysicalPushButton inspectBtn = BuildLabeledButton(scopeRoot.transform, "InspectMicroscopeButton",
                new Vector3(0f, 0f, 0.22f), new Vector3(0.14f, 0.040f, 0.092f),
                mats.ButtonGreen, "LOOK IN MICROSCOPE", "Enter Microscope View", mats, stationAudio, mainCam, out _, 0.24f);

            // Build isolated Specimen Chamber + In-Place One-Eyed Optical Eyepiece Viewer.
            GameObject specimenChamberRoot = BuildSpecimenChamber(
                stationRoot, mats, mainCam, stationAudio,
                out Camera specimenCam,
                out Renderer condenserBackdropRenderer,
                out latticeCtrl);

            GameObject viewerHudRoot = BuildEyepieceViewerRig(
                stationRoot, mats, mainCam,
                out XRSimpleInteractable lensViewportInteractable,
                out Collider lensViewportCollider,
                out Renderer apertureRimRenderer,
                out TMP_Text bezelTelemetryText,
                out TMP_Text bezelObjectiveText,
                out TMP_Text bezelHoverText,
                out TMP_Text bezelMatBtnText,
                out TMP_Text bezelZoomBtnText,
                out Button exitUiBtn,
                out XRSimpleInteractable exitXr,
                out Button tempDownBtn,
                out Button tempUpBtn,
                out Button matToggleBtn,
                out Button zoomBtn);

            MicroscopeViewController scopeCtrl = scopeRoot.AddComponent<MicroscopeViewController>();
            SerializedObject mSo = new SerializedObject(scopeCtrl);
            LabTextStyleHelper.SetObjRef(mSo, "eyepieceInteractable", eyeXr);
            LabTextStyleHelper.SetObjRef(mSo, "eyepieceUiButton", eyeUiBtn);
            LabTextStyleHelper.SetObjRef(mSo, "inspectButton", inspectBtn);
            LabTextStyleHelper.SetObjectArray(mSo, "stageLightRenderers", stageLightRends.ToArray());
            LabTextStyleHelper.SetObjRef(mSo, "viewerHudRoot", viewerHudRoot);
            LabTextStyleHelper.SetObjRef(mSo, "specimenChamberRoot", specimenChamberRoot);
            LabTextStyleHelper.SetObjRef(mSo, "specimenCamera", specimenCam);
            LabTextStyleHelper.SetObjRef(mSo, "lensViewportInteractable", lensViewportInteractable);
            LabTextStyleHelper.SetObjRef(mSo, "lensViewportCollider", lensViewportCollider);
            LabTextStyleHelper.SetFloat(mSo, "lensViewportRadius", 0.155f);
            LabTextStyleHelper.SetObjRef(mSo, "latticeHologram", latticeCtrl);
            LabTextStyleHelper.SetObjRef(mSo, "condenserBackdropRenderer", condenserBackdropRenderer);
            LabTextStyleHelper.SetObjRef(mSo, "apertureRimRenderer", apertureRimRenderer);
            LabTextStyleHelper.SetObjRef(mSo, "bezelTelemetryText", bezelTelemetryText);
            LabTextStyleHelper.SetObjRef(mSo, "bezelObjectiveText", bezelObjectiveText);
            LabTextStyleHelper.SetObjRef(mSo, "bezelHoverInfoText", bezelHoverText);
            LabTextStyleHelper.SetObjRef(mSo, "bezelMaterialButtonText", bezelMatBtnText);
            LabTextStyleHelper.SetObjRef(mSo, "bezelZoomButtonText", bezelZoomBtnText);
            LabTextStyleHelper.SetObjRef(mSo, "exitUiButton", exitUiBtn);
            LabTextStyleHelper.SetObjRef(mSo, "exitInteractable", exitXr);
            LabTextStyleHelper.SetObjRef(mSo, "bezelTempDownButton", tempDownBtn);
            LabTextStyleHelper.SetObjRef(mSo, "bezelTempUpButton", tempUpBtn);
            LabTextStyleHelper.SetObjRef(mSo, "bezelMaterialButton", matToggleBtn);
            LabTextStyleHelper.SetObjRef(mSo, "bezelZoomButton", zoomBtn);
            LabTextStyleHelper.SetObjRef(mSo, "audioSource", stationAudio);
            mSo.ApplyModifiedPropertiesWithoutUndo();

            return scopeCtrl;
        }

        private static GameObject BuildSpecimenChamber(
            Transform stationRoot,
            Station1MatSet mats,
            Camera mainCam,
            AudioSource stationAudio,
            out Camera specimenCamera,
            out Renderer condenserBackdropRenderer,
            out LatticeHologramController latticeCtrl)
        {
            // Dedicated off-stage optical specimen chamber below room floor (never visited by XR Origin).
            GameObject chamberRoot = new GameObject("MicroscopeSpecimenChamber");
            chamberRoot.transform.SetParent(stationRoot, false);
            chamberRoot.transform.localPosition = new Vector3(0f, -9.0f, 0f);

            GameObject camGo = new GameObject("MicroscopeSpecimenCamera");
            camGo.transform.SetParent(chamberRoot.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.50f, 0.08f);
            camGo.transform.localRotation = Quaternion.identity;

            specimenCamera = camGo.AddComponent<Camera>();
            specimenCamera.targetTexture = mats.MicroscopeRT;
            specimenCamera.fieldOfView = 34f;
            specimenCamera.aspect = 1.0f;
            specimenCamera.nearClipPlane = 0.05f;
            specimenCamera.farClipPlane = 10f;
            specimenCamera.clearFlags = CameraClearFlags.SolidColor;
            specimenCamera.backgroundColor = new Color(0.015f, 0.032f, 0.068f, 1f);
            specimenCamera.depth = -5f;
            specimenCamera.enabled = false;

            GameObject condenserDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            condenserDisc.name = "CondenserBackdrop";
            condenserDisc.transform.SetParent(chamberRoot.transform, false);
            condenserDisc.transform.localPosition = new Vector3(0f, 1.50f, 1.38f);
            condenserDisc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            condenserDisc.transform.localScale = new Vector3(1.15f, 0.006f, 1.15f);
            Object.DestroyImmediate(condenserDisc.GetComponent<Collider>());
            condenserBackdropRenderer = condenserDisc.GetComponent<Renderer>();
            condenserBackdropRenderer.sharedMaterial = mats.MicroscopeBackdrop;

            latticeCtrl = BuildLatticeHologram(chamberRoot.transform, mats, stationAudio, mainCam);
            return chamberRoot;
        }

        private static GameObject BuildEyepieceViewerRig(
            Transform stationRoot,
            Station1MatSet mats,
            Camera mainCam,
            out XRSimpleInteractable lensViewportInteractable,
            out Collider lensViewportCollider,
            out Renderer apertureRimRenderer,
            out TMP_Text telemetryText,
            out TMP_Text objectiveText,
            out TMP_Text hoverInfoText,
            out TMP_Text matButtonText,
            out TMP_Text zoomButtonText,
            out Button exitUiButton,
            out XRSimpleInteractable exitXr,
            out Button tempDownButton,
            out Button tempUpButton,
            out Button matToggleButton,
            out Button zoomButton)
        {
            GameObject viewerRoot = new GameObject("EyepieceViewerRig");
            viewerRoot.transform.SetParent(stationRoot, false);
            viewerRoot.transform.localPosition = new Vector3(0f, 1.55f, 0.78f);
            viewerRoot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Optical circular eyepiece on the left/center-left; side instrument panel on the right (clear of hands).
            Vector3 lensCenter = new Vector3(-0.085f, 0.015f, 0.012f);
            float holeRadius = 0.155f;

            Mesh maskMesh = CreateOrLoadAnnulusMesh(ModelsFolder + "/Mesh_MicroscopeAperture.asset", holeRadius, 1.95f, 64);
            GameObject apertureMask = new GameObject("EyepieceApertureMask");
            apertureMask.transform.SetParent(viewerRoot.transform, false);
            apertureMask.transform.localPosition = lensCenter + new Vector3(0f, 0f, -0.002f);
            apertureMask.AddComponent<MeshFilter>().sharedMesh = maskMesh;
            apertureMask.AddComponent<MeshRenderer>().sharedMaterial = mats.MicroscopeVoid;

            Mesh lensMesh = CreateOrLoadCircleMesh(ModelsFolder + "/Mesh_CircleLens.asset", holeRadius, 64);
            GameObject lensDisc = new GameObject("CircularLensDisc");
            lensDisc.transform.SetParent(viewerRoot.transform, false);
            lensDisc.transform.localPosition = lensCenter;
            lensDisc.AddComponent<MeshFilter>().sharedMesh = lensMesh;
            lensDisc.AddComponent<MeshRenderer>().sharedMaterial = mats.MicroscopeLensFeed;
            MeshCollider lensCol = lensDisc.AddComponent<MeshCollider>();
            lensCol.sharedMesh = lensMesh;
            lensViewportCollider = lensCol;
            lensViewportInteractable = lensDisc.AddComponent<XRSimpleInteractable>();
            lensViewportInteractable.colliders.Add(lensCol);

            Mesh rimMesh = CreateOrLoadAnnulusMesh(ModelsFolder + "/Mesh_MicroscopeRim.asset", holeRadius - 0.002f, holeRadius + 0.007f, 64);
            GameObject rimGo = new GameObject("EyepieceRimRing");
            rimGo.transform.SetParent(viewerRoot.transform, false);
            rimGo.transform.localPosition = lensCenter + new Vector3(0f, 0f, -0.004f);
            rimGo.AddComponent<MeshFilter>().sharedMesh = rimMesh;
            apertureRimRenderer = rimGo.AddComponent<MeshRenderer>();
            apertureRimRenderer.sharedMaterial = mats.AccentBase;

            // Delicate optical crosshair reticle ticks along inner lens rim.
            float tickR = holeRadius - 0.010f;
            Vector3[] tickPos =
            {
                lensCenter + new Vector3(0f, tickR, -0.005f),
                lensCenter + new Vector3(0f, -tickR, -0.005f),
                lensCenter + new Vector3(tickR, 0f, -0.005f),
                lensCenter + new Vector3(-tickR, 0f, -0.005f)
            };
            Vector3[] tickScale =
            {
                new Vector3(0.002f, 0.014f, 0.002f),
                new Vector3(0.002f, 0.014f, 0.002f),
                new Vector3(0.014f, 0.002f, 0.002f),
                new Vector3(0.014f, 0.002f, 0.002f)
            };
            for (int i = 0; i < tickPos.Length; i++)
            {
                GameObject tick = CreateBox("ReticleTick_" + i, viewerRoot.transform, tickPos[i], tickScale[i], mats.AccentBase, false);
                Object.DestroyImmediate(tick.GetComponent<Collider>());
            }

            // Right-side ergonomic Microscope Control & Readout Console cleanly in front of ApertureMask & clear of hands.
            GameObject sidePanel = new GameObject("SideControlPanel");
            sidePanel.transform.SetParent(viewerRoot.transform, false);
            sidePanel.transform.localPosition = new Vector3(0.245f, 0.030f, -0.055f);
            sidePanel.transform.localRotation = Quaternion.Euler(0f, 10f, 0f);

            GameObject hudBacking = CreateBox("Backing", sidePanel.transform, new Vector3(0f, 0f, 0.009f),
                new Vector3(0.292f, 0.365f, 0.012f), mats.ScreenPanel, false);
            Object.DestroyImmediate(hudBacking.GetComponent<Collider>());

            Canvas hudCanvas = LabTextStyleHelper.CreateWorldCanvas("BezelHudCanvas", sidePanel.transform,
                Vector3.zero, Vector3.zero,
                new Vector2(0.280f, 0.350f), 420, 525, mainCam, true);

            LabTextStyleHelper.CreateTMPText(hudCanvas.transform, "Header",
                "MICROSCOPE EYEPIECE", 22f,
                new Vector2(0f, 232f), new Vector2(396f, 34f),
                new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            LabTextStyleHelper.CreateTMPText(hudCanvas.transform, "SubScale",
                "100,000,000x Schematic (Not to scale)", 14.5f,
                new Vector2(0f, 206f), new Vector2(396f, 22f),
                new Color(1.0f, 0.84f, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);

            telemetryText = LabTextStyleHelper.CreateTMPText(hudCanvas.transform, "Telemetry",
                "Material: Si\nTemperature: 0 K", 16f,
                new Vector2(0f, 134f), new Vector2(388f, 108f),
                Color.white, TextAlignmentOptions.TopLeft, FontStyles.Normal);

            objectiveText = LabTextStyleHelper.CreateTMPText(hudCanvas.transform, "Objective",
                "Objective: Inspect atomic lattice", 14f,
                new Vector2(0f, 52f), new Vector2(388f, 44f),
                new Color(1.0f, 0.86f, 0.42f), TextAlignmentOptions.Center, FontStyles.Bold);

            CreateUIImageRect(hudCanvas.transform, "HoverBoxBg", new Vector2(0f, -16f), new Vector2(392f, 68f),
                new Color(0.08f, 0.13f, 0.21f, 0.92f));
            hoverInfoText = LabTextStyleHelper.CreateTMPText(hudCanvas.transform, "HoverInfo",
                "Point ray inside lens to inspect", 14.5f,
                new Vector2(0f, -16f), new Vector2(376f, 60f),
                new Color(0.78f, 0.94f, 1.0f), TextAlignmentOptions.Center, FontStyles.Normal);

            tempDownButton = CreateHudActionButton(hudCanvas.transform, "BezelTempDownBtn", "-50 K",
                new Vector2(-98f, -86f), new Vector2(186f, 42f),
                new Color(0.84f, 0.52f, 0.12f, 1f), out _);

            tempUpButton = CreateHudActionButton(hudCanvas.transform, "BezelTempUpBtn", "+50 K",
                new Vector2(98f, -86f), new Vector2(186f, 42f),
                new Color(0.14f, 0.68f, 0.36f, 1f), out _);

            matToggleButton = CreateHudActionButton(hudCanvas.transform, "BezelMatToggleBtn", "MAT: Si -> Ge",
                new Vector2(-78f, -138f), new Vector2(226f, 42f),
                new Color(0.14f, 0.50f, 0.86f, 1f), out matButtonText);

            zoomButton = CreateHudActionButton(hudCanvas.transform, "BezelZoomBtn", "ZOOM: 1.0x",
                new Vector2(118f, -138f), new Vector2(146f, 42f),
                new Color(0.24f, 0.34f, 0.48f, 1f), out zoomButtonText);

            exitUiButton = CreateHudActionButton(hudCanvas.transform, "BezelExitBtn", "EXIT MICROSCOPE",
                new Vector2(0f, -206f), new Vector2(382f, 52f),
                new Color(0.84f, 0.20f, 0.22f, 1f), out _);

            GameObject exitColGo = new GameObject("ExitGripTarget");
            exitColGo.transform.SetParent(sidePanel.transform, false);
            exitColGo.transform.localPosition = new Vector3(0f, -0.137f, 0.020f);
            BoxCollider exitCol = exitColGo.AddComponent<BoxCollider>();
            exitCol.size = new Vector3(0.25f, 0.036f, 0.02f);
            Rigidbody exitRb = exitColGo.AddComponent<Rigidbody>();
            exitRb.isKinematic = true;
            exitRb.useGravity = false;
            exitXr = exitColGo.AddComponent<XRSimpleInteractable>();
            exitXr.colliders.Add(exitCol);

            return viewerRoot;
        }

        private static Button CreateHudActionButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Color bgColor,
            out TMP_Text labelText)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Image img = go.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            btn.colors = cb;

            labelText = LabTextStyleHelper.CreateTMPText(go.transform, "Label", label, 18f,
                Vector2.zero, new Vector2(sizeDelta.x - 10f, sizeDelta.y - 6f),
                Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 13f;
            labelText.fontSizeMax = 18f;
            return btn;
        }

        private static LatticeHologramController BuildLatticeHologram(Transform chamberParent, Station1MatSet mats, AudioSource stationAudio, Camera mainCam)
        {
            GameObject holoRoot = new GameObject("LatticeHologram");
            holoRoot.transform.SetParent(chamberParent, false);
            holoRoot.transform.localPosition = new Vector3(0f, 1.50f, 0.96f);

            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(holoRoot.transform, false);

            // "Not to scale" notice inside bottom edge of optical field (clean, never overlapping bottom atom).
            GameObject scaleHolder = new GameObject("ScaleLabelHolder");
            scaleHolder.transform.SetParent(visualRoot.transform, false);
            scaleHolder.transform.localPosition = new Vector3(0f, -0.205f, 0f);
            GameObject scaleBack = CreateBox("ScaleLabelBack", scaleHolder.transform,
                new Vector3(0f, 0f, 0.006f), new Vector3(0.32f, 0.028f, 0.008f), mats.ScreenPanel, false);
            Object.DestroyImmediate(scaleBack.GetComponent<Collider>());
            Canvas scaleCanvas = LabTextStyleHelper.CreateWorldCanvas("ScaleLabelCanvas", scaleHolder.transform,
                Vector3.zero, Vector3.zero,
                new Vector2(0.30f, 0.025f), 360, 30, mainCam, false);
            TMP_Text scaleText = LabTextStyleHelper.CreateTMPText(scaleCanvas.transform, "ScaleText",
                "Schematic Crystal (Not to scale)", 14f, Vector2.zero, new Vector2(344f, 26f),
                new Color(1.0f, 0.84f, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Refined proportional Diamond-Cubic 1-unit-cell (size = 0.28 m -> comfortable margin inside 34 deg FOV circle).
            float size = 0.28f;
            float half = size * 0.5f;
            List<Vector3> unitSites = new List<Vector3>
            {
                // 8 cube corners
                new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(0,1,0), new Vector3(1,1,0),
                new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(0,1,1), new Vector3(1,1,1),
                // 6 face centers
                new Vector3(0.5f,0.5f,0f), new Vector3(0.5f,0.5f,1f),
                new Vector3(0.5f,0f,0.5f), new Vector3(0.5f,1f,0.5f),
                new Vector3(0f,0.5f,0.5f), new Vector3(1f,0.5f,0.5f),
                // 4 interior tetrahedral sites (each bonds to 4 nearest FCC neighbors -> 16 covalent bonds)
                new Vector3(0.25f,0.25f,0.25f), new Vector3(0.75f,0.75f,0.25f),
                new Vector3(0.75f,0.25f,0.75f), new Vector3(0.25f,0.75f,0.75f)
            };

            LatticeElementTarget[] atomTargets = new LatticeElementTarget[unitSites.Count];
            Vector3[] localAtomPos = new Vector3[unitSites.Count];

            for (int i = 0; i < unitSites.Count; i++)
            {
                Vector3 p = (unitSites[i] * size) - new Vector3(half, half, half);
                localAtomPos[i] = p;

                GameObject atomGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                atomGo.name = "Atom_" + i;
                atomGo.transform.SetParent(visualRoot.transform, false);
                atomGo.transform.localPosition = p;
                atomGo.transform.localScale = Vector3.one * 0.024f;

                SphereCollider col = atomGo.GetComponent<SphereCollider>();
                col.radius = 0.95f;
                Renderer r = atomGo.GetComponent<Renderer>();
                r.sharedMaterial = mats.AtomCore;

                XRSimpleInteractable xr = atomGo.AddComponent<XRSimpleInteractable>();
                LatticeElementTarget t = atomGo.AddComponent<LatticeElementTarget>();
                t.Configure(xr, r, LatticeElementKind.HostAtom, i);
                atomTargets[i] = t;
            }

            List<LatticeElementTarget> bondTargets = new List<LatticeElementTarget>();
            List<Vector3> bondPos = new List<Vector3>();
            List<Transform> bondVisuals = new List<Transform>();
            List<GameObject> brokenMarkers = new List<GameObject>();

            float expectedUnitBondLen = Mathf.Sqrt(3f) * 0.25f;
            int bondIdx = 0;
            for (int a = 14; a < unitSites.Count; a++)
            {
                for (int b = 0; b < 14; b++)
                {
                    if (Mathf.Abs(Vector3.Distance(unitSites[a], unitSites[b]) - expectedUnitBondLen) < 0.02f)
                    {
                        Vector3 pA = localAtomPos[a];
                        Vector3 pB = localAtomPos[b];
                        Vector3 mid = (pA + pB) * 0.5f;
                        Vector3 dir = pB - pA;
                        float dist = dir.magnitude;

                        GameObject bondGo = new GameObject("Bond_" + bondIdx);
                        bondGo.transform.SetParent(visualRoot.transform, false);
                        bondGo.transform.localPosition = mid;
                        bondGo.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);

                        CapsuleCollider col = bondGo.AddComponent<CapsuleCollider>();
                        col.radius = 0.014f;
                        col.height = dist * 0.85f;
                        col.direction = 1;

                        GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        rod.name = "Rod";
                        rod.transform.SetParent(bondGo.transform, false);
                        rod.transform.localScale = new Vector3(0.0055f, dist * 0.44f, 0.0055f);
                        Object.DestroyImmediate(rod.GetComponent<Collider>());
                        Renderer rodRend = rod.GetComponent<Renderer>();
                        rodRend.sharedMaterial = mats.BondIntact;

                        GameObject breakSpark = CreateBox("BreakSpark", bondGo.transform, Vector3.zero, new Vector3(0.008f, 0.008f, 0.008f), mats.BondBroken, false);
                        Object.DestroyImmediate(breakSpark.GetComponent<Collider>());
                        breakSpark.SetActive(false);

                        XRSimpleInteractable xr = bondGo.AddComponent<XRSimpleInteractable>();
                        LatticeElementTarget t = bondGo.AddComponent<LatticeElementTarget>();
                        t.Configure(xr, rodRend, LatticeElementKind.CovalentBond, bondIdx);

                        bondTargets.Add(t);
                        bondPos.Add(mid);
                        bondVisuals.Add(rod.transform);
                        brokenMarkers.Add(breakSpark);
                        bondIdx++;
                    }
                }
            }

            int poolSize = 16;
            LatticeElementTarget[] ePool = new LatticeElementTarget[poolSize];
            LatticeElementTarget[] hPool = new LatticeElementTarget[poolSize];

            for (int i = 0; i < poolSize; i++)
            {
                ePool[i] = CreateCarrierPoolItem("FreeElectron_" + i, visualRoot.transform, mats.ElectronBlue, 0.018f, LatticeElementKind.FreeElectron, i);
                hPool[i] = CreateCarrierPoolItem("Hole_" + i, visualRoot.transform, mats.HoleRed, 0.020f, LatticeElementKind.Hole, i);
            }

            LatticeHologramController ctrl = holoRoot.AddComponent<LatticeHologramController>();
            ctrl.ConfigureReferences(
                visualRoot, scaleText, null, null,
                atomTargets, bondTargets.ToArray(), bondPos.ToArray(), bondVisuals.ToArray(),
                brokenMarkers.ToArray(), ePool, hPool, stationAudio);
            SerializedObject hSo = new SerializedObject(ctrl);
            LabTextStyleHelper.SetFloat(hSo, "intactBondThickness", 0.0055f);
            LabTextStyleHelper.SetFloat(hSo, "brokenBondThickness", 0.0022f);
            LabTextStyleHelper.SetBool(hSo, "billboardTooltip", false);
            hSo.ApplyModifiedPropertiesWithoutUndo();
            return ctrl;
        }

        private static LatticeElementTarget CreateCarrierPoolItem(
            string name,
            Transform parent,
            Material mat,
            float visualDia,
            LatticeElementKind kind,
            int index)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * visualDia;

            SphereCollider col = go.GetComponent<SphereCollider>();
            col.radius = 0.85f; // slightly generous hover target for XR ray-pointing
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;

            XRSimpleInteractable xr = go.AddComponent<XRSimpleInteractable>();
            LatticeElementTarget target = go.AddComponent<LatticeElementTarget>();
            target.Configure(xr, r, kind, index);
            go.SetActive(false);
            return target;
        }

        private static ThermalChamberCubeController BuildThermalChamberCube(Transform parent, Station1MatSet mats, Camera mainCam)
        {
            GameObject root = new GameObject("ThermalChamberCube");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(-0.02f, 0.90f, 0.02f);

            GameObject ped = CreateBox("ChamberBase", root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.30f, 0.04f, 0.30f), mats.TableFrame, true);
            Object.DestroyImmediate(ped.GetComponent<Collider>());

            // Sleek base perimeter trim & slim vertical corner rails tinted dynamically from 0 K (blue) -> 500 K (orange).
            List<Renderer> glowRends = new List<Renderer>();
            GameObject glowRing = CreateBox("ThermalBaseGlow", root.transform, new Vector3(0f, 0.042f, 0f), new Vector3(0.28f, 0.005f, 0.28f), mats.AccentBase, false);
            Object.DestroyImmediate(glowRing.GetComponent<Collider>());
            glowRends.Add(glowRing.GetComponent<Renderer>());

            float cubeCenterY = 0.19f;
            float cubeSize = 0.27f;
            float halfC = cubeSize * 0.5f;

            Vector3[] cornerOffsets =
            {
                new Vector3(-halfC, cubeCenterY, -halfC),
                new Vector3( halfC, cubeCenterY, -halfC),
                new Vector3(-halfC, cubeCenterY,  halfC),
                new Vector3( halfC, cubeCenterY,  halfC)
            };
            for (int i = 0; i < cornerOffsets.Length; i++)
            {
                GameObject rail = CreateBox("CornerRail_" + i, root.transform, cornerOffsets[i], new Vector3(0.009f, cubeSize, 0.009f), mats.AccentBase, false);
                Object.DestroyImmediate(rail.GetComponent<Collider>());
                glowRends.Add(rail.GetComponent<Renderer>());
            }

            // Translucent glass shell (collider removed so rays pass smoothly).
            GameObject glassShell = CreateBox("ChamberGlassShell", root.transform, new Vector3(0f, cubeCenterY, 0f), Vector3.one * cubeSize, mats.ChamberGlass, false);
            Object.DestroyImmediate(glassShell.GetComponent<Collider>());

            GameObject interior = new GameObject("Interior");
            interior.transform.SetParent(root.transform, false);
            interior.transform.localPosition = new Vector3(0f, cubeCenterY, 0f);

            // 9 vibrating crystal lattice cores inside cube (2x2x2 corners + center).
            Vector3[] atomOffsets =
            {
                new Vector3(-0.07f, -0.07f, -0.07f),
                new Vector3( 0.07f, -0.07f, -0.07f),
                new Vector3(-0.07f,  0.07f, -0.07f),
                new Vector3( 0.07f,  0.07f, -0.07f),
                new Vector3(-0.07f, -0.07f,  0.07f),
                new Vector3( 0.07f, -0.07f,  0.07f),
                new Vector3(-0.07f,  0.07f,  0.07f),
                new Vector3( 0.07f,  0.07f,  0.07f),
                Vector3.zero
            };
            Transform[] atoms = new Transform[atomOffsets.Length];
            for (int i = 0; i < atomOffsets.Length; i++)
            {
                GameObject a = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                a.name = "ThermalAtom_" + i;
                a.transform.SetParent(interior.transform, false);
                a.transform.localPosition = atomOffsets[i];
                a.transform.localScale = Vector3.one * 0.028f;
                Object.DestroyImmediate(a.GetComponent<Collider>());
                a.GetComponent<Renderer>().sharedMaterial = mats.AtomCore;
                atoms[i] = a.transform;
            }

            // Pooled 18 electrons (blue) + 18 holes (red) bouncing inside cube.
            int cap = 18;
            Transform[] ePool = new Transform[cap];
            Transform[] hPool = new Transform[cap];
            for (int i = 0; i < cap; i++)
            {
                GameObject eg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eg.name = "ChamberElectron_" + i;
                eg.transform.SetParent(interior.transform, false);
                eg.transform.localScale = Vector3.one * 0.019f;
                Object.DestroyImmediate(eg.GetComponent<Collider>());
                eg.GetComponent<Renderer>().sharedMaterial = mats.ElectronBlue;
                eg.SetActive(false);
                ePool[i] = eg.transform;

                GameObject hg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hg.name = "ChamberHole_" + i;
                hg.transform.SetParent(interior.transform, false);
                hg.transform.localScale = Vector3.one * 0.022f;
                Object.DestroyImmediate(hg.GetComponent<Collider>());
                hg.GetComponent<Renderer>().sharedMaterial = mats.HoleRed;
                hg.SetActive(false);
                hPool[i] = hg.transform;
            }

            // Front angled readout plaque (Euler(58, 180, 0) tilts face upward toward viewer cleanly).
            GameObject labelHolder = new GameObject("ChamberLabelHolder");
            labelHolder.transform.SetParent(root.transform, false);
            labelHolder.transform.localPosition = new Vector3(0f, 0.036f, 0.185f);
            labelHolder.transform.localRotation = Quaternion.Euler(58f, 180f, 0f);
            GameObject lblBack = CreateBox("Backing", labelHolder.transform, Vector3.zero, new Vector3(0.33f, 0.056f, 0.014f), mats.ScreenPanel, false);
            Object.DestroyImmediate(lblBack.GetComponent<Collider>());
            Canvas lblCanvas = LabTextStyleHelper.CreateWorldCanvas("ChamberLabelCanvas", labelHolder.transform,
                new Vector3(0f, 0f, -0.010f), Vector3.zero,
                new Vector2(0.31f, 0.050f), 340, 54, mainCam, false);
            TMP_Text lblText = LabTextStyleHelper.CreateTMPText(lblCanvas.transform, "Status",
                "Si | 0 K: Frozen — Not to scale", 16f, Vector2.zero, new Vector2(326f, 46f),
                new Color(0.95f, 0.98f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);

            ThermalChamberCubeController ctrl = root.AddComponent<ThermalChamberCubeController>();
            ctrl.ConfigureReferences(atoms, atomOffsets, ePool, hPool, glowRends.ToArray(), lblText);
            return ctrl;
        }

        private static RotaryKnobControl BuildTemperatureConsole(
            Transform parent,
            Station1MatSet mats,
            AudioSource stationAudio,
            Camera mainCam,
            out Slider tempSlider,
            out PhysicalPushButton tempDownBtn,
            out PhysicalPushButton tempUpBtn)
        {
            GameObject deck = new GameObject("TemperatureDeck");
            deck.transform.SetParent(parent, false);
            deck.transform.localPosition = new Vector3(-0.505f, 0.90f, 0.10f);

            CreateBox("Plinth", deck.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.46f, 0.04f, 0.48f), mats.TableFrame, true);

            Canvas dialCanvas = LabTextStyleHelper.CreateWorldCanvas("TempDialLabels", deck.transform,
                new Vector3(0f, 0.044f, -0.105f), new Vector3(90f, 180f, 0f),
                new Vector2(0.44f, 0.22f), 440, 220, mainCam, false);
            LabTextStyleHelper.CreateTMPText(dialCanvas.transform, "Title", "TEMPERATURE (0 - 500 K)", 21f,
                new Vector2(0f, 88f), new Vector2(410f, 32f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);
            LabTextStyleHelper.CreateTMPText(dialCanvas.transform, "Min0K", "0 K", 18f,
                new Vector2(-132f, -10f), new Vector2(80f, 28f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            LabTextStyleHelper.CreateTMPText(dialCanvas.transform, "Mid300K", "300 K", 18f,
                new Vector2(0f, 56f), new Vector2(90f, 28f), new Color(0.55f, 0.96f, 0.72f), TextAlignmentOptions.Center, FontStyles.Bold);
            LabTextStyleHelper.CreateTMPText(dialCanvas.transform, "Max500K", "500 K", 18f,
                new Vector2(132f, -10f), new Vector2(80f, 28f), new Color(1.0f, 0.70f, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Raised, tilted UI Slider bar in clear middle row so neither Knob behind nor +/- buttons in front occlude it.
            GameObject sliderBar = new GameObject("TempSliderPedestal");
            sliderBar.transform.SetParent(deck.transform, false);
            sliderBar.transform.localPosition = new Vector3(0f, 0.072f, 0.040f);
            sliderBar.transform.localRotation = Quaternion.Euler(58f, 180f, 0f);
            GameObject sliderBack = CreateBox("Backing", sliderBar.transform, Vector3.zero, new Vector3(0.40f, 0.072f, 0.018f), mats.ScreenPanel, false);
            Object.DestroyImmediate(sliderBack.GetComponent<Collider>());
            Canvas sliderCanvas = LabTextStyleHelper.CreateWorldCanvas("TempSliderCanvas", sliderBar.transform,
                new Vector3(0f, 0f, -0.012f), Vector3.zero,
                new Vector2(0.38f, 0.068f), 380, 68, mainCam, true);
            tempSlider = CreateUISlider(sliderCanvas.transform, "TemperatureSlider", Vector2.zero, new Vector2(340f, 40f));
            tempSlider.minValue = 0f;
            tempSlider.maxValue = 500f;
            tempSlider.value = 0f;

            GameObject knobGo = new GameObject("TemperatureKnob");
            knobGo.transform.SetParent(deck.transform, false);
            knobGo.transform.localPosition = new Vector3(0f, 0.042f, -0.105f);

            GameObject dialBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dialBase.name = "DialBody";
            dialBase.transform.SetParent(knobGo.transform, false);
            dialBase.transform.localPosition = new Vector3(0f, 0.024f, 0f);
            dialBase.transform.localScale = new Vector3(0.11f, 0.024f, 0.11f);
            Object.DestroyImmediate(dialBase.GetComponent<Collider>());
            dialBase.GetComponent<Renderer>().sharedMaterial = mats.ScreenPanel;

            GameObject dialBasis = new GameObject("DialDragBasis");
            dialBasis.transform.SetParent(deck.transform, false);
            dialBasis.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject pointer = CreateBox("PointerBar", dialBase.transform, new Vector3(0f, 0.85f, -0.25f), new Vector3(0.14f, 0.22f, 0.50f), mats.AccentBase, false);
            Object.DestroyImmediate(pointer.GetComponent<Collider>());

            BoxCollider knobCol = knobGo.AddComponent<BoxCollider>();
            knobCol.center = new Vector3(0f, 0.028f, 0f);
            knobCol.size = new Vector3(0.12f, 0.056f, 0.12f);

            Rigidbody knobRb = knobGo.AddComponent<Rigidbody>();
            knobRb.isKinematic = true;
            knobRb.useGravity = false;

            XRSimpleInteractable xr = knobGo.AddComponent<XRSimpleInteractable>();
            xr.colliders.Add(knobCol);
            AddInteractionFeedback(knobGo, xr, new[] { pointer.GetComponent<Renderer>(), dialBase.GetComponent<Renderer>() },
                stationAudio, "Grip-Drag Knob (0-500 K)", new Vector3(0f, 0.12f, 0f), mats, mainCam);

            RotaryKnobControl knob = knobGo.AddComponent<RotaryKnobControl>();
            SerializedObject kSo = new SerializedObject(knob);
            LabTextStyleHelper.SetObjRef(kSo, "interactable", xr);
            LabTextStyleHelper.SetObjRef(kSo, "dialVisual", dialBase.transform);
            LabTextStyleHelper.SetObjRef(kSo, "orientationReference", dialBasis.transform);
            LabTextStyleHelper.SetObjRef(kSo, "audioSource", stationAudio);
            LabTextStyleHelper.SetFloat(kSo, "minValue", 0f);
            LabTextStyleHelper.SetFloat(kSo, "maxValue", 500f);
            LabTextStyleHelper.SetFloat(kSo, "initialValue", 0f);
            kSo.ApplyModifiedPropertiesWithoutUndo();

            tempDownBtn = BuildLabeledButton(deck.transform, "TempDownButton",
                new Vector3(0.12f, 0.04f, 0.155f), new Vector3(0.09f, 0.036f, 0.07f),
                mats.ButtonAmber, "-50 K", "Decrease Temp 50 K", mats, stationAudio, mainCam, out _, 0.14f);

            tempUpBtn = BuildLabeledButton(deck.transform, "TempUpButton",
                new Vector3(-0.12f, 0.04f, 0.155f), new Vector3(0.09f, 0.036f, 0.07f),
                mats.ButtonGreen, "+50 K", "Increase Temp 50 K", mats, stationAudio, mainCam, out _, 0.14f);

            return knob;
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

        private static void BuildConsolePosts(Transform parent, Station1MatSet mats)
        {
            CreateBox("ConsolePost_L1", parent, new Vector3(-0.80f, 1.15f, -0.36f), new Vector3(0.04f, 0.50f, 0.04f), mats.TableFrame, true);
            CreateBox("ConsolePost_L2", parent, new Vector3(-0.24f, 1.15f, -0.36f), new Vector3(0.04f, 0.50f, 0.04f), mats.TableFrame, true);
            CreateBox("ConsolePost_R1", parent, new Vector3(0.24f, 1.15f, -0.36f), new Vector3(0.04f, 0.50f, 0.04f), mats.TableFrame, true);
            CreateBox("ConsolePost_R2", parent, new Vector3(0.80f, 1.15f, -0.36f), new Vector3(0.04f, 0.50f, 0.04f), mats.TableFrame, true);
        }

        private static void BuildLeftMetersAndGroupIVConsole(
            Transform parent,
            Station1MatSet mats,
            Camera mainCam,
            out TMP_Text materialHeaderText,
            out TMP_Text tempText,
            out TMP_Text carrierText,
            out TMP_Text gapText,
            out TMP_Text sigmaText,
            out TMP_Text latticeSummaryText,
            out TMP_Text groupIvText)
        {
            GameObject panel = new GameObject("LeftMetersAndGroupIVConsole");
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = new Vector3(0.49f, 1.66f, -0.35f);
            panel.transform.localRotation = Quaternion.Euler(-8f, -10f, 0f);

            CreateBox("Frame", panel.transform, Vector3.zero, new Vector3(0.96f, 0.68f, 0.035f), mats.TableFrame, true);
            CreateBox("Backing", panel.transform, new Vector3(0f, 0f, 0.014f), new Vector3(0.92f, 0.64f, 0.018f), mats.ScreenPanel, false);

            Canvas canvas = LabTextStyleHelper.CreateWorldCanvas("MetersCanvas", panel.transform,
                new Vector3(0f, 0f, 0.026f), new Vector3(0f, 180f, 0f),
                new Vector2(0.90f, 0.62f), 900, 620, mainCam, false);

            materialHeaderText = LabTextStyleHelper.CreateTMPText(canvas.transform, "MaterialHeader", "SILICON (Si)  |  GROUP IV INTRINSIC", 30f,
                new Vector2(0f, 268f), new Vector2(850f, 44f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            tempText = LabTextStyleHelper.CreateTMPText(canvas.transform, "TempMeter", "Temperature (T): 0 K", 24f,
                new Vector2(0f, 210f), new Vector2(840f, 40f), Color.white, TextAlignmentOptions.Left, FontStyles.Normal);

            carrierText = LabTextStyleHelper.CreateTMPText(canvas.transform, "CarrierMeter", "Carriers (n = p = ni): 0", 24f,
                new Vector2(0f, 162f), new Vector2(840f, 40f), new Color(0.50f, 0.85f, 1.0f), TextAlignmentOptions.Left, FontStyles.Normal);

            gapText = LabTextStyleHelper.CreateTMPText(canvas.transform, "BandGapMeter", "Bandgap (Eg): 1.12 eV", 24f,
                new Vector2(0f, 114f), new Vector2(840f, 40f), Color.white, TextAlignmentOptions.Left, FontStyles.Normal);

            sigmaText = LabTextStyleHelper.CreateTMPText(canvas.transform, "ConductivityMeter", "Conductivity (sigma): 0 S/cm", 24f,
                new Vector2(0f, 66f), new Vector2(840f, 40f), new Color(0.55f, 0.98f, 0.72f), TextAlignmentOptions.Left, FontStyles.Normal);

            latticeSummaryText = LabTextStyleHelper.CreateTMPText(canvas.transform, "LatticeSummary", "Lattice summary...", 22f,
                new Vector2(0f, 16f), new Vector2(840f, 42f), new Color(0.95f, 0.90f, 0.65f), TextAlignmentOptions.Left, FontStyles.Normal);

            // Periodic Table Group IV highlight chart block in lower third.
            GameObject sep = new GameObject("Divider");
            sep.transform.SetParent(canvas.transform, false);
            RectTransform sepRt = sep.AddComponent<RectTransform>();
            sepRt.anchoredPosition = new Vector2(0f, -22f);
            sepRt.sizeDelta = new Vector2(840f, 4f);
            Image sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(0.24f, 0.72f, 0.92f, 0.8f);

            groupIvText = LabTextStyleHelper.CreateTMPText(canvas.transform, "GroupIVChart", "PERIODIC TABLE - GROUP IV", 22f,
                new Vector2(0f, -155f), new Vector2(840f, 235f), Color.white, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        }

        private static EnergyBandDiagramView BuildRightEnergyBandConsole(Transform parent, Station1MatSet mats, Camera mainCam)
        {
            GameObject panel = new GameObject("RightEnergyBandConsole");
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = new Vector3(-0.49f, 1.66f, -0.35f);
            panel.transform.localRotation = Quaternion.Euler(-8f, 10f, 0f);

            CreateBox("Frame", panel.transform, Vector3.zero, new Vector3(0.86f, 0.68f, 0.035f), mats.TableFrame, true);
            CreateBox("Backing", panel.transform, new Vector3(0f, 0f, 0.014f), new Vector3(0.82f, 0.64f, 0.018f), mats.ScreenPanel, false);

            Canvas canvas = LabTextStyleHelper.CreateWorldCanvas("BandDiagramCanvas", panel.transform,
                new Vector3(0f, 0f, 0.026f), new Vector3(0f, 180f, 0f),
                new Vector2(0.80f, 0.62f), 800, 620, mainCam, false);

            LabTextStyleHelper.CreateTMPText(canvas.transform, "Header", "ENERGY BAND DIAGRAM (Not to scale)", 28f,
                new Vector2(0f, 270f), new Vector2(760f, 44f), new Color(0.28f, 0.90f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            // Band area container centered at (0, 12).
            GameObject plotArea = new GameObject("BandPlotArea");
            plotArea.transform.SetParent(canvas.transform, false);
            RectTransform plotRt = plotArea.AddComponent<RectTransform>();
            plotRt.anchoredPosition = new Vector2(0f, 12f);
            plotRt.sizeDelta = new Vector2(700f, 340f);

            RectTransform gapRect = CreateUIImageRect(plotArea.transform, "GapRegion", new Vector2(0f, 0f), new Vector2(660f, 118f), new Color(0.04f, 0.06f, 0.10f, 0.94f));
            RectTransform ecRect = CreateUIImageRect(plotArea.transform, "ConductionBand", new Vector2(0f, 100f), new Vector2(660f, 80f), new Color(0.08f, 0.22f, 0.38f, 0.92f));
            RectTransform evRect = CreateUIImageRect(plotArea.transform, "ValenceBand", new Vector2(0f, -100f), new Vector2(660f, 80f), new Color(0.28f, 0.11f, 0.15f, 0.92f));
            RectTransform fermiRect = CreateUIImageRect(plotArea.transform, "IntrinsicFermiEi", new Vector2(0f, 0f), new Vector2(660f, 4f), new Color(0.32f, 0.95f, 1.0f, 0.85f));

            TMP_Text ecLbl = LabTextStyleHelper.CreateTMPText(plotArea.transform, "EcLabel", "Ec Conduction Band", 23f,
                new Vector2(-165f, 80f), new Vector2(300f, 34f), Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            TMP_Text evLbl = LabTextStyleHelper.CreateTMPText(plotArea.transform, "EvLabel", "Ev Valence Band", 23f,
                new Vector2(-165f, -80f), new Vector2(300f, 34f), Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            TMP_Text egLbl = LabTextStyleHelper.CreateTMPText(plotArea.transform, "EgLabel", "Eg = 1.12 eV", 24f,
                new Vector2(-165f, 18f), new Vector2(300f, 36f), new Color(1.0f, 0.84f, 0.38f), TextAlignmentOptions.Left, FontStyles.Bold);
            TMP_Text eiLbl = LabTextStyleHelper.CreateTMPText(plotArea.transform, "EiLabel", "Ei (Midgap Fermi)", 20f,
                new Vector2(-165f, -18f), new Vector2(300f, 30f), new Color(0.45f, 0.94f, 1.0f), TextAlignmentOptions.Left, FontStyles.Normal);

            // Pooled UI dots for animated carrier excitation across Eg.
            int dotCount = 10;
            RectTransform[] eDots = new RectTransform[dotCount];
            RectTransform[] hDots = new RectTransform[dotCount];
            for (int i = 0; i < dotCount; i++)
            {
                eDots[i] = CreateUIImageRect(plotArea.transform, "EcDot_" + i, Vector2.zero, new Vector2(18f, 18f), new Color(0.24f, 0.72f, 1.0f, 1f));
                eDots[i].gameObject.SetActive(false);
                hDots[i] = CreateUIImageRect(plotArea.transform, "EvHole_" + i, Vector2.zero, new Vector2(18f, 18f), new Color(1.0f, 0.34f, 0.38f, 1f));
                hDots[i].gameObject.SetActive(false);
            }

            RectTransform[] arrows = new RectTransform[5];
            for (int i = 0; i < arrows.Length; i++)
            {
                arrows[i] = CreateUIImageRect(plotArea.transform, "ExcitePulse_" + i, Vector2.zero, new Vector2(8f, 22f), new Color(1.0f, 0.88f, 0.35f, 0.92f));
                arrows[i].gameObject.SetActive(false);
            }

            TMP_Text regimeLbl = LabTextStyleHelper.CreateTMPText(canvas.transform, "RegimeStatus", "0 K: Frozen Insulator", 23f,
                new Vector2(0f, -242f), new Vector2(760f, 48f), new Color(0.92f, 0.96f, 1.0f), TextAlignmentOptions.Center, FontStyles.Bold);

            EnergyBandDiagramView view = panel.AddComponent<EnergyBandDiagramView>();
            SerializedObject bSo = new SerializedObject(view);
            LabTextStyleHelper.SetObjRef(bSo, "conductionBandRect", ecRect);
            LabTextStyleHelper.SetObjRef(bSo, "valenceBandRect", evRect);
            LabTextStyleHelper.SetObjRef(bSo, "fermiLineRect", fermiRect);
            LabTextStyleHelper.SetObjRef(bSo, "gapRegionRect", gapRect);
            LabTextStyleHelper.SetObjRef(bSo, "ecLabel", ecLbl);
            LabTextStyleHelper.SetObjRef(bSo, "evLabel", evLbl);
            LabTextStyleHelper.SetObjRef(bSo, "egLabel", egLbl);
            LabTextStyleHelper.SetObjRef(bSo, "eiLabel", eiLbl);
            LabTextStyleHelper.SetObjRef(bSo, "regimeLabel", regimeLbl);
            LabTextStyleHelper.SetObjectArray(bSo, "conductionElectrons", eDots);
            LabTextStyleHelper.SetObjectArray(bSo, "valenceHoles", hDots);
            LabTextStyleHelper.SetObjectArray(bSo, "transitionArrows", arrows);
            bSo.ApplyModifiedPropertiesWithoutUndo();

            return view;
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

        private static void WireStation1LessonsAndScene(IntrinsicSiliconStationController st1Controller)
        {
            LessonManager lessonManager = Object.FindAnyObjectByType<LessonManager>();
            if (lessonManager == null)
            {
                return;
            }

            // Build Station 1 DialogueLine ScriptableObjects.
            DialogueLine dlWhySilicon = CreateDialogue("DL_St1_WhySi", "st1_why_si",
                "Silicon (Group IV) has 4 valence electrons that form a strong diamond covalent crystal with a 1.12 eV bandgap — ideal for controlling conductivity by heat or doping!",
                NPCEmotion.Explaining);

            DialogueLine dlTeleportPrompt = CreateDialogue("DL_St1_Teleport", "st1_teleport",
                "Walk or teleport onto Anchor Pad 1 in front of the Intrinsic Silicon workbench.",
                NPCEmotion.Encouraging);

            dlWhySilicon.Configure("st1_why_si", dlWhySilicon.Text, NPCEmotion.Explaining, dlTeleportPrompt);
            EditorUtility.SetDirty(dlWhySilicon);

            DialogueChoice chWhy = new DialogueChoice();
            chWhy.Configure("Why Silicon?", dlWhySilicon);
            DialogueChoice chGo = new DialogueChoice();
            chGo.Configure("Let's Inspect!", dlTeleportPrompt);

            DialogueLine dlSt1Intro = CreateDialogue("DL_Station1_Intro", "st1_intro",
                "Welcome to Station 1: Intrinsic Silicon! Notice how Group IV elements C, Si, and Ge all share 4 valence electrons. Teleport onto Pad 1 to begin.",
                NPCEmotion.Happy, null, new[] { chWhy, chGo });

            DialogueLine dlTeleportH20 = CreateDialogue("DL_St1_Teleport_H20", "st1_tp_h20",
                "Hint: Aim your controller ray at the glowing pad labeled 1 in front of the left bench and release Teleport (I).",
                NPCEmotion.Thinking);
            DialogueLine dlTeleportH45 = CreateDialogue("DL_St1_Teleport_H45", "st1_tp_h45",
                "Right where I am pointing — aim your teleport ray onto Station 1's anchor pad!",
                NPCEmotion.Pointing);
            DialogueLine dlTeleportDone = CreateDialogue("DL_St1_Teleport_Done", "st1_tp_done",
                "Great! At 0 K every valence electron is locked in its covalent bond, so n = p = 0 and the crystal acts as an insulator.",
                NPCEmotion.Happy);

            DialogueLine dlLensIntro = CreateDialogue("DL_St1_Lens_Intro", "st1_lens_intro",
                "Step 2: Click or select the Optical Microscope on the left of the bench (or press LOOK IN MICROSCOPE) to view the silicon crystal through its eyepiece!",
                NPCEmotion.Explaining);
            DialogueLine dlLensH20 = CreateDialogue("DL_St1_Lens_H20", "st1_lens_h20",
                "Hint: Aim at the glowing cyan Eyepiece on the Microscope or the green LOOK IN MICROSCOPE button and press Trigger (T) or Left Click.",
                NPCEmotion.Thinking);
            DialogueLine dlLensH45 = CreateDialogue("DL_St1_Lens_H45", "st1_lens_h45",
                "Interact with the Microscope I am pointing at to look inside the one-eyed eyepiece and watch covalent bonds and electrons!",
                NPCEmotion.Pointing);
            DialogueLine dlLensDone = CreateDialogue("DL_St1_Lens_Done", "st1_lens_done",
                "You are peering inside the crystal lattice (Not to scale)! Ray-hover atoms or bonds, heat with +50 K inside the view, and click EXIT MICROSCOPE whenever ready.",
                NPCEmotion.Happy);

            DialogueLine dlHeatIntro = CreateDialogue("DL_St1_Heat_Intro", "st1_heat_intro",
                "Step 3: Raise Temperature to 250 K+ (using the +50 K button inside the Microscope or on the workbench) to break covalent bonds and observe moving e- and h+ pairs.",
                NPCEmotion.Explaining);
            DialogueLine dlHeatH20 = CreateDialogue("DL_St1_Heat_H20", "st1_heat_h20",
                "Hint: Press +50 K (inside the Microscope or on the desk) or slide Temperature above 250 K to watch crystal vibration and moving carriers.",
                NPCEmotion.Thinking);
            DialogueLine dlHeatH45 = CreateDialogue("DL_St1_Heat_H45", "st1_heat_h45",
                "Raise Temperature at the Temperature Deck I am pointing to — look through the Microscope or Thermal Cube to see blue e- and red h+ moving!",
                NPCEmotion.Pointing);
            DialogueLine dlHeatDone = CreateDialogue("DL_St1_Heat_Done", "st1_heat_done",
                "Awesome! Heating frees equal electrons and holes (n = p = ni, ~1.5e10 cm^-3 at 300 K in Si), raising conductivity sigma as electrons cross the 1.12 eV gap.",
                NPCEmotion.Happy);

            DialogueLine dlGeIntro = CreateDialogue("DL_St1_Ge_Intro", "st1_ge_intro",
                "Step 4: Press the blue Material Toggle (MAT: Si -> Ge) on the bench or inside the Microscope to compare Germanium's narrower 0.67 eV bandgap!",
                NPCEmotion.Explaining);
            DialogueLine dlGeH20 = CreateDialogue("DL_St1_Ge_H20", "st1_ge_h20",
                "Hint: Aim or click the blue MAT: Si -> Ge button on the bench or Microscope bezel.",
                NPCEmotion.Thinking);
            DialogueLine dlGeH45 = CreateDialogue("DL_St1_Ge_H45", "st1_ge_h45",
                "Press the blue Material Toggle button I am pointing at to test Germanium!",
                NPCEmotion.Pointing);
            DialogueLine dlGeDone = CreateDialogue("DL_St1_Ge_Done", "st1_ge_done",
                "Station 1 complete! Ge's narrower 0.67 eV gap lets thermal energy break far more bonds (ni ~ 2.4e13 cm^-3 at 300 K) than Si. Station 2 (Doping) awaits in Phase 4!",
                NPCEmotion.Happy);

            DialogueLine dlSt2Ready = CreateDialogue("DL_Station2_Intro", "st2_intro",
                "Station 2: N-Type Doping is now unlocked! Station 2 apparatus will be built in Phase 4.",
                NPCEmotion.Explaining);

            // Preserve Steps 01..05 (Tutorial Pad) + build real Station 1 steps (06..09) + Station 2 placeholder (10).
            List<LessonStep> updatedSteps = new List<LessonStep>();
            string[] tutFiles =
            {
                "Step_01_Teleport",
                "Step_02_Grab",
                "Step_03_Socket",
                "Step_04_Button",
                "Step_05_Screen"
            };
            for (int i = 0; i < tutFiles.Length; i++)
            {
                LessonStep tutStep = AssetDatabase.LoadAssetAtPath<LessonStep>(LessonFolder + "/" + tutFiles[i] + ".asset");
                if (tutStep != null)
                {
                    updatedSteps.Add(tutStep);
                }
            }

            updatedSteps.Add(CreateStep("Step_06_St1_Teleport", "s1_teleport", "1", "Teleport to Station 1",
                "Teleport onto Anchor Pad 1 in front of Intrinsic Silicon.", "station1.teleport",
                dlSt1Intro, dlTeleportDone, dlTeleportH20, dlTeleportH45, 0));

            updatedSteps.Add(CreateStep("Step_07_St1_Lens", "s1_microscope", "1", "Inspect Wafer in Microscope",
                "Click or select the Microscope to inspect the atomic lattice through its eyepiece.", "station1.microscope_enter",
                dlLensIntro, dlLensDone, dlLensH20, dlLensH45, 1));

            updatedSteps.Add(CreateStep("Step_08_St1_Heat", "s1_heat", "1", "Heat Crystal (250+ K)",
                "Raise Temperature Knob (>= 250 K) to free e-/h+.", "station1.heat_carriers",
                dlHeatIntro, dlHeatDone, dlHeatH20, dlHeatH45, 2));

            updatedSteps.Add(CreateStep("Step_09_St1_ToggleGe", "s1_ge", "1", "Compare Si vs Ge",
                "Toggle Material to Germanium (Eg = 0.67 eV).", "station1.toggle_ge",
                dlGeIntro, dlGeDone, dlGeH20, dlGeH45, 3));

            updatedSteps.Add(CreateStep("Step_10_Station2_Ready", "s2_ready", "2", "N-Type Doping (Phase 4)",
                "Station 2 unlocked for Phase 4 build.", "station2.complete",
                dlSt2Ready, null, dlSt2Ready, dlSt2Ready, 0));

            // Rebuild StationController array on LessonManager in exact U-shape order.
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
            LabTextStyleHelper.SetObjectList(lmSo, "steps", updatedSteps);
            LabTextStyleHelper.SetObjectArray(lmSo, "stations", controllers.ToArray());
            lmSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lessonManager);
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

        private static void BuildWorkbench(Transform parent, float width, float depth, float height, Station1MatSet mats)
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

        private static StationBeacon BuildBeaconParts(Transform parent, string stationId, string title, Station1MatSet mats, Camera mainCam, out TeleportationAnchor anchor)
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
            GameObject floorTile = CreateBox("Tile", floorSign.transform,
                Vector3.zero, new Vector3(0.32f, 0.012f, 0.32f), mats.TableFrame, false);
            Object.DestroyImmediate(floorTile.GetComponent<Collider>());
            Canvas floorCanvas = LabTextStyleHelper.CreateWorldCanvas("FloorNumberUI", floorSign.transform,
                new Vector3(0f, 0.009f, 0f), new Vector3(90f, 0f, 0f), new Vector2(0.30f, 0.30f), 320, 320, mainCam, false);
            TMP_Text floorNumText = LabTextStyleHelper.CreateTMPText(floorCanvas.transform, "Number", stationId, 180f,
                Vector2.zero, new Vector2(300f, 300f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject holoRoot = new GameObject("HologramTitle");
            holoRoot.transform.SetParent(parent, false);
            holoRoot.transform.localPosition = new Vector3(0f, 2.28f, 0f);

            GameObject holoBacking = CreateBox("HoloBacking", holoRoot.transform,
                new Vector3(0f, 0f, -0.014f), new Vector3(1.68f, 0.36f, 0.014f), mats.ScreenPanel, false);
            Object.DestroyImmediate(holoBacking.GetComponent<Collider>());

            GameObject holoBar = CreateBox("HoloAccentBar", holoRoot.transform,
                new Vector3(0f, -0.195f, -0.010f), new Vector3(1.68f, 0.025f, 0.024f), mats.AccentBase, false);
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

        private static PhysicalPushButton BuildLabeledButton(
            Transform parent,
            string name,
            Vector3 localPos,
            Vector3 capSize,
            Material capMat,
            string plateLabel,
            string tooltipLabel,
            Station1MatSet mats,
            AudioSource audioSource,
            Camera mainCam,
            out TMP_Text plateText,
            float plateWidth = 0.22f)
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

            // World-space UI Trigger/Click relay flush above cap top so Trigger (T) & Left Click press seamlessly alongside Grip/Pinch/Poke.
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
            plateText.fontSizeMin = 16f;
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

        private static void AddInteractionFeedback(
            GameObject target,
            XRBaseInteractable interactable,
            Renderer[] renderers,
            AudioSource audioSource,
            string tooltipLabel,
            Vector3 tooltipLocalPos,
            Station1MatSet mats,
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

            GameObject tipBg = CreateBox("Bg", tipRoot.transform, Vector3.zero, new Vector3(0.36f, 0.075f, 0.008f), mats.ScreenPanel, false);
            Object.DestroyImmediate(tipBg.GetComponent<Collider>());

            Canvas tipCanvas = LabTextStyleHelper.CreateWorldCanvas("TipCanvas", tipRoot.transform,
                new Vector3(0f, 0f, 0.006f), new Vector3(0f, 180f, 0f),
                new Vector2(0.34f, 0.068f), 450, 90, mainCam, false);
            TMP_Text tipText = LabTextStyleHelper.CreateTMPText(tipCanvas.transform, "Label", tooltipLabel, 30f,
                Vector2.zero, new Vector2(420f, 80f), Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

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
