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
    public class RateLimitedPipelineStage : WaitingPipelineStage
    {
        private int maxPerUpdate = 1;

        public RateLimitedPipelineStage(string name, int order,int maxPerUpdate) : base(name, order)
        {
            Assert.IsTrue(maxPerUpdate > 0);
            this.maxPerUpdate = maxPerUpdate;
        }

        public RateLimitedPipelineStage(string name, int order, int maxPerUpdate, Func<Vector3Int, int, bool> nextStageCondition, Func<Vector3Int, int, bool> waitEndedCondition) : base(name, order,nextStageCondition,waitEndedCondition)
        {
            Assert.IsTrue(maxPerUpdate > 0);
            this.maxPerUpdate = maxPerUpdate;
        }

        public override void Update(out List<Vector3Int> movingOn, out List<Vector3Int> terminating)
        {
            movingOn = new List<Vector3Int>();
            terminating = new List<Vector3Int>();

            int movedOn = 0;

            //Go through all the chunk ids in the stage until the maximum number have been moved on
            foreach (var item in chunkIdsInStage)
            {
                if (movedOn >= maxPerUpdate)
                {
                    //Stop when the max number of chunks have been moved on
                    break;
                }

                if (NextStageCondition(item, Order))
                {
                    //The chunk would ordinarily be able to move on, but we need to check the wait condition first
                    if (WaitEndedCondition(item, Order))
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