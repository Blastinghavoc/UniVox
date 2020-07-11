using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UniVox.Framework.Common;

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
        protected Dictionary<int, RotatedVoxelEntry> rotatedVoxels;

        public AbstractChunkData(Vector3Int ID, Vector3Int chunkDimensions, VoxelTypeID[] initialData = null)
        {
            ChunkID = ID;
            Dimensions = chunkDimensions;
            rotatedVoxels = new Dictionary<int, RotatedVoxelEntry>();
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

        protected NativeArray<VoxelTypeID> ToNativeBruteForce(Allocator allocator = Allocator.Persistent)
        {
            //Copy chunk data to native array
            NativeArray<VoxelTypeID> voxels = new NativeArray<VoxelTypeID>(Dimensions.x * Dimensions.y * Dimensions.z, allocator);

            int i = 0;
            for (int z = 0; z < Dimensions.z; z++)
            {
                for (int y = 0; y < Dimensions.y; y++)
                {
                    for (int x = 0; x < Dimensions.x; x++)
                    {
                        voxels[i] = this[x, y, z];

                        i++;
                    }
                }
            }

            return voxels;
        }

        /// <summary>
        /// Creates a native array representing the border of the chunk data in the given direction.
        /// E.g, with Direction = UP creates a flattened 2D array of all blocks on the top border
        /// of the chunk
        /// </summary>
        /// <typeparam name="V"></typeparam>
        /// <param name="chunkData"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        public NativeArray<VoxelTypeID> BorderToNative(Direction dir)
        {
            StartEndRange xRange = new StartEndRange() { start = 0, end = Dimensions.x };
            StartEndRange yRange = new StartEndRange() { start = 0, end = Dimensions.y };
            StartEndRange zRange = new StartEndRange() { start = 0, end = Dimensions.z };

            switch (dir)
            {
                case Direction.up:
                    yRange.start = yRange.end - 1;
                    break;
                case Direction.down:
                    yRange.end = yRange.start + 1;
                    break;
                case Direction.north:
                    zRange.start = zRange.end - 1;
                    break;
                case Direction.south:
                    zRange.end = zRange.start + 1;
                    break;
                case Direction.east:
                    xRange.start = xRange.end - 1;
                    break;
                case Direction.west:
                    xRange.end = xRange.start + 1;
                    break;
                default:
                    throw new ArgumentException($"direction {dir} was not recognised");
            }

            NativeArray<VoxelTypeID> voxelData = new NativeArray<VoxelTypeID>(xRange.Length * yRange.Length * zRange.Length, Allocator.Persistent);

            int i = 0;
            for (int z = zRange.start; z < zRange.end; z++)
            {
                for (int y = yRange.start; y < yRange.end; y++)
                {
                    for (int x = xRange.start; x < xRange.end; x++)
                    {
                        voxelData[i] = this[x, y, z];

                        i++;
                    }
                }
            }

            return voxelData;

        }

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
            return ToNativeBruteForce(allocator);
        }

        public NativeArray<RotatedVoxelEntry> NativeRotations(Allocator allocator = Allocator.Persistent)
        {
            //This conversion from dictionary to array is only efficient under the assumption that there will be few rotated voxels per chunk
            NativeArray<RotatedVoxelEntry> rotations = new NativeArray<RotatedVoxelEntry>(rotatedVoxels.Values.ToArray(), allocator);
            return rotations;
        }

        public void SetRotation(Vector3Int coords, VoxelRotation rotation)
        {
            var (x, y, z) = coords;
            var flat = Utils.Helpers.MultiIndexToFlat(x, y, z, Dimensions);
            if (rotation.isBlank)
            {
                rotatedVoxels.Remove(flat);
            }
            else
            {
                rotatedVoxels[flat] = new RotatedVoxelEntry() { flatIndex = flat, rotation = rotation };
            }
        }
        #endregion
    }
}