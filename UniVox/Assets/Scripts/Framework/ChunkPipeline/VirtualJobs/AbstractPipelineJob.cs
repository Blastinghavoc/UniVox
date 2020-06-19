using System;

namespace UniVox.Framework.ChunkPipeline.VirtualJobs
{
    public abstract class AbstractPipelineJob<T>:IDisposable
    {
        public virtual bool Done { get; protected set; } = false;

        public T Result { get; protected set; }

        //Start the job running
        public abstract void Start();

        public virtual void Dispose() 
        { 
        
        }
    }
}