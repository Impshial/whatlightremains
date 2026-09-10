using UnityEngine;
using UnityEngine.InputSystem;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        private InputActionAsset runtimeActions;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction releaseCursorAction;
        private InputAction captureCursorAction;
        private InputAction toggleCreateAction;
        private InputAction placeRoomAction;
        private InputAction sprintAction;
        private InputAction rotateModifierAction;
        private InputAction alternateRotationAxisAction;
        private InputAction rotationScrollAction;

        [SerializeField, Min(0.01f)] private float mouseWheelStepSize = 120f;
        private float accumulatedRotationScroll;

        public InputActionAsset Actions => inputActions;
        public Vector2 Move => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
        public bool JumpPressedThisFrame => jumpAction != null && jumpAction.WasPressedThisFrame();
        public bool ReleaseCursorPressedThisFrame => releaseCursorAction != null && releaseCursorAction.WasPressedThisFrame();
        public bool CaptureCursorPressedThisFrame => captureCursorAction != null && captureCursorAction.WasPressedThisFrame();
        public bool ToggleCreatePressedThisFrame => toggleCreateAction != null && toggleCreateAction.WasPressedThisFrame();
        public bool PlaceRoomPressedThisFrame => placeRoomAction != null && placeRoomAction.WasPressedThisFrame();
        public bool SprintHeld => IsPressed(sprintAction) || IsShiftPressed();
        public bool RotateModifierHeld => IsPressed(rotateModifierAction) || IsControlPressed();
        public bool AlternateRotationAxisHeld => IsPressed(alternateRotationAxisAction) || IsAltPressed();

        /// <summary>
        /// Consumes whole wheel detents while retaining fractional high-resolution wheel input.
        /// Scroll input that occurs without Ctrl is deliberately discarded so it cannot cause a
        /// delayed rotation the next time Create-mode rotation is entered.
        /// </summary>
        public int ConsumeRotationScrollSteps()
        {
            if (!RotateModifierHeld)
            {
                accumulatedRotationScroll = 0f;
                return 0;
            }

            float scroll = ReadRotationScroll();
            if (Mathf.Approximately(scroll, 0f))
            {
                return 0;
            }

            accumulatedRotationScroll += scroll;
            float stepSize = Mathf.Max(0.01f, mouseWheelStepSize);
            int steps = accumulatedRotationScroll >= 0f
                ? Mathf.FloorToInt(accumulatedRotationScroll / stepSize)
                : Mathf.CeilToInt(accumulatedRotationScroll / stepSize);
            accumulatedRotationScroll -= steps * stepSize;
            return steps;
        }

        public void ClearRotationScroll()
        {
            accumulatedRotationScroll = 0f;
        }

        public void Configure(InputActionAsset actions)
        {
            inputActions = actions;
            if (isActiveAndEnabled && Application.isPlaying)
            {
                BindRuntimeActions();
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                BindRuntimeActions();
            }
        }

        private void OnDisable()
        {
            ReleaseRuntimeActions();
        }

        private void BindRuntimeActions()
        {
            ReleaseRuntimeActions();
            if (inputActions == null)
            {
                return;
            }

            runtimeActions = Instantiate(inputActions);
            runtimeActions.hideFlags = HideFlags.DontSave;
            moveAction = FindAction(runtimeActions, "Player/Move", "Gameplay/Move", "Move");
            lookAction = FindAction(runtimeActions, "Player/Look", "Gameplay/Look", "Look");
            jumpAction = FindAction(runtimeActions, "Player/Jump", "Gameplay/Jump", "Jump");
            releaseCursorAction = FindAction(runtimeActions, "Player/ReleaseCursor", "Gameplay/ReleaseCursor", "ReleaseCursor");
            captureCursorAction = FindAction(runtimeActions, "Player/CaptureCursor", "Gameplay/CaptureCursor", "CaptureCursor");
            toggleCreateAction = FindAction(runtimeActions, "Player/ToggleCreate", "Gameplay/ToggleCreate", "ToggleCreate");
            placeRoomAction = FindAction(runtimeActions, "Player/PlaceRoom", "Gameplay/PlaceRoom", "PlaceRoom");
            sprintAction = FindAction(runtimeActions, "Player/Sprint", "Gameplay/Sprint", "Sprint");
            rotateModifierAction = FindAction(runtimeActions, "Player/RotateModifier", "Gameplay/RotateModifier", "RotateModifier");
            alternateRotationAxisAction = FindAction(runtimeActions, "Player/AlternateRotationAxis", "Gameplay/AlternateRotationAxis", "AlternateRotationAxis");
            rotationScrollAction = FindAction(runtimeActions, "Player/RotationScroll", "Gameplay/RotationScroll", "RotationScroll");
            runtimeActions.Enable();
        }

        private void ReleaseRuntimeActions()
        {
            if (runtimeActions != null)
            {
                runtimeActions.Disable();
                Destroy(runtimeActions);
            }

            runtimeActions = null;
            moveAction = null;
            lookAction = null;
            jumpAction = null;
            releaseCursorAction = null;
            captureCursorAction = null;
            toggleCreateAction = null;
            placeRoomAction = null;
            sprintAction = null;
            rotateModifierAction = null;
            alternateRotationAxisAction = null;
            rotationScrollAction = null;
            accumulatedRotationScroll = 0f;
        }

        private float ReadRotationScroll()
        {
            if (rotationScrollAction != null)
            {
                return rotationScrollAction.ReadValue<Vector2>().y;
            }

            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        }

        private static bool IsPressed(InputAction action)
        {
            return action != null && action.IsPressed();
        }

        private static bool IsShiftPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.leftShiftKey.isPressed;
        }

        private static bool IsControlPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        }

        private static bool IsAltPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
        }

        private static InputAction FindAction(InputActionAsset asset, params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                InputAction action = asset.FindAction(candidates[i], false);
                if (action != null)
                {
                    return action;
                }
            }

            return null;
        }
    }
}
