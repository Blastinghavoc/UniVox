﻿using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Framework.Jobified;
using UniVox.Framework.Serialisation;
using UniVox.Implementations.ChunkData;
using UniVox.Implementations.ProcGen;
using UniVox.MessagePassing;
using Utils;
using Utils.Noise;
using static UniVox.Framework.FrameworkEventManager;
using static UniVox.Implementations.ProcGen.ChunkColumnNoiseMaps;

namespace UniVox.Implementations.Providers
{
    public class NoisyProvider : AbstractProviderComponent, IDisposable
    {
        public ChunkDataFactory chunkDataFactory = null;

        [SerializeField] private WorldSettings worldSettings = new WorldSettings();
        [SerializeField] private TreeSettings treeSettings = new TreeSettings();
        [SerializeField] private FractalNoise treemapNoise = new FractalNoise();

        [SerializeField] private FractalNoise heightmapNoise = new FractalNoise();
        [SerializeField] private FractalNoise moisturemapNoise = new FractalNoise();

        [SerializeField] private SOVoxelTypeDefinition bedrockType = null;
        private ushort bedrockID;
        [SerializeField] private SOVoxelTypeDefinition waterType = null;
        private ushort waterID;

        private OceanGenConfig oceanGenConfig = new OceanGenConfig();

        private int minY = int.MinValue;

        [SerializeField] private BiomeDatabaseComponent biomeDatabaseComponent = null;

        [SerializeField] private StructureGenerator structureGenerator = null;

        [SerializeField] private OreGenerationSettingsComponent oreSettings = null;


        private int3 chunkDimensions;

        //Previously generated noise maps
        private Dictionary<Vector2Int, ChunkColumnNoiseMaps> noiseMaps;
        ///Tracks the number of active chunks in each column so that we know
        ///when to remove a noise map from the storage.
        private Dictionary<Vector2Int, int> numActiveChunksInColumn;
        private JobReferenceCounter<Vector2Int, NativeChunkColumnNoiseMaps> noiseGenReferenceCounter;

        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager, FrameworkEventManager eventManager)
        {
            base.Initialise(voxelTypeManager, chunkManager, eventManager);


            Assert.IsNotNull(bedrockType, $"{typeof(NoisyProvider)} must have a valid reference to a bedrock block type");
            Assert.IsNotNull(waterType, $"{typeof(NoisyProvider)} must have a valid reference to a water block type");
            Assert.IsNotNull(biomeDatabaseComponent, $"{typeof(NoisyProvider)} must have a valid reference to a biome database component");

            if (SceneMessagePasser.TryConsumeMessage<SeedMessage>(out var message))
            {
                worldSettings.seed = GetSeedAsFloat(message.seed);
                Debug.Log($"Int seed: {message.seed}, derived seed: {worldSettings.seed}");
            }
            InitialiseSeeds();

            bedrockID = voxelTypeManager.GetId(bedrockType);
            waterID = voxelTypeManager.GetId(waterType);

            chunkDimensions = chunkManager.ChunkDimensions.ToNative();

            if (chunkManager.WorldLimits.IsWorldHeightLimited)
            {
                minY = chunkManager.WorldLimits.MinChunkY * chunkManager.ChunkDimensions.y;
            }

            worldSettings.Initialise(minY, chunkManager.ChunkDimensions.ToNative());
            treeSettings.Initialise();

            biomeDatabaseComponent.Initialise();

            //Setup ocean config
            try
            {
                var oceanZone = biomeDatabaseComponent.config.elevationLowToHigh.Find((zone) => zone.Name.Equals("Ocean"));
                var oceanBiomes = oceanZone.moistureLevelsLowToHigh.Select((def) => def.biomeDefinition).ToArray();

                oceanGenConfig.oceanIDs = new NativeArray<int>(oceanBiomes.Length, Allocator.Persistent);
                for (int i = 0; i < oceanBiomes.Length; i++)
                {
                    oceanGenConfig.oceanIDs[i] = biomeDatabaseComponent.GetBiomeID(oceanBiomes[i]);
                }
                oceanGenConfig.sealevel = math.floor(math.lerp(worldSettings.minPossibleHmValue, worldSettings.maxPossibleHmValue, biomeDatabaseComponent.GetMaxElevationFraction(oceanBiomes[0])));
                oceanGenConfig.waterID = waterID;
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to find any ocean biomes for biome config {biomeDatabaseComponent.config.name}. Cause {e.Message}");
            }

            noiseMaps = new Dictionary<Vector2Int, ChunkColumnNoiseMaps>();
            numActiveChunksInColumn = new Dictionary<Vector2Int, int>();
            noiseGenReferenceCounter = new JobReferenceCounter<Vector2Int, NativeChunkColumnNoiseMaps>(MakeNoiseJob);

            structureGenerator = new StructureGenerator();
            structureGenerator.Initalise(voxelTypeManager, biomeDatabaseComponent, treeSettings.TreeThreshold, (int)treemapNoise.Seed);

            oreSettings.Initialise(voxelTypeManager);

            eventManager.OnChunkActivated += OnChunkActivated;
            eventManager.OnChunkDeactivated += OnChunkDeactivated;
        }


