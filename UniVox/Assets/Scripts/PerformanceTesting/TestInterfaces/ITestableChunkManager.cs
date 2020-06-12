using UnityEngine;
using System.Collections;
using UniVox.Framework;

namespace PerformanceTesting
{
    public interface ITestableChunkManager:IChunkManager
    {
        bool AllChunksInTargetState();

        Rigidbody GetPlayer();
    }
}