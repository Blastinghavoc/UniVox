using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace UniVox.Framework.ChunkPipeline
{
    /// <summary>
    /// A stage that limits how many can go onto the next stage per update
    /// </summary>
    public class RateLimitedPipelineStage : PipelineStage
    {
        private int maxPerUpdate = 1;

        public RateLimitedPipelineStage(string name, int order,int maxPerUpdate) : base(name, order)
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
                    break;
                }

                if (NextStageCondition(item, Order))
                {
                    movingOn.Add(item);
                    movedOn++;
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