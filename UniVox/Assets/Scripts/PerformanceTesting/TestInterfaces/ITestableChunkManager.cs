using UnityEngine;
using System.Collections;
using UniVox.Framework;
using System;

namespace PerformanceTesting
{
    public interface ITestableChunkManager:IChunkManager
    {
        int WaitingForPlayAreaUpdate { get; }
        int WaitingForPlayAreaDeactivate { get; }

        bool PipelineIsSettled();

        Rigidbody GetPlayer();

        string GetPipelineStatus();

    }
}