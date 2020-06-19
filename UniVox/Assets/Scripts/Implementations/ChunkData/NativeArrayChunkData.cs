using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework.Jobified;
using UniVox.Implementations.Common;

namespace UniVox.Implementations.ChunkData
{
    public class NativeArrayChunkData : AbstractChunkData 
    {
        /// <summary>
        /// XYZ Voxel Data
        /// </summary>
        protected NativeArray<VoxelData> voxels;

        //Dimensions.x*Dimensions.y cache
        private int dxdy;

        public NativeArrayChunkData(Vector3Int ID, Vector3Int chunkDimensions,NativeArray<VoxelData> data) : base(ID, chunkDimensions)
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

        public override void Dispose()
        {
            voxels.SmartDispose();
        }
    }
}