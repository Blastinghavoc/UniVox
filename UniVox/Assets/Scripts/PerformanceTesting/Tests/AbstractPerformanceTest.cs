using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

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

        protected void Log(string s)
        {
            log.Add(s);
        }

        protected void ResetLog()
        {
            log = new List<string>();
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

        public void Clear() 
        {
            ResetPerFrameCounters();
            ResetLog();
        }

        protected virtual void UpdatePerFrameCounters()
        {
            foreach (var counter in perFrameCounters)
            {
                counter.Update();
            }
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