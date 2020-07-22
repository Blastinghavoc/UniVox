using NSubstitute;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.ComponentModel.Design;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;
using UniVox.Framework.Common;
using UniVox.Framework.Lighting;
using System.Collections.Generic;
using UniVox.Implementations.ChunkData;
using System.Text;

namespace Tests
{
    public class LightManagerTests
    {
        IVoxelTypeManager voxelTypeManager;
        LightManager lightManager;
        VoxelTypeID lampId;
        VoxelTypeID blockerId;
        int maxIntensity;
        Vector3Int chunkDimensions;

        Dictionary<Vector3Int, IChunkData> chunkStorage;

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
                .Returns((args)=> {
                    var typeId = (VoxelTypeID) args[0];
                    if (typeId.Equals(lampId))
                    {
                        return (maxIntensity, maxIntensity);
                    }
                    else if(typeId.Equals(blockerId))
                    {
                        return (0, maxIntensity);
                    }
                    return (0,1);
                });

            lightManager = new LightManager();
            lightManager.Initialise(voxelTypeManager);
        }

        private IChunkData GetMockChunkData(Vector3Int id) 
        {
            if (chunkStorage.TryGetValue(id,out var chunkData))
            {
                return chunkData;
            }
            else
            {
                chunkData = new RLEChunkData(id, chunkDimensions);
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
                        var expectedLv = math.max(maxIntensity - pos.ManhattanMagnitude(),0);
                        Assert.AreEqual(expectedLv, neighbourhood.GetLightValue(x, y, z).Dynamic,
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

                        Assert.AreEqual(expectedLv, neighbourhood.GetLightValue(x, y, z).Dynamic,
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

                        Assert.AreEqual(expectedLv, neighbourhood.GetLightValue(x, y, z).Dynamic,
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

                        Assert.AreEqual(expectedLv, neighbourhood.GetLightValue(x, y, z).Dynamic,
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

                        Assert.AreEqual(expectedLv, neighbourhood.GetLightValue(x, y, z).Dynamic,
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
                        Assert.AreEqual(expectedLv, neighbourhood.GetLightValue(x, y, z).Dynamic,
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
                        Assert.AreEqual(expectedLv, neighbourhood.GetLightValue(x, y, z).Dynamic,
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }
        }

        private void setVoxel(Vector3Int worldPos,VoxelTypeID setTo) 
        {
            var localPos = worldPos;
            var centeredNeigh = neighbourhoodFor(ref localPos);
            var prevVoxel = centeredNeigh.GetVoxel(localPos.x, localPos.y, localPos.z);
            centeredNeigh.SetVoxel(localPos.x, localPos.y, localPos.z, setTo);
            lightManager.UpdateLightOnVoxelSet(centeredNeigh, localPos, setTo, prevVoxel);
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

        private void PrintSlice(ChunkNeighbourhood neighbourhood,int y,bool dynamic = true) 
        {
            StringBuilder sb = new StringBuilder();
            for (int z = maxIntensity; z >= -maxIntensity; z--)
            {
                for (int x = -maxIntensity; x <= maxIntensity; x++)
                {
                    if (dynamic)
                    {
                        sb.Append($"{neighbourhood.GetLightValue(x, y, z).Dynamic},");
                    }
                    else 
                    { 
                        sb.Append($"{neighbourhood.GetLightValue(x, y, z).Dynamic},");                    
                    }
                }
                sb.AppendLine();
            }
            Debug.Log(sb.ToString());
        }
    }
}
