using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace UniVox.Framework.ChunkPipeline
{
    /// <summary>
    /// A stage that limits how many can go onto the next stage per update.
    /// Optionally also allows chunks to wait for a particular condition as well.
    /// </summary>
    public class RateLimitedStage : WaitingStage
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

        public override void Update(out List<Vector3Int> movingOn, out List<Vector3Int> terminating)
        {
            movingOn = new List<Vector3Int>();
            terminating = new List<Vector3Int>();

            int movedOn = 0;

            /* Go through all the chunk ids in the stage, ensuring that any that should terminate do so,
             * but limiting the number that can move on.
             */
            foreach (var item in chunkIdsInStage)
            {
                if (NextStageCondition(item, Order))
                {
                    //The chunk would ordinarily be able to move on, but we need to check the wait condition first
                    if (movedOn < MaxPerUpdate &&  WaitEndedCondition(item, Order))
                    {                        
                        movingOn.Add(item);
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
                    terminating.Add(item);
                }
            }


            //Remove the processed chunks

            foreach (var item in movingOn)
            {
                chunkIdsInStage.Remove(item);
            }

            foreach (var item in terminating)
            {
                chunkIdsInStage.Remove(item);
            }
        }
    }
}