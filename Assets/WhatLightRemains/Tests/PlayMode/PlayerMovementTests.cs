using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class PlayerMovementTests
    {
        private const string SceneName = "Foundation";
        private const float SimulationStep = 1f / 120f;

        private Scene loadedScene;
        private Scene previousActiveScene;
        private CubeRoomClusterGenerator generator;
        private CubeRoom room;
        private FirstPersonMotor motor;
        private PlayerRoomTracker tracker;
        private CharacterController controller;
        private KinematicCapsuleMover capsuleMover;
        private PlayerGravityAlignment gravityAlignment;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousActiveScene = SceneManager.GetActiveScene();
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;

            loadedScene = SceneManager.GetSceneByName(SceneName);
            Assert.That(loadedScene.IsValid() && loadedScene.isLoaded, Is.True);
            SceneManager.SetActiveScene(loadedScene);

            generator = FindInScene<CubeRoomClusterGenerator>();
            room = generator.PrimaryRoom;
            Assert.That(room, Is.Not.Null, "Movement tests require the cluster's fixed primary room.");
            motor = FindInScene<FirstPersonMotor>();
            tracker = motor.GetComponent<PlayerRoomTracker>();
            Assert.That(tracker, Is.Not.Null);
            controller = motor.GetComponent<CharacterController>();
            capsuleMover = motor.GetComponent<KinematicCapsuleMover>();
            gravityAlignment = motor.GetComponent<PlayerGravityAlignment>();
            Assert.That(controller != null || capsuleMover != null, Is.True, "Player requires a legacy or arbitrary-gravity mover.");
            PlayerLook look = motor.GetComponent<PlayerLook>();
            if (look != null)
            {
                look.enabled = false;
            }

            // Advance the same public motor step that Update feeds from the Input
            // System, using a deterministic delta time.
            motor.enabled = false;
            yield return ResetPlayer();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }

            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(loadedScene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            loadedScene = default;
            previousActiveScene = default;
            generator = null;
            room = null;
            motor = null;
            tracker = null;
            controller = null;
            capsuleMover = null;
            gravityAlignment = null;
        }

        [UnityTest]
        public IEnumerator Movement_IsThreeMetersPerSecondAndDiagonalIsNormalized()
        {
            const float sampleDuration = 0.40f;

            Vector3 cardinalStart = motor.transform.position;
            SimulateFor(sampleDuration, Vector2.up);
            float cardinalSpeed = PlanarDistance(cardinalStart, motor.transform.position) / sampleDuration;

            yield return ResetPlayer();

            Vector3 diagonalStart = motor.transform.position;
            SimulateFor(sampleDuration, Vector2.one);
            float diagonalSpeed = PlanarDistance(diagonalStart, motor.transform.position) / sampleDuration;

            Assert.That(cardinalSpeed, Is.EqualTo(3f).Within(0.03f));
            Assert.That(diagonalSpeed, Is.EqualTo(3f).Within(0.03f));
            Assert.That(diagonalSpeed, Is.EqualTo(cardinalSpeed).Within(0.01f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Sprint_IsSixMetersPerSecondAndDiagonalIsNormalized()
        {
            const float sampleDuration = 0.30f;

            Vector3 cardinalStart = motor.transform.position;
            SimulateFor(sampleDuration, Vector2.up, true);
            float cardinalSpeed = PlanarDistance(cardinalStart, motor.transform.position) / sampleDuration;

            yield return ResetPlayer();

            Vector3 diagonalStart = motor.transform.position;
            SimulateFor(sampleDuration, Vector2.one, true);
            float diagonalSpeed = PlanarDistance(diagonalStart, motor.transform.position) / sampleDuration;

            Assert.That(cardinalSpeed, Is.EqualTo(6f).Within(0.05f));
            Assert.That(diagonalSpeed, Is.EqualTo(6f).Within(0.05f));
            Assert.That(diagonalSpeed, Is.EqualTo(cardinalSpeed).Within(0.02f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Jump_ReachesOneMeterIgnoresAirborneJumpAndLands()
        {
            float baseline = LocalPosition().y;
            float apex = baseline;

            motor.Tick(Vector2.zero, true, SimulationStep);
            Assert.That(motor.VerticalSpeed, Is.GreaterThan(0f), "The grounded jump value must start a jump.");

            for (int step = 0; step < 600; step++)
            {
                apex = Mathf.Max(apex, LocalPosition().y);

                if (step == 8)
                {
                    float speedBeforeAttempt = motor.VerticalSpeed;
                    motor.Tick(Vector2.zero, true, SimulationStep);
                    Assert.That(
                        motor.VerticalSpeed,
                        Is.LessThan(speedBeforeAttempt + 0.001f),
                        "An airborne jump value must not reset upward velocity.");
                }
                else
                {
                    motor.Tick(Vector2.zero, false, SimulationStep);
                }

                if (step > 8 && motor.IsGrounded && motor.VerticalSpeed <= 0f)
                {
                    break;
                }
            }

            Assert.That(apex - baseline, Is.InRange(0.95f, 1.03f));
            Assert.That(motor.IsGrounded, Is.True, "The player must land after the jump.");
            Assert.That(LocalPosition().y, Is.EqualTo(baseline).Within(0.04f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CeilingAndWalls_ContainSustainedMotion()
        {
            motor.JumpHeight = 10f;
            motor.Tick(Vector2.zero, true, SimulationStep);

            float highestCapsuleTop = LocalCapsuleTop();
            for (int step = 0; step < 400; step++)
            {
                motor.Tick(Vector2.zero, false, SimulationStep);
                highestCapsuleTop = Mathf.Max(highestCapsuleTop, LocalCapsuleTop());
                if (highestCapsuleTop > 2f && motor.VerticalSpeed <= 0f)
                {
                    break;
                }
            }

            Assert.That(highestCapsuleTop, Is.InRange(7.80f, 8.03f), "The capsule top should stop at the eight-metre ceiling.");
            Assert.That(motor.VerticalSpeed, Is.LessThanOrEqualTo(0f));

            Vector2Int sealedGridDirection = new[]
                {
                    Vector2Int.up,
                    Vector2Int.right,
                    Vector2Int.down,
                    Vector2Int.left,
                }
                .First(direction => !generator.GridCells.Contains(direction));
            Vector3 sealedLocalDirection = new Vector3(sealedGridDirection.x, 0f, sealedGridDirection.y);
            Assert.That(
                room.HasDoorway(WallFacing(sealedGridDirection)),
                Is.False,
                "The containment portion of this test must target a wall without a generated doorway.");

            yield return ResetPlayer(sealedLocalDirection);

            SimulateFor(1.5f, Vector2.up);
            Vector3 positionAtWall = motor.transform.position;
            SimulateFor(0.5f, Vector2.up);

            Vector3 localPosition = LocalPosition();
            float distanceTowardWall = Vector3.Dot(localPosition, sealedLocalDirection);
            Assert.That(distanceTowardWall, Is.InRange(3.58f, 3.72f));
            Assert.That(PlanarDistance(positionAtWall, motor.transform.position), Is.LessThan(0.01f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConnectedDoorway_AllowsTraversalAndUpdatesCurrentRoom()
        {
            Assert.That(
                generator.TryPlaceRoom(room, CubeRoomWall.East, out CubeRoom destination),
                Is.True,
                "The movement fixture must be able to add its own connected room now that gameplay starts with only the primary room.");
            Physics.SyncTransforms();
            Vector2Int gridDirection = generator.GridCells[1] - generator.GridCells[0];
            Assert.That(Mathf.Abs(gridDirection.x) + Mathf.Abs(gridDirection.y), Is.EqualTo(1));
            Assert.That(room.HasDoorway(WallFacing(gridDirection)), Is.True);
            Assert.That(destination.HasDoorway(WallFacing(-gridDirection)), Is.True);

            Vector3 localForward = new Vector3(gridDirection.x, 0f, gridDirection.y);
            yield return ResetPlayer(localForward);
            Vector3 startingPosition = motor.transform.position;

            // Six metres carries the controller completely through the shared wall while
            // leaving it safely inside the adjacent eight-metre room.
            SimulateFor(2f, Vector2.up);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(
                PlanarDistance(startingPosition, motor.transform.position),
                Is.GreaterThan(5.8f),
                "The centered doorway must admit the 0.6-metre-wide player capsule.");
            Assert.That(
                destination.ContainsWorldPoint(motor.transform.position),
                Is.True,
                $"The player must finish inside the connected room after crossing the doorway. " +
                $"Direction={gridDirection}, start={startingPosition}, end={motor.transform.position}, " +
                $"destination={destination.transform.position}, localEnd={destination.transform.InverseTransformPoint(motor.transform.position)}.");
            Assert.That(
                tracker.CurrentRoom,
                Is.SameAs(destination),
                "Crossing a doorway must transfer gravity/occupancy ownership to the connected room.");
        }

        [UnityTest]
        public IEnumerator RotatedDoorway_ClearsFrameBeforeAlignmentAndOffersPromptOnlyOnElevatedSide()
        {
            RoomOrientation inverted = RoomOrientation.Identity.RotateAroundGridAxis(new Vector3Int(0, 0, 1), 2);
            Assert.That(generator.TryGetPlacementCandidate(room, CubeRoomFace.East, inverted,
                out RoomPlacementCandidate candidate), Is.True);
            Assert.That(generator.TryPlaceRoom(candidate, out CubeRoom destination), Is.True);
            Assert.That(generator.Passages, Has.Count.EqualTo(1));
            RoomPassage passage = generator.Passages[0];
            PlayerRoomTraversal traversal = motor.GetComponent<PlayerRoomTraversal>();
            RoomCreationPromptView prompt = FindInScene<RoomCreationPromptView>();
            PlayerLook look = motor.GetComponent<PlayerLook>();
            Camera camera = motor.GetComponentsInChildren<Camera>(true).First(item => item.CompareTag("MainCamera"));
            Physics.SyncTransforms();

            yield return ResetPlayer(Vector3.right);
            look.CaptureCursor();
            camera.transform.rotation = Quaternion.LookRotation(
                (passage.Aperture.Center - camera.transform.position).normalized, room.RoomUp);
            yield return null;
            Assert.That(traversal.Target, Is.Null, "A floor-level doorway must never become an E target.");
            Assert.That(prompt.TraversalLabel.gameObject.activeSelf, Is.False);

            bool completed = false;
            for (int step = 0; step < 480; step++)
            {
                if (!traversal.IsOwningMovement)
                    motor.Tick(Vector2.up, false, SimulationStep);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return null;
                if (tracker.CurrentRoom == destination && !traversal.IsOwningMovement
                    && !gravityAlignment.IsAligning)
                {
                    completed = true;
                    break;
                }
            }

            Assert.That(completed, Is.True,
                "A rotated floor-level doorway must clear the shared frame, align, and land without wedging the capsule.");
            Assert.That(Vector3.Angle(motor.transform.up, destination.RoomUp), Is.LessThan(1f));
            Assert.That(destination.transform.InverseTransformPoint(motor.transform.position).y,
                Is.EqualTo(0.93f).Within(0.10f));

            camera.transform.rotation = Quaternion.LookRotation(
                (passage.Aperture.Center - camera.transform.position).normalized, destination.RoomUp);
            yield return null;
            Assert.That(traversal.Target, Is.SameAs(passage),
                "The same aperture must be targetable from its destination-elevated side.");
            Assert.That(prompt.TraversalLabel.text, Is.EqualTo(RoomCreationPromptView.TraverseText));
            Assert.That(prompt.TraversalLabel.gameObject.activeSelf, Is.True);

            Vector3 before = motor.transform.position;
            SimulateFor(0.25f, Vector2.up);
            Assert.That((motor.transform.position - before).magnitude, Is.GreaterThan(0.4f),
                "Normal movement must be restored after the automatic transition.");
        }

        [UnityTest]
        public IEnumerator CursorControls_ReleaseAndRecaptureThroughPlayerLook()
        {
            PlayerLook look = motor.GetComponent<PlayerLook>();
            Assert.That(look, Is.Not.Null);

            look.ReleaseCursor();
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(look.IsCursorCaptured, Is.False);

            look.CaptureCursor();
            Assert.That(look.IsCursorCaptured, Is.True);
            if (!Application.isBatchMode)
            {
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            }

            look.ReleaseCursor();
            Assert.That(look.IsCursorCaptured, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GlassOpacityControl_AppliesGlobalRuntimeOverride()
        {
            GlassOpacityControl opacityControl = FindInScene<GlassOpacityControl>();
            Assert.That(opacityControl.MinimumOpacity, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(opacityControl.MaximumOpacity, Is.EqualTo(1f).Within(0.0001f));

            opacityControl.SetOpacity(0.09f);
            yield return null;

            Assert.That(opacityControl.Opacity, Is.EqualTo(0.09f).Within(0.0001f));
            Assert.That(
                Shader.GetGlobalFloat("_WLRGlassOpacityOverride"),
                Is.EqualTo(0.09f).Within(0.0001f));
            Assert.That(
                Shader.GetGlobalFloat("_WLRGlassOpacityOverrideEnabled"),
                Is.EqualTo(1f).Within(0.0001f));
        }

        private IEnumerator ResetPlayer(Vector3? requestedLocalForward = null)
        {
            Vector3 localForward = requestedLocalForward ?? Vector3.forward;
            localForward = Vector3.ProjectOnPlane(localForward, Vector3.up).normalized;
            Vector3 worldForward = room.transform.TransformDirection(localForward);
            Transform movementReference = motor.MovementReference;
            if (movementReference != null && movementReference != motor.transform)
            {
                movementReference.localRotation = Quaternion.identity;
            }

            if (controller != null)
            {
                controller.enabled = false;
            }

            float spawnCenterHeight = capsuleMover != null ? motor.ControllerHeight * 0.5f + 0.03f : 0.06f;
            motor.transform.SetPositionAndRotation(
                room.transform.TransformPoint(new Vector3(0f, spawnCenterHeight, 0f)),
                Quaternion.LookRotation(worldForward, room.RoomUp));
            if (controller != null)
            {
                controller.enabled = true;
            }

            if (gravityAlignment != null)
            {
                gravityAlignment.SnapToUp(room.RoomUp);
            }

            motor.ResetMotion();
            Physics.SyncTransforms();

            for (int step = 0; step < 12 && !motor.IsGrounded; step++)
            {
                motor.Tick(Vector2.zero, false, SimulationStep);
            }

            Assert.That(motor.IsGrounded, Is.True, "Player did not settle onto the room-relative floor.");
            yield return null;
        }

        private void SimulateFor(float duration, Vector2 movement, bool sprint = false)
        {
            int steps = Mathf.RoundToInt(duration / SimulationStep);
            for (int step = 0; step < steps; step++)
            {
                motor.Tick(movement, false, sprint, SimulationStep);
            }
        }

        private Vector3 LocalPosition()
        {
            return room.transform.InverseTransformPoint(motor.transform.position);
        }

        private float LocalCapsuleTop()
        {
            float rootHeight = LocalPosition().y;
            return capsuleMover != null
                ? rootHeight + motor.ControllerHeight * 0.5f
                : rootHeight + motor.ControllerHeight;
        }

        private float PlanarDistance(Vector3 start, Vector3 end)
        {
            return Vector3.ProjectOnPlane(end - start, room.RoomUp).magnitude;
        }

        private static CubeRoomWall WallFacing(Vector2Int direction)
        {
            if (direction == Vector2Int.left)
            {
                return CubeRoomWall.West;
            }

            if (direction == Vector2Int.right)
            {
                return CubeRoomWall.East;
            }

            if (direction == Vector2Int.down)
            {
                return CubeRoomWall.South;
            }

            Assert.That(direction, Is.EqualTo(Vector2Int.up), $"{direction} is not a cardinal room-facing direction.");
            return CubeRoomWall.North;
        }

        private T FindInScene<T>() where T : Component
        {
            return loadedScene
                .GetRootGameObjects()
                .SelectMany(rootObject => rootObject.GetComponentsInChildren<T>(true))
                .Single();
        }
    }
}
