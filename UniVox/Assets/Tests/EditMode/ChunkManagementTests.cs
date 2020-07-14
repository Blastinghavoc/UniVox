using NSubstitute;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UniVox.Framework;
using UniVox.Framework.ChunkPipeline;
using UniVox.Framework.PlayAreaManagement;
using static Utils.Helpers;

namespace Tests
{
    public class ChunkManagementTests
    {
        IChunkManager mockManager;
        IChunkPipeline mockPipeline;
        IVoxelPlayer player;
        Dictionary<Vector3Int, int> statusMap;

        [SetUp]
        public void Reset()
        {
        }

        private void SetupMocks(Vector3Int chunkDimensions,WorldSizeLimits worldSizeLimits = null)
        {
            if (worldSizeLimits == null)
            {
                worldSizeLimits = new WorldSizeLimits(false,0);
                worldSizeLimits.Initalise();
            }


            mockManager = Substitute.For<IChunkManager>();
            mockManager.GenerateStructures.Returns(true);
            mockManager.WorldToChunkPosition(Arg.Any<Vector3>()).Returns(args => WorldToChunkPos((Vector3)args[0], chunkDimensions));

            mockManager.WorldLimits.Returns(worldSizeLimits);

            mockPipeline = Substitute.For<IChunkPipeline>();
            mockPipeline.TerrainDataStage.Returns(0);
            mockPipeline.OwnStructuresStage.Returns(1);
            mockPipeline.FullyGeneratedStage.Returns(2);
            mockPipeline.RenderedStage.Returns(3);
            mockPipeline.CompleteStage.Returns(4);

            statusMap = new Dictionary<Vector3Int, int>();

            //Mock set target to write to the status map instead
            mockManager
                .When(_ => _.SetTargetStageOfChunk(Arg.Any<Vector3Int>(), Arg.Any<int>()))
                .Do(args =>
                {
                    Vector3Int pos = (Vector3Int)args[0];
                    int newStatus = (int)args[1];
                    statusMap[pos] = newStatus;
                });

            //Mock deactivate to remove from the status map
            mockManager
                .When(_ => _.TryDeactivateChunk(Arg.Any<Vector3Int>()))
                .Do(args =>
                {
                    Vector3Int pos = (Vector3Int)args[0];
                    statusMap.Remove(pos);
                });

            player = Substitute.For<IVoxelPlayer>();
            player.Position = Vector3.zero;
        }

