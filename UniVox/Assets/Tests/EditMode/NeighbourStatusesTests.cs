using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;
using UniVox.Framework.ChunkPipeline.WaitForNeighbours;
using UniVox.Framework.Common;

namespace Tests
{
    public class NeighbourStatusesTests
    {

        [SetUp]
        public void Reset()
        {
        }

        [Test]
        public void Cardinal() 
        {
            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                NeighbourStatus neighbourStatus = new NeighbourStatus();
                Assert.IsFalse(neighbourStatus.AllValid, $"Status was considered valid after construction");
                //Add all neighbours except one
                for (int j = 0; j < DirectionExtensions.numDirections; j++)
                {
                    if (i==j)
                    {
                        continue;
                    }
                    neighbourStatus.AddNeighbour(j);
                }
                Assert.IsFalse(neighbourStatus.AllValid,$"Status was considered valid when it was missing direction {(Direction)i}." +
                    $" Status: {neighbourStatus}");
                neighbourStatus.AddNeighbour(i);
                Assert.IsTrue(neighbourStatus.AllValid, $"Status was considered invalid when it had all directions." +
                    $" Status: {neighbourStatus}");
                neighbourStatus.RemoveNeighbour(i);
                Assert.IsFalse(neighbourStatus.AllValid, $"Status was considered valid after having direction {(Direction)i} removed." +
                    $" Status: {neighbourStatus}");
            }
        }

        [Test]
        public void Diagonal()
        {
            for (int i = 0; i < DiagonalDirectionExtensions.numDirections; i++)
            {
                DiagonalNeighbourStatus neighbourStatus = new DiagonalNeighbourStatus();
                Assert.IsFalse(neighbourStatus.AllValid, $"Status was considered valid after construction");
                //Add all neighbours except one
                for (int j = 0; j < DiagonalDirectionExtensions.numDirections; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }
                    neighbourStatus.AddNeighbour(j);
                }
                Assert.IsFalse(neighbourStatus.AllValid, $"Status was considered valid when it was missing direction {(DiagonalDirection)i}." +
                    $" Status: {neighbourStatus}");
                neighbourStatus.AddNeighbour(i);
                Assert.IsTrue(neighbourStatus.AllValid, $"Status was considered invalid when it had all directions." +
                    $" Status: {neighbourStatus}");
                neighbourStatus.RemoveNeighbour(i);
                Assert.IsFalse(neighbourStatus.AllValid, $"Status was considered valid after having direction {(DiagonalDirection)i} removed." +
                    $" Status: {neighbourStatus}");
            }
        }

        [Test]
        public void AddAndRemoveAllCardinal() 
        {
            NeighbourStatus neighbourStatus = new NeighbourStatus();
            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                neighbourStatus.AddNeighbour(i);
            }
            Assert.IsTrue(neighbourStatus.AllValid);
            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                neighbourStatus.RemoveNeighbour(i);
            }
            Assert.IsFalse(neighbourStatus.AllValid);            
        }

        [Test]
        public void AddAndRemoveAllDiagonal()
        {
            DiagonalNeighbourStatus neighbourStatus = new DiagonalNeighbourStatus();
            for (int i = 0; i < DiagonalDirectionExtensions.numDirections; i++)
            {
                neighbourStatus.AddNeighbour(i);
            }
            Assert.IsTrue(neighbourStatus.AllValid);
            for (int i = 0; i < DiagonalDirectionExtensions.numDirections; i++)
            {
                neighbourStatus.RemoveNeighbour(i);
            }
            Assert.IsFalse(neighbourStatus.AllValid);
        }
    }
}
