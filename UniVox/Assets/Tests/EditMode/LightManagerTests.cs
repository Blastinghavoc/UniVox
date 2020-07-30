using NSubstitute;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;
using UniVox.Framework.Lighting;
using UniVox.Implementations.ChunkData;

namespace Tests
{
    public class LightManagerTests
    {
        IVoxelTypeManager voxelTypeManager;
        IChunkManager chunkManager;
        LightManager lightManager;
        VoxelTypeID lampId;
        VoxelTypeID blockerId;
        int maxIntensity;
        Vector3Int chunkDimensions;
        int heightMapYValue = 0;

        Dictionary<Vector3Int, IChunkData> chunkStorage;
        HashSet<Vector3Int> fullyGenerated;

        [SetUp]
        public void Reset()
        {
            maxIntensity = LightValue.MaxIntensity;
            heightMapYValue = 0;
            chunkDimensions = new Vector3Int(16, 16, 16);
            chunkStorage = new Dictionary<Vector3Int, IChunkData>();

            lampId = (VoxelTypeID)1;
            blockerId = (VoxelTypeID)2;
            voxelTypeManager = Substitute.For<IVoxelTypeManager>();
            voxelTypeManager.LastVoxelID.Returns(blockerId);
            voxelTypeManager.GetLightProperties(Arg.Any<VoxelTypeID>())
                .Returns((args) =>
                {
                    var typeId = (VoxelTypeID)args[0];
                    if (typeId.Equals(lampId))
                    {
                        return (maxIntensity, maxIntensity);
                    }
                    else if (typeId.Equals(blockerId))
                    {
                        return (0, maxIntensity);
                    }
                    return (0, 1);
                });

            fullyGenerated = new HashSet<Vector3Int>();
            chunkManager = Substitute.For<IChunkManager>();
            chunkManager.IsChunkFullyGenerated(Arg.Any<Vector3Int>())
                .Returns((args) =>
                {
                    var id = (Vector3Int)args[0];
                    return fullyGenerated.Contains(id);
                });

            chunkManager.ChunkToWorldPosition(Arg.Any<Vector3Int>())
                .Returns((args) =>
                {
                    var chunkId = (Vector3Int)args[0];
                    return chunkId * chunkDimensions;
                });

            chunkManager.GetReadOnlyChunkData(Arg.Any<Vector3Int>())
                .Returns((args) =>
                {
                    var chunkId = (Vector3Int)args[0];
                    return new RestrictedChunkData(GetMockChunkData(chunkId));
                });

            chunkManager.GetChunkData(Arg.Any<Vector3Int>())
                .Returns((args) =>
                {
                    var chunkId = (Vector3Int)args[0];
                    return GetMockChunkData(chunkId);
                });

            chunkManager.ChunkDimensions.Returns(chunkDimensions);

            IHeightMapProvider heightMapProvider = Substitute.For<IHeightMapProvider>();
            heightMapProvider.GetHeightMapForColumn(Arg.Any<Vector2Int>())
                .Returns((args) =>
                {
                    return getFlatHeightMap(heightMapYValue);
                });

            lightManager = new LightManager();
            lightManager.Initialise(voxelTypeManager, chunkManager, heightMapProvider);
        }

        [TearDown]
        public void Cleanup()
        {
            lightManager.Dispose();
        }

        private IChunkData GetMockChunkData(Vector3Int id)
        {
            if (chunkStorage.TryGetValue(id, out var chunkData))
            {
                return chunkData;
            }
            else
            {
                chunkData = new FlatArrayChunkData(id, chunkDimensions);
                chunkStorage.Add(id, chunkData);
                return chunkData;
            }
        }

        [Test]
        public void PlaceLampNoBarrier()
        {
            ChunkNeighbourhood neighbourhood = new ChunkNeighbourhood(Vector3Int.zero, GetMockChunkData);

            FakeGenerationOfNeighbours(neighbourhood.center.ChunkID);

            lightManager.UpdateLightOnVoxelSet(neighbourhood, Vector3Int.zero, lampId, (VoxelTypeID)VoxelTypeID.AIR_ID);

            //PrintSlice(neighbourhood, 0);

            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    for (int x = -maxIntensity; x <= maxIntensity; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedLv = math.max(maxIntensity - pos.ManhattanMagnitude(), 0);
                        Assert.AreEqual(expectedLv, neighbourhood.GetLight(x, y, z).Dynamic,
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }
        }

        [Test]
        public void PlaceBarrierThenLamp()
        {
            ChunkNeighbourhood neighbourhood = new ChunkNeighbourhood(Vector3Int.zero, GetMockChunkData);

            FakeGenerationOfNeighbours(neighbourhood.center.ChunkID);

            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    setVoxel(new Vector3Int(-1, y, z), blockerId);
                }
            }

