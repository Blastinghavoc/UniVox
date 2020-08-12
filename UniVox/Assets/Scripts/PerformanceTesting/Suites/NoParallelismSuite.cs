using System.Collections.Generic;

namespace PerformanceTesting
{

    public class NoParallelismSuite : AbstractFixedAlgorithmsSuite
    {
        public override IEnumerable<PassDetails> Passes()
        {
            SetupPass();
            provider.Parrallel = false;
            mesher.Parrallel = false;
            yield return EndPass("ParrallelOff");

            SetupPass();
            provider.Parrallel = true;
            mesher.Parrallel = true;
            yield return EndPass("ParrallelOn");
        }
    }
}