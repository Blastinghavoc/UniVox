using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework.Jobified;
using UniVox.Implementations.Common;

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
        protected VoxelData[] voxels;

        //Dimensions.x*Dimensions.y cache
        private int dxdy;

        public FlatArrayChunkData() { }

        public FlatArrayChunkData(Vector3Int ID, Vector3Int chunkDimensions) : base(ID, chunkDimensions) 
        {
        }

        public override void Initialise(Vector3Int ID, Vector3Int chunkDimensions)
        {
            base.Initialise(ID, chunkDimensions);
        }

        public FlatArrayChunkData(Vector3Int ID, Vector3Int chunkDimensions, VoxelData[] data) : base(ID, chunkDimensions)
        {
            if (chunkDimensions.x*chunkDimensions.y*chunkDimensions.z != data.Length)
            {
                throw new ArgumentException($"Chunk dimensions given {chunkDimensions} do not match size of data array {data.Length}");
            }
            voxels = data;
            dxdy = Dimensions.x * Dimensions.y;
        }

        protected override VoxelData GetVoxelAtLocalCoordinates(int x, int y, int z)
        {
            return voxels[Utils.Helper.MultiIndexToFlat(x,y,z,Dimensions.x,dxdy)];
        }

        protected override void SetVoxelAtLocalCoordinates(int x, int y, int z, VoxelData voxel)
        {
            voxels[Utils.Helper.MultiIndexToFlat(x, y, z, Dimensions.x, dxdy)] = voxel;
        }

        public override NativeArray<VoxelData> ToNative(Allocator allocator = Allocator.Persistent)
        {
            return new NativeArray<VoxelData>(voxels, allocator);
        }
    }
}