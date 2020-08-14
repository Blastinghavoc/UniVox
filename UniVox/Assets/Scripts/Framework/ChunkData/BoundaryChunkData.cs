using System;
using UnityEngine;

namespace UniVox.Framework
{
    /// <summary>
    /// Chunk data class that represents a completely empty chunk that can never have data,
    /// to be used for chunks outside the world limits.
    /// </summary>
    public class BoundaryChunkData : AbstractChunkData
    {
        public BoundaryChunkData(Vector3Int ID, Vector3Int chunkDimensions) : base(ID, chunkDimensions)
        {
        }

        protected override VoxelTypeID[] GetVoxelArray()
        {
            throw new NotImplementedException();
        }

        protected override VoxelTypeID GetVoxelID(int x, int y, int z)
        {
            return new VoxelTypeID(VoxelTypeID.AIR_ID);
        }

        protected override void SetVoxelID(int x, int y, int z, VoxelTypeID voxel)
        {
            throw new InvalidOperationException($"An {typeof(BoundaryChunkData)} object cannot have voxels set");
        }
    }
}