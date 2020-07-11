using Unity.Burst;
using Unity.Collections;
using UniVox.Framework;
using UniVox.Framework.Common;

namespace UniVox.Implementations.ProcGen
{
    [BurstCompile]
    public struct NativeBiomeDatabase 
    {
        /// <summary>
        /// What to fill remaining space with after all layers in a biome
        /// have been done. (probably always going to be stone)
        /// </summary>
        public VoxelTypeID defaultVoxelType;

        /// <summary>
        /// Layers used in every biome
        /// </summary>
        public NativeArray<NativeVoxelRange> allLayers;

        /// <summary>
        /// Start-end positions in allLayers for each biome
        /// (by integer biome id)
        /// </summary>
        public NativeArray<StartEndRange> biomeLayers;

        /// <summary>
        /// All moisture definitions used by all elevation levels
        /// </summary>
        public NativeArray<NativeBiomeMoistureDefinition> allMoistureDefs;

        /// <summary>
        /// All defined elevation zones
        /// </summary>
        public NativeArray<NativeElevationZone> allElevationZones;

        /// <summary>
        /// The two parameters are the normalised (0->1) elevation and moisture values.
        /// </summary>
        /// <param name="elevationPercentage"></param>
        /// <param name="moisturePercentage"></param>
        /// <returns></returns>
        public int GetBiomeID(float elevationPercentage, float moisturePercentage) 
        {
            //Simple linear search through elevation zones
            StartEndRange moisturelevels = new StartEndRange() { start = 0, end = 0};
            for (int i = 0; i < allElevationZones.Length; i++)
            {
                var zone = allElevationZones[i];
                moisturelevels = zone.moistureLevels;
                if (elevationPercentage < zone.maxElevationPercentage)
                {
                    break;
                }
            }

            //Simple linear search through moisture levels
            int biomeID = 0;
            for (int i = moisturelevels.start; i < moisturelevels.end; i++)
            {
                var moistureDef = allMoistureDefs[i];
                biomeID = moistureDef.biomeID;
                if (moisturePercentage < moistureDef.maxMoisturePercentage)
                {
                    break;
                }
            }
            return biomeID;
        }
    }

    public struct NativeVoxelRange 
    {
        public ushort voxelID;
        public int depth;
    }

    public struct NativeElevationZone 
    {
        public StartEndRange moistureLevels;
        public float maxElevationPercentage;
    }

    public struct NativeBiomeMoistureDefinition 
    {
        public int biomeID;
        public float maxMoisturePercentage;
    }
}