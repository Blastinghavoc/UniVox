using Unity.Collections;
using UniVox.Framework.Jobified;

namespace UniVox.Implementations.ProcGen
{
    public struct NativeBiomeDatabase 
    {
        /// <summary>
        /// What to fill remaining space with after all layers in a biome
        /// have been done. (probably always going to be stone)
        /// </summary>
        public ushort defaultVoxelId;

        /// <summary>
        /// Layers used in every biome
        /// </summary>
        public NativeArray<NativeVoxelRange> allLayers;

        /// <summary>
        /// Start-end positions in allLayers for each biome
        /// (by integer biome id)
        /// </summary>
        public NativeArray<StartEnd> biomeLayers;

        /// <summary>
        /// All moisture definitions used by all elevation levels
        /// </summary>
        public NativeArray<NativeBiomeMoistureDefinition> allMoistureDefs;

        /// <summary>
        /// All defined elevation zones
        /// </summary>
        public NativeArray<NativeElevationZone> allElevationZones;

    }

    public struct NativeVoxelRange 
    {
        public int voxelID;
        public int depth;
    }

    public struct NativeElevationZone 
    {
        public StartEnd moistureLevels;
        public float maxElevationPercentage;
    }

    public struct NativeBiomeMoistureDefinition 
    {
        public int biomeID;
        public float maxMoisturePercentage;
    }
}