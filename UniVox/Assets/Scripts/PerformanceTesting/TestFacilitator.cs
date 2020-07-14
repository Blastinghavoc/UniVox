using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityStandardAssets.CrossPlatformInput;
using UniVox.Gameplay;

namespace PerformanceTesting
{
    /// <summary>
    /// Facilitates automated performance testing
    /// </summary>
    public class TestFacilitator : MonoBehaviour
    {
        private static TestFacilitator instance;

        public string TestResultPath;
        public string LogFileName;
        public string FileExtension = ".csv";
        public uint NumRepeats = 0;

        private List<string> chunkManagerNames = new List<string>();

        public static VirtualPlayerInput virtualPlayer;

        private Transform Worlds;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            if (Worlds == null)
            {
                Worlds = GameObject.Find("VoxelWorlds").transform;
            }

            foreach (Transform child in Worlds)
            {
                var chunkManager = child.GetComponent<ITestableChunkManager>();
                if (chunkManager != null)
                {
                    var obj = child.gameObject;
                    chunkManagerNames.Add(obj.name);
                }
            }

            StartCoroutine(RunTests());

        }

        private IEnumerator RunTests()
        {
            VoxelPlayer player = FindObjectOfType<VoxelPlayer>();
            player.enabled = false;
            using (StreamWriter log = new StreamWriter(@TestResultPath + @LogFileName + @FileExtension))
            {

                //For each test
                foreach (var test in GetComponents<IPerformanceTest>())
                {
                    var testDirectory = $@"{@TestResultPath}\{@test.TestName}\";
                    EnsureDirectoryExists(testDirectory);

                    for (int repeatIndex = 0; repeatIndex <= NumRepeats; repeatIndex++)
                    {
                        var fileName = $"{test.TestName}_R{repeatIndex}";
                        var completeFilePath = @testDirectory + @fileName + @FileExtension;

                        Debug.Log($"Running test {test.TestName} repeat {repeatIndex} of {NumRepeats}");

                        using (StreamWriter testResults = new StreamWriter(completeFilePath))
                        {
                            log.WriteLine($"\n\n\nTest {fileName}:");
                            float startTime = Time.unscaledTime;
                            //For each chunk manager
                            for (int i = 0; i < chunkManagerNames.Count; i++)
                            {
                                Debug.Log($"Starting test for {chunkManagerNames[i]}");

                                var gameObj = Worlds.Find(chunkManagerNames[i]).gameObject;
                                var manager = gameObj.GetComponent<ITestableChunkManager>();

                                //Reset virtual input
                                virtualPlayer = new VirtualPlayerInput();
                                CrossPlatformInputManager.SetActiveInputMethod(virtualPlayer);

                                gameObj.SetActive(true);
                                player.enabled = true; ;

                                float subtestStartTime = Time.unscaledTime;                                

                                //Run the test
                                yield return StartCoroutine(test.Run(manager));

                                float subtestDuration = Time.unscaledTime - subtestStartTime;

                                log.WriteLine($"\nWith chunk manager {gameObj.name} took {subtestDuration} seconds");

                                //Write to log
                                foreach (var line in test.GetLogLines())
                                {
                                    log.WriteLine(line);
                                }

                                WriteTestResults(testResults, test, i == 0, gameObj.name);

                                //Cleanup
                                //reload the scene
                                yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
                                //Do garbage collection
                                GC.Collect(GC.MaxGeneration, GCCollectionMode.Default, true);
                                player = FindObjectOfType<VoxelPlayer>();
                                player.enabled = false;

                                yield return new WaitForSecondsRealtime(2);
                                //Locate Worlds object in scene
                                Worlds = GameObject.Find("VoxelWorlds").transform;
                            }
                            float duration = Time.unscaledTime - startTime;
                            log.WriteLine($"\nFinished after total time of {duration}");
                        }
                    }

                }
            }

            Debug.Log("All tests done");
#if UNITY_EDITOR
            // Application.Quit() does not work in the editor so
            // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
            UnityEditor.EditorApplication.isPlaying = false;
#else
         Application.Quit();
#endif

        }

        private void WriteTestResults(StreamWriter testResults, IPerformanceTest test, bool withHeader, string techniqueName)
        {
            var data = test.GetPerFrameData();
            if (data.Count < 1)
            {
                return;
            }

            var variableNames = data.Keys.ToList();

            if (withHeader)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(variableNames[0]);
                for (int i = 1; i < variableNames.Count; i++)
                {
                    sb.Append($",{variableNames[i]}");
                }
                sb.Append($",Technique");
                testResults.WriteLine(sb.ToString());
            }

            List<List<string>> variableData = new List<List<string>>(variableNames.Count);

            for (int i = 0; i < variableNames.Count; i++)
            {
                variableData.Add(data[variableNames[i]]);
            }

            //All data must be the same length
            var length = variableData[0].Count;

            for (int i = 0; i < length; i++)
            {
                StringBuilder line = new StringBuilder();
                line.Append($"{variableData[0][i]}");
                for (int j = 1; j < variableData.Count; j++)
                {
                    line.Append($",{variableData[j][i]}");
                }

                line.Append($",{techniqueName}");

                testResults.WriteLine(line.ToString());
            }

        }

        private static void EnsureDirectoryExists(string filePath)
        {
            FileInfo fi = new FileInfo(filePath);
            if (!fi.Directory.Exists)
            {
                Directory.CreateDirectory(fi.DirectoryName);
            }
        }

    }
}