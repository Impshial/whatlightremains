using NUnit.Framework;
using UnityEngine;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class FirstPersonMotorMathTests
    {
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
        public void CalculateJumpSpeed_ProducesRequestedBallisticHeight()
        {
            const float jumpHeight = 1f;
            const float gravityMagnitude = 9.81f;

            float jumpSpeed = FirstPersonMotor.CalculateJumpSpeed(jumpHeight, gravityMagnitude);
            float resultingHeight = jumpSpeed * jumpSpeed / (2f * gravityMagnitude);

            Assert.That(jumpSpeed, Is.EqualTo(Mathf.Sqrt(2f * gravityMagnitude)).Within(0.0001f));
            Assert.That(resultingHeight, Is.EqualTo(jumpHeight).Within(0.0001f));
        }
    }
}
