using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UniVox.Framework.Common;
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

        public static NeighbourData CacheNeighbourData(IChunkData chunkData,IChunkManager chunkManager)         
        {
            var chunkId = chunkData.ChunkID;
            NeighbourData neighbourData = new NeighbourData();
            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                var neighbourID = chunkData.ChunkID + DirectionExtensions.Vectors[i];
                try
                {
                    var neighbour = chunkManager.GetReadOnlyChunkData(neighbourID);
                    var oppositeDir = DirectionExtensions.Opposite[i];
                    neighbourData.Add((Direction)i, neighbour.BorderToNative(oppositeDir));
                    neighbourData.Add((Direction)i, neighbour.BorderToNativeLight(oppositeDir));
                }
                catch (Exception e)
                {
                    var (managerHad, pipelinehad) = chunkManager.ContainsChunkID(chunkId);
                    throw new Exception($"Failed to get neighbour data for chunk {chunkId}." +
                        $"Manager had this chunk = {managerHad}, pipeline had it = {pipelinehad}." +
                        $"Cause: {e.Message}", e);
                }
            }
            return neighbourData;
        }
    }
}