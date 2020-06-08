using UnityEngine;
using UniVox.Framework;

namespace UniVox.Framework.ChunkPipeline.VirtualJobs
{
    public class BasicMeshGenerationJob<ChunkDataType, VoxelDataType> : AbstractPipelineJob<Mesh>
            where ChunkDataType : IChunkData<VoxelDataType>
            where VoxelDataType : IVoxelData
    {
        IChunkMesher<ChunkDataType, VoxelDataType> chunkMesher;
        ChunkDataType data;

        public BasicMeshGenerationJob(IChunkMesher<ChunkDataType, VoxelDataType> chunkMesher, ChunkDataType data)
        {
            this.chunkMesher = chunkMesher;
            this.data = data;
        }

        public override void Start()
        {
            Result = chunkMesher.CreateMesh(data);
            Done = true;
        }
    }
}