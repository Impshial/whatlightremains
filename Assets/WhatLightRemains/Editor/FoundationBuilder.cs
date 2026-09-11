using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WhatLightRemains.Runtime;
using Object = UnityEngine.Object;

namespace WhatLightRemains.Editor
{
    /// <summary>
    /// Creates the complete prototype foundation from Unity-owned primitives. All assets this
    /// class replaces live below Assets/WhatLightRemains/Generated.
    /// </summary>
    public static class FoundationBuilder
    {
        private const string GeneratedRoot = "Assets/WhatLightRemains/Generated";
        private const string MaterialsFolder = GeneratedRoot + "/Materials";
        private const string PrefabsFolder = GeneratedRoot + "/Prefabs";
        private const string InputFolder = GeneratedRoot + "/Input";
        private const string ScenesFolder = GeneratedRoot + "/Scenes";

        private const string GlassMaterialPath = MaterialsFolder + "/ClearGlass.mat";
        private const string FloorMaterialPath = MaterialsFolder + "/Floor.mat";
        private const string StripMaterialPath = MaterialsFolder + "/LightStrip.mat";
        private const string StripHousingMaterialPath = MaterialsFolder + "/LightStripHousing.mat";
        private const string BaseRailMaterialPath = MaterialsFolder + "/BaseRail.mat";
        private const string RoomPreviewMaterialPath = MaterialsFolder + "/RoomPreview.mat";
        private const string RoomDeleteOutlineMaterialPath = MaterialsFolder + "/RoomDeleteOutline.mat";
        private const string HandMaterialPath = MaterialsFolder + "/Hand.mat";
        private const string ToolMaterialPath = MaterialsFolder + "/Tool.mat";
        private const string TestMaterialPath = MaterialsFolder + "/ValidationSurface.mat";
        private const string ToolIconTexturePath = MaterialsFolder + "/ToolIcon.asset";
        private const string InputActionsPath = InputFolder + "/WhatLightRemainsInput.inputactions";
        private const string LegacyNoPostProcessingSettingsFolder = GeneratedRoot + "/Settings";
        private const string CubePrefabPath = PrefabsFolder + "/CubeRoom.prefab";
        private const string PlayerPrefabPath = PrefabsFolder + "/Player.prefab";
        private const string HotbarPrefabPath = PrefabsFolder + "/Hotbar.prefab";
        public const string MainMenuScenePath = ScenesFolder + "/MainMenu.unity";
        public const string FoundationScenePath = ScenesFolder + "/Foundation.unity";
        public const string ValidationScenePath = ScenesFolder + "/Validation.unity";

        private const string FloorBaseColorTexturePath = "Assets/WhatLightRemains/Art/Textures/Floor/Floor_BaseColor.png";
        private const string FloorNormalTexturePath = "Assets/WhatLightRemains/Art/Textures/Floor/Floor_Normal.png";
        private const string GlassDetailTexturePath = "Assets/WhatLightRemains/Art/Textures/Glass/Glass_Detail.png";
        private const string GlassNormalTexturePath = "Assets/WhatLightRemains/Art/Textures/Glass/Glass_Normal.png";
        private const string MenuArtworkTexturePath = "Assets/WhatLightRemains/Art/Menu/WhatLightRemainsTitle.png";
        private const string MenuFontPath = "Assets/WhatLightRemains/Art/Fonts/Raleway-Light.otf";

        private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string PcPipelinePath = "Assets/Settings/PC_RPAsset.asset";

        private static readonly Color NeutralWhite = new Color(0.94f, 0.97f, 1f, 1f);
        private readonly struct StripEmitterDefinition
        {
            public StripEmitterDefinition(string name, Vector3 position, Vector3 direction)
            {
                Name = name;
                Position = position;
                Direction = direction.normalized;
            }

            public string Name { get; }
            public Vector3 Position { get; }
            public Vector3 Direction { get; }
        }

        private static readonly StripEmitterDefinition[] StripEmitterDefinitions = CreateStripEmitterDefinitions();

        [MenuItem("Tools/What Light Remains/Build Foundation", priority = 10)]
        public static void BuildAll()
        {
            TryBuildAll();
        }

        private static bool TryBuildAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("What Light Remains foundation build cancelled; open scene changes were not saved.");
                return false;
            }

            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                EnsureFolders();
                RemoveLegacyGeneratedAssets();
                LayerIds layers = ConfigureLayers();
                ConfigurePcPipeline();

                Texture2D floorBaseColor = ConfigureSurfaceTexture(FloorBaseColorTexturePath, false);
                Texture2D floorNormal = ConfigureSurfaceTexture(FloorNormalTexturePath, true);
                Texture2D glassDetail = ConfigureSurfaceTexture(GlassDetailTexturePath, false);
                Texture2D glassNormal = ConfigureSurfaceTexture(GlassNormalTexturePath, true);
                Texture2D menuArtwork = ConfigureMenuArtwork(MenuArtworkTexturePath);
                Font menuFont = AssetDatabase.LoadAssetAtPath<Font>(MenuFontPath);
                if (menuFont == null) throw new InvalidOperationException("Missing main-menu font: " + MenuFontPath);

                Material glass = CreateGlassMaterial(glassDetail, glassNormal);
                Material floor = CreateFloorMaterial(floorBaseColor, floorNormal);
                Material strip = CreateStripMaterial();
                Material stripHousing = CreateStripHousingMaterial();
                Material baseRail = CreateBaseRailMaterial();
                Material roomPreview = CreateRoomPreviewMaterial();
                Material roomDeleteOutline = CreateRoomDeleteOutlineMaterial();
                Material hand = CreateLitMaterial(HandMaterialPath, new Color(0.62f, 0.31f, 0.20f, 1f), 0.38f);
                Material tool = CreateLitMaterial(ToolMaterialPath, new Color(0.18f, 0.24f, 0.32f, 1f), 0.62f);
                Material validation = CreateLitMaterial(TestMaterialPath, new Color(0.50f, 0.51f, 0.53f, 1f), 0.30f);
                InputActionAsset inputActions = CreateInputActions();
                Sprite toolIcon = CreateToolIcon();

                GameObject cubePrefab = CreateCubePrefab(glass, floor, strip, stripHousing, baseRail);
                GameObject playerPrefab = CreatePlayerPrefab(inputActions, hand, tool, roomPreview, roomDeleteOutline, layers);
                GameObject hotbarPrefab = CreateHotbarPrefab(toolIcon);

                CreateMainMenuScene(menuArtwork, menuFont);
                CreateFoundationScene(cubePrefab, playerPrefab, hotbarPrefab);
                CreateValidationScene(cubePrefab, playerPrefab, hotbarPrefab, validation);
                ConfigureBuildScenes();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("What Light Remains foundation generated successfully. Open " + MainMenuScenePath + " to test the startup flow.");
                return true;
            }
            finally
            {
                if (previousSetup.Length > 0 && previousSetup.All(item => !string.IsNullOrEmpty(item.path)))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }
        }

        [MenuItem("Tools/What Light Remains/Build Windows 64", priority = 20)]
        public static void BuildWindows64()
        {
            if (!TryBuildAll())
            {
                return;
            }

            string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/Windows"));
            BuildWindowsPlayer(
                new[] { MainMenuScenePath, FoundationScenePath },
                Path.Combine(outputDirectory, "WhatLightRemains.exe"),
                "Windows build");
        }

        [MenuItem("Tools/What Light Remains/Build Validation Fixture (Windows 64)", priority = 21)]
        public static void BuildValidationWindows64()
        {
            if (!TryBuildAll())
            {
                return;
            }

            string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/Validation"));
            BuildWindowsPlayer(
                new[] { ValidationScenePath },
                Path.Combine(outputDirectory, "WhatLightRemainsValidation.exe"),
                "Validation fixture build");
        }

        private static void BuildWindowsPlayer(string[] scenes, string outputPath, string label)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            }

            Debug.Log($"{label} complete: {options.locationPathName} ({report.summary.totalSize} bytes)");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/WhatLightRemains");
            EnsureFolder(GeneratedRoot);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(InputFolder);
            EnsureFolder(ScenesFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException("Cannot create asset folder without a parent: " + path);
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void RemoveLegacyGeneratedAssets()
        {
            // Unity 6 owns and seeds the global URP default volume profile. Camera-level
            // post-processing settings are the durable way to keep this prototype unprocessed.
            if (AssetDatabase.IsValidFolder(LegacyNoPostProcessingSettingsFolder))
            {
                AssetDatabase.DeleteAsset(LegacyNoPostProcessingSettingsFolder);
            }
        }

        private static LayerIds ConfigureLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0)
            {
                throw new InvalidOperationException("Could not load ProjectSettings/TagManager.asset.");
            }

            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty layerArray = tagManager.FindProperty("layers");
            int player = EnsureLayer(layerArray, "Player", 8);
            int viewModel = EnsureLayer(layerArray, "ViewModel", 9);
            int roomVolume = EnsureLayer(layerArray, "RoomVolume", 10);
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            return new LayerIds(player, viewModel, roomVolume);
        }

        private static int EnsureLayer(SerializedProperty layers, string name, int preferredIndex)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == name)
                {
                    return i;
                }
            }

            if (preferredIndex < layers.arraySize && string.IsNullOrEmpty(layers.GetArrayElementAtIndex(preferredIndex).stringValue))
            {
                layers.GetArrayElementAtIndex(preferredIndex).stringValue = name;
                return preferredIndex;
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = name;
                    return i;
                }
            }

            throw new InvalidOperationException("No free user layer is available for " + name + ".");
        }

        private static void ConfigurePcPipeline()
        {
            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PcPipelinePath);
            ScriptableRendererData renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRendererPath);
            if (pipeline == null || renderer == null)
            {
                throw new InvalidOperationException("The Universal 3D template PC pipeline assets were not found under Assets/Settings.");
            }

            SerializedObject rendererSettings = new SerializedObject(renderer);
            SetIntIfPresent(rendererSettings, "m_RenderingMode", 2); // UniversalRendererData.RenderingMode.ForwardPlus
            SetBoolIfPresent(rendererSettings, "m_ShadowTransparentReceive", false);
            RemoveAmbientOcclusionRendererFeatures(rendererSettings);

            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);

            SerializedObject pipelineSettings = new SerializedObject(pipeline);
            SetBoolIfPresent(pipelineSettings, "m_SupportsHDR", true);
            SetBoolIfPresent(pipelineSettings, "m_RequireOpaqueTexture", true);
            SetIntIfPresent(pipelineSettings, "m_OpaqueDownsampling", 0);
            SetBoolIfPresent(pipelineSettings, "m_AdditionalLightShadowsSupported", true);
            SetBoolIfPresent(pipelineSettings, "m_SoftShadowsSupported", true);
            // Eight point lights require 48 shadow-map faces. A 4K atlas keeps their
            // per-light Low (256 px) setting intact for both gameplay camera stacks.
            SetIntIfPresent(pipelineSettings, "m_AdditionalLightsShadowmapResolution", 4096);
            SetIntIfPresent(pipelineSettings, "m_AdditionalLightsShadowResolutionTierLow", 256);
            SetFloatIfPresent(pipelineSettings, "m_ShadowDistance", 24f);
            SerializedProperty defaultVolumeProfile = pipelineSettings.FindProperty("m_VolumeProfile");
            if (defaultVolumeProfile != null)
            {
                defaultVolumeProfile.objectReferenceValue = null;
            }

            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

