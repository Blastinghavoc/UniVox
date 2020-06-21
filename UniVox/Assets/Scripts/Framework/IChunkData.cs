using UnityEngine;
using System.Collections;
using System;
using Unity.Collections;

namespace UniVox.Framework
{

    /// <summary>
    /// The data representation of a Chunk
    /// </summary>
    public interface IChunkData<V>
        where V : struct, IVoxelData
    {
        Vector3Int ChunkID { get; set; }

        Vector3Int Dimensions { get; set; }

        bool ModifiedSinceGeneration { get; set; }

        bool FullyGenerated { get; set; }

        V this[int i, int j, int k] { get; set; }
        V this[Vector3Int index] { get; set; }

        bool TryGetVoxelAtLocalCoordinates(Vector3Int coords, out V vox);
        bool TryGetVoxelAtLocalCoordinates(int x, int y, int z, out V vox);

        /// <summary>
        /// Essentially a "lazy constructor" that forms part of the interface,
        /// so that it can be accessed generically
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="chunkDimensions"></param>
        public void Initialise(Vector3Int ID,Vector3Int chunkDimensions);

        NativeArray<V> ToNative(Allocator allocator = Allocator.Persistent);
        void FromNative(NativeArray<V> native);
    }
}