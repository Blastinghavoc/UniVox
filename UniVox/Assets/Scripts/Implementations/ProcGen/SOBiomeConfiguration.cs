using System.Collections.Generic;
using UnityEngine;
using UniVox.Framework;

namespace UniVox.Implementations.ProcGen
{
    [CreateAssetMenu(menuName = "UniVox/BiomeConfiguration")]
    public class SOBiomeConfiguration : ScriptableObject
    {
        public SOVoxelTypeDefinition defaultVoxelType;
        public List<ElevationZone> elevationLowToHigh;

        [System.Serializable]
        public class ElevationZone
        {
            public string Name;
            public List<BiomeMoistureDefinition> moistureLevelsLowToHigh;
            [Range(0, 1)]
            public float max = 1;
        }

        [System.Serializable]
        public class BiomeMoistureDefinition
        {
            public SOBiomeDefinition biomeDefinition;
            [Range(0, 1)]
            public float max = 1;
        }
    }
}