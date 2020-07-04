using UnityEngine;
using UniVox.Framework;
using Utils;

namespace UniVox.Implementations.ProcGen
{
    [System.Serializable]
    public struct OreSettings 
    {
        public int depthMin;
        public int depthMax;

        public float noiseScale;
        [Range(0,1)]
        public float threshold;

        public void Initialise() 
        {
            //Convert from human readable to more convenient format for noise functions
            noiseScale = 1 / noiseScale;
        }

    }

    public struct NativeOreSettingsPair 
    {
        public VoxelTypeID voxelType;
        public OreSettings settings;
    }
}