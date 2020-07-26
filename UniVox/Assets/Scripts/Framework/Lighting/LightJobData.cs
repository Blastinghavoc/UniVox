using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UniVox.Framework.Jobified;

namespace UniVox.Framework.Lighting
{
    [BurstCompile]
    public struct LightJobData : IDisposable
    {
        [ReadOnly] public int3 chunkId;
        [ReadOnly] public int3 chunkWorldPos;
        [ReadOnly] public int3 dimensions;//Dimensions of chunk

        [ReadOnly] public NativeArray<VoxelTypeID> voxels;//Voxel data
        public NativeArray<LightValue> lights;//per-voxel light data

        [ReadOnly] public NeighbourData neighbourData;//neighbour voxel and light data
        [ReadOnly] public NativeArray<bool> directionsValid;//for each neighbour direction, has that chunk been fully generated yet

        [ReadOnly] public NativeArray<int> heightmap;

        //Externally allocated and managed
        [ReadOnly] public NativeArray<int> voxelTypeToEmissionMap;
        [ReadOnly] public NativeArray<int> voxelTypeToAbsorptionMap;
        [ReadOnly] public NativeArray<int3> directionVectors;

        public LightJobData(int3 chunkId, int3 chunkWorldPos, int3 dimensions, NativeArray<VoxelTypeID> voxels, NativeArray<LightValue> lights, NeighbourData neighbourData, NativeArray<bool> directionsValid, NativeArray<int> heightmap, NativeArray<int> voxelTypeToEmissionMap, NativeArray<int> voxelTypeToAbsorptionMap, NativeArray<int3> directionVectors)
        {
            this.chunkId = chunkId;
            this.chunkWorldPos = chunkWorldPos;
            this.dimensions = dimensions;
            this.voxels = voxels;
            this.lights = lights;
            this.neighbourData = neighbourData;
            this.directionsValid = directionsValid;
            this.heightmap = heightmap;
            this.voxelTypeToEmissionMap = voxelTypeToEmissionMap;
            this.voxelTypeToAbsorptionMap = voxelTypeToAbsorptionMap;
            this.directionVectors = directionVectors;
        }

        public void Dispose()
        {
            voxels.Dispose();
            lights.Dispose();
            neighbourData.Dispose();
            directionsValid.Dispose();
            heightmap.Dispose();
            //The voxelTypeToX maps are externally owned, so not disposed here
        }
    }

    [BurstCompile]
    public struct LightJobNeighbourUpdates : IDisposable
    {
        public NativeList<int3> up;
        public NativeList<int3> down;
        public NativeList<int3> north;
        public NativeList<int3> south;
        public NativeList<int3> east;
        public NativeList<int3> west;

        public LightJobNeighbourUpdates(Allocator allocator)
        {
            up = new NativeList<int3>(allocator);
            down = new NativeList<int3>(allocator);
            north = new NativeList<int3>(allocator);
            south = new NativeList<int3>(allocator);
            east = new NativeList<int3>(allocator);
            west = new NativeList<int3>(allocator);
        }

        public void Dispose()
        {
            up.Dispose();
            down.Dispose();
            north.Dispose();
            south.Dispose();
            east.Dispose();
            west.Dispose();
        }
    }
}