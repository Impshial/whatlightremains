using NUnit.Framework;
using UnityEngine;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class LadderTraversalTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Ladder_ClimbsAtConfiguredSpeedTransfersRoomAndHonorsDetachCooldown()
        {
            root = new GameObject("Ladder Traversal Test");
            CubeRoom lower = new GameObject("Lower Room").AddComponent<CubeRoom>();
            CubeRoom upper = new GameObject("Upper Room").AddComponent<CubeRoom>();
            lower.transform.SetParent(root.transform);
            upper.transform.SetParent(root.transform);

            GameObject player = new GameObject("Player");
            player.transform.SetParent(root.transform);
            PlayerRoomTracker tracker = player.AddComponent<PlayerRoomTracker>();
            tracker.Initialize(lower);
            PlayerLadderTraversal traversal = player.AddComponent<PlayerLadderTraversal>();
            traversal.Configure(null, null, tracker, player.transform);
            traversal.ClimbSpeed = 2.5f;
            traversal.DetachCooldown = 0.25f;

            CeilingLadder ladder = new GameObject("Ceiling Ladder").AddComponent<CeilingLadder>();
            ladder.transform.SetParent(root.transform);
            ladder.ConfigureWorldPath(
                lower,
                upper,
                Vector3.zero,
                Vector3.up * 8f,
                Vector3.down * 0.2f,
                Vector3.up * 8.2f);

            Assert.That(traversal.Mount(ladder), Is.True);
            Assert.That(tracker.IsRoomAssignmentLocked, Is.True);
            Assert.That(traversal.Tick(1f, false, 0.4f), Is.True);
            Assert.That(player.transform.position.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(traversal.IsAttached, Is.True);

            Assert.That(traversal.Tick(1f, false, 2.8f), Is.True);
            Assert.That(traversal.IsAttached, Is.False);
            Assert.That(player.transform.position, Is.EqualTo(Vector3.up * 8.2f));
            Assert.That(tracker.IsRoomAssignmentLocked, Is.False);
            Assert.That(tracker.CurrentRoom, Is.SameAs(upper));
            Assert.That(traversal.Mount(ladder), Is.False, "The endpoint cooldown must prevent immediate remounting.");

            Assert.That(traversal.Tick(0f, false, 0.25f), Is.False);
            Assert.That(traversal.Mount(ladder), Is.True);
            Assert.That(traversal.Tick(0f, true, 0.01f), Is.True);
            Assert.That(traversal.IsAttached, Is.False, "Space detaches without applying a jump impulse.");
        }
    }
}
