using UnityEngine;
using System.Collections;
using UniVox.Framework;
using System;

namespace PerformanceTesting
{
    public interface ITestableChunkManager:IChunkManager
    {
        bool PipelineIsSettled();

        Rigidbody GetPlayer();

        string GetPipelineStatus();

    }
}