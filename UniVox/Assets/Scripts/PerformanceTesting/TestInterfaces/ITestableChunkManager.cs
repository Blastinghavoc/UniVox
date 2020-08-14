using UniVox.Framework;
using UniVox.Framework.PlayAreaManagement;

namespace PerformanceTesting
{
    public interface ITestableChunkManager : IChunkManager
    {
        PlayAreaManager PlayArea { get; }

        bool PipelineIsSettled();

        IVoxelPlayer GetPlayer();

        string GetPipelineStatus();
        void SetIncludeLighting(bool include);
    }
}