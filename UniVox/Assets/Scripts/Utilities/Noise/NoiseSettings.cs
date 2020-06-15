using UnityEngine;
using System.Collections;
using System;

namespace Utils.Noise
{
    [System.Serializable]
    public class NoiseSettings
    {
        public int Octaves = 2;
        public float Persistence = 0.5f;//Aka gain
        public float Lacunarity = 2;

        public void ApplyTo(FastNoise noiseSource) 
        {
            noiseSource.SetFractalGain(Persistence);
            noiseSource.SetFractalLacunarity(Lacunarity);
            noiseSource.SetFractalOctaves(Octaves);
        }
    }
}