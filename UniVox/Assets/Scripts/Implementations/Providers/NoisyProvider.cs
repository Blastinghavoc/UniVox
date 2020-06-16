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

        private FastNoise noise;

        private int minY = int.MinValue;

        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager)
        {
            base.Initialise(voxelTypeManager, chunkManager);
            dirtID = voxelTypeManager.GetId(dirtType);
            grassID = voxelTypeManager.GetId(grassType);
            stoneID = voxelTypeManager.GetId(stoneType);
            bedrockID = voxelTypeManager.GetId(bedrockType);

            noise = new FastNoise(Seed);
            noiseSettings.ApplyTo(noise);

            if (chunkManager.IsWorldHeightLimited)
            {
                minY = chunkManager.MinChunkY * chunkManager.ChunkDimensions.y;
            }

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

            for (int x = 0; x < chunkDimensions.x; x++)
            {
                for (int z = 0; z < chunkDimensions.z; z++)
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
            float rawHeightmap = noise.GetSimplexFractal(x * HeightmapScale, z * HeightmapScale) * MaxHeight;

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
            float caveNoise = noise.GetPerlinFractal(x * 5f, y * 10f, z * 5f);

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

    public struct DataGenerationJob : IJob
    {
        public WorldSettings worldSettings;
        public JobNoiseSettings noiseSettings;
        public BlockIDs ids;
        public float3 chunkPosition;

        public NativeArray<ushort> voxelIds;

        public void Execute()
        {

            int3 dimensions = worldSettings.ChunkDimensions;

            float[,] heightMap = new float[dimensions.x, dimensions.z];

            for (int x = 0; x < dimensions.x; x++)
            {
                for (int z = 0; z < dimensions.z; z++)
                {
                    heightMap[x, z] = CalculateHeightMapAt(new float2(x + chunkPosition.x, z + chunkPosition.z));
                }
            }

            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        voxelIds[Utils.Helper.MultiIndexToFlat(x, y, z, dimensions)] = CalculateVoxelIDAt(new float3(x, y, z) + chunkPosition, heightMap[x, z]);
                    }
                }
            }
        }

        private float CalculateHeightMapAt(float2 pos)
        {            
            
            float rawHeightmap = FractalNoise(pos * worldSettings.HeightmapScale) * worldSettings.MaxHeightmapHeight;

            //add the raw heightmap to the base ground height
            return worldSettings.SeaLevel + rawHeightmap;
        }

        private ushort CalculateVoxelIDAt(float3 pos, float height)
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
            float caveNoise = FractalNoise(pos*5);

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

        private float FractalNoise(float3 pos) 
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            float totalAmplitude = 0;  // Used for normalization
            for (int i = 0; i < noiseSettings.Octaves; i++)
            {
                total += noise.snoise(pos*frequency) * amplitude;
                totalAmplitude += amplitude;
                amplitude *= noiseSettings.Persistence;
                frequency *= noiseSettings.Lacunarity;
            }
            //Return normalised
            return total / totalAmplitude;
        }

        private float FractalNoise(float2 pos)
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            float totalAmplitude = 0;  // Used for normalization
            for (int i = 0; i < noiseSettings.Octaves; i++)
            {
                total += noise.snoise(pos * frequency) * amplitude;
                totalAmplitude += amplitude;
                amplitude *= noiseSettings.Persistence;
                frequency *= noiseSettings.Lacunarity;
            }
            //Return normalised
            return total / totalAmplitude;
        }
    }
}