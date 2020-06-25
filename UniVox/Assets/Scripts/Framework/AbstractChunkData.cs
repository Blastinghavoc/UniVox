using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UniVox.Framework.Jobified;

namespace UniVox.Framework
{

    /// <summary>
    /// Abstract implementation of IChunkData, providing helpful indexers
    /// and the basic data required by the interface.
    /// </summary>
    public abstract class AbstractChunkData : IChunkData
    {
        public Vector3Int ChunkID { get; set; }
        public Vector3Int Dimensions { get; set; }
        public bool ModifiedSinceGeneration { get; set; } = false;

        public bool FullyGenerated { get; set; } = false;

        /// <summary>
        /// Store flattended indices of voxels that have a non-default rotation.
        /// Efficient only under the assumption that there are relatively few such voxels in the chunk
        /// </summary>
        protected Dictionary<int, VoxelRotation> rotatedVoxels;

        public AbstractChunkData(Vector3Int ID, Vector3Int chunkDimensions, VoxelTypeID[] initialData = null)
        {
            ChunkID = ID;
            Dimensions = chunkDimensions;
            rotatedVoxels = new Dictionary<int, VoxelRotation>();
            if (initialData != null)
            {
                var expectedLength = chunkDimensions.x * chunkDimensions.y * chunkDimensions.z;
                if (initialData.Length != expectedLength)
                {
                    throw new ArgumentException($"Initial data array length {expectedLength} does not match given dimensions {chunkDimensions}" +
                        $" with total size {expectedLength}");
                }
            }
        }


        #region Indexers
        public VoxelTypeID this[Vector3Int index]
        {
            get { return this[index.x, index.y, index.z]; }
            set { this[index.x, index.y, index.z] = value; }
        }
        public VoxelTypeID this[int i, int j, int k]
        {
            get
            {
                return GetVoxelID(i, j, k);
            }
            set
            {
                SetVoxelID(i, j, k, value);
                if (FullyGenerated)
                {
                    ModifiedSinceGeneration = true;
                }
            }
        }
        #endregion
        protected abstract void SetVoxelID(int x, int y, int z, VoxelTypeID voxel);

        protected abstract VoxelTypeID GetVoxelID(int x, int y, int z);

        #region interface methods

        public bool TryGetVoxelID(Vector3Int coords, out VoxelTypeID vox)
        {
            return TryGetVoxelID(coords.x, coords.y, coords.z, out vox);
        }

        public bool TryGetVoxelID(int x, int y, int z, out VoxelTypeID vox)
        {
            bool xValid = x >= 0 && x < Dimensions.x;
            bool yValid = y >= 0 && y < Dimensions.y;
            bool zValid = z >= 0 && z < Dimensions.z;

            if (xValid && yValid && zValid)
            {
                vox = GetVoxelID(x, y, z);
                return true;
            }
            vox = default;
            return false;
        }

        public virtual NativeArray<VoxelTypeID> ToNative(Allocator allocator = Allocator.Persistent)
        {
            return this.ToNativeBruteForce(allocator);
        }

        public NativeArray<KeyValuePair<int, VoxelRotation>> NativeRotations(Allocator allocator = Allocator.Persistent) 
        {
            //This conversion from dictionary to array is only efficient under the assumption that there will be few rotated voxels per chunk
            NativeArray<KeyValuePair<int, VoxelRotation>> rotations = new NativeArray<KeyValuePair<int, VoxelRotation>>(rotatedVoxels.ToList().ToArray(),allocator);
            return rotations;
        }
        #endregion
    }
}