using UnityEngine;

namespace UniVox.Framework.Lighting
{
    [CreateAssetMenu(menuName = "UniVox/LightConfig")]
    public class SOLightConfiguration : ScriptableObject
    {
        [Range(0,LightValue.IntensityRange-1)]
        public int Intensity = LightValue.IntensityRange-1;
    }

}