using System.Collections;
using System.Collections.Generic;

namespace PerformanceTesting
{
    public interface IPerformanceTest
    {
        IEnumerator Run(ITestableChunkManager chunkManager);

        string TestName { get; }

        /// <summary>
        /// Get lines for the log file
        /// </summary>
        /// <returns></returns>
        List<string> GetLogLines();

        /// <summary>
        /// Returns a variable name-> values dictionary for all the per-frame variables
        /// in the test. Naturally, all component lists must be the same length.
        /// </summary>
        /// <returns></returns>
        Dictionary<string, List<string>> GetPerFrameData();
        void Clear();
    }
}