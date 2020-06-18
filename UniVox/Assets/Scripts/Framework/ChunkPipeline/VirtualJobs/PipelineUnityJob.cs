using System;
using Unity.Jobs;
using UniVox.Framework.Jobified;

namespace UniVox.Framework.ChunkPipeline.VirtualJobs
{
    /// <summary>
    /// Wrapper around a Unity IJob
    /// </summary>
    public class PipelineUnityJob<T, JobType> : AbstractPipelineJob<T>
        where JobType: struct, IJob
    {
        private JobWrapper<JobType> jobWrapper;
        private JobHandle handle;
        private Func<T> cleanup ;

        public override bool Done { get {
                if (handle.IsCompleted)
                {
                    Terminate();
                    return true;
                }
                return false; 
            } 
        }

        public PipelineUnityJob(JobWrapper<JobType> jobWrapper, Func<T> cleanup)
        {
            this.jobWrapper = jobWrapper;
            this.cleanup = cleanup;
        }

        public override void Start()
        {
            handle = jobWrapper.job.Schedule();
        }

        public override void Terminate()
        {
            handle.Complete();
            Result = cleanup();
        }
    }
}