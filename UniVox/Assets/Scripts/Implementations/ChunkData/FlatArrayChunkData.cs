using Unity.Collections;
using UnityEngine;
using UniVox.Framework;
using Utils;

namespace UniVox.Implementations.ChunkData
{
    /// <summary>
    /// Similar to arrayChunkData, but implented with a flat array.
    /// This allows it to be easily constructed from the result of a job.
    /// </summary>
    public class FlatArrayChunkData : AbstractChunkData
    {
        protected FlatArrayStorage<VoxelTypeID> storage;

        public FlatArrayChunkData(Vector3Int ID, Vector3Int chunkDimensions, VoxelTypeID[] initialData = null) : base(ID, chunkDimensions, initialData)
        {
            storage = new FlatArrayStorage<VoxelTypeID>();
            if (initialData == null)
            {
                storage.InitialiseEmpty(chunkDimensions);
            }
            else
            {
                storage.InitialiseWithData(chunkDimensions, initialData);
            }
        }

        protected override VoxelTypeID GetVoxelID(int x, int y, int z)
        {
            return storage.Get(x, y, z);
        }

        protected override void SetVoxelID(int x, int y, int z, VoxelTypeID voxel)
        {
            storage.Set(x, y, z, voxel);
        }

        protected override VoxelTypeID[] GetVoxelArray()
        {
            return storage.ToArray();
        }

        public override NativeArray<VoxelTypeID> ToNative(Allocator allocator = Allocator.Persistent)
        {
            return storage.ToArray().ToNative(allocator);
        }
    }
}