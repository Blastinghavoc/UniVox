using NUnit.Framework;
using NUnit.Framework.Internal;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;
using UniVox.Framework.Common;
using UniVox.Framework.Lighting;

namespace Tests
{
    public class LightValueTests
    {

        [SetUp]
        public void Reset()
        {
        }

        [Test]
        public void Sunlight() 
        {
            LightValue lv = new LightValue();

            for (int i = 0; i < LightValue.IntensityRange; i++)
            {
                lv.Sun = i;
                Assert.AreEqual(i, lv.Sun,$"Light incorrect for value {i}");
            }
        }

        [Test]
        public void Dynamic()
        {
            LightValue lv = new LightValue();

            for (int i = 0; i < LightValue.IntensityRange; i++)
            {
                lv.Dynamic = i;
                Assert.AreEqual(i, lv.Dynamic, $"Light incorrect for value {i}");
            }
        }

        [Test]
        public void Mixed()
        {
            LightValue lv = new LightValue();

            for (int i = 0; i < LightValue.IntensityRange; i++)
            {
                for (int j = 0; j < LightValue.IntensityRange; j++)
                {
                    lv.Sun = i;
                    Assert.AreEqual(i, lv.Sun, $"Sun light incorrect for value {i},{j}");

                    lv.Dynamic = j;
                    Assert.AreEqual(j, lv.Dynamic, $"Dynamic light incorrect for value {i},{j}");

                }
            }
        }
    }
}
