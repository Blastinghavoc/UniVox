using System.Collections;
using System.Linq;
using UnityEngine;

namespace PerformanceTesting
{
    /// <summary>
    /// Records performance statistics while the world loads.
    /// Also records vertex and triangle count of meshes once the world has loaded.
    /// </summary>
    public class TimeToLoadTest : AbstractPerformanceTest
    {
        [Range(0, 300)]
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

            long vertexCount = 0;
            long triangleCount = 0;
            foreach (var meshfilter in FindObjectsOfType<MeshFilter>())
            {
                vertexCount += meshfilter.mesh.vertices.Length;
                //Triangles array contains 3 indices per triangle
                triangleCount += meshfilter.mesh.triangles.Length / 3;
            }
            Log($"Active meshes contained {vertexCount} vertices across {triangleCount} triangles");
        }
    }
}