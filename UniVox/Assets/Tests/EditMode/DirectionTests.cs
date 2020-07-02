using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;
using Utils;

namespace Tests
{
    public class DirectionTests
    {

        [SetUp]
        public void Reset()
        {
        }

        [Test]
        public void Opposites() 
        {
            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                Assert.AreEqual((Direction)i, DirectionExtensions.Opposite[(int)DirectionExtensions.Opposite[i]],
                    $"Direction {(Direction)i} opposite of opposite was not itself");
            }
        }

        [Test]
        public void OppositesDiagonal()
        {
            for (int i = 0; i < DiagonalDirectionExtensions.numDirections; i++)
            {
                Assert.AreEqual((DiagonalDirection)i, DiagonalDirectionExtensions.Opposite[(int)DiagonalDirectionExtensions.Opposite[i]],
                    $"Direction {(DiagonalDirection)i} opposite of opposite was not itself");
            }
        }

        [Test]
        public void Vectors()
        {
            var vectorList = new List<Vector3Int>(DirectionExtensions.Vectors);
            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                var vector = vectorList[i];

                Assert.AreNotEqual(Vector3Int.zero, vector, $"Direction {(Direction)i} was zero");

                var oppositeVector = vectorList[(int)DirectionExtensions.Opposite[i]];
                Assert.AreEqual(Vector3Int.zero, vector + oppositeVector, $"Direction {(Direction)i} vector does not cancel out its opposite");
            }
        }

        [Test]
        public void VectorsDiagonal()
        {
            var vectorList = new List<Vector3Int>(DiagonalDirectionExtensions.Vectors);
            for (int i = 0; i < DiagonalDirectionExtensions.numDirections; i++)
            {
                var vector = vectorList[i];

                Assert.AreNotEqual(Vector3Int.zero, vector, $"Direction {(DiagonalDirection)i} was zero");

                var oppositeVector = vectorList[(int)DiagonalDirectionExtensions.Opposite[i]];
                Assert.AreEqual(Vector3Int.zero, vector + oppositeVector, $"Direction {(DiagonalDirection)i} vector does not cancel out its opposite");
            }
        }

        [Test]
        public void CardinalAreSubsetOfDiagonal() 
        {
            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                var cardinal = (Direction)i;
                var diagonal = (DiagonalDirection)i;
                Assert.AreEqual(cardinal.ToString(), diagonal.ToString(),$"Direction {cardinal} had a different string representation when" +
                    $"converted to diagonal: {diagonal}");

                Assert.AreEqual(DirectionExtensions.Vectors[i], DiagonalDirectionExtensions.Vectors[i], $"Direction {cardinal} has " +
                    $"a different vector representation as a diagonal");
            }
        }
    }
}
