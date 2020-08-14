using UnityEngine;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework
{
    public interface IChunkMesher
    {
        bool CullFaces { get; }

        AbstractPipelineJob<MeshDescriptor> CreateMeshJob(Vector3Int chunkID);
        AbstractPipelineJob<Mesh> ApplyCollisionMeshJob(Vector3Int chunkID);
        void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager, FrameworkEventManager eventManager);
    }
}