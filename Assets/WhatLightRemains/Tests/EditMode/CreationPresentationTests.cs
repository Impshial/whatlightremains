using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class CreationPresentationTests
    {
        [TestCase(1f, 1)]
        [TestCase(-1f, -1)]
        [TestCase(120f, 1)]
        [TestCase(-120f, -1)]
        [TestCase(0f, 0)]
        [TestCase(0.005f, 0)]
        public void RotationScroll_HandlesNormalizedAndPlatformSpecificWheelDeltas(
            float scrollDelta,
            int expectedStep)
        {
            Assert.That(FirstPersonInput.ConvertRotationScrollDelta(scrollDelta), Is.EqualTo(expectedStep));
        }

        [Test]
        public void RotationPrompt_UsesFinalCtrlAndAltAxisMapping()
        {
            GameObject root = new GameObject("Prompt Test");
            try
            {
                Text instruction = new GameObject("Instruction").AddComponent<Text>();
                instruction.transform.SetParent(root.transform);
                Text rotation = new GameObject("Rotation").AddComponent<Text>();
                rotation.transform.SetParent(root.transform);
                RoomCreationPromptView prompt = root.AddComponent<RoomCreationPromptView>();
                prompt.Configure(instruction, rotation);

                prompt.SetRotationState(false, false, false, false);
                Assert.That(rotation.gameObject.activeSelf, Is.False);
                prompt.SetCreateMode(true);
                prompt.SetRotationState(true, false, false, false);
                Assert.That(rotation.gameObject.activeSelf, Is.False,
                    "Rotation help must remain hidden until a valid ghost is visible.");
                prompt.SetRotationState(true, true, false, false);
                Assert.That(rotation.text, Is.EqualTo("Hold Ctrl to Rotate"));
                prompt.SetRotationState(true, true, true, false);
                Assert.That(rotation.text, Is.EqualTo("<b>Ctrl: Rotate around y-axis</b>\nCtrl+Alt: Rotate around z-axis"));
                prompt.SetRotationState(true, true, true, true);
                Assert.That(rotation.text, Is.EqualTo("Ctrl: Rotate around y-axis\n<b>Ctrl+Alt: Rotate around z-axis</b>"));
                Assert.That(rotation.supportRichText, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PromptView_TransitionsBetweenDefaultCreateAndDeleteModes()
        {
            GameObject root = new GameObject("Prompt Mode Test");
            try
            {
                Text create = NewLabel("Create", root.transform);
                Text delete = NewLabel("Delete", root.transform);
                Text context = NewLabel("Context", root.transform);
                Text traversal = NewLabel("Traversal", root.transform);
                RoomCreationPromptView prompt = root.AddComponent<RoomCreationPromptView>();
                prompt.Configure(create, delete, context, traversal);

                Assert.That(create.text, Is.EqualTo(RoomCreationPromptView.NormalText));
                Assert.That(delete.text, Is.EqualTo(RoomCreationPromptView.NormalDeleteText));
                Assert.That(create.gameObject.activeSelf, Is.True);
                Assert.That(delete.gameObject.activeSelf, Is.True);

                prompt.SetCreateMode(true);
                Assert.That(create.text, Is.EqualTo(RoomCreationPromptView.CreateText));
                Assert.That(delete.gameObject.activeSelf, Is.False);
                prompt.SetRotationState(true, false, false, false);
                Assert.That(context.gameObject.activeSelf, Is.False);
                prompt.SetRotationState(true, true, false, false);
                Assert.That(context.gameObject.activeSelf, Is.True);

                prompt.SetDeleteMode(true);
                Assert.That(create.gameObject.activeSelf, Is.False);
                Assert.That(delete.text, Is.EqualTo(RoomCreationPromptView.DeleteModeText));
                prompt.SetDeleteTarget(true);
                Assert.That(context.text, Is.EqualTo(RoomCreationPromptView.DeleteFocusText));
                Assert.That(context.gameObject.activeSelf, Is.True);

                prompt.SetDeleteMode(false);
                Assert.That(create.gameObject.activeSelf, Is.True);
                Assert.That(delete.text, Is.EqualTo(RoomCreationPromptView.NormalDeleteText));
                Assert.That(context.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Ghost_ClonesVisibleRoomGeometryWithoutPhysicsOrLights()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            Material previewMaterial = new Material(shader);
            GameObject source = new GameObject("Room Prefab");
            RoomGhostPreview preview = null;
            try
            {
                CubeRoom room = source.AddComponent<CubeRoom>();
                GameObject floor = CreatePrimitiveChild(source.transform, "Floor");
                CreatePrimitiveChild(source.transform, "Glass Wall");
                CreatePrimitiveChild(source.transform, "Door Frame");

                previewMaterial.color = new Color(0f, 1f, 0.5f, 0.22f);
                preview = RoomGhostPreview.Create(previewMaterial, room);
                Renderer floorCopy = preview.Root.GetComponentsInChildren<Renderer>(true)
                    .Single(renderer => renderer.name == floor.name);
                Renderer wallCopy = preview.Root.GetComponentsInChildren<Renderer>(true)
                    .Single(renderer => renderer.name == "Glass Wall");
                Renderer frameCopy = preview.Root.GetComponentsInChildren<Renderer>(true)
                    .Single(renderer => renderer.name == "Door Frame");
                Renderer arrowCopy = preview.Root.GetComponentsInChildren<Renderer>(true)
                    .First(renderer => renderer.name == "Gravity Arrow Shaft");

                Assert.That(preview.Root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(preview.Root.GetComponentsInChildren<Light>(true), Is.Empty);
                Assert.That(preview.Root.GetComponentsInChildren<CubeRoom>(true), Is.Empty);
                Assert.That(preview.Root.GetComponentsInChildren<LineRenderer>(true), Has.Length.EqualTo(12));
                Assert.That(FindChild(preview.Root.transform, "Gravity Direction Arrow"), Is.Not.Null);
                Assert.That(ReadAlpha(floorCopy.sharedMaterial), Is.EqualTo(0.16f).Within(0.001f));
                Assert.That(ReadAlpha(wallCopy.sharedMaterial), Is.EqualTo(0.02f).Within(0.001f));
                Assert.That(ReadAlpha(wallCopy.sharedMaterial), Is.LessThan(ReadAlpha(floorCopy.sharedMaterial)));
                Assert.That(ReadAlpha(frameCopy.sharedMaterial), Is.EqualTo(0.10f).Within(0.001f));
                Assert.That(ReadAlpha(arrowCopy.sharedMaterial), Is.EqualTo(0.72f).Within(0.001f));
            }
            finally
            {
                preview?.Dispose();
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(previewMaterial);
            }
        }

        [Test]
        public void DoorwayFramePower_IsHalfOfStripPowerForGlowAndLight()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            Material sharedMaterial = new Material(shader);
            GameObject room = new GameObject("Lighting Test");
            try
            {
                CubeRoomLighting lighting = room.AddComponent<CubeRoomLighting>();
                Renderer strip = CreatePrimitiveChild(room.transform, "Strip").GetComponent<Renderer>();
                Renderer frame = CreatePrimitiveChild(room.transform, "Doorway Frame").GetComponent<Renderer>();
                strip.sharedMaterial = sharedMaterial;
                frame.sharedMaterial = sharedMaterial;
                Light stripLight = new GameObject("Strip Light").AddComponent<Light>();
                stripLight.transform.SetParent(room.transform);
                CubeRoomWallBoundary boundary = new GameObject("Doorway Boundary").AddComponent<CubeRoomWallBoundary>();
                boundary.transform.SetParent(room.transform);
                boundary.SetConnectionState(true, true);
                Light frameLight = new GameObject("Frame Light").AddComponent<Light>();
                frameLight.transform.SetParent(boundary.transform);
                CubeRoomCeilingBoundary ceiling = new GameObject("Ceiling Boundary").AddComponent<CubeRoomCeilingBoundary>();
                ceiling.transform.SetParent(room.transform);
                ceiling.SetConnectionState(true, RoomPassageKind.CeilingToSideDoorway, RoomCeilingEdge.North, true);
                Light ceilingFrameLight = new GameObject("Ceiling Frame Light").AddComponent<Light>();
                ceilingFrameLight.transform.SetParent(ceiling.transform);

                lighting.Configure(new[] { strip }, new[] { stripLight }, new[] { frame }, new[] { frameLight, ceilingFrameLight });
                lighting.EmissionIntensity = 6f;
                lighting.SupportingLightIntensity = 0.85f;
                lighting.DoorwayFramePowerRatio = 0.5f;

                Assert.That(lighting.DoorwayFrameEmissionIntensity, Is.EqualTo(3f).Within(0.001f));
                Assert.That(frameLight.intensity, Is.EqualTo(0.425f).Within(0.001f));
                Assert.That(ceilingFrameLight.intensity, Is.EqualTo(0.425f).Within(0.001f));
                Assert.That(ceilingFrameLight.enabled, Is.True);
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                frame.GetPropertyBlock(properties);
                Color emission = properties.GetColor(Shader.PropertyToID("_EmissionColor"));
                Assert.That(emission.r, Is.EqualTo(3f).Within(0.001f));
                Assert.That(emission.g, Is.EqualTo(3f).Within(0.001f));
                Assert.That(emission.b, Is.EqualTo(3f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(room);
                Object.DestroyImmediate(sharedMaterial);
            }
        }

        private static GameObject CreatePrimitiveChild(Transform parent, string name)
        {
            GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.name = name;
            child.transform.SetParent(parent);
            return child;
        }

        private static Text NewLabel(string name, Transform parent)
        {
            Text label = new GameObject(name).AddComponent<Text>();
            label.transform.SetParent(parent);
            return label;
        }

        private static Transform FindChild(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == name);
        }

        private static float ReadAlpha(Material material)
        {
            return material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor").a : material.color.a;
        }
    }
}
