using System;

namespace UniVox.Framework.ChunkPipeline.VirtualJobs
{
    /// <summary>
    /// Used as a wrapper so that serial functions and actual parallel jobs
    /// can both be used interchangeably in the pipeline.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class AbstractPipelineJob<T>:IDisposable
    {
        public virtual bool Done { get; protected set; } = false;

        public T Result { get; protected set; }

        public virtual void Dispose() 
        { 
        
        }
    }
}