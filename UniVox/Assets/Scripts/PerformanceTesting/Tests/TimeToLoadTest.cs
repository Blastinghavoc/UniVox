using UnityEngine;
using System.Collections;
using System.Linq;

namespace PerformanceTesting
{
    /// <summary>
    /// Records performance statistics while the world loads.
    /// </summary>
    public class TimeToLoadTest : AbstractPerformanceTest
    {
        [Range(0,300)]
        public int MaxRealTimeSeconds = 60;
        public override IEnumerator Run(ITestableChunkManager chunkManager)
        {
            ResetLog();
            ResetPerFrameCounters();

            chunkManager.Initialise();

            var startTime = Time.realtimeSinceStartup;

            while (!chunkManager.PipelineIsSettled())
            {
                UpdatePerFrameCounters();
                yield return null;
            }

            var endTime = Time.realtimeSinceStartup;

            var duration = endTime - startTime;

            Log($"Recorded {frameCounter.FrameTimesMillis.Count} frames");
            Log($"Peak memory usage of {memoryCounter.memoryPerFrame.Max()}");
            Log($"Time to load was {duration} seconds");
            Log($"Time limit was {MaxRealTimeSeconds} seconds");
        }
    }
}