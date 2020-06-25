using UnityEngine;
using System.Collections;
using UnityEngine.Assertions;
using UniVox.Framework;
using Unity.Collections;
using Utils;

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
        protected VoxelTypeID[,,] voxels;

        public ArrayChunkData(Vector3Int ID, Vector3Int chunkDimensions,VoxelTypeID[] initialData = null) : base(ID, chunkDimensions,initialData) 
        {
            if (initialData == null)
            {
                voxels = new VoxelTypeID[chunkDimensions.x, chunkDimensions.y, chunkDimensions.z];
            }
            else
            {
                voxels = initialData.Expand(chunkDimensions);
            }
        }

        protected override VoxelTypeID GetVoxelID(int x, int y, int z)
        {
            return voxels[x, y, z];
        }

        protected override void SetVoxelID(int x, int y, int z, VoxelTypeID voxel)
        {
            voxels[x, y, z] = voxel;
        }

    }
}