using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UniVox.Framework;
using static Utils.Helpers;
using static Utils.Noise.Helpers;

namespace UniVox.Implementations.ProcGen
{
    [BurstCompile]
    public struct OreGenJob : IJob
    {
        [ReadOnly] public uint seed;
        [ReadOnly] public NativeArray<NativeOreSettingsPair> oreSettings;

        [ReadOnly] public int3 dimensions;
        [ReadOnly] public float3 chunkPosition;
        [ReadOnly] public VoxelTypeID stoneId;

        //Input and Output
        public NativeArray<VoxelTypeID> chunkData;

        //Temporary
        public NativeArray<float3> offsets;

        [ReadOnly] public NativeArray<int> heightMap;

        public void Execute()
        {
            var adjustedSeed = (uint)seed;
            adjustedSeed = adjustedSeed == 0 ? adjustedSeed + 1 : adjustedSeed;
            Random random = new Random(adjustedSeed);

            for (int i = 0; i < offsets.Length; i++)
            {
                offsets[i] = random.NextFloat3();
            }


            var mapDimensions = new int2 (dimensions.x , dimensions.z);
            int flatIndex = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++,flatIndex++)
                    {
                        var mapIndex = MultiIndexToFlat(x, z, mapDimensions);
                        var worldPos = new float3(x, y, z) + chunkPosition;
                        var depth = heightMap[mapIndex] - worldPos.y;

                        if (chunkData[flatIndex] != stoneId)
                        {
                            //Can't place ores if the original block was not stone
                            continue;
                        }

                        for (int i = 0; i < oreSettings.Length; i++)
                        {
                            var ore = oreSettings[i];
                            if (depth >= ore.settings.depthMin && depth <= ore.settings.depthMax)
                            {
                                //try to generate ore from noise
                                var noiseValue = ZeroToOne(noise.snoise(worldPos*ore.settings.noiseScale + offsets[i]));
                                if (noiseValue < ore.settings.threshold)
                                {
                                    chunkData[flatIndex] = ore.voxelType;
                                    break;//break as soon as one ore was generated in a location
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}