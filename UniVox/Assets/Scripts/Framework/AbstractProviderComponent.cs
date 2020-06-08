using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace UniVox.Framework
{
    public abstract class AbstractProviderComponent<ChunkDataType, VoxelDataType> : MonoBehaviour, IChunkProvider<ChunkDataType, VoxelDataType>
        where ChunkDataType : IChunkData<VoxelDataType>
        where VoxelDataType : IVoxelData
    {
        protected VoxelTypeManager voxelTypeManager;
        protected IChunkManager chunkManager;

        /// <summary>
        /// Chunk Data for chunks that are not active, but have been modified.
        /// If a request is made to provide any of these chunks, the modified
        /// data must be returned.
        /// </summary>
        protected Dictionary<Vector3Int, ChunkDataType> ModifiedChunkData = new Dictionary<Vector3Int, ChunkDataType>();

        public virtual void Initialise(VoxelTypeManager voxelTypeManager,IChunkManager chunkManager)
        {
            this.voxelTypeManager = voxelTypeManager;
            this.chunkManager = chunkManager;
        }

        //Add or replace modified data for the given chunk
        public void AddModifiedChunkData(Vector3Int chunkID, ChunkDataType data) 
        {
            ModifiedChunkData[chunkID] = data;
        }

        public ChunkDataType ProvideChunkData(Vector3Int chunkID) 
        {
            if (ModifiedChunkData.TryGetValue(chunkID,out var data))
            {
                return data;
            }
            data = GenerateChunkData(chunkID, chunkManager.ChunkDimensions);
            data.FullyGenerated = true;
            return data;
        }

        public abstract ChunkDataType GenerateChunkData(Vector3Int chunkID, Vector3Int chunkDimensions);
    }
}