using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace UniVox.Framework.Jobified
{
    public static class NativeExtensions
    {
        public static NativeArray<V> ToNative<V>(this IChunkData<V> chunkData)
            where V : struct, IVoxelData
        {
            //Copy chunk data to native array
            NativeArray<V> voxels = new NativeArray<V>(chunkData.Dimensions.x * chunkData.Dimensions.y * chunkData.Dimensions.z, Allocator.Persistent);

            int i = 0;
            for (int z = 0; z < chunkData.Dimensions.z; z++)
            {
                for (int y = 0; y < chunkData.Dimensions.y; y++)
                {
                    for (int x = 0; x < chunkData.Dimensions.x; x++)
                    {
                        voxels[i] = chunkData[x, y, z];

                        i++;
                    }
                }
            }

            return voxels;
        }

        /// <summary>
        /// Creates a native array representing the border of the chunk data in the given direction.
        /// E.g, with Direction = UP creates a flattened 2D array of all blocks on the top border
        /// of the chunk
        /// </summary>
        /// <typeparam name="V"></typeparam>
        /// <param name="chunkData"></param>
        /// <param name="Direction"></param>
        /// <returns></returns>
        public static NativeArray<V> BorderToNative<V>(this IChunkData<V> chunkData, int Direction)
            where V : struct, IVoxelData
        {
            StartEnd xRange = new StartEnd() { start = 0, end = chunkData.Dimensions.x };
            StartEnd yRange = new StartEnd() { start = 0, end = chunkData.Dimensions.y };
            StartEnd zRange = new StartEnd() { start = 0, end = chunkData.Dimensions.z };

            switch (Direction)
            {
                case Directions.UP:
                    yRange.start = yRange.end - 1;
                    break;
                case Directions.DOWN:
                    yRange.end = yRange.start +1;
                    break;
                case Directions.NORTH:
                    zRange.start = zRange.end - 1;
                    break;
                case Directions.SOUTH:
                    zRange.end = zRange.start +1;
                    break;
                case Directions.EAST:
                    xRange.start = xRange.end - 1;
                    break;
                case Directions.WEST:
                    xRange.end = xRange.start+1;
                    break;
                default:
                    throw new ArgumentException($"direction {Direction} was not recognised");
            }

            NativeArray<V> voxelData = new NativeArray<V>(xRange.Length * yRange.Length * zRange.Length, Allocator.Persistent);

            int i = 0;
            for (int z = zRange.start; z < zRange.end; z++)
            {
                for (int y = yRange.start; y < yRange.end; y++)
                {
                    for (int x = xRange.start; x < xRange.end; x++)
                    {
                        voxelData[i] = chunkData[x, y, z];

                        i++;
                    }
                }
            }

            return voxelData;

        }

        public static Vector3[] ToBasic(this float3[] arr)
        {
            Vector3[] result = new Vector3[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                result[i] = arr[i].ToBasic();
            }
            return result;
        }

        /// <summary>
        /// Dispose of array if it was created
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr"></param>
        public static void SmartDispose<T>(this NativeArray<T> arr)
            where T : struct
        {
            if (arr.IsCreated)
            {
                arr.Dispose();
            }
        }
    }
}