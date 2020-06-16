using UnityEngine;
using System.Collections;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;
using UniVox.Implementations.Common;
using Utils.Noise;
using System;

namespace UniVox.Implementations.Providers
{
    public class NoisyProvider : AbstractProviderComponent<AbstractChunkData, VoxelData>
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

        private FastNoise noise;

        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager)
        {
            base.Initialise(voxelTypeManager, chunkManager);
            dirtID = voxelTypeManager.GetId(dirtType);
            grassID = voxelTypeManager.GetId(grassType);
            stoneID = voxelTypeManager.GetId(stoneType);

            noise = new FastNoise(Seed);
            noiseSettings.ApplyTo(noise);

        }

        public override AbstractChunkData GenerateChunkData(Vector3Int chunkID, Vector3Int chunkDimensions)
        {
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
}