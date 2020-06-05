using UnityEngine;
using System.Collections;

namespace UniVox.Framework
{
    public abstract class AbstractProviderComponent<ChunkDataType, VoxelDataType> : MonoBehaviour, IChunkProvider<ChunkDataType, VoxelDataType>
        where ChunkDataType : IChunkData<VoxelDataType>
        where VoxelDataType : IVoxelData
    {
        protected VoxelTypeManager voxelTypeManager;

        public virtual void Initialise(VoxelTypeManager voxelTypeManager)
        {
            this.voxelTypeManager = voxelTypeManager;
        }

        public abstract ChunkDataType ProvideChunkData(Vector3Int chunkID, Vector3Int chunkDimensions);
    }
}