using UnityEngine;
using System.Collections;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;
using UniVox.Implementations.Common;
using Utils.Noise;
using static Utils.Helpers;
using static Utils.Noise.Helpers;
using System;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using Unity.Burst;
using UniVox.Framework.Jobified;
using UnityEngine.Profiling;
using UniVox.Implementations.ProcGen;
using UnityEngine.Assertions;
using System.Net.NetworkInformation;

namespace UniVox.Implementations.Providers
{
    public class NoisyProvider : AbstractProviderComponent<VoxelData>
    {
        [SerializeField] private ChunkDataFactory chunkDataFactory = null;

        [SerializeField] private WorldSettings worldSettings = new WorldSettings();

        [SerializeField] private FractalNoise heightmapNoise = new FractalNoise();
        [SerializeField] private FractalNoise moisturemapNoise = new FractalNoise();

        [SerializeField] private SOVoxelTypeDefinition bedrockType = null;
        private ushort bedrockID;
        [SerializeField] private SOVoxelTypeDefinition waterType = null;
        private ushort waterID;

        [SerializeField] private SOBiomeDefinition oceanBiome = null;
        private OceanGenConfig oceanGenConfig = new OceanGenConfig();

        private int minY = int.MinValue;

        [SerializeField] private BiomeDatabaseComponent biomeDatabaseComponent = null;


        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager)
        {
            base.Initialise(voxelTypeManager, chunkManager);

            Assert.IsNotNull(bedrockType, $"{typeof(NoisyProvider)} must have a valid reference to a bedrock block type");
            Assert.IsNotNull(waterType, $"{typeof(NoisyProvider)} must have a valid reference to a water block type");
            Assert.IsNotNull(oceanBiome, $"{typeof(NoisyProvider)} must have a valid reference to an ocean biome");
            Assert.IsNotNull(biomeDatabaseComponent, $"{typeof(NoisyProvider)} must have a valid reference to a biome database component");

            bedrockID = voxelTypeManager.GetId(bedrockType);
            waterID = voxelTypeManager.GetId(waterType);

            if (chunkManager.IsWorldHeightLimited)
            {
                minY = chunkManager.MinChunkY * chunkManager.ChunkDimensions.y;
            }

            worldSettings.Initialise(minY, chunkManager.ChunkDimensions.ToBurstable());

            biomeDatabaseComponent.Initialise();

            oceanGenConfig.oceanID = biomeDatabaseComponent.GetBiomeID(oceanBiome);
            oceanGenConfig.sealevel = math.floor(math.unlerp(worldSettings.minPossibleHmValue,worldSettings.maxPossibleHmValue, biomeDatabaseComponent.GetMaxElevationFraction(oceanBiome)));
            oceanGenConfig.waterID = waterID;
        }

