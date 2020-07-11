using System;
using Unity.Collections;
using UnityEngine;
using UniVox.Framework;
using Utils;
using static Utils.Helpers;

namespace UniVox.Implementations.ChunkData
{
    public class RLEChunkData : AbstractChunkData
    {
        public RLEArray<VoxelTypeID> rle;
        private int dxdy;
        public RLEChunkData(Vector3Int ID, Vector3Int chunkDimensions, VoxelTypeID[] initialData = null) : base(ID, chunkDimensions, initialData)
        {
            if (initialData != null)
            {
                rle = new RLEArray<VoxelTypeID>(chunkDimensions,initialData);
            }
            else
            {
                rle = new RLEArray<VoxelTypeID>(chunkDimensions);
            }
            dxdy = Dimensions.x * Dimensions.y;
        }

        protected override VoxelTypeID GetVoxelID(int x, int y, int z)
        {
            return rle.Get(MultiIndexToFlat(x,y,z,Dimensions.x,dxdy));
        }

        protected override void SetVoxelID(int x, int y, int z, VoxelTypeID voxel)
        {
            rle.Set(MultiIndexToFlat(x, y, z, Dimensions.x, dxdy), voxel);
        }

        public override NativeArray<VoxelTypeID> ToNative(Allocator allocator = Allocator.Persistent)
        {
            return rle.ToArray().ToNative(allocator);
        }
    }
}