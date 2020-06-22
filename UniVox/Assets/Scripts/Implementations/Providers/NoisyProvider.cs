using UnityEngine;
using System.Collections;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;
using UniVox.Implementations.Common;
using Utils.Noise;
using System;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using Unity.Burst;
using UniVox.Framework.Jobified;
using UnityEngine.Profiling;
using UniVox.Implementations.ProcGen;

namespace UniVox.Implementations.Providers
{
    public class NoisyProvider : AbstractProviderComponent<VoxelData>
    {
        [SerializeField] private ChunkDataFactory chunkDataFactory = null;

        [SerializeField] private WorldSettings worldSettings = new WorldSettings();

        [SerializeField] private NoiseSettings noiseSettings = new NoiseSettings();

        [SerializeField] private SOVoxelTypeDefinition dirtType = null;
        private ushort dirtID;
        [SerializeField] private SOVoxelTypeDefinition grassType = null;
        private ushort grassID;
        [SerializeField] private SOVoxelTypeDefinition stoneType = null;
        private ushort stoneID;

        [SerializeField] private SOVoxelTypeDefinition bedrockType = null;
        private ushort bedrockID;

        private FastNoise fastNoise;

        private int minY = int.MinValue;

        private BiomeDatabaseComponent biomeDatabaseComponent;

        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager)
        {
            base.Initialise(voxelTypeManager, chunkManager);
            dirtID = voxelTypeManager.GetId(dirtType);
            grassID = voxelTypeManager.GetId(grassType);
            stoneID = voxelTypeManager.GetId(stoneType);
            bedrockID = voxelTypeManager.GetId(bedrockType);

            fastNoise = new FastNoise(noiseSettings.Seed);
            fastNoise.SetFractalLacunarity(noiseSettings.Lacunarity);
            fastNoise.SetFractalGain(noiseSettings.Persistence);
            fastNoise.SetFractalOctaves(noiseSettings.Octaves);

            if (chunkManager.IsWorldHeightLimited)
            {
                minY = chunkManager.MinChunkY * chunkManager.ChunkDimensions.y;
            }
            worldSettings.ChunkDimensions = chunkManager.ChunkDimensions.ToBurstable();
            worldSettings.MinY = minY;

            biomeDatabaseComponent = FindObjectOfType<BiomeDatabaseComponent>();
        }

        public override AbstractPipelineJob<IChunkData<VoxelData>> GenerateChunkDataJob(Vector3Int chunkID,Vector3Int chunkDimensions)
        {
            if (!Burst)
            {
                return new BasicFunctionJob<IChunkData<VoxelData>>(()=>GenerateChunkData(chunkID,chunkDimensions));
            }

            var jobWrapper = new JobWrapper<DataGenerationJob>();
            jobWrapper.job = new DataGenerationJob();
            jobWrapper.job.chunkPosition = chunkManager.ChunkToWorldPosition(chunkID);
            jobWrapper.job.ids = new BlockIDs()
            {
                dirt = dirtID,
                grass = grassID,
                stone = stoneID,
                bedrock = bedrockID
            };
            jobWrapper.job.noiseSettings = noiseSettings;
            jobWrapper.job.worldSettings = worldSettings;

            jobWrapper.job.biomeDatabase = biomeDatabaseComponent.BiomeDatabase;            
            jobWrapper.job.biomeMap = new NativeArray<int>(chunkDimensions.x * chunkDimensions.y, Allocator.Persistent);

            var arrayLength = chunkDimensions.x * chunkDimensions.y * chunkDimensions.z;

            NativeArray<VoxelData> voxelData = new NativeArray<VoxelData>(arrayLength, Allocator.Persistent);
            jobWrapper.job.chunkData = voxelData;

            Func<IChunkData<VoxelData>> cleanup = () =>
            {
                Profiler.BeginSample("DataJobCleanup");

                //Pass resulting array to chunk data.
                var ChunkData = chunkDataFactory.Create(chunkID, chunkDimensions, voxelData.ToArray());
                
                //Dispose of native arrays
                voxelData.Dispose();
                jobWrapper.job.biomeMap.Dispose();

                Profiler.EndSample();
                return ChunkData;
            };

            //Single threaded version  DEBUG
            return new BasicFunctionJob<IChunkData<VoxelData>>(() =>
            {
                jobWrapper.job.Run();
                return cleanup();
            });

            return new PipelineUnityJob<IChunkData<VoxelData>, DataGenerationJob>(jobWrapper, cleanup);
        }


