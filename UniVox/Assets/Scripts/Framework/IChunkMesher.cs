using UnityEngine;
using System.Collections;

public interface IChunkMesher<ChunkDataType,V> where ChunkDataType:IChunkData<V> where V : IVoxelData
{
    Mesh CreateMesh(ChunkDataType chunk);
}
