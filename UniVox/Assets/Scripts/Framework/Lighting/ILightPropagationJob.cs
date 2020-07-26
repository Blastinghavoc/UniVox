using Unity.Collections;
using Unity.Mathematics;

namespace UniVox.Framework.Lighting
{
    internal interface ILightPropagationJob
    {
        LightJobNeighbourUpdates sunlightNeighbourUpdates { get; set; }
        LightJobNeighbourUpdates dynamicNeighbourUpdates { get; set; }
        NativeQueue<int3> dynamicPropagationQueue { get; set; }
        NativeQueue<int3> sunlightPropagationQueue { get; set; }
    }
}