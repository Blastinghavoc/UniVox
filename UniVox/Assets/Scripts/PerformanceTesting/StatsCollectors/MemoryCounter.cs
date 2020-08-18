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
}