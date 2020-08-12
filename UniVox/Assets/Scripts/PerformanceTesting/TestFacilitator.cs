using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.CrossPlatformInput;
using UniVox.Framework;
using UniVox.Gameplay;
using UniVox.Implementations.Meshers;
using UniVox.Implementations.Providers;
using UniVox.MessagePassing;

namespace PerformanceTesting
{
    /// <summary>
    /// Facilitates automated performance testing
    /// </summary>
    public class TestFacilitator : MonoBehaviour
    {
        private static TestFacilitator instance;

        public string TestResultPath;//Directory path, including final "\"
        public string LogFileName;
        public string FileExtension = ".csv";
        public uint NumRepeats = 0;

        private string chunkManagerName;

        public static VirtualPlayerInput virtualPlayer;

        private Transform Worlds;

        private HashSet<string> filesWithHeader;

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

            filesWithHeader = new HashSet<string>();

            if (SceneMessagePasser.TryConsumeMessage<PerformanceTestFilepathMessage>(out var message))
            {
                TestResultPath = message.filepath;
            }

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
                    chunkManagerName = obj.name;
                    break;
                }
            }

            StartCoroutine(RunTests());

        }

        private IEnumerator RunTests()
        {
            VoxelPlayer player;            
            GameObject managerObj;
            ChunkManager manager;

            foreach (var testSuite in GetComponentsInChildren<AbstractTestSuite>())
            {
                testSuite.Initialise();
                var suiteName = testSuite.SuiteName;
                //For each repeat
                for (int repeatIndex = 0; repeatIndex <= NumRepeats; repeatIndex++)
                {

                    //Run all tests in the suite
                    for (int testIndex = 0; testIndex < testSuite.Tests.Length; testIndex++)
                    {
                        managerObj = Worlds.Find(chunkManagerName).gameObject;
                        manager = managerObj.GetComponent<ChunkManager>();
                        testSuite.SetManagerForNextPass(manager);

                        //Run all passes in the suite
                        foreach (var passDetails in testSuite.Passes())
                        {
                            yield return null;//Give time for component removals from pass to be applied

                            float startTime = Time.unscaledTime;

                            var groupName = passDetails.GroupName;
                            var test = testSuite.Tests[testIndex];
                            var testDirectory = $@"{@TestResultPath}{@suiteName}\{groupName}\";
                            EnsureDirectoryExists(testDirectory);

                            var fileName = $"{test.TestName}";
                            var completeFilePath = @testDirectory + @fileName + @FileExtension;
                            using (StreamWriter log = new StreamWriter(@TestResultPath + @LogFileName + @FileExtension,true))
                            {

                                var testRunIdentifier = $"{suiteName}\\{groupName}\\{fileName}\\{passDetails.TechniqueName}\\R{repeatIndex}";
                                log.WriteLine($"\n\n\nTest {testRunIdentifier}:");
                                Debug.Log($"Starting test for {testRunIdentifier}");

                                //Reset virtual input
                                virtualPlayer = new VirtualPlayerInput();
                                CrossPlatformInputManager.SetActiveInputMethod(virtualPlayer);

                                managerObj.SetActive(true);
                                player = FindObjectOfType<VoxelPlayer>();
                                player.enabled = true;

                                //Run the test
                                yield return StartCoroutine(test.Run(manager));

                                bool makeHeader = !filesWithHeader.Contains(completeFilePath);

                                //Write outputs
                                using (StreamWriter testResults = new StreamWriter(completeFilePath,true))
                                {
                                    WriteTestLog(log, test);
                                    WriteTestResults(testResults, test, makeHeader, passDetails.TechniqueName, repeatIndex);
                                    filesWithHeader.Add(completeFilePath);
                                }

                                ///Cleanup--------------
                                test.Clear();
                                //reload the scene
                                yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
                                //Do garbage collection
                                GC.Collect(GC.MaxGeneration, GCCollectionMode.Default, true);

                                yield return new WaitForSecondsRealtime(2);
                                //Locate Worlds object in scene
                                Worlds = GameObject.Find("VoxelWorlds").transform;
                                //Locate manager again for next pass
                                managerObj = Worlds.Find(chunkManagerName).gameObject;
                                manager = managerObj.GetComponent<ChunkManager>();
                                testSuite.SetManagerForNextPass(manager);

                                float duration = Time.unscaledTime - startTime;
                                log.WriteLine($"\nFinished after total time of {duration}");
                            }
                        }
                    }
                }

            }

            Debug.Log("All tests done");

            ///Must initalise manager, as it is not guaranteed to exit correctly otherwise
            ///(Stuff needs disposing)
            managerObj = Worlds.Find(chunkManagerName).gameObject;

            ///Ensure manager is in a valid configuration (only one provider and one mesher component)
            AbstractTestSuite.RemoveComponentsOfTypeExceptSubtype<AbstractProviderComponent, DebugProvider>(managerObj);
            AbstractTestSuite.RemoveComponentsOfTypeExceptSubtype<AbstractMesherComponent, CullingMesher>(managerObj);

            managerObj.SetActive(true);
            manager = managerObj.GetComponent<ChunkManager>();
            manager.Initialise();

            manager = null;
            managerObj = null;
            yield return SceneManager.LoadSceneAsync("MainMenu");
            //Do garbage collection
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Default, true);

            //Destroy this object
            Destroy(gameObject);
        }

        private void WriteTestLog(StreamWriter log, IPerformanceTest test)
        {
            foreach (var line in test.GetLogLines())
            {
                log.WriteLine(line);
            }
        }

        private void WriteTestResults(StreamWriter testResults, IPerformanceTest test, bool withHeader, string techniqueName, int repeatNumber)
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
                sb.Append($",RepeatNumber");
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
                line.Append($",{repeatNumber}");

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