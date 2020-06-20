using UnityEngine;
using System.Collections;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework
{
    public interface IChunkProvider<V> where V :struct, IVoxelData
    {
        /// <summary>
        /// Provides data for the chunk with given ID
        /// </summary>
        /// <param name="chunkID"></param>
        IChunkData<V> ProvideChunkData(Vector3Int chunkID);
        AbstractPipelineJob<IChunkData<V>> ProvideChunkDataJob(Vector3Int chunkID);
    }
}