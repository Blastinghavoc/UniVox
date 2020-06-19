using UnityEngine;
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
        public bool ModifiedSinceGeneration { get; set; } = false;

        public bool FullyGenerated { get; set; } = false;

        public AbstractChunkData(Vector3Int ID, Vector3Int chunkDimensions)
        {
            ChunkID = ID;
            Dimensions = chunkDimensions;
        }

        #region Indexers
        public VoxelData this[Vector3Int index]
        {
            get { return this[index.x, index.y, index.z]; }
            set { this[index.x, index.y, index.z] = value; }
        }
        public VoxelData this[int i, int j, int k]
        {
            get { 
                return GetVoxelAtLocalCoordinates(i, j, k); 
            }
            set { 
                SetVoxelAtLocalCoordinates(i, j, k, value);
                if (FullyGenerated)
                {
                    ModifiedSinceGeneration = true;
                }
            }
        }
        #endregion
        protected abstract void SetVoxelAtLocalCoordinates(int x, int y, int z, VoxelData voxel);

        protected abstract VoxelData GetVoxelAtLocalCoordinates(int x, int y, int z);

        #region Abstract implementation of interface methods

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

        public virtual void Dispose()
        {
            //Nothing to dispose, but subclasses may need this
        }
        #endregion
    }
}