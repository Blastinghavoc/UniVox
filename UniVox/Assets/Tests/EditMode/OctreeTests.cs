using NUnit.Framework;
using NUnit.Framework.Internal;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;

namespace Tests
{
    public class OctreeTests
    {

        [SetUp]
        public void Reset()
        {
        }

        [Test]
        public void GetFromEmptyIsAir() 
        {
            OctreeVoxelStorage svo = new OctreeVoxelStorage(new Vector3Int(16, 16, 16));

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
            OctreeVoxelStorage svo = new OctreeVoxelStorage(new Vector3Int(16, 16, 16));
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
            OctreeVoxelStorage svo = new OctreeVoxelStorage(new Vector3Int(16, 16, 16));
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

        /// <summary>
        /// Test added in response to bug where leaf could prune the whole tree.
        /// </summary>
        [Test]
        public void RemoveLastVoxelInLeafDoesntPruneTooMuch() 
        {
            OctreeVoxelStorage svo = new OctreeVoxelStorage(new Vector3Int(16, 16, 16));
            var testId = new VoxelTypeID(5);
            var Air = new VoxelTypeID();
            //Add a complete leaf
            svo.Set(0, 0, 0, testId);
            svo.Set(0, 0, 1, testId);
            svo.Set(0, 1, 0, testId);
            svo.Set(0, 1, 1, testId);
            svo.Set(1, 0, 0, testId);
            svo.Set(1, 0, 1, testId);
            svo.Set(1, 1, 0, testId);
            svo.Set(1, 1, 1, testId);

            //Add another node somewhere else
            svo.Set(1, 2, 1, testId);

            //remove all the nodes in the complete leaf
            svo.Set(0, 0, 0, Air);
            svo.Set(0, 0, 1, Air);
            svo.Set(0, 1, 0, Air);
            svo.Set(0, 1, 1, Air);
            svo.Set(1, 0, 0, Air);
            svo.Set(1, 0, 1, Air);
            svo.Set(1, 1, 0, Air);
            svo.Set(1, 1, 1, Air);

            Assert.IsFalse(svo.IsEmpty, $"Octree pruned more than it should have");
        }

        [Test]
        public void SetMany() 
        {
            OctreeVoxelStorage svo = new OctreeVoxelStorage(new Vector3Int(16, 16, 16));
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
            OctreeVoxelStorage svo = new OctreeVoxelStorage(new Vector3Int(16, 16, 16));

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
        public void ToArraySparse() 
        {
            int3 dimensions = new int3(16);
            OctreeVoxelStorage svo = new OctreeVoxelStorage(new Vector3Int(dimensions.x, dimensions.y, dimensions.z));
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
        public void FromArraySparse()
        {
            int3 dimensions = new int3(16);
            OctreeVoxelStorage svo;
            var testId = new VoxelTypeID(5);
            Vector3Int[] testPositions = new Vector3Int[] {
                new Vector3Int(1,2,3),
                new Vector3Int(4,5,6),
                new Vector3Int(7,8,9),
                new Vector3Int(10,11,12),
                new Vector3Int(0,0,0),
                new Vector3Int(15,15,15),
            };

            var sourceArray = new VoxelTypeID[dimensions.x * dimensions.y * dimensions.z];

            //Initialise source array
            foreach (var item in testPositions)
            {
                sourceArray[Utils.Helpers.MultiIndexToFlat(item.x, item.y, item.z, dimensions)] = testId;
            }

            svo = new OctreeVoxelStorage(dimensions.ToBasic(), sourceArray);

            int flat = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++,flat++)
                    {
                        Assert.AreEqual(sourceArray[flat], svo.Get(x, y, z),$"Position {x},{y},{z} at flat index {flat} did not match");
                    }
                }
            }
        }

        [Test]
        public void FromArrayAndBack() 
        {
            int3 dimensions = new int3(16);
            OctreeVoxelStorage svo;
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

            svo = new OctreeVoxelStorage(dimensions.ToBasic(), sourceArray);

            var resultArray = svo.ToArray();

            for (int i = 0; i < sourceArray.Length; i++)
            {
                Utils.Helpers.FlatIndexToMulti(i, dimensions, out var x, out var y, out var z);
                Assert.AreEqual(sourceArray[i], resultArray[i],$"Result array did not match source for coordinates {x},{y},{z}");
            }
        }

