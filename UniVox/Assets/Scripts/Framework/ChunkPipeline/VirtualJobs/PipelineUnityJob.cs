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
                    Finish();
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

        private void Finish()
        {
            handle.Complete();
            Result = cleanup();
        }

        /// <summary>
        /// To be called if the job need to be prematurely ended.
        /// Otherwise disposal will be handled by whatever receives the result.
        /// </summary>
        public override void Dispose()
        {
            Finish();
            if (Result is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}