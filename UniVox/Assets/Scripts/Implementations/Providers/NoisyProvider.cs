using UnityEngine;
using System.Collections;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;
using UniVox.Implementations.Common;
using Utils.Noise;
using System;
using Unity.Mathematics;
using Unity.Collections;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
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
        public bool MakeCaves;
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
}