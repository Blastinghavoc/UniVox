using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UniVox.Framework;
using static Utils.Helpers;

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
                    if (config.oceanIDs.Contains(biomeMap[i]))
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
                            while (y >= 0 && chunkData[flatIndex] == VoxelTypeID.AIR_ID)
                            {
                                if (chunkPosition.y+y < heightMap[i])
                                {
                                    //Prevent filling below the heightmap

                                    ///NOTE: Without this break check, all caves underneath oceans get filled 
                                    ///with water, even if they do not connect to the ocean at all, 
                                    ///but with it water can be "floating" over caves that carve out space 
                                    ///directly under the seabed.
                                    ///Neither of these solutions is ideal, a proper water propagation
                                    ///system would need to be implemented to get "correct" looking
                                    ///water.                                

                                    break;
                                }
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