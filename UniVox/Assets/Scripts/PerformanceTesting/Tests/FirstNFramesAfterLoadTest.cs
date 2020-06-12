using UnityEngine;
using System.Collections;
using System.Linq;
using UniVox.Framework;
using System.Text;
using UnityEditor;
using System.Collections.Generic;

namespace PerformanceTesting
{
    /// <summary>
    /// Record data for the first N frames after the world loads
    /// </summary>
    public class FirstNFramesAfterLoadTest : MonoBehaviour, IPerformanceTest
    {
        public string TestName { get; private set; } = "FirstNFrames";

        public uint numFrames = 100;

        private FrameCounter frameCounter;
        private MemoryCounter memoryCounter;

        public IEnumerator Run(ITestableChunkManager chunkManager)
        {
            frameCounter = new FrameCounter();
            memoryCounter = new MemoryCounter();

            chunkManager.Initialise();

            //Simply record for a set number of frames
            for (int i = 0; i < numFrames; i++)
            {
                Step();
                yield return null;
            }
            Debug.Log($"Recorded {frameCounter.FrameTimesMillis.Count} frames over a total time of {frameCounter.FrameTimesMillis.Sum()/1000}s, with a peak memory usage of {memoryCounter.memoryPerFrame.Max()}");

        }
        private void Step()
        {
            frameCounter.Update();
            memoryCounter.Update();
        }

        public string[] GetCSVLines()
        {
            List<string> lines = new List<string>();

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