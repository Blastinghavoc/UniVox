using Unity.Collections;
using UnityEngine;
using UniVox.Framework;
using Utils;

namespace UniVox.Implementations.ChunkData
{
    public class LazyChunkData<StorageImplementation> : AbstractChunkData, IChunkStorageOwner<VoxelTypeID> where StorageImplementation : IChunkStorageImplementation<VoxelTypeID>, new()
    {
        private IChunkStorageImplementation<VoxelTypeID> storage;
        public LazyChunkData(Vector3Int ID, Vector3Int chunkDimensions, VoxelTypeID[] initialData = null) : base(ID, chunkDimensions, initialData)
        {

            if (initialData != null)
            {
                storage = new StorageImplementation();
                storage.InitialiseWithData(chunkDimensions, initialData);
            }
            else
            {
                storage = new LazyStorageImplementation<LazyChunkData<StorageImplementation>, VoxelTypeID>(this);
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

        public void InitialiseEmptyStorage()
        {
            storage = new StorageImplementation();
            storage.InitialiseEmpty(Dimensions);
        }

        public void Set(int x, int y, int z, VoxelTypeID item)
        {
            SetVoxelID(x, y, z, item);
        }
    }
}