        #region Deprecated
        /// <summary>
        /// Main-thread chunk generation
        /// </summary>
        /// <param name="chunkID"></param>
        /// <param name="chunkDimensions"></param>
        /// <returns></returns>
        private IChunkData<VoxelData> GenerateChunkData(Vector3Int chunkID, Vector3Int chunkDimensions)
        {
            if (chunkID.y < chunkManager.MinChunkY || chunkID.y > chunkManager.MaxChunkY)
            {
                return new EmptyChunkData(chunkID, chunkManager.ChunkDimensions);
            }

            var chunkPosition = chunkManager.ChunkToWorldPosition(chunkID).ToInt();

            var ChunkData = new ArrayChunkData(chunkID, chunkDimensions);

            float[,] heightMap = new float[chunkDimensions.x, chunkDimensions.z];

            for (int z = 0; z < chunkDimensions.z; z++)
            {
                for (int x = 0; x < chunkDimensions.x; x++)
                {
                    heightMap[x, z] = CalculateHeightMapAt(x+chunkPosition.x, z+chunkPosition.z);
                }
            }            

            for (int z = 0; z < chunkDimensions.z; z++)
            {                
                for (int y = 0; y < chunkDimensions.y; y++)
                {
                    for (int x = 0; x < chunkDimensions.x; x++)
                    {
                        GenerateVoxelAt(ChunkData, chunkPosition, new Vector3Int(x, y, z),heightMap[x,z]);
                    }
                }
            }

            return ChunkData;
        }

        protected virtual void GenerateVoxelAt(AbstractChunkData chunkData,Vector3Int chunkPosition, Vector3Int localCoords,float height) 
        {
            ushort id;

            //World pos
            var pos = chunkPosition + localCoords;

            id = CalculateVoxelIDAt(pos,height);

            if (id == VoxelTypeManager.AIR_ID)
            {
                return;
            }

            chunkData[localCoords] = new VoxelData(id);
            
        }

        private float CalculateHeightMapAt(int x, int z) 
        {
            float rawHeightmap = fastNoise.GetSimplexFractal(x * worldSettings.HeightmapScale, z * worldSettings.HeightmapScale) * worldSettings.MaxHeightmapHeight;

            //add the raw heightmap to the base ground height
            return worldSettings.SeaLevel + rawHeightmap;
        }

