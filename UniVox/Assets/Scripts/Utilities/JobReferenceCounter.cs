using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine.Assertions;

namespace Utils
{
    public class JobReferenceCounter<TypeOfKey, TypeOfResult> where TypeOfResult : struct
    {
        public struct JobResultPair
        {
            public JobHandle handle;
            public TypeOfResult result;
        }

        Dictionary<TypeOfKey, JobResultPair> pendingJobs = new Dictionary<TypeOfKey, JobResultPair>();
        Dictionary<TypeOfKey, int> counts = new Dictionary<TypeOfKey, int>();
        Func<TypeOfKey, JobResultPair> makeJob;

        public JobReferenceCounter(Func<TypeOfKey, JobResultPair> makeJob)
        {
            this.makeJob = makeJob;
        }

        public void Add(TypeOfKey key, out JobHandle handle, out TypeOfResult result, out bool IsNewJob)
        {
            if (pendingJobs.TryGetValue(key, out var pair))
            {
                counts[key] += 1;
                IsNewJob = false;
            }
            else
            {
                pair = makeJob(key);
                pendingJobs.Add(key, pair);
                Assert.IsFalse(counts.ContainsKey(key));
                counts[key] = 1;

                IsNewJob = true;
            }

            handle = pair.handle;
            result = pair.result;
        }

        /// <summary>
        /// Returns true if the count for the key has reached 0.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Done(TypeOfKey key)
        {
            if (counts.TryGetValue(key, out var count))
            {
                --count;
                if (count <= 0)
                {
                    counts.Remove(key);
                    pendingJobs.Remove(key);
                    return true;
                }
                else
                {
                    counts[key] = count;
                }
            }
            else
            {
                //Key was not present in reference counter, so by definition its count is 0
                return true;
            }

            return false;
        }
    }
}