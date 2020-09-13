using PerformanceTesting;
using UnityEngine;
using UniVox.Framework.Lighting;

namespace UniVox.Framework
{
    public interface IVoxelWorld
    {
        Vector3 CenterOfVoxelAt(Vector3 position);
        bool IsChunkComplete(Vector3Int chunkId);
        bool IsChunkFullyGenerated(Vector3Int chunkId);
        void PlaceVoxel(Vector3 position,
            SOVoxelTypeDefinition voxelType,
            VoxelRotation rotation = default);
        void RemoveVoxel(Vector3 position);
        bool TryGetLightLevel(Vector3 position,
            out LightValue lightValue);
        bool TryGetVoxelType(Vector3 position,
            out SOVoxelTypeDefinition voxelType);
        bool TryGetVoxelTypeAndID(Vector3 position,
            out SOVoxelTypeDefinition voxelType,
            out VoxelTypeID voxelID);
        Vector3Int WorldToChunkPosition(Vector3 pos);
    }
}