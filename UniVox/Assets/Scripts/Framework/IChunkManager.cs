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

        bool IsWorldHeightLimited { get; }
        int MaxChunkY { get; }
        int MinChunkY { get;}

        void Initialise();

        bool TrySetVoxel(Vector3 worldPos, VoxelTypeID voxelTypeID, bool overrideExisting = false);
        bool TryGetVoxel(Vector3 worldPos,out VoxelTypeID voxelTypeID);
        bool TryGetVoxel(Vector3Int chunkID, Vector3Int localVoxelIndex, out VoxelTypeID voxelTypeID);
        ReadOnlyChunkData GetReadOnlyChunkData(Vector3Int chunkID);
        MeshDescriptor GetMeshDescriptor(Vector3Int chunkID);
    }
}