            //Create lamp
            setVoxel(Vector3Int.zero, lampId);

            //PrintSlice(neighbourhood, 0);

            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    for (int x = -maxIntensity; x <= maxIntensity; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedLv = math.max(maxIntensity - pos.ManhattanMagnitude(), 0);

                        if (x <= -1)
                        {
                            expectedLv = 0;
                        }

                        Assert.AreEqual(expectedLv, neighbourhood.GetLight(x, y, z).Dynamic,
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }
        }

        [Test]
        public void PlaceLampThenBarrier()
        {
            ChunkNeighbourhood neighbourhood = new ChunkNeighbourhood(Vector3Int.zero, GetMockChunkData);

            FakeGenerationOfNeighbours(neighbourhood.center.ChunkID);

            setVoxel(Vector3Int.zero, lampId);

            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    setVoxel(new Vector3Int(-1, y, z), blockerId);
                }
            }

            //PrintSlice(neighbourhood, 0);

            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    for (int x = -maxIntensity; x <= maxIntensity; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedLv = math.max(maxIntensity - pos.ManhattanMagnitude(), 0);

                        if (x <= -1)
                        {
                            expectedLv = 0;
                        }

                        Assert.AreEqual(expectedLv, neighbourhood.GetLight(x, y, z).Dynamic,
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }
        }

