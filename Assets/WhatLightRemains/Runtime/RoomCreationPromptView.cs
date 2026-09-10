using UnityEngine;
using UnityEngine.UI;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RoomCreationPromptView : MonoBehaviour
    {
        public const string NormalText = "Press C to create a room";
        public const string CreateText = "Left-Click to finalize placement";
        public const string RotationIdleText = "Hold Ctrl to Rotate";
        public const string RotateYText = "Mouse Wheel: Rotate Y 90°";
        public const string RotateZText = "Mouse Wheel: Rotate Z 90°";

        [SerializeField] private Text instructionLabel;
        [SerializeField] private Text rotationLabel;

        public Text InstructionLabel => instructionLabel;
        public Text RotationLabel => rotationLabel;
        public string CurrentText => instructionLabel != null ? instructionLabel.text : string.Empty;
        public string CurrentRotationText => rotationLabel != null ? rotationLabel.text : string.Empty;

        public void Configure(Text label)
        {
            instructionLabel = label;
            SetCreateMode(false);
        }

        public void Configure(Text label, Text newRotationLabel)
        {
            instructionLabel = label;
            rotationLabel = newRotationLabel;
            SetCreateMode(false);
        }

        public void SetCreateMode(bool createMode)
        {
            if (instructionLabel != null)
            {
                instructionLabel.text = createMode ? CreateText : NormalText;
            }


            if (rotationLabel != null)
            {
                rotationLabel.gameObject.SetActive(createMode);
                rotationLabel.text = RotationIdleText;
            }
        }

        public void SetRotationState(bool createMode, bool controlHeld, bool alternateAxisHeld)
        {
            if (rotationLabel == null)
            {
                return;
            }

            rotationLabel.gameObject.SetActive(createMode);
            if (!createMode)
            {
                return;
            }

            rotationLabel.text = !controlHeld
                ? RotationIdleText
                : alternateAxisHeld ? RotateZText : RotateYText;
        }

        private void Awake()
        {
            SetCreateMode(false);
        }
    }
}
