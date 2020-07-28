using System.Collections.Generic;
using UnityEngine;

namespace UniVox.Framework.ChunkPipeline
{
    public interface IPipelineStage
    {
        string Name { get; }
        int StageID { get; }
        int Count { get; }

        /// <summary>
        /// Number of items permitted to enter this stage. Can change over time.
        /// </summary>
        int EntryLimit { get; }

        HashSet<Vector3Int> GoingBackwardsThisUpdate { get; }
        HashSet<Vector3Int> MovingOnThisUpdate { get; }

        void Add(Vector3Int incoming, ChunkStageData stageData);

        bool Contains(Vector3Int chunkID);

        /// <summary>
        /// Returns a value indicating whether the current stage can accept the given chunk id,
        /// based on what's currently in the stage, and the set of ids that are pending
        /// entry to the stage.
        /// </summary>
        /// <param name="chunkId"></param>
        /// <param name="pendingEntry"></param>
        /// <returns></returns>
        bool FreeFor(Vector3Int chunkId,HashSet<Vector3Int> pendingEntry);

        void Update();

        /// <summary>
        /// To be called once the whole pipeline is constructed so that stages can intialise any
        /// properties that depend on other stages.
        /// </summary>
        void Initialise();

        /// <summary>
        /// To be called by client code when the output lists have been read.
        /// This frees them for the next update.
        /// </summary>
        void ClearLists();

    }
}