using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace UniVox.Framework.ChunkPipeline
{
    public class PipelineStage
    {
        public string Name { get; private set; }
        public int Order { get; private set; }

        public int Count { get => chunkIdsInStage.Count; }

        /// <summary>
        /// Externally assignable function to determine whether this stage should accept a given chunk id
        /// </summary>
        public Func<Vector3Int, bool> FreeFor;

        #region Lists populated after each update that may be of interest to external code
        /// <summary>
        /// Note that these are only valid after Update has been called each frame.
        /// </summary>
        public List<Vector3Int> MovingOnThisUpdate { get; protected set; }
        public List<Vector3Int> TerminatingThisUpdate { get; protected set; }
        //Items who must return to the previous stage
        public List<Vector3Int> GoingBackwardsThisUpdate { get; protected set; }
        #endregion

        /// <summary>
        /// The condition to be evalutated to decide whether to move the chunk to the next stage,
        /// or terminate it here. true -> continue, false -> terminate
        /// </summary>
        public Func<Vector3Int, int, bool> NextStageCondition = (a, b) => true;

        protected HashSet<Vector3Int> chunkIdsInStage = new HashSet<Vector3Int>();

        public PipelineStage(string name, int order)
        {
            Name = name;
            Order = order;
            MovingOnThisUpdate = new List<Vector3Int>();
            TerminatingThisUpdate = new List<Vector3Int>();
            GoingBackwardsThisUpdate = new List<Vector3Int>();
            //By default, a stage is free for some id if it does not already contain it
            FreeFor = (id) => !Contains(id);
        }

        public PipelineStage(string name, int order, Func<Vector3Int, int, bool> nextStageCondition) : this(name, order)
        {
            NextStageCondition = nextStageCondition;
        }

        /// <summary>
        /// Clear the output lists in preparation for update
        /// </summary>
        private void ClearLists() 
        {
            MovingOnThisUpdate.Clear();
            TerminatingThisUpdate.Clear();
            GoingBackwardsThisUpdate.Clear();
        }

        public void Update()
        {
            ClearLists();

            SelfUpdate();
        }

        protected virtual void SelfUpdate() 
        {
            chunkIdsInStage.RemoveWhere((item) => {
                if (NextStageCondition(item, Order))
                {
                    MovingOnThisUpdate.Add(item);
                }
                else
                {
                    TerminatingThisUpdate.Add(item);
                }
                //Always remove
                return true;

            });

            //All chunks in the stage have either moved on or terminated here
            Assert.AreEqual(0, chunkIdsInStage.Count);
        }

        public bool Contains(Vector3Int chunkID)
        {
            return chunkIdsInStage.Contains(chunkID);
        }

        public virtual void AddAll(List<Vector3Int> incoming)
        {
            chunkIdsInStage.UnionWith(incoming);
        }

        public virtual void Add(Vector3Int incoming) 
        {
            chunkIdsInStage.Add(incoming);
        }
    }
}