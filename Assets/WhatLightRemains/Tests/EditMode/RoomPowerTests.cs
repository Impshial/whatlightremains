using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class RoomPowerTests
    {
        private const string CubePrefabPath = "Assets/WhatLightRemains/Generated/Prefabs/CubeRoom.prefab";
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Power_IsIndependentAndDebugLightingCannotOverrideAnOffRoom()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CubePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            root = new GameObject("Room Power Tests");
            CubeRoom first = Object.Instantiate(prefab, root.transform).GetComponent<CubeRoom>();
            CubeRoom second = Object.Instantiate(prefab, root.transform).GetComponent<CubeRoom>();

            Assert.That(first.PowerOn, Is.True);
            Assert.That(second.PowerOn, Is.True);
            first.SetPower(false);
            Assert.That(first.PowerOn, Is.False);
            Assert.That(first.GetComponentsInChildren<Light>(true).All(light => !light.enabled), Is.True);
            Assert.That(second.GetComponentsInChildren<Light>(true).Any(light => light.enabled), Is.True);

            first.SetLightingEnabled(true);
            Assert.That(first.GetComponentsInChildren<Light>(true).All(light => !light.enabled), Is.True,
                "The debug lighting override must not energize a room whose Power state is OFF.");
            first.TogglePower();
            Assert.That(first.PowerOn, Is.True);
            Assert.That(first.GetComponentsInChildren<Light>(true).Any(light => light.enabled), Is.True);
        }
    }
}
