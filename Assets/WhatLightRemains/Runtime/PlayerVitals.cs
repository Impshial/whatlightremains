using System;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    /// <summary>Player-owned health and food reserves. Values change only through explicit API calls.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerVitals : MonoBehaviour
    {
        [Header("Health — session starting configuration")]
        [SerializeField, Min(0f), Tooltip("Initial health, clamped to the configured maximum at session initialization.")]
        private float startingHealth = 100f;
        [SerializeField, Min(0.001f), Tooltip("Finite, strictly positive health capacity. Invalid configuration repairs to 100.")]
        private float maximumHealth = 100f;

        [Header("Hunger — full means fed")]
        [SerializeField, Min(0f), Tooltip("Food reserve: 100/100 is fully fed; zero is empty. No automatic consumption yet.")]
        private float startingHunger = 100f;
        [SerializeField, Min(0.001f), Tooltip("Finite, strictly positive food capacity, captured at session initialization.")]
        private float maximumHunger = 100f;

        private BoundedVitalValue health;
        private BoundedVitalValue hunger;

        public float HealthCurrent { get { EnsureInitialized(); return health.Current; } }
        public float HealthMax { get { EnsureInitialized(); return health.Maximum; } }
        public float HealthNormalized { get { EnsureInitialized(); return health.Normalized; } }
        public float HungerCurrent { get { EnsureInitialized(); return hunger.Current; } }
        public float HungerMax { get { EnsureInitialized(); return hunger.Maximum; } }
        public float HungerNormalized { get { EnsureInitialized(); return hunger.Normalized; } }

        /// <summary>Emits the final current value and maximum once per effective health change.</summary>
        public event Action<float, float> HealthChanged;
        /// <summary>Emits the final current value and maximum once per effective food-reserve change.</summary>
        public event Action<float, float> HungerChanged;

        /// <summary>Adds a finite, nonnegative amount; invalid input throws ArgumentOutOfRangeException.</summary>
        public void AddHealth(float amount)
        {
            EnsureInitialized();
            if (health.Add(amount)) HealthChanged?.Invoke(health.Current, health.Maximum);
        }

        /// <summary>Removes a finite, nonnegative amount; invalid input throws ArgumentOutOfRangeException.</summary>
        public void RemoveHealth(float amount)
        {
            EnsureInitialized();
            if (health.Remove(amount)) HealthChanged?.Invoke(health.Current, health.Maximum);
        }

        /// <summary>Clamps finite values to [0, maximum]; nonfinite input throws ArgumentOutOfRangeException.</summary>
        public void SetHealth(float value)
        {
            EnsureInitialized();
            if (health.Set(value)) HealthChanged?.Invoke(health.Current, health.Maximum);
        }

        /// <summary>Adds food using a finite, nonnegative amount; invalid input throws ArgumentOutOfRangeException.</summary>
        public void AddHunger(float amount)
        {
            EnsureInitialized();
            if (hunger.Add(amount)) HungerChanged?.Invoke(hunger.Current, hunger.Maximum);
        }

        /// <summary>Removes food using a finite, nonnegative amount; invalid input throws ArgumentOutOfRangeException.</summary>
        public void RemoveHunger(float amount)
        {
            EnsureInitialized();
            if (hunger.Remove(amount)) HungerChanged?.Invoke(hunger.Current, hunger.Maximum);
        }

        /// <summary>Clamps finite food reserves to [0, maximum]; nonfinite input throws ArgumentOutOfRangeException.</summary>
        public void SetHunger(float value)
        {
            EnsureInitialized();
            if (hunger.Set(value)) HungerChanged?.Invoke(hunger.Current, hunger.Maximum);
        }

        /// <summary>Restores the session's captured starting values; unchanged values emit no event.</summary>
        public void ResetToStartingValues()
        {
            EnsureInitialized();
            if (health.Reset()) HealthChanged?.Invoke(health.Current, health.Maximum);
            if (hunger.Reset()) HungerChanged?.Invoke(hunger.Current, hunger.Maximum);
        }

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            if (health != null) return;
            health = new BoundedVitalValue(startingHealth, maximumHealth);
            hunger = new BoundedVitalValue(startingHunger, maximumHunger);
        }

        private void OnValidate()
        {
            BoundedVitalValue.SanitizeConfiguration(ref startingHealth, ref maximumHealth);
            BoundedVitalValue.SanitizeConfiguration(ref startingHunger, ref maximumHunger);
        }
    }
}
