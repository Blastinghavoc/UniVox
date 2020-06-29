using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework.ChunkPipeline
{
    public class WaitForJobStage<T> : WaitingStage, IDisposable
    {
        Dictionary<Vector3Int, AbstractPipelineJob<T>> jobs = new Dictionary<Vector3Int, AbstractPipelineJob<T>>();

        protected Func<Vector3Int, AbstractPipelineJob<T>> makeJob;
        protected Action<Vector3Int, T> onJobDone;

        public int MaxInStage { get; protected set; } = 4;

        //TODO remove DEBUG
        public Func<Vector3Int, bool> PreconditionCheck;

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

        protected override void SelfUpdate()
        {
            chunkIdsInStage.RemoveWhere((item) => {
                if (JobDone(item, Order))//Cannot terminate or move on unfinished jobs
                {
                    if (NextStageCondition(item,Order))
                    {
                        MovingOnThisUpdate.Add(item);
                        onJobDone(item, jobs[item].Result);
                        jobs.Remove(item);
                    }
                    else
                    {
                        TerminatingThisUpdate.Add(item);
                        //Do NOT execute OnJobDone, the result is discarded
                        jobs.Remove(item);
                    }
                    //Remove any finished jobs
                    return true;
                }
                //Must wait for jobs to be done before they can be moved
                return false;
            });

        }

        public override void AddAll(List<Vector3Int> incoming)
        {
            foreach (var item in incoming)
            {
                AddJob(item);
            }

            base.AddAll(incoming);
            
        }

        public override void Add(Vector3Int incoming)
        {
            base.Add(incoming);
            AddJob(incoming);
        }

        private void AddJob(Vector3Int item) 
        {
            //TODO remove DEBUG
            if (jobs.ContainsKey(item))
            {
                throw new ArgumentException($"Chunk {item} in incoming list already existed in stage {Name}" +
                    $". The existing job status was {jobs[item].Done}." +
                    $"Was it in the hashset : {this.Contains(item)}.");
            }

            if (PreconditionCheck != null)
            {
                if (!PreconditionCheck(item))
                {
                    throw new ArgumentException($"Preconditions failed for {item}");
                }
            }


            var job = makeJob(item);
            jobs.Add(item, job);


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
                jobs[id].Dispose();
                jobs.Remove(id);
                return true;
            });
        }
    }
}