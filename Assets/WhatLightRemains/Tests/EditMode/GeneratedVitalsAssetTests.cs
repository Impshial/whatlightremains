using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class GeneratedVitalsAssetTests
    {
        private const string GeneratedRoot = "Assets/WhatLightRemains/Generated/";
        private const string BuilderHint = "Regenerate Foundation assets before running generated-vitals tests.";

        [Test]
        public void Prefabs_KeepPlayerVitalsAndWorldReserveOutOfRoomsAndPresentation()
        {
            GameObject player = LoadPrefab("Player");
            GameObject room = LoadPrefab("CubeRoom");
            GameObject hotbar = LoadPrefab("Hotbar");

            Assert.That(player.GetComponentsInChildren<PlayerVitals>(true), Has.Length.EqualTo(1));
            Assert.That(player.GetComponent<PlayerVitals>(), Is.Not.Null,
                "Health and Hunger must be owned by the actual player root.");
            Assert.That(player.GetComponentsInChildren<WorldOxygenReserve>(true), Is.Empty,
                "Oxygen is the shared world reserve, never a personal player tank.");
            Assert.That(room.GetComponentsInChildren<PlayerVitals>(true), Is.Empty);
            Assert.That(room.GetComponentsInChildren<WorldOxygenReserve>(true), Is.Empty);
            Assert.That(room.GetComponentsInChildren<VitalsHudView>(true), Is.Empty);
            Assert.That(hotbar.GetComponentsInChildren<PlayerVitals>(true), Is.Empty);
            Assert.That(hotbar.GetComponentsInChildren<WorldOxygenReserve>(true), Is.Empty);
        }

        [Test]
        public void HotbarPrefab_HasExactlyThreeNoninteractiveRadialGaugesOnItsExistingCanvas()
        {
            GameObject hotbar = LoadPrefab("Hotbar");
            VitalsHudView[] views = hotbar.GetComponentsInChildren<VitalsHudView>(true);
            Assert.That(views, Has.Length.EqualTo(1), BuilderHint);
            Assert.That(hotbar.GetComponentsInChildren<VitalGaugeView>(true), Has.Length.EqualTo(3));
            Assert.That(hotbar.GetComponentsInChildren<Canvas>(true), Has.Length.EqualTo(1),
                "Vitals reuse the existing gameplay Canvas.");

            VitalsHudView view = views[0];
            VitalGaugeView[] gauges = { view.HealthGauge, view.HungerGauge, view.OxygenGauge };
            string[] labels = { "Health", "Hunger", "Oxygen" };
            VitalIconGraphic.Symbol[] symbols = { VitalIconGraphic.Symbol.Heart, VitalIconGraphic.Symbol.Food, VitalIconGraphic.Symbol.Air };
            Assert.That(gauges.Distinct().Count(), Is.EqualTo(3));
            Canvas canvas = hotbar.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));

            for (int index = 0; index < gauges.Length; index++)
            {
                VitalGaugeView gauge = gauges[index];
                Assert.That(gauge, Is.Not.Null);
                Assert.That(gauge.GetComponentInParent<Canvas>(true), Is.SameAs(canvas));
                Assert.That(gauge.name, Is.EqualTo(labels[index] + " Gauge"));
                Assert.That(gauge.GetComponentsInChildren<Text>(true), Is.Empty,
                    "The requested ring-only display has no numeric values or labels beside the icons.");
                Assert.That(gauge.FillImage, Is.Not.Null);
                Assert.That(gauge.FillImage.sprite, Is.Not.Null);
                Assert.That(gauge.FillImage.type, Is.EqualTo(Image.Type.Filled));
                Assert.That(gauge.FillImage.fillMethod, Is.EqualTo(Image.FillMethod.Radial360),
                    "The user's reference specifies circular depleted-color indicators.");
                Assert.That(gauge.FillImage.fillOrigin, Is.EqualTo((int)Image.Origin360.Top));
                Assert.That(gauge.FillImage.fillClockwise, Is.True);
                VitalIconGraphic[] icons = gauge.GetComponentsInChildren<VitalIconGraphic>(true);
                Assert.That(icons, Has.Length.EqualTo(1));
                Assert.That(icons[0].Icon, Is.EqualTo(symbols[index]));
                Assert.That(Vector3.Distance(icons[0].rectTransform.position, gauge.FillImage.rectTransform.position),
                    Is.LessThan(0.001f), "Each white symbol stays centered inside its colored ring.");
                Assert.That(gauge.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
                Assert.That(gauge.GetComponentsInChildren<Selectable>(true), Is.Empty);
                Assert.That(gauge.GetComponentsInChildren<Light>(true), Is.Empty);
            }
        }

        [Test]
        public void FoundationScene_WiresOneAuthoritativeReserveAndOneHudToOnePlayer()
        {
            WithGeneratedScene("Foundation", scene =>
            {
                PlayerVitals[] players = FindInScene<PlayerVitals>(scene);
                WorldOxygenReserve[] reserves = FindInScene<WorldOxygenReserve>(scene);
                VitalsHudView[] views = FindInScene<VitalsHudView>(scene);
                Assert.That(players, Has.Length.EqualTo(1), BuilderHint);
                Assert.That(reserves, Has.Length.EqualTo(1), BuilderHint);
                Assert.That(views, Has.Length.EqualTo(1), BuilderHint);
                Assert.That(FindInScene<VitalGaugeView>(scene), Has.Length.EqualTo(3));
                Assert.That(views[0].PlayerVitals, Is.SameAs(players[0]));
                Assert.That(views[0].OxygenReserve, Is.SameAs(reserves[0]));
                Assert.That(players[0].GetComponent<FirstPersonMotor>(), Is.Not.Null);
                Assert.That(reserves[0].GetComponentInParent<PlayerVitals>(true), Is.Null);
                Assert.That(reserves[0].GetComponentInParent<CubeRoom>(true), Is.Null);
                Assert.That(reserves[0].GetComponentInParent<HotbarView>(true), Is.Null);
                Assert.That(reserves[0].GetComponentInParent<Canvas>(true), Is.Null);
            });
        }

        [Test]
        public void MainMenuScene_HasNoSessionVitalsOrGameplayGauges()
        {
            WithGeneratedScene("MainMenu", scene =>
            {
                Assert.That(FindInScene<PlayerVitals>(scene), Is.Empty);
                Assert.That(FindInScene<WorldOxygenReserve>(scene), Is.Empty);
                Assert.That(FindInScene<VitalsHudView>(scene), Is.Empty);
                Assert.That(FindInScene<VitalGaugeView>(scene), Is.Empty);
            });
        }

        private static GameObject LoadPrefab(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedRoot + "Prefabs/" + name + ".prefab");
            Assert.That(prefab, Is.Not.Null, BuilderHint);
            return prefab;
        }

        private static T[] FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        private static void WithGeneratedScene(string name, System.Action<Scene> assertions)
        {
            string path = GeneratedRoot + "Scenes/" + name + ".unity";
            Assert.That(System.IO.File.Exists(path), Is.True, BuilderHint);
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                assertions(scene);
            }
            finally
            {
                if (openedForTest) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