        [Test]
        public void PlaceBarrierThenLampThenRemoveBarrier()
        {
            ChunkNeighbourhood neighbourhood = new ChunkNeighbourhood(Vector3Int.zero, GetMockChunkData);

            FakeGenerationOfNeighbours(neighbourhood.center.ChunkID);

            //Create barrier before lamp
            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    setVoxel(new Vector3Int(-1, y, z), blockerId);
                }
            }

            //create lamp
            lightManager.UpdateLightOnVoxelSet(neighbourhood, Vector3Int.zero, lampId, (VoxelTypeID)VoxelTypeID.AIR_ID);

            //Assert light blocked correctly
            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    for (int x = -maxIntensity; x <= maxIntensity; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedLv = math.max(maxIntensity - pos.ManhattanMagnitude(), 0);

                        if (x <= -1)
                        {
                            expectedLv = 0;
                        }

                        Assert.AreEqual(expectedLv, neighbourhood.GetLight(x, y, z).Dynamic,
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }

            //Remove barrier
            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    setVoxel(new Vector3Int(-1, y, z), (VoxelTypeID)VoxelTypeID.AIR_ID);
                }
            }

            //PrintSlice(neighbourhood, 0);

            //Assert light no longer blocked
            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    for (int x = -maxIntensity; x <= maxIntensity; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedLv = math.max(maxIntensity - pos.ManhattanMagnitude(), 0);

                        Assert.AreEqual(expectedLv, neighbourhood.GetLight(x, y, z).Dynamic,
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }

        }

        [Test]
        public void PlaceAndRemoveDynamicLight()
        {
            ChunkNeighbourhood neighbourhood = new ChunkNeighbourhood(Vector3Int.zero, GetMockChunkData);

            FakeGenerationOfNeighbours(neighbourhood.center.ChunkID);

            //Place
            lightManager.UpdateLightOnVoxelSet(neighbourhood, Vector3Int.zero, lampId, (VoxelTypeID)VoxelTypeID.AIR_ID);
            //Remove
            lightManager.UpdateLightOnVoxelSet(neighbourhood, Vector3Int.zero, (VoxelTypeID)VoxelTypeID.AIR_ID, lampId);

            //PrintSlice(neighbourhood,0);

            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    for (int x = -maxIntensity; x <= maxIntensity; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedLv = 0;
                        Assert.AreEqual(expectedLv, neighbourhood.GetLight(x, y, z).Dynamic,
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }
        }

        [Test]
        public void PlaceAndRemoveDynamicLightNextToEachOther()
        {
            ChunkNeighbourhood neighbourhood = new ChunkNeighbourhood(Vector3Int.zero, GetMockChunkData);

            Vector3Int testPos = new Vector3Int(5, 0, 0);

            FakeGenerationOfNeighbours(neighbourhood.center.ChunkID);

            //place two lights
            lightManager.UpdateLightOnVoxelSet(neighbourhood, Vector3Int.zero, lampId, (VoxelTypeID)VoxelTypeID.AIR_ID);
            lightManager.UpdateLightOnVoxelSet(neighbourhood, testPos, lampId, (VoxelTypeID)VoxelTypeID.AIR_ID);

            //PrintSlice(neighbourhood,0);

            //remove the second one
            lightManager.UpdateLightOnVoxelSet(neighbourhood, testPos, (VoxelTypeID)VoxelTypeID.AIR_ID, lampId);

            //PrintSlice(neighbourhood,0);

            //The remaining light should be that of the first light only
            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    for (int x = -maxIntensity; x <= maxIntensity; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedLv = math.max(maxIntensity - pos.ManhattanMagnitude(), 0);
                        Assert.AreEqual(expectedLv, neighbourhood.GetLight(x, y, z).Dynamic,
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }
        }

        [Test]
        [TestCase(1000, TestName = "NoSun")]
        [TestCase(-1000, TestName = "Sun")]
        public void OnChunkGeneratedEmpty(int hm)
        {
            var vp = Vector3Int.zero;
            var neighbourhood = neighbourhoodFor(ref vp);

            heightMapYValue = hm;
            RunLightingGeneration(neighbourhood.center.ChunkID);


            var expectedLv = new LightValue() { Sun = (hm < 0) ? maxIntensity : 0, Dynamic = 0 };

            for (int z = 0; z < chunkDimensions.z; z++)
            {
                for (int y = 0; y < chunkDimensions.y; y++)
                {
                    for (int x = 0; x < chunkDimensions.x; x++)
                    {

                        Assert.AreEqual(expectedLv, neighbourhood.GetLight(x, y, z),
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }
        }

        [Test(Description = "Asserts that a chunk generated under the heightmap level will have no sunlight")]
        public void OnChunkGeneratedUnderground()
        {
            var chunkId = Vector3Int.down;

            var expectedLv = new LightValue() { Sun = 0, Dynamic = 0 };

            //Generate chunk
            var neighbourhood = new ChunkNeighbourhood(chunkId, GetMockChunkData);
            //Ground level is at 0, so the chunk will think it has no sunlight
            heightMapYValue = 0;
            RunLightingGeneration(neighbourhood.center.ChunkID);

            //PrintSlice(neighbourhood, 0,false);

            var chunkData = GetMockChunkData(chunkId);

            for (int z = 0; z < chunkDimensions.z; z++)
            {
                for (int y = 0; y < chunkDimensions.y; y++)
                {
                    for (int x = 0; x < chunkDimensions.x; x++)
                    {
                        Assert.AreEqual(expectedLv, chunkData.GetLight(x, y, z),
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }
        }

        [Test(Description = "Tests whether sunlight is correctly propagated into chunks, irrespective of" +
            " the order they were generated in, for a pair of chunks")]
        [TestCase(true, TestName = "TopToBottom")]
        [TestCase(false, TestName = "BottomToTop")]
        public void OnChunksGeneratedSunlight(bool topToBottom)
        {
            int first = 0;
            int second = 1;
            if (!topToBottom)
            {
                Utils.Helpers.Swap(ref first, ref second);
            }

            Vector3Int[] ids = new Vector3Int[] {
                Vector3Int.zero,
                Vector3Int.down
            };

            var upChunkData = GetMockChunkData(ids[0]);
            var lowChunkData = GetMockChunkData(ids[1]);

            var expectedLv = new LightValue() { Sun = 0, Dynamic = 0 };

            //Ground level is at 0, so the lower chunk will at first think it has no sunlight
            heightMapYValue = 0;

            //Generate first chunk
            RunLightingGeneration(ids[first]);
            lightManager.Update();

            //Second chunk should not have sunlight yet
            var secondChunkData = GetMockChunkData(ids[second]);
            for (int z = 0; z < chunkDimensions.z; z++)
            {
                for (int y = 0; y < chunkDimensions.y; y++)
                {
                    for (int x = 0; x < chunkDimensions.x; x++)
                    {
                        Assert.AreEqual(expectedLv, secondChunkData.GetLight(x, y, z),
                            $"Light value not as expected for position {x},{y},{z} in second chunk");
                    }
                }
            }

            //Generate second chunk
            RunLightingGeneration(ids[second]);
            lightManager.Update();

            var bottomNeigh = new ChunkNeighbourhood(lowChunkData, GetMockChunkData);
            //Debug.Log("TopSlice");
            //PrintSlice(bottomNeigh, 15,false);
            //Debug.Log("BottomSlice");
            //PrintSlice(bottomNeigh, 0, false);

            expectedLv = new LightValue() { Sun = maxIntensity, Dynamic = 0 };

            for (int z = 0; z < chunkDimensions.z; z++)
            {
                for (int y = 0; y < chunkDimensions.y; y++)
                {
                    for (int x = 0; x < chunkDimensions.x; x++)
                    {
                        Assert.AreEqual(expectedLv, upChunkData.GetLight(x, y, z),
                            $"Light value not as expected for position {x},{y},{z} in upper chunk");
                        Assert.AreEqual(expectedLv, lowChunkData.GetLight(x, y, z),
                            $"Light value not as expected for position {x},{y},{z} in lower chunk");
                    }
                }
            }
        }

        [Test(Description = "Tests whether sunlight is correctly propagated into chunks, irrespective of" +
            " the order they were generated in, for a column of three chunks")]
        [TestCaseSource("ChunkOrdersTriple")]
        public void OnChunksGeneratedSunlightTriple(int[] order)
        {

            Vector3Int[] ids = new Vector3Int[] {
                Vector3Int.zero,
                Vector3Int.down,
                Vector3Int.down * 2
            };

            IChunkData[] chunkDatas = new IChunkData[]
            {
                GetMockChunkData(ids[0]),
                GetMockChunkData(ids[1]),
                GetMockChunkData(ids[2]),
            };

            //Ground level is at 0, so the lower chunk will at first think it has no sunlight
            heightMapYValue = 0;

            foreach (var index in order)
            {
                RunLightingGeneration(ids[index]);
                lightManager.Update();
            }

            //var bottomNeigh = new ChunkNeighbourhood(lowChunkData, GetMockChunkData);
            //Debug.Log("TopSlice");
            //PrintSlice(bottomNeigh, 15,false);
            //Debug.Log("BottomSlice");
            //PrintSlice(bottomNeigh, 0, false);

            var expectedLv = new LightValue() { Sun = maxIntensity, Dynamic = 0 };

            for (int z = 0; z < chunkDimensions.z; z++)
            {
                for (int y = 0; y < chunkDimensions.y; y++)
                {
                    for (int x = 0; x < chunkDimensions.x; x++)
                    {
                        for (int i = 0; i < ids.Length; i++)
                        {
                            Assert.AreEqual(expectedLv, chunkDatas[i].GetLight(x, y, z),
                                $"Light value not as expected for position {x},{y},{z} in chunk {i}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Regression test for a race condition that was found and fixed on day 60
        /// </summary>
        [Test]
        public void MultiplePropagationsIntoSameChunkInOneUpdate()
        {
            lightManager.Parallel = true;//Parallel execution

            //Ground level is at 0, so the lower chunk will at first think it has no sunlight
            heightMapYValue = 0;
            Vector3Int testChunkId = Vector3Int.down;
            var upId = testChunkId + Vector3Int.up;
            var adjacentId = testChunkId + Vector3Int.left;

            var neighbourhood = new ChunkNeighbourhood(testChunkId, GetMockChunkData);
            //The lamp in the adjacent chunk should cause a propagation update in the test chunk
            var lampPos = new Vector3Int(-1, 0, 0);
            neighbourhood.SetVoxel(lampPos.x,lampPos.y,lampPos.z,lampId);

            //generate test chunk
            RunLightingGeneration(testChunkId);
            lightManager.Update();

            //generate up chunk, then adjacent chunk.
            RunLightingGeneration(upId);            
            RunLightingGeneration(adjacentId);
            lightManager.Update();//Run propagations in the same update

            ///This order could result in no sunlight in the test chunk if race conditions are present,
            ///we are testing that there isn't a race condition.

            var expectedSunlight = LightValue.MaxIntensity;

            var testChunkData = GetMockChunkData(testChunkId);

            for (int z = 0; z < chunkDimensions.z; z++)
            {
                for (int y = 0; y < chunkDimensions.y; y++)
                {
                    for (int x = 0; x < chunkDimensions.x; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedDynamic = math.max(maxIntensity - (pos - lampPos).ManhattanMagnitude(), 0);
                        var expectedLv = new LightValue() { Dynamic = expectedDynamic, Sun = expectedSunlight };

                        Assert.AreEqual(expectedLv, testChunkData.GetLight(x, y, z),
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }

        }

        private static IEnumerable<TestCaseData> ChunkOrdersTriple()
        {
            yield return new TestCaseData(new int[] { 0, 1, 2 }).SetName("TopToBottom");
            yield return new TestCaseData(new int[] { 2, 1, 0 }).SetName("BottomToTop");
            yield return new TestCaseData(new int[] { 0, 2, 1 }).SetName("TopBottomMiddle");
            yield return new TestCaseData(new int[] { 1, 0, 2 }).SetName("MiddleTopBottom");
            yield return new TestCaseData(new int[] { 1, 2, 0 }).SetName("MiddleBottomTop");
            yield return new TestCaseData(new int[] { 2, 0, 1 }).SetName("BottomTopMiddle");
        }

        [Test]
        [TestCase(1000, TestName = "NoSun")]
        [TestCase(-1000, TestName = "Sun")]
        public void OnChunkGeneratedWithLightNoNeighbours(int hm)
        {
            var vp = Vector3Int.zero;
            var lampPos = vp;
            var neighbourhood = neighbourhoodFor(ref vp);
            //act as if this voxel was generated as part of the chunk
            neighbourhood.SetVoxel(vp.x, vp.y, vp.z, lampId);

            heightMapYValue = hm;
            RunLightingGeneration(neighbourhood.center.ChunkID);

            //PrintSlice(neighbourhood, 0);

            //Should have propagated the lamp light only within the center chunk
            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    for (int x = -maxIntensity; x <= maxIntensity; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedLv = new LightValue() { Sun = (hm < 0) ? maxIntensity : 0 };
                        expectedLv.Dynamic = math.max(maxIntensity - pos.ManhattanMagnitude(), 0);

                        if (!insideChunkId(pos, Vector3Int.zero))
                        {
                            expectedLv.Dynamic = 0;
                            expectedLv.Sun = 0;
                        }

                        if (pos.Equals(lampPos))
                        {//This is where the lamp is, lamps are opaque to sunlight
                            expectedLv.Sun = 0;
                        }

                        Assert.AreEqual(expectedLv, neighbourhood.GetLight(x, y, z),
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }
        }

        [Test]
        [TestCase(1000, TestName = "NoSun")]
        [TestCase(-1000, TestName = "Sun")]
        public void OnChunkGeneratedWithLightInNeighbour(int hm)
        {
            var vp = Vector3Int.zero;
            var lampPos = vp;
            var neighbourhood = neighbourhoodFor(ref vp);
            //act as if this voxel was generated as part of the chunk
            neighbourhood.SetVoxel(vp.x, vp.y, vp.z, lampId);

            heightMapYValue = hm;
            RunLightingGeneration(neighbourhood.center.ChunkID);

            //generate another chunk next to this one, the light should spill in
            vp = new Vector3Int(-1, 0, 0);
            RunLightingGeneration(vp);
            var tstChunkId = new Vector3Int(-1, 0, 0);

            //PrintSlice(neighbourhood, 0);
            //PrintSlice(neighbourhood, 0,false);

            ///Should have propagated the lamp light only within the center chunk
            ///and the newly added chunk
            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    for (int x = -maxIntensity; x <= maxIntensity; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedLv = new LightValue() { Sun = (hm < 0) ? maxIntensity : 0 };
                        expectedLv.Dynamic = math.max(maxIntensity - pos.ManhattanMagnitude(), 0);

                        if (!insideChunkId(pos, Vector3Int.zero) && !insideChunkId(pos, tstChunkId))
                        {
                            expectedLv.Dynamic = 0;
                            expectedLv.Sun = 0;
                        }

                        if (pos.Equals(lampPos))
                        {//This is where the lamp is, lamps are opaque to sunlight
                            expectedLv.Sun = 0;
                        }

                        Assert.AreEqual(expectedLv, neighbourhood.GetLight(x, y, z),
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }
        }

        [Test]
        [TestCase(1000, TestName = "NoSun")]
        [TestCase(-1000, TestName = "Sun")]
        public void OnChunkGeneratedWithLightWithNeighbourFullyGenerated(int hm)
        {
            var vp = Vector3Int.zero;
            var lampPos = vp;
            var neighbourhood = neighbourhoodFor(ref vp);
            //act as if this voxel was generated as part of the chunk
            neighbourhood.SetVoxel(vp.x, vp.y, vp.z, lampId);

            heightMapYValue = hm;

            //generate another chunk next to this one, the light should spill in from the zero chunk
            var tstChunkId = new Vector3Int(-1, 0, 0);
            RunLightingGeneration(tstChunkId);
            lightManager.Update();

            //run the generation action for the zero chunk
            RunLightingGeneration(neighbourhood.center.ChunkID);
            lightManager.Update();

            //PrintSlice(neighbourhood, 0);

            ///Should have propagated the lamp light only within the center chunk
            ///and the newly added chunk
            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    for (int x = -maxIntensity; x <= maxIntensity; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedLv = new LightValue() { Sun = (hm < 0) ? maxIntensity : 0 };
                        expectedLv.Dynamic = math.max(maxIntensity - pos.ManhattanMagnitude(), 0);

                        if (!insideChunkId(pos, Vector3Int.zero) && !insideChunkId(pos, tstChunkId))
                        {
                            expectedLv.Dynamic = 0;
                            expectedLv.Sun = 0;
                        }

                        if (pos.Equals(lampPos))
                        {//This is where the lamp is, lamps are opaque to sunlight
                            expectedLv.Sun = 0;
                        }

                        Assert.AreEqual(expectedLv, neighbourhood.GetLight(x, y, z),
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }
        }

        private void RunLightingGeneration(Vector3Int chunkId)
        {
            var job = lightManager.CreateGenerationJob(chunkId);
            if (lightManager.Parallel)
            {
                while (!job.Done)
                {
                    //busy wait
                }
            }
            else
            {
                Assert.IsTrue(job.Done, "Job not done after creation");
            }
            lightManager.ApplyGenerationResult(chunkId, job.Result);
            fullyGenerated.Add(chunkId);
        }

        private int[] getFlatHeightMap(int yValue)
        {
            int[] hm = new int[chunkDimensions.x * chunkDimensions.z];
            for (int i = 0; i < hm.Length; i++)
            {
                hm[i] = yValue;
            }
            return hm;
        }

        private void setVoxel(Vector3Int worldPos, VoxelTypeID setTo)
        {
            var localPos = worldPos;
            var centeredNeigh = neighbourhoodFor(ref localPos);
            var prevVoxel = centeredNeigh.GetVoxel(localPos.x, localPos.y, localPos.z);
            centeredNeigh.SetVoxel(localPos.x, localPos.y, localPos.z, setTo);
            lightManager.UpdateLightOnVoxelSet(centeredNeigh, localPos, setTo, prevVoxel);
        }

        private bool insideChunkId(Vector3Int pos, Vector3Int chunkId)
        {
            return Utils.Helpers.IsInsideChunkId(pos, chunkId, chunkDimensions);
        }

        /// <summary>
        /// Can reuse the same neighbourhood if operating on positions within
        /// the same chunk, otherwise the light manager expects a neighbourhood
        /// centered on the chunk containing the modified voxel.
        /// Adjusts voxel pos to be relative to the center chunk.
        /// </summary>
        /// <param name="voxelPos"></param>
        /// <returns></returns>
        private ChunkNeighbourhood neighbourhoodFor(ref Vector3Int voxelPos)
        {
            //ChunkId is elementwise integer division by the Chunk dimensions
            var chunkId = voxelPos.ElementWise((a, b) => Mathf.FloorToInt(a / (float)b), chunkDimensions);

            var remainder = voxelPos.ElementWise((a, b) => a % b, chunkDimensions);
            //Local voxel index is the remainder, with negatives adjusted
            voxelPos = remainder.ElementWise((a, b) => a < 0 ? b + a : a, chunkDimensions);

            return new ChunkNeighbourhood(chunkId, GetMockChunkData);
        }

        private void FakeGenerationOfNeighbours(Vector3Int chunkId)
        {
            //Act as if all nearby chunks are fully generated
            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        fullyGenerated.Add(new Vector3Int(x, y, z) + chunkId);
                    }
                }
            }
        }

        private void PrintSlice(ChunkNeighbourhood neighbourhood, int y, bool dynamic = true)
        {
            StringBuilder sb = new StringBuilder();
            for (int z = maxIntensity; z >= -maxIntensity; z--)
            {
                for (int x = -maxIntensity; x <= maxIntensity; x++)
                {
                    if (dynamic)
                    {
                        sb.Append($"{neighbourhood.GetLight(x, y, z).Dynamic},");
                    }
                    else
                    {
                        sb.Append($"{neighbourhood.GetLight(x, y, z).Sun},");
                    }
                }
                sb.AppendLine();
            }
            Debug.Log(sb.ToString());
        }
    }
}
