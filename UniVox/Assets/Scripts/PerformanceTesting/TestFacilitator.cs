using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.CrossPlatformInput;
using UniVox.Framework;

namespace PerformanceTesting
{
    /// <summary>
    /// To be attached to the VoxelWorld to facilitate automated performance testing
    /// </summary>
    public class TestFacilitator : MonoBehaviour
    {
        private static TestFacilitator instance;

        public string TestResultPath;

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
                Destroy(this.gameObject);
                return;
            }

            DontDestroyOnLoad(this.gameObject);

            if (Worlds== null)
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
            using (StreamWriter sw = new StreamWriter(@TestResultPath)) 
            { 
            
                //For each test
                foreach (var test in GetComponents<IPerformanceTest>())
                {
                    //For each chunk manager
                    for (int i = 0; i < chunkManagerNames.Count; i++)
                    {
                        var gameObj = Worlds.Find(chunkManagerNames[i]).gameObject;
                        var manager = gameObj.GetComponent<ITestableChunkManager>();

                        //Reset virtual input
                        virtualPlayer = new VirtualPlayerInput();
                        CrossPlatformInputManager.SetActiveInputMethod(virtualPlayer);

                        gameObj.SetActive(true);

                        yield return StartCoroutine(test.Run(manager));

                        sw.WriteLine($"\nResults for test {test.TestName} with chunk manager {gameObj.name}");
                        foreach (var line in test.GetCSVLines())
                        {
                            sw.WriteLine(line);
                        }

                        //Cleanup
                        //reload the scene
                        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
                        //Do garbage collection
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Default, true);

                        yield return new WaitForSecondsRealtime(2);
                        //Locate Worlds object in scene
                        Worlds = GameObject.Find("VoxelWorlds").transform;
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

    }
}