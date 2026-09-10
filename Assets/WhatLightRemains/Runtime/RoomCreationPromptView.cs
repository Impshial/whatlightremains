using UnityEngine;
using UnityEngine.UI;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RoomCreationPromptView : MonoBehaviour
    {
        public const string NormalText = "Press C to create a room";
        public const string CreateText = "Left-Click to finalize placement";

        [SerializeField] private Text instructionLabel;

        public Text InstructionLabel => instructionLabel;
        public string CurrentText => instructionLabel != null ? instructionLabel.text : string.Empty;

        public void Configure(Text label)
        {
            instructionLabel = label;
            SetCreateMode(false);
        }

        public void SetCreateMode(bool createMode)
        {
            if (instructionLabel != null)
            {
                instructionLabel.text = createMode ? CreateText : NormalText;
            }
        }

        private void Awake()
        {
            SetCreateMode(false);
        }
    }
}
