using UnityEngine;

namespace UniVox.Framework
{
    public interface IChunkManager
    {
        Vector3Int ChunkDimensions { get; }

        Vector3 ChunkToWorldPosition(Vector3Int chunkID);
        Vector3Int WorldToChunkPosition(Vector3 pos);
        Vector3Int LocalVoxelIndexOfPosition(Vector3Int position);
        Vector3 SnapToVoxelCenter(Vector3 pos);

        void Initialise();

        bool TrySetVoxel(Vector3 worldPos, ushort voxelTypeID, bool overrideExisting = false);
        bool TryGetVoxel(Vector3 worldPos,out ushort voxelTypeID);
        bool TryGetVoxel(Vector3Int chunkID, Vector3Int localVoxelIndex, out ushort voxelTypeID);
    }
}