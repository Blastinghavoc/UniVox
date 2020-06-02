using UnityEngine;
using System.Collections;
using UnityEngine.Assertions;

public class BasicChunkData : IChunkData<BasicVoxelData>
{
    public Vector3Int ChunkID { get; set; }
    public Vector3Int Dimensions { get; set; }

    public BasicVoxelData this[Vector3Int index] {
        get { return GetVoxelAtLocalCoordinates(index); }
        set { SetVoxelAtLocalCoordinates(index, value); }
    }
    public BasicVoxelData this[int i, int j, int k] {
        get { return this[new Vector3Int(i, j, k)]; }
        set { this[new Vector3Int(i, j, k)] = value; }
    }

    /// <summary>
    /// XYZ Voxel Data
    /// </summary>
    protected BasicVoxelData[,,] voxels;

    public BasicChunkData(Vector3Int ID, Vector3Int chunkDimensions) {
        ChunkID = ID;
        Dimensions = chunkDimensions;
        voxels = new BasicVoxelData[chunkDimensions.x, chunkDimensions.y, chunkDimensions.z];
    }

    public BasicVoxelData GetVoxelAtLocalCoordinates(Vector3Int coords)
    {
        return voxels[coords.x, coords.y, coords.z];
    }

    public void SetVoxelAtLocalCoordinates(Vector3Int coords, BasicVoxelData voxel)
    {
        voxels[coords.x, coords.y, coords.z] = voxel;
    }
}
