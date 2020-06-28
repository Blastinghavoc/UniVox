using UnityEngine;
using System.Collections;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework
{
    public interface IChunkProvider
    {
        ///Stores modified chunk data
        void StoreModifiedChunkData(Vector3Int chunkID, IChunkData data);

        ///Returns a pipeline job that provides terrain data for a chunk.
        ///This means everything except structures, i.e anything that can be done
        ///without access to neighbouring chunks.
        AbstractPipelineJob<IChunkData> ProvideTerrainData(Vector3Int chunkID);



        void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager,FrameworkEventManager eventManager);
    }
}