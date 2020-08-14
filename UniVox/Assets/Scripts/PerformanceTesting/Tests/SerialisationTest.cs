using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework;
using UniVox.Framework.Serialisation;

namespace PerformanceTesting
{
    /// <summary>
    /// Similar to the place/delete test, this test deletes a large section of
    /// voxels, but then measures the time to save and the resulting file size.
    /// </summary>
    public class SerialisationTest : AbstractPerformanceTest
    {
        [Range(0, 30)]
        public uint cubeDimension = 10;
        public override IEnumerator Run(ITestableChunkManager chunkManager)
        {
            Assert.IsTrue(SaveUtils.DoSave);
            Assert.IsFalse(string.IsNullOrEmpty(SaveUtils.WorldName), "World name cannot be empty for serialisation test");

            //Ensure save does not already exist by deleting it if it does
            if (Directory.Exists(SaveUtils.CurrentWorldSaveDirectory))
            {
                SaveUtils.DeleteSave(SaveUtils.WorldName);
            }

            ResetLog();
            ResetPerFrameCounters();

            chunkManager.Initialise();

            //Wait until all chunks are complete
            while (!chunkManager.PipelineIsSettled())
            {
                yield return null;
            }

            var worldInterface = FindObjectOfType<VoxelWorldInterface>();

            yield return new WaitForSecondsRealtime(1);

            var player = chunkManager.GetPlayer();
            var playerPos = player.Position;
            var basePosition = playerPos + new Vector3(2, -1, 2);

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
                yield return null;
            }


            ///Now the real test, how long does it take to save,
            ///how many chunks were saved, and how much storage space did 
            ///it take to save them.
            var startTime = Time.unscaledTime;

            //Save
            chunkManager.StoreAllModifiedChunks();

            var saveDuration = Time.unscaledTime - startTime;

            var chunkDirectory = SaveUtils.CurrentWorldSaveDirectory + "chunks/";

            Assert.IsTrue(Directory.Exists(chunkDirectory), "Directory does not exist!");

            long totalFileSize = 0;
            int numFiles = 0;
            foreach (var fileName in Directory.GetFiles(chunkDirectory, "*.*", SearchOption.AllDirectories))
            {
                numFiles++;
                totalFileSize += new FileInfo(fileName).Length;
            }

            Log($"Took {saveDuration * 1000} millis to save {numFiles} chunks, " +
                $"\nwith a total file size of {totalFileSize} bytes");

        }
    }
}