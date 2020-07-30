using UnityEngine;
using System.Collections;
using UniVox.Framework;
using System;
using UniVox.Framework.PlayAreaManagement;

namespace PerformanceTesting
{
    public interface ITestableChunkManager:IChunkManager
    {
        PlayAreaManager PlayArea { get; }

        bool PipelineIsSettled();

        IVoxelPlayer GetPlayer();

        string GetPipelineStatus();
        string GetMinPipelineStageOfChunkByName(Vector3Int chunkId);
    }
}