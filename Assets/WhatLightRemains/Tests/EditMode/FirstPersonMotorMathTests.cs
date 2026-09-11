using NUnit.Framework;
using UnityEngine;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class FirstPersonMotorMathTests
    {
        [Test]
        public void StandardGravity_IsExactlyThirtyTwoFeetPerSecondSquared()
        {
            Assert.That(CubeRoom.StandardGravityStrength / 0.3048f, Is.EqualTo(32f).Within(0.0001f));
        }

        [Test]
        public void CalculatePlanarMove_ZeroInputProducesZeroVelocity()
        {
            Vector3 result = FirstPersonMotor.CalculatePlanarMove(
                Vector2.zero,
                Vector3.forward,
                Vector3.right,
                3f);

            Assert.That(result, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void CalculatePlanarMove_DiagonalInputIsNormalized()
        {
            const float speed = 3f;

            Vector3 cardinal = FirstPersonMotor.CalculatePlanarMove(
                Vector2.up,
                Vector3.forward,
                Vector3.right,
                speed);
            Vector3 diagonal = FirstPersonMotor.CalculatePlanarMove(
                Vector2.one,
                Vector3.forward,
                Vector3.right,
                speed);

            Assert.That(cardinal.magnitude, Is.EqualTo(speed).Within(0.0001f));
            Assert.That(diagonal.magnitude, Is.EqualTo(speed).Within(0.0001f));
            Assert.That(diagonal.x, Is.EqualTo(diagonal.z).Within(0.0001f));
        }

        [Test]
        public void CalculatePlanarMove_UsesViewRelativeAxes()
        {
            Vector3 result = FirstPersonMotor.CalculatePlanarMove(
                Vector2.up,
                Vector3.right,
                Vector3.back,
                3f);

            Assert.That(result.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ComputePlanarVelocity_RemainsPerpendicularToRoomUp()
        {
            Vector3 roomUp = Vector3.right;

            Vector3 result = FirstPersonMotor.ComputePlanarVelocity(
                Vector2.one,
                Vector3.forward + Vector3.right,
                roomUp,
                3f);

            Assert.That(Vector3.Dot(result, roomUp), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.magnitude, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void ComputePlanarVelocity_UsesStableFallbackWhenLookingAlongGravityAxis()
        {
            Vector3 result = FirstPersonMotor.ComputePlanarVelocity(
                Vector2.up,
                Vector3.forward,
                Vector3.forward,
                3f);

            Assert.That(result.magnitude, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(Vector3.Dot(result, Vector3.forward), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void CalculateJumpSpeed_ProducesRequestedBallisticHeight()
        {
            const float jumpHeight = 1f;
            const float gravityMagnitude = CubeRoom.StandardGravityStrength;

            float jumpSpeed = FirstPersonMotor.CalculateJumpSpeed(jumpHeight, gravityMagnitude);
            float resultingHeight = jumpSpeed * jumpSpeed / (2f * gravityMagnitude);

            Assert.That(jumpSpeed, Is.EqualTo(Mathf.Sqrt(2f * gravityMagnitude)).Within(0.0001f));
            Assert.That(resultingHeight, Is.EqualTo(jumpHeight).Within(0.0001f));
        }

        [Test]
        public void ComputePlanarVelocity_SprintUsesSixMetersPerSecondWithoutChangingDirection()
        {
            Vector3 walk = FirstPersonMotor.ComputePlanarVelocity(
                Vector2.one,
                Vector3.forward,
                Vector3.up,
                3f);
            Vector3 sprint = FirstPersonMotor.ComputePlanarVelocity(
                Vector2.one,
                Vector3.forward,
                Vector3.up,
                6f);

            Assert.That(walk.magnitude, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(sprint.magnitude, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(Vector3.Dot(walk.normalized, sprint.normalized), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void GrappleCurve_ArcsAcceleratesAndFinishesAtOpeningCenter()
        {
            Vector3 start = new Vector3(0f, 0.93f, 0f);
            Vector3 opening = new Vector3(6f, 5f, 0f);
            Vector3 control = PlayerRoomTraversal.CalculateGrappleControlPoint(
                start, opening, Vector3.up, Vector3.forward, 1.15f);

            Vector3 straightMidpoint = Vector3.Lerp(start, opening, 0.5f);
            Assert.That((control - straightMidpoint).magnitude, Is.GreaterThan(1f));
            Assert.That(PlayerRoomTraversal.EvaluateQuadraticBezier(start, control, opening, 0f), Is.EqualTo(start));
            Assert.That(PlayerRoomTraversal.EvaluateQuadraticBezier(start, control, opening, 1f), Is.EqualTo(opening));

            Vector3 firstHalf = PlayerRoomTraversal.EvaluateQuadraticBezier(start, control, opening, 0.25f);
            Vector3 finish = PlayerRoomTraversal.EvaluateQuadraticBezier(start, control, opening, 1f);
            Assert.That((finish - firstHalf).magnitude, Is.GreaterThan((firstHalf - start).magnitude * 2f),
                "Squaring elapsed time must make the grapple cover substantially more distance late in the pull.");
        }

        [TestCase(1f, 0f, 0f)]
        [TestCase(0f, -1f, 0f)]
        [TestCase(0f, 0f, 1f)]
        public void GravityAlignment_TargetRotationAdoptsRequestedUp(float x, float y, float z)
        {
            Vector3 targetUp = new Vector3(x, y, z);
            Quaternion result = PlayerGravityAlignment.CalculateTargetRotation(
                Quaternion.LookRotation(Vector3.forward, Vector3.up),
                targetUp);

            Assert.That(Vector3.Angle(result * Vector3.up, targetUp), Is.LessThan(0.001f));
            Assert.That(Vector3.Dot(result * Vector3.forward, targetUp), Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
