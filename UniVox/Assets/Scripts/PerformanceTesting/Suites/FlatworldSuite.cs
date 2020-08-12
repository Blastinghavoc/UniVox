﻿using System.Collections.Generic;
using UniVox.Framework;
using UniVox.Implementations.Meshers;
using UniVox.Implementations.Providers;
using UniVox.Implementations.ChunkData;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor.VersionControl;

namespace PerformanceTesting
{
    public class FlatworldSuite : AbstractTestSuite
    {
        public Vector3Int renderedChunksRadii;

        /// <summary>
        /// Expects the chunk manager to be kept up to date externally.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<PassDetails> Passes()
        {
            ///For each mesh algorithm, with fixed storage type (flat array)
            
            //Naive
            mesher = RemoveComponentsOfTypeExceptSubtype<AbstractMesherComponent, NaiveMesher>();
            CommonForMeshTests();
            yield return EndPass("MeshingComparisons");

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
            chunkManager.gameObject.GetComponent<DebugProvider>().chunkDataFactory.typeToCreate = ChunkDataFactory.ChunkDataType.FlatArray;
            yield return EndPass("StorageComparisons");

            //Octree
            CommonForStorageTests();
            chunkManager.gameObject.GetComponent<DebugProvider>().chunkDataFactory.typeToCreate = ChunkDataFactory.ChunkDataType.Octree;
            yield return EndPass("StorageComparisons");

            //RLE
            CommonForStorageTests();
            chunkManager.gameObject.GetComponent<DebugProvider>().chunkDataFactory.typeToCreate = ChunkDataFactory.ChunkDataType.RLE;
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
            provider = RemoveComponentsOfTypeExceptSubtype<AbstractProviderComponent, DebugProvider>();
            var debugProvider = provider as DebugProvider;
            debugProvider.worldType = DebugProvider.WorldType.flat;
            debugProvider.chunkDataFactory.typeToCreate = ChunkDataFactory.ChunkDataType.FlatArray;

            chunkManager.SetRenderedRadii(renderedChunksRadii);
        }
    }
}