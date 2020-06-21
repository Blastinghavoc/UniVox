using System.Collections.Generic;
using UnityEngine;
using UniVox.Framework;

namespace UniVox.Implementations.ProcGen
{
    /// <summary>
    /// Simple biome defintion, where a biome has N layers of
    /// some voxel types, and then the rest is filled with a single type.
    /// </summary>
    [CreateAssetMenu(menuName = "UniVox/BiomeDefinition")]
    public class SOBiomeDefinition : ScriptableObject 
    {
        public List<VoxelRange> topLayers;

        [System.Serializable]
        public class VoxelRange
        {
            public SOVoxelTypeDefinition voxelType;
            public int depth;
        }
    }
}