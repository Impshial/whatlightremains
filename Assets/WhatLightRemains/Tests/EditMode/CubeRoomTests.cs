using NUnit.Framework;
using UnityEngine;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class CubeRoomTests
    {
        private const float FloatTolerance = 0.0001f;

        [Test]
        public void Defaults_UseEarthGravityAlongLocalDown()
        {
            GameObject roomObject = new GameObject("Room");

            try
            {
                CubeRoom room = roomObject.AddComponent<CubeRoom>();

                Assert.That(room.GravityStrength, Is.EqualTo(9.81f).Within(FloatTolerance));
                AssertVector(room.InteriorSize, new Vector3(8f, 8f, 8f));
                Assert.That(CubeRoom.DoorwayWidth, Is.EqualTo(2f).Within(FloatTolerance));
                Assert.That(CubeRoom.DoorwayHeight, Is.EqualTo(2.4f).Within(FloatTolerance));
                AssertVector(room.RoomUp, Vector3.up);
                AssertVector(room.GravityAcceleration, Vector3.down * 9.81f);
            }
            finally
            {
                Object.DestroyImmediate(roomObject);
            }
        }

        [Test]
        public void GravityAcceleration_FollowsRoomRotationAndConfiguredMagnitude()
        {
            GameObject roomObject = new GameObject("Rotated Room");
            Vector3 globalGravityBefore = Physics.gravity;

            try
            {
                CubeRoom room = roomObject.AddComponent<CubeRoom>();
                room.GravityStrength = 3.25f;
                roomObject.transform.rotation = Quaternion.Euler(0f, 0f, 90f);

                Assert.That(room.GravityAcceleration.magnitude, Is.EqualTo(3.25f).Within(FloatTolerance));
                Assert.That(
                    Vector3.Dot(room.GravityAcceleration.normalized, -roomObject.transform.up),
                    Is.GreaterThan(0.9999f));
                AssertVector(room.GravityAcceleration, Vector3.right * 3.25f);
                AssertVector(Physics.gravity, globalGravityBefore);
            }
            finally
            {
                Object.DestroyImmediate(roomObject);
            }
        }

        [Test]
        public void TwoRooms_KeepGravityAndLightingStateIndependent()
        {
            GameObject firstObject = new GameObject("First Room");
            GameObject secondObject = new GameObject("Second Room");
            Material sharedMaterial = null;

            try
            {
                CubeRoom firstRoom = firstObject.AddComponent<CubeRoom>();
                CubeRoom secondRoom = secondObject.AddComponent<CubeRoom>();
                CubeRoomLighting firstLighting = firstObject.AddComponent<CubeRoomLighting>();
                CubeRoomLighting secondLighting = secondObject.AddComponent<CubeRoomLighting>();
                Light firstLight = CreateChildLight(firstObject.transform, "First Supporting Light");
                Light secondLight = CreateChildLight(secondObject.transform, "Second Supporting Light");
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
                Assert.That(shader, Is.Not.Null, "An unlit shader is required to verify per-instance strip properties.");
                sharedMaterial = new Material(shader);
                Renderer firstStrip = CreateChildRenderer(firstObject.transform, "First Strip", sharedMaterial);
                Renderer secondStrip = CreateChildRenderer(secondObject.transform, "Second Strip", sharedMaterial);
                Renderer firstDoorwayFrame = CreateChildRenderer(firstObject.transform, "First Doorway Frame", sharedMaterial);
                Renderer secondDoorwayFrame = CreateChildRenderer(secondObject.transform, "Second Doorway Frame", sharedMaterial);

                firstLighting.Configure(new[] { firstStrip }, new[] { firstLight }, new[] { firstDoorwayFrame });
                secondLighting.Configure(new[] { secondStrip }, new[] { secondLight }, new[] { secondDoorwayFrame });
                firstRoom.Configure(9.81f, firstLighting, null);
                secondRoom.Configure(2.5f, secondLighting, null);

                firstLighting.SupportingLightIntensity = 12f;
                firstLighting.SupportingLightRange = 4f;
                firstLighting.EmissionIntensity = 3f;
                firstLighting.DoorwayFrameEmissionIntensity = 1.25f;
                firstLighting.StripColor = Color.red;
                firstLighting.ApplySettings();

                secondLighting.SupportingLightIntensity = 31f;
                secondLighting.SupportingLightRange = 7f;
                secondLighting.EmissionIntensity = 5f;
                secondLighting.DoorwayFrameEmissionIntensity = 0.75f;
                secondLighting.StripColor = Color.cyan;
                secondLighting.ApplySettings();
                secondRoom.SetLightingEnabled(false);

                Assert.That(firstRoom.GravityStrength, Is.EqualTo(9.81f).Within(FloatTolerance));
                Assert.That(secondRoom.GravityStrength, Is.EqualTo(2.5f).Within(FloatTolerance));
                Assert.That(firstLighting.SupportingLightIntensity, Is.EqualTo(12f).Within(FloatTolerance));
                Assert.That(secondLighting.SupportingLightIntensity, Is.EqualTo(31f).Within(FloatTolerance));
                Assert.That(firstLight.intensity, Is.EqualTo(12f).Within(FloatTolerance));
                Assert.That(firstLight.range, Is.EqualTo(4f).Within(FloatTolerance));
                Assert.That(firstLight.color, Is.EqualTo(Color.red));
                Assert.That(firstLighting.IsLightingEnabled, Is.True);
                Assert.That(firstLight.enabled, Is.True);
                Assert.That(firstStrip.enabled, Is.True);
                Assert.That(secondLighting.IsLightingEnabled, Is.False);
                Assert.That(secondLight.enabled, Is.False);
                Assert.That(secondStrip.enabled, Is.False);
                Assert.That(firstStrip.sharedMaterial, Is.SameAs(sharedMaterial));
                Assert.That(secondStrip.sharedMaterial, Is.SameAs(sharedMaterial));

                MaterialPropertyBlock firstProperties = new MaterialPropertyBlock();
                MaterialPropertyBlock secondProperties = new MaterialPropertyBlock();
                MaterialPropertyBlock firstFrameProperties = new MaterialPropertyBlock();
                MaterialPropertyBlock secondFrameProperties = new MaterialPropertyBlock();
                firstStrip.GetPropertyBlock(firstProperties);
                secondStrip.GetPropertyBlock(secondProperties);
                firstDoorwayFrame.GetPropertyBlock(firstFrameProperties);
                secondDoorwayFrame.GetPropertyBlock(secondFrameProperties);
                Color firstVisibleColor = new Color(3f, 0f, 0f, 1f);
                Color secondVisibleColor = new Color(0f, 5f, 5f, 1f);
                Color firstFrameVisibleColor = new Color(1.25f, 0f, 0f, 1f);
                Color secondFrameDisabledColor = new Color(0f, 0f, 0f, 1f);
                AssertColor(firstProperties.GetColor(Shader.PropertyToID("_BaseColor")), firstVisibleColor);
                AssertColor(firstProperties.GetColor(Shader.PropertyToID("_Color")), firstVisibleColor);
                AssertColor(firstProperties.GetColor(Shader.PropertyToID("_EmissionColor")), firstVisibleColor);
                AssertColor(secondProperties.GetColor(Shader.PropertyToID("_BaseColor")), secondVisibleColor);
                AssertColor(secondProperties.GetColor(Shader.PropertyToID("_Color")), secondVisibleColor);
                AssertColor(secondProperties.GetColor(Shader.PropertyToID("_EmissionColor")), secondVisibleColor);
                AssertColor(firstFrameProperties.GetColor(Shader.PropertyToID("_BaseColor")), firstFrameVisibleColor);
                AssertColor(secondFrameProperties.GetColor(Shader.PropertyToID("_BaseColor")), secondFrameDisabledColor);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
                if (sharedMaterial != null)
                {
                    Object.DestroyImmediate(sharedMaterial);
                }
            }
        }

        [Test]
        public void RoomTracker_InitializesAndFallsBackAcrossOverlappingRooms()
        {
            GameObject trackerObject = new GameObject("Player");
            GameObject firstObject = new GameObject("First Room");
            GameObject secondObject = new GameObject("Second Room");

            try
            {
                PlayerRoomTracker tracker = trackerObject.AddComponent<PlayerRoomTracker>();
                CubeRoom firstRoom = firstObject.AddComponent<CubeRoom>();
                CubeRoom secondRoom = secondObject.AddComponent<CubeRoom>();

                tracker.Initialize(firstRoom);
                Assert.That(tracker.CurrentRoom, Is.SameAs(firstRoom));
                AssertVector(tracker.GravityAcceleration, firstRoom.GravityAcceleration);

                tracker.EnterRoom(secondRoom);
                Assert.That(tracker.CurrentRoom, Is.SameAs(secondRoom));

                tracker.ExitRoom(secondRoom);
                Assert.That(tracker.CurrentRoom, Is.SameAs(firstRoom));

                tracker.ExitRoom(firstRoom);
                Assert.That(tracker.CurrentRoom, Is.Null);
                AssertVector(tracker.GravityAcceleration, Vector3.zero);
            }
            finally
            {
                Object.DestroyImmediate(trackerObject);
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        private static Light CreateChildLight(Transform parent, string name)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            return lightObject.AddComponent<Light>();
        }

        private static Renderer CreateChildRenderer(Transform parent, string name, Material material)
        {
            GameObject stripObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripObject.name = name;
            stripObject.transform.SetParent(parent, false);
            Renderer renderer = stripObject.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(FloatTolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(FloatTolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(FloatTolerance));
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(FloatTolerance));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(FloatTolerance));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(FloatTolerance));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(FloatTolerance));
        }
    }
}
