using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PNJunctionLab.Editor
{
    /// <summary>
    /// Shared Editor utilities for crisp world-space TextMeshPro typography, mipmapped SDF font generation,
    /// native DPI GameView preview, world-space Canvas creation, and SerializedObject wiring.
    /// </summary>
    public static class LabTextStyleHelper
    {
        public const string UiFolder = "Assets/PNJunctionLab/UI";
        public const string CrispFontAssetPath = UiFolder + "/LiberationSans_Crisp_SDF.asset";
        private const string SourceTtfPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";

        private const string PreloadCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "\u00B1\u00B0\u00B5\u03A9\u00B7\u00D7\u00F7\u2212\u2264\u2265\u2248\u2192\u2194\u2014\u2013\u2022\u00B2\u00B3\u221E";

        private static TMP_FontAsset cachedFontAsset;

        [MenuItem("PNJunctionLab/Rebuild Crisp Font Asset")]
        public static void RebuildCrispFontMenu()
        {
            cachedFontAsset = null;
            if (File.Exists(CrispFontAssetPath))
            {
                AssetDatabase.DeleteAsset(CrispFontAssetPath);
            }

            TMP_FontAsset asset = GetOrCreateCrispFontAsset();
            UnityEngine.Debug.Log(asset != null
                ? "Created mipmapped crisp SDF font asset: " + CrispFontAssetPath
                : "Failed to create crisp SDF font asset.");
        }

        /// <summary>
        /// Ensures Unity's GameView renders at 1:1 native OS display resolution (no 1.25x/1.5x low-res blur).
        /// </summary>
        public static void EnsureNativeGameViewDPI()
        {
            System.Type gvType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (gvType == null) return;

            Object[] gvs = Resources.FindObjectsOfTypeAll(gvType);
            if (gvs == null) return;

            FieldInfo lowResField = gvType.GetField("m_LowResolutionForAspectRatios",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (lowResField == null) return;

            for (int i = 0; i < gvs.Length; i++)
            {
                EditorWindow gv = gvs[i] as EditorWindow;
                if (gv == null) continue;
                bool[] arr = lowResField.GetValue(gv) as bool[];
                if (arr != null)
                {
                    bool changed = false;
                    for (int k = 0; k < arr.Length; k++)
                    {
                        if (arr[k])
                        {
                            arr[k] = false;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        lowResField.SetValue(gv, arr);
                        gv.Repaint();
                    }
                }
            }
        }

        public static TMP_FontAsset GetOrCreateCrispFontAsset()
        {
            EnsureNativeGameViewDPI();

            if (cachedFontAsset != null)
            {
                return cachedFontAsset;
            }

            if (!AssetDatabase.IsValidFolder(UiFolder))
            {
                AssetDatabase.CreateFolder("Assets/PNJunctionLab", "UI");
            }

            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CrispFontAssetPath);
            if (existing != null && existing.material != null && existing.atlasTexture != null)
            {
                EnsureMipmappedAtlas(existing, CrispFontAssetPath);
                ConfigureCrispFontMaterial(existing);
                EnsureFallbackFont(existing);
                EditorUtility.SetDirty(existing);
                if (existing.material != null) EditorUtility.SetDirty(existing.material);
                if (existing.atlasTexture != null) EditorUtility.SetDirty(existing.atlasTexture);
                AssetDatabase.SaveAssets();
                cachedFontAsset = existing;
                return cachedFontAsset;
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceTtfPath);
            if (sourceFont == null)
            {
                return TMP_Settings.defaultFontAsset;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                96,
                12,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                false);

            if (fontAsset == null)
            {
                return TMP_Settings.defaultFontAsset;
            }

            fontAsset.name = "LiberationSans_Crisp_SDF";
            fontAsset.TryAddCharacters(PreloadCharacters, out _);

            AssetDatabase.CreateAsset(fontAsset, CrispFontAssetPath);
            EnsureMipmappedAtlas(fontAsset, CrispFontAssetPath);
            ConfigureCrispFontMaterial(fontAsset);
            EnsureFallbackFont(fontAsset);

            if (fontAsset.material != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(fontAsset.material)))
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            if (fontAsset.material != null) EditorUtility.SetDirty(fontAsset.material);
            if (fontAsset.atlasTexture != null) EditorUtility.SetDirty(fontAsset.atlasTexture);
            AssetDatabase.SaveAssets();

            cachedFontAsset = fontAsset;
            return cachedFontAsset;
        }

        private static void EnsureMipmappedAtlas(TMP_FontAsset fontAsset, string assetPath)
        {
            if (fontAsset == null || fontAsset.atlasTexture == null)
            {
                return;
            }

            Texture2D cur = fontAsset.atlasTexture;
            if (cur.mipmapCount > 1)
            {
                cur.filterMode = FilterMode.Trilinear;
                cur.anisoLevel = 8;
                return;
            }

            // Build a true mipmapped Alpha8 SDF atlas so far/angled world-space text never shimmers under minification.
            Texture2D mipTex = new Texture2D(cur.width, cur.height, cur.format, true, true);
            mipTex.name = "LiberationSans_Crisp_SDF Atlas";
            mipTex.filterMode = FilterMode.Trilinear;
            mipTex.anisoLevel = 8;
            mipTex.SetPixelData(cur.GetRawTextureData(), 0);
            mipTex.Apply(true, false);

            Object.DestroyImmediate(cur, true);
            AssetDatabase.AddObjectToAsset(mipTex, assetPath);

            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0)
            {
                fontAsset.atlasTextures = new[] { mipTex };
            }
            else
            {
                fontAsset.atlasTextures[0] = mipTex;
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.mainTexture = mipTex;
            }
        }

        private static void EnsureFallbackFont(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null || TMP_Settings.defaultFontAsset == null || TMP_Settings.defaultFontAsset == fontAsset)
            {
                return;
            }

            if (fontAsset.fallbackFontAssetTable == null)
            {
                fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            }

            if (!fontAsset.fallbackFontAssetTable.Contains(TMP_Settings.defaultFontAsset))
            {
                fontAsset.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
            }
        }

        private static void ConfigureCrispFontMaterial(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null || fontAsset.material == null)
            {
                return;
            }

            Shader ssdShader = Shader.Find("TextMeshPro/Distance Field SSD");
            if (ssdShader != null && fontAsset.material.shader != ssdShader)
            {
                fontAsset.material.shader = ssdShader;
            }

            Material mat = fontAsset.material;
            mat.name = "LiberationSans_Crisp_SDF Material";
            if (fontAsset.atlasTexture != null)
            {
                mat.mainTexture = fontAsset.atlasTexture;
            }

            // Smooth anti-aliased edge (_Sharpness ~ -0.05) + gentle stem reinforcement (_FaceDilate / _WeightNormal)
            // eliminates binary jagged pixels and flickering thin strokes in world-space VR/desktop views.
            if (mat.HasProperty("_FaceDilate")) mat.SetFloat("_FaceDilate", 0.06f);
            if (mat.HasProperty("_WeightNormal")) mat.SetFloat("_WeightNormal", 0.18f);
            if (mat.HasProperty("_WeightBold")) mat.SetFloat("_WeightBold", 0.55f);
            if (mat.HasProperty("_Sharpness")) mat.SetFloat("_Sharpness", -0.05f);
            if (mat.HasProperty("_PerspectiveFilter")) mat.SetFloat("_PerspectiveFilter", 0.875f);

            if (fontAsset.atlasTexture != null)
            {
                fontAsset.atlasTexture.filterMode = FilterMode.Trilinear;
                fontAsset.atlasTexture.anisoLevel = 8;
            }
        }

        public static void ApplyCrispStyle(TMP_Text tmp)
        {
            if (tmp == null)
            {
                return;
            }

            TMP_FontAsset crisp = GetOrCreateCrispFontAsset();
            if (crisp != null)
            {
                tmp.font = crisp;
                if (crisp.material != null)
                {
                    tmp.fontSharedMaterial = crisp.material;
                }
            }

            tmp.extraPadding = true;
        }

        public static Canvas CreateWorldCanvas(
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
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = eventCamera;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.dynamicPixelsPerUnit = 15f;
            scaler.referencePixelsPerUnit = 100f;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(pxWidth, pxHeight);
            rt.localScale = new Vector3(worldSize.x / pxWidth, worldSize.y / pxHeight, 1f);

            if (interactive)
            {
                go.AddComponent<GraphicRaycaster>();
                go.AddComponent<TrackedDeviceGraphicRaycaster>();
            }

            return canvas;
        }

        public static TMP_Text CreateTMPText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Color color,
            TextAlignmentOptions alignment,
            FontStyles style,
            bool raycastTarget = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            ApplyCrispStyle(tmp);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = raycastTarget;

            RectTransform rt = tmp.rectTransform;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return tmp;
        }

        public static void SetObjRef(SerializedObject so, string propName, Object value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null) p.objectReferenceValue = value;
        }

        public static void SetBool(SerializedObject so, string propName, bool value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null) p.boolValue = value;
        }

        public static void SetStr(SerializedObject so, string propName, string value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null) p.stringValue = value;
        }

        public static void SetFloat(SerializedObject so, string propName, float value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null) p.floatValue = value;
        }

        public static void SetObjectArray<T>(SerializedObject so, string propName, T[] items) where T : Object
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop == null) return;
            prop.arraySize = items != null ? items.Length : 0;
            if (items == null) return;
            for (int i = 0; i < items.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }

        public static void SetObjectList<T>(SerializedObject so, string propName, List<T> items) where T : Object
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop == null) return;
            prop.arraySize = items != null ? items.Count : 0;
            if (items == null) return;
            for (int i = 0; i < items.Count; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }
    }
}
