using UnityEngine;
using System.Collections;
using Unity.Collections;

namespace UniVox.Framework
{
    public class ReadOnlyChunkData<V> : IChunkData<V>
        where V :struct, IVoxelData
    {
        private IChunkData<V> realData;

        public ReadOnlyChunkData(IChunkData<V> realData)
        {
            this.realData = realData;
        }

        public V this[Vector3Int index] { get => realData[index]; set => throw new System.NotImplementedException(); }
        public V this[int i, int j, int k] { get => realData[i,j,k]; set => throw new System.NotImplementedException(); }

        public Vector3Int ChunkID { get => realData.ChunkID; set => throw new System.NotImplementedException(); }
        public Vector3Int Dimensions { get => realData.Dimensions; set => throw new System.NotImplementedException(); }
        public bool ModifiedSinceGeneration { get => realData.ModifiedSinceGeneration; set => throw new System.NotImplementedException(); }
        public bool FullyGenerated { get => realData.FullyGenerated; set => throw new System.NotImplementedException(); }

        public NativeArray<V> ToNative(Allocator allocator = Allocator.Persistent)
        {
            return realData.ToNative(allocator);
        }

        public bool TryGetVoxelAtLocalCoordinates(Vector3Int coords, out V vox)
        {
            return realData.TryGetVoxelAtLocalCoordinates(coords, out vox);
        }

        public bool TryGetVoxelAtLocalCoordinates(int x, int y, int z, out V vox)
        {
            return realData.TryGetVoxelAtLocalCoordinates(x,y,z, out vox);
        }
    }
}