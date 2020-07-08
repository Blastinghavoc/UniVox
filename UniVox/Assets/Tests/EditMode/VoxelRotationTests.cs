using NUnit.Framework;
using NUnit.Framework.Internal;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;

namespace Tests
{
    public class VoxelRotationTests
    {

        [SetUp]
        public void Reset()
        {
        }

        [Test]
        public void X()
        {
            VoxelRotation rot = new VoxelRotation();
            Assert.AreEqual(0, rot.x);
            Assert.AreEqual(0, rot.y);
            Assert.AreEqual(0, rot.z);

            rot.x = 3;
            Assert.AreEqual(3, rot.x);
            rot.x -= 1;
            Assert.AreEqual(2, rot.x);
        }

        [Test]
        public void Y()
        {
            VoxelRotation rot = new VoxelRotation();
            Assert.AreEqual(0, rot.x);
            Assert.AreEqual(0, rot.y);
            Assert.AreEqual(0, rot.z);

            rot.y = 3;
            Assert.AreEqual(3, rot.y);
            rot.y -= 1;
            Assert.AreEqual(2, rot.y);
        }

        [Test]
        public void Z()
        {
            VoxelRotation rot = new VoxelRotation();
            Assert.AreEqual(0, rot.x);
            Assert.AreEqual(0, rot.y);
            Assert.AreEqual(0, rot.z);

            rot.z = 3;
            Assert.AreEqual(3, rot.z);
            rot.z -= 1;
            Assert.AreEqual(2, rot.z);
        }

        [Test]
        public void XY()
        {
            VoxelRotation rot = new VoxelRotation();
            Assert.AreEqual(0, rot.y);
            Assert.AreEqual(0, rot.x);

            rot.x = 3;
            rot.y = 2;
            Assert.AreEqual(3, rot.x);
            Assert.AreEqual(2, rot.y);

            rot.x -= 1;
            Assert.AreEqual(2, rot.x);
            Assert.AreEqual(2, rot.y);
            rot.y += 1;
            Assert.AreEqual(2, rot.x);
            Assert.AreEqual(3, rot.y);
        }


        [Test]
        public void XYZ()
        {
            VoxelRotation rot = new VoxelRotation();
            Assert.AreEqual(0, rot.y);
            Assert.AreEqual(0, rot.x);
            Assert.AreEqual(0, rot.z);

            rot.x = 3;
            rot.y = 2;
            rot.z = 1;
            Assert.AreEqual(3, rot.x);
            Assert.AreEqual(2, rot.y);
            Assert.AreEqual(1, rot.z);

            rot.x -= 1;
            Assert.AreEqual(2, rot.x);
            Assert.AreEqual(2, rot.y);
            Assert.AreEqual(1, rot.z);
            rot.y += 1;
            Assert.AreEqual(2, rot.x);
            Assert.AreEqual(3, rot.y);
            Assert.AreEqual(1, rot.z);
            rot.z += 1;
            Assert.AreEqual(2, rot.x);
            Assert.AreEqual(3, rot.y);
            Assert.AreEqual(2, rot.z);
        }

        [Test]
        public void Blank() 
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        var rot = new VoxelRotation() { x = i, y = j, z = k };
                        bool empty = i == 0 && j == 0 && k == 0;
                        Assert.AreEqual(empty, rot.isBlank,$"Rotation isBlank method returned an unexpected result for x={i},y={j},z={k}");
                    }
                }
            }

        }

        [Test]
        public void VertexTransformation() 
        {
            var directionHelper = DirectionRotatorExtensions.Create();

            //Should rotate 90 degrees about the x axis
            var quat = directionHelper.GetRotationQuat(new VoxelRotation() { x = 1 });

            var testVector = new float3(0, 1, 0);
            //Should become 0,0,1
            var result = math.round(math.mul(quat, testVector));
            Assert.AreEqual(new float3(0, 0, 1), result);

            //Should rotate 180 degress around x then 90 around z
            quat = directionHelper.GetRotationQuat(new VoxelRotation() { x = 2, z = 1 });
            result = math.round(math.mul(quat, testVector));
            Assert.AreEqual(new float3(1,0,0), result);


            directionHelper.Dispose();
        }
    }
}
