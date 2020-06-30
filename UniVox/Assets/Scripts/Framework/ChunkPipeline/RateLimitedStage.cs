using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework.ChunkPipeline.WaitForNeighbours;

namespace UniVox.Framework.ChunkPipeline
{
    //NOTE: Unused, Prioritized stage is used instead.

    /// <summary>
    /// A stage that limits how many can go onto the next stage per update.
    /// Optionally also allows chunks to wait for a particular condition as well.
    /// </summary>
    public class RateLimitedStage : WaitForNeighboursStage
    {
        public int MaxPerUpdate { get; set; } = 1;

        public RateLimitedStage(string name, int order,int maxPerUpdate) : base(name, order)
        {
            Assert.IsTrue(maxPerUpdate > 0);
            this.MaxPerUpdate = maxPerUpdate;
        }

        public RateLimitedStage(string name, int order, int maxPerUpdate, Func<Vector3Int, int, bool> nextStageCondition, Func<Vector3Int, int, bool> waitEndedCondition) : base(name, order,nextStageCondition,waitEndedCondition)
        {
            Assert.IsTrue(maxPerUpdate > 0);
            this.MaxPerUpdate = maxPerUpdate;
        }

        protected override void SelfUpdate()
        {
            int movedOn = 0;

            /* Go through all the chunk ids in the stage, ensuring that any that should terminate do so,
             * but limiting the number that can move on.
             */
            foreach (var item in chunkIdsInStage)
            {
                if (NextStageCondition(item, StageID))
                {
                    //The chunk would ordinarily be able to move on, but we need to check the wait condition first
                    if (movedOn < MaxPerUpdate &&  WaitEndedCondition(item, StageID))
                    {                        
                        MovingOnThisUpdate.Add(item);
                        movedOn++;
                    }
                    else
                    {
                        //Otherwise, the chunk neither moves on nor terminates, it waits (so is not removed)
                        continue;
                    }

                }
                else
                {
                    TerminatingThisUpdate.Add(item);
                }
            }


            //Remove the processed chunks

            foreach (var item in MovingOnThisUpdate)
            {
                chunkIdsInStage.Remove(item);
            }

            foreach (var item in TerminatingThisUpdate)
            {
                chunkIdsInStage.Remove(item);
            }
        }
    }
}