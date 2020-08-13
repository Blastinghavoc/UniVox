using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Utils;

namespace UniVox.Implementations.ProcGen
{
    /// <summary>
    /// Holds all 2D noise maps for a column of chunks,
    /// including heightmap, biomemap, tree map etc.
    /// </summary>
    public class ChunkColumnNoiseMaps 
    {
        public int[] heightMap;
        public int[] biomeMap;
        public float[] treeMap;

        [BurstCompile]
        public struct NativeChunkColumnNoiseMaps:IDisposable
        {
            public NativeArray<int> heightMap;
            public NativeArray<int> biomeMap;
            public NativeArray<float> treeMap;
            public NativeArray<float> moistureMap;
            public void Dispose() 
            {
                heightMap.SmartDispose();
                biomeMap.SmartDispose();
                treeMap.SmartDispose();
                moistureMap.SmartDispose();
            }

            public NativeChunkColumnNoiseMaps(int flatSize,Allocator allocator = Allocator.Persistent) 
            {
                heightMap = new NativeArray<int>(flatSize, allocator);
                biomeMap = new NativeArray<int>(flatSize, allocator);
                treeMap = new NativeArray<float>(flatSize, allocator);
                moistureMap = new NativeArray<float>(flatSize, allocator);
            }
        }

        public NativeChunkColumnNoiseMaps ToNative(Allocator allocator = Allocator.Persistent) 
        {
            NativeChunkColumnNoiseMaps native = new NativeChunkColumnNoiseMaps();
            native.heightMap = heightMap.ToNative(allocator);
            native.biomeMap = biomeMap.ToNative(allocator);
            native.treeMap = treeMap.ToNative(allocator);     
            return native;
        }

        public ChunkColumnNoiseMaps (NativeChunkColumnNoiseMaps maps) 
        {
            heightMap = maps.heightMap.ToArray();
            biomeMap = maps.biomeMap.ToArray();
            treeMap = maps.treeMap.ToArray();
        }

    }


}