        public override AbstractPipelineJob<IChunkData<VoxelData>> GenerateChunkDataJob(Vector3Int chunkID,Vector3Int chunkDimensions)
        {           

            var mainGenJob = new JobWrapper<DataGenerationJob>();
            mainGenJob.job = new DataGenerationJob();
            mainGenJob.job.chunkPosition = chunkManager.ChunkToWorldPosition(chunkID);
           
            mainGenJob.job.heightmapNoise = heightmapNoise;
            mainGenJob.job.moisturemapNoise = moisturemapNoise;
            mainGenJob.job.worldSettings = worldSettings;

            mainGenJob.job.bedrockID = bedrockID;

            mainGenJob.job.biomeDatabase = biomeDatabaseComponent.BiomeDatabase;

            mainGenJob.job.heightMap = new NativeArray<int>(chunkDimensions.x * chunkDimensions.y, Allocator.Persistent);
            mainGenJob.job.biomeMap = new NativeArray<int>(chunkDimensions.x * chunkDimensions.y, Allocator.Persistent);

            var arrayLength = chunkDimensions.x * chunkDimensions.y * chunkDimensions.z;

            mainGenJob.job.chunkData = new NativeArray<VoxelData>(arrayLength, Allocator.Persistent);

            //Setup ocean generation job
            var oceanGenJob = new JobWrapper<OceanGenJob>();
            oceanGenJob.job.config = oceanGenConfig;
            oceanGenJob.job.dimensions = worldSettings.ChunkDimensions;
            oceanGenJob.job.chunkPosition = mainGenJob.job.chunkPosition; 
            oceanGenJob.job.chunkData = mainGenJob.job.chunkData;
            oceanGenJob.job.heightMap = mainGenJob.job.heightMap;
            oceanGenJob.job.biomeMap = mainGenJob.job.biomeMap;

            Func<IChunkData<VoxelData>> cleanup = () =>
            {
                Profiler.BeginSample("DataJobCleanup");

                //Pass resulting array to chunk data.
                var ChunkData = chunkDataFactory.Create(chunkID, chunkDimensions, oceanGenJob.job.chunkData.ToArray());

                //Dispose of native arrays
                oceanGenJob.job.chunkData.Dispose();
                oceanGenJob.job.heightMap.Dispose();
                oceanGenJob.job.biomeMap.Dispose();

                Profiler.EndSample();
                return ChunkData;
            };


            if (!Parrallel)
            {
                //Single threaded version  DEBUG
                return new BasicFunctionJob<IChunkData<VoxelData>>(() =>
                {
                    mainGenJob.Run();
                    oceanGenJob.Run();
                    return cleanup();
                });
            }

            var mainHandle = mainGenJob.Schedule();
            var finalHandle = oceanGenJob.Schedule(mainHandle);

            return new PipelineUnityJob<IChunkData<VoxelData>>(finalHandle, cleanup);
        }        

    }

    [System.Serializable]
    public struct OceanGenConfig 
    {
        public int oceanID;
        public float sealevel;
        public ushort waterID;
    }

    [System.Serializable]
    public struct WorldSettings 
    {
        public float HeightmapScale;
        public float MoistureMapScale;
        public float MaxHeightmapHeight;
        public float HeightmapExponentPositive;
        public float HeightmapExponentNegative;        
        public int HeightmapYOffset;
        [NonSerialized] public float MinY;
        public float CaveThreshold;
        public float CaveScale;
        [NonSerialized] public int3 ChunkDimensions;
        [NonSerialized] public float maxPossibleHmValue;
        [NonSerialized] public float minPossibleHmValue;

        public void Initialise(float miny, int3 chunkDimensions) 
        {
            MinY = miny;
            ChunkDimensions = chunkDimensions;

            ///Translate scale variables into the form needed by noise operations,
            /// i.e, invert them
            HeightmapScale = 1 / HeightmapScale;
            MoistureMapScale = 1 / MoistureMapScale;
            CaveScale = 1 / CaveScale;

            maxPossibleHmValue = MaxHeightmapHeight + HeightmapYOffset;
            minPossibleHmValue = -1 * MaxHeightmapHeight + HeightmapYOffset;
        }
    }

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

                    var distanceFromTop = height - (chunkPosition.y+yStart);

                    //top to bottom, skipping all that are above the height (as these are air)
                    for (int y = yStart; y >= 0; y--,distanceFromTop++)
                    {
                        var pos = chunkPosition + new float3(x,y,z);

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
                            //3D noise for caves
                            float caveNoise = noise.snoise(pos * worldSettings.CaveScale);

                            if (caveNoise > worldSettings.CaveThreshold)
                            {
                                //Cave
                                idToPlace = VoxelTypeManager.AIR_ID;
                            }
                        }

                        var flatIndex = MultiIndexToFlat(x, y, z, dx,dxdy);
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
                    biomeMap[i] = biomeDatabase.GetBiomeID(elevationPercentage,moisturePercentage);
                    i++;
                }
            }
        }

        private void ComputeMoistureMap(ref NativeArray<float> moistureMap,int3 dimensions) 
        {
            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    moistureMap[i] = ZeroToOne(
                        moisturemapNoise.Sample(
                            new float2(x + chunkPosition.x, z + chunkPosition.z)*worldSettings.MoistureMapScale                            )
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

    [BurstCompile]
    public struct OceanGenJob :IJob
    {
        [ReadOnly] public OceanGenConfig config;
        [ReadOnly] public int3 dimensions;
        [ReadOnly] public float3 chunkPosition;

        //Input and Output
        public NativeArray<VoxelData> chunkData;

        //Input
        [ReadOnly] public NativeArray<int> heightMap;
        [ReadOnly] public NativeArray<int> biomeMap;

        public void Execute()
        {
            int dx = dimensions.x;
            int dxdy = dimensions.x * dimensions.y;

            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++,i++)
                {
                    if (biomeMap[i] == config.oceanID)
                    {
                        if (heightMap[i] <= config.sealevel)
                        {
                            var yStart = (int)math.floor(math.min(config.sealevel - chunkPosition.y, dimensions.y - 1));

                            //This version is cheaper, but doesn't account for caves
                            //var yEnd = (int)math.floor(math.max(heightMap[i] - chunkPosition.y, 0));
                            //for (int y = yStart; y >= yEnd; y--)
                            //{
                            //    var flatIndex = MultiIndexToFlat(x, y, z, dx, dxdy);
                            //    chunkData[flatIndex] = new VoxelData(config.waterID);
                            //}

                            if (yStart < 0)
                            {
                                continue;
                            }

                            int y = yStart;
                            int flatIndex = MultiIndexToFlat(x, y, z, dx, dxdy);
                            do
                            {
                                chunkData[flatIndex] = new VoxelData(config.waterID);
                                y--;
                                flatIndex = MultiIndexToFlat(x, y, z, dx, dxdy);
                            } while (y>=0 && chunkData[flatIndex].TypeID == VoxelTypeManager.AIR_ID);

                        }
                    }
                }
            }
        }
    }
}