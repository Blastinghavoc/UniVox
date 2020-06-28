using UniVox.Framework;
using static Utils.Helpers;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using static UniVox.Implementations.ProcGen.ChunkColumnNoiseMaps;

namespace UniVox.Implementations.ProcGen
{
    [BurstCompile]
    public struct DataGenerationJob : IJob
    {
        [ReadOnly] public WorldSettings worldSettings;        
        [ReadOnly] public ushort bedrockID;
        [ReadOnly] public float3 chunkPosition;

        //Output
        public NativeArray<VoxelTypeID> chunkData;

        [ReadOnly] public NativeBiomeDatabase biomeDatabase;

        [ReadOnly] public NativeArray<int> heightMap;
        [ReadOnly] public NativeArray<int> biomeMap;

        public void Execute()
        {            
            int3 dimensions = worldSettings.ChunkDimensions;            

            var mapDimensions = new int2(dimensions.x, dimensions.z);

            int dx = dimensions.x;
            int dxdy = dimensions.x * dimensions.y;

            //int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    //Process one column of voxels
                    var mapIndex = MultiIndexToFlat(x, z, mapDimensions);
                    var layers = biomeDatabase.biomeLayers[biomeMap[mapIndex]];

                    var height = heightMap[mapIndex];

                    var currentLayerIndex = layers.start;
                    var currentLayer = biomeDatabase.allLayers[currentLayerIndex];

                    var yStart = (int)math.floor(math.min(height - chunkPosition.y, dimensions.y - 1));

                    //Voxel to place according to the current layer
                    var layerVoxel = currentLayer.voxelID;

                    //Accumulated depth from all layers
                    var totalLayerDepth = currentLayer.depth;

                    //Indicates whether there are still layers remainging to process, or if we are using the default voxel type instead
                    bool stillHaveLayers = true;

                    var distanceFromTop = height - (chunkPosition.y + yStart);

                    //top to bottom, skipping all that are above the height (as these are air)
                    for (int y = yStart; y >= 0; y--, distanceFromTop++)
                    {
                        var pos = chunkPosition + new float3(x, y, z);

                        //Get next layer while necessary
                        while (stillHaveLayers && distanceFromTop >= totalLayerDepth)
                        {
                            currentLayerIndex++;
                            if (currentLayerIndex == layers.end)
                            {
                                //If out of layers, switch to default voxel type
                                layerVoxel = biomeDatabase.defaultVoxelId;
                                stillHaveLayers = false;
                                break;
                            }
                            else
                            {
                                //Go to next layer
                                totalLayerDepth += currentLayer.depth;
                                currentLayer = biomeDatabase.allLayers[currentLayerIndex];
                                layerVoxel = currentLayer.voxelID;
                            }
                        }

                        var idToPlace = layerVoxel;

                        //handle bedrock and caves
                        if (pos.y <= worldSettings.MinY)
                        {
                            idToPlace = bedrockID;
                        }
                        else
                        {
                            if (worldSettings.MakeCaves)
                            {
                                //3D noise for caves
                                float caveNoise = noise.snoise(pos * worldSettings.CaveScale);

                                if (caveNoise > worldSettings.CaveThreshold)
                                {
                                    //Cave
                                    idToPlace = VoxelTypeManager.AIR_ID;
                                }
                            }
                        }

                        var flatIndex = MultiIndexToFlat(x, y, z, dx, dxdy);
                        chunkData[flatIndex] = new VoxelTypeID(idToPlace);
                    }
                }
            }
        }       

    }
}