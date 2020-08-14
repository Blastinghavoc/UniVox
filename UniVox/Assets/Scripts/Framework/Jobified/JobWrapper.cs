using Unity.Jobs;

namespace UniVox.Framework.Jobified
{
    /// <summary>
    /// Acts as a reference to a job, to prevent copying it
    /// </summary>
    /// <typeparam name="JobType"></typeparam>
    public class JobWrapper<JobType> where JobType : struct, IJob
    {
        public JobType job;

        public JobWrapper()
        {
        }

        public JobWrapper(JobType job)
        {
            this.job = job;
        }

        public void Run()
        {
            job.Run();
        }

        public JobHandle Schedule()
        {
            return job.Schedule();
        }

        public JobHandle Schedule(JobHandle dependsOn)
        {
            return job.Schedule(dependsOn);
        }
    }
}