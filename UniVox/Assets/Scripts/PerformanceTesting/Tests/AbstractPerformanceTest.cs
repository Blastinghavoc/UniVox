using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.Profiling;

namespace PerformanceTesting
{

    public abstract class AbstractPerformanceTest : MonoBehaviour, IPerformanceTest
    {
        [SerializeField] private string testName = "";
        public string TestName { get =>testName; }

        protected List<string> log;

        protected FrameCounter frameCounter;
        protected MemoryCounter memoryCounter;

        protected List<IStatsCollector> perFrameCounters;

        private long logSize = 0;

        protected void Log(string s)
        {
            log.Add(s);
            logSize += s.Length * sizeof(char);
        }

        protected void ResetLog()
        {
            log = new List<string>();
            logSize = 0;
        }

        protected virtual void ResetPerFrameCounters()
        {
            perFrameCounters = new List<IStatsCollector>();
            frameCounter = new FrameCounter();
            perFrameCounters.Add(frameCounter);
            memoryCounter = new MemoryCounter();
            perFrameCounters.Add(memoryCounter);

            //make sure to log the memory usage baseline
            log.Add($"Memory usage baseline: {memoryCounter.BaseLine}");
        }

        /// <summary>
        /// Clear memory used by the test
        /// </summary>
        public void Clear() 
        {
            ResetPerFrameCounters();
            ResetLog();
        }

        protected virtual void UpdatePerFrameCounters()
        {
            long estimatedMemoryUsage = logSize;
            foreach (var counter in perFrameCounters)
            {
                estimatedMemoryUsage += counter.EstimateMemoryUsageBytes();
                if (counter == memoryCounter)
                {
                    continue;//Skip update for now
                }
                counter.Update();
            }
            ///Update the memory counter last, with the estimated memory usage of the other counters
            ///and the current log size
            memoryCounter.Update(estimatedMemoryUsage);
        }

        public List<string> GetLogLines()
        {
            return log;
        }

        public Dictionary<string, List<string>> GetPerFrameData()
        {
            Dictionary<string, List<string>> data = new Dictionary<string, List<string>>();

            foreach (var counter in perFrameCounters)
            {
                data.Add(counter.VariableName, counter.Data);
            }
            return data;

        }

        public abstract IEnumerator Run(ITestableChunkManager chunkManager);
    }
}