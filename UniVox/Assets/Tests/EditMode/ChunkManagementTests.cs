using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UniVox.Framework;

namespace Tests
{
    public class ChunkManagementTests
    {

        [SetUp]
        public void Reset()
        {
        }

        [Test]
        public void CuboidalAreaFromZero() 
        {            
            Vector3Int center = new Vector3Int(0, 0, 0);
            Vector3Int radii = new Vector3Int(2, 2, 2);

            var definitive = GenerateDefinitive(center, radii);

            List<Vector3Int> fromGenerator = Utils.Helpers.CuboidalArea(center, radii).ToList();

            Assert.AreEqual(definitive.Count, fromGenerator.Count,$"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (var item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        [Test]
        public void CuboidalAreaFromNonZero()
        {
            Vector3Int center = new Vector3Int(16, 12, -5);
            Vector3Int radii = new Vector3Int(2, 2, 2);

            var definitive = GenerateDefinitive(center, radii);

            List<Vector3Int> fromGenerator = Utils.Helpers.CuboidalArea(center, radii).ToList();

            Assert.AreEqual(definitive.Count, fromGenerator.Count, $"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (var item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        [Test]
        public void CuboidalAreaWithStartRadii()
        {
            Vector3Int center = new Vector3Int(0, 0, 0);
            Vector3Int radii = new Vector3Int(2, 2, 2);
            Vector3Int startRadii = new Vector3Int(1, 1, 1);

            var definitive = GenerateDefinitive(center, radii,startRadii);

            List<Vector3Int> fromGenerator = Utils.Helpers.CuboidalArea(center, radii,startRadii).ToList();

            foreach (var item in definitive)
            {
                Assert.IsTrue(fromGenerator.Contains(item), $"Definitive produced {item} which was not in generated list");
            }

            Assert.AreEqual(definitive.Count, fromGenerator.Count, $"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (var item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        [Test]
        public void CuboidalAreaFromNonZeroWithNoValid()
        {
            Vector3Int center = new Vector3Int(16, 12, -5);
            Vector3Int radii = new Vector3Int(2, 2, 2);
            Vector3Int startRadii = new Vector3Int(3, 3, 3);//Start is greater than end

            var definitive = GenerateDefinitive(center, radii, startRadii);

            List<Vector3Int> fromGenerator = Utils.Helpers.CuboidalArea(center, radii, startRadii).ToList();

            Assert.AreEqual(0, fromGenerator.Count);

            Assert.AreEqual(definitive.Count, fromGenerator.Count, $"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (var item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        [Test]
        public void CuboidalAreaWithStartEqualsEndRadii()
        {
            Vector3Int center = new Vector3Int(16, 12, -5);
            Vector3Int radii = new Vector3Int(5, 5, 5);
            Vector3Int startRadii = radii;

            var definitive = GenerateDefinitive(center, radii, startRadii);

            List<Vector3Int> fromGenerator = Utils.Helpers.CuboidalArea(center, radii, startRadii).ToList();

            Assert.AreEqual(definitive.Count, fromGenerator.Count, $"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (var item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        [Test]
        public void CuboidalAreaNonUniformEndRadii() 
        {
            Vector3Int center = new Vector3Int(0, 0, 0);
            Vector3Int radii = new Vector3Int(2, 5, 2);
            Vector3Int startRadii = new Vector3Int(1, 1, 1);

            var definitive = GenerateDefinitive(center, radii, startRadii);

            List<Vector3Int> fromGenerator = Utils.Helpers.CuboidalArea(center, radii, startRadii).ToList();

            foreach (var item in definitive)
            {
                Assert.IsTrue(fromGenerator.Contains(item), $"Definitive produced {item} which was not in generated list");
            }

            Assert.AreEqual(definitive.Count, fromGenerator.Count, $"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (var item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        [Test]
        public void CuboidalAreaNonUniformStartAndEndRadii()
        {
            Vector3Int center = new Vector3Int(0, 0, 0);
            Vector3Int radii = new Vector3Int(2, 5, 2);
            Vector3Int startRadii = new Vector3Int(1, 0, 1);

            var definitive = GenerateDefinitive(center, radii, startRadii);

            List<Vector3Int> fromGenerator = Utils.Helpers.CuboidalArea(center, radii, startRadii).ToList();

            foreach (var item in definitive)
            {
                Assert.IsTrue(fromGenerator.Contains(item), $"Definitive produced {item} which was not in generated list");
            }

            Assert.AreEqual(definitive.Count, fromGenerator.Count, $"The lists should contain the same number of items");
            Debug.Log($"Produced {fromGenerator.Count} items");

            foreach (var item in fromGenerator)
            {
                Assert.IsTrue(definitive.Contains(item), $"Generator produced {item} which was not in definitive list");
            }
        }

        List<Vector3Int> GenerateDefinitive(Vector3Int center,Vector3Int radii,Vector3Int startRadii = default) 
        {            
            Vector3Int maxRadii = radii + Vector3Int.one;
            List<Vector3Int> definitive = new List<Vector3Int>();

            var excludeRadii = startRadii - Vector3Int.one;

            for (int x = -maxRadii.x; x <= maxRadii.x; x++)
            {
                for (int y = -maxRadii.y; y <= maxRadii.y; y++)
                {
                    for (int z = -maxRadii.z; z <= maxRadii.z; z++)
                    {
                        var displacement = new Vector3Int(x, y, z);
                        if (InsideRadius(displacement,radii))
                        {
                            if (!InsideRadius(displacement,excludeRadii))
                            {
                                //Only add elements when they are not inside the excluded radii
                                definitive.Add(displacement + center);
                            }
                        }
                    }
                }
            }

            return definitive;
        }

        bool InsideRadius(Vector3Int displacement, Vector3Int Radii)
        {
            var absDisplacement = displacement.ElementWise(Mathf.Abs);

            //Inside if all elements of the absolute displacement are less than or equal to the radius
            return absDisplacement.All((a, b) => a <= b, Radii);
        }
    }

}