        [Test]
        public void CuboidalAreaFromZero()
        {
            Vector3Int center = new Vector3Int(0, 0, 0);
            Vector3Int radii = new Vector3Int(2, 2, 2);

            List<Vector3Int> definitive = GenerateDefinitive(center, radii);

            List<Vector3Int> fromGenerator = CuboidalArea(center, radii).ToList();

            Assert.AreEqual(definitive.Count, fromGenerator.Count, $"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (Vector3Int item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        [Test]
        public void CuboidalAreaFromNonZero()
        {
            Vector3Int center = new Vector3Int(16, 12, -5);
            Vector3Int radii = new Vector3Int(2, 2, 2);

            List<Vector3Int> definitive = GenerateDefinitive(center, radii);

            List<Vector3Int> fromGenerator = CuboidalArea(center, radii).ToList();

            Assert.AreEqual(definitive.Count, fromGenerator.Count, $"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (Vector3Int item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        [Test]
        public void CuboidalAreaWithStartRadii()
        {
            Vector3Int center = new Vector3Int(0, 0, 0);
            Vector3Int radii = new Vector3Int(2, 2, 2);
            Vector3Int startRadii = new Vector3Int(1, 1, 1);

            List<Vector3Int> definitive = GenerateDefinitive(center, radii, startRadii);

            List<Vector3Int> fromGenerator = CuboidalArea(center, radii, startRadii).ToList();

            foreach (Vector3Int item in definitive)
            {
                Assert.IsTrue(fromGenerator.Contains(item), $"Definitive produced {item} which was not in generated list");
            }

            Assert.AreEqual(definitive.Count, fromGenerator.Count, $"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (Vector3Int item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        [Test]
        public void CuboidalAreaFromNonZeroWithNoValid()
        {
            Vector3Int center = new Vector3Int(16, 12, -5);
            Vector3Int radii = new Vector3Int(2, 2, 2);
            Vector3Int startRadii = new Vector3Int(3, 3, 3);//Start is greater than end

            List<Vector3Int> definitive = GenerateDefinitive(center, radii, startRadii);

            List<Vector3Int> fromGenerator = CuboidalArea(center, radii, startRadii).ToList();

            Assert.AreEqual(0, fromGenerator.Count);

            Assert.AreEqual(definitive.Count, fromGenerator.Count, $"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (Vector3Int item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        [Test]
        public void CuboidalAreaWithStartEqualsEndRadii()
        {
            Vector3Int center = new Vector3Int(16, 12, -5);
            Vector3Int radii = new Vector3Int(5, 5, 5);
            Vector3Int startRadii = radii;

            List<Vector3Int> definitive = GenerateDefinitive(center, radii, startRadii);

            List<Vector3Int> fromGenerator = CuboidalArea(center, radii, startRadii).ToList();

            Assert.AreEqual(definitive.Count, fromGenerator.Count, $"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (Vector3Int item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        [Test]
        public void CuboidalAreaNonUniformEndRadii()
        {
            Vector3Int center = new Vector3Int(0, 0, 0);
            Vector3Int radii = new Vector3Int(2, 5, 2);
            Vector3Int startRadii = new Vector3Int(1, 1, 1);

            List<Vector3Int> definitive = GenerateDefinitive(center, radii, startRadii);

            List<Vector3Int> fromGenerator = CuboidalArea(center, radii, startRadii).ToList();

            foreach (Vector3Int item in definitive)
            {
                Assert.IsTrue(fromGenerator.Contains(item), $"Definitive produced {item} which was not in generated list");
            }

            Assert.AreEqual(definitive.Count, fromGenerator.Count, $"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (Vector3Int item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        [Test]
        public void CuboidalAreaNonUniformStartAndEndRadii()
        {
            Vector3Int center = new Vector3Int(0, 0, 0);
            Vector3Int radii = new Vector3Int(2, 5, 2);
            Vector3Int startRadii = new Vector3Int(1, 0, 1);

            List<Vector3Int> definitive = GenerateDefinitive(center, radii, startRadii);

            List<Vector3Int> fromGenerator = CuboidalArea(center, radii, startRadii).ToList();

            foreach (Vector3Int item in definitive)
            {
                Assert.IsTrue(fromGenerator.Contains(item), $"Definitive produced {item} which was not in generated list");
            }

            Assert.AreEqual(definitive.Count, fromGenerator.Count, $"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (Vector3Int item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        [Test]
        public void PlayAreaInitialise()
        {
            Vector3Int chunkDimensions = new Vector3Int(16, 16, 16);
            Vector3Int collidableRadii = new Vector3Int(1, 1, 1);
            Vector3Int renderedRadii = new Vector3Int(4, 4, 4);

            //Set up the mocks
            SetupMocks(chunkDimensions);

            PlayAreaManager playArea = new PlayAreaManager(collidableRadii, renderedRadii);

            //Initialise play area
            playArea.Initialise(mockManager, mockPipeline,player);

            Vector3Int MaxActiveRadii = renderedRadii + new Vector3Int(3, 3, 3);
            //Ensure we've got the max active radii correct
            Assert.AreEqual(MaxActiveRadii, playArea.MaximumActiveRadii);

            Vector3Int PlayerChunkId = playArea.playerChunkID;
            //Ensure player chunk id correct
            Assert.AreEqual(Vector3Int.zero, PlayerChunkId);

            //After initialise, check all positions have the expected target.
            AssertStatusMapCorrect(MaxActiveRadii, PlayerChunkId, playArea);


        }

        [Test]
        [TestCase(0, 0, 0)]
        [TestCase(0, 0, 1)]
        [TestCase(0, 1, 0)]
        [TestCase(0, 1, 1)]
        [TestCase(1, 0, 0)]
        [TestCase(1, 0, 1)]
        [TestCase(1, 1, 0)]
        [TestCase(1, 1, 1)]
        public void PlayAreaUpdate(int xDisp, int yDisp, int zDisp)
        {
            Vector3Int displacement = new Vector3Int(xDisp, yDisp, zDisp);

            Vector3Int chunkDimensions = new Vector3Int(16, 16, 16);

            Vector3Int collidableRadii = new Vector3Int(1, 1, 1);
            Vector3Int renderedRadii = new Vector3Int(2, 2, 2);

            //Set up the mocks
            SetupMocks(chunkDimensions);

            PlayAreaManager playArea = new PlayAreaManager(collidableRadii, renderedRadii);
            playArea.UpdateRate = ushort.MaxValue;//Unlimited update rate for testing

            //Initialise play area
            playArea.Initialise(mockManager, mockPipeline,player);
            Vector3Int MaxActiveRadii = playArea.MaximumActiveRadii;
            Vector3Int originalPlayerChunkId = playArea.playerChunkID;

            player.Position += displacement * chunkDimensions;
            Debug.Log("PreUpdate");
            printStageMap2DSlice(MaxActiveRadii, 0, originalPlayerChunkId);
            playArea.Update();

            Vector3Int playerChunkIdAfterUpdate = playArea.playerChunkID;
            //Ensure player chunk updated correctly
            Assert.AreEqual(originalPlayerChunkId + displacement, playerChunkIdAfterUpdate,
                $"Player chunk id was incorrect");

            Debug.Log("PostUpdate");
            //TODO remove DEBUG
            printStageMap2DSlice(MaxActiveRadii, 0, playerChunkIdAfterUpdate);
            //for (int i = -MaxActiveRadii.y; i <= MaxActiveRadii.y; i++)
            //{
            //    printStageMap2DSlice(MaxActiveRadii, i,playerChunkIdAfterUpdate);
            //}

            //Check play area is correct
            AssertStatusMapCorrect(MaxActiveRadii, playerChunkIdAfterUpdate, playArea);
        }

        private void AssertStatusMapCorrect(Vector3Int MaxActiveRadii, Vector3Int PlayerChunkId, PlayAreaManager playArea)
        {
            HashSet<Vector3Int> keysTmp = new HashSet<Vector3Int>(statusMap.Keys);

            for (int x = -MaxActiveRadii.x; x <= MaxActiveRadii.x; x++)
            {
                for (int y = -MaxActiveRadii.y; y <= MaxActiveRadii.y; y++)
                {
                    for (int z = -MaxActiveRadii.z; z <= MaxActiveRadii.z; z++)
                    {
                        Vector3Int displacement = new Vector3Int(x, y, z);
                        Vector3Int chunkId = displacement + PlayerChunkId;

                        Assert.IsTrue(statusMap.ContainsKey(chunkId), $"Chunk ID {chunkId} with displacement {displacement} was not in the status map");

                        if (InsideRadius(displacement, playArea.CollidableChunksRadii))
                        {
                            Assert.AreEqual(mockPipeline.CompleteStage, statusMap[chunkId],
                                $"Chunk ID {chunkId} with displacement {displacement} not correct");
                        }
                        else if (InsideRadius(displacement, playArea.RenderedChunksRadii))
                        {
                            Assert.AreEqual(mockPipeline.RenderedStage, statusMap[chunkId],
                                 $"Chunk ID {chunkId} with displacement {displacement} not correct");
                        }
                        else if (InsideRadius(displacement, playArea.FullyGeneratedRadii))
                        {
                            Assert.AreEqual(mockPipeline.FullyGeneratedStage, statusMap[chunkId],
                                 $"Chunk ID {chunkId} with displacement {displacement} not correct");
                        }
                        else if (InsideRadius(displacement, playArea.StructureChunksRadii))
                        {
                            Assert.AreEqual(mockPipeline.OwnStructuresStage, statusMap[chunkId],
                                 $"Chunk ID {chunkId} with displacement {displacement} not correct");
                        }
                        else
                        {
                            Assert.AreEqual(mockPipeline.TerrainDataStage, statusMap[chunkId],
                                 $"Chunk ID {chunkId} with displacement {displacement} not correct");
                        }
                        keysTmp.Remove(chunkId);
                    }
                }
            }

            Assert.AreEqual(0, keysTmp.Count, $"Status map contained more than the maximum active area");
        }

        private void printStageMap2DSlice(Vector3Int MaxActiveRadii, int y, Vector3Int playerChunkID)
        {
            StringBuilder sb = new StringBuilder();
            for (int z = MaxActiveRadii.z; z >= -MaxActiveRadii.z; z--)
            {
                for (int x = -MaxActiveRadii.x; x <= MaxActiveRadii.x; x++)
                {
                    Vector3Int chunkId = new Vector3Int(x, y, z) + playerChunkID;

                    string rep;

                    if (statusMap.TryGetValue(chunkId, out int val))
                    {
                        rep = val.ToString();
                    }
                    else
                    {
                        rep = "_";
                    }

                    sb.Append(rep);
                    sb.Append(",");
                }
                sb.AppendLine();
            }
            Debug.Log(sb.ToString());
        }

        List<Vector3Int> GenerateDefinitive(Vector3Int center, Vector3Int radii, Vector3Int startRadii = default)
        {
            Vector3Int maxRadii = radii + Vector3Int.one;
            List<Vector3Int> definitive = new List<Vector3Int>();

            Vector3Int excludeRadii = startRadii - Vector3Int.one;

            for (int x = -maxRadii.x; x <= maxRadii.x; x++)
            {
                for (int y = -maxRadii.y; y <= maxRadii.y; y++)
                {
                    for (int z = -maxRadii.z; z <= maxRadii.z; z++)
                    {
                        Vector3Int displacement = new Vector3Int(x, y, z);
                        if (InsideRadius(displacement, radii))
                        {
                            if (!InsideRadius(displacement, excludeRadii))
                            {
                                //Only add elements when they are not inside the excluded radii
                                definitive.Add(displacement + center);
                            }
                        }
                    }
                }
            }

            return definitive;
        }

        Vector3Int WorldToChunkPos(Vector3 pos, Vector3Int ChunkDimensions)
        {
            Vector3Int floor = new Vector3Int();
            floor.x = Mathf.FloorToInt(pos.x);
            floor.y = Mathf.FloorToInt(pos.y);
            floor.z = Mathf.FloorToInt(pos.z);

            //Result is elementwise integer division by the Chunk dimensions
            Vector3Int result = floor.ElementWise((a, b) => Mathf.FloorToInt(a / (float)b), ChunkDimensions);
            return result;
        }

        bool InsideRadius(Vector3Int displacement, Vector3Int Radii)
        {
            Vector3Int absDisplacement = displacement.ElementWise(Mathf.Abs);

            //Inside if all elements of the absolute displacement are less than or equal to the radius
            return absDisplacement.All((a, b) => a <= b, Radii);
        }
    }

}
