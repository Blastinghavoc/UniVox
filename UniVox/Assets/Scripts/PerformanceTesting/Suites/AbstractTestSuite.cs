using System;
using System.Collections.Generic;
using UnityEngine;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;
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

        public string SuiteName;

        public IPerformanceTest[] Tests;

        protected ChunkManager chunkManager;
        public void Initialise()
        {
            Tests = GetComponentsInChildren<IPerformanceTest>();
        }

        protected AbstractMesherComponent mesher;
        protected AbstractProviderComponent provider;

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

        protected void Clear()
        {
            chunkManager = null;
            mesher = null;
            provider = null;
        }

        /// <summary>
        /// Removes all components of given type except one subtype.
        /// Assumes that only a single component will be left, and returns it.
        /// </summary>
        /// <typeparam name="Type"></typeparam>
        /// <typeparam name="SubType"></typeparam>
        /// <returns></returns>
        protected SubType RemoveComponentsOfTypeExceptSubtype<Type, SubType>()
            where Type : UnityEngine.Object where SubType : UnityEngine.Object
        {
            Type[] components = chunkManager.gameObject.GetComponents<Type>();

            SubType DesiredObject = null;

            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp is SubType desired)
                {
                    if (DesiredObject == null)
                    {
                        DesiredObject = desired;
                    }
                    else
                    {
                        throw new Exception("More than one component of the subtype was found");
                    }
                }
                else
                {
                    Destroy(comp);
                }
            }

            return DesiredObject;
        }

        public static SubType RemoveComponentsOfTypeExceptSubtype<Type, SubType>(GameObject obj)
            where Type : UnityEngine.Object where SubType : UnityEngine.Object
        {
            Type[] components = obj.GetComponents<Type>();

            SubType DesiredObject = null;

            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp is SubType desired)
                {
                    if (DesiredObject == null)
                    {
                        DesiredObject = desired;
                    }
                    else
                    {
                        throw new Exception("More than one component of the subtype was found");
                    }
                }
                else
                {
                    Destroy(comp);
                }
            }

            return DesiredObject;
        }

        protected string GetTechniqueName()
        {
            string prefix = "";
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

        protected virtual PassDetails EndPass(string groupName)
        {
            PassDetails details = new PassDetails() { GroupName = groupName, TechniqueName = GetTechniqueName() };
            Clear();
            return details;
        }


        public class PassDetails
        {
            public string GroupName;
            public string TechniqueName;
        }

    }
}