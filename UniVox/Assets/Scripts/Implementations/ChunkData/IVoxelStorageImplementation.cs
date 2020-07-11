using UnityEngine;
using UniVox.Framework;

namespace UniVox.Implementations.ChunkData
{
    /// <summary>
    /// Interface for any implementation of voxel storage.
    /// </summary>
    public interface IVoxelStorageImplementation 
    {
        void InitialiseEmpty(Vector3Int dimensions);

        void InitialiseWithData(Vector3Int dimensions, VoxelTypeID[] initialData);

        VoxelTypeID Get(int x, int y, int z);
        void Set(int x, int y, int z,VoxelTypeID typeID);

        VoxelTypeID[] ToArray();
    }
}