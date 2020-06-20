using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniVox.Framework.ChunkPipeline
{
    /// <summary>
    /// Supports a third option for chunks in the stage (other than continue/terminate),
    /// the waiting option allows the chunk to remain held by the stage until some
    /// condition is met.
    /// </summary>
    public class WaitingStage : PipelineStage
    {
        /// <summary>
        /// If the condition is met, the wait has ended. Otherwise the chunk neither continues nor terminates at this stage
        /// </summary>
        protected Func<Vector3Int, int, bool> WaitEndedCondition = (a, b) => true;

        protected override void SelfUpdate()
        {
            chunkIdsInStage.RemoveWhere((item) => {
                if (NextStageCondition(item, Order))
                {
                    //The chunk would ordinarily be able to move on, but we need to check the wait condition first
                    if (WaitEndedCondition(item, Order))
                    {
                        MovingOnThisUpdate.Add(item);
                    }
                    else
                    {
                        //Otherwise, the chunk neither moves on nor terminates, it waits (so is not removed)
                        return false;
                    }
                }
                else
                {
                    TerminatingThisUpdate.Add(item);
                }
                //Always remove if moving on or terminating
                return true;

            });
        }

        public WaitingStage(string name, int order) : base(name, order)
        {
        }

        public WaitingStage(string name, int order, Func<Vector3Int, int, bool> nextStageCondition, Func<Vector3Int, int, bool> waitEndedCondition) : base(name, order, nextStageCondition)
        {
            WaitEndedCondition = waitEndedCondition;
        }

    }
}