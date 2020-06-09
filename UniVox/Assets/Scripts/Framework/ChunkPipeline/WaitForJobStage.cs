﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework.ChunkPipeline
{
    public class WaitForJobStage<T> : WaitingPipelineStage
    {
        Dictionary<Vector3Int, AbstractPipelineJob<T>> jobs = new Dictionary<Vector3Int, AbstractPipelineJob<T>>();

        protected Func<Vector3Int, AbstractPipelineJob<T>> makeJob;
        protected Action<Vector3Int, T> onJobDone;

        public WaitForJobStage(string name, int order, Func<Vector3Int, int, bool> nextStageCondition, Func<Vector3Int, AbstractPipelineJob<T>> makeJob, Action<Vector3Int, T> onJobDone) : base(name, order)
        {
            this.makeJob = makeJob;
            this.onJobDone = onJobDone;
            NextStageCondition = nextStageCondition;
            WaitEndedCondition = JobDone;
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
            base.Update(out movingOn, out terminating);
            foreach (var item in movingOn)
            {
                onJobDone(item,jobs[item].Result);
                jobs.Remove(item);
            }
            foreach (var item in terminating)
            {
                //Do NOT execute onJobDone, the result is discarded
                jobs.Remove(item);
            }
        }

        public override void AddAll(List<Vector3Int> incoming)
        {
            base.AddAll(incoming);
            foreach (var item in incoming)
            {
                var job = makeJob(item);
                jobs.Add(item, job);
                job.Start();
            }
        }

        public override void Add(Vector3Int incoming)
        {
            base.Add(incoming);
            var job = makeJob(incoming);
            jobs.Add(incoming, job);
            job.Start();
        }
    }
}