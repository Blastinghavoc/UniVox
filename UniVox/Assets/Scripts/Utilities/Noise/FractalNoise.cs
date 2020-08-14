using System;
using Unity.Burst;
using Unity.Mathematics;

namespace Utils.Noise
{
    [System.Serializable]
    [BurstCompile]
    public struct FractalNoise
    {
        public int Octaves;
        public float Persistence;//power multiplier per octave
        public float Lacunarity;//frequency multiplier per octave
        [NonSerialized] public float Seed;
        [NonSerialized] private float MaxAmplitudeCache;

        public void Initialise()
        {
            PrecalculateMaxNoiseAmplitude();
        }

        private void PrecalculateMaxNoiseAmplitude()
        {
            //Precalcuate max noise amplitude, to avoid having to do so for each noise calculation
            float amplitude = 1;
            MaxAmplitudeCache = 0;
            for (int n = 0; n < Octaves; n++)
            {
                MaxAmplitudeCache += amplitude;
                amplitude *= Persistence;
            }
        }

        public float Sample(float3 pos)
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            for (int i = 0; i < Octaves; i++)
            {
                total += noise.snoise(new float4(pos * frequency, Seed)) * amplitude;
                amplitude *= Persistence;
                frequency *= Lacunarity;
            }
            //Return normalised
            return total / MaxAmplitudeCache;
        }

        public float Sample(float2 pos)
        {
            float total = 0;
            float frequency = 1;
            float amplitude = 1;
            for (int i = 0; i < Octaves; i++)
            {
                total += noise.snoise(new float3(pos * frequency, Seed)) * amplitude;
                amplitude *= Persistence;
                frequency *= Lacunarity;
            }
            //Return normalised
            return total / MaxAmplitudeCache;
        }


    }

}