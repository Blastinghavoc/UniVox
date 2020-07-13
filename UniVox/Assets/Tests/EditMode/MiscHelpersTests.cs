using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using UnityEngine;

namespace Tests
{
    public class MiscHelpersTests
    {

        [SetUp]
        public void Reset()
        {
        }

        [Test]
        public void SameSign()
        {
            KeyValuePair<Vector2Int, bool>[] cases = new KeyValuePair<Vector2Int, bool>[] {
                new KeyValuePair<Vector2Int,bool>(new Vector2Int(0,0),true),
                new KeyValuePair<Vector2Int,bool>(new Vector2Int(0,1),true),
                new KeyValuePair<Vector2Int,bool>(new Vector2Int(1,0),true),
                new KeyValuePair<Vector2Int,bool>(new Vector2Int(1,12341234),true),
                new KeyValuePair<Vector2Int,bool>(new Vector2Int(-1,0),false),
                new KeyValuePair<Vector2Int,bool>(new Vector2Int(-1,-1),true),
                new KeyValuePair<Vector2Int,bool>(new Vector2Int(-1,-13241234),true),
                new KeyValuePair<Vector2Int,bool>(new Vector2Int(int.MinValue,int.MaxValue),false),
            };

            for (int i = 0; i < cases.Length; i++)
            {
                var currentCase = cases[i];
                Assert.AreEqual(Utils.Helpers.SameSign(currentCase.Key.x, currentCase.Key.y), currentCase.Value,
                    $"Case {i} failed");
            }
        }
    }

}
