using System.Collections.Generic;
using System.IO;
using UniVox.Framework.Serialisation;

namespace PerformanceTesting
{
    /// <summary>
    /// Rather different performance tests suite, designed to run
    /// just one test (the serialisation test).
    /// </summary>
    public class SerialisationSuite : AbstractFixedAlgorithmsSuite
    {
        public override IEnumerable<PassDetails> Passes()
        {
            SetupPass();
            SaveUtils.DoSave = true;
            SaveUtils.WorldName = "PERF-TEST_SERIALISATION_SUITE";
            yield return EndPass("SerialisationOn");
            if (Directory.Exists(SaveUtils.CurrentWorldSaveDirectory))
            {
                SaveUtils.DeleteSave(SaveUtils.WorldName);
            }
            SaveUtils.DoSave = false;
        }
    }
}