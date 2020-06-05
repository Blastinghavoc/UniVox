using UnityEngine;
using System.Collections;
using UnityEngine.Assertions;
using UniVox.Implementations.Common;

namespace UniVox.Implementations.ChunkData
{

    /// <summary>
    /// ChunkData class storing the data in a 3D array.
    /// </summary>
    public class ArrayChunkData : AbstractChunkData
    {
        /// <summary>
        /// XYZ Voxel Data
        /// </summary>
        protected VoxelData[,,] voxels;

        public ArrayChunkData(Vector3Int ID, Vector3Int chunkDimensions) : base(ID, chunkDimensions)
        {
            voxels = new VoxelData[chunkDimensions.x, chunkDimensions.y, chunkDimensions.z];
        }

        protected override VoxelData GetVoxelAtLocalCoordinates(int x, int y, int z)
        {
            return voxels[x, y, z];
        }

        protected override void SetVoxelAtLocalCoordinates(int x, int y, int z, VoxelData voxel)
        {
            voxels[x, y, z] = voxel;
        }

    }
}