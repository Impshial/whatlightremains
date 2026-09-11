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
        public const string RotateYText = "<b>Ctrl: Rotate around y-axis</b>\nCtrl+Alt: Rotate around z-axis";
        public const string RotateZText = "Ctrl: Rotate around y-axis\n<b>Ctrl+Alt: Rotate around z-axis</b>";
        public const string TraverseText = "Hold E to Traverse";

        [SerializeField] private Text instructionLabel;
        [SerializeField] private Text rotationLabel;
        [SerializeField] private Text traversalLabel;

        public Text InstructionLabel => instructionLabel;
        public Text RotationLabel => rotationLabel;
        public string CurrentText => instructionLabel != null ? instructionLabel.text : string.Empty;
        public string CurrentRotationText => rotationLabel != null ? rotationLabel.text : string.Empty;
        public Text TraversalLabel => traversalLabel;

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

        public void Configure(Text label, Text newRotationLabel, Text newTraversalLabel)
        {
            instructionLabel = label;
            rotationLabel = newRotationLabel;
            traversalLabel = newTraversalLabel;
            if (traversalLabel != null)
            {
                traversalLabel.text = TraverseText;
                traversalLabel.gameObject.SetActive(false);
            }
            SetCreateMode(false);
        }

        public void SetTraversalPrompt(bool visible)
        {
            if (traversalLabel == null) return;
            traversalLabel.text = TraverseText;
            traversalLabel.gameObject.SetActive(visible);
        }

        public void SetCreateMode(bool createMode)
        {
            if (instructionLabel != null)
            {
                instructionLabel.text = createMode ? CreateText : NormalText;
            }


            if (rotationLabel != null)
            {
                rotationLabel.supportRichText = true;
                rotationLabel.gameObject.SetActive(createMode);
                rotationLabel.text = RotationIdleText;
            }
            if (createMode) SetTraversalPrompt(false);
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
