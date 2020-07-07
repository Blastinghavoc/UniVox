using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace UniVox.Framework.Jobified
{
    [BurstCompile]
    public struct SortIndicesByMaterialJob : IJob
    {
        [ReadOnly] public NativeArray<int> allTriangleIndices;
        public NativeArray<MaterialRun> materialRuns;
        public NativeList<MaterialRun> packedRuns;
        public NativeList<int> packedIndices;

        //Single element array
        [ReadOnly] public NativeArray<int> collisionMeshMaterialRunLength;

        private struct RunComparer : IComparer<MaterialRun>
        {
            public int Compare(MaterialRun x, MaterialRun y)
            {
                return x.materialID.CompareTo(y.materialID);
            }
        }

        public void Execute()
        {
            var comparer = new RunComparer();
            //Sort the runs by material ID

            var collidableRunLength = collisionMeshMaterialRunLength[0];

            var collidableRuns = materialRuns.GetSubArray(0, collidableRunLength);
            collidableRuns.Sort(comparer);
            
            var nonCollidableRuns = materialRuns.GetSubArray(collidableRunLength, materialRuns.Length - collidableRunLength);
            nonCollidableRuns.Sort(comparer);


            //Resize packedIndices list to required capacity
            packedIndices.Capacity = allTriangleIndices.Length;            

            //Apply the ordering of the runs to the triangle indices
            MaterialRun currentPackedRun = new MaterialRun();
            currentPackedRun.materialID = materialRuns[0].materialID;
            currentPackedRun.range.start = 0;
            for (int i = 0; i < materialRuns.Length; i++)
            {
                var run = materialRuns[i];

                if (run.materialID != currentPackedRun.materialID)
                {
                    currentPackedRun.range.end = packedIndices.Length;
                    if (currentPackedRun.range.Length > 0)
                    {
                        packedRuns.Add(currentPackedRun);
                    }
                    currentPackedRun.materialID = run.materialID;
                    currentPackedRun.range.start = packedIndices.Length;
                }

                //Copy ranges into the packedIndices array in order by material
                var allSlice = allTriangleIndices.GetSubArray(run.range.start, run.range.Length);
                packedIndices.AddRange(allSlice);
            }

            currentPackedRun.range.end = packedIndices.Length;
            if (currentPackedRun.range.Length > 0)
            {
                packedRuns.Add(currentPackedRun);
            }
        }
    }
}