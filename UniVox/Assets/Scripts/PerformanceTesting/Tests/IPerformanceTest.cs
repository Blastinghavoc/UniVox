using UnityEngine;
using System.Collections;
using UniVox.Framework;

namespace PerformanceTesting
{
    public interface IPerformanceTest
    {
        IEnumerator Run(ITestableChunkManager chunkManager);

        string TestName { get; }

        string[] GetCSVLines();
    }
}