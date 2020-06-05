using UnityEngine;
using System.Collections;
using UniVox.Framework;
using UniVox.Implementations.Common;

namespace UniVox.Implementations.ChunkData
{

    /// <summary>
    /// Abstract implementation of IChunkData, providing helpful indexers
    /// and the basic data required by the interface.
    /// </summary>
    public abstract class AbstractChunkData : IChunkData<VoxelData>
    {
        public Vector3Int ChunkID { get; set; }
        public Vector3Int Dimensions { get; set; }

        public AbstractChunkData(Vector3Int ID, Vector3Int chunkDimensions)
        {
            ChunkID = ID;
            Dimensions = chunkDimensions;
        }

        #region Indexers
        public VoxelData this[Vector3Int index]
        {
            get { return GetVoxelAtLocalCoordinates(index); }
            set { SetVoxelAtLocalCoordinates(index, value); }
        }
        public VoxelData this[int i, int j, int k]
        {
            get { return GetVoxelAtLocalCoordinates(i, j, k); }
            set { SetVoxelAtLocalCoordinates(i, j, k, value); }
        }
        #endregion

        #region Abstract implementation of interface methods
        public abstract void SetVoxelAtLocalCoordinates(Vector3Int coords, VoxelData voxel);

        public abstract void SetVoxelAtLocalCoordinates(int x, int y, int z, VoxelData voxel);

        public abstract VoxelData GetVoxelAtLocalCoordinates(Vector3Int coords);

        public abstract VoxelData GetVoxelAtLocalCoordinates(int x, int y, int z);

        public bool TryGetVoxelAtLocalCoordinates(Vector3Int coords, out VoxelData vox)
        {
            return TryGetVoxelAtLocalCoordinates(coords.x, coords.y, coords.z, out vox);
        }

        public bool TryGetVoxelAtLocalCoordinates(int x, int y, int z, out VoxelData vox)
        {
            bool xValid = x >= 0 && x < Dimensions.x;
            bool yValid = y >= 0 && y < Dimensions.y;
            bool zValid = z >= 0 && z < Dimensions.z;

            if (xValid && yValid && zValid)
            {
                vox = GetVoxelAtLocalCoordinates(x, y, z);
                return true;
            }
            vox = default;
            return false;
        }
        #endregion
    }
}