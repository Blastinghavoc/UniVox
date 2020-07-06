using NUnit.Framework;
using NUnit.Framework.Internal;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;

namespace Tests
{
    public class SVOTests
    {

        [SetUp]
        public void Reset()
        {
        }

        [Test]
        public void GetFromEmptyIsAir() 
        {
            SVO svo = new SVO(new Vector3Int(16, 16, 16));

            var testId = new VoxelTypeID();

            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    for (int k = 0; k < 16; k++)
                    {
                        var id = svo.Get(i, j, k);
                        Assert.AreEqual(testId, id, $"Getting coordinates {i},{j},{k} from empty svo did not return air");
                    }
                }
            }
        }

        [Test]
        public void SetSingleVoxel() 
        {
            SVO svo = new SVO(new Vector3Int(16, 16, 16));
            var testId = new VoxelTypeID(5);
            int x = 10;
            int y = 4;
            int z = 8;
            svo.Set(x, y, z, testId);
            Assert.AreEqual(testId, svo.Get(x, y, z), $"Get did not return the previously set item");

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
                        var id = svo.Get(i, j, k);
                        Assert.AreEqual(Air, id, $"Coordinates {i},{j},{k} were not empty, but nothing was placed here");
                    }
                }
            }
        }

        [Test]
        public void SetAndRemoveVoxel()
        {
            SVO svo = new SVO(new Vector3Int(16, 16, 16));
            var testId = new VoxelTypeID(5);
            var Air = new VoxelTypeID();          
            int x = 10;
            int y = 4;
            int z = 8;
            svo.Set(x, y, z, testId);
            svo.Set(x, y, z, Air);
            Assert.AreEqual(Air, svo.Get(x, y, z), $"Location was not empty after being set to air");
            Assert.IsTrue(svo.IsEmpty, "SVO not empty after having item removed from it");
        }

        [Test]
        public void SetMany() 
        {
            SVO svo = new SVO(new Vector3Int(16, 16, 16));
            Vector3Int center = new Vector3Int(7, 7, 7);
            VoxelTypeID testId = new VoxelTypeID(5);

            VoxelTypeID[,,] authoritativeArray = new VoxelTypeID[16, 16, 16];

            foreach (var item in Utils.Helpers.ManhattanCircle(center,3))
            {
                svo.Set(item.x, item.y, item.z, testId);
                authoritativeArray[item.x, item.y, item.z] = testId;
            }

            for (int z = 0; z < 16; z++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        Assert.AreEqual(authoritativeArray[x, y, z], svo.Get(x, y, z), $"Coordinates {x},{y},{z} not equal");
                    }
                }
            }

        }

        [Test]
        public void LocalCoords() 
        {
            SVO svo = new SVO(new Vector3Int(16, 16, 16));

            Vector3Int[] expected = new Vector3Int[8] {
                new Vector3Int(0,0,0),
                new Vector3Int(1,0,0),
                new Vector3Int(0,1,0),
                new Vector3Int(1,1,0),
                new Vector3Int(0,0,1),
                new Vector3Int(1,0,1),
                new Vector3Int(0,1,1),
                new Vector3Int(1,1,1),            
            };

            for (int i = 0; i < 8; i++)
            {
                var returnedLocal = svo.getLocalCoords(i);
                Assert.AreEqual(expected[i], returnedLocal);
            }
        }

        [Test]
        public void ToArray() 
        {
            int3 dimensions = new int3(16);
            SVO svo = new SVO(new Vector3Int(dimensions.x, dimensions.y, dimensions.z));
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
                svo.Set(item.x, item.y, item.z, testId);
            }

            var array = svo.ToArray();

            foreach (var item in testPositions)
            {
                var flat = Utils.Helpers.MultiIndexToFlat(item.x, item.y, item.z, dimensions);
                var value = array[flat];
                Assert.AreEqual(testId, value,$"Array index {item} was not correctly translated");
            }
        }

        [Test]
        public void FromArrayAndBack() 
        {
            int3 dimensions = new int3(16);
            SVO svo;
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
                sourceArray[Utils.Helpers.MultiIndexToFlat(item.x, item.y, item.z, dimensions)] = testId;
            }

            svo = new SVO(dimensions.ToBasic(), sourceArray);

            var resultArray = svo.ToArray();

            for (int i = 0; i < sourceArray.Length; i++)
            {
                Utils.Helpers.FlatIndexToMulti(i, dimensions, out var x, out var y, out var z);
                Assert.AreEqual(sourceArray[i], resultArray[i],$"Result array did not match source for coordinates {x},{y},{z}");
            }
        }

        [Test]
        public void FromArrayEmpty() 
        {
            int3 dimensions = new int3(16);
            var sourceArray = new VoxelTypeID[dimensions.x * dimensions.y * dimensions.z];
            SVO svo = new SVO(dimensions.ToBasic(), sourceArray);
            Assert.IsTrue(svo.IsEmpty, $"Octree not empty after initialisation from empty array");
        }

        [Test]
        public void FromArrayFull()
        {
            int3 dimensions = new int3(16);
            var sourceArray = new VoxelTypeID[dimensions.x * dimensions.y * dimensions.z];
            var testId = new VoxelTypeID(5);

            for (int i = 0; i < sourceArray.Length; i++)
            {
                sourceArray[i] = testId;
            }

            SVO svo = new SVO(dimensions.ToBasic(), sourceArray);
            Assert.IsFalse(svo.IsEmpty, $"Octree empty after initialisation from full array");
        }
    }
}
