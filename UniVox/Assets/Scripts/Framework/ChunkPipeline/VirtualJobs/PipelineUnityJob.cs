using System;
using Unity.Jobs;

namespace UniVox.Framework.ChunkPipeline.VirtualJobs
{
    /// <summary>
    /// Wrapper around a Unity IJob
    /// </summary>
    public class PipelineUnityJob<T,jobType> : AbstractPipelineJob<T>
        where jobType: struct,IJob
    {
        private jobType job;
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

        public PipelineUnityJob(jobType job, Func<T> cleanup)
        {
            this.job = job;
            this.cleanup = cleanup;
        }

        public override void Start()
        {
            handle = job.Schedule();
        }

        public override void Terminate()
        {
            handle.Complete();
            Result = cleanup();
        }
    }
}