using System.Collections.Generic;
using UnityEngine;

namespace UniVox.Framework.Lighting
{
    public interface ILightManager
    {
        void Initialise(IChunkManager chunkManager, IVoxelTypeManager voxelTypeManager);
        void OnChunkFullyGenerated(ChunkNeighbourhood neighbourhood,int[] heightMap);
        void Update();
        List<Vector3Int> UpdateLightOnVoxelSet(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, VoxelTypeID voxelType, VoxelTypeID previousType);
    }
}