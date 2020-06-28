﻿using UnityEngine;
using System.Collections;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;
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
using System.Collections.Generic;
using static UniVox.Framework.FrameworkEventManager;
using static UniVox.Implementations.ProcGen.ChunkColumnNoiseMaps;
using Unity.Jobs;
using System.Threading;

namespace UniVox.Implementations.Providers
{
    public class NoisyProvider : AbstractProviderComponent,IDisposable
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

        private Dictionary<Vector2Int, ChunkColumnNoiseMaps> noiseMaps;
        //Noise maps currently being generated
        private Dictionary<Vector2Int, KeyValuePair<JobHandle,NativeChunkColumnNoiseMaps>> noiseMapsPending;
        //Records how many jobs are currently using a pending noise job's result. (Reference counting)
        private Dictionary<Vector2Int, int> usingPending;


        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager, FrameworkEventManager eventManager)
        {
            base.Initialise(voxelTypeManager, chunkManager,eventManager);

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

            noiseMaps = new Dictionary<Vector2Int, ChunkColumnNoiseMaps>();
            noiseMapsPending = new Dictionary<Vector2Int, KeyValuePair<JobHandle, NativeChunkColumnNoiseMaps>>();
            usingPending = new Dictionary<Vector2Int, int>();

            eventManager.OnChunkDeactivated += OnChunkDeactivated;
        }

        public void Dispose() 
        {
            eventManager.OnChunkDeactivated -= OnChunkDeactivated;
        }

        /// <summary>
        /// Remove the stored noise data if a chunk is deactivated for being outside the xz range
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnChunkDeactivated(object sender, ChunkDeactivatedArgs args) 
        {
            if (args.absAmountOutsideRadii.x >0 || args.absAmountOutsideRadii.z > 0)
            {
                noiseMaps.Remove(new Vector2Int(args.chunkID.x, args.chunkID.z));
            }
        }

        public override AbstractPipelineJob<IChunkData> GenerateChunkDataJob(Vector3Int chunkID,Vector3Int chunkDimensions)
        {
            bool waitForNoiseMapGeneration = false;
            bool thisJobIsGeneratingTheNoiseMaps = false;

            var chunkPosition = chunkManager.ChunkToWorldPosition(chunkID);
            NativeChunkColumnNoiseMaps nativeNoiseMaps;
            JobHandle noiseGenHandle = new JobHandle();

            var columnId = new Vector2Int(chunkID.x, chunkID.z);

            Profiler.BeginSample("DealWithNoiseMaps");

            if (noiseMaps.TryGetValue(columnId, out var chunkNoiseMaps))
            {
                //Noise maps have been precomputed
                nativeNoiseMaps = chunkNoiseMaps.ToNative();
            }
            else
            {
                //If another job is already generating the noise maps, wait for it
                if (noiseMapsPending.TryGetValue(columnId,out var pair))
                {
                    noiseGenHandle = pair.Key;
                    nativeNoiseMaps = pair.Value;
                    //Reference counting
                    if (usingPending.TryGetValue(columnId,out var count))
                    {
                        usingPending[columnId] = count + 1;
                    }
                    else
                    {
                        throw new Exception("A noise map job is pending but its reference count does not exist");
                    }
                }
                else
                {
                    //Otherwise, make a job to generate the noise maps
                    var noiseGenJob = new JobWrapper<NoiseMapGenerationJob>();
                    noiseGenJob.job.worldSettings = worldSettings;
                    noiseGenJob.job.biomeDatabase = biomeDatabaseComponent.BiomeDatabase;
                    noiseGenJob.job.chunkPosition = chunkPosition;
                    noiseGenJob.job.heightmapNoise = heightmapNoise;
                    noiseGenJob.job.moisturemapNoise = moisturemapNoise;
                    nativeNoiseMaps = new NativeChunkColumnNoiseMaps(chunkDimensions.x * chunkDimensions.z, Allocator.Persistent);
                    noiseGenJob.job.noiseMaps = nativeNoiseMaps;

                    thisJobIsGeneratingTheNoiseMaps = true;

                    if (!Parrallel)
                    {
                        noiseGenJob.Run();
                    }
                    else 
                    {
                        noiseGenHandle = noiseGenJob.Schedule();
                    }

                    //Add to the pending noise jobs
                    noiseMapsPending.Add(columnId, new KeyValuePair<JobHandle, NativeChunkColumnNoiseMaps>(noiseGenHandle, nativeNoiseMaps));
                    //Reference counting
                    usingPending[columnId] = 1;
                }

                waitForNoiseMapGeneration = true;
            }

            Profiler.EndSample();

            var mainGenJob = new JobWrapper<DataGenerationJob>();
            mainGenJob.job = new DataGenerationJob();
            mainGenJob.job.chunkPosition = chunkPosition;

            mainGenJob.job.worldSettings = worldSettings;

            mainGenJob.job.bedrockID = bedrockID;

            mainGenJob.job.biomeDatabase = biomeDatabaseComponent.BiomeDatabase;
            mainGenJob.job.noiseMaps = nativeNoiseMaps;

            var arrayLength = chunkDimensions.x * chunkDimensions.y * chunkDimensions.z;

            mainGenJob.job.chunkData = new NativeArray<VoxelTypeID>(arrayLength, Allocator.Persistent);

            //Setup ocean generation job
            var oceanGenJob = new JobWrapper<OceanGenJob>();
            oceanGenJob.job.config = oceanGenConfig;
            oceanGenJob.job.dimensions = worldSettings.ChunkDimensions;
            oceanGenJob.job.chunkPosition = mainGenJob.job.chunkPosition; 
            oceanGenJob.job.chunkData = mainGenJob.job.chunkData;
            oceanGenJob.job.heightMap = nativeNoiseMaps.heightMap;
            oceanGenJob.job.biomeMap = nativeNoiseMaps.biomeMap;

            Func<IChunkData> cleanup = () =>
            {
                Profiler.BeginSample("DataJobCleanup");

                if (thisJobIsGeneratingTheNoiseMaps)
                {
                    //Store the noise maps, provided that the chunk id is still in the active range.
                    if (chunkManager.InsideChunkRadius(chunkID,chunkManager.MaximumActiveRadii))
                    {
                        noiseMaps.Add(columnId, new ChunkColumnNoiseMaps(nativeNoiseMaps));
                    }
                    //Remove from pending
                    noiseMapsPending.Remove(columnId);
                }

                //Pass resulting array to chunk data.
                var ChunkData = chunkDataFactory.Create(chunkID, chunkDimensions, oceanGenJob.job.chunkData.ToArray());

                //Dispose of native arrays
                oceanGenJob.job.chunkData.Dispose();

                //Handle reference counting on noise maps
                if (usingPending.TryGetValue(columnId,out var count))
                {
                    count -= 1;
                    if (count > 0)
                    {
                        usingPending[columnId] = count;
                    }
                    else
                    {
                        usingPending.Remove(columnId);
                        //Dispose noise maps
                        nativeNoiseMaps.Dispose();
                    }
                }
                else
                {
                    //Dispose noise maps
                    nativeNoiseMaps.Dispose();
                }

                Profiler.EndSample();
                return ChunkData;
            };


            if (!Parrallel)
            {
                //Single threaded version  DEBUG
                return new BasicFunctionJob<IChunkData>(() =>
                {
                    mainGenJob.Run();
                    oceanGenJob.Run();
                    return cleanup();
                });
            }

            JobHandle finalHandle;

            if (waitForNoiseMapGeneration)
            {
                var mainHandle = mainGenJob.Schedule(noiseGenHandle);
                finalHandle = oceanGenJob.Schedule(mainHandle);
            }
            else
            {
                var mainHandle = mainGenJob.Schedule();
                finalHandle = oceanGenJob.Schedule(mainHandle);
            }


            return new PipelineUnityJob<IChunkData>(finalHandle, cleanup);
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