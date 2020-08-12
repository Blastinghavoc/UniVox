using System.Collections;
using System.Linq;
using UnityEngine;

namespace PerformanceTesting
{
    public class FlyForwardTest : AbstractPerformanceTest
    {
        public float WalkDistance = 100;
        public float Altitude = 60;

        public override IEnumerator Run(ITestableChunkManager chunkManager)
        {
            ResetLog();

            chunkManager.Initialise();

            //Wait until all chunks are complete
            while (!chunkManager.PipelineIsSettled())
            {
                yield return null;
            }

            yield return new WaitForSecondsRealtime(1);

            //Start the real test, recording frame time and memory while flying up then forward
            ResetPerFrameCounters();

            var player = chunkManager.GetPlayer();

            var distanceSqr = WalkDistance * WalkDistance;

            var startTime = Time.unscaledTime;
            //Activate flight mode
            TestFacilitator.virtualPlayer.SetButtonDown("ToggleFly");

            //Force the player to fly up
            TestFacilitator.virtualPlayer.SetAxis("Fly", 1);

            //record starting position for vertical flight
            var startpos = player.Position;

            //Wait until they're at correct altitude
            while (player.Position.y < Altitude)
            {
                UpdatePerFrameCounters();
                yield return null;
            }
            var verticalDistanceFlown = player.Position.y - startpos.y;

            //record starting position for horizontal flight
            startpos = player.Position;

            //Stop flying up
            TestFacilitator.virtualPlayer.SetAxis("Fly", 0);

            //Start flying forward
            TestFacilitator.virtualPlayer.SetAxis("Vertical", 1);

            //Wait until they've flown far enough
            while (Vector3.SqrMagnitude(player.Position - startpos) < distanceSqr)
            {
                UpdatePerFrameCounters();
                yield return null;
            }

            var timeToComplete = Time.unscaledTime - startTime;
            Log($"Took {timeToComplete} seconds to fly up {verticalDistanceFlown} meters and {WalkDistance} meters horizontally");
            Log($"Frame time stats: Min {frameCounter.FrameTimesMillis.Min()}, Max {frameCounter.FrameTimesMillis.Max()}, Mean {frameCounter.FrameTimesMillis.Average()}");
            Log($"Peak memory usage: {memoryCounter.memoryPerFrame.Max()}");
        }

    }
}