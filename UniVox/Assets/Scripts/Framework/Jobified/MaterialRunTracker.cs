using Unity.Burst;
using Unity.Collections;

namespace UniVox.Framework.Jobified
{
    [BurstCompile]
    public struct MaterialRunTracker 
    {
        MaterialRun currentRun;

        public void Update(ushort materialID, NativeList<MaterialRun> materialRuns, NativeList<int> allTriangleIndices) 
        {
            if (materialID != currentRun.materialID)
            {
                EndRun(materialRuns,allTriangleIndices);
                currentRun.materialID = materialID;
            }
        }

        /// <summary>
        /// End the current run
        /// </summary>
        public void EndRun(NativeList<MaterialRun> materialRuns, NativeList<int> allTriangleIndices) 
        {
            currentRun.range.end = allTriangleIndices.Length;
            materialRuns.Add(currentRun);
            //Start a new run
            currentRun.range.start = allTriangleIndices.Length;
        }
    }
}