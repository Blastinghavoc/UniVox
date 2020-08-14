using System;
using Unity.Burst;
using UnityEngine;

namespace UniVox.Framework.Lighting
{
    [BurstCompile]
    public struct LightValue : IEquatable<LightValue>
    {
        public const int IntensityRange = 16;
        public const int MaxIntensity = IntensityRange - 1;

        //4 bits for dynamic light intensity, 4 bits for sun light intensity
        //ddddssss
        private byte bits;

        public int Sun { get => bits & 0xF; set => bits = (byte)((bits & 0xF0) | value); }
        public int Dynamic { get => (bits >> 4) & 0xF; set => bits = (byte)((bits & 0xF) | value << 4); }


        public Color ToVertexColour()
        {
            var dynamicIntensity = (1f / (IntensityRange - 1)) * Dynamic;
            var sunIntensity = (1f / (IntensityRange - 1)) * Sun;
            Color col = new Color(dynamicIntensity, dynamicIntensity, dynamicIntensity, sunIntensity);
            return col;
        }

        public override string ToString()
        {
            return $"(sun:{Sun},dynamic:{Dynamic})";
        }

        public bool Equals(LightValue other)
        {
            return other.bits == bits;
        }

        public override int GetHashCode()
        {
            return bits.GetHashCode();
        }
    }
}