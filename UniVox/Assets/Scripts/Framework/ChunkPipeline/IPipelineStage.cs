﻿using System.Collections.Generic;
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

        List<Vector3Int> GoingBackwardsThisUpdate { get; }
        List<Vector3Int> MovingOnThisUpdate { get; }

        void Add(Vector3Int incoming);
        void AddAll(List<Vector3Int> incoming);

        bool Contains(Vector3Int chunkID);
        public bool FreeFor(Vector3Int chunkId);

        void Update();

        /// <summary>
        /// To be called by client code when the output lists have been read.
        /// This frees them for the next update.
        /// </summary>
        void ClearLists();

    }
}