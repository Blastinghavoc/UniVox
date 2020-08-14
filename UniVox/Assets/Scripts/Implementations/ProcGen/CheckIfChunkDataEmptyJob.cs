using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UniVox.Framework;
using Utils;

namespace UniVox.Implementations.ProcGen
{
    [BurstCompile]
    public struct CheckIfChunkDataEmptyJob : IJob, IDisposable
    {
        [ReadOnly] public NativeArray<VoxelTypeID> chunkData;
        public NativeArray<bool> isEmpty;

        public CheckIfChunkDataEmptyJob(NativeArray<VoxelTypeID> chunkData, Allocator allocator)
        {
            this.chunkData = chunkData;
            isEmpty = new NativeArray<bool>(1, allocator);
        }

        public void Dispose()
        {
            isEmpty.SmartDispose();
        }

        public void Execute()
        {
            VoxelTypeID air = (VoxelTypeID)VoxelTypeID.AIR_ID;
            for (int i = 0; i < chunkData.Length; i++)
            {
                if (chunkData[i] != air)
                {
                    isEmpty[0] = false;
                    return;
                }
            }
        }


    }
}