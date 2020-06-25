using System.Collections;
using System.Linq;
using UnityEngine;

namespace PerformanceTesting
{
    public class WalkForwardTest : AbstractPerformanceTest
    {
        public float WalkDistance = 100;

        public override IEnumerator Run(ITestableChunkManager chunkManager)
        {
            ResetLog();

            chunkManager.Initialise();

            //Wait until all chunks are complete
            while (!chunkManager.AllChunksInTargetState())
            {
                yield return null;
            }

            yield return new WaitForSecondsRealtime(1);

            //Start the real test, recording frame time and memory while walking forward
            ResetPerFrameCounters();

            var player = chunkManager.GetPlayer();

            var startpos = player.position;
            var distanceSqr = WalkDistance * WalkDistance;

            var startTime = Time.unscaledTime;

            //Force the player to walk forward
            TestFacilitator.virtualPlayer.SetAxis("Vertical", 1);

            //Wait until they've walked far enough
            while (Vector3.SqrMagnitude(player.position - startpos) < distanceSqr)
            {
                UpdatePerFrameCounters();
                yield return null;
            }

            var timeToCompleteWalk = Time.unscaledTime - startTime;
            Log($"Took {timeToCompleteWalk} seconds to walk {WalkDistance} meters");
            Log($"Frame time stats: Min {frameCounter.FrameTimesMillis.Min()}, Max {frameCounter.FrameTimesMillis.Max()}, Mean {frameCounter.FrameTimesMillis.Average()}");
            Log($"Peak memory usage: {memoryCounter.memoryPerFrame.Max()}");
        }

    }
}