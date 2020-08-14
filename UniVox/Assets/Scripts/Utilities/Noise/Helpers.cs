using Unity.Burst;
using Unity.Mathematics;

namespace Utils.Noise
{
    public static class Helpers
    {
        /// <summary>
        /// Transform a noise value from the -1->1 range that the noise functions
        /// output to the 0->1 range
        /// </summary>
        /// <param name="rawNoise"></param>
        /// <returns></returns>
        [BurstCompile]
        public static float ZeroToOne(float rawNoise)
        {
            return math.unlerp(-1, 1, rawNoise);
        }
    }

}