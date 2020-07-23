using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UniVox.Implementations.ChunkData;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using System;
using UnityEngine.Profiling;
using UniVox.Framework.Jobified;

namespace UniVox.Framework
{
    public abstract class AbstractProviderComponent : MonoBehaviour, IChunkProvider
    {
        protected VoxelTypeManager voxelTypeManager;
        protected IChunkManager chunkManager;

        //TODO remove, testing only
        public bool Parrallel = true;

        /// <summary>
        /// Chunk Data for chunks that are not active, but have been modified.
        /// If a request is made to provide any of these chunks, the modified
        /// data must be returned.
        /// </summary>
        protected Dictionary<Vector3Int, IChunkData> ModifiedChunkData = new Dictionary<Vector3Int, IChunkData>();

        protected FrameworkEventManager eventManager;

        public virtual void Initialise(VoxelTypeManager voxelTypeManager,IChunkManager chunkManager, FrameworkEventManager eventManager)
        {
            this.voxelTypeManager = voxelTypeManager;
            this.chunkManager = chunkManager;
            this.eventManager = eventManager;
        }

        //Add or replace modified data for the given chunk
        public void StoreModifiedChunkData(Vector3Int chunkID, IChunkData data) 
        {
            ModifiedChunkData[chunkID] = data;
        }

        public bool TryGetStoredDataForChunk(Vector3Int chunkID, out IChunkData storedData)
        {
            if (ModifiedChunkData.TryGetValue(chunkID, out var data))
            {
                storedData = data;
                return true;
            }
            storedData = null;
            return false;
        }

        /// <summary>
        /// To be implemented by derived classes, returning a pipeline job to generatie the chunk data.
        /// </summary>
        /// <param name="chunkID"></param>
        /// <param name="chunkDimensions"></param>
        /// <returns></returns>
        public abstract AbstractPipelineJob<IChunkData> GenerateTerrainData(Vector3Int chunkID);

        public abstract AbstractPipelineJob<ChunkNeighbourhood> GenerateStructuresForNeighbourhood(Vector3Int centerChunkID, ChunkNeighbourhood neighbourhood);

        public abstract int[] GetHeightMapForColumn(Vector2Int columnId);
    }
}