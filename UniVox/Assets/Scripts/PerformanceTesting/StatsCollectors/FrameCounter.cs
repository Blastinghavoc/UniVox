using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace PerformanceTesting
{
    public class FrameCounter:IStatsCollector
    {

        public List<float> FrameTimesMillis { get; private set; } = new List<float>();

        public string VariableName { get; } = "FrameTimeMillis";

        public List<string> Data { get => CSVUtils.ListToStringList(FrameTimesMillis); }

        public string[] ToCSVLines()
        {
            return new string[]
            {
                "FrameTimesMillis:",
                CSVUtils.MakeCSVString(FrameTimesMillis),
            };
        }

        /// <summary>
        /// To be called from an Update function (i.e once per frame)
        /// </summary>
        public void Update()
        {            
            FrameTimesMillis.Add(Time.unscaledDeltaTime*1000);
        }
    }
}