using UnityEngine;

namespace UniVox.Framework
{
    /// <summary>
    /// Interface for any implementation of per-voxel data storage.
    /// </summary>
    public interface IChunkStorageImplementation<T>
    {
        void InitialiseEmpty(Vector3Int dimensions);

        void InitialiseWithData(Vector3Int dimensions, T[] initialData);

        T Get(int x, int y, int z);
        void Set(int x, int y, int z, T typeID);

        T[] ToArray();
    }
}