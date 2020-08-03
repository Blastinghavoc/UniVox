using UnityEngine;
using System.Collections;
using UnityEngine.Assertions;
using UniVox.Framework;
using Unity.Collections;

namespace UniVox.Implementations.ChunkData
{

    /// <summary>
    /// ChunkData class storing the data in a 3D array.
    /// </summary>
    public class ArrayChunkData : AbstractChunkData
    {
        MultiDimensionalArrayVoxelStorage storage;

        public ArrayChunkData(Vector3Int ID, Vector3Int chunkDimensions,VoxelTypeID[] initialData = null) : base(ID, chunkDimensions,initialData) 
        {
            storage = new MultiDimensionalArrayVoxelStorage();
            if (initialData == null)
            {
                storage.InitialiseEmpty(chunkDimensions);
            }
            else
            {
                storage.InitialiseWithData(chunkDimensions,initialData);
            }
        }

        protected override VoxelTypeID[] GetVoxelArray()
        {
            return storage.ToArray();
        }

        protected override VoxelTypeID GetVoxelID(int x, int y, int z)
        {
            return storage.Get(x, y, z);
        }

        protected override void SetVoxelID(int x, int y, int z, VoxelTypeID voxel)
        {
            storage.Set(x, y, z, voxel);
        }
    }
}