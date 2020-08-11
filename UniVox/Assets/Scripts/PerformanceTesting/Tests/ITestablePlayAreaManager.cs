using UnityEngine;

namespace PerformanceTesting
{
    public interface ITestablePlayAreaManager 
    {
        void SetRenderedChunkRadii(Vector3Int radii);
    }
}