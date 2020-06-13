using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Profiling;
using System.Collections.Generic;

namespace PerformanceTesting
{
    public class MemoryCounter:IStatsCollector
    {
        public List<long> memoryPerFrame = new List<long>();

        public long BaseLine { get; private set; }

        public string VariableName { get; } = "MemoryUsageBytes";

        public List<string> Data { get => CSVUtils.ListToStringList(memoryPerFrame); }

        public MemoryCounter()         
        {
            BaseLine = Profiler.GetTotalAllocatedMemoryLong();
        }

        public void Update()
        {            
            memoryPerFrame.Add(Profiler.GetTotalAllocatedMemoryLong() - BaseLine);
        }

    }
}