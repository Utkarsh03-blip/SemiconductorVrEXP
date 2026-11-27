using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Upgrades ONLY the non-colliding visual child meshes and URP Lit materials for:
    /// 1) Single faceted HDR-glowing crystal chunks inside all 16 dopant vials (Stations 2 & 3),
    /// 2) Realistic laboratory thermal diffusion furnaces (Stations 2 N-type & 3 P-type),
    /// 3) Precision semiconductor junction alignment & bonding rig + central chamber (Station 4).
    /// Preserves all StationController logic, XRSocketInteractor snap coordinates, AttachTransforms,
    /// Collider bounds, SemiconductorModel code, and uses zero new realtime Point Lights.
    /// </summary>
    public static class Phase7VialFurnaceRigUpgradeBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string TexFolder = "Assets/PNJunctionLab/Textures";
        private const string MatFolder = "Assets/PNJunctionLab/Materials/Hardware";
        private const string PrefixV2 = "HW2_";
        private const string PrefixV1 = "HW_";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private sealed class UpgradeAssets
        {
            public ProceduralHardwareMeshFactory.HardwareMeshSet Meshes;

            // Core hardware materials (reused cleanly from Hardware folder)
            public Material BrushedMetalDark;
            public Material BrushedMetalLight;
            public Material RecessDark;
            public Material RubberDark;
            public Material BrassMetal;
            public Material CopperBand;
            public Material NickelTip;
            public Material QuartzTubeGlass;
            public Material HeatingCoilGlow;
            public Material OpticalBreadboard;
            public Material LinearScalePlate;

            // Station accents (zero Point Lights — emissive URP Lit strips/LEDs)
            public Material AccentCyanSolid;
            public Material AccentCyanGlow;
            public Material AccentMagentaSolid;
            public Material AccentMagentaGlow;
            public Material AccentAmberGlow;
            public Material AccentRedGlow;
            public Material AccentTealSolid;
            public Material AccentBlueSolid;
            public Material LaserCrosshairCyan;

            // Dopant vial crystal materials (HDR emission, glossy translucent)
            public Material CrystalDonorCyan;
            public Material CrystalDonorElectricBlue;
            public Material CrystalAcceptorMagenta;
            public Material CrystalAcceptorRubyAmber;
            public Material CrystalControlSilverViolet;
        }

        [MenuItem("PNJunctionLab/Upgrade Vials, Furnaces & Junction Rig")]
        public static void ApplyVialFurnaceRigUpgradesMenu()
        {
            Debug.Log(ApplyVialFurnaceRigUpgrades());
        }

        /// <summary>
        /// Programmatic entry point callable via Unity MCP execute_code.
        /// Builds/updates persistent textures, meshes, materials, and upgrades Stations 2, 3, and 4 visuals.
        /// </summary>
        public static string ApplyVialFurnaceRigUpgrades()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.EndsWith("PNJunctionLab.unity", StringComparison.OrdinalIgnoreCase))
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            UpgradeAssets assets = BuildAndLoadAssets();
            LessonManager lm = Object.FindAnyObjectByType<LessonManager>();
            if (lm == null || lm.Stations == null)
            {
                return "Upgrade Error: LessonManager not found.";
            }

            int crystalsUpdated = 0;
            int furnacesUpgraded = 0;
            int rigsUpgraded = 0;

            for (int i = 0; i < lm.Stations.Count; i++)
            {
                StationController st = lm.Stations[i];
                if (st == null) continue;

                if (st.StationId == "2" || st.StationId == "3")
                {
                    crystalsUpdated += UpgradeDopantVialCrystals(st, assets);
                    if (UpgradeThermalDiffusionFurnace(st, assets))
                    {
                        furnacesUpgraded++;
                    }
                }
                else if (st.StationId == "4")
                {
                    if (UpgradeJunctionAlignmentRig(st, assets))
                    {
                        rigsUpgraded++;
                    }
                }
            }

            SyncStationPrefabs(lm);

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"VialFurnaceRigUpgrade Complete: VialCrystals={crystalsUpdated}, DiffusionFurnaces={furnacesUpgraded}, JunctionRigs={rigsUpgraded}.";
        }

        private static UpgradeAssets BuildAndLoadAssets()
        {
            Directory.CreateDirectory(TexFolder);
            Directory.CreateDirectory(MatFolder);

            UpgradeAssets a = new UpgradeAssets
            {
                Meshes = ProceduralHardwareMeshFactory.EnsureAllMeshes()
            };

            Texture2D metalAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/Tex_BrushedMetal_Albedo.png");
            Texture2D metalNrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/Tex_BrushedMetal_Normal.png");
            Texture2D ticksTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/Tex_LinearScaleTicks_Albedo.png");

            Texture2D breadAlb = SavePngTexture(ProceduralLabTextures.CreateOpticalBreadboardAlbedo(), $"{TexFolder}/Tex_OpticalBreadboard_Albedo.png", false, false);
            Texture2D breadNrm = SavePngTexture(ProceduralLabTextures.CreateOpticalBreadboardNormal(), $"{TexFolder}/Tex_OpticalBreadboard_Normal.png", true, false);

            a.BrushedMetalDark = LoadOrCreateMat("Mat_HW_BrushedMetalDark", new Color(0.30f, 0.34f, 0.41f, 1f), 0.80f, 0.68f, metalAlb, metalNrm, 0.48f, Color.black, false);
            a.BrushedMetalLight = LoadOrCreateMat("Mat_HW_BrushedMetalLight", new Color(0.84f, 0.88f, 0.93f, 1f), 0.88f, 0.78f, metalAlb, metalNrm, 0.36f, Color.black, false);
            a.RecessDark = LoadOrCreateMat("Mat_HW_RecessDark", new Color(0.06f, 0.07f, 0.10f, 1f), 0.18f, 0.32f, null, null, 0f, Color.black, false);
            a.RubberDark = LoadOrCreateMat("Mat_HW_RubberDark", new Color(0.11f, 0.12f, 0.15f, 1f), 0.02f, 0.22f, null, null, 0f, Color.black, false);
            a.BrassMetal = LoadOrCreateMat("Mat_HW_BrassMetal", new Color(0.90f, 0.70f, 0.28f, 1f), 0.92f, 0.82f, metalAlb, metalNrm, 0.30f, Color.black, false);
            a.CopperBand = LoadOrCreateMat("Mat_HW_CopperBand", new Color(0.86f, 0.52f, 0.24f, 1f), 0.90f, 0.80f, metalAlb, metalNrm, 0.25f, Color.black, false);
            a.NickelTip = LoadOrCreateMat("Mat_HW_NickelTip", new Color(0.88f, 0.91f, 0.96f, 1f), 0.94f, 0.90f, metalAlb, null, 0f, Color.black, false);

            a.QuartzTubeGlass = LoadOrCreateMat("Mat_HW_QuartzTube",
                new Color(0.86f, 0.96f, 1.0f, 0.28f), 0.06f, 0.96f, null, null, 0f, new Color(0.03f, 0.07f, 0.10f, 1f), true);
            a.HeatingCoilGlow = LoadOrCreateMat("Mat_HW_HeatingCoil",
                new Color(0.92f, 0.42f, 0.18f, 1f), 0.78f, 0.72f, null, null, 0f, new Color(1.25f, 0.30f, 0.05f, 1f), false);

            a.OpticalBreadboard = LoadOrCreateMat("Mat_HW_OpticalBreadboard",
                Color.white, 0.68f, 0.62f, breadAlb, breadNrm, 0.55f, Color.black, false);
            a.LinearScalePlate = LoadOrCreateMat("Mat_HW_LinearScalePlate",
                new Color(0.88f, 0.95f, 1.0f, 0.95f), 0.15f, 0.45f, ticksTex, null, 0f, new Color(0.18f, 0.24f, 0.32f, 1f), true);

            a.AccentCyanSolid = LoadOrCreateMat("Mat_HW_AccentCyanSolid",
                new Color(0.14f, 0.74f, 0.96f, 1f), 0.45f, 0.72f, null, null, 0f, new Color(0.06f, 0.38f, 0.56f, 1f), false);
            a.AccentCyanGlow = LoadOrCreateMat("Mat_HW_AccentCyanGlow",
                new Color(0.24f, 0.90f, 1.0f, 1f), 0.10f, 0.80f, null, null, 0f, new Color(0.20f, 1.15f, 1.50f, 1f), false);
            a.AccentMagentaSolid = LoadOrCreateMat("Mat_HW_AccentMagentaSolid",
                new Color(0.88f, 0.22f, 0.52f, 1f), 0.45f, 0.72f, null, null, 0f, new Color(0.45f, 0.08f, 0.24f, 1f), false);
            a.AccentMagentaGlow = LoadOrCreateMat("Mat_HW_AccentMagentaGlow",
                new Color(0.98f, 0.34f, 0.58f, 1f), 0.10f, 0.80f, null, null, 0f, new Color(1.30f, 0.24f, 0.68f, 1f), false);
            a.AccentAmberGlow = LoadOrCreateMat("Mat_HW_AccentAmberGlow",
                new Color(1.0f, 0.64f, 0.14f, 1f), 0.10f, 0.80f, null, null, 0f, new Color(1.25f, 0.58f, 0.08f, 1f), false);
            a.AccentRedGlow = LoadOrCreateMat("Mat_HW_AccentRedGlow",
                new Color(0.95f, 0.22f, 0.24f, 1f), 0.15f, 0.78f, null, null, 0f, new Color(1.10f, 0.16f, 0.18f, 1f), false);
            a.AccentTealSolid = LoadOrCreateMat("Mat_HW_AccentTealSolid",
                new Color(0.14f, 0.78f, 0.76f, 1f), 0.42f, 0.72f, null, null, 0f, new Color(0.05f, 0.34f, 0.34f, 1f), false);
            a.AccentBlueSolid = LoadOrCreateMat("Mat_HW_AccentBlueSolid",
                new Color(0.18f, 0.54f, 0.96f, 1f), 0.42f, 0.72f, null, null, 0f, new Color(0.06f, 0.24f, 0.54f, 1f), false);
            a.LaserCrosshairCyan = LoadOrCreateMat("Mat_HW_LaserCrosshair",
                new Color(0.28f, 0.96f, 1.0f, 0.88f), 0.0f, 0.90f, null, null, 0f, new Color(0.22f, 1.35f, 1.65f, 1f), true);

            // Translucent HDR-glowing single crystal chunk materials
            a.CrystalDonorCyan = LoadOrCreateMat("Mat_HW_CrystalDonorCyan",
                new Color(0.16f, 0.88f, 1.0f, 0.84f), 0.16f, 0.93f, null, null, 0f, new Color(0.16f, 1.25f, 1.75f, 1f), true);
            a.CrystalDonorElectricBlue = LoadOrCreateMat("Mat_HW_CrystalDonorBlue",
                new Color(0.20f, 0.62f, 1.0f, 0.84f), 0.16f, 0.93f, null, null, 0f, new Color(0.18f, 0.80f, 1.80f, 1f), true);
            a.CrystalAcceptorMagenta = LoadOrCreateMat("Mat_HW_CrystalAcceptorMagenta",
                new Color(0.98f, 0.20f, 0.62f, 0.84f), 0.16f, 0.93f, null, null, 0f, new Color(1.55f, 0.18f, 0.88f, 1f), true);
            a.CrystalAcceptorRubyAmber = LoadOrCreateMat("Mat_HW_CrystalAcceptorRuby",
                new Color(1.0f, 0.34f, 0.26f, 0.84f), 0.16f, 0.93f, null, null, 0f, new Color(1.65f, 0.36f, 0.22f, 1f), true);
            a.CrystalControlSilverViolet = LoadOrCreateMat("Mat_HW_CrystalControlViolet",
                new Color(0.76f, 0.70f, 0.95f, 0.84f), 0.22f, 0.92f, null, null, 0f, new Color(0.52f, 0.42f, 0.92f, 1f), true);

            AssetDatabase.SaveAssets();
            return a;
        }

        private static int UpgradeDopantVialCrystals(StationController st, UpgradeAssets assets)
        {
            int count = 0;
            DopantVialItem[] vials = st.GetComponentsInChildren<DopantVialItem>(true);
            for (int i = 0; i < vials.Length; i++)
            {
                DopantVialItem vial = vials[i];
                if (vial == null) continue;

                // Remove old granular powder/pellet fill & any previous crystal chunks
                for (int c = vial.transform.childCount - 1; c >= 0; c--)
                {
                    Transform ch = vial.transform.GetChild(c);
                    if (ch.name.StartsWith($"{PrefixV1}Dopant", StringComparison.Ordinal)
                        || ch.name.StartsWith(PrefixV2, StringComparison.Ordinal))
                    {
                        Object.DestroyImmediate(ch.gameObject);
                    }
                }

                Material crystalMat = ResolveCrystalMaterial(vial.ElementSymbol, vial.ValenceGroup, assets);

                // Single faceted crystalline chunk assembly resting cleanly on HW_GlassBase (y = -0.039) inside Bottle
                GameObject chunkRoot = new GameObject($"{PrefixV2}CrystalChunk");
                chunkRoot.transform.SetParent(vial.transform, false);
                chunkRoot.transform.localPosition = new Vector3(0f, -0.039f, 0f);
                chunkRoot.transform.localRotation = Quaternion.identity;

                CreateCustomMesh(chunkRoot.transform, $"{PrefixV2}Shard_Core", assets.Meshes.CrystalShard,
                    new Vector3(0.000f, 0.000f, 0.000f), new Vector3(6f, 18f, -5f), new Vector3(0.018f, 0.033f, 0.018f), crystalMat);
                CreateCustomMesh(chunkRoot.transform, $"{PrefixV2}Shard_SideA", assets.Meshes.CrystalShard,
                    new Vector3(-0.004f, 0.000f, 0.003f), new Vector3(-16f, 95f, 14f), new Vector3(0.014f, 0.025f, 0.014f), crystalMat);
                CreateCustomMesh(chunkRoot.transform, $"{PrefixV2}Shard_SideB", assets.Meshes.CrystalShard,
                    new Vector3(0.004f, 0.000f, -0.003f), new Vector3(15f, -110f, -12f), new Vector3(0.013f, 0.022f, 0.013f), crystalMat);
                CreateCustomMesh(chunkRoot.transform, $"{PrefixV2}Shard_BaseCluster", assets.Meshes.RoundedBlock,
                    new Vector3(0.000f, 0.006f, 0.000f), new Vector3(4f, 38f, -6f), new Vector3(0.024f, 0.012f, 0.024f), crystalMat);

                EditorUtility.SetDirty(vial.gameObject);
                count++;
            }

            return count;
        }

        private static bool UpgradeThermalDiffusionFurnace(StationController st, UpgradeAssets assets)
        {
            Transform furn = st.transform.Find("DiffusionFurnace");
            if (furn == null) return false;

            bool isNType = st.StationId == "2";
            Material accentSolid = isNType ? assets.AccentCyanSolid : assets.AccentMagentaSolid;
            Material accentGlow = isNType ? assets.AccentCyanGlow : assets.AccentAmberGlow;
            string stationTag = isNType ? "S2 · N-TYPE" : "S3 · P-TYPE";

            StripPrefixedChildren(furn, PrefixV2);

            Transform deck = furn.Find("FurnaceDeck");
            if (deck != null)
            {
                SwapMeshAndMat(deck, assets.Meshes.ChamferedBezel, assets.BrushedMetalDark);
            }

            // 1. 4 Rubberized corner mounting feet hugging bottom corners cleanly
            Vector3[] footOffsets =
            {
                new Vector3(-0.258f, 0.004f, -0.132f),
                new Vector3(+0.258f, 0.004f, -0.132f),
                new Vector3(-0.258f, 0.004f, +0.132f),
                new Vector3(+0.258f, 0.004f, +0.132f)
            };
            for (int f = 0; f < footOffsets.Length; f++)
            {
                CreatePrimitive(furn, $"{PrefixV2}RubberFoot_{f}", PrimitiveType.Cylinder,
                    footOffsets[f], Vector3.zero, new Vector3(0.036f, 0.004f, 0.036f), assets.RubberDark);
            }

            // 2. Furnace superstructure situated cleanly on the rear chamber zone (Z <= -0.068),
            // leaving WaferSocketBay (+0.17, 0.03, +0.03), DopantSocketBay (-0.17, 0.03, +0.03),
            // and their +/-0.097m SocketRings/Bezel collars 100% open & unobstructed!
            const float tubeY = 0.116f;
            const float tubeZ = -0.108f;

            // Lower hearth saddle flush inside FurnaceDeck ([-0.150 .. -0.066], clear of -0.055m SocketRings)
            CreateCustomMesh(furn, $"{PrefixV2}HearthBase", assets.Meshes.ChamferedBezel,
                new Vector3(0f, 0.044f, tubeZ), Vector3.zero, new Vector3(0.510f, 0.028f, 0.084f), assets.BrushedMetalLight);

            // Rear anodized wall & insulated side clamshell housings + louver vents + end-caps
            CreateCustomMesh(furn, $"{PrefixV2}ChassisBackWall", assets.Meshes.ChamferedBezel,
                new Vector3(0f, 0.124f, -0.142f), Vector3.zero, new Vector3(0.490f, 0.148f, 0.018f), assets.BrushedMetalDark);
            CreateCustomMesh(furn, $"{PrefixV2}SideHousing_L", assets.Meshes.ChamferedBezel,
                new Vector3(0.222f, 0.120f, tubeZ), Vector3.zero, new Vector3(0.068f, 0.140f, 0.090f), assets.BrushedMetalDark);
            CreateCustomMesh(furn, $"{PrefixV2}SideHousing_R", assets.Meshes.ChamferedBezel,
                new Vector3(-0.222f, 0.120f, tubeZ), Vector3.zero, new Vector3(0.068f, 0.140f, 0.090f), assets.BrushedMetalDark);

            // Side ventilation louver slats
            for (int side = -1; side <= 1; side += 2)
            {
                float sx = side * 0.257f;
                for (int l = 0; l < 4; l++)
                {
                    float ly = 0.080f + l * 0.024f;
                    CreatePrimitive(furn, $"{PrefixV2}VentLouver_{(side > 0 ? "L" : "R")}_{l}", PrimitiveType.Cube,
                        new Vector3(sx, ly, tubeZ), Vector3.zero, new Vector3(0.006f, 0.008f, 0.064f), assets.RecessDark);
                }
            }

            // Brushed steel upper hood arch + front station-colored accent trim strip + 6 aluminum heat-sink fins
            CreateCustomMesh(furn, $"{PrefixV2}UpperClamshellHood", assets.Meshes.ChamferedBezel,
                new Vector3(0f, 0.188f, tubeZ - 0.006f), Vector3.zero, new Vector3(0.506f, 0.026f, 0.104f), assets.BrushedMetalLight);
            CreatePrimitive(furn, $"{PrefixV2}StationAccentStrip", PrimitiveType.Cube,
                new Vector3(0f, 0.188f, tubeZ + 0.045f), Vector3.zero, new Vector3(0.440f, 0.010f, 0.006f), accentSolid);

            for (int fin = 0; fin < 6; fin++)
            {
                float fx = Mathf.Lerp(-0.175f, 0.175f, fin / 5f);
                CreatePrimitive(furn, $"{PrefixV2}HeatSinkFin_{fin}", PrimitiveType.Cube,
                    new Vector3(fx, 0.211f, tubeZ - 0.008f), Vector3.zero, new Vector3(0.012f, 0.022f, 0.084f), assets.BrushedMetalLight);
            }

            // Insulated cylindrical end-cap flanges cradling the quartz tube
            CreatePrimitive(furn, $"{PrefixV2}InsulatedEndCap_L", PrimitiveType.Cylinder,
                new Vector3(0.180f, tubeY, tubeZ), new Vector3(0f, 0f, 90f), new Vector3(0.098f, 0.018f, 0.098f), assets.BrushedMetalLight);
            CreatePrimitive(furn, $"{PrefixV2}InsulatedEndCap_R", PrimitiveType.Cylinder,
                new Vector3(-0.180f, tubeY, tubeZ), new Vector3(0f, 0f, 90f), new Vector3(0.098f, 0.018f, 0.098f), assets.BrushedMetalLight);
            CreatePrimitive(furn, $"{PrefixV2}EndRingAccent_L", PrimitiveType.Cylinder,
                new Vector3(0.160f, tubeY, tubeZ), new Vector3(0f, 0f, 90f), new Vector3(0.092f, 0.004f, 0.092f), accentSolid);
            CreatePrimitive(furn, $"{PrefixV2}EndRingAccent_R", PrimitiveType.Cylinder,
                new Vector3(-0.160f, tubeY, tubeZ), new Vector3(0f, 0f, 90f), new Vector3(0.092f, 0.004f, 0.092f), accentSolid);

            // 3. Central translucent quartz process tube + FurnaceCoreChamber (preserve furnaceCoreRenderer!) + 6 heating coils
            Transform coreChamber = furn.Find("FurnaceCoreChamber");
            if (coreChamber != null)
            {
                coreChamber.localPosition = new Vector3(0f, tubeY, tubeZ);
                coreChamber.localEulerAngles = new Vector3(0f, 0f, 90f);
                coreChamber.localScale = new Vector3(0.028f, 0.145f, 0.028f);
                Mesh cyl = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
                if (cyl != null)
                {
                    MeshFilter mf = coreChamber.GetComponent<MeshFilter>();
                    if (mf != null) mf.sharedMesh = cyl;
                }
                EditorUtility.SetDirty(coreChamber.gameObject);
            }

            CreatePrimitive(furn, $"{PrefixV2}QuartzProcessTube", PrimitiveType.Cylinder,
                new Vector3(0f, tubeY, tubeZ), new Vector3(0f, 0f, 90f), new Vector3(0.068f, 0.175f, 0.068f), assets.QuartzTubeGlass);

            for (int c = 0; c < 6; c++)
            {
                float cx = Mathf.Lerp(-0.125f, 0.125f, c / 5f);
                CreateCustomMesh(furn, $"{PrefixV2}HeatingCoil_{c}", assets.Meshes.CoilRing,
                    new Vector3(cx, tubeY, tubeZ), Vector3.zero, new Vector3(0.095f, 0.084f, 0.084f), assets.HeatingCoilGlow);
            }

            // 4. Stainless carrier-gas inlet tubing & miniature brass shutoff valves on left flank (+X) + exhaust on right (-X)
            CreatePrimitive(furn, $"{PrefixV2}GasInletManifold", PrimitiveType.Cube,
                new Vector3(0.242f, 0.044f, tubeZ), Vector3.zero, new Vector3(0.036f, 0.024f, 0.056f), assets.BrushedMetalDark);
            CreatePrimitive(furn, $"{PrefixV2}GasTubeVert", PrimitiveType.Cylinder,
                new Vector3(0.264f, 0.082f, tubeZ + 0.018f), Vector3.zero, new Vector3(0.012f, 0.036f, 0.012f), assets.NickelTip);
            CreatePrimitive(furn, $"{PrefixV2}GasTubeHoriz", PrimitiveType.Cylinder,
                new Vector3(0.236f, tubeY, tubeZ + 0.018f), new Vector3(0f, 0f, 90f), new Vector3(0.012f, 0.028f, 0.012f), assets.NickelTip);
            CreateCustomMesh(furn, $"{PrefixV2}BrassShutoffValve", assets.Meshes.HexNut,
                new Vector3(0.264f, 0.088f, tubeZ + 0.034f), new Vector3(90f, 0f, 0f), new Vector3(0.024f, 0.014f, 0.024f), assets.BrassMetal);
            CreatePrimitive(furn, $"{PrefixV2}ValveHandleBar", PrimitiveType.Cube,
                new Vector3(0.264f, 0.088f, tubeZ + 0.042f), new Vector3(0f, 0f, 22f), new Vector3(0.026f, 0.007f, 0.007f), accentSolid);

            // Exhaust outlet on opposite right flank (-X)
            CreatePrimitive(furn, $"{PrefixV2}ExhaustPipe", PrimitiveType.Cylinder,
                new Vector3(-0.248f, tubeY, tubeZ), new Vector3(0f, 0f, 90f), new Vector3(0.016f, 0.026f, 0.016f), assets.NickelTip);
            CreateCustomMesh(furn, $"{PrefixV2}ExhaustCollar", assets.Meshes.HexNut,
                new Vector3(-0.262f, tubeY, tubeZ), new Vector3(0f, 0f, 90f), new Vector3(0.026f, 0.010f, 0.026f), assets.BrassMetal);

            // 5. Recessed digital process temperature bezel (1000 C) & cleanly framed FurnaceTitleCanvas
            // Note: World-Space Canvas faces local -Z, so euler (26, 180, 0) tilts its front upward toward +Y/+Z player.
            GameObject tempBezel = CreateCustomMesh(furn, $"{PrefixV2}TempBezelHousing", assets.Meshes.ChamferedBezel,
                new Vector3(-0.125f, 0.188f, -0.068f), new Vector3(26f, 180f, 0f), new Vector3(0.165f, 0.048f, 0.018f), assets.BrushedMetalDark);
            CreatePrimitive(tempBezel.transform, $"{PrefixV2}TempScreenRecess", PrimitiveType.Cube,
                new Vector3(0f, 0f, -0.36f), Vector3.zero, new Vector3(0.86f, 0.78f, 0.36f), assets.RecessDark);

            GameObject tempCanvasGo = new GameObject($"{PrefixV2}TempCanvas");
            tempCanvasGo.transform.SetParent(furn, false);
            tempCanvasGo.transform.localPosition = new Vector3(-0.125f, 0.1924f, -0.0588f);
            tempCanvasGo.transform.localEulerAngles = new Vector3(26f, 180f, 0f);
            tempCanvasGo.transform.localScale = new Vector3(0.001f, 0.001f, 1f);
            Canvas tc = tempCanvasGo.AddComponent<Canvas>();
            tc.renderMode = RenderMode.WorldSpace;
            RectTransform trt = tempCanvasGo.GetComponent<RectTransform>();
            trt.sizeDelta = new Vector2(142f, 38f);
            GameObject tTxtGo = new GameObject($"{PrefixV2}TempText");
            tTxtGo.transform.SetParent(tempCanvasGo.transform, false);
            RectTransform tTxtRt = tTxtGo.AddComponent<RectTransform>();
            tTxtRt.anchorMin = Vector2.zero;
            tTxtRt.anchorMax = Vector2.one;
            tTxtRt.offsetMin = Vector2.zero;
            tTxtRt.offsetMax = Vector2.zero;
            TMP_Text tmp = tTxtGo.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 15f;
            tmp.color = isNType ? new Color(0.45f, 0.96f, 1f, 1f) : new Color(1f, 0.72f, 0.35f, 1f);
            tmp.text = $"<b>1000 °C</b>\n<size=65%>{stationTag}</size>";
            tmp.raycastTarget = false;

            Transform titleCanvas = furn.Find("FurnaceTitleCanvas");
            if (titleCanvas != null)
            {
                GameObject titleBezel = CreateCustomMesh(furn, $"{PrefixV2}TitlePlateBezel", assets.Meshes.ChamferedBezel,
                    new Vector3(0.115f, 0.188f, -0.068f), new Vector3(26f, 180f, 0f), new Vector3(0.220f, 0.048f, 0.018f), assets.BrushedMetalDark);
                CreatePrimitive(titleBezel.transform, $"{PrefixV2}TitleScreenRecess", PrimitiveType.Cube,
                    new Vector3(0f, 0f, -0.36f), Vector3.zero, new Vector3(0.88f, 0.78f, 0.36f), assets.RecessDark);

                titleCanvas.localPosition = new Vector3(0.115f, 0.1924f, -0.0588f);
                titleCanvas.localEulerAngles = new Vector3(26f, 180f, 0f);
                titleCanvas.localScale = new Vector3(0.001f, 0.001f, 1f);
                RectTransform titleRt = titleCanvas.GetComponent<RectTransform>();
                if (titleRt != null)
                {
                    titleRt.sizeDelta = new Vector2(190f, 38f);
                }

                TMP_Text titleTmp = titleCanvas.GetComponentInChildren<TMP_Text>(true);
                if (titleTmp != null)
                {
                    RectTransform ttRt = titleTmp.GetComponent<RectTransform>();
                    ttRt.anchorMin = Vector2.zero;
                    ttRt.anchorMax = Vector2.one;
                    ttRt.offsetMin = new Vector2(4f, 2f);
                    ttRt.offsetMax = new Vector2(-4f, -2f);
                    titleTmp.text = isNType ? "N-TYPE FURNACE" : "P-TYPE FURNACE";
                    titleTmp.enableAutoSizing = true;
                    titleTmp.fontSizeMin = 11f;
                    titleTmp.fontSizeMax = 18f;
                    titleTmp.overflowMode = TextOverflowModes.Truncate;
                    EditorUtility.SetDirty(titleTmp.gameObject);
                }
                EditorUtility.SetDirty(titleCanvas.gameObject);
            }

            // 6. Illuminated status bezel collars framing WaferSocketBay and DopantSocketBay without touching SocketRing
            UpgradeSocketPortCollar(furn.Find("WaferSocketBay"), $"{PrefixV2}WaferPortRing", assets.BrushedMetalLight, accentGlow);
            UpgradeSocketPortCollar(furn.Find("DopantSocketBay"), $"{PrefixV2}DopantPortRing", assets.BrushedMetalLight, accentGlow);

            EditorUtility.SetDirty(furn.gameObject);
            return true;
        }

        private static void UpgradeSocketPortCollar(Transform bay, string name, Material outerMetal, Material glowMat)
        {
            if (bay == null) return;
            for (int i = bay.childCount - 1; i >= 0; i--)
            {
                if (bay.GetChild(i).name.StartsWith(PrefixV2, StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(bay.GetChild(i).gameObject);
                }
            }

            // Thin outer chamfer collar & illuminated micro status ring seated cleanly flush at bay base
            CreatePrimitive(bay, $"{name}_Bezel", PrimitiveType.Cylinder,
                new Vector3(0f, 0.0025f, 0f), Vector3.zero, new Vector3(0.194f, 0.0025f, 0.194f), outerMetal);
            CreatePrimitive(bay, $"{name}_Glow", PrimitiveType.Cylinder,
                new Vector3(0f, 0.0042f, 0f), Vector3.zero, new Vector3(0.182f, 0.0018f, 0.182f), glowMat);
        }

        private static bool UpgradeJunctionAlignmentRig(StationController st, UpgradeAssets assets)
        {
            Transform jig = st.transform.Find("PNJunctionJig");
            if (jig == null) return false;

            StripPrefixedChildren(jig, PrefixV2);

            // 1. Optical Breadboard Baseplate & Corner Leveling Bolts
            Transform deck = jig.Find("JigDeck");
            if (deck != null)
            {
                SwapMeshAndMat(deck, assets.Meshes.ChamferedBezel, assets.OpticalBreadboard);
            }

            Vector3[] boltCoords =
            {
                new Vector3(-0.268f, 0.018f, -0.140f),
                new Vector3(+0.268f, 0.018f, -0.140f),
                new Vector3(-0.268f, 0.018f, +0.140f),
                new Vector3(+0.268f, 0.018f, +0.140f)
            };
            for (int b = 0; b < boltCoords.Length; b++)
            {
                CreateCustomMesh(jig, $"{PrefixV2}LevelBolt_{b}", assets.Meshes.HexNut,
                    boltCoords[b], Vector3.zero, new Vector3(0.024f, 0.036f, 0.024f), assets.BrassMetal);
            }

            // 2. Parallel Steel Linear Guide Rails + End Pillow Blocks + Vernier Millimeter Scale Strip
            const float sockZ = -0.040f;
            float[] railZs = { sockZ - 0.096f, sockZ + 0.094f }; // -0.136 & +0.054 cleanly clearing 0.18m SocketRings
            for (int r = 0; r < railZs.Length; r++)
            {
                CreatePrimitive(jig, $"{PrefixV2}LinearRail_{r}", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.036f, railZs[r]), new Vector3(0f, 0f, 90f),
                    new Vector3(0.011f, 0.258f, 0.011f), assets.NickelTip);

                CreateCustomMesh(jig, $"{PrefixV2}RailPillowL_{r}", assets.Meshes.ChamferedBezel,
                    new Vector3(0.258f, 0.036f, railZs[r]), Vector3.zero,
                    new Vector3(0.022f, 0.022f, 0.022f), assets.BrushedMetalDark);
                CreateCustomMesh(jig, $"{PrefixV2}RailPillowR_{r}", assets.Meshes.ChamferedBezel,
                    new Vector3(-0.258f, 0.036f, railZs[r]), Vector3.zero,
                    new Vector3(0.022f, 0.022f, 0.022f), assets.BrushedMetalDark);
            }

            // Vernier mm scale plaque centered on front optical bridge cleanly behind front rail (zero overlap with JoinButton)
            CreatePrimitive(jig, $"{PrefixV2}VernierScaleBackdrop", PrimitiveType.Cube,
                new Vector3(0f, 0.031f, 0.028f), Vector3.zero, new Vector3(0.148f, 0.0025f, 0.020f), assets.RecessDark);
            CreatePrimitive(jig, $"{PrefixV2}VernierScaleTicks", PrimitiveType.Quad,
                new Vector3(0f, 0.0326f, 0.028f), new Vector3(90f, 180f, 0f), new Vector3(0.140f, 0.016f, 1f), assets.LinearScalePlate);

            // Lift P/N BayLabels 5mm cleanly onto rear carriage bezel plane so nothing clips
            Transform pLabel = jig.Find("PSideSocketBay/BayLabel");
            if (pLabel != null) pLabel.localPosition = new Vector3(0f, 0.020f, -0.114f);
            Transform nLabel = jig.Find("NSideSocketBay/BayLabel");
            if (nLabel != null) nLabel.localPosition = new Vector3(0f, 0.020f, -0.114f);

            // Central laser crosshair alignment mark at X = 0 metallurgical interface (terminates cleanly at vernier bar)
            CreatePrimitive(jig, $"{PrefixV2}InterfaceLaserBar", PrimitiveType.Cube,
                new Vector3(0f, 0.0312f, -0.052f), Vector3.zero, new Vector3(0.0035f, 0.002f, 0.132f), assets.LaserCrosshairCyan);

            // 3. P-Side (Left +X, Red/Teal) & N-Side (Right -X, Blue/Orange) Specimen Carriages,
            // Micrometer Drums, Brass Contact Probes, Copper Busbars & Front Test Terminals
            BuildCarriageSide(jig, true, sockZ, assets);
            BuildCarriageSide(jig, false, sockZ, assets);

            EditorUtility.SetDirty(jig.gameObject);

            // 4. Upgrade Central Junction Chamber (JunctionSimulationChamber / SimView) cleanly around hologram!
            Transform simChamber = st.transform.Find("JunctionSimulationChamber");
            if (simChamber != null)
            {
                StripPrefixedChildren(simChamber, PrefixV2);

                Transform stageBase = simChamber.Find("StageBase");
                if (stageBase != null)
                {
                    SwapMeshAndMat(stageBase, assets.Meshes.ChamferedBezel, assets.OpticalBreadboard);
                }

                // Back rear pillars upgraded cleanly
                string[] origPosts = { "PostL", "PostR" };
                for (int p = 0; p < origPosts.Length; p++)
                {
                    Transform post = simChamber.Find(origPosts[p]);
                    if (post != null) SwapMatOnly(post, assets.BrushedMetalDark);
                }

                // Open-top front corner pillars & side perimeter upper rails (clear center, unobstructed open top)
                CreateCustomMesh(simChamber, $"{PrefixV2}FrontPillar_L", assets.Meshes.ChamferedBezel,
                    new Vector3(0.355f, 0.180f, 0.098f), Vector3.zero, new Vector3(0.022f, 0.340f, 0.022f), assets.BrushedMetalLight);
                CreateCustomMesh(simChamber, $"{PrefixV2}FrontPillar_R", assets.Meshes.ChamferedBezel,
                    new Vector3(-0.355f, 0.180f, 0.098f), Vector3.zero, new Vector3(0.022f, 0.340f, 0.022f), assets.BrushedMetalLight);
                CreatePrimitive(simChamber, $"{PrefixV2}TopRail_L", PrimitiveType.Cube,
                    new Vector3(0.352f, 0.348f, 0.004f), Vector3.zero, new Vector3(0.018f, 0.014f, 0.210f), assets.BrushedMetalDark);
                CreatePrimitive(simChamber, $"{PrefixV2}TopRail_R", PrimitiveType.Cube,
                    new Vector3(-0.352f, 0.348f, 0.004f), Vector3.zero, new Vector3(0.018f, 0.014f, 0.210f), assets.BrushedMetalDark);

                // Laser-etched crosshair alignment guides marking X=0 metallurgical boundary plane cleanly inside base & rear
                CreatePrimitive(simChamber, $"{PrefixV2}CrosshairFloorLine", PrimitiveType.Cube,
                    new Vector3(0f, 0.026f, 0f), Vector3.zero, new Vector3(0.004f, 0.002f, 0.215f), assets.LaserCrosshairCyan);
                CreatePrimitive(simChamber, $"{PrefixV2}CrosshairVertL", PrimitiveType.Cube,
                    new Vector3(0f, 0.180f, -0.105f), Vector3.zero, new Vector3(0.004f, 0.295f, 0.004f), assets.LaserCrosshairCyan);
                CreatePrimitive(simChamber, $"{PrefixV2}CrosshairHoriz", PrimitiveType.Cube,
                    new Vector3(0f, 0.180f, -0.105f), Vector3.zero, new Vector3(0.680f, 0.003f, 0.003f), assets.LaserCrosshairCyan);

                // Emissive P/N Status Indicator LEDs mounted on front corner pillar caps (no Point Lights)
                CreatePrimitive(simChamber, $"{PrefixV2}StatusLed_P_Top", PrimitiveType.Sphere,
                    new Vector3(0.355f, 0.358f, 0.098f), Vector3.zero, new Vector3(0.024f, 0.024f, 0.024f), assets.AccentRedGlow);
                CreatePrimitive(simChamber, $"{PrefixV2}StatusLed_P_Base", PrimitiveType.Sphere,
                    new Vector3(0.355f, 0.032f, 0.098f), Vector3.zero, new Vector3(0.018f, 0.018f, 0.018f), assets.AccentCyanGlow);
                CreatePrimitive(simChamber, $"{PrefixV2}StatusLed_N_Top", PrimitiveType.Sphere,
                    new Vector3(-0.355f, 0.358f, 0.098f), Vector3.zero, new Vector3(0.024f, 0.024f, 0.024f), assets.AccentCyanGlow);
                CreatePrimitive(simChamber, $"{PrefixV2}StatusLed_N_Base", PrimitiveType.Sphere,
                    new Vector3(-0.355f, 0.032f, 0.098f), Vector3.zero, new Vector3(0.018f, 0.018f, 0.018f), assets.AccentAmberGlow);

                EditorUtility.SetDirty(simChamber.gameObject);
            }

            return true;
        }

        private static void BuildCarriageSide(Transform jig, bool isPSide, float sockZ, UpgradeAssets assets)
        {
            float sign = isPSide ? 1f : -1f;
            float cx = sign * 0.180f;
            string tag = isPSide ? "P" : "N";

            Material primaryTrim = isPSide ? assets.AccentTealSolid : assets.AccentBlueSolid;
            Material statusLed = isPSide ? assets.AccentRedGlow : assets.AccentAmberGlow;

            // Carriage saddle blocks straddling the front and rear rails cleanly outside the socket drop area
            CreateCustomMesh(jig, $"{PrefixV2}CarriageBlockRear_{tag}", assets.Meshes.ChamferedBezel,
                new Vector3(cx, 0.035f, sockZ - 0.096f), Vector3.zero, new Vector3(0.118f, 0.016f, 0.020f), assets.BrushedMetalDark);
            CreateCustomMesh(jig, $"{PrefixV2}CarriageBlockFront_{tag}", assets.Meshes.ChamferedBezel,
                new Vector3(cx, 0.035f, sockZ + 0.094f), Vector3.zero, new Vector3(0.118f, 0.016f, 0.020f), assets.BrushedMetalDark);

            // Outer clamp jaw block housing micrometer + probe
            CreateCustomMesh(jig, $"{PrefixV2}CarriageClampJaw_{tag}", assets.Meshes.ChamferedBezel,
                new Vector3(sign * 0.254f, 0.044f, sockZ), Vector3.zero, new Vector3(0.024f, 0.036f, 0.145f), assets.BrushedMetalLight);
            CreatePrimitive(jig, $"{PrefixV2}CarriageAccentTrim_{tag}", PrimitiveType.Cube,
                new Vector3(sign * 0.254f, 0.063f, sockZ), Vector3.zero, new Vector3(0.020f, 0.005f, 0.130f), primaryTrim);

            // Micrometer adjustment drum protruding outward along +-X
            CreatePrimitive(jig, $"{PrefixV2}MicroBarrel_{tag}", PrimitiveType.Cylinder,
                new Vector3(sign * 0.278f, 0.046f, sockZ), new Vector3(0f, 0f, 90f),
                new Vector3(0.020f, 0.016f, 0.020f), assets.NickelTip);
            CreateCustomMesh(jig, $"{PrefixV2}MicroKnurledDrum_{tag}", assets.Meshes.KnurledDial,
                new Vector3(sign * 0.304f, 0.046f, sockZ), new Vector3(0f, 0f, 90f),
                new Vector3(0.026f, 0.014f, 0.026f), assets.BrushedMetalDark);
            CreatePrimitive(jig, $"{PrefixV2}MicroCollar_{tag}", PrimitiveType.Cylinder,
                new Vector3(sign * 0.289f, 0.046f, sockZ), new Vector3(0f, 0f, 90f),
                new Vector3(0.023f, 0.004f, 0.023f), primaryTrim);

            // Spring-loaded gold/brass contact probe pointing inward cleanly outside block perimeter (|X| = 0.250..0.242)
            CreatePrimitive(jig, $"{PrefixV2}ProbeBody_{tag}", PrimitiveType.Cylinder,
                new Vector3(sign * 0.240f, 0.048f, sockZ - 0.018f), new Vector3(0f, 0f, 90f),
                new Vector3(0.008f, 0.010f, 0.008f), assets.BrassMetal);
            CreatePrimitive(jig, $"{PrefixV2}ProbeSpringTip_{tag}", PrimitiveType.Cylinder,
                new Vector3(sign * 0.240f, 0.048f, sockZ + 0.018f), new Vector3(0f, 0f, 90f),
                new Vector3(0.008f, 0.010f, 0.008f), assets.BrassMetal);

            // Heavy copper busbar + front test terminal beside JoinButton
            CreatePrimitive(jig, $"{PrefixV2}CopperBusbar_{tag}", PrimitiveType.Cube,
                new Vector3(sign * 0.214f, 0.032f, 0.056f), Vector3.zero,
                new Vector3(0.018f, 0.006f, 0.118f), assets.CopperBand);
            CreateCustomMesh(jig, $"{PrefixV2}TestTerminalNut_{tag}", assets.Meshes.HexNut,
                new Vector3(sign * 0.214f, 0.038f, 0.114f), Vector3.zero,
                new Vector3(0.032f, 0.012f, 0.032f), assets.BrassMetal);
            CreatePrimitive(jig, $"{PrefixV2}TestTerminalCollar_{tag}", PrimitiveType.Cylinder,
                new Vector3(sign * 0.214f, 0.048f, 0.114f), Vector3.zero,
                new Vector3(0.024f, 0.006f, 0.024f), primaryTrim);
            CreatePrimitive(jig, $"{PrefixV2}TestTerminalLed_{tag}", PrimitiveType.Sphere,
                new Vector3(sign * 0.252f, 0.034f, 0.114f), Vector3.zero,
                new Vector3(0.012f, 0.012f, 0.012f), statusLed);
        }

        private static Material ResolveCrystalMaterial(string symbol, DopantValenceGroup group, UpgradeAssets assets)
        {
            switch (group)
            {
                case DopantValenceGroup.GroupV_Donor:
                    return symbol == "As" ? assets.CrystalDonorElectricBlue : assets.CrystalDonorCyan;
                case DopantValenceGroup.GroupIII_Acceptor:
                    return (symbol == "Ga" || symbol == "In") ? assets.CrystalAcceptorRubyAmber : assets.CrystalAcceptorMagenta;
                default:
                    return assets.CrystalControlSilverViolet;
            }
        }

        private static Texture2D SavePngTexture(Texture2D runtimeTex, string assetPath, bool isNormalMap, bool hasAlpha)
        {
            byte[] png = runtimeTex.EncodeToPNG();
            Object.DestroyImmediate(runtimeTex);
            File.WriteAllBytes(assetPath, png);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !isNormalMap;
                importer.alphaIsTransparency = !isNormalMap && hasAlpha;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Material LoadOrCreateMat(
            string matName,
            Color baseColor,
            float metallic,
            float smoothness,
            Texture2D baseMap,
            Texture2D normalMap,
            float bumpScale,
            Color emissionColor,
            bool transparent)
        {
            string path = $"{MatFolder}/{matName}.mat";
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(litShader) { name = matName };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != litShader)
            {
                mat.shader = litShader;
            }

            mat.SetColor(BaseColorId, baseColor);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_SpecularHighlights", 1f);
            mat.SetFloat("_EnvironmentReflections", 1f);
            mat.SetFloat("_Cull", 2f);
            mat.SetTexture("_BaseMap", baseMap);

            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.SetFloat("_BumpScale", bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                mat.SetTexture("_BumpMap", null);
                mat.DisableKeyword("_NORMALMAP");
            }

            if (emissionColor.maxColorComponent > 0.001f)
            {
                mat.SetColor(EmissionColorId, emissionColor);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.SetColor(EmissionColorId, Color.black);
                mat.DisableKeyword("_EMISSION");
            }

            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
                mat.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_ZWrite", 1f);
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Geometry;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SyncStationPrefabs(LessonManager lm)
        {
            for (int i = 0; i < lm.Stations.Count; i++)
            {
                StationController st = lm.Stations[i];
                if (st == null) continue;
                if (st.StationId != "2" && st.StationId != "3" && st.StationId != "4") continue;
                if (PrefabUtility.IsPartOfPrefabInstance(st.gameObject))
                {
                    string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(st.gameObject);
                    if (!string.IsNullOrEmpty(assetPath) && assetPath.StartsWith("Assets/PNJunctionLab/", StringComparison.OrdinalIgnoreCase))
                    {
                        PrefabUtility.ApplyPrefabInstance(st.gameObject, InteractionMode.AutomatedAction);
                    }
                }
            }
        }

        private static void StripPrefixedChildren(Transform parent, string prefix)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void SwapMeshAndMat(Transform tr, Mesh mesh, Material mat)
        {
            MeshFilter mf = tr.GetComponent<MeshFilter>();
            if (mf != null && mesh != null)
            {
                mf.sharedMesh = mesh;
                EditorUtility.SetDirty(mf);
            }
            SwapMatOnly(tr, mat);
        }

        private static void SwapMatOnly(Transform tr, Material mat)
        {
            MeshRenderer mr = tr.GetComponent<MeshRenderer>();
            if (mr != null && mat != null)
            {
                mr.sharedMaterial = mat;
                EditorUtility.SetDirty(mr);
            }
        }

        private static GameObject CreatePrimitive(
            Transform parent, string name, PrimitiveType primitive,
            Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }
            ConfigureTransform(go, parent, localPos, localEuler, localScale, mat);
            return go;
        }

        private static GameObject CreateCustomMesh(
            Transform parent, string name, Mesh mesh,
            Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            ConfigureTransform(go, parent, localPos, localEuler, localScale, mat);
            return go;
        }

        private static void ConfigureTransform(
            GameObject go, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null)
            {
                mr.sharedMaterial = mat;
            }
            EditorUtility.SetDirty(go);
        }
    }
}
