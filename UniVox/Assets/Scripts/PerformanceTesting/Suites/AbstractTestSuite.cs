using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniVox.Framework;
using UniVox.Implementations.Meshers;
using UniVox.Implementations.Providers;
using UniVox.Implementations.ChunkData;

namespace PerformanceTesting
{
    /// <summary>
    /// A collection of tests to be run with a particular configuration
    /// 
    /// The individual tests should be added as components to the test suite
    /// game object
    /// </summary>
    public abstract class AbstractTestSuite : MonoBehaviour 
    {

        public string SuiteName;

        public IPerformanceTest[] Tests;

        protected ChunkManager chunkManager;
        public void Initialise()
        {
            Tests = GetComponentsInChildren<IPerformanceTest>();
        }

        /// <summary>
        /// Runs setup for multiple passes over all tests
        /// in the suite, with different configurations.
        /// 
        /// Returned values are pass details
        /// </summary>
        /// <returns></returns>
        public abstract IEnumerable<PassDetails> Passes();

        public void SetManagerForNextPass(ChunkManager managerForNextPass) 
        {
            chunkManager = managerForNextPass;
        }

        protected void RemoveComponentsOfTypeThatAreNotSubtype<Type,SubType>()
            where Type: UnityEngine.Object
        {
            Type[] components = chunkManager.gameObject.GetComponents<Type>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (!(comp is SubType))
                {
                    Destroy(comp);
                }
            }
        }

        protected string GetTechniqueName() 
        {
            string prefix = "";
            var mesher = chunkManager.gameObject.GetComponent<AbstractMesherComponent>();
            switch (mesher)
            {
                case NaiveMesher n:
                    prefix = "Naive";
                    break;
                case CullingMesher m:
                    prefix = "Culling";
                    break;
                case GreedyMesher g:
                    prefix = "Greedy";
                    break;
                default:
                    throw new Exception("Unrecongised mesher");
            }

            var provider = chunkManager.GetComponent<AbstractProviderComponent>();
            var chunkDataType = ChunkDataFactory.ChunkDataType.FlatArray;
            switch (provider)
            {
                case DebugProvider d:
                    chunkDataType = d.chunkDataFactory.typeToCreate;
                    break;
                case NoisyProvider n:
                    chunkDataType = n.chunkDataFactory.typeToCreate;
                    break;
                default:
                    throw new Exception("Unrecognised provider");
            }

            string suffix = chunkDataType.ToString();

            return prefix + "_" + suffix;

        }


        public class PassDetails 
        {
            public string GroupName;
            public string TechniqueName;
        }

    }
}