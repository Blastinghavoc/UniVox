using UnityEngine;
using System.Collections;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework
{
    public interface IChunkMesher
    {
        bool IsMeshDependentOnNeighbourChunks { get; }

        AbstractPipelineJob<MeshDescriptor> CreateMeshJob(Vector3Int chunkID);
        AbstractPipelineJob<Mesh> ApplyCollisionMeshJob(Vector3Int chunkID);
        void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager);
    }
}