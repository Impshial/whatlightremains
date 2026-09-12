using System;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    // A session snapshot: Inspector configuration never becomes mutable gameplay state.
    internal sealed class BoundedVitalValue
    {
        private readonly float startingValue;

        public float Current { get; private set; }
        public float Maximum { get; }
        public float Normalized => Current / Maximum;

        public BoundedVitalValue(float start, float maximum)
        {
            SanitizeConfiguration(ref start, ref maximum);
            startingValue = start;
            Current = start;
            Maximum = maximum;
        }

        public bool Add(float amount)
        {
            ValidateAmount(amount);
            // Use a wider intermediate so finite amounts can saturate even near float.MaxValue.
            return SetClamped((float)Math.Min((double)Current + amount, Maximum));
        }

        public bool Remove(float amount)
        {
            ValidateAmount(amount);
            return SetClamped(Mathf.Max(0f, Current - amount));
        }

        public bool Set(float value)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A vital value must be finite.");
            }

            return SetClamped(Mathf.Clamp(value, 0f, Maximum));
        }

        public bool Reset() => SetClamped(startingValue);

        internal static void SanitizeConfiguration(ref float start, ref float maximum)
        {
            if (!IsFinite(maximum) || maximum <= 0f)
            {
                maximum = 100f;
            }

            start = IsFinite(start) ? Mathf.Clamp(start, 0f, maximum) : maximum;
        }

        private static void ValidateAmount(float amount)
        {
            if (!IsFinite(amount) || amount < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount,
                    "Add and remove amounts must be finite and nonnegative.");
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private bool SetClamped(float value)
        {
            if (Current == value)
            {
                return false;
            }

            Current = value;
            return true;
        }
    }
}
