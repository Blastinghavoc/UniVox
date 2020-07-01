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

        IChunkProvider chunkProvider { get; }
        Func<Vector3Int, IChunkComponent> getChunkComponent { get; }
        bool StructureGen { get; }
        IChunkMesher chunkMesher { get; }

        bool NextStageFreeForChunk(Vector3Int chunkID, int currentStage);

        bool TargetStageGreaterThanCurrent(Vector3Int chunkID, int currentStage);
        bool TargetStageGreaterThanCurrent(int currentStage, ChunkStageData stageData);

        IPipelineStage NextStage(int currentStage);
        bool ChunkMinStageGreaterThan(Vector3Int id, int stageId);


    }
}