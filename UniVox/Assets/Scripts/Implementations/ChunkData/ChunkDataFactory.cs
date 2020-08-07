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
            Octree,
            RLE
        }

        public ChunkDataType typeToCreate;
        public bool lazy = false;

        public IChunkData Create(Vector3Int chunkID,Vector3Int chunkDimensions , VoxelTypeID[] initialData = null) 
        {
            switch (typeToCreate)
            {
                case ChunkDataType.FlatArray:
                    if (lazy)
                    {
                        return new LazyChunkData<FlatArrayStorage<VoxelTypeID>>(chunkID, chunkDimensions, initialData);
                    }
                    return new FlatArrayChunkData(chunkID, chunkDimensions, initialData);
                case ChunkDataType.MultiArray:
                    if (lazy)
                    {
                        return new LazyChunkData<MultiDimensionalArrayVoxelStorage>(chunkID, chunkDimensions, initialData);
                    }
                    return new ArrayChunkData(chunkID, chunkDimensions, initialData);
                case ChunkDataType.Octree:
                    if (lazy)
                    {
                        return new LazyChunkData<OctreeVoxelStorage>(chunkID, chunkDimensions, initialData);
                    }
                    return new OctreeChunkData(chunkID, chunkDimensions, initialData);
                case ChunkDataType.RLE:
                    if (lazy)
                    {
                        return new LazyChunkData<RLEVoxelStorage>(chunkID, chunkDimensions, initialData);
                    }
                    return new RLEChunkData(chunkID, chunkDimensions, initialData);
                default:
                    throw new System.Exception($"No definition exists for chunk data type {typeToCreate}");
            }
        }

    }
}