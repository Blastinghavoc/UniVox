using Utils.Noise;
using static Utils.Noise.Helpers;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using static UniVox.Implementations.ProcGen.ChunkColumnNoiseMaps;

namespace UniVox.Implementations.ProcGen
{
    [BurstCompile]
    public struct NoiseMapGenerationJob : IJob
    {
        [ReadOnly] public float3 chunkPosition;
        [ReadOnly] public WorldSettings worldSettings;

        [ReadOnly] public FractalNoise heightmapNoise;
        [ReadOnly] public FractalNoise moisturemapNoise;

        [ReadOnly] public NativeBiomeDatabase biomeDatabase;

        public NativeChunkColumnNoiseMaps noiseMaps;

        public void Execute()
        {
            heightmapNoise.Initialise();
            moisturemapNoise.Initialise();

            int3 dimensions = worldSettings.ChunkDimensions;

            ComputeHeightMap(dimensions);
            ComputeBiomeMap(dimensions);

        }

        private void ComputeBiomeMap(int3 dimensions)
        {
            var maxPossibleHmValue = worldSettings.maxPossibleHmValue;
            var minPossibleHmValue = worldSettings.minPossibleHmValue;

            //Compute moisture map in range 0->1
            NativeArray<float> moistureMap = new NativeArray<float>(dimensions.x * dimensions.y, Allocator.Temp);
            ComputeMoistureMap(ref moistureMap, dimensions);

            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    var elevationPercentage = math.unlerp(minPossibleHmValue, maxPossibleHmValue, noiseMaps.heightMap[i]);
                    //Assumes moisture map is already in 0->1 range
                    var moisturePercentage = moistureMap[i];
                    noiseMaps.biomeMap[i] = biomeDatabase.GetBiomeID(elevationPercentage, moisturePercentage);
                    i++;
                }
            }
        }

        private void ComputeMoistureMap(ref NativeArray<float> moistureMap, int3 dimensions)
        {
            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    moistureMap[i] = ZeroToOne(
                        moisturemapNoise.Sample(
                            new float2(x + chunkPosition.x, z + chunkPosition.z) * worldSettings.MoistureMapScale)
                        );
                    i++;
                }
            }
        }

        private void ComputeHeightMap(int3 dimensions)
        {
            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    noiseMaps.heightMap[i] = CalculateHeightMapAt(new float2(x + chunkPosition.x, z + chunkPosition.z));
                    i++;
                }
            }
        }

        /// <summary>
        /// Changes the distribution of the input noise value (assumed to be in range -1->1)
        /// Range is unchanged.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private float AdjustHeightMapNoiseValue(float val)
        {
            if (val > 0)
            {
                return math.pow(val, worldSettings.HeightmapExponentPositive);
            }
            else
            {
                //Negative exponent
                return -1 * math.pow(-1 * val, worldSettings.HeightmapExponentNegative);
            }
        }

        public int CalculateHeightMapAt(float2 pos)
        {

            int rawHeightmap = (int)math.floor(
                AdjustHeightMapNoiseValue(heightmapNoise.Sample(pos * worldSettings.HeightmapScale))
                * worldSettings.MaxHeightmapHeight
                );

            //add the raw heightmap to the base ground height
            return worldSettings.HeightmapYOffset + rawHeightmap;
        }
    }
}