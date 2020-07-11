using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;
using UniVox.Framework.Jobified;

namespace Utils
{
    public static class NativeExtensions
    {      

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

        public static NativeArray<T> ToNative<T>(this T[] arr, Allocator allocator = Allocator.Persistent)
            where T : struct
        {
            NativeArray<T> result = new NativeArray<T>(arr, allocator);
            return result;
        }
    }
}