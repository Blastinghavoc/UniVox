using UnityEngine;
using System.Collections;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;
using UniVox.Implementations.Common;

namespace UniVox.Implementations.Providers
{
    public class DebugProvider : AbstractProviderComponent<AbstractChunkData, VoxelData>
    {
        public SOVoxelTypeDefinition dirtType;
        private ushort dirtID;
        public SOVoxelTypeDefinition grassType;
        private ushort grassID;

        public override void Initialise(VoxelTypeManager voxelTypeManager)
        {
            base.Initialise(voxelTypeManager);
            dirtID = voxelTypeManager.GetId(dirtType);
            grassID = voxelTypeManager.GetId(grassType);
        }

        public override AbstractChunkData ProvideChunkData(Vector3Int chunkID, Vector3Int chunkDimensions)
        {
            return HalfHeight(chunkID, chunkDimensions);
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
                            ChunkData[x, y, z] = new VoxelData(grassID);
                        }
                        else
                        {
                            ChunkData[x, y, z] = new VoxelData(dirtID);
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
                        ChunkData[x, y, z] = new VoxelData(dirtID);
                    }
                }
            }
            return ChunkData;
        }
    }
}