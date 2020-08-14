using System.Collections.Generic;

namespace PerformanceTesting
{
    public interface IStatsCollector
    {
        void Update();

        string VariableName { get; }

        List<string> Data { get; }

        long EstimateMemoryUsageBytes();
    }
}