using System;
using Unity.Jobs;
using UniVox.Framework.Jobified;

namespace UniVox.Framework.ChunkPipeline.VirtualJobs
{
    /// <summary>
    /// Wrapper around a Unity JobHandle
    /// </summary>
    public class PipelineUnityJob<T> : AbstractPipelineJob<T>
    {
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

        /// <summary>
        /// First parameter is a handle to a scheduled job, second is the 
        /// cleanup (e.g native disposal) function to execute when the job is
        /// complete.
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="cleanup"></param>
        public PipelineUnityJob(JobHandle handle, Func<T> cleanup)
        {
            this.handle = handle;
            this.cleanup = cleanup;
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