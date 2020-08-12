using System.Collections.Generic;
using UniVox.Framework;
using UniVox.Implementations.Meshers;
using UniVox.Implementations.Providers;
using UniVox.Implementations.ChunkData;
using UniVox.MessagePassing;

namespace PerformanceTesting
{
    public class ProceduralSuite : AbstractTestSuite 
    {
        public int seed;
        public override IEnumerable<PassDetails> Passes()
        {
            ///For each mesh algorithm (except naive), with fixed storage type (flat array)

            //Culling
            mesher = RemoveComponentsOfTypeExceptSubtype<AbstractMesherComponent, CullingMesher>();
            CommonForMeshTests();
            yield return EndPass("MeshingComparisons");

            //Greedy
            mesher = RemoveComponentsOfTypeExceptSubtype<AbstractMesherComponent, GreedyMesher>();
            CommonForMeshTests();
            yield return EndPass("MeshingComparisons");

            ///Then, for each storage type, but with fixed meshing algorithm

            //Flat array
            CommonForStorageTests();
            chunkManager.gameObject.GetComponent<NoisyProvider>().chunkDataFactory.typeToCreate = ChunkDataFactory.ChunkDataType.FlatArray;
            yield return EndPass("StorageComparisons");

            //Octree
            CommonForStorageTests();
            chunkManager.gameObject.GetComponent<NoisyProvider>().chunkDataFactory.typeToCreate = ChunkDataFactory.ChunkDataType.Octree;
            yield return EndPass("StorageComparisons");

            //RLE
            CommonForStorageTests();
            chunkManager.gameObject.GetComponent<NoisyProvider>().chunkDataFactory.typeToCreate = ChunkDataFactory.ChunkDataType.RLE;
            yield return EndPass("StorageComparisons");
        }

        private void CommonForStorageTests()
        {
            //Use the greedy mesher for storage tests
            mesher = RemoveComponentsOfTypeExceptSubtype<AbstractMesherComponent, GreedyMesher>();
            CommonForMeshTests();
        }

        private void CommonForMeshTests()
        {
            provider = RemoveComponentsOfTypeExceptSubtype<AbstractProviderComponent, NoisyProvider>();
            var noisyProvider = provider as NoisyProvider;
            SceneMessagePasser.SetMessage(new SeedMessage() { seed = seed });
            noisyProvider.chunkDataFactory.typeToCreate = ChunkDataFactory.ChunkDataType.FlatArray;
        }
    }
}