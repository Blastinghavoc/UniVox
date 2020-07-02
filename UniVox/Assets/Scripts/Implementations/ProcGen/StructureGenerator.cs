using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UniVox.Framework;
using UniVox.Framework.Jobified;

namespace UniVox.Implementations.ProcGen
{
    public class StructureGenerator 
    {


        private float treeThreshold;
        private BiomeDatabaseComponent biomeDatabase;
        private VoxelTypeManager typeManager;
        private Unity.Mathematics.Random random;

        public void Initalise(VoxelTypeManager voxelTypeManager,BiomeDatabaseComponent biomeDatabase,float treeThreshold,int seed) 
        {
            this.treeThreshold = treeThreshold;
            this.biomeDatabase = biomeDatabase;
            this.typeManager = voxelTypeManager;
            random = new Unity.Mathematics.Random((uint)seed);
        }

        public ChunkNeighbourhood generateTrees(Vector3 chunkPosition,Vector3Int dimensions,ChunkNeighbourhood neighbourhood,ChunkColumnNoiseMaps chunkColumnNoise) 
        {
            Profiler.BeginSample("GeneratingTrees");
            int mapIndex = 0;
            var chunkTop = chunkPosition.y + dimensions.y;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++,mapIndex++)
                {
                    var hm = chunkColumnNoise.heightMap[mapIndex];
                    if (hm >= chunkPosition.y && hm < chunkTop)
                    {
                        var groundY = (int)math.floor(hm - chunkPosition.y);
                        //Check ground is still there (not been removed by cave)
                        if (neighbourhood[x,groundY,z] != VoxelTypeManager.AIR_ID)
                        {
                            if (chunkColumnNoise.treeMap[mapIndex] > treeThreshold)
                            {
                                //Put a tree here
                                var y = groundY + 1;

                                MakeTree(neighbourhood, x, y, z,chunkColumnNoise.biomeMap[mapIndex]);
                            
                            }
                        }

                    }
                }
            }
            Profiler.EndSample();
            return neighbourhood;
        }

        private void MakeTree(ChunkNeighbourhood neighbourhood, int x, int y, int z, int biomeID) 
        {
            var SOtreeDef = biomeDatabase.GetBiomeDefinition(biomeID).treeType;
            if (SOtreeDef == null)
            {
                return;
            }
            var treeDef = SOtreeDef.ToNative(typeManager);
            switch (treeDef.style)
            {
                case SOTreeDefinition.treeStyle.oak:
                    MakeOak(neighbourhood, x, y, z, treeDef);
                    break;
                case SOTreeDefinition.treeStyle.spruce:
                    MakeSpruce(neighbourhood, x, y, z, treeDef);
                    break;
                case SOTreeDefinition.treeStyle.cactus:
                    MakeCactus(neighbourhood, x, y, z, treeDef);
                    break;
                default:
                    break;
            }
        }

        private void MakeOak(ChunkNeighbourhood neighbourhood, int x, int y, int z,NativeTreeDefinition treeDef) 
        {
            int numLeavesOnTop = 2;
            int minCanopyHeight = numLeavesOnTop+2;
            var height = random.NextInt(treeDef.minHeight, treeDef.maxHeight);
            var leafStart = random.NextInt(treeDef.minLeafClearance, height - minCanopyHeight);
            for (int i = 0; i < height- numLeavesOnTop; i++, y++)
            {
                neighbourhood[x, y, z] = treeDef.logID;

                if (i > leafStart)
                {
                    for (int j = -2; j <= 2; j++)
                    {
                        for (int k = -2; k <= 2; k++)
                        {
                            if (j == 0 && k == 0)
                            {
                                continue;
                            }

                            neighbourhood.SetIfUnoccupied(x + j, y, z + k, treeDef.leafID);
                        }
                    }
                }
            }

            //Top Leaves
            for (int i = 0; i < numLeavesOnTop; i++, y++)
            {

                for (int j = -1; j <= 1; j++)
                {
                    for (int k = -1; k <= 1; k++)
                    {

                        neighbourhood.SetIfUnoccupied(x + j, y, z + k, treeDef.leafID);
                    }
                }
            }
        }

        private void MakeSpruce(ChunkNeighbourhood neighbourhood, int x, int y, int z, NativeTreeDefinition treeDef)
        {
            int numLeavesOnTop = 2;
            int minCanopyHeight = numLeavesOnTop + 2;
            var height = random.NextInt(treeDef.minHeight, treeDef.maxHeight);
            var leafStart = random.NextInt(treeDef.minLeafClearance, height - minCanopyHeight);
            var leafWidth = height / 2;
            for (int i = 0; i < height - numLeavesOnTop; i++, y++)
            {
                neighbourhood[x, y, z] = treeDef.logID;

                if (i > leafStart)
                {
                    for (int j = -leafWidth; j <= leafWidth; j++)
                    {
                        for (int k = -leafWidth; k <= leafWidth; k++)
                        {
                            if (j == 0 && k == 0)
                            {
                                continue;
                            }

                            neighbourhood.SetIfUnoccupied(x + j, y, z + k, treeDef.leafID);
                        }
                    }
                    leafWidth = math.max(leafWidth - 1, 1);
                }
            }

            //Top Leaves
            for (int i = 0; i < numLeavesOnTop; i++, y++)
            {

                for (int j = -leafWidth; j <= leafWidth; j++)
                {
                    for (int k = -leafWidth; k <= leafWidth; k++)
                    {
                        neighbourhood.SetIfUnoccupied(x + j, y, z + k, treeDef.leafID);
                    }
                }
            }
        }

        private void MakeCactus(ChunkNeighbourhood neighbourhood, int x, int y, int z, NativeTreeDefinition treeDef)
        {
            var height = random.NextInt(treeDef.minHeight, treeDef.maxHeight);
            for (int i = 0; i < height; i++, y++)
            {
                neighbourhood[x, y, z] = treeDef.logID;                
            }            
        }

    }

}