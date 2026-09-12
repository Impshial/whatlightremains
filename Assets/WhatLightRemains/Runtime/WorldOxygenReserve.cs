using System;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    /// <summary>
    /// One scene-owned shared oxygen reserve for the entire world, including disconnected rooms.
    /// This is an abstract supply, not personal breath or atmospheric concentration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldOxygenReserve : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Initial shared world reserve. No room or player action automatically consumes oxygen yet.")]
        private float startingOxygen = 100f;
        [SerializeField, Min(0.001f), Tooltip("Placeholder shared capacity, independent of room count. Finite and strictly positive; invalid configuration repairs to 100.")]
        private float maximumOxygen = 100f;

        private BoundedVitalValue oxygen;

        public float CurrentOxygen { get { EnsureInitialized(); return oxygen.Current; } }
        public float MaxOxygen { get { EnsureInitialized(); return oxygen.Maximum; } }
        public float OxygenNormalized { get { EnsureInitialized(); return oxygen.Normalized; } }

        /// <summary>Emits the final current reserve and maximum once per effective change.</summary>
        public event Action<float, float> OxygenChanged;

        /// <summary>Adds a finite, nonnegative amount; invalid input throws ArgumentOutOfRangeException.</summary>
        public void AddOxygen(float amount)
        {
            EnsureInitialized();
            if (oxygen.Add(amount)) OxygenChanged?.Invoke(oxygen.Current, oxygen.Maximum);
        }

        /// <summary>Removes a finite, nonnegative amount; invalid input throws ArgumentOutOfRangeException.</summary>
        public void RemoveOxygen(float amount)
        {
            EnsureInitialized();
            if (oxygen.Remove(amount)) OxygenChanged?.Invoke(oxygen.Current, oxygen.Maximum);
        }

        /// <summary>Clamps finite reserves to [0, maximum]; nonfinite input throws ArgumentOutOfRangeException.</summary>
        public void SetOxygen(float value)
        {
            EnsureInitialized();
            if (oxygen.Set(value)) OxygenChanged?.Invoke(oxygen.Current, oxygen.Maximum);
        }

        /// <summary>Restores the session's captured starting reserve; unchanged values emit no event.</summary>
        public void ResetToStartingValues()
        {
            EnsureInitialized();
            if (oxygen.Reset()) OxygenChanged?.Invoke(oxygen.Current, oxygen.Maximum);
        }

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            if (oxygen == null) oxygen = new BoundedVitalValue(startingOxygen, maximumOxygen);
        }

        private void OnValidate()
        {
            BoundedVitalValue.SanitizeConfiguration(ref startingOxygen, ref maximumOxygen);
        }
    }
}
