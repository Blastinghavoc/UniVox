using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using static UniVox.Framework.ChunkPipeline.IPipelineStage;

namespace UniVox.Framework.ChunkPipeline
{
    /// <summary>
    /// Abstract implementation of interface, with some supporting methods and
    /// datastructures.
    /// </summary>
    public abstract class AbstractPipelineStage :IPipelineStage
    {
        protected IChunkPipeline pipeline;

        public string Name { get; private set; }
        public int StageID { get; private set; }
        public abstract int Count { get; }
        public abstract int EntryLimit { get; }

        #region Lists populated after each update that may be of interest to external code
        /// <summary>
        /// Note that these are only valid after Update has been called each frame.
        /// </summary>
        public List<Vector3Int> MovingOnThisUpdate { get; protected set; }
        //Items who must return to the previous stage
        public List<Vector3Int> GoingBackwardsThisUpdate { get; protected set; }

        #endregion

        private bool listsCleared = true;

        public AbstractPipelineStage(string name, int stageId, IChunkPipeline pipeline)
        {
            Name = name;
            StageID = stageId;
            MovingOnThisUpdate = new List<Vector3Int>();
            GoingBackwardsThisUpdate = new List<Vector3Int>();
            this.pipeline = pipeline;
        }

        /// <summary>
        /// Condition under which a chunkId should terminate at this stage
        /// rather than passing through.
        /// </summary>
        /// <param name="chunkId"></param>
        /// <param name="currentStageId"></param>
        /// <returns></returns>
        protected abstract bool TerminateHereCondition(Vector3Int chunkId);

        /// <summary>
        /// Clear the output lists in preparation for next update
        /// </summary>
        public void ClearLists()
        {
            MovingOnThisUpdate.Clear();
            //TerminatingThisUpdate.Clear();
            GoingBackwardsThisUpdate.Clear();
            listsCleared = true;
        }

        public virtual void Update()
        {
            Assert.IsTrue(listsCleared, "Lists were not correctly cleared before the update");
            listsCleared = false;
        }

        /// <summary>
        /// Default FreeFor implementation
        /// </summary>
        /// <param name="chunkId"></param>
        /// <returns></returns>
        public virtual bool FreeFor(Vector3Int chunkId)
        {
            return !Contains(chunkId);
        }

        /// <summary>
        /// Utility to add multiple elements
        /// </summary>
        /// <param name="incoming"></param>
        public void AddAll(List<Vector3Int> incoming)
        {
            foreach (var item in incoming)
            {
                Add(item);
            }
        }

        public abstract void Add(Vector3Int incoming);

        public abstract bool Contains(Vector3Int chunkID);

    }
}