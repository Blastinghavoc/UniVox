using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UniVox.Implementations.ChunkData;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using System;

namespace UniVox.Framework
{
    public abstract class AbstractProviderComponent<V> : MonoBehaviour, IChunkProvider<V>
        where V : IVoxelData
    {
        protected VoxelTypeManager voxelTypeManager;
        protected IChunkManager chunkManager;

        /// <summary>
        /// Chunk Data for chunks that are not active, but have been modified.
        /// If a request is made to provide any of these chunks, the modified
        /// data must be returned.
        /// </summary>
        protected Dictionary<Vector3Int, IChunkData<V>> ModifiedChunkData = new Dictionary<Vector3Int, IChunkData<V>>();

        public virtual void Initialise(VoxelTypeManager voxelTypeManager,IChunkManager chunkManager)
        {
            this.voxelTypeManager = voxelTypeManager;
            this.chunkManager = chunkManager;
        }

        //Add or replace modified data for the given chunk
        public void AddModifiedChunkData(Vector3Int chunkID, IChunkData<V> data) 
        {
            ModifiedChunkData[chunkID] = data;
        }

        public IChunkData<V> ProvideChunkData(Vector3Int chunkID) 
        {
            if (ModifiedChunkData.TryGetValue(chunkID,out var data))
            {
                return data;
            }           

            data = GenerateChunkData(chunkID, chunkManager.ChunkDimensions);
            data.FullyGenerated = true;
            return data;
        }

        public abstract IChunkData<V> GenerateChunkData(Vector3Int chunkID, Vector3Int chunkDimensions);

        public AbstractPipelineJob<IChunkData<V>> ProvideChunkDataJob(Vector3Int chunkID) 
        {
            if (ModifiedChunkData.TryGetValue(chunkID, out var data))
            {
                return new BasicFunctionJob<IChunkData<V>>(() => data);
            }

            return GenerateChunkDataJob(chunkID, chunkManager.ChunkDimensions);
        }

        public virtual AbstractPipelineJob<IChunkData<V>> GenerateChunkDataJob(Vector3Int chunkID, Vector3Int chunkDimensions) 
        {
            return new BasicFunctionJob<IChunkData<V>>(()=>GenerateChunkData(chunkID,chunkDimensions));
        }
    }
}