using System;

namespace UniVox.Framework.ChunkPipeline.VirtualJobs
{
    public class BasicFunctionJob<T> : AbstractPipelineJob<T>
    {
        Func<T> func;

        public BasicFunctionJob(Func<T> func)
        {
            this.func = func;
        }

        public override void Start()
        {
            Result = func();
            Done = true;
        }
    }
}