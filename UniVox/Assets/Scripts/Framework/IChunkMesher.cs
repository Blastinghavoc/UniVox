using UnityEngine;
using System.Collections;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework
{
    public interface IChunkMesher<V> where V :struct, IVoxelData
    {
        bool IsMeshDependentOnNeighbourChunks { get; }

        AbstractPipelineJob<Mesh> CreateMeshJob(Vector3Int chunkID);
    }
}