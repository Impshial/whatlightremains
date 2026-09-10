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

        public InputActionAsset Actions => inputActions;
        public Vector2 Move => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
        public bool JumpPressedThisFrame => jumpAction != null && jumpAction.WasPressedThisFrame();
        public bool ReleaseCursorPressedThisFrame => releaseCursorAction != null && releaseCursorAction.WasPressedThisFrame();
        public bool CaptureCursorPressedThisFrame => captureCursorAction != null && captureCursorAction.WasPressedThisFrame();

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
