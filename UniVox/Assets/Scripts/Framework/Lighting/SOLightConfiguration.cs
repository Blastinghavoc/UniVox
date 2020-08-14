using UnityEngine;

namespace UniVox.Framework.Lighting
{
    [CreateAssetMenu(menuName = "UniVox/LightConfig")]
    public class SOLightConfiguration : ScriptableObject
    {
        [Range(0, LightValue.IntensityRange - 1)]
        [Tooltip("Amount of light emitted by this voxel.")]
        public int EmissionIntensity = LightValue.IntensityRange - 1;

        [Range(1, LightValue.IntensityRange - 1)]
        [Tooltip("Amount by which light values are diminished when propagating through this voxel.")]
        public int DiminishLightAmount = LightValue.IntensityRange - 1;
    }
}