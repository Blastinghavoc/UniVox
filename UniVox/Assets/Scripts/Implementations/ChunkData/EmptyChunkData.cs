using UnityEngine;
using UniVox.Implementations.Common;
using UniVox.Framework;
using Unity.Collections;

namespace UniVox.Implementations.ChunkData
{
    /// <summary>
    /// Chunk data class that represents a completely empty chunk,
    /// to be used for chunks outside the world limits.
    /// </summary>
    public class EmptyChunkData : AbstractChunkData
    {
        public EmptyChunkData(Vector3Int ID, Vector3Int chunkDimensions) : base(ID, chunkDimensions)
        {
        }

        public override void FromNative(NativeArray<VoxelData> native)
        {
            throw new System.NotImplementedException($"This is an empty chunk data object, it cannot contain data");
        }

        protected override VoxelData GetVoxelAtLocalCoordinates(int x, int y, int z)
        {
            return new VoxelData(VoxelTypeManager.AIR_ID);
        }

        protected override void SetVoxelAtLocalCoordinates(int x, int y, int z, VoxelData voxel)
        {
            return;
        }
    }
}