        public void Dispose()
        {
            eventManager.OnChunkActivated -= OnChunkActivated;
            eventManager.OnChunkDeactivated -= OnChunkDeactivated;
            oreSettings.Dispose();
            biomeDatabaseComponent.Dispose();
            oceanGenConfig.Dispose();
        }

        private void OnChunkActivated(object sender, ChunkActivatedArgs args)
        {
            var columnId = new Vector2Int(args.chunkId.x, args.chunkId.z);
            if (!numActiveChunksInColumn.TryGetValue(columnId, out var count))
            {
                count = 0;
            }
            numActiveChunksInColumn[columnId] = count + 1;
        }

        /// <summary>
        /// Remove the stored noise data if a chunk is deactivated for being outside the xz range
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnChunkDeactivated(object sender, ChunkDeactivatedArgs args)
        {
            var columnId = new Vector2Int(args.chunkID.x, args.chunkID.z);

            if (numActiveChunksInColumn.TryGetValue(columnId, out var count))
            {
                count--;
            }
            else
            {
                count = 0;
            }

            if (count < 1)
            {
                //Remove
                noiseMaps.Remove(columnId);//Delete noise map
                numActiveChunksInColumn.Remove(columnId);//Stop counting for this column

                //TODO remove DEBUG
                var (managerHas, pipelineHas) = chunkManager.ContainsChunkID(args.chunkID);
                Assert.IsTrue((!managerHas && !pipelineHas), $"When removing a noisemap, both the pipeline and the chunk" +
                    $" manager should have removed the corresponding id {args.chunkID}." +
                    $"Manager had it = {managerHas}, pipeline had it = {pipelineHas}");
            }
            else
            {
                //Update stored count
                numActiveChunksInColumn[columnId] = count;
            }
        }
        /// <summary>
        /// Convert integer seed to a float.
        /// Separated as a method, as there's more than one way this could be done.
        /// </summary>
        /// <param name="seed"></param>
        /// <returns></returns>
        private float GetSeedAsFloat(int seed)
        {
            UnityEngine.Random.InitState(seed);
            return UnityEngine.Random.Range(-100000, 100000);
        }

        private void InitialiseSeeds()
        {
            heightmapNoise.Seed = worldSettings.seed;
            moisturemapNoise.Seed = worldSettings.seed;
            treemapNoise.Seed = worldSettings.seed;
        }

