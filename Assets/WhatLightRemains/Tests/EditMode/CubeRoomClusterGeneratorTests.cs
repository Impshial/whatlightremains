using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class CubeRoomClusterGeneratorTests
    {
        private static readonly Vector2Int[] AllowedFirstAttachments =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.left,
        };

        [Test]
        public void CreateLayout_FourRoomClusterIsUniqueOrderedAndFaceConnected()
        {
            for (int seed = 0; seed < 64; seed++)
            {
                Vector2Int[] cells = CubeRoomClusterGenerator.CreateLayout(3, seed);

                Assert.That(cells, Has.Length.EqualTo(4), $"Seed {seed} must create one primary and three additional rooms.");
                Assert.That(cells[0], Is.EqualTo(Vector2Int.zero), $"Seed {seed} moved the primary grid cell.");
                Assert.That(
                    AllowedFirstAttachments,
                    Does.Contain(cells[1]),
                    $"Seed {seed}'s first generated room must attach north, east, or west of the primary.");
                Assert.That(
                    new HashSet<Vector2Int>(cells),
                    Has.Count.EqualTo(cells.Length),
                    $"Seed {seed} placed multiple rooms in the same grid cell.");

                for (int roomIndex = 1; roomIndex < cells.Length; roomIndex++)
                {
                    bool attachesToEarlierRoom = cells
                        .Take(roomIndex)
                        .Any(existing => ManhattanDistance(existing, cells[roomIndex]) == 1);
                    Assert.That(
                        attachesToEarlierRoom,
                        Is.True,
                        $"Seed {seed}'s room {roomIndex} is not face-adjacent to the existing cluster.");
                }

                Assert.That(
                    cells.Skip(1).Any(cell => ManhattanDistance(Vector2Int.zero, cell) == 1),
                    Is.True,
                    $"Seed {seed} has no generated room directly attached to the primary.");
            }
        }

        [Test]
        public void CreateLayout_IsDeterministicPerSeedAndVariesAcrossSeeds()
        {
            HashSet<string> distinctLayouts = new HashSet<string>();

            for (int seed = 0; seed < 64; seed++)
            {
                Vector2Int[] first = CubeRoomClusterGenerator.CreateLayout(3, seed);
                Vector2Int[] repeated = CubeRoomClusterGenerator.CreateLayout(3, seed);

                Assert.That(repeated, Is.EqualTo(first), $"Seed {seed} did not reproduce the same ordered layout.");
                distinctLayouts.Add(CanonicalLayoutSignature(first));
            }

            Assert.That(
                distinctLayouts.Count,
                Is.GreaterThan(1),
                "Different seeds must be capable of producing visibly different four-room clusters.");
        }

        private static int ManhattanDistance(Vector2Int first, Vector2Int second)
        {
            Vector2Int delta = first - second;
            return Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        }

        private static string CanonicalLayoutSignature(IEnumerable<Vector2Int> cells)
        {
            return string.Join(
                ";",
                cells
                    .OrderBy(cell => cell.x)
                    .ThenBy(cell => cell.y)
                    .Select(cell => $"{cell.x},{cell.y}"));
        }
    }
}
