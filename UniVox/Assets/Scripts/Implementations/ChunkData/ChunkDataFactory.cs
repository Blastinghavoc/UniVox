﻿using UnityEngine;
using UniVox.Framework;
using UniVox.Implementations.Common;

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
            MultiArray
        }

        public ChunkDataType typeToCreate;

        public IChunkData<VoxelData> Create(Vector3Int chunkID,Vector3Int chunkDimensions , VoxelData[] initialData = null) 
        {
            switch (typeToCreate)
            {
                case ChunkDataType.FlatArray:
                    return new FlatArrayChunkData(chunkID, chunkDimensions, initialData);
                case ChunkDataType.MultiArray:
                    return new ArrayChunkData(chunkID, chunkDimensions, initialData);
                default:
                    throw new System.Exception($"No definition exists for chunk data type {typeToCreate.ToString()}");
            }
        }

    }
}