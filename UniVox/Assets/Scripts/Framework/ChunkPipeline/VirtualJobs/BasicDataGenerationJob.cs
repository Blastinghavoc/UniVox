using UnityEngine;

namespace UniVox.Framework.ChunkPipeline.VirtualJobs
{
    public class BasicDataGenerationJob<ChunkDataType, VoxelDataType> : AbstractPipelineJob<ChunkDataType>
            where ChunkDataType : IChunkData<VoxelDataType>
            where VoxelDataType : IVoxelData
    {
        IChunkProvider<ChunkDataType, VoxelDataType> chunkProvider;
        Vector3Int chunkID;

        public BasicDataGenerationJob(IChunkProvider<ChunkDataType,VoxelDataType> chunkProvider,Vector3Int chunkID) 
        {
            this.chunkProvider = chunkProvider;
            this.chunkID = chunkID;
        }

        public override void Start()
        {
            Result = chunkProvider.ProvideChunkData(chunkID);
            Done = true;
        }
    }
}