using UnityEngine;
using System.Collections;
using UnityEngine.Assertions;
using UniVox.Implementations.Common;
using UniVox.Framework;
using Unity.Collections;

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

        /// <summary>
        /// Constructor that does not initialise
        /// </summary>
        public ArrayChunkData() {}

        /// <summary>
        /// Initialising constructor
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="chunkDimensions"></param>
        public ArrayChunkData(Vector3Int ID, Vector3Int chunkDimensions) : base(ID, chunkDimensions) 
        { 
        }

        public override void Initialise(Vector3Int ID, Vector3Int chunkDimensions)
        {
            base.Initialise(ID, chunkDimensions);
            voxels = new VoxelData[chunkDimensions.x, chunkDimensions.y, chunkDimensions.z];
        }

        public override void FromNative(NativeArray<VoxelData> native)
        {
            int i = 0;
            for (int z = 0; z < Dimensions.z; z++)
            {
                for (int y = 0; y < Dimensions.y; y++)
                {
                    for (int x = 0; x < Dimensions.x; x++)
                    {
                        voxels[x,y,z] = native[i];

                        i++;
                    }
                }
            }
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