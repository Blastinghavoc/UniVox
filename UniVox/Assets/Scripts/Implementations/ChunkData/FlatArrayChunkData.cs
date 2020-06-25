using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework;
using UniVox.Framework.Jobified;

namespace UniVox.Implementations.ChunkData
{
    /// <summary>
    /// Similar to arrayChunkData, but implented with a flat array.
    /// This allows it to be easily constructed from the result of a job.
    /// </summary>
    public class FlatArrayChunkData : AbstractChunkData 
    {
        /// <summary>
        /// XYZ Voxel Data
        /// </summary>
        protected VoxelTypeID[] voxels;

        //Dimensions.x*Dimensions.y cache
        private int dxdy;
            
        public FlatArrayChunkData(Vector3Int ID, Vector3Int chunkDimensions,VoxelTypeID[] initialData = null) : base(ID, chunkDimensions,initialData) 
        {
            if (initialData == null)
            {
                voxels = new VoxelTypeID[chunkDimensions.x * chunkDimensions.y * chunkDimensions.z];
            }
            else
            {
                voxels = initialData;
            }
            dxdy = chunkDimensions.x * chunkDimensions.y;
        }

        protected override VoxelTypeID GetVoxelID(int x, int y, int z)
        {
            return voxels[Utils.Helpers.MultiIndexToFlat(x,y,z,Dimensions.x,dxdy)];
        }

        protected override void SetVoxelID(int x, int y, int z, VoxelTypeID voxel)
        {
            voxels[Utils.Helpers.MultiIndexToFlat(x, y, z, Dimensions.x, dxdy)] = voxel;
        }

        public override NativeArray<VoxelTypeID> ToNative(Allocator allocator = Allocator.Persistent)
        {
            return new NativeArray<VoxelTypeID>(voxels, allocator);
        }
    }
}