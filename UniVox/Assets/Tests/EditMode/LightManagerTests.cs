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
        int lampIntensity;
        Vector3Int chunkDimensions;

        Dictionary<Vector3Int, IChunkData> chunkStorage;

        //TODO add mock chunk data for light values

        [SetUp]
        public void Reset()
        {
            lampIntensity = LightValue.IntensityRange - 1;
            chunkDimensions = new Vector3Int(16, 16, 16);
            chunkStorage = new Dictionary<Vector3Int, IChunkData>();

            lampId = (VoxelTypeID)1337;
            voxelTypeManager = Substitute.For<IVoxelTypeManager>();
            voxelTypeManager.GetLightEmission(Arg.Any<VoxelTypeID>())
                .Returns((args)=> {
                    var typeId = (VoxelTypeID) args[0];
                    if (typeId.Equals(lampId))
                    {
                        return lampIntensity;
                    }
                    return 0;
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
        public void PropagateDynamicOnLightSourcePlaced() 
        {
            ChunkNeighbourhood neighbourhood = new ChunkNeighbourhood(Vector3Int.zero, GetMockChunkData);
            lightManager.UpdateLightOnVoxelSet(neighbourhood, Vector3Int.zero, lampId, (VoxelTypeID)VoxelTypeID.AIR_ID);

            //PrintSlice(neighbourhood, 0);
           
            for (int z = -lampIntensity; z <= lampIntensity; z++)
            {
                for (int y = -lampIntensity; y <= lampIntensity; y++)
                {
                    for (int x = -lampIntensity; x <= lampIntensity; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedLv = math.max(lampIntensity - pos.ManhattanMagnitude(),0);
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

            for (int z = -lampIntensity; z <= lampIntensity; z++)
            {
                for (int y = -lampIntensity; y <= lampIntensity; y++)
                {
                    for (int x = -lampIntensity; x <= lampIntensity; x++)
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
            for (int z = -lampIntensity; z <= lampIntensity; z++)
            {
                for (int y = -lampIntensity; y <= lampIntensity; y++)
                {
                    for (int x = -lampIntensity; x <= lampIntensity; x++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        var expectedLv = math.max(lampIntensity - pos.ManhattanMagnitude(), 0);
                        Assert.AreEqual(expectedLv, neighbourhood.GetLightValue(x, y, z).Dynamic,
                            $"Light value not as expected for position {x},{y},{z}");
                    }
                }
            }
        }

        private void PrintSlice(ChunkNeighbourhood neighbourhood,int y,bool dynamic = true) 
        {
            StringBuilder sb = new StringBuilder();
            for (int z = lampIntensity; z >= -lampIntensity; z--)
            {
                for (int x = -lampIntensity; x <= lampIntensity; x++)
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
