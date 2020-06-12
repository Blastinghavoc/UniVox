using UnityEngine;
using System.Collections;
using UniVox.Framework;
using System.Collections.Generic;

namespace PerformanceTesting
{
    public class WalkForwardTest : MonoBehaviour, IPerformanceTest
    {
        public string TestName { get; private set; } = "WalkForward";

        public float WalkDistance = 100;

        private FrameCounter frameCounter;
        private MemoryCounter memoryCounter;

        private float timeToCompleteWorld;
        private float timeToCompleteWalk;

        public IEnumerator Run(ITestableChunkManager chunkManager)
        {       

            var startTime = Time.unscaledTime;

            chunkManager.Initialise();

            //Wait until all chunks are complete
            while (!chunkManager.AllChunksInTargetState())
            {
                yield return null;
            }

            timeToCompleteWorld = Time.unscaledTime - startTime;

            yield return new WaitForSecondsRealtime(1);

            //Start the real test, recording frame time and memory while walking forward
            frameCounter = new FrameCounter();
            memoryCounter = new MemoryCounter();

            var player = chunkManager.GetPlayer();

            var startpos = player.position;
            var distanceSqr = WalkDistance * WalkDistance;

            startTime = Time.unscaledTime;

            //Force the player to walk forward
            TestFacilitator.virtualPlayer.SetAxis("Vertical", 1);

            //Wait until they've walked far enough
            while (Vector3.SqrMagnitude(player.position-startpos) < distanceSqr)
            {
                frameCounter.Update();
                memoryCounter.Update();
                yield return null;
            }

            timeToCompleteWalk = Time.unscaledTime - startTime;

        }

        public string[] GetCSVLines()
        {
            List<string> lines = new List<string>();

            lines.Add($"Took {timeToCompleteWorld} seconds to complete world, and an additional {timeToCompleteWalk} seconds to walk {WalkDistance} meters");

            foreach (var line in frameCounter.ToCSVLines())
            {
                lines.Add(line);
            }

            foreach (var line in memoryCounter.ToCSVLines())
            {
                lines.Add(line);
            }

            return lines.ToArray();
        }

    }
}