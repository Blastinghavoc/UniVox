using System;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;
using UniVox.Framework.Jobified;

namespace UniVox.Implementations.ProcGen
{
    [System.Serializable]
    public class StructureGenerator 
    {
        [SerializeField] private SOVoxelTypeDefinition logType = null;
        [NonSerialized] VoxelTypeID logId;
        [SerializeField] private SOVoxelTypeDefinition leafType = null;
        [NonSerialized] VoxelTypeID leafID;

        [SerializeField] private SOBiomeDefinition oceanDefinition = null;
        [NonSerialized] int oceanId;

        [NonSerialized] private float treeThreshold;
        public void Initalise(VoxelTypeManager voxelTypeManager,BiomeDatabaseComponent biomeDatabase,float treeThreshold) 
        {
            logId = voxelTypeManager.GetId(logType);
            leafID = voxelTypeManager.GetId(leafType);
            oceanId = biomeDatabase.GetBiomeID(oceanDefinition);
            this.treeThreshold = treeThreshold;
        }

        public ChunkNeighbourhood generateTrees(Vector3 chunkPosition,Vector3Int dimensions,ChunkNeighbourhood neighbourhood,ChunkColumnNoiseMaps chunkColumnNoise) 
        {
            int mapIndex = 0;
            var chunkTop = chunkPosition.y + dimensions.y;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++,mapIndex++)
                {
                    var hm = chunkColumnNoise.heightMap[mapIndex];
                    if (hm >= chunkPosition.y && hm < chunkTop)
                    {
                        //Put a tree here, standing in for noise function
                        if (chunkColumnNoise.biomeMap[mapIndex] != oceanId &&  chunkColumnNoise.treeMap[mapIndex] > treeThreshold)
                        {
                            var y = (int)math.floor(hm - chunkPosition.y);

                            MakeTree(neighbourhood, x, y, z);
                            
                        }
                    }
                }
            }
            return neighbourhood;
        }

        private void MakeTree(ChunkNeighbourhood neighbourhood,int x,int y, int z) 
        {
            for (int i = 0; i < 10; i++, y++)
            {
                neighbourhood[x, y, z] = logId;

                if (i > 4)
                {
                    for (int j = -2; j <= 2; j++)
                    {                        
                        for (int k = -2; k <= 2; k++)
                        {
                            if (j==0 && k == 0)
                            {
                                continue;
                            }

                            neighbourhood.SetIfUnoccupied(x + j, y, z + k,leafID);
                        }
                    }
                }
            }

            for (int i = 0; i<2; i++,y++)
		    {
                
                for (int j = -1; j <= 1; j++)
                {
                    for (int k = -1; k <= 1; k++)
                    {
                        if (j == 0 && k == 0)
                        {
                            continue;
                        }

                        neighbourhood.SetIfUnoccupied(x + j, y, z + k, leafID);
                    }
                }
                
            }

        }


    }

}