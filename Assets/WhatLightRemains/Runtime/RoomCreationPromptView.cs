using UnityEngine;
using UnityEngine.UI;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RoomCreationPromptView : MonoBehaviour
    {
        public const string NormalText = "Press C to create a room";
        public const string NormalDeleteText = "Press Del to Delete a Room";
        public const string CreateText = "Left-Click to finalize placement";
        public const string DeleteModeText = "Press Esc to Exit Delete Mode";
        public const string DeleteFocusText = "Right-Click to Delete room";
        public const string RotationIdleText = "Hold Ctrl to Rotate";
        public const string RotateYText = "<b>Ctrl: Rotate around y-axis</b>\nCtrl+Alt: Rotate around z-axis";
        public const string RotateZText = "Ctrl: Rotate around y-axis\n<b>Ctrl+Alt: Rotate around z-axis</b>";
        public const string TraverseText = "Hold E to Traverse";

        [SerializeField] private Text instructionLabel;
        [SerializeField] private Text deleteInstructionLabel;
        [SerializeField] private Text rotationLabel;
        [SerializeField] private Text traversalLabel;

        private bool isCreateMode;
        private bool isDeleteMode;
        private bool isMapMode;

        public Text InstructionLabel => instructionLabel;
        public Text DeleteInstructionLabel => deleteInstructionLabel;
        public Text RotationLabel => rotationLabel;
        public string CurrentText => instructionLabel != null ? instructionLabel.text : string.Empty;
        public string CurrentRotationText => rotationLabel != null ? rotationLabel.text : string.Empty;
        public Text TraversalLabel => traversalLabel;
        public string CurrentDeleteText => deleteInstructionLabel != null ? deleteInstructionLabel.text : string.Empty;

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

        public void Configure(Text label, Text newDeleteInstructionLabel, Text newRotationLabel, Text newTraversalLabel)
        {
            instructionLabel = label;
            deleteInstructionLabel = newDeleteInstructionLabel;
            rotationLabel = newRotationLabel;
            traversalLabel = newTraversalLabel;
            if (traversalLabel != null)
            {
                traversalLabel.text = TraverseText;
                traversalLabel.gameObject.SetActive(false);
            }
            ShowDefaultMode();
        }

        public void SetTraversalPrompt(bool visible)
        {
            if (traversalLabel == null) return;
            traversalLabel.text = TraverseText;
            traversalLabel.gameObject.SetActive(visible && !isCreateMode && !isDeleteMode && !isMapMode);
        }

        public void SetMapMode(bool mapMode)
        {
            isMapMode = mapMode;
            if (isMapMode)
            {
                HideAllLabels();
                return;
            }

            if (isCreateMode) ApplyCreateModePresentation();
            else if (isDeleteMode) ApplyDeleteModePresentation();
            else ShowDefaultMode();
        }

        public void SetCreateMode(bool createMode)
        {
            if (!createMode)
            {
                ShowDefaultMode();
                return;
            }

            isCreateMode = true;
            isDeleteMode = false;
            if (isMapMode)
            {
                HideAllLabels();
                return;
            }
            ApplyCreateModePresentation();
        }

        private void ApplyCreateModePresentation()
        {
            if (instructionLabel != null)
            {
                instructionLabel.gameObject.SetActive(true);
                instructionLabel.text = CreateText;
            }
            if (deleteInstructionLabel != null) deleteInstructionLabel.gameObject.SetActive(false);
            HideContextLabel();
            SetTraversalPrompt(false);
        }

        public void SetRotationState(bool createMode, bool ghostVisible, bool controlHeld, bool alternateAxisHeld)
        {
            if (rotationLabel == null)
            {
                return;
            }

            bool visible = createMode && isCreateMode && ghostVisible && !isMapMode;
            rotationLabel.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            rotationLabel.text = !controlHeld
                ? RotationIdleText
                : alternateAxisHeld ? RotateZText : RotateYText;
        }

        public void SetDeleteMode(bool deleteMode)
        {
            if (!deleteMode)
            {
                ShowDefaultMode();
                return;
            }

            isCreateMode = false;
            isDeleteMode = true;
            if (isMapMode)
            {
                HideAllLabels();
                return;
            }
            ApplyDeleteModePresentation();
        }

        private void ApplyDeleteModePresentation()
        {
            if (instructionLabel != null) instructionLabel.gameObject.SetActive(false);
            if (deleteInstructionLabel != null)
            {
                deleteInstructionLabel.gameObject.SetActive(true);
                deleteInstructionLabel.text = DeleteModeText;
            }
            HideContextLabel();
            SetTraversalPrompt(false);
        }

        public void SetDeleteTarget(bool visible)
        {
            if (rotationLabel == null) return;
            bool show = isDeleteMode && visible && !isMapMode;
            rotationLabel.gameObject.SetActive(show);
            if (show) rotationLabel.text = DeleteFocusText;
        }

        private void ShowDefaultMode()
        {
            isCreateMode = false;
            isDeleteMode = false;
            if (isMapMode)
            {
                HideAllLabels();
                return;
            }
            if (instructionLabel != null)
            {
                instructionLabel.gameObject.SetActive(true);
                instructionLabel.text = NormalText;
            }
            if (deleteInstructionLabel != null)
            {
                deleteInstructionLabel.gameObject.SetActive(true);
                deleteInstructionLabel.text = NormalDeleteText;
            }
            HideContextLabel();
            SetTraversalPrompt(false);
        }

        private void HideContextLabel()
        {
            if (rotationLabel == null) return;
            rotationLabel.supportRichText = true;
            rotationLabel.text = RotationIdleText;
            rotationLabel.gameObject.SetActive(false);
        }

        private void HideAllLabels()
        {
            if (instructionLabel != null) instructionLabel.gameObject.SetActive(false);
            if (deleteInstructionLabel != null) deleteInstructionLabel.gameObject.SetActive(false);
            if (rotationLabel != null) rotationLabel.gameObject.SetActive(false);
            if (traversalLabel != null) traversalLabel.gameObject.SetActive(false);
        }

        private void Awake()
        {
            SetCreateMode(false);
        }
    }
}
