using System.Collections.Generic;

namespace PerformanceTesting
{
    public class NoLightingSuite : AbstractFixedAlgorithmsSuite
    {
        public override IEnumerable<PassDetails> Passes()
        {
            //Without lighting
            SetupPass();
            chunkManager.SetIncludeLighting(false);
            yield return EndPass("LightOff");

            //With lighting
            SetupPass();
            chunkManager.SetIncludeLighting(true);
            yield return EndPass("LightOn");
        }
    }
}