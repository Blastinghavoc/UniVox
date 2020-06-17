using System;
using UnityEngine;
using Priority_Queue;
using System.Collections.Generic;

namespace UniVox.Framework.ChunkPipeline
{
    public class PrioritizedStage : RateLimitedStage
    {
        SimplePriorityQueue<Vector3Int> queue = new SimplePriorityQueue<Vector3Int>();

        protected Func<Vector3Int, float> getPriority = (_) => 0;

        public Func<int> UpdateMax = null;

        public PrioritizedStage(string name, int order, int maxPerUpdate, 
            Func<Vector3Int, int, bool> nextStageCondition, 
            Func<Vector3Int, int, bool> waitEndedCondition,
            Func<Vector3Int, float> priorityFunc
            ) : base(name, order, maxPerUpdate, nextStageCondition, waitEndedCondition)
        {
            getPriority = priorityFunc;
        }

        public override void Update(out List<Vector3Int> movingOn, out List<Vector3Int> terminating)
        {
            if (UpdateMax != null)
            {
                //Potentially update the maximum every update.
                MaxPerUpdate = UpdateMax();
            }

            movingOn = new List<Vector3Int>();
            terminating = new List<Vector3Int>();

            int movedOn = 0;

            List<Vector3Int> waiting = new List<Vector3Int>();
            while (movedOn < MaxPerUpdate && queue.Count > 0)
            {
                var item = queue.Dequeue();

                if (NextStageCondition(item, Order))
                {
                    //The chunk would ordinarily be able to move on, but we need to check the wait condition first
                    if (WaitEndedCondition(item, Order))
                    {
                        movingOn.Add(item);
                        chunkIdsInStage.Remove(item);
                        movedOn++;
                    }
                    else
                    {
                        //Otherwise, the chunk neither moves on nor terminates, it waits
                        waiting.Add(item);
                        continue;
                    }

                }
                else
                {
                    terminating.Add(item);
                    chunkIdsInStage.Remove(item);
                }

            }

            //Put all waiting items back in the queue (side effect of updating their priority)
            foreach (var item in waiting)
            {
                queue.Enqueue(item, getPriority(item));
            }

            //Must still remove any chunks that should terminate
            List<Vector3Int> tmpTerminating = new List<Vector3Int>();
            chunkIdsInStage.RemoveWhere((id) => {
                if (!NextStageCondition(id, Order))
                {
                    tmpTerminating.Add(id);
                    return true;

                }
                return false;
            });

            terminating.AddRange(tmpTerminating);
        }

        public override void Add(Vector3Int incoming)
        {
            base.Add(incoming);
            queue.Enqueue(incoming, getPriority(incoming));
        }

        public override void AddAll(List<Vector3Int> incoming)
        {
            base.AddAll(incoming);
            foreach (var item in incoming)
            {
                queue.Enqueue(item, getPriority(item));
            }
        }
    }
}