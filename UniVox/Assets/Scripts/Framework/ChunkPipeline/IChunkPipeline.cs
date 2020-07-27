using System;
using UnityEngine;

namespace UniVox.Framework.ChunkPipeline
{
    public interface IChunkPipeline 
    {
        /// <summary>
        /// Event for when a chunk finishes generation
        /// </summary>
        event Action<Vector3Int> OnChunkFinishedGenerating;
        void FireChunkFinishedGeneratingEvent(Vector3Int chunkId);

        event Action<Vector3Int> OnChunkRemovedFromPipeline;
        //args: id, added at stage
        event Action<Vector3Int,int> OnChunkAddedToPipeline;
        //args: id, new min stage
        event Action<Vector3Int, int> OnChunkMinStageDecreased;
        //args: id, new target stage
        event Action<Vector3Int, int> OnChunkTargetStageDecreased;

        IChunkProvider chunkProvider { get; }
        IChunkMesher chunkMesher { get; }
        Func<Vector3Int, IChunkComponent> getChunkComponent { get; }
        bool GenerateStructures { get; }

        int TerrainDataStage { get; }
        int OwnStructuresStage { get; }
        int PreLightGenStage { get; }
        int FullyGeneratedStage { get; }
        int RenderedStage { get; }
        int CompleteStage { get; }

        bool NextStageFreeForChunk(Vector3Int chunkID, int currentStage);

        bool TargetStageGreaterThanCurrent(Vector3Int chunkID, int currentStage);
        bool TargetStageGreaterThanCurrent(int currentStage, ChunkStageData stageData);

        IPipelineStage GetStage(int stageIndex);
        bool ChunkMinStageGreaterThan(Vector3Int id, int stageId);
        void SetTarget(Vector3Int chunkId, int targetStage, TargetUpdateMode mode = TargetUpdateMode.any);
    }
}