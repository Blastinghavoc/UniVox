using UnityEngine;
using System.Collections;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Framework.Jobified;

namespace UniVox.Framework
{
    public interface IChunkProvider:IHeightMapProvider
    {
        ///Stores modified chunk data
        void StoreModifiedChunkData(Vector3Int chunkID, IChunkData data);

        bool TryGetStoredDataForChunk(Vector3Int chunkID,out IChunkData storedData);

        ///Returns a pipeline job that provides terrain data for a chunk.
        ///This means everything except structures, i.e anything that can be done
        ///without access to neighbouring chunks.
        AbstractPipelineJob<IChunkData> GenerateTerrainData(Vector3Int chunkID);

        /// <summary>
        /// Returns a pipeline job that provides structure data for a chunk id, and may also modifiy
        /// the data of neighbouring chunks in doing so.
        /// </summary>
        /// <param name="centerChunkID"></param>
        /// <param name="neighbourhood"></param>
        /// <returns></returns>
        AbstractPipelineJob<ChunkNeighbourhood> GenerateStructuresForNeighbourhood(Vector3Int centerChunkID,ChunkNeighbourhood neighbourhood);

        void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager,FrameworkEventManager eventManager);

    }

    public interface IHeightMapProvider 
    {
        int[] GetHeightMapForColumn(Vector2Int columnId);
    }
}