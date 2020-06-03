using UnityEngine;
using System.Collections;

public abstract class AbstractMesherComponent<ChunkDataType, VoxelDataType> : MonoBehaviour, IChunkMesher<ChunkDataType, VoxelDataType> 
    where ChunkDataType: IChunkData<VoxelDataType>
    where VoxelDataType:IVoxelData
{
    public abstract Mesh CreateMesh(ChunkDataType chunk);
}
