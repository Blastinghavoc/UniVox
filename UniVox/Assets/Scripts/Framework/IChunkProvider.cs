using UnityEngine;
using System.Collections;

namespace UniVox.Framework
{
    public interface IChunkProvider<ChunkDataType, V> where ChunkDataType : IChunkData<V> where V : IVoxelData
    {
        /// <summary>
        /// Provides data for the chunk with given ID
        /// </summary>
        /// <param name="chunkID"></param>
        ChunkDataType ProvideChunkData(Vector3Int chunkID);
    }
}