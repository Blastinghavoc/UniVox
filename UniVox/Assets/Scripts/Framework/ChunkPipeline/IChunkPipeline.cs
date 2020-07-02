using System;
using UnityEngine;

namespace UniVox.Framework.ChunkPipeline
{
    public interface IChunkPipeline 
    {
        event Action<Vector3Int> OnChunkRemovedFromPipeline;
        //args: id, added at stage
        event Action<Vector3Int,int> OnChunkAddedToPipeline;
        //args: id, new min stage
        event Action<Vector3Int, int> OnChunkMinStageDecreased;
        //args: id, new target stage
        event Action<Vector3Int, int> OnChunkTargetStageDecreased;

        IChunkProvider chunkProvider { get; }
        Func<Vector3Int, IChunkComponent> getChunkComponent { get; }
        bool GenerateStructures { get; }
        IChunkMesher chunkMesher { get; }

        bool NextStageFreeForChunk(Vector3Int chunkID, int currentStage);

        bool TargetStageGreaterThanCurrent(Vector3Int chunkID, int currentStage);
        bool TargetStageGreaterThanCurrent(int currentStage, ChunkStageData stageData);

        IPipelineStage GetStage(int stageIndex);
        bool ChunkMinStageGreaterThan(Vector3Int id, int stageId);


    }
}