        [Test]
        public void FromArrayDense() 
        {
            int3 dimensions = new int3(16);
            var sourceArray = new VoxelTypeID[dimensions.x * dimensions.y * dimensions.z];            

            int flat = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++, flat++)
                    {
                        if (y < dimensions.y/2)
                        {
                            sourceArray[flat] = new VoxelTypeID(1);
                        }
                        else if (x > z)
                        {
                            sourceArray[flat] = new VoxelTypeID(2);
                        }
                    }
                }
            }

            OctreeVoxelStorage svo = new OctreeVoxelStorage(dimensions.ToBasic(), sourceArray);

            flat = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++, flat++)
                    {
                        Assert.AreEqual(sourceArray[flat], svo.Get(x, y, z), $"Position {x},{y},{z} at flat index {flat} did not match");
                    }
                }
            }
        }

        [Test]
        public void FromArrayEmpty() 
        {
            int3 dimensions = new int3(16);
            var sourceArray = new VoxelTypeID[dimensions.x * dimensions.y * dimensions.z];
            OctreeVoxelStorage svo = new OctreeVoxelStorage(dimensions.ToBasic(), sourceArray);
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

            OctreeVoxelStorage svo = new OctreeVoxelStorage(dimensions.ToBasic(), sourceArray);
            Assert.IsFalse(svo.IsEmpty, $"Octree empty after initialisation from full array");

            int flat = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++, flat++)
                    {
                        Assert.AreEqual(sourceArray[flat], svo.Get(x, y, z), $"Position {x},{y},{z} at flat index {flat} did not match");
                    }
                }
            }
        }

        /// <summary>
        /// Check the the result of contstructing from array is the same as
        /// if we just added each individual element
        /// </summary>
        [Test]
        public void FromArrayVsBruteForce() 
        {
            int3 dimensions = new int3(16);
            var sourceArray = new VoxelTypeID[dimensions.x * dimensions.y * dimensions.z];
            var testId = new VoxelTypeID(5);

            OctreeVoxelStorage svo1 = new OctreeVoxelStorage(dimensions.ToBasic());

            int flat = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++, flat++)
                    {
                        sourceArray[flat] = testId;
                        svo1.Set(x, y, z, testId);                       
                    }
                }
            }

            OctreeVoxelStorage svo2 = new OctreeVoxelStorage(dimensions.ToBasic(), sourceArray);

            flat = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++, flat++)
                    {
                        Assert.AreEqual(svo1.Get(x,y,z), svo2.Get(x, y, z), $"Position {x},{y},{z} at flat index {flat} did not match");
                    }
                }
            }

            Assert.AreEqual(svo1, svo2,$"Despite equal elements, the octrees did not compare equal");

        }

        //As above, but with a densely populated array
        [Test]
        public void FromArrayVsBruteForceDense()
        {
            int3 dimensions = new int3(16);
            var sourceArray = new VoxelTypeID[dimensions.x * dimensions.y * dimensions.z];
            var testId = new VoxelTypeID(5);

            OctreeVoxelStorage bruteForceOctree = new OctreeVoxelStorage(dimensions.ToBasic());

            int flat = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++, flat++)
                    {
                        if (y < dimensions.y / 2)
                        {
                            sourceArray[flat] = new VoxelTypeID(1);
                        }
                        else if (x > z)
                        {
                            sourceArray[flat] = new VoxelTypeID(2);
                        }
                        bruteForceOctree.Set(x, y, z, sourceArray[flat]);
                    }
                }
            }            

            OctreeVoxelStorage fromArrayOctree = new OctreeVoxelStorage(dimensions.ToBasic(), sourceArray);

            flat = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++, flat++)
                    {
                        Assert.AreEqual(bruteForceOctree.Get(x, y, z), fromArrayOctree.Get(x, y, z), $"Position {x},{y},{z} at flat index {flat} did not match");
                    }
                }
            }

            Assert.AreEqual(bruteForceOctree, fromArrayOctree, $"Despite equal elements, the octrees did not compare equal");

        }

        [Test]
        public void Equals() 
        {
            int3 dimensions = new int3(16);
            OctreeVoxelStorage svo1 = new OctreeVoxelStorage(dimensions.ToBasic());
            OctreeVoxelStorage svo2 = new OctreeVoxelStorage(dimensions.ToBasic());

            Assert.AreEqual(svo1, svo2,$"Empty octrees not equal");

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
                svo1.Set(item.x, item.y, item.z, testId);
                svo2.Set(item.x, item.y, item.z, testId);
            }

            Assert.AreEqual(svo1, svo2, $"Octrees with same elements not equal");

            svo2.Set(testPositions[0].x, testPositions[0].y, testPositions[0].z, new VoxelTypeID(0));

            Assert.AreNotEqual(svo1, svo2, $"Octrees with different elements should note be equal");
        }
    }
}
