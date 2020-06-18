using UnityEngine;
using System.Collections;
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
    }
}