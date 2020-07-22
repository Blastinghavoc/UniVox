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

        Dictionary<Vector3Int, IChunkData> chunkStorage;
        HashSet<Vector3Int> fullyGenerated;

        //TODO add mock chunk data for light values

        [SetUp]
        public void Reset()
        {
            maxIntensity = LightValue.IntensityRange - 1;
            chunkDimensions = new Vector3Int(16, 16, 16);
            chunkStorage = new Dictionary<Vector3Int, IChunkData>();

            lampId = (VoxelTypeID)1;
            blockerId = (VoxelTypeID)2;
            voxelTypeManager = Substitute.For<IVoxelTypeManager>();
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

            lightManager = new LightManager();
            lightManager.Initialise(chunkManager, voxelTypeManager);
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

            setVoxel(Vector3Int.zero, lampId);

            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    setVoxel(new Vector3Int(-1, y, z), blockerId);
                }
            }

            PrintSlice(neighbourhood, 0);

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

            PrintSlice(neighbourhood, 0);

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
            lightManager.UpdateLightOnVoxelSet(neighbourhood, Vector3Int.zero, lampId, (VoxelTypeID)VoxelTypeID.AIR_ID);
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
        public void OnChunkGeneratedEmpty()
        {
            var vp = Vector3Int.zero;
            var neighbourhood = neighbourhoodFor(ref vp);
            lightManager.OnChunkFullyGenerated(neighbourhood);

            var expectedLv = new LightValue() { Sun = maxIntensity, Dynamic = 0 };

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

        [Test]
        public void OnChunkGeneratedWithLightNoNeighbours()
        {
            var vp = Vector3Int.zero;
            var neighbourhood = neighbourhoodFor(ref vp);
            //act as if this voxel was generated as part of the chunk
            neighbourhood.SetVoxel(vp.x, vp.y, vp.z, lampId);

            lightManager.OnChunkFullyGenerated(neighbourhood);

            PrintSlice(neighbourhood, 0);

            //Should have propagated the lamp light only within the center chunk
            for (int z = -maxIntensity; z <= maxIntensity; z++)
            {
                for (int y = -maxIntensity; y <= maxIntensity; y++)
                {
                    for (int x = -maxIntensity; x <= maxIntensity; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedLv = math.max(maxIntensity - pos.ManhattanMagnitude(), 0);

                        if (pos.x < 0 || pos.x >= chunkDimensions.x ||
                            pos.y < 0 || pos.y >= chunkDimensions.y ||
                            pos.z < 0 || pos.z >= chunkDimensions.z
                            )
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
        public void OnChunkGeneratedWithLightInNeighbour()
        {
            var vp = Vector3Int.zero;
            var neighbourhood = neighbourhoodFor(ref vp);
            //act as if this voxel was generated as part of the chunk
            neighbourhood.SetVoxel(vp.x, vp.y, vp.z, lampId);

            lightManager.OnChunkFullyGenerated(neighbourhood);
            fullyGenerated.Add(neighbourhood.center.ChunkID);

            //generate another chunk next to this one, the light should spill in
            vp = new Vector3Int(-1, 0, 0);
            lightManager.OnChunkFullyGenerated(neighbourhoodFor(ref vp));
            var tstChunkId = new Vector3Int(-1, 0, 0);

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
                        var expectedLv = math.max(maxIntensity - pos.ManhattanMagnitude(), 0);

                        if (!insideChunkId(pos,Vector3Int.zero) && !insideChunkId(pos,tstChunkId))
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
        public void OnChunkGeneratedWithLightWithNeighbourFullyGenerated()
        {
            var vp = Vector3Int.zero;
            var neighbourhood = neighbourhoodFor(ref vp);
            //act as if this voxel was generated as part of the chunk
            neighbourhood.SetVoxel(vp.x, vp.y, vp.z, lampId);

            //generate another chunk next to this one, the light should spill in from the zero chunk
            vp = new Vector3Int(-1, 0, 0);
            lightManager.OnChunkFullyGenerated(neighbourhoodFor(ref vp));
            var tstChunkId = new Vector3Int(-1, 0, 0);
            fullyGenerated.Add(tstChunkId);

            //run the generation action for the zero chunk
            lightManager.OnChunkFullyGenerated(neighbourhood);
            fullyGenerated.Add(neighbourhood.center.ChunkID);

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
                        var expectedLv = math.max(maxIntensity - pos.ManhattanMagnitude(), 0);

                        if (!insideChunkId(pos, Vector3Int.zero) && !insideChunkId(pos, tstChunkId))
                        {
                            expectedLv = 0;
                        }

                        Assert.AreEqual(expectedLv, neighbourhood.GetLight(x, y, z).Dynamic,
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }
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
            var chunkLB = chunkId * chunkDimensions;
            var chunkUB = (chunkId + Vector3Int.one) * chunkDimensions;
            return pos.All((a, b) => a >= b, chunkLB) && pos.All((a, b) => a < b, chunkUB);
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
                        sb.Append($"{neighbourhood.GetLight(x, y, z).Dynamic},");
                    }
                }
                sb.AppendLine();
            }
            Debug.Log(sb.ToString());
        }
    }
}
