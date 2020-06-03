using UnityEngine;
using System.Collections;

/// <summary>
/// The data representation of a Chunk
/// </summary>
public interface IChunkData<V> where V: IVoxelData
{    
    Vector3Int ChunkID { get; set; }

    Vector3Int Dimensions { get; set;}

    V this[int i,int j, int k] { get; set; }
    V this[Vector3Int index] { get; set; }

    void SetVoxelAtLocalCoordinates(Vector3Int coords, V voxel);
    void SetVoxelAtLocalCoordinates(int x, int y, int z, V voxel);

    V GetVoxelAtLocalCoordinates(Vector3Int coords);
    V GetVoxelAtLocalCoordinates(int x, int y, int z);
}
