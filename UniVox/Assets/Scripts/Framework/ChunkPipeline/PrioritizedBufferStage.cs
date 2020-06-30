using System;
using UnityEngine;
using Priority_Queue;
using System.Collections.Generic;
using UnityEngine.Profiling;
using UnityEngine.Assertions;

namespace UniVox.Framework.ChunkPipeline
{
    /// <summary>
    /// Stage that buffers items inside it until the next stage can accept them, dispatching them in
    /// a prioritised order.
    /// </summary>
    public class PrioritizedBufferStage : AbstractPipelineStage
    {
        SimplePriorityQueue<Vector3Int> queue = new SimplePriorityQueue<Vector3Int>();

        public override int Count => queue.Count;

        //No limit
        public override int EntryLimit => int.MaxValue;

        protected Func<Vector3Int, float> getPriority = (_) => 0;

        /// <summary>
        /// Local list used to assist updating.
        /// </summary>
        private List<Vector3Int> terminatingThisUpdateHelper = new List<Vector3Int>();

        /// <summary>
        /// A condition that is assumed to hold for any item in this stage.
        /// If it is checked and found to be false, the item must go back to the previous stage.
        /// </summary>
        public Func<Vector3Int, bool> ExternalPrecondition = (_) => true;        

        public PrioritizedBufferStage(string name, int order, 
            IChunkPipeline pipeline,
            Func<Vector3Int, float> priorityFunc
            ) : base(name, order, pipeline)
        {
            getPriority = priorityFunc;
        }

        /// <summary>
        /// As this stage supports waiting, the termination condition is just to do with the
        /// target stage of the chunkId.
        /// </summary>
        /// <param name="chunkId"></param>
        /// <param name="currentStageId"></param>
        /// <returns></returns>
        protected override bool TerminateHereCondition(Vector3Int chunkId)
        {
            return !pipeline.TargetStageGreaterThanCurrent(chunkId, StageID);
        }

        /// <summary>
        /// Checks the preconditions assumed for the chunkId before it can exit the stage.
        /// Also handles resolution of these precondtions.
        /// </summary>
        /// <param name="chunkId"></param>
        /// <returns></returns>
        private bool CheckAndResolvePreconditionsBeforeExit(Vector3Int chunkId)
        {
            if (TerminateHereCondition(chunkId))
            {
                ///This must be false on entry, and therefore should still be false on exit.
                ///This can be violated if the target stage of a chunkId changes, in which case the
                ///id should terminate here.
                terminatingThisUpdateHelper.Add(chunkId);
                return false;
            }
            else if (!ExternalPrecondition(chunkId))
            {
                ///The external precondion is assumed to be true on entry, if found to be false the 
                ///chunk must go backwards in the pipline.
                GoingBackwardsThisUpdate.Add(chunkId);
                return false;
            }
            return true;
        }

        public override void Update()
        {
            base.Update();

            int maxToMoveOn = pipeline.NextStage(StageID).EntryLimit;

            int movedOn = 0;

            //Iterate over queue in order, until at most maxToMoveOn items have been moved on
            foreach (var item in queue)
            {
                if (movedOn >= maxToMoveOn)
                {
                    break;
                }                
                
                if (pipeline.NextStageFreeForChunk(item,StageID))
                {
                    //Just before the chunk would move on, re-check that the preconditions still hold
                    if (CheckAndResolvePreconditionsBeforeExit(item))
                    {
                        MovingOnThisUpdate.Add(item);
                        movedOn++;
                    }
                }
                else
                {
                    //Otherwise, the chunk neither moves on nor terminates, it waits
                    continue;
                }               
            }

            //Remove items from the queue when they move on
            foreach (var item in MovingOnThisUpdate)
            {
                queue.Remove(item);
            }

            //Remove items from the queue when they terminate
            foreach (var item in terminatingThisUpdateHelper)
            {
                queue.Remove(item);
            }

            //Remove items from the queue when the go backwards
            foreach (var item in GoingBackwardsThisUpdate)
            {
                queue.Remove(item);
            }


            //Clear the terminating helper list
            terminatingThisUpdateHelper.Clear();
        }

        public override void Add(Vector3Int incoming)
        {
            if (!TerminateHereCondition(incoming))
            {
                Assert.IsTrue(!queue.Contains(incoming), $"Queue already contained {incoming} in stage {Name}");
                queue.Enqueue(incoming, getPriority(incoming));
            }
            //incoming terminates here and so does not need to be added to the queue
        }

        public override bool Contains(Vector3Int chunkID)
        {
            return queue.Contains(chunkID);
        }
    }
}