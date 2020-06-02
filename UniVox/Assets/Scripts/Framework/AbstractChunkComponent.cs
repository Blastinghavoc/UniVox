using UnityEngine;
using System.Collections;

/// <summary>
/// The Component managing the operation of a Chunk GameObject
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public abstract class AbstractChunkComponent<ChunkDataType,VoxelDataType> : MonoBehaviour 
    where ChunkDataType:IChunkData<VoxelDataType> 
    where VoxelDataType: IVoxelData
{
    public ChunkDataType Data;

    public MeshFilter meshFilter;

    public void SetMesh(Mesh mesh) {
        meshFilter.mesh = mesh;
    }
}
