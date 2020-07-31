using System;
using UnityEngine;
using UniVox.Framework.ChunkPipeline;
using UniVox.Framework.Lighting;
using UniVox.Framework.PlayAreaManagement;

namespace UniVox.Framework
{
    public interface IChunkManager
    {
        Vector3Int ChunkDimensions { get; }

        Vector3 ChunkToWorldPosition(Vector3Int chunkID);
        Vector3Int WorldToChunkPosition(Vector3 pos);
        Vector3 SnapToVoxelCenter(Vector3 pos);

        //Maximum radii from the player on each axis at which chunks may be active (in the lowest pipeline stage)
        Vector3Int MaximumActiveRadii { get; }
        bool GenerateStructures { get; }
        bool IncludeLighting { get; }
        WorldSizeLimits WorldLimits { get; }

        void Initialise();

        bool TrySetVoxel(Vector3 worldPos, VoxelTypeID voxelTypeID,VoxelRotation voxelRotation = default, bool overrideExisting = false);
        bool TryGetVoxel(Vector3 worldPos,out VoxelTypeID voxelTypeID);
        bool TryGetVoxel(Vector3Int chunkID, Vector3Int localVoxelIndex, out VoxelTypeID voxelTypeID);

        bool TryGetLightLevel(Vector3 worldPos, out LightValue lightValue);

        RestrictedChunkData GetReadOnlyChunkData(Vector3Int chunkID);

        IChunkData GetChunkData(Vector3Int chunkId);
        MeshDescriptor GetMeshDescriptor(Vector3Int chunkID);

        bool InsideChunkRadius(Vector3Int id, Vector3Int radii);


        //TODO remove DEBUG
        /// <summary>
        /// First return indicates whether the manager itself contains the id in its loaded chunks,
        /// second return indicates whether the pipeline containts the id.
        /// </summary>
        /// <param name="chunkID"></param>
        /// <returns></returns>
        Tuple<bool, bool> ContainsChunkID(Vector3Int chunkID);

        //TODO remove DEBUG
        int GetMinPipelineStageOfChunk(Vector3Int chunkId);

        bool IsChunkComplete(Vector3Int chunkId);
        void SetTargetStageOfChunk(Vector3Int chunkID, int targetStage,TargetUpdateMode updateMode = TargetUpdateMode.any);
        bool TryDeactivateChunk(Vector3Int chunkID);
        Vector3Int[] GetAllLoadedChunkIds();
        bool IsChunkFullyGenerated(Vector3Int chunkId);
    }
}