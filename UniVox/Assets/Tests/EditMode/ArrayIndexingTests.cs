using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Internal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UniVox.Framework;
using UniVox.Framework.ChunkPipeline;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Implementations.ChunkData;
using UniVox.Implementations.Common;

namespace Tests
{
    public class ArrayIndexingTests
    {    

        [SetUp]
        public void Reset()
        {
        }

        [Test]
        public void KnownFlattening()
        {
            Vector3Int dimensions = new Vector3Int(10, 3, 7);

            var testCoords = new Vector3Int(1, 1, 0);

            var flatIndex = Utils.Helper.MultiIndexToFlat(testCoords.x, testCoords.y, testCoords.z, dimensions);

            Assert.AreEqual(11,flatIndex);

            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        Debug.Log(Utils.Helper.MultiIndexToFlat(x, y, z, dimensions));
                    }
                }
            }

        }

        [Test] 
        public void ToFlatAndBack3D() 
        {
            Vector3Int dimensions = new Vector3Int(10, 3, 7);

            var testCoords = new Vector3Int(5, 2, 1);
            
            var flatIndex = Utils.Helper.MultiIndexToFlat(testCoords.x,testCoords.y,testCoords.z, dimensions);

            Debug.Log($"Flattened {testCoords} to {flatIndex}");

            Utils.Helper.FlatIndexToMulti(flatIndex, dimensions, out var x, out var y, out var z);
            var backAgain = new Vector3Int(x,y,z);

            Assert.AreEqual(testCoords, backAgain);

            for (int i = 0; i < dimensions.x * dimensions.y * dimensions.z; i++)
            {
                Utils.Helper.FlatIndexToMulti(i, dimensions, out x, out y, out z);
                Debug.Log($"{x},{y},{z}");
            }

        }

        [Test]
        public void ToFlatAndBack2D() 
        {
            Vector2Int dimensions = new Vector2Int(10, 7);

            var testCoords = new Vector2Int(5, 2);

            var flatIndex = Utils.Helper.MultiIndexToFlat(testCoords.x, testCoords.y, dimensions);

            Debug.Log($"Flattened {testCoords} to {flatIndex}");

            Utils.Helper.FlatIndexToMulti(flatIndex, dimensions, out var x, out var y);
            var backAgain = new Vector2Int(x, y);

            Assert.AreEqual(testCoords, backAgain);

            for (int i = 0; i < dimensions.x * dimensions.y; i++)
            {
                Utils.Helper.FlatIndexToMulti(i, dimensions, out x, out y);
                Debug.Log($"{x},{y}");
            }
        }
       
    }
}
