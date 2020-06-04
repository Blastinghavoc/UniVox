using UnityEngine;
using System.Collections;
using System;
using UnityEngine.UIElements;

/// <summary>
/// Global interface to the voxel world
/// </summary>
public class VoxelWorldInterface : MonoBehaviour
{
    protected IChunkManager chunkManager;

    public void Intialise(IChunkManager manager) 
    {
        chunkManager = manager;
    }

    public void PlaceVoxel(Vector3 position, ushort voxelTypeID)
    {
        chunkManager.TrySetVoxel(position, voxelTypeID);
    }

    public void RemoveVoxel(Vector3 position)
    {
        chunkManager.TrySetVoxel(position, 0,true);
    }

    public Vector3 CenterOfVoxelAt(Vector3 position) 
    {
        return chunkManager.SnapToVoxelCenter(position);
    }
}
