using UnityEngine;
using System.Collections;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework
{
    public interface IChunkProvider<V> where V :struct, IVoxelData
    {
        ///Returns a pipeline job that provides chunk data.
        AbstractPipelineJob<IChunkData<V>> ProvideChunkDataJob(Vector3Int chunkID);
    }
}