#pragma warning disable CS0618
            GraphicsSettings.defaultRenderPipeline = pipeline;
#pragma warning restore CS0618
            QualitySettings.renderPipeline = pipeline;
        }

        private static void RemoveAmbientOcclusionRendererFeatures(SerializedObject rendererSettings)
        {
            SerializedProperty features = rendererSettings.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = rendererSettings.FindProperty("m_RendererFeatureMap");
            if (features == null || !features.isArray)
            {
                return;
            }

            for (int index = features.arraySize - 1; index >= 0; index--)
            {
                Object feature = features.GetArrayElementAtIndex(index).objectReferenceValue;
                if (feature == null || feature.GetType().Name.IndexOf("AmbientOcclusion", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                features.DeleteArrayElementAtIndex(index);
                if (featureMap != null && featureMap.isArray && index < featureMap.arraySize)
                {
                    featureMap.DeleteArrayElementAtIndex(index);
                }
            }
        }

        private static void SetBoolIfPresent(SerializedObject target, string propertyName, bool value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetIntIfPresent(SerializedObject target, string propertyName, int value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetFloatIfPresent(SerializedObject target, string propertyName, float value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static Texture2D ConfigureSurfaceTexture(string path, bool isNormalMap)
        {
            string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    "A required checked-in surface texture is missing. Restore it before rebuilding the foundation.",
                    absolutePath);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Could not configure texture importer for " + path + ".");
            }

            TextureImporterType expectedType = isNormalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            bool requiresImport = importer.textureType != expectedType
                || importer.sRGBTexture == isNormalMap
                || importer.wrapMode != TextureWrapMode.Repeat
                || importer.filterMode != FilterMode.Trilinear
                || !importer.mipmapEnabled
                || !importer.streamingMipmaps
                || importer.anisoLevel != 8
                || importer.isReadable
                || importer.maxTextureSize != 4096
                || importer.npotScale != TextureImporterNPOTScale.None
                || importer.textureCompression != TextureImporterCompression.CompressedHQ;

            if (requiresImport)
            {
                importer.textureType = expectedType;
                importer.sRGBTexture = !isNormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = true;
                importer.anisoLevel = 8;
                importer.isReadable = false;
                importer.maxTextureSize = 4096;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                throw new InvalidOperationException("Failed to import required surface texture: " + path);
            }

            return texture;
        }

        private static Texture2D ConfigureMenuArtwork(string path)
        {
            string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException("The checked-in main-menu artwork is missing.", absolutePath);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Could not configure main-menu artwork at " + path + ".");
            }

            bool requiresImport = importer.textureType != TextureImporterType.Default
                || !importer.sRGBTexture
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.filterMode != FilterMode.Bilinear
                || importer.mipmapEnabled
                || importer.isReadable
                || importer.maxTextureSize != 4096
                || importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (requiresImport)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                throw new InvalidOperationException("Failed to import main-menu artwork: " + path);
            }

            return texture;
        }

        private static Material CreateFloorMaterial(Texture2D baseColor, Texture2D normal)
        {
            Material material = CreateLitMaterial(FloorMaterialPath, Color.white, 0.28f);
            SetTexture(material, "_BaseMap", baseColor, Vector2.one);
            SetTexture(material, "_BumpMap", normal, Vector2.one);
            SetFloat(material, "_BumpScale", 0.5f);
            SetFloat(material, "_Metallic", 0f);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateGlassMaterial(Texture2D detail, Texture2D normal)
        {
            Material material = LoadOrCreateMaterial(
                GlassMaterialPath,
                "What Light Remains/Light-Transmitting Glass");
            Vector2 glassTiling = Vector2.one;
            SetTexture(material, "_BaseMap", detail, glassTiling);
            SetTexture(material, "_BumpMap", normal, glassTiling);
            SetColor(material, "_Tint", new Color(0.22f, 0.30f, 0.34f, 1f));
            SetColor(material, "_GlassTint", new Color(0.10f, 0.14f, 0.16f, 1f));
            SetFloat(material, "_ImperfectionStrength", 0.004f);
            SetFloat(material, "_NormalDetailStrength", 1f);
            SetFloat(material, "_DetailContrast", 4f);
            SetFloat(material, "_DetailThreshold", 0.02f);
            SetFloat(material, "_HighPassMip", 5f);
            SetFloat(material, "_DistortionPixels", 2.5f);
            SetFloat(material, "_DistortionBlend", 0.55f);
            SetFloat(material, "_DistortionMip", 4f);
            SetFloat(material, "_Translucency", 0.035f);
            SetFloat(material, "_Cull", (float)CullMode.Off);
            SetFloat(material, "_SrcBlend", (float)BlendMode.One);
            SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_ZWrite", 0f);

            // Clear keywords left by the previous URP/Lit material. This shader's sole
            // unlit pass samples the already-lit opaque scene for subtle refraction and cannot
            // receive point lights, cast shadows, or create reflection-like highlights.
            material.shaderKeywords = Array.Empty<string>();
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateStripMaterial()
        {
            Material material = LoadOrCreateMaterial(StripMaterialPath, "Universal Render Pipeline/Unlit");
            Color brightCore = new Color(NeutralWhite.r * 6f, NeutralWhite.g * 6f, NeutralWhite.b * 6f, 1f);
            SetColor(material, "_BaseColor", brightCore);
            SetColor(material, "_Color", brightCore);
            SetFloat(material, "_Surface", 0f);
            SetFloat(material, "_Blend", 0f);
            SetFloat(material, "_Cull", (float)CullMode.Back);
            SetFloat(material, "_AlphaClip", 0f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.One);
            SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
            SetFloat(material, "_ZWrite", 1f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Geometry;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateStripHousingMaterial()
        {
            Material material = CreateLitMaterial(
                StripHousingMaterialPath,
                new Color(0.025f, 0.035f, 0.045f, 1f),
                0.42f);
            SetFloat(material, "_Metallic", 0.65f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateBaseRailMaterial()
        {
            // The room is intentionally very dark, so the glass/floor seam needs a
            // separate mid-charcoal material instead of inheriting the near-black strip
            // housing. It remains non-emissive and responds naturally to room lighting.
            Material material = CreateLitMaterial(
                BaseRailMaterialPath,
                new Color(0.18f, 0.21f, 0.24f, 1f),
                0.48f);
            SetFloat(material, "_Metallic", 0.58f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateRoomPreviewMaterial()
        {
            Material material = LoadOrCreateMaterial(RoomPreviewMaterialPath, "Universal Render Pipeline/Unlit");
            Color hologramGreen = new Color(0.04f, 2.2f, 0.32f, 0.28f);
            SetColor(material, "_BaseColor", hologramGreen);
            SetColor(material, "_Color", hologramGreen);
            SetFloat(material, "_Surface", 1f);
            SetFloat(material, "_Blend", 0f);
            SetFloat(material, "_Cull", (float)CullMode.Off);
            SetFloat(material, "_AlphaClip", 0f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_ZWrite", 0f);
            // URP rebuilds a transparent material's render queue from this property
            // when it imports the asset. Keep it in sync with the explicit queue so
            // the ghost remains ordered after the room glass across editor reloads.
            SetFloat(material, "_QueueOffset", 100f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent + 100;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateRoomDeleteOutlineMaterial()
        {
            Material material = LoadOrCreateMaterial(RoomDeleteOutlineMaterialPath, "Universal Render Pipeline/Unlit");
            Color deletionRed = new Color(5f, 0.015f, 0.01f, 1f);
            SetColor(material, "_BaseColor", deletionRed);
            SetColor(material, "_Color", deletionRed);
            SetFloat(material, "_Surface", 0f);
            SetFloat(material, "_Blend", 0f);
            SetFloat(material, "_Cull", (float)CullMode.Off);
            SetFloat(material, "_AlphaClip", 0f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.One);
            SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
            SetFloat(material, "_ZWrite", 1f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Geometry + 20;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateLitMaterial(string path, Color color, float smoothness)
        {
            Material material = LoadOrCreateMaterial(path, "Universal Render Pipeline/Lit");
            SetColor(material, "_BaseColor", color);
            SetFloat(material, "_Surface", 0f);
            SetFloat(material, "_Blend", 0f);
            SetFloat(material, "_Cull", (float)CullMode.Back);
            SetFloat(material, "_AlphaClip", 0f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.One);
            SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
            SetFloat(material, "_ZWrite", 1f);
            SetFloat(material, "_Metallic", 0f);
            SetFloat(material, "_Smoothness", smoothness);
            SetFloat(material, "_ReceiveShadows", 1f);
            SetColor(material, "_EmissionColor", Color.black);
            material.SetOverrideTag("RenderType", "Opaque");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_EMISSION");
            material.renderQueue = (int)RenderQueue.Geometry;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException("Required shader was not found: " + shaderName);
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            return material;
        }

        private static void SetFloat(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetColor(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetTexture(Material material, string propertyName, Texture texture, Vector2 scale)
        {
            if (!material.HasProperty(propertyName))
            {
                return;
            }

            material.SetTexture(propertyName, texture);
            material.SetTextureScale(propertyName, scale);
            material.SetTextureOffset(propertyName, Vector2.zero);
        }

        private static InputActionAsset CreateInputActions()
        {
            string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", InputActionsPath));
            string json = InputActionsJson.Replace("\r\n", "\n");
            string existing = File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : string.Empty;
            if (!string.Equals(existing, json, StringComparison.Ordinal))
            {
                File.WriteAllText(absolutePath, json, new UTF8Encoding(false));
            }

            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (asset == null)
            {
                throw new InvalidOperationException("Failed to import generated Input Actions asset.");
            }

            EditorBuildSettings.AddConfigObject("com.unity.input.settings.actions", asset, true);
            return asset;
        }

        private static Sprite CreateToolIcon()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ToolIconTexturePath);
            if (texture == null)
            {
                texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
                {
                    name = "ToolIconTexture",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                AssetDatabase.CreateAsset(texture, ToolIconTexturePath);
            }

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color body = new Color(0.62f, 0.72f, 0.82f, 1f);
            Color screen = new Color(0.30f, 0.82f, 1f, 1f);
            Color[] pixels = Enumerable.Repeat(clear, 16 * 16).ToArray();
            for (int y = 2; y <= 13; y++)
            {
                int left = y < 5 ? 6 : 4;
                int right = y < 5 ? 9 : 11;
                for (int x = left; x <= right; x++)
                {
                    pixels[y * 16 + x] = body;
                }
            }

            for (int y = 8; y <= 11; y++)
            {
                for (int x = 6; x <= 9; x++)
                {
                    pixels[y * 16 + x] = screen;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);

            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(ToolIconTexturePath).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
            {
                sprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
                sprite.name = "ToolIcon";
                AssetDatabase.AddObjectToAsset(sprite, texture);
            }

            EditorUtility.SetDirty(sprite);
            return sprite;
        }

        private static GameObject CreateCubePrefab(
            Material glass,
            Material floor,
            Material strip,
            Material stripHousing,
            Material baseRail)
        {
            GameObject root = new GameObject("CubeRoom");
            try
            {
                CubeRoom room = root.AddComponent<CubeRoom>();

                Transform geometry = NewChild(root.transform, "Geometry");
                CreateCube("Floor", geometry, new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 8f), floor, true, true);

                Transform glassRoot = NewChild(geometry, "Glass");
                Transform colliders = NewChild(root.transform, "Boundary Colliders");
                CubeRoomWallBoundary westBoundary = CreateWallBoundary(
                    "West", CubeRoomWall.West, glassRoot, colliders,
                    new Vector3(-4f, 4f, 0f), new Vector3(0f, -90f, 0f), glass, strip, baseRail);
                CubeRoomWallBoundary eastBoundary = CreateWallBoundary(
                    "East", CubeRoomWall.East, glassRoot, colliders,
                    new Vector3(4f, 4f, 0f), new Vector3(0f, 90f, 0f), glass, strip, baseRail);
                CubeRoomWallBoundary southBoundary = CreateWallBoundary(
                    "South", CubeRoomWall.South, glassRoot, colliders,
                    new Vector3(0f, 4f, -4f), new Vector3(0f, 180f, 0f), glass, strip, baseRail);
                CubeRoomWallBoundary northBoundary = CreateWallBoundary(
                    "North", CubeRoomWall.North, glassRoot, colliders,
                    new Vector3(0f, 4f, 4f), Vector3.zero, glass, strip, baseRail);
                CubeRoomCeilingBoundary ceilingBoundary = CreateCeilingBoundary(
                    room,
                    glassRoot,
                    colliders,
                    glass,
                    strip,
                    stripHousing,
                    baseRail);

                room.ConfigureWallBoundaries(westBoundary, eastBoundary, southBoundary, northBoundary);
                room.ConfigureCeilingBoundary(ceilingBoundary);
                room.ConfigureWallGlass(
                    westBoundary.ClosedGlassRenderer,
                    eastBoundary.ClosedGlassRenderer,
                    southBoundary.ClosedGlassRenderer,
                    northBoundary.ClosedGlassRenderer);
                room.ConfigureWallBoundaryColliders(
                    westBoundary.ClosedCollider,
                    eastBoundary.ClosedCollider,
                    southBoundary.ClosedCollider,
                    northBoundary.ClosedCollider);
                Transform lightingRoot = NewChild(root.transform, "Lighting");
                CubeRoomLighting lighting = lightingRoot.gameObject.AddComponent<CubeRoomLighting>();
                Transform housingsRoot = NewChild(lightingRoot, "Strip Housings");
                const float housingWidth = 0.14f;
                CreateCube("Housing Vertical SW", housingsRoot, new Vector3(-3.95f, 4f, -3.95f), new Vector3(housingWidth, 8f, housingWidth), stripHousing, false, false);
                CreateCube("Housing Vertical SE", housingsRoot, new Vector3(3.95f, 4f, -3.95f), new Vector3(housingWidth, 8f, housingWidth), stripHousing, false, false);
                CreateCube("Housing Vertical NW", housingsRoot, new Vector3(-3.95f, 4f, 3.95f), new Vector3(housingWidth, 8f, housingWidth), stripHousing, false, false);
                CreateCube("Housing Vertical NE", housingsRoot, new Vector3(3.95f, 4f, 3.95f), new Vector3(housingWidth, 8f, housingWidth), stripHousing, false, false);
                Transform ceilingAssembliesRoot = NewChild(lightingRoot, "Ceiling Edge Assemblies");
                Transform ceilingWest = NewChild(ceilingAssembliesRoot, "Ceiling West Assembly");
                Transform ceilingEast = NewChild(ceilingAssembliesRoot, "Ceiling East Assembly");
                Transform ceilingSouth = NewChild(ceilingAssembliesRoot, "Ceiling South Assembly");
                Transform ceilingNorth = NewChild(ceilingAssembliesRoot, "Ceiling North Assembly");
                CreateCube("Housing Ceiling South", ceilingSouth, new Vector3(0f, 7.95f, -3.95f), new Vector3(8f, housingWidth, housingWidth), stripHousing, false, false);
                CreateCube("Housing Ceiling North", ceilingNorth, new Vector3(0f, 7.95f, 3.95f), new Vector3(8f, housingWidth, housingWidth), stripHousing, false, false);
                CreateCube("Housing Ceiling West", ceilingWest, new Vector3(-3.95f, 7.95f, 0f), new Vector3(housingWidth, housingWidth, 8f), stripHousing, false, false);
                CreateCube("Housing Ceiling East", ceilingEast, new Vector3(3.95f, 7.95f, 0f), new Vector3(housingWidth, housingWidth, 8f), stripHousing, false, false);

                Transform stripsRoot = NewChild(lightingRoot, "Visible Strips");
                List<Renderer> stripRenderers = new List<Renderer>();
                const float stripWidth = 0.07f;
                stripRenderers.Add(CreateCube("Strip Vertical SW", stripsRoot, new Vector3(-3.87f, 4f, -3.87f), new Vector3(stripWidth, 8f, stripWidth), strip, false, false));
                stripRenderers.Add(CreateCube("Strip Vertical SE", stripsRoot, new Vector3(3.87f, 4f, -3.87f), new Vector3(stripWidth, 8f, stripWidth), strip, false, false));
                stripRenderers.Add(CreateCube("Strip Vertical NW", stripsRoot, new Vector3(-3.87f, 4f, 3.87f), new Vector3(stripWidth, 8f, stripWidth), strip, false, false));
                stripRenderers.Add(CreateCube("Strip Vertical NE", stripsRoot, new Vector3(3.87f, 4f, 3.87f), new Vector3(stripWidth, 8f, stripWidth), strip, false, false));
                stripRenderers.Add(CreateCube("Strip Ceiling South", ceilingSouth, new Vector3(0f, 7.87f, -3.87f), new Vector3(8f, stripWidth, stripWidth), strip, false, false));
                stripRenderers.Add(CreateCube("Strip Ceiling North", ceilingNorth, new Vector3(0f, 7.87f, 3.87f), new Vector3(8f, stripWidth, stripWidth), strip, false, false));
                stripRenderers.Add(CreateCube("Strip Ceiling West", ceilingWest, new Vector3(-3.87f, 7.87f, 0f), new Vector3(stripWidth, stripWidth, 8f), strip, false, false));
                stripRenderers.Add(CreateCube("Strip Ceiling East", ceilingEast, new Vector3(3.87f, 7.87f, 0f), new Vector3(stripWidth, stripWidth, 8f), strip, false, false));
                ceilingBoundary.ConfigureEdgeCrossingRoots(
                    ceilingWest.gameObject,
                    ceilingEast.gameObject,
                    ceilingSouth.gameObject,
                    ceilingNorth.gameObject);

                Transform supportRoot = NewChild(lightingRoot, "Supporting Lights");
                // URP does not provide real-time area lights. Several broad, shadow-free spot
                // emitters follow each visible strip so illumination originates along its full
                // length instead of converging on a proxy source near the center of the room.
                List<Light> stripLights = new List<Light>(StripEmitterDefinitions.Length);
                for (int index = 0; index < StripEmitterDefinitions.Length; index++)
                {
                    StripEmitterDefinition definition = StripEmitterDefinitions[index];
                    Transform lightParent = definition.Name.StartsWith("Ceiling West", StringComparison.Ordinal) ? ceilingWest
                        : definition.Name.StartsWith("Ceiling East", StringComparison.Ordinal) ? ceilingEast
                        : definition.Name.StartsWith("Ceiling South", StringComparison.Ordinal) ? ceilingSouth
                        : definition.Name.StartsWith("Ceiling North", StringComparison.Ordinal) ? ceilingNorth
                        : supportRoot;
                    stripLights.Add(CreateSupportingLight(lightParent, definition, index + 1));
                }
                Light[] lights = stripLights.ToArray();
                stripRenderers.AddRange(ceilingBoundary.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.name.StartsWith("Strip Ceiling Passage", StringComparison.Ordinal)));
                stripLights.AddRange(ceilingBoundary.GetComponentsInChildren<Light>(true)
                    .Where(light => light.name.StartsWith("Strip Passage Emitter", StringComparison.Ordinal)));
                lights = stripLights.ToArray();
                Renderer[] doorwayFrames = new Component[]
                    { westBoundary, eastBoundary, southBoundary, northBoundary, ceilingBoundary }
                    .SelectMany(boundary => boundary.GetComponentsInChildren<Renderer>(true))
                    .Where(renderer => renderer.name.StartsWith("Doorway Frame", StringComparison.Ordinal))
                    .ToArray();
                Light[] doorwayLights = new Component[]
                    { westBoundary, eastBoundary, southBoundary, northBoundary, ceilingBoundary }
                    .SelectMany(boundary => boundary.GetComponentsInChildren<Light>(true))
                    .Where(light => light.name.StartsWith("Doorway Emitter", StringComparison.Ordinal))
                    .ToArray();
                lighting.StripColor = NeutralWhite;
                lighting.StripWidth = stripWidth;
                lighting.EmissionIntensity = 6f;
                lighting.DoorwayFramePowerRatio = 0.5f;
                lighting.SupportingLightIntensity = 0.85f;
                lighting.SupportingLightRange = 12f;
                lighting.SupportingLightSpotAngle = 170f;
                lighting.SupportingLightInnerSpotAngle = 160f;
                lighting.SupportingLightShadows = LightShadows.None;
                lighting.SupportingLightShadowResolution = RoomLightShadowResolutionTier.Low;
                lighting.Configure(stripRenderers.ToArray(), lights, doorwayFrames, doorwayLights);
                lighting.ApplySettings();

                GameObject volumeObject = new GameObject("Interior Volume");
                volumeObject.transform.SetParent(root.transform, false);
                volumeObject.layer = LayerMask.NameToLayer("RoomVolume");
                volumeObject.transform.localPosition = new Vector3(0f, 4f, 0f);
                BoxCollider trigger = volumeObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                // Keep a small horizontal separation between adjacent room volumes while
                // covering the complete floor-to-ceiling span. The player's root sits only
                // a few centimetres above the floor after grounding and must still count as
                // geometrically inside the destination room after doorway traversal.
                trigger.size = new Vector3(7.90f, 8.00f, 7.90f);
                RoomOccupancyVolume occupancy = volumeObject.AddComponent<RoomOccupancyVolume>();
                occupancy.Configure(room);

                room.Configure(CubeRoom.StandardGravityStrength, lighting, occupancy);
                return PrefabUtility.SaveAsPrefabAsset(root, CubePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePlayerPrefab(
            InputActionAsset inputActions,
            Material hand,
            Material tool,
            Material roomPreview,
            Material roomDeleteOutline,
            LayerIds layers)
        {
            GameObject root = new GameObject("Player");
            try
            {
                root.layer = layers.Player;
                CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.height = 1.8f;
                capsule.radius = 0.30f;
                capsule.center = Vector3.zero;
                Rigidbody body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.constraints = RigidbodyConstraints.FreezeRotation;

                FirstPersonInput input = root.AddComponent<FirstPersonInput>();
                PlayerRoomTracker tracker = root.AddComponent<PlayerRoomTracker>();
                KinematicCapsuleMover mover = root.AddComponent<KinematicCapsuleMover>();
                PlayerGravityAlignment alignment = root.AddComponent<PlayerGravityAlignment>();
                PlayerRoomTraversal roomTraversal = root.AddComponent<PlayerRoomTraversal>();
                FirstPersonMotor motor = root.AddComponent<FirstPersonMotor>();
                PlayerLook look = root.AddComponent<PlayerLook>();
                RoomCreationController roomCreation = root.AddComponent<RoomCreationController>();
                RoomDeletionController roomDeletion = root.AddComponent<RoomDeletionController>();
                input.Configure(inputActions);
                mover.Configure(capsule, body);
                mover.SetDimensions(1.8f, 0.30f, true);

                Transform yaw = NewChild(root.transform, "Yaw Pivot");
                Transform pitch = NewChild(yaw, "Pitch Pivot");
                // The physical root starts 0.93 m above the floor (3 cm skin clearance),
                // so a 0.72 m offset preserves the requested 1.65 m eye height.
                pitch.localPosition = new Vector3(0f, 0.72f, 0f);
                pitch.localRotation = Quaternion.Euler(-6f, 0f, 0f);

                GameObject mainCameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                mainCameraObject.tag = "MainCamera";
                mainCameraObject.transform.SetParent(pitch, false);
                Camera mainCamera = mainCameraObject.GetComponent<Camera>();
                ConfigureCamera(mainCamera, 75f, 0.03f, 200f);
                mainCamera.cullingMask = ~(1 << layers.ViewModel);
                UniversalAdditionalCameraData mainData = mainCamera.GetUniversalAdditionalCameraData();
                mainData.renderType = CameraRenderType.Base;
                mainData.renderPostProcessing = false;
                mainData.cameraStack.Clear();

                GameObject overlayObject = new GameObject("Viewmodel Camera", typeof(Camera));
                overlayObject.transform.SetParent(pitch, false);
                Camera overlayCamera = overlayObject.GetComponent<Camera>();
                ConfigureCamera(overlayCamera, 55f, 0.01f, 10f);
                overlayCamera.cullingMask = 1 << layers.ViewModel;
                UniversalAdditionalCameraData overlayData = overlayCamera.GetUniversalAdditionalCameraData();
                overlayData.renderType = CameraRenderType.Overlay;
                overlayData.renderShadows = true;
                overlayData.renderPostProcessing = false;
                SerializedObject overlaySettings = new SerializedObject(overlayData);
                SetBoolIfPresent(overlaySettings, "m_ClearDepth", true);
                overlaySettings.ApplyModifiedPropertiesWithoutUndo();
                mainData.cameraStack.Add(overlayCamera);

                Transform viewModel = NewChild(pitch, "Viewmodel");
                SetLayerRecursively(viewModel.gameObject, layers.ViewModel);
                GameObject palm = CreatePrimitive("Right Hand", PrimitiveType.Capsule, viewModel, new Vector3(0.31f, -0.29f, 0.76f), new Vector3(0.09f, 0.16f, 0.09f), hand);
                palm.transform.localRotation = Quaternion.Euler(70f, 0f, -28f);
                GameObject device = CreatePrimitive("Held Tool", PrimitiveType.Cube, viewModel, new Vector3(0.27f, -0.24f, 0.92f), new Vector3(0.12f, 0.16f, 0.23f), tool);
                device.transform.localRotation = Quaternion.Euler(8f, -8f, -8f);
                CreatePrimitive("Tool Display", PrimitiveType.Cube, device.transform, new Vector3(0f, 0.02f, -0.52f), new Vector3(0.72f, 0.56f, 0.05f), tool);
                SetLayerRecursively(viewModel.gameObject, layers.ViewModel);
                foreach (Collider viewModelCollider in viewModel.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(viewModelCollider);
                }

                foreach (Renderer renderer in viewModel.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                }

                look.Configure(input, yaw, pitch, mainCamera);
                look.FieldOfView = 75f;
                motor.Configure(mover, input, tracker, alignment, roomTraversal, yaw);
                roomCreation.Configure(input, tracker, look, mainCamera, roomPreview);
                roomDeletion.Configure(input, tracker, look, mainCamera, roomDeleteOutline, roomCreation, roomTraversal);
                roomTraversal.Configure(input, tracker, mover, alignment, mainCamera, look, roomCreation, null, roomDeletion);
                return PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateHotbarPrefab(Sprite toolIcon)
        {
            GameObject root = new GameObject(
                "Hotbar",
                typeof(HotbarView),
                typeof(GlassOpacityControl),
                typeof(RoomCreationPromptView));
            try
            {
                root.transform.localScale = Vector3.one;
                GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(root.transform, false);
                canvasObject.GetComponent<RectTransform>().localScale = Vector3.one;
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                GameObject panel = CreateUiImage("Hotbar Panel", canvasObject.transform, new Color(0.025f, 0.03f, 0.04f, 0.78f));
                RectTransform panelRect = panel.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.5f, 0f);
                panelRect.anchorMax = new Vector2(0.5f, 0f);
                panelRect.pivot = new Vector2(0.5f, 0f);
                panelRect.anchoredPosition = new Vector2(0f, 24f);
                panelRect.sizeDelta = new Vector2(472f, 62f);

                GameObject slots = new GameObject("Slots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                slots.transform.SetParent(panel.transform, false);
                RectTransform slotsRect = slots.GetComponent<RectTransform>();
                slotsRect.anchorMin = Vector2.zero;
                slotsRect.anchorMax = Vector2.one;
                slotsRect.offsetMin = new Vector2(8f, 7f);
                slotsRect.offsetMax = new Vector2(-8f, -7f);
                HorizontalLayoutGroup layout = slots.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                GameObject template = CreateUiImage("Slot Template", canvasObject.transform, new Color(0.13f, 0.15f, 0.18f, 0.96f));
                template.GetComponent<RectTransform>().sizeDelta = new Vector2(48f, 48f);
                GameObject selection = CreateUiImage("Selection", template.transform, new Color(0.26f, 0.65f, 1f, 0.18f));
                StretchToParent(selection.GetComponent<RectTransform>(), -2f);
                Outline outline = selection.AddComponent<Outline>();
                outline.effectColor = new Color(0.84f, 0.92f, 1f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);

                GameObject icon = CreateUiImage("Icon", template.transform, Color.white);
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(30f, 34f);
                Image iconImage = icon.GetComponent<Image>();
                iconImage.sprite = toolIcon;
                iconImage.preserveAspect = true;
                template.SetActive(false);

                Text creationInstruction = CreateUiText(
                    "Room Creation Instruction",
                    canvasObject.transform,
                    RoomCreationPromptView.NormalText,
                    20,
                    TextAnchor.LowerLeft);
                RectTransform instructionRect = creationInstruction.rectTransform;
                instructionRect.anchorMin = Vector2.zero;
                instructionRect.anchorMax = Vector2.zero;
                instructionRect.pivot = Vector2.zero;
                instructionRect.anchoredPosition = new Vector2(28f, 51f);
                instructionRect.sizeDelta = new Vector2(440f, 30f);
                Outline instructionOutline = creationInstruction.gameObject.AddComponent<Outline>();
                instructionOutline.effectColor = new Color(0f, 0f, 0f, 0.92f);
                instructionOutline.effectDistance = new Vector2(2f, -2f);

                Text deletionInstruction = CreateUiText(
                    "Room Deletion Instruction",
                    canvasObject.transform,
                    RoomCreationPromptView.NormalDeleteText,
                    20,
                    TextAnchor.LowerLeft);
                RectTransform deletionRect = deletionInstruction.rectTransform;
                deletionRect.anchorMin = Vector2.zero;
                deletionRect.anchorMax = Vector2.zero;
                deletionRect.pivot = Vector2.zero;
                deletionRect.anchoredPosition = new Vector2(28f, 23f);
                deletionRect.sizeDelta = new Vector2(440f, 30f);
                Outline deletionOutline = deletionInstruction.gameObject.AddComponent<Outline>();
                deletionOutline.effectColor = new Color(0f, 0f, 0f, 0.92f);
                deletionOutline.effectDistance = new Vector2(2f, -2f);

                Text rotationInstruction = CreateUiText(
                    "Room Rotation Instruction",
                    canvasObject.transform,
                    RoomCreationPromptView.RotationIdleText,
                    20,
                    TextAnchor.MiddleCenter);
                RectTransform rotationRect = rotationInstruction.rectTransform;
                rotationRect.anchorMin = new Vector2(0.5f, 0f);
                rotationRect.anchorMax = new Vector2(0.5f, 0f);
                rotationRect.pivot = new Vector2(0.5f, 0f);
                rotationRect.anchoredPosition = new Vector2(0f, 98f);
                rotationRect.sizeDelta = new Vector2(680f, 62f);
                rotationInstruction.supportRichText = true;
                rotationInstruction.lineSpacing = 0.9f;
                Outline rotationOutline = rotationInstruction.gameObject.AddComponent<Outline>();
                rotationOutline.effectColor = new Color(0f, 0f, 0f, 0.94f);
                rotationOutline.effectDistance = new Vector2(2f, -2f);
                Text traversalInstruction = CreateUiText(
                    "Traversal Instruction",
                    canvasObject.transform,
                    RoomCreationPromptView.TraverseText,
                    20,
                    TextAnchor.MiddleCenter);
                RectTransform traversalRect = traversalInstruction.rectTransform;
                traversalRect.anchorMin = new Vector2(0.5f, 0f);
                traversalRect.anchorMax = new Vector2(0.5f, 0f);
                traversalRect.pivot = new Vector2(0.5f, 0f);
                traversalRect.anchoredPosition = new Vector2(0f, 98f);
                traversalRect.sizeDelta = new Vector2(680f, 38f);
                Outline traversalOutline = traversalInstruction.gameObject.AddComponent<Outline>();
                traversalOutline.effectColor = new Color(0f, 0f, 0f, 0.94f);
                traversalOutline.effectDistance = new Vector2(2f, -2f);
                root.GetComponent<RoomCreationPromptView>().Configure(
                    creationInstruction,
                    deletionInstruction,
                    rotationInstruction,
                    traversalInstruction);

                GameObject glassPanel = CreateUiImage(
                    "Glass Opacity Panel",
                    canvasObject.transform,
                    new Color(0.025f, 0.03f, 0.04f, 0.82f));
                RectTransform glassPanelRect = glassPanel.GetComponent<RectTransform>();
                glassPanelRect.anchorMin = Vector2.one;
                glassPanelRect.anchorMax = Vector2.one;
                glassPanelRect.pivot = Vector2.one;
                glassPanelRect.anchoredPosition = new Vector2(-24f, -24f);
                glassPanelRect.sizeDelta = new Vector2(310f, 76f);

                Text opacityLabel = CreateUiText(
                    "Glass Opacity Label",
                    glassPanel.transform,
                    "Glass opacity  3.5%   (Esc, then drag)",
                    15,
                    TextAnchor.MiddleLeft);
                RectTransform opacityLabelRect = opacityLabel.rectTransform;
                opacityLabelRect.anchorMin = new Vector2(0f, 1f);
                opacityLabelRect.anchorMax = Vector2.one;
                opacityLabelRect.offsetMin = new Vector2(14f, -38f);
                opacityLabelRect.offsetMax = new Vector2(-14f, -8f);

                GameObject sliderObject = new GameObject("Glass Opacity Slider", typeof(RectTransform), typeof(Slider));
                sliderObject.transform.SetParent(glassPanel.transform, false);
                RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
                sliderRect.anchorMin = new Vector2(0f, 0f);
                sliderRect.anchorMax = new Vector2(1f, 0f);
                sliderRect.pivot = new Vector2(0.5f, 0f);
                sliderRect.offsetMin = new Vector2(14f, 12f);
                sliderRect.offsetMax = new Vector2(-14f, 32f);

                GameObject sliderBackground = CreateUiImage(
                    "Background",
                    sliderObject.transform,
                    new Color(0.10f, 0.12f, 0.15f, 1f));
                RectTransform sliderBackgroundRect = sliderBackground.GetComponent<RectTransform>();
                StretchToParent(sliderBackgroundRect, 0f);
                sliderBackgroundRect.offsetMin = new Vector2(0f, 7f);
                sliderBackgroundRect.offsetMax = new Vector2(0f, -7f);

                GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
                fillArea.transform.SetParent(sliderObject.transform, false);
                RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
                StretchToParent(fillAreaRect, 0f);
                fillAreaRect.offsetMin = new Vector2(2f, 8f);
                fillAreaRect.offsetMax = new Vector2(-8f, -8f);
                Image sliderFill = CreateUiImage(
                    "Fill",
                    fillArea.transform,
                    new Color(0.30f, 0.72f, 1f, 1f)).GetComponent<Image>();
                StretchToParent(sliderFill.rectTransform, 0f);

                GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
                handleArea.transform.SetParent(sliderObject.transform, false);
                RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
                StretchToParent(handleAreaRect, 0f);
                handleAreaRect.offsetMin = new Vector2(7f, 0f);
                handleAreaRect.offsetMax = new Vector2(-7f, 0f);
                Image sliderHandle = CreateUiImage(
                    "Handle",
                    handleArea.transform,
                    new Color(0.84f, 0.94f, 1f, 1f)).GetComponent<Image>();
                sliderHandle.rectTransform.sizeDelta = new Vector2(14f, 20f);

                Slider opacitySlider = sliderObject.GetComponent<Slider>();
                opacitySlider.transition = Selectable.Transition.None;
                opacitySlider.fillRect = sliderFill.rectTransform;
                opacitySlider.handleRect = sliderHandle.rectTransform;
                opacitySlider.targetGraphic = sliderHandle;
                opacitySlider.direction = Slider.Direction.LeftToRight;
                opacitySlider.minValue = 0f;
                opacitySlider.maxValue = 1f;
                opacitySlider.value = 0.035f;

                GlassOpacityControl opacityControl = root.GetComponent<GlassOpacityControl>();
                opacityControl.Configure(opacitySlider, opacityLabel, 0f, 1f, 0.035f);

                HotbarView hotbar = root.GetComponent<HotbarView>();
                hotbar.ToolIcon = toolIcon;
                hotbar.Configure(slotsRect, template, 8);
                hotbar.Select(0);
                return PrefabUtility.SaveAsPrefabAsset(root, HotbarPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateMainMenuScene(Texture2D menuArtwork, Font menuFont)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureBlackEnvironment();

            GameObject cameraObject = new GameObject("Main Menu Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            GameObject menuRoot = new GameObject("Main Menu", typeof(MainMenuController));
            GameObject canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            canvasObject.transform.SetParent(menuRoot.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            CanvasGroup menuGroup = canvasObject.GetComponent<CanvasGroup>();

            GameObject backdrop = CreateUiImage("Black Backdrop", canvasObject.transform, Color.black);
            StretchToParent(backdrop.GetComponent<RectTransform>(), 0f);

            GameObject artworkSafeArea = new GameObject("Artwork Safe Area", typeof(RectTransform));
            artworkSafeArea.transform.SetParent(canvasObject.transform, false);
            RectTransform artworkSafeRect = artworkSafeArea.GetComponent<RectTransform>();
            artworkSafeRect.anchorMin = new Vector2(0.05f, 0.05f);
            artworkSafeRect.anchorMax = new Vector2(0.95f, 0.95f);
            artworkSafeRect.offsetMin = Vector2.zero;
            artworkSafeRect.offsetMax = Vector2.zero;

            GameObject artworkObject = new GameObject(
                "What Light Remains Artwork",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(AspectRatioFitter));
            artworkObject.transform.SetParent(artworkSafeArea.transform, false);
            RectTransform artworkRect = artworkObject.GetComponent<RectTransform>();
            StretchToParent(artworkRect, 0f);
            RawImage artwork = artworkObject.GetComponent<RawImage>();
            artwork.texture = menuArtwork;
            artwork.color = Color.white;
            artwork.raycastTarget = false;
            AspectRatioFitter fitter = artworkObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = menuArtwork.width / (float)menuArtwork.height;

            GameObject buttonObject = new GameObject(
                "New Game Button",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Button),
                typeof(MainMenuTextHover));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 30f);
            buttonRect.sizeDelta = new Vector2(320f, 72f);
            Text buttonLabel = buttonObject.GetComponent<Text>();
            buttonLabel.font = menuFont;
            buttonLabel.fontSize = 30;
            buttonLabel.alignment = TextAnchor.MiddleCenter;
            buttonLabel.text = "New Game";
            buttonLabel.raycastTarget = true;
            Button newGameButton = buttonObject.GetComponent<Button>();
            newGameButton.targetGraphic = buttonLabel;
            newGameButton.transition = Selectable.Transition.None;
            buttonObject.GetComponent<MainMenuTextHover>().Configure(
                buttonLabel,
                new Color(0.90f, 0.87f, 0.78f, 0.82f),
                new Color(0.98f, 0.96f, 0.90f, 1f),
                1.025f,
                0.12f);

            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(menuRoot.transform, false);

            menuRoot.GetComponent<MainMenuController>().Configure(
                menuGroup,
                newGameButton,
                Path.GetFileNameWithoutExtension(FoundationScenePath),
                0.8f);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void CreateFoundationScene(GameObject cubePrefab, GameObject playerPrefab, GameObject hotbarPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureBlackEnvironment();

            GameObject clusterRoot = new GameObject("Room Cluster");
            CubeRoom room = InstantiatePrefab<CubeRoom>(cubePrefab);
            room.name = "Primary Cube Room";
            room.transform.SetParent(clusterRoot.transform, false);
            room.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            CubeRoomClusterGenerator cluster = clusterRoot.AddComponent<CubeRoomClusterGenerator>();
            cluster.Configure(room, cubePrefab.GetComponent<CubeRoom>(), 0);

            PlayerRoomTracker tracker = InstantiatePrefab<PlayerRoomTracker>(playerPrefab);
            tracker.transform.SetPositionAndRotation(new Vector3(0f, 0.93f, 0f), Quaternion.identity);
            Transform yaw = tracker.transform.Find("Yaw Pivot");
            if (yaw != null) yaw.localRotation = Quaternion.Euler(0f, 45f, 0f);
            tracker.Initialize(cluster.PrimaryRoom);
            GameObject hotbar = (GameObject)PrefabUtility.InstantiatePrefab(hotbarPrefab);
            tracker.GetComponent<RoomCreationController>().Initialize(
                cluster,
                hotbar.GetComponent<RoomCreationPromptView>());
            tracker.GetComponent<RoomDeletionController>().Initialize(
                cluster,
                hotbar.GetComponent<RoomCreationPromptView>());
            tracker.GetComponent<PlayerRoomTraversal>().Configure(
                tracker.GetComponent<FirstPersonInput>(),
                tracker,
                tracker.GetComponent<KinematicCapsuleMover>(),
                tracker.GetComponent<PlayerGravityAlignment>(),
                tracker.GetComponentInChildren<Camera>(),
                tracker.GetComponent<PlayerLook>(),
                tracker.GetComponent<RoomCreationController>(),
                hotbar.GetComponent<RoomCreationPromptView>(),
                tracker.GetComponent<RoomDeletionController>());
            EditorSceneManager.SaveScene(scene, FoundationScenePath);
        }

        private static void CreateValidationScene(GameObject cubePrefab, GameObject playerPrefab, GameObject hotbarPrefab, Material validationMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureBlackEnvironment();

            CubeRoom sourceRoom = InstantiatePrefab<CubeRoom>(cubePrefab);
            sourceRoom.name = "Source Cube - Lighting Enabled";
            sourceRoom.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            CubeRoom receivingRoom = InstantiatePrefab<CubeRoom>(cubePrefab);
            receivingRoom.name = "Independent Cube - Lighting Disabled";
            receivingRoom.transform.SetPositionAndRotation(new Vector3(16f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f));
            receivingRoom.GravityStrength = CubeRoom.StandardGravityStrength * 0.5f;
            receivingRoom.SetLightingEnabled(false);

            PlayerRoomTracker tracker = InstantiatePrefab<PlayerRoomTracker>(playerPrefab);
            tracker.transform.SetPositionAndRotation(new Vector3(0f, 0.93f, 0f), Quaternion.identity);
            Transform validationYaw = tracker.transform.Find("Yaw Pivot");
            if (validationYaw != null) validationYaw.localRotation = Quaternion.Euler(0f, 90f, 0f);
            tracker.Initialize(sourceRoom);
            PrefabUtility.InstantiatePrefab(hotbarPrefab);

            // The east-south supporting light is directly behind this small blocker.
            // The larger receiver leaves an unobstructed region beside the resulting shadow,
            // making both glass light transmission and opaque occlusion easy to compare.
            GameObject blocker = CreatePrimitive("Opaque Shadow Blocker", PrimitiveType.Cube, null, new Vector3(4.75f, 4f, -3.72f), new Vector3(0.35f, 1.40f, 1.40f), validationMaterial);
            blocker.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            GameObject receiver = CreatePrimitive("External Light Receiver", PrimitiveType.Cube, null, new Vector3(5.75f, 4f, -3.00f), new Vector3(0.20f, 5.00f, 4.00f), validationMaterial);
            receiver.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            EditorSceneManager.SaveScene(scene, ValidationScenePath);
        }

        private static void ConfigureBlackEnvironment()
        {
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0f;
        }

        private static void ConfigureBuildScenes()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(scene => scene.path != MainMenuScenePath
                    && scene.path != FoundationScenePath
                    && scene.path != ValidationScenePath)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ValidationScenePath, false));
            scenes.Insert(0, new EditorBuildSettingsScene(FoundationScenePath, true));
            scenes.Insert(0, new EditorBuildSettingsScene(MainMenuScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T InstantiatePrefab<T>(GameObject prefab) where T : Component
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            T component = instance.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException($"Prefab {prefab.name} does not contain {typeof(T).Name}.");
            }

            return component;
        }

        private static Renderer CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool keepCollider, bool castShadows)
        {
            GameObject gameObject = CreatePrimitive(name, PrimitiveType.Cube, parent, position, scale, material);
            if (!keepCollider)
            {
                Object.DestroyImmediate(gameObject.GetComponent<Collider>());
            }

            Renderer renderer = gameObject.GetComponent<Renderer>();
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = castShadows;
            return renderer;
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType primitive, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(primitive);
            gameObject.name = name;
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
                gameObject.transform.localPosition = position;
                gameObject.transform.localScale = scale;
            }
            else
            {
                gameObject.transform.position = position;
                gameObject.transform.localScale = scale;
            }

            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            return gameObject;
        }

        private static Renderer CreateGlassPane(string name, Transform parent, Vector3 position, Vector3 rotation, Material material)
        {
            Renderer renderer = CreateGlassSection(name, parent, position, new Vector2(8f, 8f), material);
            renderer.transform.localRotation = Quaternion.Euler(rotation);
            return renderer;
        }

        private static CubeRoomWallBoundary CreateWallBoundary(
            string label,
            CubeRoomWall wall,
            Transform glassParent,
            Transform colliderParent,
            Vector3 localPosition,
            Vector3 localRotation,
            Material glassMaterial,
            Material frameMaterial,
            Material baseTrimMaterial)
        {
            GameObject boundaryObject = new GameObject(label + " Wall Boundary");
            boundaryObject.transform.SetParent(glassParent, false);
            boundaryObject.transform.localPosition = localPosition;
            boundaryObject.transform.localRotation = Quaternion.Euler(localRotation);
            CubeRoomWallBoundary boundary = boundaryObject.AddComponent<CubeRoomWallBoundary>();

            Renderer closedGlass = CreateGlassSection(
                "Glass " + label,
                boundaryObject.transform,
                Vector3.zero,
                new Vector2(CubeRoom.InteriorWidth, CubeRoom.InteriorHeight),
                glassMaterial);

            float sideWidth = (CubeRoom.InteriorWidth - CubeRoom.DoorwayWidth) * 0.5f;
            float sideCenter = CubeRoom.DoorwayWidth * 0.5f + sideWidth * 0.5f;
            float headerHeight = CubeRoom.InteriorHeight - CubeRoom.DoorwayHeight;
            float headerCenter = -CubeRoom.InteriorHeight * 0.5f
                + CubeRoom.DoorwayHeight
                + headerHeight * 0.5f;
            Renderer[] doorwayGlass =
            {
                CreateGlassSection(
                    "Doorway Glass Left",
                    boundaryObject.transform,
                    new Vector3(-sideCenter, 0f, 0f),
                    new Vector2(sideWidth, CubeRoom.InteriorHeight),
                    glassMaterial),
                CreateGlassSection(
                    "Doorway Glass Right",
                    boundaryObject.transform,
                    new Vector3(sideCenter, 0f, 0f),
                    new Vector2(sideWidth, CubeRoom.InteriorHeight),
                    glassMaterial),
                CreateGlassSection(
                    "Doorway Glass Header",
                    boundaryObject.transform,
                    new Vector3(0f, headerCenter, 0f),
                    new Vector2(CubeRoom.DoorwayWidth, headerHeight),
                    glassMaterial),
            };

            const float frameWidth = 0.14f;
            const float frameDepth = 0.14f;
            float doorwayCenterY = -CubeRoom.InteriorHeight * 0.5f + CubeRoom.DoorwayHeight * 0.5f;
            float frameSideX = CubeRoom.DoorwayWidth * 0.5f + frameWidth * 0.5f;
            float frameHeaderY = -CubeRoom.InteriorHeight * 0.5f + CubeRoom.DoorwayHeight + frameWidth * 0.5f;
            Renderer[] doorwayFrames =
            {
                CreateCube(
                    "Doorway Frame Left",
                    boundaryObject.transform,
                    new Vector3(-frameSideX, doorwayCenterY, 0f),
                    new Vector3(frameWidth, CubeRoom.DoorwayHeight, frameDepth),
                    frameMaterial,
                    false,
                    true),
                CreateCube(
                    "Doorway Frame Right",
                    boundaryObject.transform,
                    new Vector3(frameSideX, doorwayCenterY, 0f),
                    new Vector3(frameWidth, CubeRoom.DoorwayHeight, frameDepth),
                    frameMaterial,
                    false,
                    true),
                CreateCube(
                    "Doorway Frame Header",
                    boundaryObject.transform,
                    new Vector3(0f, frameHeaderY, 0f),
                    new Vector3(CubeRoom.DoorwayWidth + frameWidth * 2f, frameWidth, frameDepth),
                    frameMaterial,
                    false,
                    true),
            };
            CreateDoorwayFrameEmitters(
                boundaryObject.transform,
                doorwayCenterY,
                frameSideX,
                frameHeaderY);

            const float baseTrimHeight = 0.12f;
            const float baseTrimDepth = 0.12f;
            float baseTrimY = -CubeRoom.InteriorHeight * 0.5f + baseTrimHeight * 0.5f;
            float baseTrimZ = -baseTrimDepth * 0.5f;
            Renderer closedBaseTrim = CreateCube(
                "Closed Base Rail",
                boundaryObject.transform,
                new Vector3(0f, baseTrimY, baseTrimZ),
                new Vector3(CubeRoom.InteriorWidth, baseTrimHeight, baseTrimDepth),
                baseTrimMaterial,
                false,
                false);
            Renderer[] doorwayBaseTrims =
            {
                CreateCube(
                    "Doorway Base Rail Left",
                    boundaryObject.transform,
                    new Vector3(-sideCenter, baseTrimY, baseTrimZ),
                    new Vector3(sideWidth, baseTrimHeight, baseTrimDepth),
                    baseTrimMaterial,
                    false,
                    false),
                CreateCube(
                    "Doorway Base Rail Right",
                    boundaryObject.transform,
                    new Vector3(sideCenter, baseTrimY, baseTrimZ),
                    new Vector3(sideWidth, baseTrimHeight, baseTrimDepth),
                    baseTrimMaterial,
                    false,
                    false),
            };

            Transform colliderRoot = NewChild(colliderParent, label + " Collider Set");
            colliderRoot.localPosition = localPosition;
            colliderRoot.localRotation = Quaternion.Euler(localRotation);
            BoxCollider closedCollider = CreateBoxCollider(
                label + " Collider",
                colliderRoot,
                new Vector3(0f, 0f, 0.05f),
                new Vector3(8.2f, CubeRoom.InteriorHeight, 0.10f));

            float doorwaySideColliderWidth = (8.2f - CubeRoom.DoorwayWidth) * 0.5f;
            float doorwaySideColliderCenter = CubeRoom.DoorwayWidth * 0.5f
                + doorwaySideColliderWidth * 0.5f;
            Collider[] doorwayColliders =
            {
                CreateBoxCollider(
                    "Doorway Collider Left",
                    colliderRoot,
                    new Vector3(-doorwaySideColliderCenter, 0f, 0.05f),
                    new Vector3(doorwaySideColliderWidth, CubeRoom.InteriorHeight, 0.10f)),
                CreateBoxCollider(
                    "Doorway Collider Right",
                    colliderRoot,
                    new Vector3(doorwaySideColliderCenter, 0f, 0.05f),
                    new Vector3(doorwaySideColliderWidth, CubeRoom.InteriorHeight, 0.10f)),
                CreateBoxCollider(
                    "Doorway Collider Header",
                    colliderRoot,
                    new Vector3(0f, headerCenter, 0.05f),
                    new Vector3(CubeRoom.DoorwayWidth, headerHeight, 0.10f)),
            };

            boundary.Configure(
                wall,
                closedGlass,
                doorwayGlass,
                doorwayFrames,
                closedBaseTrim,
                doorwayBaseTrims,
                closedCollider,
                doorwayColliders);
            return boundary;
        }

        private static CubeRoomCeilingBoundary CreateCeilingBoundary(
            CubeRoom room,
            Transform glassParent,
            Transform colliderParent,
            Material glassMaterial,
            Material frameMaterial,
            Material stripHousingMaterial,
            Material trimMaterial)
        {
            GameObject boundaryObject = new GameObject("Ceiling Boundary");
            boundaryObject.transform.SetParent(glassParent, false);
            CubeRoomCeilingBoundary boundary = boundaryObject.AddComponent<CubeRoomCeilingBoundary>();

            Renderer closedGlass = CreateGlassPane(
                "Glass Ceiling",
                boundaryObject.transform,
                new Vector3(0f, CubeRoom.InteriorHeight, 0f),
                new Vector3(90f, 0f, 0f),
                glassMaterial);
            BoxCollider closedCollider = CreateBoxCollider(
                "Ceiling Collider",
                colliderParent,
                new Vector3(0f, CubeRoom.InteriorHeight + 0.05f, 0f),
                new Vector3(8.2f, 0.10f, 8.2f));

            (RoomCeilingEdge edge, Vector3 inward)[] definitions =
            {
                (RoomCeilingEdge.West, Vector3.right),
                (RoomCeilingEdge.East, Vector3.left),
                (RoomCeilingEdge.South, Vector3.forward),
                (RoomCeilingEdge.North, Vector3.back),
            };
            List<CubeRoomCeilingBoundary.EdgeVariant> variants =
                new List<CubeRoomCeilingBoundary.EdgeVariant>(definitions.Length);
            foreach ((RoomCeilingEdge edge, Vector3 inward) definition in definitions)
            {
                GameObject passageRoot = new GameObject(definition.edge + " Ceiling Passage");
                passageRoot.transform.SetParent(boundaryObject.transform, false);
                passageRoot.transform.localPosition = new Vector3(0f, CubeRoom.InteriorHeight, 0f);
                // This maps the familiar side-wall X/Y layout onto the ceiling: local -Y is
                // the selected outer edge and local +Z points down into the source room.
                passageRoot.transform.localRotation = Quaternion.LookRotation(Vector3.down, definition.inward);

                float sideWidth = (CubeRoom.InteriorWidth - CubeRoom.DoorwayWidth) * 0.5f;
                float sideCenter = CubeRoom.DoorwayWidth * 0.5f + sideWidth * 0.5f;
                float headerHeight = CubeRoom.InteriorHeight - CubeRoom.DoorwayHeight;
                float headerCenter = -CubeRoom.InteriorHeight * 0.5f
                    + CubeRoom.DoorwayHeight
                    + headerHeight * 0.5f;
                List<Renderer> renderers = new List<Renderer>
                {
                    CreateGlassSection("Ceiling Doorway Glass Left", passageRoot.transform,
                        new Vector3(-sideCenter, 0f, 0f), new Vector2(sideWidth, CubeRoom.InteriorHeight), glassMaterial),
                    CreateGlassSection("Ceiling Doorway Glass Right", passageRoot.transform,
                        new Vector3(sideCenter, 0f, 0f), new Vector2(sideWidth, CubeRoom.InteriorHeight), glassMaterial),
                    CreateGlassSection("Ceiling Doorway Glass Header", passageRoot.transform,
                        new Vector3(0f, headerCenter, 0f), new Vector2(CubeRoom.DoorwayWidth, headerHeight), glassMaterial),
                };

                const float frameWidth = 0.14f;
                const float frameDepth = 0.14f;
                float doorwayCenterY = -CubeRoom.InteriorHeight * 0.5f + CubeRoom.DoorwayHeight * 0.5f;
                float frameSideX = CubeRoom.DoorwayWidth * 0.5f + frameWidth * 0.5f;
                float frameHeaderY = -CubeRoom.InteriorHeight * 0.5f + CubeRoom.DoorwayHeight + frameWidth * 0.5f;
                renderers.Add(CreateCube("Doorway Frame Ceiling Left", passageRoot.transform,
                    new Vector3(-frameSideX, doorwayCenterY, 0f),
                    new Vector3(frameWidth, CubeRoom.DoorwayHeight, frameDepth), frameMaterial, false, false));
                renderers.Add(CreateCube("Doorway Frame Ceiling Right", passageRoot.transform,
                    new Vector3(frameSideX, doorwayCenterY, 0f),
                    new Vector3(frameWidth, CubeRoom.DoorwayHeight, frameDepth), frameMaterial, false, false));
                renderers.Add(CreateCube("Doorway Frame Ceiling Header", passageRoot.transform,
                    new Vector3(0f, frameHeaderY, 0f),
                    new Vector3(CubeRoom.DoorwayWidth + frameWidth * 2f, frameWidth, frameDepth), frameMaterial, false, false));
                CreateDoorwayFrameEmitters(passageRoot.transform, doorwayCenterY, frameSideX, frameHeaderY);

                // Replace the selected full perimeter strip with two segments so the light
                // itself never spans the 2 m aperture. The matching proxy emitters live under
                // this same passage root and therefore follow its active state exactly.
                const float passageStripWidth = 0.07f;
                renderers.Add(CreateCube("Strip Ceiling Passage Left", passageRoot.transform,
                    new Vector3(-sideCenter, -3.87f, 0.13f),
                    new Vector3(sideWidth, passageStripWidth, passageStripWidth), frameMaterial, false, false));
                renderers.Add(CreateCube("Strip Ceiling Passage Right", passageRoot.transform,
                    new Vector3(sideCenter, -3.87f, 0.13f),
                    new Vector3(sideWidth, passageStripWidth, passageStripWidth), frameMaterial, false, false));
                renderers.Add(CreateCube("Housing Ceiling Passage Left", passageRoot.transform,
                    new Vector3(-sideCenter, -3.95f, 0.05f),
                    new Vector3(sideWidth, 0.14f, 0.14f), stripHousingMaterial, false, false));
                renderers.Add(CreateCube("Housing Ceiling Passage Right", passageRoot.transform,
                    new Vector3(sideCenter, -3.95f, 0.05f),
                    new Vector3(sideWidth, 0.14f, 0.14f), stripHousingMaterial, false, false));
                CreatePassageStripEmitter(passageRoot.transform, "Left", new Vector3(-sideCenter, -3.87f, 0.13f));
                CreatePassageStripEmitter(passageRoot.transform, "Right", new Vector3(sideCenter, -3.87f, 0.13f));

                const float trimHeight = 0.12f;
                const float trimDepth = 0.12f;
                float trimY = -CubeRoom.InteriorHeight * 0.5f + trimHeight * 0.5f;
                renderers.Add(CreateCube("Ceiling Opening Edge Trim Left", passageRoot.transform,
                    new Vector3(-sideCenter, trimY, 0.05f),
                    new Vector3(sideWidth, trimHeight, trimDepth), trimMaterial, false, false));
                renderers.Add(CreateCube("Ceiling Opening Edge Trim Right", passageRoot.transform,
                    new Vector3(sideCenter, trimY, 0.05f),
                    new Vector3(sideWidth, trimHeight, trimDepth), trimMaterial, false, false));

                float colliderSideWidth = (8.2f - CubeRoom.DoorwayWidth) * 0.5f;
                float colliderSideCenter = CubeRoom.DoorwayWidth * 0.5f + colliderSideWidth * 0.5f;
                Collider[] passageColliders =
                {
                    CreateBoxCollider("Ceiling Doorway Collider Left", passageRoot.transform,
                        new Vector3(-colliderSideCenter, 0f, 0.05f),
                        new Vector3(colliderSideWidth, CubeRoom.InteriorHeight, 0.10f)),
                    CreateBoxCollider("Ceiling Doorway Collider Right", passageRoot.transform,
                        new Vector3(colliderSideCenter, 0f, 0.05f),
                        new Vector3(colliderSideWidth, CubeRoom.InteriorHeight, 0.10f)),
                    CreateBoxCollider("Ceiling Doorway Collider Header", passageRoot.transform,
                        new Vector3(0f, headerCenter, 0.05f),
                        new Vector3(CubeRoom.DoorwayWidth, headerHeight, 0.10f)),
                };

                variants.Add(new CubeRoomCeilingBoundary.EdgeVariant(
                    definition.edge,
                    renderers.ToArray(),
                    passageColliders,
                    passageRoot));
            }

            boundary.Configure(
                new[] { closedGlass },
                new Collider[] { closedCollider },
                variants.ToArray());
            return boundary;
        }

        private static void CreateDoorwayFrameEmitters(
            Transform parent,
            float doorwayCenterY,
            float frameSideX,
            float frameHeaderY)
        {
            (string label, Vector3 position)[] sites =
            {
                ("Left", new Vector3(-frameSideX, doorwayCenterY, 0f)),
                ("Right", new Vector3(frameSideX, doorwayCenterY, 0f)),
                ("Header", new Vector3(0f, frameHeaderY, 0f)),
            };
            foreach ((string label, Vector3 position) site in sites)
            {
                CreateDoorwayEmitter(parent, site.label + " Inward", site.position + Vector3.forward * 0.025f, Vector3.forward);
                CreateDoorwayEmitter(parent, site.label + " Outward", site.position + Vector3.back * 0.025f, Vector3.back);
            }
        }

        private static Light CreateDoorwayEmitter(Transform parent, string label, Vector3 position, Vector3 direction)
        {
            GameObject gameObject = new GameObject("Doorway Emitter " + label);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);
            Light light = gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.color = NeutralWhite;
            light.intensity = 0.425f;
            light.range = 12f;
            light.spotAngle = 170f;
            light.innerSpotAngle = 160f;
            light.shadows = LightShadows.None;
            light.shadowNearPlane = 0.05f;
            light.cullingMask = ~0;
            light.renderingLayerMask = ~0;
            light.renderMode = LightRenderMode.ForcePixel;
            return light;
        }

        private static Light CreatePassageStripEmitter(Transform parent, string label, Vector3 position)
        {
            GameObject gameObject = new GameObject("Strip Passage Emitter " + label);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            Light light = gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.color = NeutralWhite;
            light.intensity = 0.85f;
            light.range = 12f;
            light.spotAngle = 170f;
            light.innerSpotAngle = 160f;
            light.shadows = LightShadows.None;
            light.shadowNearPlane = 0.05f;
            light.cullingMask = ~0;
            light.renderingLayerMask = ~0;
            light.renderMode = LightRenderMode.ForcePixel;
            return light;
        }

        private static Renderer CreateGlassSection(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector2 size,
            Material material)
        {
            GameObject pane = CreatePrimitive(
                name,
                PrimitiveType.Quad,
                parent,
                localPosition,
                new Vector3(size.x, size.y, 1f),
                material);
            Object.DestroyImmediate(pane.GetComponent<Collider>());
            Renderer renderer = pane.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static BoxCollider CreateBoxCollider(string name, Transform parent, Vector3 center, Vector3 size)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = center;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }

        private static StripEmitterDefinition[] CreateStripEmitterDefinitions()
        {
            List<StripEmitterDefinition> definitions = new List<StripEmitterDefinition>(28);
            float[] verticalHeights = { 0.10f, 2.68f, 5.34f, 7.84f };
            (string label, float x, float z)[] corners =
            {
                ("Vertical SW", -3.87f, -3.87f),
                ("Vertical SE",  3.87f, -3.87f),
                ("Vertical NW", -3.87f,  3.87f),
                ("Vertical NE",  3.87f,  3.87f),
            };

            foreach ((string label, float x, float z) corner in corners)
            {
                Vector3 inward = new Vector3(-Mathf.Sign(corner.x), 0f, -Mathf.Sign(corner.z)).normalized;
                for (int heightIndex = 0; heightIndex < verticalHeights.Length; heightIndex++)
                {
                    Vector3 direction = inward + Vector3.down * 0.02f;
                    definitions.Add(new StripEmitterDefinition(
                        $"{corner.label} {heightIndex + 1}",
                        new Vector3(corner.x, verticalHeights[heightIndex], corner.z),
                        direction));
                }
            }

            float[] ceilingOffsets = { -2.60f, 0f, 2.60f };
            for (int index = 0; index < ceilingOffsets.Length; index++)
            {
                float offset = ceilingOffsets[index];
                definitions.Add(new StripEmitterDefinition("Ceiling South " + (index + 1), new Vector3(offset, 7.87f, -3.87f), new Vector3(0f, -1f, 0.20f)));
                definitions.Add(new StripEmitterDefinition("Ceiling North " + (index + 1), new Vector3(offset, 7.87f, 3.87f), new Vector3(0f, -1f, -0.20f)));
                definitions.Add(new StripEmitterDefinition("Ceiling West " + (index + 1), new Vector3(-3.87f, 7.87f, offset), new Vector3(0.20f, -1f, 0f)));
                definitions.Add(new StripEmitterDefinition("Ceiling East " + (index + 1), new Vector3(3.87f, 7.87f, offset), new Vector3(-0.20f, -1f, 0f)));
            }

            return definitions.ToArray();
        }

        private static Light CreateSupportingLight(Transform parent, StripEmitterDefinition definition, int index)
        {
            GameObject gameObject = new GameObject($"Strip Emitter {index:00} - {definition.Name}");
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = definition.Position;
            gameObject.transform.localRotation = Quaternion.LookRotation(definition.Direction, Vector3.up);
            Light light = gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.color = NeutralWhite;
            light.intensity = 0.85f;
            light.range = 12f;
            light.spotAngle = 170f;
            light.innerSpotAngle = 160f;
            light.shadows = LightShadows.None;
            light.shadowNearPlane = 0.1f;
            light.cullingMask = ~0;
            light.renderingLayerMask = ~0;
            light.renderMode = LightRenderMode.ForcePixel;
            UniversalAdditionalLightData additionalLightData = light.GetUniversalAdditionalLightData();
            SerializedObject serializedLightData = new SerializedObject(additionalLightData);
            SetIntIfPresent(
                serializedLightData,
                "m_AdditionalLightsShadowResolutionTier",
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow);
            serializedLightData.ApplyModifiedPropertiesWithoutUndo();
            return light;
        }

        private static void ConfigureCamera(Camera camera, float fieldOfView, float nearClip, float farClip)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = nearClip;
            camera.farClipPlane = farClip;
            camera.allowHDR = true;
            camera.allowMSAA = true;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.transform;
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static GameObject CreateUiImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return gameObject;
        }

        private static Text CreateUiText(
            string name,
            Transform parent,
            string text,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text label = gameObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = new Color(0.86f, 0.92f, 0.98f, 1f);
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        private static void StretchToParent(RectTransform rectTransform, float inset)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(inset, inset);
            rectTransform.offsetMax = new Vector2(-inset, -inset);
        }

        private readonly struct LayerIds
        {
            public LayerIds(int player, int viewModel, int roomVolume)
            {
                Player = player;
                ViewModel = viewModel;
                RoomVolume = roomVolume;
            }

            public int Player { get; }
            public int ViewModel { get; }
            public int RoomVolume { get; }
        }

        private const string InputActionsJson = @"{
    ""name"": ""WhatLightRemainsInput"",
    ""maps"": [
        {
            ""name"": ""Player"",
            ""id"": ""f8a49a86-ae6d-4fea-95ea-ecf86658712b"",
            ""actions"": [
                { ""name"": ""Move"", ""type"": ""Value"", ""id"": ""fbaacdfa-4418-445b-8428-0dfa08ec6218"", ""expectedControlType"": ""Vector2"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": true },
                { ""name"": ""Look"", ""type"": ""Value"", ""id"": ""e2f84b39-d0cb-42fd-8d65-5d5c1b541c5c"", ""expectedControlType"": ""Vector2"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": true },
                { ""name"": ""Jump"", ""type"": ""Button"", ""id"": ""1fd455b7-9d8c-477b-9ea8-4584bd57f3fa"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                { ""name"": ""ReleaseCursor"", ""type"": ""Button"", ""id"": ""b75f8437-030b-4f69-a654-0ef177ac9fb1"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                { ""name"": ""CaptureCursor"", ""type"": ""Button"", ""id"": ""f3525105-cb20-4cd8-bf43-ef95eb8ee037"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                { ""name"": ""ToggleCreate"", ""type"": ""Button"", ""id"": ""fe1131a6-5d0b-4c84-b97b-110f5f2a77ac"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                { ""name"": ""PlaceRoom"", ""type"": ""Button"", ""id"": ""48ba252b-3562-44d1-936c-88f433ab2d51"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                { ""name"": ""Sprint"", ""type"": ""Button"", ""id"": ""d1f3b5dc-d57c-48d6-a369-6545085ec79e"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": true },
                { ""name"": ""RotateModifier"", ""type"": ""Button"", ""id"": ""23d2106b-77e8-4f37-8a68-4c8e163cfed4"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": true },
                { ""name"": ""AlternateRotationAxis"", ""type"": ""Button"", ""id"": ""05e33d90-555f-486f-aa8a-87a8f4818763"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": true },
                { ""name"": ""RotationScroll"", ""type"": ""Value"", ""id"": ""348de50f-ebd6-43cc-a07c-349b7b54bfa9"", ""expectedControlType"": ""Vector2"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": true },
                { ""name"": ""Traverse"", ""type"": ""Button"", ""id"": ""7f634884-4d48-47e8-9681-e021214690e1"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                { ""name"": ""ToggleDelete"", ""type"": ""Button"", ""id"": ""879bd2db-a10e-44bd-a6e4-535ea562521c"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false },
                { ""name"": ""DeleteRoom"", ""type"": ""Button"", ""id"": ""7b4df604-8f24-4501-8d67-326d74f0fc91"", ""expectedControlType"": ""Button"", ""processors"": """", ""interactions"": """", ""initialStateCheck"": false }
            ],
            ""bindings"": [
                { ""name"": ""WASD"", ""id"": ""547d0149-dfbb-4be4-b147-475b0ceaa247"", ""path"": ""2DVector"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Move"", ""isComposite"": true, ""isPartOfComposite"": false },
                { ""name"": ""up"", ""id"": ""6ad44007-af59-46fd-ab0c-96e909cb5250"", ""path"": ""<Keyboard>/w"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Move"", ""isComposite"": false, ""isPartOfComposite"": true },
                { ""name"": ""down"", ""id"": ""3ccfa9de-3934-4193-b30f-459c52cf78e1"", ""path"": ""<Keyboard>/s"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Move"", ""isComposite"": false, ""isPartOfComposite"": true },
                { ""name"": ""left"", ""id"": ""469ecc17-efda-4a83-9b7d-2f63cee584be"", ""path"": ""<Keyboard>/a"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Move"", ""isComposite"": false, ""isPartOfComposite"": true },
                { ""name"": ""right"", ""id"": ""5d44915f-fbe0-4f0e-bfed-1f68636a448c"", ""path"": ""<Keyboard>/d"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Move"", ""isComposite"": false, ""isPartOfComposite"": true },
                { ""name"": """", ""id"": ""cda9e182-3d11-41a6-a562-d080ce5c6664"", ""path"": ""<Mouse>/delta"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Look"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""0a60c9ac-b0db-4c52-8d68-d80e48979076"", ""path"": ""<Keyboard>/space"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Jump"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""27d14848-d21b-4275-9b98-05ca76289f74"", ""path"": ""<Keyboard>/escape"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""ReleaseCursor"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""7eb4af7e-76e6-4b7a-bd29-b6fa98d1b3be"", ""path"": ""<Mouse>/leftButton"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""CaptureCursor"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""6328840f-0870-4f0f-9024-1e0a246302a4"", ""path"": ""<Keyboard>/c"", ""interactions"": ""Press"", ""processors"": """", ""groups"": """", ""action"": ""ToggleCreate"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""0d660884-6f90-4f75-8d97-dd5df83e3faa"", ""path"": ""<Mouse>/leftButton"", ""interactions"": ""Press"", ""processors"": """", ""groups"": """", ""action"": ""PlaceRoom"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""0ce9cf3e-ffb1-42e1-8960-fd83f247f687"", ""path"": ""<Keyboard>/leftShift"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Sprint"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""4d07b074-e94d-42c6-b4d1-65f6d6b9e6e5"", ""path"": ""<Keyboard>/leftCtrl"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""RotateModifier"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""67978e59-3b69-4139-a50f-f018bb7e6130"", ""path"": ""<Keyboard>/rightCtrl"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""RotateModifier"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""60b07283-3630-407e-abee-0b6e57b8f4a1"", ""path"": ""<Keyboard>/leftAlt"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""AlternateRotationAxis"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""9707ed9c-0e82-48e5-9baa-29e43097d73b"", ""path"": ""<Keyboard>/rightAlt"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""AlternateRotationAxis"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""34b638e9-cfc2-4ba5-87a4-084f71cd3f84"", ""path"": ""<Mouse>/scroll"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""RotationScroll"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""5ec9e9cf-86bd-4b06-b63d-03f5db39ef34"", ""path"": ""<Keyboard>/e"", ""interactions"": """", ""processors"": """", ""groups"": """", ""action"": ""Traverse"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""052ce469-3af8-4414-90b8-a69c8196c8ab"", ""path"": ""<Keyboard>/delete"", ""interactions"": ""Press"", ""processors"": """", ""groups"": """", ""action"": ""ToggleDelete"", ""isComposite"": false, ""isPartOfComposite"": false },
                { ""name"": """", ""id"": ""db7eb8aa-f86d-4652-855c-73b476c50d52"", ""path"": ""<Mouse>/rightButton"", ""interactions"": ""Press"", ""processors"": """", ""groups"": """", ""action"": ""DeleteRoom"", ""isComposite"": false, ""isPartOfComposite"": false }
            ]
        }
    ],
    ""controlSchemes"": []
}";
    }
}
