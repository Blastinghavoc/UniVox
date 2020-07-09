using UnityEngine;
using UniVox.Framework;

namespace UniVox.Implementations.ChunkData
{
    /// <summary>
    /// Class to encapsulate instantiating different ChunkData implementations.
    /// </summary>
    [System.Serializable]
    public class ChunkDataFactory
    {
        public enum ChunkDataType 
        { 
            FlatArray,
            MultiArray,
            SVO,
            RLE
        }

        public ChunkDataType typeToCreate;

        public IChunkData Create(Vector3Int chunkID,Vector3Int chunkDimensions , VoxelTypeID[] initialData = null) 
        {
            switch (typeToCreate)
            {
                case ChunkDataType.FlatArray:
                    return new FlatArrayChunkData(chunkID, chunkDimensions, initialData);
                case ChunkDataType.MultiArray:
                    return new ArrayChunkData(chunkID, chunkDimensions, initialData);
                case ChunkDataType.SVO:
                    return new SVOChunkData(chunkID, chunkDimensions, initialData);
                case ChunkDataType.RLE:
                    return new RLEChunkData(chunkID, chunkDimensions, initialData);
                default:
                    throw new System.Exception($"No definition exists for chunk data type {typeToCreate}");
            }
        }

    }
}