        /// <summary>
        /// Based on REF: https://github.com/samhogan/Minecraft-Unity3D/blob/master/Assets/Scripts/TerrainGenerator.cs        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        private ushort CalculateVoxelIDAt(Vector3Int pos,float height) 
        {
            var (x, y, z) = pos;
            ushort id = VoxelTypeManager.AIR_ID;

            if (y > height)
            {//Air
                return id;
            }

            if (y == minY )
            {
                id = bedrockID;
                return id;
            }

            //3D noise for caves
            float caveNoise = fastNoise.GetPerlinFractal(x * 5f, y * 10f, z * 5f);

            if (caveNoise > worldSettings.CaveThreshold)
            {
                //Cave
                return id;
            }

            if (y > height -1)
            {
                id = grassID;
            }
            else
            {
                if (y < height -4)
                {
                    id = stoneID;
                }
                else
                {                    
                    id = dirtID;
                }
            }


            return id;
        }
        #endregion

    }

    [System.Serializable]
    public struct NoiseSettings
    {
        public int Octaves;
        public float Persistence;//Aka gain
        public float Lacunarity;
        public int Seed;
    }

    public struct BlockIDs 
    {
        public ushort stone;
        public ushort dirt;
        public ushort grass;
        public ushort bedrock;
    }

    [System.Serializable]
    public struct WorldSettings 
    {
        public float HeightmapScale;
        public float MaxHeightmapHeight;
        public float HeightmapExponentPositive;
        public float HeightmapExponentNegative;
        public float SeaLevel;
        [NonSerialized] public float MinY;
        public float CaveThreshold;
        public float CaveScale;
        [NonSerialized] public int3 ChunkDimensions;
    }

    [BurstCompile]
    public struct DataGenerationJob : IJob
    {
        [ReadOnly] public WorldSettings worldSettings;
        [ReadOnly] public NoiseSettings noiseSettings;
        [ReadOnly] public BlockIDs ids;
        [ReadOnly] public float3 chunkPosition;

        //Output
        public NativeArray<VoxelData> chunkData;

        [ReadOnly] public NativeBiomeDatabase biomeDatabase;

        //Used for noise normalization
        private float MaxNoiseAmplitude;

        private float moistureSeed;

        public NativeArray<int> biomeMap;

        public void Execute()
        {
            PrecalculateMaxNoiseAmplitude();

            var rand = new Unity.Mathematics.Random((uint)noiseSettings.Seed);
            moistureSeed = rand.NextFloat();

            int3 dimensions = worldSettings.ChunkDimensions;

            NativeArray<float> heightMap = new NativeArray<float>(dimensions.x*dimensions.y,Allocator.Temp);
            ComputeHeightMap(ref heightMap, dimensions);            
            ComputeBiomeMap(ref heightMap, dimensions);

            var mapDimensions = new int2(dimensions.x, dimensions.z);

            int dx = dimensions.x;
            int dxdy = dimensions.x * dimensions.y;

            //int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    //Process one column of voxels
                    var mapIndex = Utils.Helper.MultiIndexToFlat(x, z, mapDimensions);
                    var layers = biomeDatabase.biomeLayers[biomeMap[mapIndex]];

                    var height = heightMap[mapIndex];

                    var currentLayerIndex = layers.start;
                    var currentLayer = biomeDatabase.allLayers[currentLayerIndex];
                    int placedThisLayer = 0;

                    var yStart = (int)math.floor(math.min(height - chunkPosition.y, dimensions.y - 1));

                    var layerVoxel = currentLayer.voxelID;

                    //top to bottom, skipping all that are above the height (as these are air)
                    for (int y = yStart; y >= 0; y--)
                    {
                        var pos = chunkPosition + new float3(x,y,z);

                        if (placedThisLayer == currentLayer.depth)
                        {
                            currentLayerIndex++;
                            if (currentLayerIndex == layers.end)
                            {
                                //If out of layers, switch to default
                                layerVoxel = biomeDatabase.defaultVoxelId;
                            }
                            else
                            {
                                //Go to next layer, reset counter
                                placedThisLayer = 0;
                                currentLayer = biomeDatabase.allLayers[currentLayerIndex];
                                layerVoxel = currentLayer.voxelID;
                            }
                        }

                        var idToPlace = layerVoxel;
                            
                        //handle bedrock and caves
                        if (pos.y <= worldSettings.MinY)
                        {
                            idToPlace = ids.bedrock;
                        }
                        else
                        {
                            //3D noise for caves
                            float caveNoise = FractalNoise(pos * worldSettings.CaveScale, noiseSettings.Seed);

                            if (caveNoise > worldSettings.CaveThreshold)
                            {
                                //Cave
                                idToPlace = VoxelTypeManager.AIR_ID;
                            }
                        }

                        var flatIndex = Utils.Helper.MultiIndexToFlat(x, y, z, dx,dxdy);
                        chunkData[flatIndex] = new VoxelData(idToPlace);
                        placedThisLayer++;
                    }
                }
            }
        }

        private void ComputeBiomeMap(ref NativeArray<float> heightMap, int3 dimensions) 
        {
            var maxPossibleHmValue = worldSettings.MaxHeightmapHeight;
            var minPossibleHmValue = -1 *worldSettings.MaxHeightmapHeight;

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
                    moistureMap[i] = ZeroToOne(FractalNoise(new float2(x + chunkPosition.x, z + chunkPosition.z),moistureSeed));
                    i++;
                }
            }
        }

        private void ComputeHeightMap(ref NativeArray<float> heightMap, int3 dimensions) 
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

        public float CalculateHeightMapAt(float2 pos)
        {            
            
            float rawHeightmap = AdjustHeightMapNoiseValue(FractalNoise(pos * worldSettings.HeightmapScale,noiseSettings.Seed)) * worldSettings.MaxHeightmapHeight;

            //add the raw heightmap to the base ground height
            return worldSettings.SeaLevel + rawHeightmap;
        }

        public ushort CalculateVoxelIDAt(float3 pos, float height,StartEnd layers)
        {            
            ushort id = VoxelTypeManager.AIR_ID;

            for (int i = layers.start; i < layers.end; i++)
            {
                
            }


            if (pos.y > height)
            {//Air
                return id;
            }

            if (pos.y == worldSettings.MinY)
            {
                id = ids.bedrock;
                return id;
            }

            //3D noise for caves
            float caveNoise = FractalNoise(pos * worldSettings.CaveScale,noiseSettings.Seed);

            if (caveNoise > worldSettings.CaveThreshold)
            {
                //Cave
                return id;
            }

            if (pos.y > height - 1)
            {
                id = ids.grass;
            }
            else
            {
                if (pos.y < height - 4)
                {
                    id = ids.stone;
                }
                else
                {
                    id = ids.dirt;
                }
            }

            return id;
        }

        private void PrecalculateMaxNoiseAmplitude() 
        {
            //Precalcuate max noise amplitude, to avoid having to do so for each noise calculation
            float amplitude = 1;
            MaxNoiseAmplitude = 0;
            for (int n = 0; n < noiseSettings.Octaves; n++)
            {
                MaxNoiseAmplitude += amplitude;
                amplitude *= noiseSettings.Persistence;
            }
        }

        /// <summary>
        /// Transform a noise value from the -1->1 range that the noise functions
        /// output to the 0->1 range
        /// </summary>
        /// <param name="rawNoise"></param>
        /// <returns></returns>
        private float ZeroToOne(float rawNoise) 
        {
            return math.unlerp(-1, 1, rawNoise);
        }

        private float FractalNoise(float3 pos,float seed) 
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            for (int i = 0; i < noiseSettings.Octaves; i++)
            {
                total += noise.snoise(new float4(pos*frequency,seed)) * amplitude;
                amplitude *= noiseSettings.Persistence;
                frequency *= noiseSettings.Lacunarity;
            }
            //Return normalised
            return total / MaxNoiseAmplitude;
        }

        private float FractalNoise(float2 pos,float seed)
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            for (int i = 0; i < noiseSettings.Octaves; i++)
            {
                total += noise.snoise(new float3(pos * frequency,seed)) * amplitude;
                amplitude *= noiseSettings.Persistence;
                frequency *= noiseSettings.Lacunarity;
            }
            //Return normalised
            return total / MaxNoiseAmplitude;
        }
    }
}