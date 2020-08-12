using System.Collections.Generic;

namespace PerformanceTesting
{
    public class NoLightingSuite : AbstractFixedAlgorithmsSuite
    {
        public override IEnumerable<PassDetails> Passes()
        {
            //With lighting
            SetupPass();
            chunkManager.SetIncludeLighting(true);
            yield return EndPass("LightOn");

            //Without lighting
            SetupPass();
            chunkManager.SetIncludeLighting(false);
            yield return EndPass("LlightOff");
        }
    }
}