using UnityEngine;
using System.Collections;
using UnityEngine.Assertions;

/// <summary>
/// ChunkData class storing the data in a 3D array.
/// </summary>
public class ArrayChunkData : AbstractChunkData
{
    /// <summary>
    /// XYZ Voxel Data
    /// </summary>
    protected VoxelData[,,] voxels;

    public ArrayChunkData(Vector3Int ID, Vector3Int chunkDimensions):base(ID,chunkDimensions) {
        voxels = new VoxelData[chunkDimensions.x, chunkDimensions.y, chunkDimensions.z];
    }

    public override VoxelData GetVoxelAtLocalCoordinates(Vector3Int coords)
    {
        return voxels[coords.x, coords.y, coords.z];
    }

    public override VoxelData GetVoxelAtLocalCoordinates(int x, int y, int z)
    {
        return voxels[x, y, z];
    }

    public override void SetVoxelAtLocalCoordinates(Vector3Int coords, VoxelData voxel)
    {
        voxels[coords.x, coords.y, coords.z] = voxel;
    }

    public override void SetVoxelAtLocalCoordinates(int x, int y, int z, VoxelData voxel)
    {
        voxels[x, y, z] = voxel;
    }

}