        public override AbstractPipelineJob<IChunkData> GenerateTerrainData(Vector3Int chunkID)
        {
            bool noisemapInReferenceCounter = false;
            bool thisJobIsGeneratingTheNoiseMaps = false;

            var chunkPosition = chunkManager.ChunkToWorldPosition(chunkID);
            NativeChunkColumnNoiseMaps nativeNoiseMaps;
            JobHandle noiseGenHandle = default;

            var columnId = new Vector2Int(chunkID.x, chunkID.z);

            Profiler.BeginSample("DealWithNoiseMaps");

            if (noiseMaps.TryGetValue(columnId, out var chunkNoiseMaps))
            {
                //Noise maps have been precomputed
                nativeNoiseMaps = chunkNoiseMaps.ToNative();
            }
            else
            {
                noiseGenReferenceCounter.Add(columnId, out noiseGenHandle, out nativeNoiseMaps, out thisJobIsGeneratingTheNoiseMaps);

                //The noise map is in the reference counter.
                noisemapInReferenceCounter = true;
            }

            Profiler.EndSample();

            var mainGenJob = new JobWrapper<DataGenerationJob>();
            mainGenJob.job = new DataGenerationJob();
            mainGenJob.job.chunkPosition = chunkPosition;

            mainGenJob.job.worldSettings = worldSettings;

            mainGenJob.job.bedrockID = bedrockID;

            mainGenJob.job.biomeDatabase = biomeDatabaseComponent.BiomeDatabase;
            mainGenJob.job.heightMap = nativeNoiseMaps.heightMap;
            mainGenJob.job.biomeMap = nativeNoiseMaps.biomeMap;

            var arrayLength = chunkDimensions.x * chunkDimensions.y * chunkDimensions.z;

            mainGenJob.job.chunkData = new NativeArray<VoxelTypeID>(arrayLength, Allocator.Persistent);

            //Setup ore generation job
            var oreGenJob = new OreGenJob();
            oreGenJob.seed = (uint)heightmapNoise.Seed;
            oreGenJob.chunkData = mainGenJob.job.chunkData;
            oreGenJob.chunkPosition = mainGenJob.job.chunkPosition;
            oreGenJob.dimensions = worldSettings.ChunkDimensions;
            oreGenJob.heightMap = nativeNoiseMaps.heightMap;
            oreGenJob.stoneId = biomeDatabaseComponent.BiomeDatabase.defaultVoxelType;
            oreGenJob.oreSettings = oreSettings.Native;
            oreGenJob.offsets = new NativeArray<float3>(oreSettings.Native.Length, Allocator.Persistent);

            //Setup ocean generation job
            var oceanGenJob = new OceanGenJob();
            oceanGenJob.config = oceanGenConfig;
            oceanGenJob.dimensions = worldSettings.ChunkDimensions;
            oceanGenJob.chunkPosition = mainGenJob.job.chunkPosition;
            oceanGenJob.chunkData = mainGenJob.job.chunkData;
            oceanGenJob.heightMap = nativeNoiseMaps.heightMap;
            oceanGenJob.biomeMap = nativeNoiseMaps.biomeMap;

            //Setup check if empty job
            var checkIfEmptyJob = new CheckIfChunkDataEmptyJob(oceanGenJob.chunkData, Allocator.Persistent);

            Func<IChunkData> cleanup = () =>
            {
                Profiler.BeginSample("DataJobCleanup");

                if (thisJobIsGeneratingTheNoiseMaps)
                {
                    //Store the noise maps, provided that there are still active chunks in the column
                    var numInColumn = numActiveChunksInColumn.TryGetValue(columnId, out var count) ? count : 0;
                    if (numInColumn > 0)
                    {
                        noiseMaps.Add(columnId, new ChunkColumnNoiseMaps(nativeNoiseMaps));
                    }
                }


                IChunkData ChunkData;
                if (checkIfEmptyJob.isEmpty[0])
                {
                    ChunkData = chunkDataFactory.Create(chunkID, chunkDimensions.ToBasic());
                }
                else
                {
                    //Pass data only if not empty
                    ChunkData = chunkDataFactory.Create(chunkID, chunkDimensions.ToBasic(), oceanGenJob.chunkData.ToArray());
                }

                //Dispose of native arrays
                oreGenJob.chunkData.Dispose();
                oreGenJob.offsets.Dispose();
                checkIfEmptyJob.Dispose();

                //Handle reference counting on noise maps
                if (noisemapInReferenceCounter)
                {
                    //This noise map was tracked by the reference counter, check if it can be disposed.
                    if (noiseGenReferenceCounter.Done(columnId))
                    {
                        //Dispose noise maps
                        try
                        {
                            nativeNoiseMaps.Dispose();
                        }
                        catch (Exception e)
                        {
                            throw new Exception($"Error trying to dispose of noise maps for chunk {chunkID}." +
                                $" Was this the job generating the noise maps? {thisJobIsGeneratingTheNoiseMaps}"
                                , e);
                        }
                    }
                }
                else
                {
                    //This job had exclusive ownership of the noise map, and is free to dispose it.
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
                    //Note that when not parallel, the noise gen job will have run by this point
                    mainGenJob.Run();
                    oreGenJob.Run();
                    oceanGenJob.Run();
                    checkIfEmptyJob.Run();
                    return cleanup();
                });
            }

