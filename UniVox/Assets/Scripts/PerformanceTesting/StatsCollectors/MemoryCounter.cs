using System.Collections.Generic;
using UnityEngine.Profiling;

namespace PerformanceTesting
{
    public class MemoryCounter : IStatsCollector
    {
        public List<long> memoryPerFrame = new List<long>();

        public string VariableName { get; } = "MemoryUsageBytes";

        public List<string> Data { get => CSVUtils.ListToStringList(memoryPerFrame); }

        public MemoryCounter()
        {
        }

        public void Update()
        {
            memoryPerFrame.Add(Profiler.GetTotalAllocatedMemoryLong());
        }

        public void Update(long memoryUsedByStatsCollecting)
        {
            memoryPerFrame.Add((Profiler.GetTotalAllocatedMemoryLong()) - memoryUsedByStatsCollecting);
        }

        public long EstimateMemoryUsageBytes()
        {
            return memoryPerFrame.Count * sizeof(long);
        }
    }

    public class AdjustedMemoryCounter : IStatsCollector
    {
        public MemoryCounter rawCounter;

        public AdjustedMemoryCounter(MemoryCounter rawCounter)
        {
            this.rawCounter = rawCounter;
        }

        public string VariableName => "MemoryDifferenceBytes";

        public List<string> Data {
            get 
            {
                if (rawCounter.memoryPerFrame == null || rawCounter.memoryPerFrame.Count == 0)
                {
                    return new List<string>();
                }
                var baseline = rawCounter.memoryPerFrame[0];
                var adjustedList = new List<long>(rawCounter.memoryPerFrame);
                for (int i = 0; i < adjustedList.Count; i++)
                {
                    adjustedList[i] -= baseline;
                }

                return CSVUtils.ListToStringList(adjustedList);
            }
        }

        public long EstimateMemoryUsageBytes()
        {
            return 0;
        }

        public void Update()
        {            
        }
    }
}