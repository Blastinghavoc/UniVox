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
        [ReadOnly] public float2 chunkPositionXZ;
        [ReadOnly] public WorldSettings worldSettings;

        [ReadOnly] public FractalNoise heightmapNoise;
        [ReadOnly] public FractalNoise moisturemapNoise;
        [ReadOnly] public FractalNoise treemapNoise;
        [ReadOnly] public TreeSettings treeSettings;

        [ReadOnly] public NativeBiomeDatabase biomeDatabase;

        public NativeChunkColumnNoiseMaps noiseMaps;

        public void Execute()
        {
            heightmapNoise.Initialise();
            moisturemapNoise.Initialise();
            treemapNoise.Initialise();

            int3 dimensions = worldSettings.ChunkDimensions;

            ComputeBiomeMapAndHeightMap(dimensions);
            ComputeTreeMap(dimensions);

        }

        private void ComputeTreeMap(int3 dimensions) 
        {
            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++,i++)
                {
                    var samplePoint = new float2(x + chunkPositionXZ.x, z + chunkPositionXZ.y)* treeSettings.TreemapScale;
                    var sample = treemapNoise.Sample(samplePoint); 

                    noiseMaps.treeMap[i] = sample;
                }
            }
        }

        private void ComputeBiomeMapAndHeightMap(int3 dimensions)
        {
            var maxPossibleHmValue = worldSettings.maxPossibleHmValue;
            var minPossibleHmValue = worldSettings.minPossibleHmValue;

            //Compute moisture map in range 0->1
            ComputeMoistureMap(dimensions);

            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    var hmFloat = CalculateHeightMapAt(new float2(x + chunkPositionXZ.x, z + chunkPositionXZ.y));
                    noiseMaps.heightMap[i] = (int)math.floor(hmFloat);

                    var elevationPercentage = math.unlerp(minPossibleHmValue, maxPossibleHmValue, hmFloat);
                    //Assumes moisture map is already in 0->1 range
                    var moisturePercentage = noiseMaps.moistureMap[i];
                    noiseMaps.biomeMap[i] = biomeDatabase.GetBiomeID(elevationPercentage, moisturePercentage);
                    i++;
                }
            }
        }

        private void ComputeMoistureMap(int3 dimensions)
        {
            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int x = 0; x < dimensions.x; x++)
                {
                    noiseMaps.moistureMap[i] = ZeroToOne(
                        moisturemapNoise.Sample(
                            new float2(x + chunkPositionXZ.x, z + chunkPositionXZ.y) * worldSettings.MoistureMapScale)
                        );
                    i++;
                }
            }
        }

        //private void ComputeHeightMap(int3 dimensions)
        //{
        //    int i = 0;
        //    for (int z = 0; z < dimensions.z; z++)
        //    {
        //        for (int x = 0; x < dimensions.x; x++)
        //        {
        //            noiseMaps.heightMap[i] = CalculateHeightMapAt(new float2(x + chunkPositionXZ.x, z + chunkPositionXZ.y));
        //            i++;
        //        }
        //    }
        //}

        /// <summary>
        /// Changes the distribution of the input noise value (assumed to be in range -1->1)
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private float AdjustHeightMapNoiseValue(float val)
        {
            if (val > 0)
            {
                return math.pow(val, worldSettings.HeightmapExponentPositive) * worldSettings.MaxHeightmapHeight;
            }
            else
            {
                //Negative exponent
                return math.pow(-1 * val, worldSettings.HeightmapExponentNegative) * worldSettings.MinHeightmapHeight;
            }
        }

        public float CalculateHeightMapAt(float2 pos)
        {

            float rawHeightmap = 
                AdjustHeightMapNoiseValue(heightmapNoise.Sample(pos * worldSettings.HeightmapScale));

            //add the raw heightmap to the base ground height
            return worldSettings.HeightmapYOffset + rawHeightmap;
        }
    }
}