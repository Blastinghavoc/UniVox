using System;

namespace UniVox.Framework.ChunkPipeline.VirtualJobs
{
    //Simply executes the function and store the result
    public class BasicFunctionJob<T> : AbstractPipelineJob<T>
    {
        public BasicFunctionJob(Func<T> func)
        {
            Result = func();
            Done = true;
        }
    }
}