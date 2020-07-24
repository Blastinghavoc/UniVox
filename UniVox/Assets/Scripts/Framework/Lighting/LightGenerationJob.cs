using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UniVox.Framework.Jobified;
using Utils;

namespace UniVox.Framework.Lighting
{
    [BurstCompile]
    public struct LightGenerationJob : IJob
    {
        public void Execute()
        {
            
        }
    }

    [BurstCompile]
    public struct LightJobData : IDisposable
    {
        [ReadOnly] public int3 dimensions;//Dimensions of chunk

        [ReadOnly] public NativeArray<VoxelTypeID> voxels;//Voxel data
        public NativeArray<LightValue> lights;//per-voxel light data

        [ReadOnly] public NeighbourData neighbourData;//neighbour voxel and light data

        [ReadOnly] public NativeArray<int> voxelTypeToEmissionMap;
        [ReadOnly] public NativeArray<int> voxelTypeToAbsorptionMap;

        public void Dispose()
        {
            voxels.SmartDispose();
            lights.SmartDispose();
            neighbourData.Dispose();
            //The voxelTypeToX maps are externally owned
        }
    }
}