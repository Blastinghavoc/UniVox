using UnityEngine;
using UniVox.Framework;
using Unity.Collections;
using System;

namespace UniVox.Implementations.ChunkData
{
    /// <summary>
    /// Chunk data class that represents a completely empty chunk that can never have data,
    /// to be used for chunks outside the world limits.
    /// </summary>
    public class EmptyChunkData : AbstractChunkData
    {
        public EmptyChunkData(Vector3Int ID, Vector3Int chunkDimensions) : base(ID, chunkDimensions)
        {
        }

        protected override VoxelTypeID GetVoxelAtLocalCoordinates(int x, int y, int z)
        {
            return new VoxelTypeID(VoxelTypeManager.AIR_ID);
        }

        protected override void SetVoxelAtLocalCoordinates(int x, int y, int z, VoxelTypeID voxel)
        {
            throw new InvalidOperationException($"An {typeof(EmptyChunkData)} object cannot have voxels set");
        }
    }
}