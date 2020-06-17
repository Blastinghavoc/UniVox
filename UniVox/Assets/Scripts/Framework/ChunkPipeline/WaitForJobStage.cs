using System;
using System.Collections.Generic;
using UnityEngine;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework.ChunkPipeline
{
    public class WaitForJobStage<T> : WaitingStage, IDisposable
    {
        Dictionary<Vector3Int, AbstractPipelineJob<T>> jobs = new Dictionary<Vector3Int, AbstractPipelineJob<T>>();

        protected Func<Vector3Int, AbstractPipelineJob<T>> makeJob;
        protected Action<Vector3Int, T> onJobDone;

        public int MaxInStage { get; protected set; } = 4;

        /// <summary>
        /// Returns the number of items that may be added to this stage,
        /// given the current number of items already in this stage.
        /// </summary>
        /// <returns></returns>
        public int MaxToEnter() 
        {
            return MaxInStage - Count;
        }

        public WaitForJobStage(string name, int order, Func<Vector3Int, int, bool> nextStageCondition, 
            Func<Vector3Int, AbstractPipelineJob<T>> makeJob, 
            Action<Vector3Int, T> onJobDone,
            int maxInStage) : base(name, order)
        {
            this.makeJob = makeJob;
            this.onJobDone = onJobDone;
            NextStageCondition = nextStageCondition;
            WaitEndedCondition = JobDone;
            MaxInStage = maxInStage;
        }

        private bool JobDone(Vector3Int chunkID,int _) 
        {
            if (jobs.TryGetValue(chunkID,out var job))
            {
                return job.Done;
            }
            throw new Exception("Tried to check if a nonexistent job was done");
        }

        public override void Update(out List<Vector3Int> movingOn, out List<Vector3Int> terminating)
        {
            var movingOnTmp = new List<Vector3Int>();
            var terminatingTmp = new List<Vector3Int>();

            chunkIdsInStage.RemoveWhere((item) => {
                if (JobDone(item, Order))//Cannot terminate or move on unfinished jobs
                {
                    if (NextStageCondition(item,Order))
                    {
                        movingOnTmp.Add(item);
                        onJobDone(item, jobs[item].Result);
                        jobs.Remove(item);
                    }
                    else
                    {
                        terminatingTmp.Add(item);
                        jobs.Remove(item);
                        //Do NOT execute OnJobDone, the result is discarded
                    }
                    //Remove any finished jobs
                    return true;
                }
                //Must wait for jobs to be done before they can be moved
                return false;
            });

            movingOn = movingOnTmp;
            terminating = terminatingTmp;
        }

        public override void AddAll(List<Vector3Int> incoming)
        {
            base.AddAll(incoming);
            foreach (var item in incoming)
            {
                var job = makeJob(item);
                //DEBUG
                try
                {
                    jobs.Add(item, job);
                    job.Start();
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException($"Chunk {item} in incoming list already existed in stage {Name}" +
                        $". The existing job status was {jobs[item].Done}",e);
                }
            }
        }

        public override void Add(Vector3Int incoming)
        {
            base.Add(incoming);
            var job = makeJob(incoming);
            jobs.Add(incoming, job);
            job.Start();
        }

        /// <summary>
        /// Forcibly terminate all in-progress jobs
        /// This means waiting for any UnityJobs (IJobs)
        /// to finish
        /// </summary>
        public void Dispose() 
        {
            Debug.Log($"Disposing of {chunkIdsInStage.Count} unfinished jobs in stage {Name}");
            chunkIdsInStage.RemoveWhere((id)=> 
            {
                jobs[id].Terminate();
                jobs.Remove(id);
                return true;
            });
        }
    }
}