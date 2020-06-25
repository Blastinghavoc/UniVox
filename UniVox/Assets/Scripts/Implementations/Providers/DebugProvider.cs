using UnityEngine;
using System.Collections;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Implementations.Providers
{
    public class DebugProvider : AbstractProviderComponent
    {
        public SOVoxelTypeDefinition dirtType;
        private ushort dirtID;
        public SOVoxelTypeDefinition grassType;
        private ushort grassID;

        public override void Initialise(VoxelTypeManager voxelTypeManager,IChunkManager chunkManager)
        {
            base.Initialise(voxelTypeManager,chunkManager);
            dirtID = voxelTypeManager.GetId(dirtType);
            grassID = voxelTypeManager.GetId(grassType);
        }

        public override AbstractPipelineJob<IChunkData> GenerateChunkDataJob(Vector3Int chunkID, Vector3Int chunkDimensions)
        {
            return new BasicFunctionJob<IChunkData>(() => FlatWorld(chunkID, chunkDimensions));
        }

        private AbstractChunkData FlatWorld(Vector3Int chunkID, Vector3Int chunkDimensions) 
        {
            var ChunkData = new ArrayChunkData(chunkID, chunkDimensions);

            int groundHeight = 0;
            int chunkYCuttoff = (groundHeight + chunkDimensions.y)/chunkDimensions.y;

            var chunkPosition = chunkManager.ChunkToWorldPosition(chunkID);

            if (chunkID.y < chunkYCuttoff)//Chunks above the cuttof are just pure air
            {
                for (int z = 0; z < chunkDimensions.z; z++)
                {
                    for (int y = 0; y < chunkDimensions.y; y++)
                    {
                        for (int x = 0; x < chunkDimensions.x; x++)
                        {
                            var height = y + chunkPosition.y;
                            if (height == groundHeight)
                            {
                                ChunkData[x, y, z] = new VoxelTypeID(grassID);
                            }
                            else if (height < groundHeight)
                            {
                                ChunkData[x, y, z] = new VoxelTypeID(dirtID);
                            }
                        }
                    }
                }
            }

            return ChunkData;
        }

        private AbstractChunkData HalfHeight(Vector3Int chunkID, Vector3Int chunkDimensions)
        {
            var ChunkData = new ArrayChunkData(chunkID, chunkDimensions);
            var maxY = chunkDimensions.y / 2;

            for (int z = 0; z < chunkDimensions.z; z++)
            {
                for (int y = 0; y < maxY; y++)
                {
                    for (int x = 0; x < chunkDimensions.x; x++)
                    {
                        if (y == maxY - 1)
                        {
                            ChunkData[x, y, z] = new VoxelTypeID(grassID);
                        }
                        else
                        {
                            ChunkData[x, y, z] = new VoxelTypeID(dirtID);
                        }
                    }
                }
            }
            return ChunkData;
        }

        private AbstractChunkData HalfLattice(Vector3Int chunkID, Vector3Int chunkDimensions)
        {
            bool b = true;
            var ChunkData = new ArrayChunkData(chunkID, chunkDimensions);
            for (int z = 0; z < chunkDimensions.z; z++)
            {
                b = !b;
                for (int y = 0; y < chunkDimensions.y / 2; y++)
                {
                    b = !b;
                    for (int x = 0; x < chunkDimensions.x; x++)
                    {
                        b = !b;
                        if (b)
                        {
                            continue;
                        }
                        ChunkData[x, y, z] = new VoxelTypeID(dirtID);
                    }
                }
            }
            return ChunkData;
        }        
    }
}