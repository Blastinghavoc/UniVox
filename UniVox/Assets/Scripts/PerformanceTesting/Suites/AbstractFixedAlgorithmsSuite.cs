using UniVox.Framework;
using UniVox.Implementations.ChunkData;
using UniVox.Implementations.Meshers;
using UniVox.Implementations.Providers;
using UniVox.MessagePassing;

namespace PerformanceTesting
{
    /// <summary>
    /// Suite for running tests where neither the meshing algorithm
    /// nor the storage type are variables.
    /// </summary>
    public abstract class AbstractFixedAlgorithmsSuite : AbstractTestSuite
    {
        public int seed;

        protected virtual void SetupPass()
        {
            mesher = RemoveComponentsOfTypeExceptSubtype<AbstractMesherComponent, GreedyMesher>();
            provider = RemoveComponentsOfTypeExceptSubtype<AbstractProviderComponent, NoisyProvider>();
            var noisyProvider = provider as NoisyProvider;
            SceneMessagePasser.SetMessage(new SeedMessage() { seed = seed });
            noisyProvider.chunkDataFactory.typeToCreate = ChunkDataFactory.ChunkDataType.FlatArray;
        }
    }
}