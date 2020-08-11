using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Framework.ChunkPipeline.WaitForNeighbours;

namespace UniVox.Framework.ChunkPipeline
{
    public abstract class WaitForJobStage<T> : AbstractPipelineStage, IDisposable,IWaitForJobStage
    {
        Dictionary<Vector3Int, AbstractPipelineJob<T>> jobs = new Dictionary<Vector3Int, AbstractPipelineJob<T>>();

        private List<Vector3Int> removalHelper = new List<Vector3Int>();

        public int MaxInStage { get; protected set; } = 1;

        public override int Count => jobs.Count;

        public override int EntryLimit => MaxInStage - Count;

        public WaitForJobStage(string name, int order, IChunkPipeline pipeline, 
            int maxInStage) : base(name, order,pipeline)
        {
            MaxInStage = maxInStage;
        }

        protected abstract AbstractPipelineJob<T> MakeJob(Vector3Int chunkId);
        protected abstract void OnJobDone(Vector3Int chunkId,T result);

        public override void Update()
        {            
            base.Update();

            foreach (var pair in jobs)
            {
                var chunkId = pair.Key;
                var job = pair.Value;                
                if (job.Done)
                {
                    if (CheckAndResolvePreconditionsBeforeExit(chunkId))
                    {
                        MovingOnThisUpdate.Add(chunkId);
                        Profiler.BeginSample("OnJobDone");
                        OnJobDone(chunkId, job.Result);
                        Profiler.EndSample();
                        //Remove this id from the stage, as it's moving on
                        removalHelper.Add(chunkId);
                    }
                }                
            }

            foreach (var item in removalHelper)
            {
                jobs.Remove(item);
            }

            removalHelper.Clear();
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
                removalHelper.Add(chunkId);//Discard the result of a job
                return false;
            }
            return true;
        }

        public override void Add(Vector3Int incoming,ChunkStageData stageData)
        {
            if (!TerminateHereCondition(stageData))
            {
                Assert.IsFalse(jobs.ContainsKey(incoming),$"Job stages do not support duplicates," +
                    $" tried to add {incoming} when it already existed in stage {Name}");

                AddJob(incoming);                
            }
        }

        private void AddJob(Vector3Int item) 
        {
            var job = MakeJob(item);
            jobs.Add(item, job);
        }

        /// <summary>
        /// Forcibly terminate all in-progress jobs
        /// This means waiting for any UnityJobs (IJobs)
        /// to finish
        /// </summary>
        public void Dispose() 
        {
            //Debug.Log($"Disposing of {Count} unfinished jobs in stage {Name}");
            foreach (var job in jobs.Values)
            {
                job.Dispose();
            }
            jobs.Clear();            
        }

        public override bool Contains(Vector3Int chunkID)
        {
            return jobs.ContainsKey(chunkID);
        }
    }

    //Symbolic interface
    public interface IWaitForJobStage 
    { 
    
    }
}