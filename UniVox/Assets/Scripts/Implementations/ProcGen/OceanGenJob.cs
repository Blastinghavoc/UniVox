using UniVox.Framework;
using static Utils.Helpers;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Utils;

namespace UniVox.Implementations.ProcGen
{
    [BurstCompile]
    public struct OceanGenJob : IJob
    {
        [ReadOnly] public OceanGenConfig config;
        [ReadOnly] public int3 dimensions;
        [ReadOnly] public float3 chunkPosition;

        //Input and Output
        public NativeArray<VoxelTypeID> chunkData;

        //Input
        [ReadOnly] public NativeArray<int> heightMap;
        [ReadOnly] public NativeArray<int> biomeMap;

        public void Execute()
        {
            int dx = dimensions.x;
            int dxdy = dimensions.x * dimensions.y;

            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++, i++)
                {
                    if (biomeMap[i] == config.oceanID)
                    {
                        if (heightMap[i] <= config.sealevel)
                        {
                            var yStart = (int)math.floor(math.min(config.sealevel - chunkPosition.y, dimensions.y - 1));

                            if (yStart < 0)
                            {
                                continue;
                            }

                            int y = yStart;
                            int flatIndex = MultiIndexToFlat(x, y, z, dx, dxdy);
                            while (y >= 0 && chunkData[flatIndex] == VoxelTypeManager.AIR_ID)
                            {
                                chunkData[flatIndex] = new VoxelTypeID(config.waterID);
                                y--;
                                flatIndex = MultiIndexToFlat(x, y, z, dx, dxdy);
                            }

                        }
                    }
                }
            }
        }
    }
}