using UnityEngine;
using System.Collections;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework
{
    public interface IChunkProvider
    {
        ///Stores modified chunk data
        void StoreModifiedChunkData(Vector3Int chunkID, IChunkData data);

        ///Returns a pipeline job that provides chunk data.
        AbstractPipelineJob<IChunkData> ProvideChunkDataJob(Vector3Int chunkID);
        void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager);
    }
}