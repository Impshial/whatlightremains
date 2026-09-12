using UnityEngine;

namespace WhatLightRemains.Runtime
{
    /// <summary>Explicit session references; disabling presentation never resets the owners.</summary>
    [DisallowMultipleComponent]
    public sealed class VitalsHudView : MonoBehaviour
    {
        [SerializeField] private PlayerVitals playerVitals;
        [SerializeField] private WorldOxygenReserve oxygenReserve;
        [SerializeField] private VitalGaugeView healthGauge;
        [SerializeField] private VitalGaugeView hungerGauge;
        [SerializeField] private VitalGaugeView oxygenGauge;
        private PlayerVitals subscribedPlayer;
        private WorldOxygenReserve subscribedOxygen;

        public PlayerVitals PlayerVitals => playerVitals;
        public WorldOxygenReserve OxygenReserve => oxygenReserve;
        public VitalGaugeView HealthGauge => healthGauge;
        public VitalGaugeView HungerGauge => hungerGauge;
        public VitalGaugeView OxygenGauge => oxygenGauge;

        public void ConfigureGauges(VitalGaugeView health, VitalGaugeView hunger, VitalGaugeView oxygen)
        {
            healthGauge = health;
            hungerGauge = hunger;
            oxygenGauge = oxygen;
            if (isActiveAndEnabled) Bind();
        }

        public void Configure(PlayerVitals player, WorldOxygenReserve worldOxygen)
        {
            Unbind();
            playerVitals = player;
            oxygenReserve = worldOxygen;
            if (isActiveAndEnabled) Bind();
        }

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();

        private void Bind()
        {
            Unbind();
            if (playerVitals != null)
            {
                ShowHealth(playerVitals.HealthCurrent, playerVitals.HealthMax);
                ShowHunger(playerVitals.HungerCurrent, playerVitals.HungerMax);
                subscribedPlayer = playerVitals;
                subscribedPlayer.HealthChanged += ShowHealth;
                subscribedPlayer.HungerChanged += ShowHunger;
            }
            if (oxygenReserve != null)
            {
                ShowOxygen(oxygenReserve.CurrentOxygen, oxygenReserve.MaxOxygen);
                subscribedOxygen = oxygenReserve;
                subscribedOxygen.OxygenChanged += ShowOxygen;
            }
        }

        private void Unbind()
        {
            if (subscribedPlayer != null)
            {
                subscribedPlayer.HealthChanged -= ShowHealth;
                subscribedPlayer.HungerChanged -= ShowHunger;
            }
            if (subscribedOxygen != null) subscribedOxygen.OxygenChanged -= ShowOxygen;
            subscribedPlayer = null;
            subscribedOxygen = null;
        }

        private void ShowHealth(float current, float maximum) => healthGauge?.ShowValue(current, maximum);
        private void ShowHunger(float current, float maximum) => hungerGauge?.ShowValue(current, maximum);
        private void ShowOxygen(float current, float maximum) => oxygenGauge?.ShowValue(current, maximum);
    }
}
