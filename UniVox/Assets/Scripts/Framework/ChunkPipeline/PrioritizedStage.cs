using System;
using UnityEngine;
using Priority_Queue;
using System.Collections.Generic;
using UnityEngine.Profiling;
using UnityEngine.Assertions;

namespace UniVox.Framework.ChunkPipeline
{
    public class PrioritizedStage : RateLimitedStage
    {
        SimplePriorityQueue<Vector3Int> queue = new SimplePriorityQueue<Vector3Int>();

        protected Func<Vector3Int, float> getPriority = (_) => 0;

        public Func<int> UpdateMax = null;

        /// <summary>
        /// A condition that is assumed to hold for any item in this stage.
        /// If it is checked and found to be false, the item must go back to the previous stage.
        /// </summary>
        public Func<Vector3Int, bool> Precondition = (_) => true;

        public PrioritizedStage(string name, int order, int maxPerUpdate, 
            Func<Vector3Int, int, bool> nextStageCondition, 
            Func<Vector3Int, int, bool> waitEndedCondition,
            Func<Vector3Int, float> priorityFunc
            ) : base(name, order, maxPerUpdate, nextStageCondition, waitEndedCondition)
        {
            getPriority = priorityFunc;
        }

        protected override void SelfUpdate()
        {
            Profiler.BeginSample("PrioritizedStageUpdate");

            if (UpdateMax != null)
            {
                //Potentially update the maximum every update.
                MaxPerUpdate = UpdateMax();
            }

            int movedOn = 0;

            //List<Vector3Int> waiting = new List<Vector3Int>();

            //Iterate over queue in order, until at most MaxPerUpdate items have been moved on
            foreach (var item in queue)
            {
                if (movedOn >= MaxPerUpdate)
                {
                    break;
                }                

                if (NextStageCondition(item, Order))
                {
                    //The chunk would ordinarily be able to move on, but we need to check the wait condition first
                    if (WaitEndedCondition(item, Order))
                    {
                        //Just before the chunk would move on, re-check that the precondition still holds
                        if (Precondition(item))
                        {
                            MovingOnThisUpdate.Add(item);
                            chunkIdsInStage.Remove(item);
                            movedOn++;
                        }
                        else
                        {
                            GoingBackwardsThisUpdate.Add(item);
                            chunkIdsInStage.Remove(item);
                        }
                    }
                    else
                    {
                        //Otherwise, the chunk neither moves on nor terminates, it waits
                        continue;
                    }

                }
                else
                {
                    TerminatingThisUpdate.Add(item);
                    chunkIdsInStage.Remove(item);
                }
            }

            //Remove items from the queue when they move on
            foreach (var item in MovingOnThisUpdate)
            {
                queue.Remove(item);
            }

            //Remove items from the queue when they terminate
            foreach (var item in TerminatingThisUpdate)
            {
                queue.Remove(item);
            }

            //Remove items from the queue when the go backwards
            foreach (var item in GoingBackwardsThisUpdate)
            {
                queue.Remove(item);
            }

            //while (movedOn < MaxPerUpdate && queue.Count > 0)
            //{
            //    var item = queue.Dequeue();

            //    if (NextStageCondition(item, Order))
            //    {
            //        //The chunk would ordinarily be able to move on, but we need to check the wait condition first
            //        if (WaitEndedCondition(item, Order))
            //        {
            //            movingOn.Add(item);
            //            chunkIdsInStage.Remove(item);
            //            movedOn++;
            //        }
            //        else
            //        {
            //            //Otherwise, the chunk neither moves on nor terminates, it waits
            //            waiting.Add(item);
            //            continue;
            //        }

            //    }
            //    else
            //    {
            //        terminating.Add(item);
            //        chunkIdsInStage.Remove(item);
            //    }

            //}

            //Put all waiting items back in the queue (side effect of updating their priority)
            //foreach (var item in waiting)
            //{
            //    queue.Enqueue(item, getPriority(item));
            //}

            //Must still remove any chunks that should terminate
            //List<Vector3Int> tmpTerminating = new List<Vector3Int>();
            //chunkIdsInStage.RemoveWhere((id) => {
            //    if (!NextStageCondition(id, Order))
            //    {
            //        tmpTerminating.Add(id);
            //        return true;

            //    }
            //    return false;
            //});

            //terminating.AddRange(tmpTerminating);

            Profiler.EndSample();
        }

        public override void Add(Vector3Int incoming)
        {
            base.Add(incoming);
            Assert.IsTrue(!queue.Contains(incoming), $"Queue already contained {incoming} in stage {Name}");
            queue.Enqueue(incoming, getPriority(incoming));
        }

        public override void AddAll(List<Vector3Int> incoming)
        {
            base.AddAll(incoming);
            foreach (var item in incoming)
            {
                Assert.IsTrue(!queue.Contains(item), $"Queue already contained {item} in stage {Name}");
                queue.Enqueue(item, getPriority(item));
            }
        }
    }
}