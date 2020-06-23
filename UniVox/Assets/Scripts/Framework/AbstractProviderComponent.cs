using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UniVox.Implementations.ChunkData;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using System;
using UnityEngine.Profiling;

namespace UniVox.Framework
{
    public abstract class AbstractProviderComponent<V> : MonoBehaviour, IChunkProvider<V>
        where V : struct,IVoxelData
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

        public AbstractPipelineJob<IChunkData<V>> ProvideChunkDataJob(Vector3Int chunkID) 
        {
            if (ModifiedChunkData.TryGetValue(chunkID, out var data))
            {
                return new BasicFunctionJob<IChunkData<V>>(() => data);
            }

            Profiler.BeginSample("CreateGenerationJob");
            var tmp = GenerateChunkDataJob(chunkID, chunkManager.ChunkDimensions);
            Profiler.EndSample();

            return tmp;
        }

        /// <summary>
        /// To be implemented by derived classes, returning a pipeline job to generatie the chunk data.
        /// </summary>
        /// <param name="chunkID"></param>
        /// <param name="chunkDimensions"></param>
        /// <returns></returns>
        public abstract AbstractPipelineJob<IChunkData<V>> GenerateChunkDataJob(Vector3Int chunkID, Vector3Int chunkDimensions);
    }
}