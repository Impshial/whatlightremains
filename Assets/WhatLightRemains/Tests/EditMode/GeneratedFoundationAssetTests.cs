using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class GeneratedFoundationAssetTests
    {
        private const string CubePrefabPath = "Assets/WhatLightRemains/Generated/Prefabs/CubeRoom.prefab";
        private const string PlayerPrefabPath = "Assets/WhatLightRemains/Generated/Prefabs/Player.prefab";
        private const string HotbarPrefabPath = "Assets/WhatLightRemains/Generated/Prefabs/Hotbar.prefab";
        private const string InputActionsPath = "Assets/WhatLightRemains/Generated/Input/WhatLightRemainsInput.inputactions";
        private const string GlassMaterialPath = "Assets/WhatLightRemains/Generated/Materials/ClearGlass.mat";
        private const string FloorMaterialPath = "Assets/WhatLightRemains/Generated/Materials/Floor.mat";
        private const string StripMaterialPath = "Assets/WhatLightRemains/Generated/Materials/LightStrip.mat";
        private const string StripHousingMaterialPath = "Assets/WhatLightRemains/Generated/Materials/LightStripHousing.mat";
        private const string BaseRailMaterialPath = "Assets/WhatLightRemains/Generated/Materials/BaseRail.mat";
        private const string RoomPreviewMaterialPath = "Assets/WhatLightRemains/Generated/Materials/RoomPreview.mat";
        private const string FloorBaseColorPath = "Assets/WhatLightRemains/Art/Textures/Floor/Floor_BaseColor.png";
        private const string FloorNormalPath = "Assets/WhatLightRemains/Art/Textures/Floor/Floor_Normal.png";
        private const string GlassDetailPath = "Assets/WhatLightRemains/Art/Textures/Glass/Glass_Detail.png";
        private const string GlassNormalPath = "Assets/WhatLightRemains/Art/Textures/Glass/Glass_Normal.png";
        private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string PcPipelinePath = "Assets/Settings/PC_RPAsset.asset";
        private const string FoundationScenePath = "Assets/WhatLightRemains/Generated/Scenes/Foundation.unity";
        private const string ValidationScenePath = "Assets/WhatLightRemains/Generated/Scenes/Validation.unity";
        private const string BuilderHint =
            "Run the What Light Remains foundation builder to generate the required assets, then rerun the tests.";

        [Test]
        public void CubePrefab_IsSelfContainedAndHasExpectedFoundationStructure()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(CubePrefabPath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, new Vector3(1000f, 1000f, 1000f), Quaternion.identity);

            try
            {
                CubeRoom[] rooms = instance.GetComponentsInChildren<CubeRoom>(true);
                CubeRoomLighting[] lightingRigs = instance.GetComponentsInChildren<CubeRoomLighting>(true);
                Assert.That(rooms, Has.Length.EqualTo(1));
                Assert.That(lightingRigs, Has.Length.EqualTo(1));
                Assert.That(rooms[0].gameObject, Is.SameAs(instance), "CubeRoom must be attached to the prefab root.");
                Assert.That(instance.GetComponentsInChildren<RoomOccupancyVolume>(true), Has.Length.EqualTo(1));
                Assert.That(rooms[0].Lighting, Is.SameAs(lightingRigs[0]), "CubeRoom must own its local lighting rig.");
                Assert.That(rooms[0].OccupancyVolume, Is.Not.Null, "CubeRoom must own its local occupancy volume.");
                Assert.That(
                    instance.GetComponentsInChildren<Collider>(true).Count(collider => !collider.isTrigger),
                    Is.GreaterThanOrEqualTo(6),
                    "The floor, four walls, and ceiling must all have solid collision.");
                Assert.That(
                    instance.GetComponentsInChildren<Collider>(true).Any(collider => collider.isTrigger),
                    Is.True,
                    "The cube requires an interior trigger volume for room ownership.");

                AssertConfiguredStripRenderers(instance, lightingRigs[0]);
                AssertConfiguredGlassRenderers(instance);
                AssertConfiguredWallBoundaryColliders(instance, rooms[0]);

                Assert.That(instance.GetComponentsInChildren<Camera>(true), Is.Empty, "The cube prefab must not own a camera.");
                Assert.That(instance.GetComponentsInChildren<Canvas>(true), Is.Empty, "The cube prefab must not own HUD UI.");
                Assert.That(instance.GetComponentsInChildren<FirstPersonMotor>(true), Is.Empty, "The cube prefab must not own a player.");
                Assert.That(instance.GetComponentsInChildren<PlayerRoomTracker>(true), Is.Empty, "The cube prefab must not own player state.");

                AssertInteriorBoundaryDistances(instance);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PlayerAndHotbarPrefabs_AreGeneratedAndSeparatedFromRoom()
        {
            GameObject player = LoadRequiredAsset<GameObject>(PlayerPrefabPath);
            GameObject hotbar = LoadRequiredAsset<GameObject>(HotbarPrefabPath);

            Assert.That(player.GetComponentInChildren<FirstPersonMotor>(true), Is.Not.Null);
            Assert.That(player.GetComponentInChildren<FirstPersonInput>(true), Is.Not.Null);
            Assert.That(player.GetComponentInChildren<PlayerRoomTracker>(true), Is.Not.Null);
            Assert.That(player.GetComponentInChildren<RoomCreationController>(true), Is.Not.Null);
            Assert.That(player.GetComponentInChildren<Camera>(true), Is.Not.Null);
            Assert.That(player.GetComponentInChildren<CubeRoom>(true), Is.Null);
            Assert.That(
                player.GetComponentsInChildren<Rigidbody>(true).All(body => !body.useGravity),
                Is.True,
                "Player bodies receiving room gravity must not also use global Rigidbody gravity.");
            FirstPersonMotor motor = player.GetComponentInChildren<FirstPersonMotor>(true);
            CharacterController controller = player.GetComponent<CharacterController>();
            Assert.That(motor.ControllerHeight, Is.EqualTo(1.8f).Within(0.001f));
            Assert.That(motor.ControllerRadius, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(controller.height, Is.EqualTo(1.8f).Within(0.001f));
            Assert.That(controller.radius, Is.EqualTo(0.3f).Within(0.001f));
            Transform pitchPivot = player.transform.Find("Pitch Pivot");
            Assert.That(pitchPivot.localPosition.y, Is.EqualTo(1.65f).Within(0.001f));
            Assert.That(
                Quaternion.Angle(pitchPivot.localRotation, Quaternion.Euler(-6f, 0f, 0f)),
                Is.LessThan(0.01f),
                "The player should start looking slightly upward so the ceiling-perimeter lights are visible.");
            HotbarView hotbarView = hotbar.GetComponentInChildren<HotbarView>(true);
            Assert.That(hotbarView, Is.Not.Null);
            Assert.That(hotbarView.SlotCount, Is.EqualTo(8));
            Assert.That(hotbarView.SelectedIndex, Is.EqualTo(0));
            GlassOpacityControl opacityControl = hotbar.GetComponentInChildren<GlassOpacityControl>(true);
            Assert.That(opacityControl, Is.Not.Null);
            Assert.That(opacityControl.MinimumOpacity, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(opacityControl.MaximumOpacity, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(opacityControl.Opacity, Is.EqualTo(0.035f).Within(0.0001f));
            RoomCreationPromptView creationPrompt = hotbar.GetComponentInChildren<RoomCreationPromptView>(true);
            Assert.That(creationPrompt, Is.Not.Null);
            Assert.That(creationPrompt.CurrentText, Is.EqualTo(RoomCreationPromptView.NormalText));
            Assert.That(creationPrompt.InstructionLabel.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(creationPrompt.InstructionLabel.rectTransform.anchorMax, Is.EqualTo(Vector2.zero));
            Assert.That(hotbar.GetComponentsInChildren<Slider>(true), Has.Length.EqualTo(1));
            Assert.That(hotbar.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(hotbar.GetComponentInChildren<Canvas>(true).renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(hotbar.GetComponentInChildren<CubeRoom>(true), Is.Null);
        }

        [Test]
        public void InputAsset_ContainsTheFoundationKeyboardAndMouseActions()
        {
            InputActionAsset actions = LoadRequiredAsset<InputActionAsset>(InputActionsPath);
            InputAction move = actions.FindAction("Player/Move", true);

            Assert.That(move.bindings.Any(binding => binding.isComposite && binding.path == "2DVector"), Is.True);
            Assert.That(move.bindings.Select(binding => binding.path), Does.Contain("<Keyboard>/w"));
            Assert.That(move.bindings.Select(binding => binding.path), Does.Contain("<Keyboard>/a"));
            Assert.That(move.bindings.Select(binding => binding.path), Does.Contain("<Keyboard>/s"));
            Assert.That(move.bindings.Select(binding => binding.path), Does.Contain("<Keyboard>/d"));
            Assert.That(actions.FindAction("Player/Look", true).bindings.Select(binding => binding.path), Does.Contain("<Mouse>/delta"));
            Assert.That(actions.FindAction("Player/Jump", true).bindings.Select(binding => binding.path), Does.Contain("<Keyboard>/space"));
            Assert.That(actions.FindAction("Player/ReleaseCursor", true).bindings.Select(binding => binding.path), Does.Contain("<Keyboard>/escape"));
            Assert.That(actions.FindAction("Player/CaptureCursor", true).bindings.Select(binding => binding.path), Does.Contain("<Mouse>/leftButton"));
            Assert.That(actions.FindAction("Player/ToggleCreate", true).bindings.Select(binding => binding.path), Does.Contain("<Keyboard>/c"));
            Assert.That(actions.FindAction("Player/PlaceRoom", true).bindings.Select(binding => binding.path), Does.Contain("<Mouse>/leftButton"));

            Material preview = LoadRequiredAsset<Material>(RoomPreviewMaterialPath);
            Assert.That(preview.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
            Assert.That(preview.GetTag("RenderType", false, string.Empty), Is.EqualTo("Transparent"));
            Assert.That(preview.renderQueue, Is.GreaterThan((int)RenderQueue.Transparent));

            Assert.That(
                EditorBuildSettings.TryGetConfigObject("com.unity.input.settings.actions", out InputActionAsset configuredActions),
                Is.True,
                "The generated actions must also be the project-wide Input System actions asset.");
            Assert.That(AssetDatabase.GetAssetPath(configuredActions), Is.EqualTo(InputActionsPath));
        }

        [Test]
        public void Rendering_UsesForwardPlusWithoutGlobalEffectsAndLightTransmittingGlass()
        {
            ScriptableRendererData renderer = LoadRequiredAsset<ScriptableRendererData>(PcRendererPath);
            SerializedObject rendererSettings = new SerializedObject(renderer);
            Assert.That(rendererSettings.FindProperty("m_RenderingMode").intValue, Is.EqualTo(2));
            Assert.That(
                renderer.rendererFeatures.Any(feature =>
                    feature != null
                    && feature.GetType().Name.IndexOf("AmbientOcclusion", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False,
                "The foundation renderer must not include SSAO.");

            UniversalRenderPipelineAsset pipeline = LoadRequiredAsset<UniversalRenderPipelineAsset>(PcPipelinePath);
            SerializedObject pipelineSettings = new SerializedObject(pipeline);
            Assert.That(pipelineSettings.FindProperty("m_VolumeProfile").objectReferenceValue, Is.Null);
            Assert.That(pipelineSettings.FindProperty("m_AdditionalLightsShadowmapResolution").intValue, Is.EqualTo(4096));
            Assert.That(
                pipelineSettings.FindProperty("m_RequireOpaqueTexture").boolValue,
                Is.True,
                "The glass distortion samples URP's opaque-scene texture.");
            Assert.That(
                pipelineSettings.FindProperty("m_OpaqueDownsampling").intValue,
                Is.EqualTo(0),
                "The subtle thick-glass distortion should use the full-resolution opaque texture.");

            Material glass = LoadRequiredAsset<Material>(GlassMaterialPath);
            Assert.That(glass.shader.name, Is.EqualTo("What Light Remains/Light-Transmitting Glass"));
            Assert.That(glass.GetTag("RenderType", false, string.Empty), Is.EqualTo("Transparent"));
            Assert.That(glass.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
            Assert.That(glass.GetFloat("_Cull"), Is.EqualTo((float)CullMode.Off));
            Assert.That(glass.GetFloat("_SrcBlend"), Is.EqualTo((float)BlendMode.One));
            Assert.That(glass.GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.OneMinusSrcAlpha));
            Assert.That(glass.GetFloat("_ZWrite"), Is.EqualTo(0f));
            Assert.That(glass.FindPass("ShadowCaster"), Is.EqualTo(-1), "Glass must never contribute a shadow-caster pass.");
            Assert.That(glass.FindPass("ForwardLit"), Is.EqualTo(-1), "Glass must not use a Lit forward pass or respond to proxy lights/reflection probes.");
            Assert.That(glass.HasProperty("_Metallic"), Is.False);
            Assert.That(glass.HasProperty("_Smoothness"), Is.False);
            Assert.That(glass.HasProperty("_SpecColor"), Is.False);
            Assert.That(glass.HasProperty("_SpecularHighlights"), Is.False);
            Assert.That(glass.HasProperty("_EnvironmentReflections"), Is.False);
            Assert.That(glass.HasProperty("_ReflectionProbeUsage"), Is.False);
            Assert.That(glass.HasProperty("_Tint"), Is.True);
            Assert.That(glass.HasProperty("_ImperfectionStrength"), Is.True);
            Assert.That(glass.HasProperty("_NormalDetailStrength"), Is.True);
            Assert.That(glass.HasProperty("_DetailContrast"), Is.True);
            Assert.That(glass.HasProperty("_DetailThreshold"), Is.True);
            Assert.That(glass.HasProperty("_HighPassMip"), Is.True);
            Assert.That(glass.HasProperty("_DistortionPixels"), Is.True);
            Assert.That(glass.HasProperty("_DistortionBlend"), Is.True);
            Assert.That(glass.HasProperty("_DistortionMip"), Is.True);
            Assert.That(glass.HasProperty("_GlassTint"), Is.True);
            Assert.That(glass.HasProperty("_Translucency"), Is.True);

            Color glassTint = glass.GetColor("_Tint");
            Assert.That(glassTint.r, Is.EqualTo(0.22f).Within(0.001f));
            Assert.That(glassTint.g, Is.EqualTo(0.30f).Within(0.001f));
            Assert.That(glassTint.b, Is.EqualTo(0.34f).Within(0.001f));
            Assert.That(glassTint.a, Is.EqualTo(1f).Within(0.001f));
            Color glassBodyTint = glass.GetColor("_GlassTint");
            Assert.That(glassBodyTint.r, Is.EqualTo(0.10f).Within(0.001f));
            Assert.That(glassBodyTint.g, Is.EqualTo(0.14f).Within(0.001f));
            Assert.That(glassBodyTint.b, Is.EqualTo(0.16f).Within(0.001f));

            Material strip = LoadRequiredAsset<Material>(StripMaterialPath);
            Assert.That(strip.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));

            Material housing = LoadRequiredAsset<Material>(StripHousingMaterialPath);
            Assert.That(housing.GetFloat("_Surface"), Is.EqualTo(0f));

            Material baseRail = LoadRequiredAsset<Material>(BaseRailMaterialPath);
            Assert.That(baseRail.GetFloat("_Surface"), Is.EqualTo(0f));
            Color railColor = baseRail.GetColor("_BaseColor");
            Assert.That(railColor.r, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(railColor.g, Is.EqualTo(0.21f).Within(0.001f));
            Assert.That(railColor.b, Is.EqualTo(0.24f).Within(0.001f));
        }

        [Test]
        public void SurfaceTextures_AreHighDefinitionAndAppliedWithRepeatImportSettings()
        {
            Texture2D floorBase = LoadRequiredAsset<Texture2D>(FloorBaseColorPath);
            Texture2D floorNormal = LoadRequiredAsset<Texture2D>(FloorNormalPath);
            Texture2D glassDetail = LoadRequiredAsset<Texture2D>(GlassDetailPath);
            Texture2D glassNormal = LoadRequiredAsset<Texture2D>(GlassNormalPath);

            AssertHighDefinitionTexture(floorBase, FloorBaseColorPath, false);
            AssertHighDefinitionTexture(floorNormal, FloorNormalPath, true);
            AssertHighDefinitionTexture(glassDetail, GlassDetailPath, false);
            AssertHighDefinitionTexture(glassNormal, GlassNormalPath, true);

            Material floor = LoadRequiredAsset<Material>(FloorMaterialPath);
            Assert.That(floor.GetTexture("_BaseMap"), Is.SameAs(floorBase));
            Assert.That(floor.GetTexture("_BumpMap"), Is.SameAs(floorNormal));
            Assert.That(floor.IsKeywordEnabled("_NORMALMAP"), Is.True);
            Assert.That(floor.GetFloat("_Smoothness"), Is.EqualTo(0.28f).Within(0.001f));
            Assert.That(floor.GetFloat("_BumpScale"), Is.EqualTo(0.50f).Within(0.001f));
            Assert.That(
                Vector2.Distance(floor.GetTextureScale("_BaseMap"), Vector2.one),
                Is.LessThan(0.001f),
                "The authored 4-by-4 slab layout should span the full eight-metre floor without further tiling.");

            Material glass = LoadRequiredAsset<Material>(GlassMaterialPath);
            Assert.That(glass.GetTexture("_BaseMap"), Is.SameAs(glassDetail));
            Assert.That(glass.GetTexture("_BumpMap"), Is.SameAs(glassNormal));
            Assert.That(Vector2.Distance(glass.GetTextureScale("_BaseMap"), Vector2.one), Is.LessThan(0.001f));
            Assert.That(Vector2.Distance(glass.GetTextureScale("_BumpMap"), Vector2.one), Is.LessThan(0.001f));
            Assert.That(glass.GetFloat("_ImperfectionStrength"), Is.EqualTo(0.004f).Within(0.0001f));
            Assert.That(glass.GetFloat("_NormalDetailStrength"), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(glass.GetFloat("_DetailContrast"), Is.EqualTo(4f).Within(0.001f));
            Assert.That(glass.GetFloat("_DetailThreshold"), Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(glass.GetFloat("_HighPassMip"), Is.EqualTo(5f).Within(0.001f));
            Assert.That(glass.GetFloat("_DistortionPixels"), Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(glass.GetFloat("_DistortionBlend"), Is.EqualTo(0.55f).Within(0.001f));
            Assert.That(glass.GetFloat("_DistortionMip"), Is.EqualTo(4f).Within(0.001f));
            Assert.That(glass.GetFloat("_Translucency"), Is.EqualTo(0.035f).Within(0.001f));
        }

        [Test]
        public void CubePrefab_TwoInstancesKeepRoomAndLightingConfigurationIndependent()
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(CubePrefabPath);
            GameObject firstInstance = UnityEngine.Object.Instantiate(prefab);
            GameObject secondInstance = UnityEngine.Object.Instantiate(prefab, new Vector3(16f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f));

            try
            {
                CubeRoom firstRoom = firstInstance.GetComponent<CubeRoom>();
                CubeRoom secondRoom = secondInstance.GetComponent<CubeRoom>();
                Assert.That(firstRoom, Is.Not.Null);
                Assert.That(secondRoom, Is.Not.Null);
                Assert.That(firstRoom, Is.Not.SameAs(secondRoom));
                Assert.That(firstRoom.Lighting, Is.Not.Null);
                Assert.That(secondRoom.Lighting, Is.Not.Null);

                float firstGravity = firstRoom.GravityStrength;
                Color firstColor = firstRoom.Lighting.StripColor;
                secondRoom.GravityStrength = 3.25f;
                secondRoom.Lighting.StripColor = Color.magenta;
                secondRoom.SetLightingEnabled(false);

                Assert.That(firstRoom.GravityStrength, Is.EqualTo(firstGravity).Within(0.0001f));
                Assert.That(firstRoom.Lighting.StripColor, Is.EqualTo(firstColor));
                Assert.That(firstRoom.Lighting.IsLightingEnabled, Is.True);
                Assert.That(secondRoom.GravityAcceleration.magnitude, Is.EqualTo(3.25f).Within(0.0001f));
                Assert.That(
                    Vector3.Dot(secondRoom.GravityAcceleration.normalized, -secondInstance.transform.up),
                    Is.GreaterThan(0.9999f));
                Assert.That(secondRoom.Lighting.IsLightingEnabled, Is.False);

                Assert.That(secondInstance.GetComponentsInChildren<Camera>(true), Is.Empty);
                Assert.That(secondInstance.GetComponentsInChildren<FirstPersonMotor>(true), Is.Empty);
                Assert.That(secondInstance.GetComponentsInChildren<Canvas>(true), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstInstance);
                UnityEngine.Object.DestroyImmediate(secondInstance);
            }
        }

        [Test]
        public void FoundationScene_HasOneAuthoredRoomClusterAndOnePlayerPresentation()
        {
            Assert.That(System.IO.File.Exists(FoundationScenePath), Is.True, MissingAssetMessage(FoundationScenePath));

            Scene scene = SceneManager.GetSceneByPath(FoundationScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(FoundationScenePath, OpenSceneMode.Additive);
            }

            try
            {
                List<CubeRoom> rooms = FindInScene<CubeRoom>(scene);
                List<CubeRoomClusterGenerator> clusters = FindInScene<CubeRoomClusterGenerator>(scene);
                List<FirstPersonMotor> players = FindInScene<FirstPersonMotor>(scene);
                List<HotbarView> hotbars = FindInScene<HotbarView>(scene);
                Assert.That(rooms, Has.Count.EqualTo(1));
                Assert.That(clusters, Has.Count.EqualTo(1));
                Assert.That(players, Has.Count.EqualTo(1));
                List<PlayerRoomTracker> trackers = FindInScene<PlayerRoomTracker>(scene);
                Assert.That(trackers, Has.Count.EqualTo(1));
                AssertCameraStack(FindInScene<Camera>(scene));
                Assert.That(
                    FindInScene<Volume>(scene),
                    Is.Empty,
                    "The black-void foundation scene must not contain a scene volume.");
                Assert.That(hotbars, Has.Count.EqualTo(1));
                AssertPrefabSource(rooms[0].gameObject, CubePrefabPath);
                AssertPrefabSource(players[0].gameObject, PlayerPrefabPath);
                AssertPrefabSource(hotbars[0].gameObject, HotbarPrefabPath);

                CubeRoomClusterGenerator cluster = clusters[0];
                Assert.That(cluster.gameObject.name, Is.EqualTo("Room Cluster"));
                Assert.That(cluster.PrimaryRoom, Is.SameAs(rooms[0]));
                Assert.That(cluster.PrimaryRoom.transform.parent, Is.SameAs(cluster.transform));
                Assert.That(cluster.PrimaryRoom.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(cluster.AdditionalRoomCount, Is.EqualTo(0));
                Assert.That(cluster.RoomPrefab, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(cluster.RoomPrefab), Is.EqualTo(CubePrefabPath));
                Assert.That(cluster.Rooms, Is.Empty, "Generated neighbors must not be serialized into the Foundation scene.");
                Assert.That(cluster.GridCells, Is.Empty, "The runtime registry must initialize from the one authored room when play begins.");

                RoomCreationController creation = players[0].GetComponent<RoomCreationController>();
                Assert.That(creation, Is.Not.Null);
                SerializedObject creationData = new SerializedObject(creation);
                Assert.That(creationData.FindProperty("roomLayout").objectReferenceValue, Is.SameAs(cluster));
                Assert.That(
                    creationData.FindProperty("promptView").objectReferenceValue,
                    Is.SameAs(hotbars[0].GetComponent<RoomCreationPromptView>()));

                SerializedObject trackerData = new SerializedObject(trackers[0]);
                Assert.That(
                    trackerData.FindProperty("startingRoom").objectReferenceValue,
                    Is.SameAs(cluster.PrimaryRoom),
                    "The player must serialize an explicit reference to the authored primary room.");
                Assert.That(
                    Quaternion.Angle(players[0].transform.rotation, Quaternion.Euler(0f, 45f, 0f)),
                    Is.LessThan(0.01f),
                    "The foundation start should frame more than one illuminated corner.");
                Assert.That(
                    FindInScene<Light>(scene).Any(light => light.type == LightType.Directional),
                    Is.False,
                    "The black-void foundation scene must not contain a sun or directional light.");
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void ValidationScene_ProvesRoomIndependenceAndExternalLightingLayout()
        {
            Assert.That(System.IO.File.Exists(ValidationScenePath), Is.True, MissingAssetMessage(ValidationScenePath));
            Scene scene = EditorSceneManager.OpenScene(ValidationScenePath, OpenSceneMode.Additive);

            try
            {
                List<CubeRoom> rooms = FindInScene<CubeRoom>(scene);
                Assert.That(rooms, Has.Count.EqualTo(2));
                CubeRoom source = rooms.Single(room => room.name.Contains("Lighting Enabled"));
                CubeRoom independent = rooms.Single(room => room.name.Contains("Lighting Disabled"));

                Assert.That(source.Lighting.IsLightingEnabled, Is.True);
                Assert.That(independent.Lighting.IsLightingEnabled, Is.False);
                Assert.That(independent.GravityStrength, Is.EqualTo(4.905f).Within(0.0001f));
                Assert.That(Vector3.Distance(source.transform.position, independent.transform.position), Is.EqualTo(16f).Within(0.001f));
                Assert.That(Quaternion.Angle(independent.transform.rotation, Quaternion.Euler(0f, 0f, 90f)), Is.LessThan(0.01f));
                Assert.That(Vector3.Dot(independent.GravityAcceleration.normalized, Vector3.right), Is.GreaterThan(0.999f));

                Assert.That(FindInScene<FirstPersonMotor>(scene), Has.Count.EqualTo(1));
                Assert.That(FindInScene<PlayerRoomTracker>(scene), Has.Count.EqualTo(1));
                Assert.That(FindInScene<HotbarView>(scene), Has.Count.EqualTo(1));
                AssertCameraStack(FindInScene<Camera>(scene));

                GameObject blocker = scene.GetRootGameObjects().Single(root => root.name == "Opaque Shadow Blocker");
                GameObject receiver = scene.GetRootGameObjects().Single(root => root.name == "External Light Receiver");
                Renderer blockerRenderer = blocker.GetComponent<Renderer>();
                Renderer receiverRenderer = receiver.GetComponent<Renderer>();
                Assert.That(blockerRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
                Assert.That(receiverRenderer.receiveShadows, Is.True);
                Assert.That(receiver.transform.position.x, Is.GreaterThan(blocker.transform.position.x));
                Assert.That(Vector3.Distance(blocker.transform.position, new Vector3(4.75f, 4f, -3.72f)), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(blocker.transform.localScale, new Vector3(0.35f, 1.4f, 1.4f)), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(receiver.transform.position, new Vector3(5.75f, 4f, -3f)), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(receiver.transform.localScale, new Vector3(0.2f, 5f, 4f)), Is.LessThan(0.001f));

                Light testLight = source.GetComponentsInChildren<Light>(true)
                    .OrderBy(light => Vector3.Distance(light.transform.localPosition, new Vector3(3.72f, 4f, -3.72f)))
                    .First();
                Vector3 shadowTarget = new Vector3(receiver.transform.position.x, blocker.transform.position.y, blocker.transform.position.z);
                Assert.That(Vector3.Distance(testLight.transform.position, shadowTarget), Is.LessThan(testLight.range));
                Ray shadowRay = new Ray(testLight.transform.position, (shadowTarget - testLight.transform.position).normalized);
                Assert.That(blocker.GetComponent<Collider>().bounds.IntersectRay(shadowRay, out float blockerDistance), Is.True);
                Assert.That(blockerDistance, Is.LessThan(Vector3.Distance(testLight.transform.position, shadowTarget)));
                Assert.That(receiverRenderer.sharedMaterial.GetFloat("_Surface"), Is.EqualTo(0f), "The receiver must remain opaque.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T LoadRequiredAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, MissingAssetMessage(path));
            return asset;
        }

        private static string MissingAssetMessage(string path)
        {
            return $"Required generated asset is missing at '{path}'. {BuilderHint}";
        }

        private static List<T> FindInScene<T>(Scene scene) where T : Component
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToList();
        }

        private static void AssertCameraStack(List<Camera> cameras)
        {
            Assert.That(cameras, Has.Count.EqualTo(2), "Expected one gameplay camera and one viewmodel overlay camera.");

            Camera baseCamera = cameras.SingleOrDefault(camera =>
                camera.TryGetComponent(out UniversalAdditionalCameraData data)
                && data.renderType == CameraRenderType.Base);
            Camera overlayCamera = cameras.SingleOrDefault(camera =>
                camera.TryGetComponent(out UniversalAdditionalCameraData data)
                && data.renderType == CameraRenderType.Overlay);

            Assert.That(baseCamera, Is.Not.Null, "Exactly one camera must use the URP Base render type.");
            Assert.That(overlayCamera, Is.Not.Null, "Exactly one camera must use the URP Overlay render type.");
            Assert.That(baseCamera.CompareTag("MainCamera"), Is.True, "The URP Base camera must be tagged MainCamera.");
            Assert.That(baseCamera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(baseCamera.backgroundColor.maxColorComponent, Is.LessThanOrEqualTo(0.001f));
            Assert.That(baseCamera.fieldOfView, Is.EqualTo(75f).Within(0.01f));
            Assert.That(
                overlayCamera.name.IndexOf("viewmodel", StringComparison.OrdinalIgnoreCase),
                Is.GreaterThanOrEqualTo(0),
                "The URP Overlay camera must be clearly identified as the viewmodel camera.");
            Assert.That(
                baseCamera.GetComponent<UniversalAdditionalCameraData>().cameraStack,
                Does.Contain(overlayCamera),
                "The gameplay camera stack must include the viewmodel overlay camera.");
            Assert.That(overlayCamera.fieldOfView, Is.EqualTo(55f).Within(0.01f));
            Assert.That(overlayCamera.GetComponent<UniversalAdditionalCameraData>().clearDepth, Is.True);
            Assert.That(baseCamera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing, Is.False);
            Assert.That(overlayCamera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing, Is.False);
        }

        private static void AssertConfiguredStripRenderers(GameObject roomInstance, CubeRoomLighting lighting)
        {
            SerializedObject serializedLighting = new SerializedObject(lighting);
            SerializedProperty strips = serializedLighting.FindProperty("stripRenderers");
            Assert.That(strips, Is.Not.Null, "CubeRoomLighting must serialize its strip renderers.");
            Assert.That(
                strips.arraySize,
                Is.EqualTo(8),
                "The room rig must contain exactly four vertical and four ceiling-perimeter strip renderers.");

            Dictionary<string, Vector3> expectedPositions = new Dictionary<string, Vector3>
            {
                { "Strip Vertical SW", new Vector3(-3.87f, 4f, -3.87f) },
                { "Strip Vertical SE", new Vector3(3.87f, 4f, -3.87f) },
                { "Strip Vertical NW", new Vector3(-3.87f, 4f, 3.87f) },
                { "Strip Vertical NE", new Vector3(3.87f, 4f, 3.87f) },
                { "Strip Ceiling South", new Vector3(0f, 7.87f, -3.87f) },
                { "Strip Ceiling North", new Vector3(0f, 7.87f, 3.87f) },
                { "Strip Ceiling West", new Vector3(-3.87f, 7.87f, 0f) },
                { "Strip Ceiling East", new Vector3(3.87f, 7.87f, 0f) },
            };

            HashSet<Renderer> uniqueStrips = new HashSet<Renderer>();
            for (int index = 0; index < strips.arraySize; index++)
            {
                Renderer strip = strips.GetArrayElementAtIndex(index).objectReferenceValue as Renderer;
                Assert.That(strip, Is.Not.Null, $"Strip renderer reference {index} is missing.");
                Assert.That(strip.transform.IsChildOf(roomInstance.transform), Is.True, "Every strip must belong to its cube instance.");
                Assert.That(uniqueStrips.Add(strip), Is.True, "Each of the eight strip references must be unique.");
                Assert.That(expectedPositions.ContainsKey(strip.name), Is.True, $"Unexpected strip '{strip.name}'.");
                Assert.That(Vector3.Distance(strip.transform.localPosition, expectedPositions[strip.name]), Is.LessThan(0.001f));
                AssertStripDimensions(strip.transform.localScale, 8f, 0.07f, strip.name);
                Assert.That(strip.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
            }

            Transform housingsRoot = roomInstance.transform.Find("Lighting/Strip Housings");
            Assert.That(housingsRoot, Is.Not.Null, "The visible strips require contrasting housings so they remain readable against glass.");
            Renderer[] housings = housingsRoot.GetComponentsInChildren<Renderer>(true);
            Assert.That(housings, Has.Length.EqualTo(8));
            Dictionary<string, Vector3> expectedHousingPositions = new Dictionary<string, Vector3>
            {
                { "Housing Vertical SW", new Vector3(-3.95f, 4f, -3.95f) },
                { "Housing Vertical SE", new Vector3(3.95f, 4f, -3.95f) },
                { "Housing Vertical NW", new Vector3(-3.95f, 4f, 3.95f) },
                { "Housing Vertical NE", new Vector3(3.95f, 4f, 3.95f) },
                { "Housing Ceiling South", new Vector3(0f, 7.95f, -3.95f) },
                { "Housing Ceiling North", new Vector3(0f, 7.95f, 3.95f) },
                { "Housing Ceiling West", new Vector3(-3.95f, 7.95f, 0f) },
                { "Housing Ceiling East", new Vector3(3.95f, 7.95f, 0f) },
            };
            Material housingMaterial = LoadRequiredAsset<Material>(StripHousingMaterialPath);
            foreach (Renderer housing in housings)
            {
                Assert.That(expectedHousingPositions.ContainsKey(housing.name), Is.True, $"Unexpected strip housing '{housing.name}'.");
                Assert.That(Vector3.Distance(housing.transform.localPosition, expectedHousingPositions[housing.name]), Is.LessThan(0.001f));
                AssertStripDimensions(housing.transform.localScale, 8f, 0.14f, housing.name);
                Assert.That(housing.sharedMaterial, Is.SameAs(housingMaterial));
                Assert.That(housing.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            }

            SerializedProperty supportingLights = serializedLighting.FindProperty("supportingLights");
            Assert.That(supportingLights, Is.Not.Null);
            Assert.That(supportingLights.arraySize, Is.EqualTo(28), "Distributed emitters must follow the full length of all eight strips.");
            SerializedProperty doorwayFrames = serializedLighting.FindProperty("doorwayFrameRenderers");
            Assert.That(doorwayFrames, Is.Not.Null);
            Assert.That(doorwayFrames.arraySize, Is.EqualTo(12), "Every doorway variant requires three dimmer emissive frame pieces.");
            int floorEmitterCount = 0;
            int ceilingEmitterCount = 0;
            for (int index = 0; index < supportingLights.arraySize; index++)
            {
                Light supportingLight = supportingLights.GetArrayElementAtIndex(index).objectReferenceValue as Light;
                Assert.That(supportingLight, Is.Not.Null, $"Supporting light reference {index} is missing.");
                Assert.That(supportingLight.transform.IsChildOf(roomInstance.transform), Is.True);
                Vector3 position = supportingLight.transform.localPosition;
                bool followsVerticalStrip = Mathf.Abs(Mathf.Abs(position.x) - 3.87f) < 0.001f
                    && Mathf.Abs(Mathf.Abs(position.z) - 3.87f) < 0.001f;
                bool followsCeilingStrip = Mathf.Abs(position.y - 7.87f) < 0.001f
                    && (Mathf.Abs(Mathf.Abs(position.x) - 3.87f) < 0.001f
                        || Mathf.Abs(Mathf.Abs(position.z) - 3.87f) < 0.001f);
                Assert.That(
                    followsVerticalStrip || followsCeilingStrip,
                    Is.True,
                    $"Emitter {supportingLight.name} must sit alongside a visible strip, not in the room center.");

                Vector3 forward = supportingLight.transform.localRotation * Vector3.forward;
                if (followsVerticalStrip)
                {
                    Vector3 inward = new Vector3(-Mathf.Sign(position.x), 0f, -Mathf.Sign(position.z)).normalized;
                    Vector3 horizontalForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
                    Assert.That(Vector3.Dot(horizontalForward, inward), Is.GreaterThan(0.999f));
                    Assert.That(forward.y, Is.LessThan(-0.01f), "Vertical-strip emitters should feather light toward the floor without aiming a cone into it.");
                    if (position.y < 0.25f)
                    {
                        floorEmitterCount++;
                    }
                }
                else
                {
                    ceilingEmitterCount++;
                    Assert.That(forward.y, Is.LessThan(-0.95f), "Ceiling-strip emitters must project mostly downward.");
                }

                Assert.That(supportingLight.lightmapBakeType, Is.EqualTo(LightmapBakeType.Realtime));
                Assert.That(supportingLight.shadows, Is.EqualTo(LightShadows.None));
                Assert.That(supportingLight.cullingMask, Is.EqualTo(~0));
                Assert.That(supportingLight.renderingLayerMask, Is.EqualTo(~0));
                Assert.That(supportingLight.intensity, Is.EqualTo(0.85f).Within(0.001f));
                Assert.That(supportingLight.range, Is.EqualTo(12f).Within(0.001f));
                Assert.That(supportingLight.type, Is.EqualTo(LightType.Spot), "Point lights must not be present in a cube room.");
                Assert.That(supportingLight.spotAngle, Is.EqualTo(170f).Within(0.001f));
                Assert.That(supportingLight.innerSpotAngle, Is.EqualTo(160f).Within(0.001f));
                Assert.That(
                    supportingLight.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier,
                    Is.EqualTo(UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow));
            }

            Assert.That(floorEmitterCount, Is.EqualTo(4), "Every vertical strip must illuminate its floor intersection.");
            Assert.That(ceilingEmitterCount, Is.EqualTo(12), "Three distributed emitters must follow each ceiling strip.");

            Assert.That(lighting.StripWidth, Is.EqualTo(0.07f).Within(0.001f));
            Assert.That(lighting.EmissionIntensity, Is.EqualTo(6f).Within(0.001f));
            Assert.That(lighting.DoorwayFrameEmissionIntensity, Is.EqualTo(1.35f).Within(0.001f));
            Assert.That(lighting.SupportingLightIntensity, Is.EqualTo(0.85f).Within(0.001f));
            Assert.That(lighting.SupportingLightRange, Is.EqualTo(12f).Within(0.001f));
            Assert.That(lighting.SupportingLightSpotAngle, Is.EqualTo(170f).Within(0.001f));
            Assert.That(lighting.SupportingLightInnerSpotAngle, Is.EqualTo(160f).Within(0.001f));
            Assert.That(lighting.SupportingLightShadows, Is.EqualTo(LightShadows.None));
            Assert.That(lighting.SupportingLightShadowResolution, Is.EqualTo(RoomLightShadowResolutionTier.Low));
        }

        private static void AssertConfiguredGlassRenderers(GameObject roomInstance)
        {
            Transform glassRoot = roomInstance.transform.Find("Geometry/Glass");
            Assert.That(glassRoot, Is.Not.Null, "The room must keep its glass panes under Geometry/Glass.");

            Material glassMaterial = LoadRequiredAsset<Material>(GlassMaterialPath);
            Material frameMaterial = LoadRequiredAsset<Material>(StripMaterialPath);
            Material baseTrimMaterial = LoadRequiredAsset<Material>(BaseRailMaterialPath);
            Renderer[] allRenderers = glassRoot.GetComponentsInChildren<Renderer>(true);
            Renderer[] panes = allRenderers.Where(renderer => renderer.sharedMaterial == glassMaterial).ToArray();
            Renderer[] frames = allRenderers.Where(renderer => renderer.sharedMaterial == frameMaterial).ToArray();
            Renderer[] baseTrims = allRenderers.Where(renderer => renderer.sharedMaterial == baseTrimMaterial).ToArray();
            Assert.That(
                panes.Length,
                Is.EqualTo(17),
                "The room requires four closed panes, twelve doorway segments, and one glass ceiling.");
            Assert.That(frames, Has.Length.EqualTo(12), "Each of the four walls requires a three-piece doorway frame.");
            Assert.That(baseTrims, Has.Length.EqualTo(12), "Each wall requires one closed rail and two doorway-side rails at the glass/floor seam.");

            CubeRoom room = roomInstance.GetComponent<CubeRoom>();
            Renderer[] wallPanes = Enum
                .GetValues(typeof(CubeRoomWall))
                .Cast<CubeRoomWall>()
                .Select(room.GetWallGlassRenderer)
                .ToArray();
            Assert.That(
                wallPanes.All(renderer => renderer != null),
                Is.True,
                "CubeRoom must serialize each addressable wall pane.");
            Assert.That(wallPanes.Distinct().Count(), Is.EqualTo(4));
            Assert.That(wallPanes.All(renderer => renderer.enabled), Is.True);

            foreach (CubeRoomWall wall in Enum.GetValues(typeof(CubeRoomWall)))
            {
                CubeRoomWallBoundary boundary = room.GetWallBoundary(wall);
                Assert.That(boundary, Is.Not.Null, $"The {wall} wall requires configurable doorway geometry.");
                Assert.That(boundary.Wall, Is.EqualTo(wall));
                Assert.That(boundary.ClosedGlassRenderer, Is.SameAs(room.GetWallGlassRenderer(wall)));
                Assert.That(boundary.DoorwayGlassRenderers.Count, Is.EqualTo(3));
                Assert.That(boundary.DoorwayGlassRenderers.All(renderer => renderer != null && !renderer.enabled), Is.True);
                Assert.That(boundary.DoorwayFrameRenderers.Count, Is.EqualTo(3));
                Assert.That(boundary.DoorwayFrameRenderers.All(renderer => renderer != null && !renderer.enabled), Is.True);
                Assert.That(boundary.ClosedBaseTrimRenderer, Is.Not.Null);
                Assert.That(boundary.ClosedBaseTrimRenderer.enabled, Is.True);
                Assert.That(boundary.ClosedBaseTrimRenderer.sharedMaterial, Is.SameAs(baseTrimMaterial));
                Assert.That(boundary.DoorwayBaseTrimRenderers.Count, Is.EqualTo(2));
                Assert.That(boundary.DoorwayBaseTrimRenderers.All(renderer => renderer != null && !renderer.enabled), Is.True);
                Assert.That(room.HasDoorway(wall), Is.False, $"The standalone prefab's {wall} wall must start sealed.");
            }

            foreach (Renderer pane in panes)
            {
                Assert.That(pane.sharedMaterial, Is.SameAs(glassMaterial));
                Assert.That(pane.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off), $"{pane.name} must let direct light pass without casting a shadow.");
                Assert.That(pane.receiveShadows, Is.False, $"{pane.name} must not darken in response to room shadows.");
            }
        }

        private static void AssertConfiguredWallBoundaryColliders(GameObject roomInstance, CubeRoom room)
        {
            HashSet<Collider> wallColliders = new HashSet<Collider>();
            CubeRoomWall[] walls =
            {
                CubeRoomWall.West,
                CubeRoomWall.East,
                CubeRoomWall.South,
                CubeRoomWall.North,
            };

            foreach (CubeRoomWall wall in walls)
            {
                Collider wallCollider = room.GetWallBoundaryCollider(wall);
                CubeRoomWallBoundary boundary = room.GetWallBoundary(wall);
                Assert.That(boundary, Is.Not.Null, $"The {wall} wall requires a configurable boundary component.");
                Assert.That(boundary.Wall, Is.EqualTo(wall));
                Assert.That(boundary.ClosedCollider, Is.SameAs(wallCollider));
                Assert.That(boundary.DoorwayColliders.Count, Is.EqualTo(3));
                Assert.That(boundary.DoorwayColliders.All(collider => collider != null && !collider.enabled), Is.True);
                Assert.That(boundary.OwnsBoundary, Is.True);
                Assert.That(room.HasDoorway(wall), Is.False, $"The standalone prefab's {wall} wall must start sealed.");
                Assert.That(wallCollider, Is.Not.Null, $"The {wall} wall requires an addressable boundary collider.");
                Assert.That(wallCollider.transform.IsChildOf(roomInstance.transform), Is.True);
                Assert.That(wallCollider.isTrigger, Is.False, $"The {wall} boundary must remain a solid collider.");
                Assert.That(wallCollider.enabled, Is.True, $"The standalone prefab's {wall} boundary must start enabled.");
                Assert.That(wallColliders.Add(wallCollider), Is.True, $"The {wall} wall must own a distinct boundary collider.");
            }

            Assert.That(wallColliders, Has.Count.EqualTo(4));
        }

        private static void AssertPrefabSource(GameObject instanceObject, string expectedPath)
        {
            GameObject nearestRoot = PrefabUtility.GetNearestPrefabInstanceRoot(instanceObject);
            Assert.That(nearestRoot, Is.Not.Null, $"'{instanceObject.name}' must remain a prefab instance in the Foundation scene.");
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(nearestRoot);
            Assert.That(source, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo(expectedPath));
        }

        private static void AssertInteriorBoundaryDistances(GameObject roomInstance)
        {
            Physics.SyncTransforms();
            Vector3 center = roomInstance.transform.TransformPoint(new Vector3(0f, 4f, 0f));
            Vector3[] localDirections =
            {
                Vector3.left,
                Vector3.right,
                Vector3.down,
                Vector3.up,
                Vector3.back,
                Vector3.forward,
            };

            foreach (Vector3 localDirection in localDirections)
            {
                Vector3 worldDirection = roomInstance.transform.TransformDirection(localDirection);
                RaycastHit? roomHit = Physics
                    .RaycastAll(center, worldDirection, 10f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(hit => hit.collider.transform.IsChildOf(roomInstance.transform))
                    .OrderBy(hit => hit.distance)
                    .Cast<RaycastHit?>()
                    .FirstOrDefault();

                Assert.That(roomHit.HasValue, Is.True, $"No solid boundary was found toward local {localDirection}.");
                Assert.That(
                    roomHit.Value.distance,
                    Is.EqualTo(4f).Within(0.02f),
                    $"The clear interior must measure 8 m along local {localDirection}; wall thickness must extend outward.");
            }
        }

        private static void AssertHighDefinitionTexture(Texture2D texture, string path, bool isNormalMap)
        {
            Assert.That(texture.width, Is.GreaterThanOrEqualTo(1024), $"'{path}' must retain HD source detail.");
            Assert.That(texture.height, Is.GreaterThanOrEqualTo(1024), $"'{path}' must retain HD source detail.");

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.EqualTo(!isNormalMap));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.streamingMipmaps, Is.True);
            Assert.That(importer.anisoLevel, Is.EqualTo(8));
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.maxTextureSize, Is.EqualTo(4096));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ));
        }

        private static void AssertStripDimensions(Vector3 scale, float expectedLength, float expectedWidth, string objectName)
        {
            float[] dimensions = { Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z) };
            Array.Sort(dimensions);
            Assert.That(dimensions[0], Is.EqualTo(expectedWidth).Within(0.001f), $"{objectName} width mismatch.");
            Assert.That(dimensions[1], Is.EqualTo(expectedWidth).Within(0.001f), $"{objectName} depth mismatch.");
            Assert.That(dimensions[2], Is.EqualTo(expectedLength).Within(0.001f), $"{objectName} length mismatch.");
        }
    }
}
