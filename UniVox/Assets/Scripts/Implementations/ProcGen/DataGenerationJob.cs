using UniVox.Framework;
using UniVox.Implementations.Common;
using Utils.Noise;
using static Utils.Helpers;
using static Utils.Noise.Helpers;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using UniVox.Implementations.Providers;

namespace UniVox.Implementations.ProcGen
{
    [BurstCompile]
    public struct DataGenerationJob : IJob
    {
        [ReadOnly] public WorldSettings worldSettings;
        [ReadOnly] public FractalNoise heightmapNoise;
        [ReadOnly] public FractalNoise moisturemapNoise;
        [ReadOnly] public ushort bedrockID;
        [ReadOnly] public float3 chunkPosition;

        //Output
        public NativeArray<VoxelData> chunkData;

        [ReadOnly] public NativeBiomeDatabase biomeDatabase;

        public NativeArray<int> heightMap;
        public NativeArray<int> biomeMap;

        public void Execute()
        {
            heightmapNoise.Initialise();
            moisturemapNoise.Initialise();

            int3 dimensions = worldSettings.ChunkDimensions;

            ComputeHeightMap(dimensions);
            ComputeBiomeMap(dimensions);

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
                        chunkData[flatIndex] = new VoxelData(idToPlace);
                    }
                }
            }
        }

        private void ComputeBiomeMap(int3 dimensions)
        {
            var maxPossibleHmValue = worldSettings.maxPossibleHmValue;
            var minPossibleHmValue = worldSettings.minPossibleHmValue;

            //Compute moisture map in range 0->1
            NativeArray<float> moistureMap = new NativeArray<float>(dimensions.x * dimensions.y, Allocator.Temp);
            ComputeMoistureMap(ref moistureMap, dimensions);

            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    var elevationPercentage = math.unlerp(minPossibleHmValue, maxPossibleHmValue, heightMap[i]);
                    //Assumes moisture map is already in 0->1 range
                    var moisturePercentage = moistureMap[i];
                    biomeMap[i] = biomeDatabase.GetBiomeID(elevationPercentage, moisturePercentage);
                    i++;
                }
            }
        }

        private void ComputeMoistureMap(ref NativeArray<float> moistureMap, int3 dimensions)
        {
            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    moistureMap[i] = ZeroToOne(
                        moisturemapNoise.Sample(
                            new float2(x + chunkPosition.x, z + chunkPosition.z) * worldSettings.MoistureMapScale)
                        );
                    i++;
                }
            }
        }

        private void ComputeHeightMap(int3 dimensions)
        {
            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    heightMap[i] = CalculateHeightMapAt(new float2(x + chunkPosition.x, z + chunkPosition.z));
                    i++;
                }
            }
        }

        /// <summary>
        /// Changes the distribution of the input noise value (assumed to be in range -1->1)
        /// Range is unchanged.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private float AdjustHeightMapNoiseValue(float val)
        {
            if (val > 0)
            {
                return math.pow(val, worldSettings.HeightmapExponentPositive);
            }
            else
            {
                //Negative exponent
                return -1 * math.pow(-1 * val, worldSettings.HeightmapExponentNegative);
            }
        }

        public int CalculateHeightMapAt(float2 pos)
        {

            int rawHeightmap = (int)math.floor(
                AdjustHeightMapNoiseValue(heightmapNoise.Sample(pos * worldSettings.HeightmapScale))
                * worldSettings.MaxHeightmapHeight
                );

            //add the raw heightmap to the base ground height
            return worldSettings.HeightmapYOffset + rawHeightmap;
        }

    }
}