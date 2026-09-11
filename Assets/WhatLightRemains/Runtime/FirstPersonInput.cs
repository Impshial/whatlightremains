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
        private InputAction traverseAction;

        // Input System 1.20 normalizes wheel deltas to roughly -1..1 by default. Older
        // platform-specific input (notably Windows) can still report +/-120, so rotation
        // must be based on direction rather than assuming either magnitude.
        [SerializeField, Range(0f, 0.5f)] private float mouseWheelDeadZone = 0.01f;

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
        public bool TraverseHeld => IsPressed(traverseAction) || (Keyboard.current != null && Keyboard.current.eKey.isPressed);
        public bool TraversePressedThisFrame => (traverseAction != null && traverseAction.WasPressedThisFrame())
            || (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);

        /// <summary>
        /// Converts the current frame's wheel delta into one discrete quarter-turn. Input System
        /// may report a wheel tick as either +/-1 or +/-120 depending on its scroll-delta setting;
        /// treating the value as a direction makes both configurations behave identically.
        /// </summary>
        public int ConsumeRotationScrollSteps()
        {
            if (!RotateModifierHeld)
            {
                return 0;
            }

            return ConvertRotationScrollDelta(ReadRotationScroll(), mouseWheelDeadZone);
        }

        public void ClearRotationScroll()
        {
            // Mouse scroll is a delta control and resets every input update. This method remains
            // as an explicit mode-transition hook and for compatibility with existing callers.
        }

        public static int ConvertRotationScrollDelta(float scrollDelta, float deadZone = 0.01f)
        {
            if (float.IsNaN(scrollDelta) || Mathf.Abs(scrollDelta) <= Mathf.Max(0f, deadZone))
            {
                return 0;
            }

            return scrollDelta > 0f ? 1 : -1;
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
            traverseAction = FindAction(runtimeActions, "Player/Traverse", "Gameplay/Traverse", "Traverse");
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
            traverseAction = null;
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
