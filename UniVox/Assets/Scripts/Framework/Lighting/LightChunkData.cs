using Unity.Collections;
using UnityEngine;
using UniVox.Implementations.ChunkData;
using Utils;

namespace UniVox.Framework.Lighting
{
    public class LightChunkData : IChunkStorageOwner<LightValue>
    {
        private IChunkStorageImplementation<LightValue> storage;
        public Vector3Int Dimensions { get; set; }

        public LightValue this[int x, int y, int z]
        {
            get { return storage.Get(x, y, z); }
            set { storage.Set(x, y, z, value); }
        }

        public LightChunkData(Vector3Int dimensions, LightValue[] initialData = null)
        {
            Dimensions = dimensions;
            if (initialData != null)
            {
                storage = new FlatArrayStorage<LightValue>();
                storage.InitialiseWithData(dimensions, initialData);
            }
            else
            {
                //Lazy storage implementation for reduce memory usage
                storage = new LazyStorageImplementation<LightChunkData, LightValue>(this);
            }
        }

        public NativeArray<LightValue> ToNative(Allocator allocator = Allocator.Persistent)
        {
            return storage.ToArray().ToNative(allocator);
        }

        public void InitialiseEmptyStorage()
        {
            storage = new FlatArrayStorage<LightValue>();
            storage.InitialiseEmpty(Dimensions);
        }

        public void Set(int x, int y, int z, LightValue item)
        {
            storage.Set(x, y, z, item);
        }
    }
}