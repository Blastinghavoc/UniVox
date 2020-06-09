using UnityEngine;
using UnityEngine.Assertions;

namespace UniVox.Framework.ChunkPipeline.VirtualJobs
{
    public class BasicCollisionMeshingJob<ChunkDataType, VoxelDataType> : AbstractPipelineJob<Mesh>
            where ChunkDataType : IChunkData<VoxelDataType>
            where VoxelDataType : IVoxelData
    {
        IChunkComponent<ChunkDataType, VoxelDataType> chunkComponent;

        public BasicCollisionMeshingJob(IChunkComponent<ChunkDataType,VoxelDataType> chunkComponent) 
        {
            this.chunkComponent = chunkComponent;
        }

        public override void Start()
        {
            //Using the render mesh as the collision mesh
            Result = chunkComponent.GetRenderMesh();
            Done = true;
        }
    }
}