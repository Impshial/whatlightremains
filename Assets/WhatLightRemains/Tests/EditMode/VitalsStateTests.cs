using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WhatLightRemains.Runtime;
using Object = UnityEngine.Object;

namespace WhatLightRemains.Tests
{
    public sealed class VitalsStateTests
    {
        public enum Vital { Health, Hunger, Oxygen }

        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Vitals State Tests");
            root.SetActive(false);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [TestCase(Vital.Health)]
        [TestCase(Vital.Hunger)]
        [TestCase(Vital.Oxygen)]
        public void Defaults_AreFullAndInitializationEmitsNoChange(Vital vital)
        {
            Subject subject = Create(vital);
            int changes = 0;
            subject.Subscribe((_, _) => changes++);

            Assert.That(subject.Current(), Is.EqualTo(100f));
            Assert.That(subject.Maximum(), Is.EqualTo(100f));
            Assert.That(subject.Normalized(), Is.EqualTo(1f));
            subject.Reset();
            Assert.That(changes, Is.Zero);
        }

        [TestCase(Vital.Health)]
        [TestCase(Vital.Hunger)]
        [TestCase(Vital.Oxygen)]
        public void Mutations_SupportFractionsAndClampFiniteExtremes(Vital vital)
        {
            Subject subject = Create(vital);
            subject.Remove(12.25f);
            Assert.That(subject.Current(), Is.EqualTo(87.75f));
            subject.Add(0.125f);
            Assert.That(subject.Current(), Is.EqualTo(87.875f));
            Assert.That(subject.Normalized(), Is.EqualTo(0.87875f).Within(0.000001f));

            subject.Set(27.5f);
            Assert.That(subject.Current(), Is.EqualTo(27.5f));
            subject.Add(float.MaxValue);
            Assert.That(subject.Current(), Is.EqualTo(100f));
            subject.Remove(float.MaxValue);
            Assert.That(subject.Current(), Is.Zero);
            Assert.That(subject.Normalized(), Is.Zero);
            subject.Set(float.MaxValue);
            Assert.That(subject.Current(), Is.EqualTo(100f));
            subject.Set(-float.MaxValue);
            Assert.That(subject.Current(), Is.Zero);
        }

        [TestCase(Vital.Health)]
        [TestCase(Vital.Hunger)]
        [TestCase(Vital.Oxygen)]
        public void InvalidInputs_ThrowWithoutMutationOrEvents(Vital vital)
        {
            Subject subject = Create(vital);
            subject.Set(62.5f);
            int changes = 0;
            subject.Subscribe((_, _) => changes++);

            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => subject.Add(invalid));
                Assert.Throws<ArgumentOutOfRangeException>(() => subject.Remove(invalid));
                Assert.Throws<ArgumentOutOfRangeException>(() => subject.Set(invalid));
                Assert.That(subject.Current(), Is.EqualTo(62.5f));
            }