            var mainGenHandle = mainGenJob.Schedule(noiseGenHandle);

            var oreHandle = oreGenJob.Schedule(mainGenHandle);
            var oceanHandle = oceanGenJob.Schedule(oreHandle);
            var checkIfEmptyHandle = checkIfEmptyJob.Schedule(oceanHandle);

            JobHandle finalHandle = checkIfEmptyHandle;
            return new PipelineUnityJob<IChunkData>(finalHandle, cleanup);
        }

        private JobReferenceCounter<Vector2Int, NativeChunkColumnNoiseMaps>.JobResultPair MakeNoiseJob(Vector2Int columnId)
        {
            float3 worldPos = chunkManager.ChunkToWorldPosition(new Vector3Int(columnId.x, 0, columnId.y));

            var noiseGenJob = new NoiseMapGenerationJob();
            noiseGenJob.worldSettings = worldSettings;
            noiseGenJob.treeSettings = treeSettings;
            noiseGenJob.biomeDatabase = biomeDatabaseComponent.BiomeDatabase;
            noiseGenJob.chunkPositionXZ = worldPos.xz;
            noiseGenJob.heightmapNoise = heightmapNoise;
            noiseGenJob.moisturemapNoise = moisturemapNoise;
            noiseGenJob.treemapNoise = treemapNoise;
            var nativeNoiseMaps = new NativeChunkColumnNoiseMaps(chunkDimensions.x * chunkDimensions.z, Allocator.Persistent);
            noiseGenJob.noiseMaps = nativeNoiseMaps;

            JobHandle handle = default;
            if (Parrallel)
            {
                handle = noiseGenJob.Schedule();
            }
            else
            {
                noiseGenJob.Run();
            }

            return new JobReferenceCounter<Vector2Int, NativeChunkColumnNoiseMaps>.JobResultPair() { handle = handle, result = nativeNoiseMaps };
        }

        public override AbstractPipelineJob<ChunkNeighbourhood> GenerateStructuresForNeighbourhood(Vector3Int centerChunkID, ChunkNeighbourhood neighbourhood)
        {

            if (noiseMaps.TryGetValue(new Vector2Int(centerChunkID.x, centerChunkID.z), out var chunkColumnNoise))
            {
                return new BasicFunctionJob<ChunkNeighbourhood>(() =>
                    {
                        return structureGenerator.generateTrees(chunkManager.ChunkToWorldPosition(centerChunkID), chunkManager.ChunkDimensions, neighbourhood, chunkColumnNoise);
                    }
                );

            }
            else
            {
                var (managerHad, pipelinehad) = chunkManager.ContainsChunkID(centerChunkID);
                string minStage = "NA";
                if (pipelinehad)
                {
                    minStage = chunkManager.GetMinPipelineStageOfChunk(centerChunkID).ToString();
                }
                var columnId = new Vector2Int(centerChunkID.x, centerChunkID.z);
                var numInColumn = numActiveChunksInColumn.TryGetValue(columnId, out var count) ? count : 0;
                throw new Exception($"No noisemaps found when trying to generate structures for chunk {centerChunkID}." +
                    $" Did manager contain chunk? {managerHad}. Did pipeline contain chunk? {pipelinehad}." +
                    $" Min pipeline stage of chunk {minStage}." +
                    $" Num in column {numInColumn}");
            }

        }

        public override int[] GetHeightMapForColumn(Vector2Int columnId)
        {
            if (noiseMaps.TryGetValue(columnId, out var columnNoiseMaps))
            {
                return columnNoiseMaps.heightMap;
            }
            else
            {//If the heightmap doesn't exist, guess it to be flat 
                var array = new int[chunkDimensions.x * chunkDimensions.z];
                if (worldSettings.HeightmapYOffset != 0)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        array[i] = worldSettings.HeightmapYOffset;
                    }
                }
                return array;
            }
        }

        protected override IChunkData InitialiseChunkDataFromSaved(ChunkSaveData chunkSaveData, Vector3Int chunkId)
        {
            var data = chunkDataFactory.Create(chunkId, chunkManager.ChunkDimensions, chunkSaveData.voxels);
            data.SetRotationsFromArray(chunkSaveData.rotatedEntries);
            return data;
        }
    }
}
