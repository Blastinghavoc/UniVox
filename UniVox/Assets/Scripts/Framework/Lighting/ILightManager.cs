using System;
using System.Collections.Generic;
using UnityEngine;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework.Lighting
{
    public interface ILightManager : IDisposable
    {
        int MaxChunksGeneratedPerUpdate { get; }
        void ApplyGenerationResult(Vector3Int chunkId, LightmapGenerationJobResult result);
        AbstractPipelineJob<LightmapGenerationJobResult> CreateGenerationJob(Vector3Int chunkId);
        void Initialise(IVoxelTypeManager voxelTypeManager, IChunkManager chunkManager, IHeightMapProvider heightMapProvider);
        HashSet<Vector3Int> Update();
        List<Vector3Int> UpdateLightOnVoxelSet(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, VoxelTypeID voxelType, VoxelTypeID previousType);
    }
}