using System;
using UnityEngine;
using Priority_Queue;
using System.Collections.Generic;
using UnityEngine.Profiling;
using UnityEngine.Assertions;
using UniVox.Framework.ChunkPipeline.WaitForNeighbours;

namespace UniVox.Framework.ChunkPipeline
{
    /// <summary>
    /// Stage that buffers items inside it until the next stage can accept them, dispatching them in
    /// a prioritised order.
    /// </summary>
    public class PrioritizedBufferStage : AbstractPipelineStage,IDisposable
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

        public PrioritizedBufferStage(string name, int order, 
            IChunkPipeline pipeline,
            Func<Vector3Int, float> priorityFunc
            ) : base(name, order, pipeline)
        {
            getPriority = priorityFunc;
            pipeline.OnChunkRemovedFromPipeline += WhenChunkRemovedFromPipeline;            
        }

        public override void Initialise()
        {
            ///Automatically detect and subscribe to preconditions
            if (StageID > 0)
            {
                var previousStage = pipeline.GetStage(StageID - 1);
                if (previousStage is WaitForNeighboursStage waitingStage)
                {
                    waitingStage.NotifyPreconditionFailure += OnPreconditionFailure;
                }
            }
        }

        /// <summary>
        /// Unbind events
        /// </summary>
        public void Dispose()
        {
            pipeline.OnChunkRemovedFromPipeline -= WhenChunkRemovedFromPipeline;
            if (StageID > 0)
            {
                var previousStage = pipeline.GetStage(StageID - 1);
                if (previousStage is WaitForNeighboursStage waitingStage)
                {
                    waitingStage.NotifyPreconditionFailure -= OnPreconditionFailure;
                }
            }
        }

        /// <summary>
        /// When the precondition for a chunk to be in this stage fails,
        /// if the chunk is in this stage, send it back.
        /// </summary>
        /// <param name="chunkId"></param>
        private void OnPreconditionFailure(Vector3Int chunkId) 
        {
            if (queue.TryRemove(chunkId))
            {
                GoingBackwardsThisUpdate.Add(chunkId);
            }
        }

        /// <summary>
        /// Try to remove the chunk id from this stage when a chunk is removed from the pipeline
        /// </summary>
        /// <param name="chunkId"></param>
        private void WhenChunkRemovedFromPipeline(Vector3Int chunkId)
        {
            queue.TryRemove(chunkId);
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
            return true;
        }

        public override void Update()
        {
            base.Update();

            //Get next stage's entry limit
            int maxToMoveOn = pipeline.GetStage(StageID+1).EntryLimit;

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

            ///Items in the going backwards list have already been removed from the queue,
            ///but we need to make sure they should really be going backwards, not just terminating
            GoingBackwardsThisUpdate.RemoveWhere((id) => TerminateHereCondition(id));

            //Clear the terminating helper list
            terminatingThisUpdateHelper.Clear();
        }

        public override void Add(Vector3Int incoming,ChunkStageData stageData)
        {
            if (!TerminateHereCondition(stageData))
            {
                if (queue.Contains(incoming))//Update priority if item existed
                {
                    queue.UpdatePriority(incoming, getPriority(incoming));
                }
                else
                {//Add to queue otherwise
                    queue.Enqueue(incoming, getPriority(incoming));
                }

            }
            //incoming terminates here and so does not need to be added to the queue
        }

        public override bool Contains(Vector3Int chunkID)
        {
            return queue.Contains(chunkID);
        }        
    }
}