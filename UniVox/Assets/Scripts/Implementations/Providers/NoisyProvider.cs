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
        public Vector2 BiomeScale = new Vector2(10, 10);

        public NoiseSettings noiseSettings;

        public SOVoxelTypeDefinition dirtType;
        private ushort dirtID;
        public SOVoxelTypeDefinition grassType;
        private ushort grassID;

        private FastNoise noise;

        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager)
        {
            base.Initialise(voxelTypeManager, chunkManager);
            dirtID = voxelTypeManager.GetId(dirtType);
            grassID = voxelTypeManager.GetId(grassType);

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
            float rawHeightmap = noise.GetSimplexFractal(x, z) * MaxHeight;// * noise.GetSimplex(x*BiomeScale.x,z*BiomeScale.y);

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

            //3d noise for caves and overhangs and such
            //float caveNoise = noise.GetPerlinFractal(x * 5f, y * 10f, z * 5f);
            float caveNoise = noise.GetPerlinFractal(x * 5f, y * 10f, z * 5f);
            //float caveMask =1- Mathf.Clamp(Mathf.InverseLerp(-128, 128, y),0,1);

            //stone layer heightmap
            //float simplexStone1 = noise.GetSimplex(x * 1f, z * 1f) * 10;
            //float simplexStone2 = (noise.GetSimplex(x * 5f, z * 5f) + .5f) * 20 * (noise.GetSimplex(x * .3f, z * .3f) + .5f);

            //float stoneHeightMap = simplexStone1 + simplexStone2;
            //float baseStoneHeight = TerrainChunk.chunkHeight * .25f + stoneHeightMap;


            //float cliffThing = noise.GetSimplex(x * 1f, z * 1f, y) * 10;
            //float cliffThingMask = noise.GetSimplex(x * .4f, z * .4f) + .3f;


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
                id = dirtID;
            }

            return id;
        }
        
    }
}