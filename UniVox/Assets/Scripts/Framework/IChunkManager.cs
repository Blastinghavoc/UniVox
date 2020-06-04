using UnityEngine;

public interface IChunkManager
{
    Vector3 ChunkToWorldPosition(Vector3Int chunkID);
    Vector3Int WorldToChunkPosition(Vector3 pos);

    Vector3 SnapToVoxelCenter(Vector3 pos);

    bool TrySetVoxel(Vector3 worldPos, ushort voxelTypeID,bool overrideExisting = false);
}