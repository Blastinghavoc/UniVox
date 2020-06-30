using System;
using UnityEngine;

namespace UniVox.Framework.ChunkPipeline
{
    public delegate void ChunkRemovedHandler(Vector3Int chunkId);
    public delegate void ChunkAddedHandler(Vector3Int chunkId,int addedAtStage);
    public delegate void ChunkMinStageDecreasedHandler(Vector3Int chunkId,int newMinStage);
    public interface IChunkPipeline 
    {
        event ChunkRemovedHandler OnChunkRemovedFromPipeline;
        event ChunkAddedHandler OnChunkAddedToPipeline;
        event ChunkMinStageDecreasedHandler OnChunkMinStageDecreased;

        bool NextStageFreeForChunk(Vector3Int chunkID, int currentStage);

        bool TargetStageGreaterThanCurrent(Vector3Int chunkID, int currentStage);

        IPipelineStage NextStage(int currentStage);
        bool ChunkPassedStage(Vector3Int id, int stageId);
    }
}