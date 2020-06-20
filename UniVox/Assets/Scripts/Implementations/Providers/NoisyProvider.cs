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

namespace UniVox.Implementations.Providers
{
    public class NoisyProvider : AbstractProviderComponent<VoxelData>
    {
        public int Seed = 1337;
        public int SeaLevel = 0;
        public float Density = 0.5f;
        public float MaxHeight = 32;
        public float HeightmapScale = 1;

        public NoiseSettings noiseSettings;

        public SOVoxelTypeDefinition dirtType;
        private ushort dirtID;
        public SOVoxelTypeDefinition grassType;
        private ushort grassID;
        public SOVoxelTypeDefinition stoneType;
        private ushort stoneID;

        public SOVoxelTypeDefinition bedrockType;
        private ushort bedrockID;

        private FastNoise fastNoise;

        private int minY = int.MinValue;

        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager)
        {
            base.Initialise(voxelTypeManager, chunkManager);
            dirtID = voxelTypeManager.GetId(dirtType);
            grassID = voxelTypeManager.GetId(grassType);
            stoneID = voxelTypeManager.GetId(stoneType);
            bedrockID = voxelTypeManager.GetId(bedrockType);

            fastNoise = new FastNoise(Seed);
            noiseSettings.ApplyTo(fastNoise);

            if (chunkManager.IsWorldHeightLimited)
            {
                minY = chunkManager.MinChunkY * chunkManager.ChunkDimensions.y;
            }

        }

        public override AbstractPipelineJob<IChunkData<VoxelData>> GenerateChunkDataJob(Vector3Int chunkID,Vector3Int chunkDimensions)
        {
            if (!Burst)
            {
                return base.GenerateChunkDataJob(chunkID, chunkDimensions);
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
            jobWrapper.job.noiseSettings = new JobNoiseSettings()
            {
                Lacunarity = noiseSettings.Lacunarity,
                Persistence = noiseSettings.Persistence,
                Octaves = noiseSettings.Octaves
            };
            jobWrapper.job.worldSettings = new WorldSettings()
            {
                CaveDensity = Density,
                SeaLevel = SeaLevel,
                HeightmapScale = HeightmapScale,
                ChunkDimensions = new int3(chunkDimensions.x, chunkDimensions.y, chunkDimensions.z),
                MaxHeightmapHeight = MaxHeight,
                MinY = minY
            };

            var arrayLength = chunkDimensions.x * chunkDimensions.y * chunkDimensions.z;

            NativeArray<VoxelData> voxelData = new NativeArray<VoxelData>(arrayLength, Allocator.Persistent);
            jobWrapper.job.chunkData = voxelData;

            Func<IChunkData<VoxelData>> cleanup = () =>
            {
                Profiler.BeginSample("DataJobCleanup");

                //Pass flat array to chunk data.
                var ChunkData = new FlatArrayChunkData(chunkID, chunkDimensions, voxelData.ToArray());
                //Dispose of native array
                voxelData.Dispose();

                Profiler.EndSample();
                return ChunkData;
            };

            return new PipelineUnityJob<IChunkData<VoxelData>, DataGenerationJob>(jobWrapper, cleanup);
        }

        public override IChunkData<VoxelData> GenerateChunkData(Vector3Int chunkID, Vector3Int chunkDimensions)
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
            float rawHeightmap = fastNoise.GetSimplexFractal(x * HeightmapScale, z * HeightmapScale) * MaxHeight;

            //add the raw heightmap to the base ground height
            return SeaLevel + rawHeightmap;
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

            if (caveNoise > Density)
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
        
    }

    [System.Serializable]
    public struct JobNoiseSettings
    {
        public int Octaves;
        public float Persistence;//Aka gain
        public float Lacunarity;
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
        public float SeaLevel;
        public float MinY;
        public float CaveDensity;
        public int3 ChunkDimensions;
    }

    [BurstCompile]
    public struct DataGenerationJob : IJob
    {
        public WorldSettings worldSettings;
        public JobNoiseSettings noiseSettings;
        public BlockIDs ids;
        public float3 chunkPosition;

        public NativeArray<VoxelData> chunkData;

        //Used for noise normalization
        private float MaxNoiseAmplitude;

        public void Execute()
        {
            PrecalculateMaxNoiseAmplitude();


            int3 dimensions = worldSettings.ChunkDimensions;

            //float[,] heightMap = new float[dimensions.x, dimensions.z];
            NativeArray<float> heightMap = new NativeArray<float>(dimensions.x*dimensions.y,Allocator.Temp);

            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    heightMap[i] = CalculateHeightMapAt(new float2(x + chunkPosition.x, z + chunkPosition.z));
                    i++;
                }
            }
            var heightMapDimensions = new int2(dimensions.x, dimensions.z);

            i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        var pos = chunkPosition + new float3(x, y, z);

                        var id = CalculateVoxelIDAt(pos, heightMap[Utils.Helper.MultiIndexToFlat(x,z, heightMapDimensions)]);

                        chunkData[i] = new VoxelData(id);
                        i++;
                    }
                }
            }
        }

        public float CalculateHeightMapAt(float2 pos)
        {            
            
            float rawHeightmap = FractalNoise(pos * worldSettings.HeightmapScale) * worldSettings.MaxHeightmapHeight;

            //add the raw heightmap to the base ground height
            return worldSettings.SeaLevel + rawHeightmap;
        }

        public ushort CalculateVoxelIDAt(float3 pos, float height)
        {            
            ushort id = VoxelTypeManager.AIR_ID;

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
            float caveNoise = FractalNoise(pos * 0.01f);

            if (caveNoise > worldSettings.CaveDensity)
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

        private float FractalNoise(float3 pos) 
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            for (int i = 0; i < noiseSettings.Octaves; i++)
            {
                total += noise.snoise(pos*frequency) * amplitude;
                amplitude *= noiseSettings.Persistence;
                frequency *= noiseSettings.Lacunarity;
            }
            //Return normalised
            return total / MaxNoiseAmplitude;
        }

        private float FractalNoise(float2 pos)
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            for (int i = 0; i < noiseSettings.Octaves; i++)
            {
                total += noise.snoise(pos * frequency) * amplitude;
                amplitude *= noiseSettings.Persistence;
                frequency *= noiseSettings.Lacunarity;
            }
            //Return normalised
            return total / MaxNoiseAmplitude;
        }
    }
}