            foreach (float negative in new[] { -0.125f, -float.MaxValue })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => subject.Add(negative));
                Assert.Throws<ArgumentOutOfRangeException>(() => subject.Remove(negative));
                Assert.That(subject.Current(), Is.EqualTo(62.5f));
            }

            Assert.That(subject.Maximum(), Is.EqualTo(100f));
            Assert.That(changes, Is.Zero);
        }

        [TestCase(Vital.Health)]
        [TestCase(Vital.Hunger)]
        [TestCase(Vital.Oxygen)]
        public void Events_EmitExactlyOnceWithFinalStateAndSkipNoOps(Vital vital)
        {
            Subject subject = Create(vital);
            var snapshots = new List<(float current, float maximum)>();
            subject.Subscribe((current, maximum) =>
            {
                Assert.That(subject.Current(), Is.EqualTo(current));
                Assert.That(subject.Maximum(), Is.EqualTo(maximum));
                snapshots.Add((current, maximum));
            });

            subject.Add(0f);
            subject.Remove(0f);
            subject.Add(10f);
            subject.Set(1000f);
            subject.Reset();
            Assert.That(snapshots, Is.Empty);

            subject.Remove(12.5f);
            subject.Set(87.5f);
            subject.Set(-1f);
            subject.Remove(50f);
            subject.Set(-10f);
            subject.Add(150f);
            subject.Add(1f);
            Assert.That(snapshots, Is.EqualTo(new[] { (87.5f, 100f), (0f, 100f), (100f, 100f) }));

            subject.Set(35f);
            subject.Reset();
            subject.Reset();
            Assert.That(snapshots.Count, Is.EqualTo(5));
            Assert.That(snapshots[4], Is.EqualTo((100f, 100f)));
        }

        [TestCase(Vital.Health)]
        [TestCase(Vital.Hunger)]
        [TestCase(Vital.Oxygen)]
        public void InspectorConfiguration_SupportsFractionalStartAndPositiveCapacity(Vital vital)
        {
            Subject subject = Create(vital);
            SetConfiguration(subject, 31.25f, 125f);
            Invoke(subject.Owner, "OnValidate");

            Assert.That(subject.Current(), Is.EqualTo(31.25f));
            Assert.That(subject.Maximum(), Is.EqualTo(125f));
            Assert.That(subject.Normalized(), Is.EqualTo(0.25f));
            subject.Add(500f);
            Assert.That(subject.Current(), Is.EqualTo(125f));
            subject.Reset();
            Assert.That(subject.Current(), Is.EqualTo(31.25f));
        }

        [TestCase(Vital.Health, true)]
        [TestCase(Vital.Health, false)]
        [TestCase(Vital.Hunger, true)]
        [TestCase(Vital.Hunger, false)]
        [TestCase(Vital.Oxygen, true)]
        [TestCase(Vital.Oxygen, false)]
        public void InvalidInspectorCapacity_IsRepairedInEditorAndAtInitialization(Vital vital, bool validate)
        {
            foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Subject subject = Create(vital);
                SetConfiguration(subject, 35.5f, invalid);
                if (validate) Invoke(subject.Owner, "OnValidate");

                Assert.That(subject.Maximum(), Is.EqualTo(100f));
                Assert.That(subject.Current(), Is.EqualTo(35.5f));
                Assert.That(subject.Normalized(), Is.EqualTo(0.355f).Within(0.000001f));
                if (validate)
                {
                    Assert.That(ReadField(subject.Owner, subject.MaximumField), Is.EqualTo(100f));
                }
            }
        }

        [TestCase(Vital.Health, true)]
        [TestCase(Vital.Health, false)]
        [TestCase(Vital.Hunger, true)]
        [TestCase(Vital.Hunger, false)]
        [TestCase(Vital.Oxygen, true)]
        [TestCase(Vital.Oxygen, false)]
        public void InvalidInspectorStart_IsClampedOrRepairedToFull(Vital vital, bool validate)
        {
            foreach (float start in new[] { -15f, 250f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Subject subject = Create(vital);
                SetConfiguration(subject, start, 125f);
                if (validate) Invoke(subject.Owner, "OnValidate");

                float expected = start == -15f ? 0f : 125f;
                Assert.That(subject.Current(), Is.EqualTo(expected));
                Assert.That(subject.Maximum(), Is.EqualTo(125f));
                if (validate)
                {
                    Assert.That(ReadField(subject.Owner, subject.StartField), Is.EqualTo(expected));
                }
            }
        }

        [TestCase(Vital.Health)]
        [TestCase(Vital.Hunger)]
        [TestCase(Vital.Oxygen)]
        public void FiniteArithmeticOverflow_SaturatesAtConfiguredMaximum(Vital vital)
        {
            Subject subject = Create(vital);
            SetConfiguration(subject, float.MaxValue * 0.75f, float.MaxValue);
            subject.Add(float.MaxValue);
            Assert.That(subject.Current(), Is.EqualTo(float.MaxValue));
            Assert.That(subject.Normalized(), Is.EqualTo(1f));
            subject.Remove(float.MaxValue);
            Assert.That(subject.Current(), Is.Zero);
        }

        [TestCase(Vital.Health)]
        [TestCase(Vital.Hunger)]
        [TestCase(Vital.Oxygen)]
        public void StrictlyPositiveCapacity_IncludingTinyValues_RemainsValid(Vital vital)
        {
            Subject subject = Create(vital);
            const float tinyCapacity = 0.00001f;
            SetConfiguration(subject, tinyCapacity, tinyCapacity);
            Invoke(subject.Owner, "OnValidate");
            Assert.That(subject.Maximum(), Is.EqualTo(tinyCapacity));
            Assert.That(subject.Normalized(), Is.EqualTo(1f));
        }

        [TestCase(Vital.Health)]
        [TestCase(Vital.Hunger)]
        [TestCase(Vital.Oxygen)]
        public void InitializationAndValidation_DoNotOverwriteLiveStateOrCapturedConfiguration(Vital vital)
        {
            Subject subject = Create(vital);
            SetConfiguration(subject, 60f, 120f);
            Invoke(subject.Owner, "Awake");
            subject.Set(27.5f);
            int changes = 0;
            subject.Subscribe((_, _) => changes++);
            SetConfiguration(subject, 100f, 200f);
            Invoke(subject.Owner, "OnValidate");
            Invoke(subject.Owner, "Awake");
            ((Behaviour)subject.Owner).enabled = false;
            ((Behaviour)subject.Owner).enabled = true;

            Assert.That(subject.Current(), Is.EqualTo(27.5f));
            Assert.That(subject.Maximum(), Is.EqualTo(120f));
            Assert.That(changes, Is.Zero);
            subject.Reset();
            Assert.That(subject.Current(), Is.EqualTo(60f));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void PlayerValues_AreIndependentAndResetEmitsOneEventPerChangedValue()
        {
            PlayerVitals player = Create(Vital.Health).Owner as PlayerVitals;
            int healthChanges = 0;
            int hungerChanges = 0;
            player.HealthChanged += (_, _) => healthChanges++;
            player.HungerChanged += (_, _) => hungerChanges++;

            player.RemoveHealth(75f);
            Assert.That(player.HealthCurrent, Is.EqualTo(25f));
            Assert.That(player.HungerCurrent, Is.EqualTo(100f));
            Assert.That(hungerChanges, Is.Zero);
            player.RemoveHunger(50f);
            Assert.That(player.HealthCurrent, Is.EqualTo(25f));
            Assert.That(player.HungerCurrent, Is.EqualTo(50f));
            Assert.That(healthChanges, Is.EqualTo(1));
            player.ResetToStartingValues();
            player.ResetToStartingValues();
            Assert.That(player.HealthCurrent, Is.EqualTo(100f));
            Assert.That(player.HungerCurrent, Is.EqualTo(100f));
            Assert.That(healthChanges, Is.EqualTo(2));
            Assert.That(hungerChanges, Is.EqualTo(2));
        }

        private Subject Create(Vital vital)
        {
            var owner = new GameObject(vital.ToString());
            owner.transform.SetParent(root.transform);
            if (vital == Vital.Oxygen)
            {
                WorldOxygenReserve oxygen = owner.AddComponent<WorldOxygenReserve>();
                return new Subject
                {
                    Owner = oxygen, StartField = "startingOxygen", MaximumField = "maximumOxygen",
                    Current = () => oxygen.CurrentOxygen, Maximum = () => oxygen.MaxOxygen,
                    Normalized = () => oxygen.OxygenNormalized, Add = oxygen.AddOxygen,
                    Remove = oxygen.RemoveOxygen, Set = oxygen.SetOxygen, Reset = oxygen.ResetToStartingValues,
                    Subscribe = callback => oxygen.OxygenChanged += callback
                };
            }

            PlayerVitals player = owner.AddComponent<PlayerVitals>();
            return vital == Vital.Health
                ? new Subject
                {
                    Owner = player, StartField = "startingHealth", MaximumField = "maximumHealth",
                    Current = () => player.HealthCurrent, Maximum = () => player.HealthMax,
                    Normalized = () => player.HealthNormalized, Add = player.AddHealth,
                    Remove = player.RemoveHealth, Set = player.SetHealth, Reset = player.ResetToStartingValues,
                    Subscribe = callback => player.HealthChanged += callback
                }
                : new Subject
                {
                    Owner = player, StartField = "startingHunger", MaximumField = "maximumHunger",
                    Current = () => player.HungerCurrent, Maximum = () => player.HungerMax,
                    Normalized = () => player.HungerNormalized, Add = player.AddHunger,
                    Remove = player.RemoveHunger, Set = player.SetHunger, Reset = player.ResetToStartingValues,
                    Subscribe = callback => player.HungerChanged += callback
                };
        }

        private static void SetConfiguration(Subject subject, float start, float maximum)
        {
            WriteField(subject.Owner, subject.StartField, start);
            WriteField(subject.Owner, subject.MaximumField, maximum);
        }

        private static void WriteField(Component owner, string field, float value) =>
            owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);

        private static float ReadField(Component owner, string field) =>
            (float)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static void Invoke(Component owner, string method) =>
            owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);

        private sealed class Subject
        {
            public Component Owner;
            public string StartField;
            public string MaximumField;
            public Func<float> Current;
            public Func<float> Maximum;
            public Func<float> Normalized;
            public Action<float> Add;
            public Action<float> Remove;
            public Action<float> Set;
            public Action Reset;
            public Action<Action<float, float>> Subscribe;
        }
    }
}
