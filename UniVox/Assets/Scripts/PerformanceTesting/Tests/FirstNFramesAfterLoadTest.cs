using UnityEngine;
using System.Collections;
using System.Linq;
using UniVox.Framework;
using System.Text;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Profiling;

namespace PerformanceTesting
{
    /// <summary>
    /// Record data for the first N frames after the world loads
    /// </summary>
    public class FirstNFramesAfterLoadTest : AbstractPerformanceTest
    {
        public uint numFrames = 100;

        public override IEnumerator Run(ITestableChunkManager chunkManager)
        {
            ResetLog();
            ResetPerFrameCounters();

            chunkManager.Initialise();

            //Simply record for a set number of frames
            for (int i = 0; i < numFrames; i++)
            {
                UpdatePerFrameCounters();
                yield return null;
            }

            Log($"Recorded {frameCounter.FrameTimesMillis.Count} frames");
            Log($"Peak memory usage of {memoryCounter.memoryPerFrame.Max()}");

        }
    }
}