using System;
using Unity.Jobs;

namespace UniVox.Framework.Jobified
{
    public interface IMeshingJob : IJob, IDisposable
    {
        //Run the job immediately on the main thread
        void Run();
        //Schedule the job for later execution
        JobHandle Schedule(JobHandle dependsOn = default);

        MeshJobData data { get; set; }
    }
}