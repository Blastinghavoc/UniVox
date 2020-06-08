using UnityEngine;

namespace UniVox.Framework.ChunkPipeline.VirtualJobs
{
    public class BasicCollisionMeshingJob<ChunkDataType, VoxelDataType> : AbstractPipelineJob<Mesh>
            where ChunkDataType : IChunkData<VoxelDataType>
            where VoxelDataType : IVoxelData
    {
        AbstractChunkComponent<ChunkDataType, VoxelDataType> chunkComponent;

        public BasicCollisionMeshingJob(AbstractChunkComponent<ChunkDataType,VoxelDataType> chunkComponent) 
        {
            this.chunkComponent = chunkComponent;
        }

        public override void Start()
        {
            Result = chunkComponent.meshFilter.mesh;
            Done = true;
        }
    }
}