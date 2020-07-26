using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UniVox.Framework.Lighting;

namespace UniVox.Framework.Jobified
{
    [BurstCompile]
    public static class JobUtils 
    {
        public static VoxelTypeID GetVoxel(int3 pos,NativeArray<VoxelTypeID> voxels,int3 dimensions,NeighbourData neighbourData)
        {
            neighbourData.AdjustLocalPos(ref pos, out var InChunk, out var DirectionOfNeighbour, dimensions);
            if (InChunk)
            {
                return voxels[Utils.Helpers.MultiIndexToFlat(pos.x, pos.y, pos.z, dimensions)];
            }
            else
            {
                var localIndexInNeighbour = neighbourData.IndicesInNeighbour(DirectionOfNeighbour, pos);
                var neighbourDimensions = neighbourData.IndicesInNeighbour(DirectionOfNeighbour, dimensions);

                var flattenedIndex = Utils.Helpers.MultiIndexToFlat(localIndexInNeighbour.x, localIndexInNeighbour.y, neighbourDimensions);

                var neighbourVoxelData = neighbourData.GetVoxels(DirectionOfNeighbour);
                return neighbourVoxelData[flattenedIndex];
            }
        }

        public static LightValue GetLightValue(int3 pos, NativeArray<LightValue> lights, int3 dimensions, NeighbourData neighbourData)
        {
            neighbourData.AdjustLocalPos(ref pos, out var InChunk, out var DirectionOfNeighbour, dimensions);
            if (InChunk)
            {
                return lights[Utils.Helpers.MultiIndexToFlat(pos.x, pos.y, pos.z, dimensions)];
            }
            else
            {
                var localIndexInNeighbour = neighbourData.IndicesInNeighbour(DirectionOfNeighbour, pos);
                var neighbourDimensions = neighbourData.IndicesInNeighbour(DirectionOfNeighbour, dimensions);

                var flattenedIndex = Utils.Helpers.MultiIndexToFlat(localIndexInNeighbour.x, localIndexInNeighbour.y, neighbourDimensions);

                var neighbourLightData = neighbourData.GetLightValues(DirectionOfNeighbour);
                return neighbourLightData[flattenedIndex];
            }
        }
    }
}