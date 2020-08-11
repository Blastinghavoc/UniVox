using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniVox.Framework;
using UniVox.Implementations.Meshers;
using UniVox.Implementations.Providers;

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
        public ChunkManager chunkManager;

        /// <summary>
        /// Runs setup for multiple passes over all tests
        /// in the suite, with different configurations.
        /// </summary>
        /// <returns></returns>
        public abstract IEnumerator Passes();

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


    }

    public class FlatworldSuite : AbstractTestSuite
    {
        /// <summary>
        /// Expects the chunk manager to be kept up to date externally.
        /// </summary>
        /// <returns></returns>
        public override IEnumerator Passes()
        {
            ///For each mesh algorithm, with fixed storage type (flat array)
            
            //Naive
            RemoveComponentsOfTypeThatAreNotSubtype<AbstractMesherComponent, NaiveMesher>();
            Common();
            yield return null;

            //Culling
            RemoveComponentsOfTypeThatAreNotSubtype<AbstractMesherComponent, CullingMesher>();
            Common();
            yield return null;
        }

        private void Common() 
        {
            RemoveComponentsOfTypeThatAreNotSubtype<AbstractProviderComponent, DebugProvider>();
            chunkManager.gameObject.GetComponent<DebugProvider>().worldType = DebugProvider.WorldType.flat;
        }
    }
}