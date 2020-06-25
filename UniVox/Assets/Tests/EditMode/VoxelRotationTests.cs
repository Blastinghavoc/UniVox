using NUnit.Framework;
using NUnit.Framework.Internal;
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

            rot.x = 3;
            Assert.AreEqual(3, rot.x);
            rot.x -= 1;
            Assert.AreEqual(2, rot.x);
        }

        [Test]
        public void Y()
        {
            VoxelRotation rot = new VoxelRotation();
            Assert.AreEqual(0, rot.y);
            Assert.AreEqual(0, rot.x);

            rot.y = 3;
            Assert.AreEqual(3, rot.y);
            rot.y -= 1;
            Assert.AreEqual(2, rot.y);
        }

        [Test]
        public void YX()
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

    }
}
