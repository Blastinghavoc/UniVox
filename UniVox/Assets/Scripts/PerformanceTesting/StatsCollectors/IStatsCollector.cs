using UnityEngine;
using System.Collections;

namespace PerformanceTesting
{
    public interface IStatsCollector
    {
        string[] ToCSVLines();
    }
}