using System.Collections.Generic;
using UniVox.Framework;
using UniVox.Implementations.Meshers;
using UniVox.Implementations.Providers;
using UniVox.Implementations.ChunkData;

namespace PerformanceTesting
{
    public class FlatworldSuite : AbstractTestSuite
    {
        /// <summary>
        /// Expects the chunk manager to be kept up to date externally.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<PassDetails> Passes()
        {
            ///For each mesh algorithm, with fixed storage type (flat array)
            
            //Naive
            RemoveComponentsOfTypeThatAreNotSubtype<AbstractMesherComponent, NaiveMesher>();
            CommonForMeshTests();
            yield return new PassDetails() { GroupName = "MeshingComparisons", TechniqueName = GetTechniqueName()};

            //Culling
            RemoveComponentsOfTypeThatAreNotSubtype<AbstractMesherComponent, CullingMesher>();
            CommonForMeshTests();
            yield return new PassDetails() { GroupName = "MeshingComparisons", TechniqueName = GetTechniqueName()};

            //Greedy
            RemoveComponentsOfTypeThatAreNotSubtype<AbstractMesherComponent, GreedyMesher>();
            CommonForMeshTests();
            yield return new PassDetails() { GroupName = "MeshingComparisons", TechniqueName = GetTechniqueName()};

            ///Then, for each storage type, but with fixed meshing algorithm

            //Flat array
            CommonForStorageTests();
            chunkManager.gameObject.GetComponent<DebugProvider>().chunkDataFactory.typeToCreate = ChunkDataFactory.ChunkDataType.FlatArray;
            yield return new PassDetails() { GroupName = "StorageComparisons", TechniqueName = GetTechniqueName() };

            //Octree
            CommonForStorageTests();
            chunkManager.gameObject.GetComponent<DebugProvider>().chunkDataFactory.typeToCreate = ChunkDataFactory.ChunkDataType.Octree;
            yield return new PassDetails() { GroupName = "StorageComparisons", TechniqueName = GetTechniqueName() };

            //RLE
            CommonForStorageTests();
            chunkManager.gameObject.GetComponent<DebugProvider>().chunkDataFactory.typeToCreate = ChunkDataFactory.ChunkDataType.RLE;
            yield return new PassDetails() { GroupName = "StorageComparisons", TechniqueName = GetTechniqueName() };
        }

        private void CommonForStorageTests() 
        {
            //Use the greedy mesher for storage tests
            RemoveComponentsOfTypeThatAreNotSubtype<AbstractMesherComponent, GreedyMesher>();
            CommonForMeshTests();            
        }

        private void CommonForMeshTests() 
        {
            RemoveComponentsOfTypeThatAreNotSubtype<AbstractProviderComponent, DebugProvider>();
            var provider = chunkManager.gameObject.GetComponent<DebugProvider>();
            provider.worldType = DebugProvider.WorldType.flat;
            provider.chunkDataFactory.typeToCreate = ChunkDataFactory.ChunkDataType.FlatArray;
        }
    }
}