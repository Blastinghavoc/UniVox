using System.Collections;
using System.Linq;
using UnityEngine;
using UniVox.Framework;

namespace PerformanceTesting
{
    public class PlaceDeleteTest : AbstractPerformanceTest
    {
        [Range(0, 30)]
        public uint cubeDimension = 10;

        public SOVoxelTypeDefinition voxelTypeToPlace;

        public uint gracePeriodFrames = 10;

        public override IEnumerator Run(ITestableChunkManager chunkManager)
        {
            ResetLog();

            chunkManager.Initialise();

            //Wait until all chunks are complete
            while (!chunkManager.PipelineIsSettled())
            {
                yield return null;
            }

            var worldInterface = FindObjectOfType<VoxelWorldInterface>();

            yield return new WaitForSecondsRealtime(1);

            var player = chunkManager.GetPlayer();
            var playerPos = player.position;
            var basePosition = playerPos + new Vector3(2, -1, 2);

            //Start the real test, recording frame time and memory while breaking blocks
            ResetPerFrameCounters();

            var startTime = Time.unscaledTime;

            //Break a cube of blocks
            for (int i = 0; i < cubeDimension; i++)
            {
                for (int j = 0; j < cubeDimension; j++)
                {
                    for (int k = 0; k < cubeDimension; k++)
                    {
                        worldInterface.RemoveVoxel(basePosition + new Vector3(i, -j, k));
                    }
                }
            }            

            //Wait until all chunks are complete
            while (!chunkManager.PipelineIsSettled())
            {
                UpdatePerFrameCounters();
                yield return null;
            }

            var allBrokenTime = Time.unscaledTime - startTime;

            //Wait a few frames (but keep recording)
            for (int i = 0; i < gracePeriodFrames; i++)
            {
                UpdatePerFrameCounters();
                yield return null;
            }

            startTime = Time.unscaledTime;

            //Place the cube of blocks back again
            for (int i = 0; i < cubeDimension; i++)
            {
                for (int j = 0; j < cubeDimension; j++)
                {
                    for (int k = 0; k < cubeDimension; k++)
                    {
                        worldInterface.PlaceVoxel(basePosition + new Vector3(i, -j, k),voxelTypeToPlace);
                    }
                }
            }

            //Wait until all chunks are complete
            while (!chunkManager.PipelineIsSettled())
            {
                UpdatePerFrameCounters();
                yield return null;
            }

            var allReplacedTime = Time.unscaledTime - startTime;
            //DONE
            Log($"Grace period {gracePeriodFrames} frames");
            Log($"Num Blocks: {cubeDimension * cubeDimension * cubeDimension}");
            Log($"Break time millis: {allBrokenTime * 1000}");
            Log($"Place time millis: {allReplacedTime * 1000}");
            Log($"Peak memory: {memoryCounter.memoryPerFrame.Max()}");

        }
    }
}