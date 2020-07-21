using System.Collections.Generic;
using UnityEngine;

namespace UniVox.Framework.Lighting
{
    public interface ILightManager
    {
        void Initialise(IVoxelTypeManager voxelTypeManager);
        void OnChunkGenerated(IChunkData chunkData,IChunkData aboveChunkData);
        List<Vector3Int> UpdateLightOnVoxelSet(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, VoxelTypeID voxelType, VoxelTypeID previousType);
    }
}