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
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Upgrades ONLY the visual child meshes, materials, and procedural 512x512 PBR textures
    /// of interactive props and lab controls across Stations T through 6 without modifying any
    /// StationController logic, socket rules, Collider bounds, AttachTransform positions, or SemiconductorModel code.
    /// </summary>
    public static class Phase7HardwareVisualUpgradeBuilder
    {
        private const string ScenePath = "Assets/PNJunctionLab/Scenes/PNJunctionLab.unity";
        private const string TexFolder = "Assets/PNJunctionLab/Textures";
        private const string MatFolder = "Assets/PNJunctionLab/Materials/Hardware";
        private const string VisualPrefix = "HW_";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private sealed class HardwareAssetSet
        {
            public ProceduralHardwareMeshFactory.HardwareMeshSet Meshes;

            // Textures
            public Texture2D LinearScaleTicks;

            // Shared Materials
            public Material WaferIridescent;
            public Material WaferSpecimen;
            public Material SiliconCubeCrystal;
            public Material BrushedMetalDark;
            public Material BrushedMetalLight;
            public Material RecessDark;
            public Material BrassMetal;
            public Material CopperBand;
            public Material NickelTip;
            public Material RubberDark;
            public Material BorosilicateGlass;
            public Material VialLabelWrap;
            public Material BatteryCasing;
            public Material ScaleDialPlate;
            public Material PointerWhite;
            public Material LeadRedSatin;
            public Material LeadBlackSatin;

            // Dopants
            public Material DopantOrange;
            public Material DopantAmberRed;
            public Material DopantGoldSilver;
            public Material DopantTeal;
            public Material DopantSilver;
            public Material DopantGalliumBlue;
            public Material DopantSlate;

            // Diodes & Resistors
            public Material DiodeEpoxyBlack;
            public Material DiodeGlassAmber;
            public Material DiodeGlassZener;
            public Material DiodeLedDome;
            public Material ResistorBody;
            public Material ResistorRed;
            public Material ResistorBrown;
            public Material ResistorGold;

            // Crystal Blocks
            public Material BlockCore;
            public Material CrystalShell;

            // Translucent Illuminated Button Caps & Inner Glow Rings
            public readonly Dictionary<string, Material> ButtonCapMats = new Dictionary<string, Material>();
            public readonly Dictionary<string, Material> ButtonGlowMats = new Dictionary<string, Material>();
        }

        [MenuItem("PNJunctionLab/Upgrade Interactive Props & Control Hardware")]
        public static void ApplyAllHardwareVisualsMenu()
        {
            Debug.Log(ApplyAllHardwareVisuals());
        }

        /// <summary>
        /// Programmatic entry point callable via Unity MCP execute_code.
        /// Generates persistent 512x512 PNG textures, low-poly meshes, URP/Lit PBR materials,
        /// upgrades visual children on all props & controls across T..6, syncs prefabs, and saves the scene.
        /// </summary>
        public static string ApplyAllHardwareVisuals()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.path.EndsWith("PNJunctionLab.unity", StringComparison.OrdinalIgnoreCase))
            {
                activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            HardwareAssetSet assets = CreateTexturesMeshesAndMaterials();
            LessonManager lm = Object.FindAnyObjectByType<LessonManager>();
            if (lm == null || lm.Stations == null)
            {
                return "HardwareUpgrade Error: LessonManager not found.";
            }

            int wafers = 0;
            int vials = 0;
            int buttons = 0;
            int knobs = 0;
            int sliders = 0;
            int diodes = 0;
            int blocks = 0;
            int batteries = 0;
            int leads = 0;

            for (int i = 0; i < lm.Stations.Count; i++)
            {
                StationController st = lm.Stations[i];
                if (st == null || string.Equals(st.StationId, "A", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                wafers += UpgradeWafersAndMicroscopeInStation(st, assets);
                vials += UpgradeDopantVialsInStation(st, assets);
                buttons += UpgradePushButtonsInStation(st, assets);
                knobs += UpgradeRotaryKnobsInStation(st, assets);
                sliders += UpgradeSlidersInStation(st, assets);
                blocks += UpgradeSemiconductorBlocksInStation(st, assets);

                if (st.StationId == "5")
                {
                    UpgradeBatteryCircuitAndLeads(st, assets, ref batteries, ref leads);
                }

                if (st.StationId == "6")
                {
                    diodes += UpgradeSampleDiodesAndAnalyzer(st, assets);
                }

                if (st.StationId == "2" || st.StationId == "3")
                {
                    UpgradeFurnaceHousingVisual(st, assets);
                }
            }

            RefreshAllInteractionHighlightCaches();
            ApplyProjectPrefabOverrides(lm);

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            StringBuilder sb = new StringBuilder();
            sb.Append($"HardwareUpgrade Complete: Textures=12 (512x512 PNGs), Meshes=7 (.asset), ");
            sb.Append($"Wafers={wafers}, Vials={vials}, Buttons={buttons}, Knobs={knobs}, ");
            sb.Append($"Sliders={sliders}, Batteries={batteries}, Leads={leads}, Diodes={diodes}, Blocks={blocks}.");
            return sb.ToString();
        }

        private static HardwareAssetSet CreateTexturesMeshesAndMaterials()
        {
            Directory.CreateDirectory(TexFolder);
            Directory.CreateDirectory(MatFolder);

            HardwareAssetSet set = new HardwareAssetSet
            {
                Meshes = ProceduralHardwareMeshFactory.EnsureAllMeshes()
            };

            // Generate and persist 512x512 PNG albedo + normal/detail assets
            Texture2D waferAlb = SavePngTexture(ProceduralLabTextures.CreateSiliconWaferAlbedo(), $"{TexFolder}/Tex_SiliconWafer_Albedo.png", false, false);
            Texture2D waferNrm = SavePngTexture(ProceduralLabTextures.CreateSiliconWaferNormal(), $"{TexFolder}/Tex_SiliconWafer_Normal.png", true, false);
            Texture2D metalAlb = SavePngTexture(ProceduralLabTextures.CreateBrushedMetalAlbedo(), $"{TexFolder}/Tex_BrushedMetal_Albedo.png", false, false);
            Texture2D metalNrm = SavePngTexture(ProceduralLabTextures.CreateBrushedMetalNormal(), $"{TexFolder}/Tex_BrushedMetal_Normal.png", true, false);
            Texture2D batAlb = SavePngTexture(ProceduralLabTextures.CreateBatteryCasingAlbedo(), $"{TexFolder}/Tex_BatteryCasing_Albedo.png", false, false);
            Texture2D batNrm = SavePngTexture(ProceduralLabTextures.CreateBatteryCasingNormal(), $"{TexFolder}/Tex_BatteryCasing_Normal.png", true, false);
            Texture2D dopAlb = SavePngTexture(ProceduralLabTextures.CreateDopantGranularAlbedo(), $"{TexFolder}/Tex_DopantGranular_Albedo.png", false, false);
            Texture2D dopNrm = SavePngTexture(ProceduralLabTextures.CreateDopantGranularNormal(), $"{TexFolder}/Tex_DopantGranular_Normal.png", true, false);
            Texture2D latAlb = SavePngTexture(ProceduralLabTextures.CreateCrystalLatticeAlbedo(), $"{TexFolder}/Tex_CrystalLattice_Albedo.png", false, false);
            Texture2D latNrm = SavePngTexture(ProceduralLabTextures.CreateCrystalLatticeNormal(), $"{TexFolder}/Tex_CrystalLattice_Normal.png", true, false);
            Texture2D dialScale = SavePngTexture(ProceduralLabTextures.CreateScaleDialPlateAlbedo(), $"{TexFolder}/Tex_ScaleDialPlate_Albedo.png", false, false);
            set.LinearScaleTicks = SavePngTexture(ProceduralLabTextures.CreateLinearScaleTicksAlbedo(), $"{TexFolder}/Tex_LinearScaleTicks_Albedo.png", false, true);

            // Create/refresh URP/Lit PBR Materials
            set.WaferIridescent = SavePbrMaterial("Mat_HW_SiliconWafer",
                new Color(0.84f, 0.92f, 1.0f, 1f), 0.85f, 0.88f, waferAlb, waferNrm, 0.42f, Color.black, false);
            set.WaferSpecimen = SavePbrMaterial("Mat_HW_WaferSpecimen",
                Color.white, 0.85f, 0.88f, waferAlb, waferNrm, 0.42f, Color.black, false);
            set.SiliconCubeCrystal = SavePbrMaterial("Mat_HW_SiliconCube",
                new Color(0.72f, 0.84f, 0.96f, 1f), 0.72f, 0.84f, waferAlb, waferNrm, 0.35f, new Color(0.03f, 0.08f, 0.14f, 1f), false);

            set.BrushedMetalDark = SavePbrMaterial("Mat_HW_BrushedMetalDark",
                new Color(0.32f, 0.36f, 0.43f, 1f), 0.80f, 0.68f, metalAlb, metalNrm, 0.48f, Color.black, false);
            set.BrushedMetalLight = SavePbrMaterial("Mat_HW_BrushedMetalLight",
                new Color(0.84f, 0.88f, 0.93f, 1f), 0.88f, 0.78f, metalAlb, metalNrm, 0.36f, Color.black, false);
            set.RecessDark = SavePbrMaterial("Mat_HW_RecessDark",
                new Color(0.06f, 0.07f, 0.10f, 1f), 0.18f, 0.32f, null, null, 0f, Color.black, false);
            set.BrassMetal = SavePbrMaterial("Mat_HW_BrassMetal",
                new Color(0.90f, 0.70f, 0.28f, 1f), 0.92f, 0.82f, metalAlb, metalNrm, 0.30f, Color.black, false);
            set.CopperBand = SavePbrMaterial("Mat_HW_CopperBand",
                new Color(0.86f, 0.52f, 0.24f, 1f), 0.90f, 0.80f, metalAlb, metalNrm, 0.25f, Color.black, false);
            set.NickelTip = SavePbrMaterial("Mat_HW_NickelTip",
                new Color(0.88f, 0.91f, 0.96f, 1f), 0.94f, 0.90f, metalAlb, null, 0f, Color.black, false);
            set.RubberDark = SavePbrMaterial("Mat_HW_RubberDark",
                new Color(0.11f, 0.12f, 0.15f, 1f), 0.02f, 0.22f, null, null, 0f, Color.black, false);
            set.BorosilicateGlass = SavePbrMaterial("Mat_HW_BorosilicateGlass",
                new Color(0.84f, 0.95f, 1.0f, 0.26f), 0.08f, 0.95f, null, null, 0f, new Color(0.02f, 0.05f, 0.08f, 1f), true);
            set.VialLabelWrap = SavePbrMaterial("Mat_HW_VialLabelWrap",
                new Color(0.92f, 0.94f, 0.97f, 1f), 0.02f, 0.38f, null, null, 0f, Color.black, false);
            set.BatteryCasing = SavePbrMaterial("Mat_HW_BatteryCasing",
                Color.white, 0.42f, 0.58f, batAlb, batNrm, 0.55f, Color.black, false);
            set.ScaleDialPlate = SavePbrMaterial("Mat_HW_ScaleDialPlate",
                Color.white, 0.36f, 0.52f, dialScale, null, 0f, new Color(0.02f, 0.03f, 0.05f, 1f), false);
            set.PointerWhite = SavePbrMaterial("Mat_HW_PointerWhite",
                new Color(0.97f, 0.99f, 1.0f, 1f), 0.1f, 0.65f, null, null, 0f, new Color(0.28f, 0.34f, 0.42f, 1f), false);

            set.LeadRedSatin = SavePbrMaterial("Mat_HW_LeadRedSatin",
                new Color(0.84f, 0.14f, 0.16f, 1f), 0.06f, 0.46f, null, null, 0f, Color.black, false);
            set.LeadBlackSatin = SavePbrMaterial("Mat_HW_LeadBlackSatin",
                new Color(0.12f, 0.13f, 0.16f, 1f), 0.06f, 0.42f, null, null, 0f, Color.black, false);

            // Granular Dopants
            set.DopantOrange = SavePbrMaterial("Mat_HW_DopantOrange",
                new Color(0.96f, 0.52f, 0.14f, 1f), 0.25f, 0.78f, dopAlb, dopNrm, 0.80f, new Color(0.10f, 0.03f, 0.01f, 1f), false);
            set.DopantAmberRed = SavePbrMaterial("Mat_HW_DopantAmberRed",
                new Color(0.88f, 0.26f, 0.12f, 1f), 0.22f, 0.76f, dopAlb, dopNrm, 0.80f, new Color(0.08f, 0.02f, 0.01f, 1f), false);
            set.DopantGoldSilver = SavePbrMaterial("Mat_HW_DopantGoldSilver",
                new Color(0.82f, 0.72f, 0.46f, 1f), 0.68f, 0.84f, dopAlb, dopNrm, 0.80f, Color.black, false);
            set.DopantTeal = SavePbrMaterial("Mat_HW_DopantTeal",
                new Color(0.16f, 0.76f, 0.74f, 1f), 0.30f, 0.78f, dopAlb, dopNrm, 0.80f, new Color(0.01f, 0.08f, 0.08f, 1f), false);
            set.DopantSilver = SavePbrMaterial("Mat_HW_DopantSilver",
                new Color(0.76f, 0.82f, 0.88f, 1f), 0.74f, 0.84f, dopAlb, dopNrm, 0.80f, Color.black, false);
            set.DopantGalliumBlue = SavePbrMaterial("Mat_HW_DopantGalliumBlue",
                new Color(0.46f, 0.74f, 0.86f, 1f), 0.62f, 0.86f, dopAlb, dopNrm, 0.75f, new Color(0.02f, 0.05f, 0.08f, 1f), false);
            set.DopantSlate = SavePbrMaterial("Mat_HW_DopantSlate",
                new Color(0.32f, 0.36f, 0.42f, 1f), 0.45f, 0.68f, dopAlb, dopNrm, 0.80f, Color.black, false);

            // Diodes & Resistors
            set.DiodeEpoxyBlack = SavePbrMaterial("Mat_HW_DiodeEpoxyBlack",
                new Color(0.11f, 0.12f, 0.15f, 1f), 0.05f, 0.28f, null, null, 0f, Color.black, false);
            set.DiodeGlassAmber = SavePbrMaterial("Mat_HW_DiodeGlassAmber",
                new Color(0.95f, 0.56f, 0.16f, 0.42f), 0.06f, 0.95f, null, null, 0f, new Color(0.10f, 0.04f, 0.01f, 1f), true);
            set.DiodeGlassZener = SavePbrMaterial("Mat_HW_DiodeGlassZener",
                new Color(0.92f, 0.24f, 0.32f, 0.42f), 0.06f, 0.95f, null, null, 0f, new Color(0.10f, 0.02f, 0.03f, 1f), true);
            set.DiodeLedDome = SavePbrMaterial("Mat_HW_DiodeLedDome",
                new Color(0.72f, 0.98f, 0.82f, 0.36f), 0.04f, 0.96f, null, null, 0f, new Color(0.03f, 0.14f, 0.06f, 1f), true);
            set.ResistorBody = SavePbrMaterial("Mat_HW_ResistorBody",
                new Color(0.84f, 0.74f, 0.56f, 1f), 0.05f, 0.38f, null, null, 0f, Color.black, false);
            set.ResistorRed = SavePbrMaterial("Mat_HW_ResistorRed",
                new Color(0.82f, 0.14f, 0.14f, 1f), 0.05f, 0.45f, null, null, 0f, Color.black, false);
            set.ResistorBrown = SavePbrMaterial("Mat_HW_ResistorBrown",
                new Color(0.38f, 0.20f, 0.08f, 1f), 0.05f, 0.42f, null, null, 0f, Color.black, false);
            set.ResistorGold = SavePbrMaterial("Mat_HW_ResistorGold",
                new Color(0.88f, 0.70f, 0.24f, 1f), 0.88f, 0.80f, null, null, 0f, Color.black, false);

            // Crystal Blocks
            set.BlockCore = SavePbrMaterial("Mat_HW_BlockCore",
                Color.white, 0.22f, 0.82f, latAlb, latNrm, 0.55f, Color.black, false);
            set.CrystalShell = SavePbrMaterial("Mat_HW_CrystalShell",
                new Color(0.84f, 0.94f, 1.0f, 0.30f), 0.08f, 0.94f, latAlb, latNrm, 0.45f, new Color(0.03f, 0.06f, 0.10f, 1f), true);

            // Button Cap Palette (translucent backlit caps + emissive inner rings)
            RegisterButtonColor(set, "Green", new Color(0.18f, 0.86f, 0.44f, 0.84f), new Color(0.10f, 0.72f, 0.30f, 1f));
            RegisterButtonColor(set, "Blue", new Color(0.20f, 0.64f, 0.98f, 0.84f), new Color(0.12f, 0.52f, 0.96f, 1f));
            RegisterButtonColor(set, "Amber", new Color(0.98f, 0.68f, 0.16f, 0.84f), new Color(0.92f, 0.50f, 0.08f, 1f));
            RegisterButtonColor(set, "Red", new Color(0.94f, 0.24f, 0.26f, 0.84f), new Color(0.90f, 0.14f, 0.16f, 1f));
            RegisterButtonColor(set, "Teal", new Color(0.16f, 0.80f, 0.78f, 0.84f), new Color(0.08f, 0.68f, 0.68f, 1f));
            RegisterButtonColor(set, "Gold", new Color(0.96f, 0.80f, 0.26f, 0.84f), new Color(0.88f, 0.64f, 0.10f, 1f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return set;
        }

        private static void RegisterButtonColor(HardwareAssetSet set, string key, Color capRgba, Color glowColor)
        {
            set.ButtonCapMats[key] = SavePbrMaterial($"Mat_HW_BtnCap_{key}",
                capRgba, 0.12f, 0.84f, null, null, 0f, glowColor * 0.22f, true);
            set.ButtonGlowMats[key] = SavePbrMaterial($"Mat_HW_BtnRing_{key}",
                glowColor, 0.15f, 0.70f, null, null, 0f, glowColor * 1.45f, false);
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

        private static Material SavePbrMaterial(
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

        private static int UpgradeWafersAndMicroscopeInStation(StationController st, HardwareAssetSet assets)
        {
            int count = 0;
            if (st.StationId == "T")
            {
                Transform cube = st.transform.Find("TestSiliconCube");
                if (cube != null)
                {
                    SwapMeshAndMat(cube, assets.Meshes.RoundedBlock, assets.SiliconCubeCrystal);
                    StripOldVisualChildren(cube);
                    // Subtle metallic frame trim inset cleanly inside the 0.14m cube collider
                    CreateVisualPrimitive(cube, $"{VisualPrefix}FrameBand", PrimitiveType.Cube,
                        Vector3.zero, Vector3.zero, new Vector3(1.01f, 0.14f, 1.01f), assets.BrushedMetalLight);
                    EditorUtility.SetDirty(cube.gameObject);
                    count++;
                }
                return count;
            }

            if (st.StationId == "1")
            {
                Transform mic = st.transform.Find("LabMicroscope");
                if (mic != null)
                {
                    Transform specWafer = mic.Find("StageWaferSpecimen");
                    if (specWafer != null)
                    {
                        SwapMeshAndMat(specWafer, assets.Meshes.SiliconWafer, assets.WaferSpecimen);
                        EditorUtility.SetDirty(specWafer.gameObject);
                        count++;
                    }

                    string[] knobNames = { "FocusKnob_L", "FocusKnob_R" };
                    for (int k = 0; k < knobNames.Length; k++)
                    {
                        Transform fk = mic.Find(knobNames[k]);
                        if (fk != null)
                        {
                            SwapMeshAndMat(fk, assets.Meshes.KnurledDial, assets.BrushedMetalLight);
                            EditorUtility.SetDirty(fk.gameObject);
                        }
                    }
                }
                return count;
            }

            Transform waferRoot = FindChildRecursive(st.transform, "SiliconWafer_Grabbable");
            if (waferRoot != null)
            {
                Transform disc = waferRoot.Find("DiscMesh");
                if (disc != null)
                {
                    StripOldVisualChildren(disc);
                    SwapMeshAndMat(disc, assets.Meshes.SiliconWafer, assets.WaferIridescent);
                    // Rotate so SEMI flat edge (-Z local) faces clearly toward the player (-X in St2/3 world = +Z local)
                    disc.localEulerAngles = new Vector3(0f, 180f, 0f);
                    EditorUtility.SetDirty(disc.gameObject);
                    count++;
                }
            }

            Transform tray = FindChildRecursive(st.transform, "WaferTray");
            if (tray != null)
            {
                StripOldVisualChildren(tray);
                SwapMeshAndMat(tray, assets.Meshes.ChamferedBezel, assets.BrushedMetalDark);
                CreateVisualPrimitive(tray, $"{VisualPrefix}TrayRecess", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(0.84f, 0.18f, 0.84f), assets.RecessDark);
                EditorUtility.SetDirty(tray.gameObject);
            }

            return count;
        }

        private static int UpgradeDopantVialsInStation(StationController st, HardwareAssetSet assets)
        {
            int count = 0;
            DopantVialItem[] vials = st.GetComponentsInChildren<DopantVialItem>(true);
            for (int i = 0; i < vials.Length; i++)
            {
                DopantVialItem vial = vials[i];
                if (vial == null) continue;

                StripOldVisualChildren(vial.transform);

                Transform bottle = vial.transform.Find("Bottle");
                if (bottle != null)
                {
                    SwapMatOnly(bottle, assets.BorosilicateGlass);
                }

                // Sparkling granular dopant mound + 3 faceted crystals inside bottom half of glass bottle
                Material pelletMat = ResolveDopantGranularMaterial(vial.ElementSymbol, assets);
                CreateVisualPrimitive(vial.transform, $"{VisualPrefix}DopantPowder", PrimitiveType.Cylinder,
                    new Vector3(0f, -0.020f, 0f), Vector3.zero, new Vector3(0.044f, 0.020f, 0.044f), pelletMat);

                Vector3[] crystalOffsets =
                {
                    new Vector3(-0.009f, -0.001f, 0.005f),
                    new Vector3(0.010f, -0.002f, -0.006f),
                    new Vector3(0.001f, 0.001f, 0.009f)
                };
                Vector3[] crystalEulers =
                {
                    new Vector3(18f, 25f, 12f),
                    new Vector3(-14f, 64f, 22f),
                    new Vector3(28f, -40f, 16f)
                };
                for (int c = 0; c < 3; c++)
                {
                    CreateVisualCustomMesh(vial.transform, $"{VisualPrefix}DopantCrystal_{c}", assets.Meshes.RoundedBlock,
                        crystalOffsets[c], crystalEulers[c], new Vector3(0.012f, 0.009f, 0.011f), pelletMat);
                }

                // Thick glass base cushion inside bottom
                CreateVisualPrimitive(vial.transform, $"{VisualPrefix}GlassBase", PrimitiveType.Cylinder,
                    new Vector3(0f, -0.042f, 0f), Vector3.zero, new Vector3(0.053f, 0.003f, 0.053f), assets.BrushedMetalLight);

                // Rubber stopper + metallic crimp collar beneath colored polarity cap crown
                CreateVisualPrimitive(vial.transform, $"{VisualPrefix}StopperRubber", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.039f, 0f), Vector3.zero, new Vector3(0.047f, 0.007f, 0.047f), assets.RubberDark);
                CreateVisualPrimitive(vial.transform, $"{VisualPrefix}CrimpCollar", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.043f, 0f), Vector3.zero, new Vector3(0.059f, 0.0045f, 0.059f), assets.BrushedMetalLight);

                Transform cap = vial.transform.Find("Cap");
                if (cap != null)
                {
                    cap.localPosition = new Vector3(0f, 0.051f, 0f);
                    cap.localScale = new Vector3(0.056f, 0.006f, 0.056f);
                }

                // Wrapped middle collar band at Y = +0.016 (leaving entire bottom half Y <= -0.001 crystal-clear for pellets)
                CreateVisualPrimitive(vial.transform, $"{VisualPrefix}LabelWrapBand", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.016f, 0f), Vector3.zero, new Vector3(0.0575f, 0.016f, 0.0575f), assets.VialLabelWrap);

                // Clean up front backing plate to frame 2-line Symbol + Atomic Number cleanly without overflow
                Transform lblBack = vial.transform.Find("LabelBacking");
                if (lblBack != null)
                {
                    lblBack.localPosition = new Vector3(0f, 0.016f, 0.0285f);
                    lblBack.localScale = new Vector3(0.052f, 0.036f, 0.004f);
                }

                Transform canvasTf = vial.transform.Find("VialCanvas");
                if (canvasTf != null)
                {
                    canvasTf.localPosition = new Vector3(0f, 0.016f, 0.0315f);
                    TMP_Text symText = canvasTf.GetComponentInChildren<TMP_Text>(true);
                    if (symText != null)
                    {
                        string grpRoman = vial.ValenceGroup == DopantValenceGroup.GroupV_Donor ? "V"
                            : (vial.ValenceGroup == DopantValenceGroup.GroupIII_Acceptor ? "III" : "IV");
                        symText.enableAutoSizing = true;
                        symText.fontSizeMin = 12f;
                        symText.fontSizeMax = 44f;
                        symText.textWrappingMode = TextWrappingModes.NoWrap;
                        symText.text = $"<b>{vial.ElementSymbol}</b>\n<size=56%>Z={vial.AtomicNumber} · {grpRoman}</size>";
                    }
                }

                EditorUtility.SetDirty(vial.gameObject);
                count++;
            }

            return count;
        }

        private static int UpgradePushButtonsInStation(StationController st, HardwareAssetSet assets)
        {
            int count = 0;
            PhysicalPushButton[] btns = st.GetComponentsInChildren<PhysicalPushButton>(true);
            for (int i = 0; i < btns.Length; i++)
            {
                PhysicalPushButton btn = btns[i];
                if (btn == null) continue;

                StripOldVisualChildren(btn.transform);

                Transform housing = btn.transform.Find("Housing");
                if (housing != null)
                {
                    SwapMeshAndMat(housing, assets.Meshes.ChamferedBezel, assets.BrushedMetalDark);
                    // Recessed dark collar around cap well
                    Vector3 hPos = housing.localPosition;
                    Vector3 hScale = housing.localScale;
                    CreateVisualCustomMesh(btn.transform, $"{VisualPrefix}RecessedCollar", assets.Meshes.ChamferedBezel,
                        new Vector3(hPos.x, hPos.y + hScale.y * 0.50f + 0.001f, hPos.z),
                        Vector3.zero,
                        new Vector3(hScale.x * 0.88f, 0.008f, hScale.z * 0.88f),
                        assets.RecessDark);
                }

                Transform cap = btn.transform.Find("ButtonCap");
                if (cap != null)
                {
                    StripOldVisualChildren(cap);
                    string key = ResolveButtonColorKey(cap);
                    Material capMat = assets.ButtonCapMats[key];
                    Material glowMat = assets.ButtonGlowMats[key];
                    SwapMeshAndMat(cap, assets.Meshes.RoundedCap, capMat);

                    // Inner emissive status ring inside translucent button cap (depresses cleanly with capTransform!)
                    CreateVisualCustomMesh(cap, $"{VisualPrefix}InnerStatusRing", assets.Meshes.RoundedCap,
                        new Vector3(0f, -0.05f, 0f), Vector3.zero, new Vector3(0.84f, 0.62f, 0.84f), glowMat);
                }

                Transform labelPlate = btn.transform.Find("LabelPlate");
                if (labelPlate != null)
                {
                    SwapMeshAndMat(labelPlate, assets.Meshes.ChamferedBezel, assets.BrushedMetalDark);
                }

                // Crisp non-raycast micro-icon badge / LED indicator on CapUiOverlay
                Transform capOverlay = btn.transform.Find("CapUiOverlay");
                if (capOverlay != null)
                {
                    StripOldVisualChildren(capOverlay);
                    CreateButtonMicroIcon(capOverlay, btn.name);
                }

                EditorUtility.SetDirty(btn.gameObject);
                count++;
            }

            return count;
        }

        private static int UpgradeRotaryKnobsInStation(StationController st, HardwareAssetSet assets)
        {
            int count = 0;
            RotaryKnobControl[] knobs = st.GetComponentsInChildren<RotaryKnobControl>(true);
            for (int i = 0; i < knobs.Length; i++)
            {
                RotaryKnobControl knob = knobs[i];
                if (knob == null) continue;

                StripOldVisualChildren(knob.transform);

                Transform dialBody = knob.transform.Find("DialBody");
                float dialDiam = dialBody != null ? dialBody.localScale.x : 0.10f;

                // Circular graduated tick-mark faceplate stationary underneath dial body (rotated 180Y so 12-o'clock is -Z!)
                CreateVisualPrimitive(knob.transform, $"{VisualPrefix}ScaleTickPlate", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.002f, 0f), new Vector3(0f, 180f, 0f),
                    new Vector3(dialDiam * 1.48f, 0.0025f, dialDiam * 1.48f), assets.ScaleDialPlate);

                // Dark anodized collar lip below dial
                CreateVisualPrimitive(knob.transform, $"{VisualPrefix}KnobBaseCollar", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.005f, 0f), Vector3.zero,
                    new Vector3(dialDiam * 1.12f, 0.004f, dialDiam * 1.12f), assets.RecessDark);

                if (dialBody != null)
                {
                    StripOldVisualChildren(dialBody);
                    SwapMeshAndMat(dialBody, assets.Meshes.KnurledDial, assets.BrushedMetalDark);

                    // Brushed metal top crown cap rotating with DialBody
                    CreateVisualPrimitive(dialBody, $"{VisualPrefix}KnobMetalCap", PrimitiveType.Cylinder,
                        new Vector3(0f, 1.02f, 0f), Vector3.zero,
                        new Vector3(0.76f, 0.08f, 0.76f), assets.BrushedMetalLight);

                    // Crisp white radial indicator pointer line extending toward -Z (0 deg = 12 o'clock, -135..+135 deg arc)
                    CreateVisualPrimitive(dialBody, $"{VisualPrefix}PointerLine", PrimitiveType.Cube,
                        new Vector3(0f, 1.15f, -0.22f), Vector3.zero,
                        new Vector3(0.085f, 0.14f, 0.44f), assets.PointerWhite);
                }

                EditorUtility.SetDirty(knob.gameObject);
                count++;
            }

            return count;
        }

        private static int UpgradeSlidersInStation(StationController st, HardwareAssetSet assets)
        {
            int count = 0;
            Slider[] sliders = st.GetComponentsInChildren<Slider>(true);
            HashSet<Transform> upgradedDecks = new HashSet<Transform>();

            for (int i = 0; i < sliders.Length; i++)
            {
                Slider sl = sliders[i];
                if (sl == null) continue;

                // Upgrade physical backing console plate once per deck
                Transform deck = sl.transform.parent != null ? sl.transform.parent.parent : null;
                if (deck != null && upgradedDecks.Add(deck))
                {
                    StripOldVisualChildren(deck);
                    Transform backing = deck.Find("Backing");
                    if (backing != null)
                    {
                        SwapMeshAndMat(backing, assets.Meshes.ChamferedBezel, assets.BrushedMetalDark);
                        Vector3 bs = backing.localScale;
                        float sx = bs.x * 0.44f;
                        float sy = bs.y * 0.40f;
                        float sz = -bs.z * 0.52f;
                        Vector3[] screwOffsets =
                        {
                            new Vector3(-sx, +sy, sz), new Vector3(+sx, +sy, sz),
                            new Vector3(-sx, -sy, sz), new Vector3(+sx, -sy, sz)
                        };
                        for (int s = 0; s < 4; s++)
                        {
                            CreateVisualPrimitive(deck, $"{VisualPrefix}Screw_{s}", PrimitiveType.Cylinder,
                                screwOffsets[s], new Vector3(90f, 0f, 0f),
                                new Vector3(0.011f, 0.002f, 0.011f), assets.BrushedMetalLight);
                        }
                    }
                }

                // Upgrade slider UI graphics with crisp tick-mark strip + machined metallic fader handle + center white line
                StripOldVisualChildren(sl.transform);
                RectTransform slRt = sl.GetComponent<RectTransform>();
                if (slRt != null && assets.LinearScaleTicks != null)
                {
                    GameObject ticksGo = new GameObject($"{VisualPrefix}ScaleTicks");
                    ticksGo.transform.SetParent(sl.transform, false);
                    ticksGo.transform.SetAsFirstSibling();
                    RectTransform trt = ticksGo.AddComponent<RectTransform>();
                    trt.anchorMin = Vector2.zero;
                    trt.anchorMax = Vector2.one;
                    trt.offsetMin = new Vector2(-2f, -4f);
                    trt.offsetMax = new Vector2(2f, 4f);
                    RawImage rimg = ticksGo.AddComponent<RawImage>();
                    rimg.texture = assets.LinearScaleTicks;
                    rimg.raycastTarget = false;
                }

                if (sl.handleRect != null)
                {
                    StripOldVisualChildren(sl.handleRect);
                    sl.handleRect.sizeDelta = new Vector2(24f, 0f);
                    Image handleImg = sl.handleRect.GetComponent<Image>();
                    if (handleImg != null)
                    {
                        handleImg.color = new Color(0.16f, 0.20f, 0.25f, 1f);
                    }

                    // Metallic inner bar + white center hairline on fader handle
                    GameObject metalCap = new GameObject($"{VisualPrefix}HandleCap");
                    metalCap.transform.SetParent(sl.handleRect, false);
                    RectTransform mcRt = metalCap.AddComponent<RectTransform>();
                    mcRt.anchorMin = new Vector2(0.15f, 0.08f);
                    mcRt.anchorMax = new Vector2(0.85f, 0.92f);
                    mcRt.offsetMin = Vector2.zero;
                    mcRt.offsetMax = Vector2.zero;
                    Image mcImg = metalCap.AddComponent<Image>();
                    mcImg.color = new Color(0.68f, 0.74f, 0.82f, 1f);
                    mcImg.raycastTarget = false;

                    GameObject lineGo = new GameObject($"{VisualPrefix}HandleIndicator");
                    lineGo.transform.SetParent(sl.handleRect, false);
                    RectTransform lRt = lineGo.AddComponent<RectTransform>();
                    lRt.anchorMin = new Vector2(0.42f, 0.12f);
                    lRt.anchorMax = new Vector2(0.58f, 0.88f);
                    lRt.offsetMin = Vector2.zero;
                    lRt.offsetMax = Vector2.zero;
                    Image lImg = lineGo.AddComponent<Image>();
                    lImg.color = new Color(0.98f, 0.99f, 1f, 1f);
                    lImg.raycastTarget = false;
                }

                EditorUtility.SetDirty(sl.gameObject);
                count++;
            }

            return count;
        }

        private static void UpgradeBatteryCircuitAndLeads(
            StationController st, HardwareAssetSet assets, ref int batteries, ref int leads)
        {
            Transform bat = st.transform.Find("VariableDCBattery");
            if (bat != null)
            {
                StripOldVisualChildren(bat);

                Transform housing = bat.Find("BatteryHousing");
                if (housing != null)
                {
                    SwapMatOnly(housing, assets.BatteryCasing);
                    EditorUtility.SetDirty(housing.gameObject);
                }

                Transform topAccent = bat.Find("TopAccent");
                if (topAccent != null)
                {
                    SwapMeshAndMat(topAccent, assets.Meshes.ChamferedBezel, assets.BrushedMetalLight);
                }

                // Twin brass + insulated binding terminal posts
                UpgradeBatteryPost(bat.Find("Post_PosRed"), true, assets);
                UpgradeBatteryPost(bat.Find("Post_NegBlack"), false, assets);

                // Holsters
                UpgradeHolster(bat.Find("Lead_Red_Positive_Holster"), assets);
                UpgradeHolster(bat.Find("Lead_Black_Negative_Holster"), assets);

                // Pluggable leads
                if (UpgradeBatteryLead(bat.Find("Lead_Red_Positive"), true, assets)) leads++;
                if (UpgradeBatteryLead(bat.Find("Lead_Black_Negative"), false, assets)) leads++;

                batteries++;
            }

            Transform circuit = st.transform.Find("DiodeCircuitAndCutaway");
            if (circuit != null)
            {
                Transform deck = circuit.Find("CircuitDeck");
                if (deck != null)
                {
                    SwapMeshAndMat(deck, assets.Meshes.ChamferedBezel, assets.BrushedMetalDark);
                }

                Transform res = circuit.Find("SeriesResistor220Ohm");
                if (res != null)
                {
                    StripOldVisualChildren(res);
                    SwapMatOnly(res, assets.ResistorBody);
                    // Upgrade cylindrical core + 4 color code bands for 220 Ohm (Red, Red, Brown, Gold)
                    CreateVisualPrimitive(res, $"{VisualPrefix}ResistorCore", PrimitiveType.Cylinder,
                        Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(1.05f, 0.48f, 1.05f), assets.ResistorBody);
                    CreateVisualPrimitive(res, $"{VisualPrefix}Band1_Red", PrimitiveType.Cylinder,
                        new Vector3(0f, 0f, 0.28f), new Vector3(90f, 0f, 0f), new Vector3(1.12f, 0.06f, 1.12f), assets.ResistorRed);
                    CreateVisualPrimitive(res, $"{VisualPrefix}Band2_Red", PrimitiveType.Cylinder,
                        new Vector3(0f, 0f, 0.10f), new Vector3(90f, 0f, 0f), new Vector3(1.12f, 0.06f, 1.12f), assets.ResistorRed);
                    CreateVisualPrimitive(res, $"{VisualPrefix}Band3_Brown", PrimitiveType.Cylinder,
                        new Vector3(0f, 0f, -0.08f), new Vector3(90f, 0f, 0f), new Vector3(1.12f, 0.06f, 1.12f), assets.ResistorBrown);
                    CreateVisualPrimitive(res, $"{VisualPrefix}Band4_Gold", PrimitiveType.Cylinder,
                        new Vector3(0f, 0f, -0.28f), new Vector3(90f, 0f, 0f), new Vector3(1.12f, 0.06f, 1.12f), assets.ResistorGold);
                    CreateVisualPrimitive(res, $"{VisualPrefix}LeadWireF", PrimitiveType.Cylinder,
                        new Vector3(0f, -0.10f, 0.58f), new Vector3(90f, 0f, 0f), new Vector3(0.22f, 0.16f, 0.22f), assets.NickelTip);
                    CreateVisualPrimitive(res, $"{VisualPrefix}LeadWireB", PrimitiveType.Cylinder,
                        new Vector3(0f, -0.10f, -0.58f), new Vector3(90f, 0f, 0f), new Vector3(0.22f, 0.16f, 0.22f), assets.NickelTip);
                }

                Transform resTag = circuit.Find("ResistorTag");
                if (resTag != null)
                {
                    resTag.localPosition = new Vector3(0.390f, 0.036f, 0.138f);
                }

                Transform led = circuit.Find("IndicatorLED_Bulb");
                if (led != null)
                {
                    StripOldVisualChildren(led);
                    SwapMeshOnly(led, assets.Meshes.DomedLed);
                    // Internal metal anvil & reflector post inside domed bulb (scaled proportionally inside [-0.5..0.5])
                    CreateVisualPrimitive(led, $"{VisualPrefix}InternalPost", PrimitiveType.Cube,
                        new Vector3(-0.14f, -0.18f, 0f), Vector3.zero, new Vector3(0.16f, 0.42f, 0.12f), assets.NickelTip);
                    CreateVisualPrimitive(led, $"{VisualPrefix}InternalAnvil", PrimitiveType.Cube,
                        new Vector3(0.12f, -0.22f, 0f), Vector3.zero, new Vector3(0.22f, 0.34f, 0.12f), assets.NickelTip);
                }
            }
        }

        private static void UpgradeBatteryPost(Transform post, bool isPositive, HardwareAssetSet assets)
        {
            if (post == null) return;
            StripOldVisualChildren(post);
            SwapMeshAndMat(post, assets.Meshes.HexNut, assets.BrassMetal);
            post.localScale = new Vector3(0.044f, 0.018f, 0.044f);

            // Taller brass threaded shaft + fluted red/black insulated collar + central banana jack cavity
            CreateVisualPrimitive(post, $"{VisualPrefix}BrassShaft", PrimitiveType.Cylinder,
                new Vector3(0f, 1.15f, 0f), Vector3.zero, new Vector3(0.56f, 0.88f, 0.56f), assets.BrassMetal);
            Material collarMat = isPositive ? assets.LeadRedSatin : assets.LeadBlackSatin;
            CreateVisualCustomMesh(post, $"{VisualPrefix}ThumbCollar", assets.Meshes.KnurledDial,
                new Vector3(0f, 1.05f, 0f), Vector3.zero, new Vector3(0.84f, 0.44f, 0.84f), collarMat);
            CreateVisualPrimitive(post, $"{VisualPrefix}JackCavity", PrimitiveType.Cylinder,
                new Vector3(0f, 2.02f, 0f), Vector3.zero, new Vector3(0.32f, 0.06f, 0.32f), assets.RecessDark);

            EditorUtility.SetDirty(post.gameObject);
        }

        private static void UpgradeHolster(Transform holster, HardwareAssetSet assets)
        {
            if (holster == null) return;
            StripOldVisualChildren(holster);
            SwapMeshAndMat(holster, assets.Meshes.ChamferedBezel, assets.BrushedMetalDark);
            CreateVisualPrimitive(holster, $"{VisualPrefix}HolsterWell", PrimitiveType.Cylinder,
                new Vector3(0f, 0.46f, 0f), Vector3.zero, new Vector3(0.46f, 0.14f, 0.46f), assets.BrassMetal);
            CreateVisualPrimitive(holster, $"{VisualPrefix}HolsterBore", PrimitiveType.Cylinder,
                new Vector3(0f, 0.56f, 0f), Vector3.zero, new Vector3(0.30f, 0.08f, 0.30f), assets.RecessDark);
            EditorUtility.SetDirty(holster.gameObject);
        }

        private static bool UpgradeBatteryLead(Transform lead, bool isPositive, HardwareAssetSet assets)
        {
            if (lead == null) return false;
            StripOldVisualChildren(lead);

            Material insulMat = isPositive ? assets.LeadRedSatin : assets.LeadBlackSatin;

            Transform handle = lead.Find("PlugHandle");
            if (handle != null)
            {
                StripOldVisualChildren(handle);
                handle.localPosition = Vector3.zero;
                handle.localEulerAngles = new Vector3(90f, 0f, 0f);
                handle.localScale = new Vector3(0.036f, 0.028f, 0.036f);
                Mesh cylMesh = GetBuiltinMesh("Cylinder.fbx");
                if (cylMesh != null)
                {
                    SwapMeshAndMat(handle, cylMesh, insulMat);
                }
                else
                {
                    SwapMatOnly(handle, insulMat);
                }
            }

            // Finger-guard collar flange + 3-rib dark rubber flexible strain-relief boot leading back toward WireAttach
            CreateVisualPrimitive(lead, $"{VisualPrefix}FingerFlange", PrimitiveType.Cylinder,
                new Vector3(0f, 0f, -0.012f), new Vector3(90f, 0f, 0f), new Vector3(0.046f, 0.0045f, 0.046f), insulMat);

            for (int r = 0; r < 3; r++)
            {
                float t = r / 2f;
                float zOff = 0.027f + r * 0.007f;
                float yOff = r * 0.003f;
                float diam = Mathf.Lerp(0.028f, 0.018f, t);
                CreateVisualPrimitive(lead, $"{VisualPrefix}StrainRib_{r}", PrimitiveType.Cylinder,
                    new Vector3(0f, yOff, zOff), new Vector3(72f, 0f, 0f),
                    new Vector3(diam, 0.0028f, diam), assets.RubberDark);
            }

            Transform metalTip = lead.Find("MetalTip");
            if (metalTip != null)
            {
                StripOldVisualChildren(metalTip);
                metalTip.localPosition = new Vector3(0f, -0.015f, -0.025f);
                metalTip.localEulerAngles = new Vector3(45f, 0f, 0f);
                metalTip.localScale = new Vector3(0.014f, 0.018f, 0.014f);
                Mesh cylMesh = GetBuiltinMesh("Cylinder.fbx");
                if (cylMesh != null)
                {
                    SwapMeshAndMat(metalTip, cylMesh, assets.NickelTip);
                }
                else
                {
                    SwapMatOnly(metalTip, assets.NickelTip);
                }

                // Lantern spring contact sleeve + rounded plug tip nose
                CreateVisualPrimitive(metalTip, $"{VisualPrefix}LanternBand", PrimitiveType.Cylinder,
                    new Vector3(0f, -0.35f, 0f), Vector3.zero, new Vector3(1.16f, 0.28f, 1.16f), assets.BrassMetal);
                CreateVisualPrimitive(metalTip, $"{VisualPrefix}TipDome", PrimitiveType.Sphere,
                    new Vector3(0f, -1.0f, 0f), Vector3.zero, new Vector3(0.95f, 0.65f, 0.95f), assets.NickelTip);
            }

            Transform plugBadge = lead.Find("PlugBadge");
            if (plugBadge != null)
            {
                plugBadge.localPosition = new Vector3(0f, 0.039f, 0.010f);
            }

            EditorUtility.SetDirty(lead.gameObject);
            return true;
        }

        private static int UpgradeSampleDiodesAndAnalyzer(StationController st, HardwareAssetSet assets)
        {
            Transform az = st.transform.Find("IVAnalyzerConsole");
            if (az != null)
            {
                Transform deck = az.Find("AnalyzerHousing");
                if (deck != null)
                {
                    SwapMeshAndMat(deck, assets.Meshes.ChamferedBezel, assets.BrushedMetalDark);
                }
            }

            int count = 0;
            DiodeSampleModule[] diodes = st.GetComponentsInChildren<DiodeSampleModule>(true);
            for (int i = 0; i < diodes.Length; i++)
            {
                DiodeSampleModule dm = diodes[i];
                if (dm == null) continue;

                StripOldVisualChildren(dm.transform);

                // Low-profile brushed metal carrier baseplate cleanly inside (0.128, 0.058, 0.086) collider
                CreateVisualCustomMesh(dm.transform, $"{VisualPrefix}CarrierBase", assets.Meshes.ChamferedBezel,
                    new Vector3(0f, -0.022f, 0f), Vector3.zero, new Vector3(0.118f, 0.008f, 0.078f), assets.BrushedMetalDark);

                // Metallic terminal clips on PinP (+X anode) and PinN (-X cathode)
                Transform pinP = dm.transform.Find("PinP");
                Transform pinN = dm.transform.Find("PinN");
                if (pinP != null)
                {
                    pinP.localPosition = new Vector3(0.053f, -0.012f, 0f);
                    pinP.localScale = new Vector3(0.014f, 0.016f, 0.020f);
                    SwapMeshAndMat(pinP, assets.Meshes.ChamferedBezel, assets.NickelTip);
                }
                if (pinN != null)
                {
                    pinN.localPosition = new Vector3(-0.053f, -0.012f, 0f);
                    pinN.localScale = new Vector3(0.014f, 0.016f, 0.020f);
                    SwapMeshAndMat(pinN, assets.Meshes.ChamferedBezel, assets.NickelTip);
                }

                Transform pkg = dm.transform.Find("Package");
                if (pkg != null)
                {
                    StripOldVisualChildren(pkg);
                    Mesh cylMesh = GetBuiltinMesh("Cylinder.fbx");

                    if (dm.DiodeType == DiodeType.LED)
                    {
                        // Upright domed transparent/tinted LED package + visible internal anvil/post + bent leads
                        pkg.localPosition = new Vector3(0f, 0.012f, -0.006f);
                        pkg.localEulerAngles = Vector3.zero;
                        pkg.localScale = new Vector3(0.034f, 0.042f, 0.034f);
                        SwapMeshAndMat(pkg, assets.Meshes.DomedLed, assets.DiodeLedDome);

                        CreateVisualPrimitive(pkg, $"{VisualPrefix}InternalAnvil", PrimitiveType.Cube,
                            new Vector3(0.11f, -0.06f, 0f), Vector3.zero, new Vector3(0.20f, 0.44f, 0.14f), assets.BrassMetal);
                        CreateVisualPrimitive(pkg, $"{VisualPrefix}InternalPost", PrimitiveType.Cube,
                            new Vector3(-0.12f, -0.04f, 0f), Vector3.zero, new Vector3(0.14f, 0.48f, 0.14f), assets.NickelTip);

                        CreateVisualPrimitive(dm.transform, $"{VisualPrefix}LedLeadP", PrimitiveType.Cylinder,
                            new Vector3(0.032f, -0.012f, -0.006f), new Vector3(0f, 0f, 90f),
                            new Vector3(0.0045f, 0.022f, 0.0045f), assets.NickelTip);
                        CreateVisualPrimitive(dm.transform, $"{VisualPrefix}LedLeadN", PrimitiveType.Cylinder,
                            new Vector3(-0.032f, -0.012f, -0.006f), new Vector3(0f, 0f, 90f),
                            new Vector3(0.0045f, 0.022f, 0.0045f), assets.NickelTip);
                    }
                    else
                    {
                        // Sleek horizontal axial diode barrel (0.018m dia x 0.052m len) lifted above label strip
                        pkg.localPosition = new Vector3(0f, 0.008f, -0.006f);
                        pkg.localEulerAngles = new Vector3(0f, 0f, 90f);
                        pkg.localScale = new Vector3(0.019f, 0.026f, 0.019f);

                        Material bodyMat = dm.DiodeType == DiodeType.Silicon
                            ? assets.DiodeEpoxyBlack
                            : (dm.DiodeType == DiodeType.Germanium ? assets.DiodeGlassAmber : assets.DiodeGlassZener);
                        if (cylMesh != null) SwapMeshAndMat(pkg, cylMesh, bodyMat);
                        else SwapMatOnly(pkg, bodyMat);

                        // Prominent cathode polarity band near N-end (-X = +Y in +90Z rotated cylinder)
                        Material bandMat = dm.DiodeType == DiodeType.Silicon ? assets.BrushedMetalLight : assets.DiodeEpoxyBlack;
                        CreateVisualPrimitive(pkg, $"{VisualPrefix}CathodeBand", PrimitiveType.Cylinder,
                            new Vector3(0f, 0.62f, 0f), Vector3.zero, new Vector3(1.08f, 0.18f, 1.08f), bandMat);

                        // Internal semiconductor die & whisker inside translucent glass diodes (Ge & Zener)
                        if (dm.DiodeType == DiodeType.Germanium || dm.DiodeType == DiodeType.Zener)
                        {
                            CreateVisualPrimitive(pkg, $"{VisualPrefix}InternalDie", PrimitiveType.Cylinder,
                                new Vector3(0f, -0.18f, 0f), Vector3.zero, new Vector3(0.46f, 0.24f, 0.46f), assets.CopperBand);
                            CreateVisualPrimitive(pkg, $"{VisualPrefix}InternalWhisker", PrimitiveType.Cylinder,
                                new Vector3(0f, 0.34f, 0f), new Vector3(12f, 0f, 0f), new Vector3(0.16f, 0.46f, 0.16f), assets.BrassMetal);
                        }

                        // Tinned-copper axial leads + vertical bends into PinP / PinN clips
                        CreateVisualPrimitive(dm.transform, $"{VisualPrefix}AxialHoriz", PrimitiveType.Cylinder,
                            new Vector3(0f, 0.008f, -0.006f), new Vector3(0f, 0f, 90f),
                            new Vector3(0.0046f, 0.052f, 0.0046f), assets.NickelTip);
                        CreateVisualPrimitive(dm.transform, $"{VisualPrefix}AxialDropP", PrimitiveType.Cylinder,
                            new Vector3(0.051f, -0.002f, -0.003f), Vector3.zero,
                            new Vector3(0.0046f, 0.012f, 0.0046f), assets.NickelTip);
                        CreateVisualPrimitive(dm.transform, $"{VisualPrefix}AxialDropN", PrimitiveType.Cylinder,
                            new Vector3(-0.051f, -0.002f, -0.003f), Vector3.zero,
                            new Vector3(0.0046f, 0.012f, 0.0046f), assets.NickelTip);
                    }
                }

                // Clear front carrier lip plaque forward of axial component so zero overlap occurs
                Transform lbl = dm.transform.Find("SampleLbl");
                if (lbl != null)
                {
                    lbl.localPosition = new Vector3(0f, -0.014f, 0.024f);
                    lbl.localEulerAngles = new Vector3(90f, 180f, 0f);
                }

                EditorUtility.SetDirty(dm.gameObject);
                count++;
            }

            return count;
        }

        private static int UpgradeSemiconductorBlocksInStation(StationController st, HardwareAssetSet assets)
        {
            int count = 0;
            ExtrinsicSemiconductorBlock[] blocks = st.GetComponentsInChildren<ExtrinsicSemiconductorBlock>(true);
            for (int i = 0; i < blocks.Length; i++)
            {
                ExtrinsicSemiconductorBlock blk = blocks[i];
                if (blk == null) continue;

                StripOldVisualChildren(blk.transform);

                // Find inner dynamic block renderer ("BlockMesh" on St2/3 or "Mesh" on St4)
                Transform core = blk.transform.Find("BlockMesh") ?? blk.transform.Find("Mesh");
                if (core != null)
                {
                    Vector3 origScale = core.name == "BlockMesh"
                        ? new Vector3(0.140f, 0.080f, 0.140f)
                        : new Vector3(0.130f, 0.080f, 0.130f);

                    // Inset core slightly so glossy translucent outer crystal shell wraps cleanly inside original collider bounds
                    core.localScale = new Vector3(origScale.x * 0.80f, origScale.y * 0.82f, origScale.z * 0.80f);
                    SwapMeshAndMat(core, assets.Meshes.RoundedBlock, assets.BlockCore);

                    // Translucent quartz/crystal lattice shell matching exact block dimensions
                    CreateVisualCustomMesh(blk.transform, $"{VisualPrefix}CrystalHousingShell", assets.Meshes.RoundedBlock,
                        Vector3.zero, Vector3.zero, origScale, assets.CrystalShell);

                    // Brushed metallic ohmic end-contact plates on left & right faces
                    float endX = origScale.x * 0.50f - 0.003f;
                    Vector3 plateScale = new Vector3(0.009f, origScale.y * 0.82f, origScale.z * 0.78f);
                    CreateVisualCustomMesh(blk.transform, $"{VisualPrefix}ContactPlate_L", assets.Meshes.ChamferedBezel,
                        new Vector3(-endX, 0f, 0f), Vector3.zero, plateScale, assets.BrushedMetalLight);
                    CreateVisualCustomMesh(blk.transform, $"{VisualPrefix}ContactPlate_R", assets.Meshes.ChamferedBezel,
                        new Vector3(endX, 0f, 0f), Vector3.zero, plateScale, assets.BrushedMetalLight);
                }

                EditorUtility.SetDirty(blk.gameObject);
                count++;
            }

            return count;
        }

        private static void UpgradeFurnaceHousingVisual(StationController st, HardwareAssetSet assets)
        {
            Transform furn = st.transform.Find("DiffusionFurnace");
            if (furn == null) return;

            Transform deck = furn.Find("FurnaceDeck");
            if (deck != null)
            {
                SwapMeshAndMat(deck, assets.Meshes.ChamferedBezel, assets.BrushedMetalDark);
            }
        }

        private static void RefreshAllInteractionHighlightCaches()
        {
            InteractionFeedback[] feedbacks = Object.FindObjectsByType<InteractionFeedback>(FindObjectsInactive.Include);
            for (int i = 0; i < feedbacks.Length; i++)
            {
                InteractionFeedback fb = feedbacks[i];
                if (fb == null) continue;

                SerializedObject so = new SerializedObject(fb);
                SerializedProperty hr = so.FindProperty("highlightRenderers");
                if (hr == null) continue;

                // Keep existing renderers if valid, dropping nulls and skipping transparent outer crystal glass on blocks
                List<Renderer> valid = new List<Renderer>();
                for (int j = 0; j < hr.arraySize; j++)
                {
                    Renderer r = hr.GetArrayElementAtIndex(j).objectReferenceValue as Renderer;
                    if (r != null && !valid.Contains(r))
                    {
                        valid.Add(r);
                    }
                }

                if (valid.Count == 0)
                {
                    Renderer firstMesh = fb.GetComponentInChildren<MeshRenderer>(true);
                    if (firstMesh != null) valid.Add(firstMesh);
                }

                hr.arraySize = valid.Count;
                for (int j = 0; j < valid.Count; j++)
                {
                    hr.GetArrayElementAtIndex(j).objectReferenceValue = valid[j];
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fb);
            }
        }

        private static void ApplyProjectPrefabOverrides(LessonManager lm)
        {
            for (int i = 0; i < lm.Stations.Count; i++)
            {
                StationController st = lm.Stations[i];
                if (st == null) continue;
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

        private static void CreateButtonMicroIcon(Transform capOverlay, string buttonName)
        {
            string lower = buttonName.ToLowerInvariant();
            GameObject iconGo = new GameObject($"{VisualPrefix}MicroIcon");
            iconGo.transform.SetParent(capOverlay, false);
            RectTransform rt = iconGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);

            // Place a sleek backlit indicator LED strip at the upper rim of the button cap overlay
            bool isToggle = lower.Contains("toggle") || lower.Contains("autosweep") || lower.Contains("ideal");
            rt.sizeDelta = isToggle ? new Vector2(28f, 6f) : new Vector2(18f, 4f);
            rt.anchoredPosition = new Vector2(0f, 18f);

            Image img = iconGo.AddComponent<Image>();
            img.color = isToggle ? new Color(0.75f, 1.0f, 0.88f, 0.92f) : new Color(0.95f, 0.98f, 1f, 0.72f);
            img.raycastTarget = false;
        }

        private static string ResolveButtonColorKey(Transform cap)
        {
            MeshRenderer mr = cap.GetComponent<MeshRenderer>();
            string matName = mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name.ToLowerInvariant() : string.Empty;
            if (matName.Contains("green")) return "Green";
            if (matName.Contains("blue") || matName.Contains("ntype") || matName.Contains("electron")) return "Blue";
            if (matName.Contains("amber")) return "Amber";
            if (matName.Contains("red") || matName.Contains("ptype") || matName.Contains("hole")) return "Red";
            if (matName.Contains("teal") || matName.Contains("ion")) return "Teal";
            if (matName.Contains("gold")) return "Gold";
            return "Blue";
        }

        private static Material ResolveDopantGranularMaterial(string symbol, HardwareAssetSet assets)
        {
            switch (symbol)
            {
                case "P": return assets.DopantAmberRed;
                case "As": return assets.DopantOrange;
                case "Sb": return assets.DopantGoldSilver;
                case "B": return assets.DopantTeal;
                case "Al": return assets.DopantSilver;
                case "Ga": return assets.DopantGalliumBlue;
                case "In": return assets.DopantSilver;
                case "Ge":
                case "C":
                default: return assets.DopantSlate;
            }
        }

        private static void StripOldVisualChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith(VisualPrefix, StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void SwapMeshAndMat(Transform tr, Mesh mesh, Material mat)
        {
            SwapMeshOnly(tr, mesh);
            SwapMatOnly(tr, mat);
        }

        private static void SwapMeshOnly(Transform tr, Mesh mesh)
        {
            MeshFilter mf = tr.GetComponent<MeshFilter>();
            if (mf != null && mesh != null)
            {
                mf.sharedMesh = mesh;
                EditorUtility.SetDirty(mf);
            }
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

        private static GameObject CreateVisualPrimitive(
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

            ConfigureVisualTransform(go, parent, localPos, localEuler, localScale, mat);
            return go;
        }

        private static GameObject CreateVisualCustomMesh(
            Transform parent, string name, Mesh mesh,
            Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
        {
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            ConfigureVisualTransform(go, parent, localPos, localEuler, localScale, mat);
            return go;
        }

        private static void ConfigureVisualTransform(
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

        private static Mesh GetBuiltinMesh(string name)
        {
            return Resources.GetBuiltinResource<Mesh>(name);
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c.name == targetName) return c;
                Transform nested = FindChildRecursive(c, targetName);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
