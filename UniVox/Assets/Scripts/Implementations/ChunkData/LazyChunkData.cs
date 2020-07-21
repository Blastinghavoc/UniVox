using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework;
using Utils;

namespace UniVox.Implementations.ChunkData
{
    public class LazyChunkData<T> : AbstractChunkData where T: IVoxelStorageImplementation,new()
    {
        private IVoxelStorageImplementation storage;
        public LazyChunkData(Vector3Int ID, Vector3Int chunkDimensions, VoxelTypeID[] initialData = null) : base(ID, chunkDimensions, initialData)
        {
            
            if (initialData != null)
            {
                storage = new T();
                storage.InitialiseWithData(chunkDimensions,initialData);
            }
            else
            {
                storage = new LazyStorageImplementation(this);
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

        public override NativeArray<VoxelTypeID> ToNative(Allocator allocator = Allocator.Persistent)
        {
            return storage.ToArray().ToNative(allocator);
        }

        /// <summary>
        /// This storage implementation efficiently represents empty storage, and
        /// lazily initialises real storage only if an attempt is made to write to the data.
        /// </summary>
        private class LazyStorageImplementation : IVoxelStorageImplementation
        {
            LazyChunkData<T> owner;
            public LazyStorageImplementation(LazyChunkData<T> owner) 
            {
                this.owner = owner;
            }

            public VoxelTypeID Get(int x, int y, int z)
            {
                return (VoxelTypeID)VoxelTypeID.AIR_ID;
            }

            public void InitialiseEmpty(Vector3Int dimensions)
            {
                throw new System.NotImplementedException();
            }

            public void InitialiseWithData(Vector3Int dimensions, VoxelTypeID[] initialData)
            {
                throw new System.NotImplementedException();
            }

            /// <summary>
            /// Setting a voxel causes the real storage to be initialised.
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="z"></param>
            /// <param name="typeID"></param>
            public void Set(int x, int y, int z, VoxelTypeID typeID)
            {
                owner.storage = new T();
                owner.storage.InitialiseEmpty(owner.Dimensions);
                owner.storage.Set(x, y, z, typeID);
            }


            public VoxelTypeID[] ToArray()
            {
                return new VoxelTypeID[owner.Dimensions.x * owner.Dimensions.y * owner.Dimensions.z];
            }
        }
    }
}