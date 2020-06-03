using UnityEngine;
using System.Collections;

public abstract class AbstractProviderComponent<ChunkDataType, VoxelDataType> : MonoBehaviour, IChunkProvider<ChunkDataType, VoxelDataType>
    where ChunkDataType : IChunkData<VoxelDataType>
    where VoxelDataType : IVoxelData
{
    public abstract ChunkDataType ProvideChunkData(Vector3Int chunkID, Vector3Int chunkDimensions);
}
