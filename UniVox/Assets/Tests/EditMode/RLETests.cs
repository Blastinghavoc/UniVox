using NUnit.Framework;
using NUnit.Framework.Internal;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;
using static Utils.Helpers;

namespace Tests
{
    public class RLETests
    {

        RLEArray<VoxelTypeID> rle;
        int3 dimensions;

       [SetUp]
        public void Reset()
        {
            dimensions = new int3(16);
            rle = new RLEArray<VoxelTypeID>(dimensions.ToBasic());
        }

        [Test]
        public void GetFromEmptyIsAir() 
        {
            var testId = new VoxelTypeID();

            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    for (int k = 0; k < 16; k++)
                    {
                        var id = rle.Get(MultiIndexToFlat(i,j,k, dimensions));
                        Assert.AreEqual(testId, id, $"Getting coordinates {i},{j},{k} from empty rle did not return air");
                    }
                }
            }
        }

        [Test]
        public void SetSingleVoxel() 
        {
            var testId = new VoxelTypeID(5);
            int x = 10;
            int y = 4;
            int z = 8;
            rle.Set(MultiIndexToFlat(x, y, z, dimensions), testId);
            Assert.AreEqual(testId, rle.Get(MultiIndexToFlat(x, y, z, dimensions)), $"Get did not return the previously set item");

            var Air = new VoxelTypeID();

            //All other locations should be empty
            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    for (int k = 0; k < 16; k++)
                    {
                        if (i==x && j == y && k == z)
                        {
                            continue;
                        }
                        var id = rle.Get(MultiIndexToFlat(i, j, k, dimensions));
                        Assert.AreEqual(Air, id, $"Coordinates {i},{j},{k} were not empty, but nothing was placed here");
                    }
                }
            }
        }

        [Test]
        public void SetAtEndOfRun()
        {
            var testId = new VoxelTypeID(5);
            int x = 10;
            int y = 4;
            int z = 8;
            var flat = MultiIndexToFlat(x, y, z, dimensions);
            rle.Set(flat, testId);
            Assert.AreEqual(testId, rle.Get(MultiIndexToFlat(x, y, z, dimensions)), $"Get did not return the previously set item");

            var runsAfterOne = rle.NumRuns;
            Assert.AreEqual(3, runsAfterOne);

            rle.Set(flat - 1, testId);
            Assert.AreEqual(runsAfterOne, rle.NumRuns, "Adding at the end of a run failed to extend the subsequent run of the same value");            
        }

        [Test]
        public void SetAtStartOfRun()
        {
            var testId = new VoxelTypeID(5);
            int x = 10;
            int y = 4;
            int z = 8;
            var flat = MultiIndexToFlat(x, y, z, dimensions);
            rle.Set(flat, testId);
            Assert.AreEqual(testId, rle.Get(MultiIndexToFlat(x, y, z, dimensions)), $"Get did not return the previously set item");

            var runsAfterOne = rle.NumRuns;
            Assert.AreEqual(3, runsAfterOne);

            rle.Set(flat + 1, testId);
            Assert.AreEqual(runsAfterOne, rle.NumRuns, "Adding at the start of a run failed to extend the previous run of the same value");
        }

        [Test]
        public void SetAndRemoveVoxel()
        {
            var testId = new VoxelTypeID(5);
            var Air = new VoxelTypeID();          
            int x = 10;
            int y = 4;
            int z = 8;
            rle.Set(MultiIndexToFlat(x, y, z, dimensions), testId);
            rle.Set(MultiIndexToFlat(x, y, z, dimensions), Air);
            Assert.AreEqual(Air, rle.Get(MultiIndexToFlat(x, y, z, dimensions)), $"Location was not empty after being set to air");
            Assert.IsTrue(rle.IsEmpty, "RLEArray<VoxelTypeID> not empty after having item removed from it");
        }

        [Test]
        public void SetMany() 
        {
            Vector3Int center = new Vector3Int(7, 7, 7);
            VoxelTypeID testId = new VoxelTypeID(5);

            VoxelTypeID[,,] authoritativeArray = new VoxelTypeID[16, 16, 16];

            foreach (var item in ManhattanCircle(center,3))
            {
                rle.Set(MultiIndexToFlat(item.x, item.y, item.z, dimensions), testId);
                authoritativeArray[item.x, item.y, item.z] = testId;
            }

            for (int z = 0; z < 16; z++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        Assert.AreEqual(authoritativeArray[x, y, z], rle.Get(MultiIndexToFlat(x, y, z, dimensions)), 
                            $"Coordinates {x},{y},{z} not equal");
                    }
                }
            }

        }

        [Test]
        public void ToArray()
        {
            var testId = new VoxelTypeID(5);
            Vector3Int[] testPositions = new Vector3Int[] {
                new Vector3Int(1,2,3),
                new Vector3Int(4,5,6),
                new Vector3Int(7,8,9),
                new Vector3Int(10,11,12),
                new Vector3Int(0,0,0),
                new Vector3Int(15,15,15),
            };

            foreach (var item in testPositions)
            {
                rle.Set(MultiIndexToFlat(item.x, item.y, item.z, dimensions), testId);
            }

            var array = rle.ToArray();

            foreach (var item in testPositions)
            {
                var flat = MultiIndexToFlat(item.x, item.y, item.z, dimensions);
                var value = array[flat];
                Assert.AreEqual(testId, value, $"Array index {item} was not correctly translated");
            }
        }

        [Test]
        public void FromArrayAndBack()
        {
            var testId = new VoxelTypeID(5);

            var sourceArray = new VoxelTypeID[dimensions.x * dimensions.y * dimensions.z];

            Vector3Int[] testPositions = new Vector3Int[] {
                new Vector3Int(1,2,3),
                new Vector3Int(4,5,6),
                new Vector3Int(7,8,9),
                new Vector3Int(10,11,12),
                new Vector3Int(0,0,0),
                new Vector3Int(15,15,15),
            };

            //Initialise source array
            foreach (var item in testPositions)
            {
                var flat = MultiIndexToFlat(item.x, item.y, item.z, dimensions);
                sourceArray[flat] = testId;
            }

            rle = new RLEArray<VoxelTypeID>(dimensions.ToBasic(),sourceArray);

            var resultArray = rle.ToArray();

            for (int i = 0; i < sourceArray.Length; i++)
            {
                FlatIndexToMulti(i, dimensions, out var x, out var y, out var z);
                Assert.AreEqual(sourceArray[i], resultArray[i], $"Result array did not match source for coordinates {x},{y},{z}");
            }
        }

        [Test]
        public void FromArrayEmpty()
        {
            var sourceArray = new VoxelTypeID[dimensions.x * dimensions.y * dimensions.z];
            RLEArray<VoxelTypeID> rle = new RLEArray<VoxelTypeID>(dimensions.ToBasic(), sourceArray);
            Assert.IsTrue(rle.IsEmpty, $"RLE not empty after initialisation from empty array");
        }

        [Test]
        public void FromArrayFull()
        {
            var sourceArray = new VoxelTypeID[dimensions.x * dimensions.y * dimensions.z];
            var testId = new VoxelTypeID(5);

            for (int i = 0; i < sourceArray.Length; i++)
            {
                sourceArray[i] = testId;
            }

            RLEArray<VoxelTypeID> rle = new RLEArray<VoxelTypeID>(dimensions.ToBasic(),sourceArray);
            Assert.IsFalse(rle.IsEmpty, $"RLE empty after initialisation from full array");
        }
    }
}
