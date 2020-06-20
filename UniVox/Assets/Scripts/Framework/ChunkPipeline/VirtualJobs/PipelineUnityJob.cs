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

        private bool cleanedUp = false;
        public override bool Done { get {
                if (handle.IsCompleted)
                {
                    if (!cleanedUp)
                    {
                        DoCleanup();
                    }
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

        private void DoCleanup()
        {
            handle.Complete();
            Result = cleanup();
            cleanedUp = true;
        }

        /// <summary>
        /// To be called only if the job needs to be prematurely ended.
        /// Otherwise the cleanup will be done when the job completes.
        /// </summary>
        public override void Dispose()
        {
            if (!Done)
            {
                DoCleanup();
            }
        }
    }
}