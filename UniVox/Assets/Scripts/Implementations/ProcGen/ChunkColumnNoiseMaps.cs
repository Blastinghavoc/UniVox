using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using UniVox.Framework.Jobified;

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

        [BurstCompile]
        public struct NativeChunkColumnNoiseMaps:IDisposable
        {
            public NativeArray<int> heightMap;
            public NativeArray<int> biomeMap;
            public void Dispose() 
            {
                heightMap.SmartDispose();
                biomeMap.SmartDispose();
            }

            public NativeChunkColumnNoiseMaps(int flatSize,Allocator allocator = Allocator.Persistent) 
            {
                heightMap = new NativeArray<int>(flatSize, allocator);
                biomeMap = new NativeArray<int>(flatSize, allocator);
            }
        }

        public NativeChunkColumnNoiseMaps ToNative(Allocator allocator = Allocator.Persistent) 
        {
            NativeChunkColumnNoiseMaps native = new NativeChunkColumnNoiseMaps();
            native.heightMap = new NativeArray<int>(heightMap, allocator);
            native.biomeMap = new NativeArray<int>(biomeMap, allocator);
            return native;
        }

        public ChunkColumnNoiseMaps (NativeChunkColumnNoiseMaps maps) 
        {
            heightMap = maps.heightMap.ToArray();
            biomeMap = maps.biomeMap.ToArray();
        }

    }


}