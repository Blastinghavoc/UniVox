using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Internal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UniVox.Framework;
using UniVox.Framework.ChunkPipeline;
using UniVox.Implementations.ChunkData;
using UniVox.Implementations.Common;

namespace Tests
{
    public class ChunkPipelineTests
    {
        class MockMesher : IChunkMesher<AbstractChunkData, VoxelData>
        {
            public bool IsMeshDependentOnNeighbourChunks { get; set; } = false;

            public Mesh CreateMesh(AbstractChunkData chunk)
            {
                return new Mesh();
            }
        }

        class MockProvider : IChunkProvider<AbstractChunkData, VoxelData>
        {
            public AbstractChunkData ProvideChunkData(Vector3Int chunkID)
            {
                return new ArrayChunkData(chunkID, new Vector3Int(1, 1, 1));
            }
        }

        class MockChunkComponent : IChunkComponent<AbstractChunkData, VoxelData>
        {
            public Vector3Int ChunkID { get; set; }

            public AbstractChunkData Data { get; set; }

            public Mesh RenderMesh { get; set; }

            public Mesh CollisionMesh { get; set; }

            public Mesh GetRenderMesh()
            {
                return RenderMesh;
            }

            public void Initialise(Vector3Int id, Vector3 position)
            {
            }

            public void RemoveCollisionMesh()
            {
                CollisionMesh = null;
            }

            public void RemoveRenderMesh()
            {
                RenderMesh = null;
            }

            public void SetCollisionMesh(Mesh mesh)
            {
                CollisionMesh = mesh;
            }

            public void SetRenderMesh(Mesh mesh)
            {
                RenderMesh = mesh;
            }
        }

        MockMesher mockMesher;
        MockProvider mockProvider;

        Dictionary<Vector3Int, MockChunkComponent> mockComponentStorage;

        MockChunkComponent mockGetComponent(Vector3Int id) 
        {
            return mockComponentStorage[id];
        }

        void mockCreateNewChunkWithTarget(Vector3Int id, int target) 
        {
            pipeline.AddChunk(id, target);
            mockComponentStorage.Add(id,new MockChunkComponent() { ChunkID = id});
        }

        ChunkPipelineManager<AbstractChunkData,VoxelData> pipeline;


        [SetUp]
        public void Reset()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            mockMesher = new MockMesher();
            mockProvider = new MockProvider();
            mockComponentStorage = new Dictionary<Vector3Int, MockChunkComponent>();
        }

        [Test] 
        public void CompletePassNoChunkDependencies() 
        {
            pipeline = new ChunkPipelineManager<AbstractChunkData, VoxelData>(
                mockProvider,
                mockMesher,
                mockGetComponent,
                mockCreateNewChunkWithTarget,
                6,
                1,
                1
                );

            var testChunkID = new Vector3Int(0, 0, 0);

            mockCreateNewChunkWithTarget(testChunkID, pipeline.CompleteStage);

            pipeline.Update();

            //Chunk passes the whole way through
            Assert.AreEqual(pipeline.CompleteStage,pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage,pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage,pipeline.GetMinStage(testChunkID));
        }

        [Test]
        public void CompletePassWithChunkDependencies() 
        {
            mockMesher.IsMeshDependentOnNeighbourChunks = true;

            pipeline = new ChunkPipelineManager<AbstractChunkData, VoxelData>(
               mockProvider,
               mockMesher,
               mockGetComponent,
               mockCreateNewChunkWithTarget,
               6,
               1,
               1
               );

            var testChunkID = new Vector3Int(0, 0, 0);

            mockCreateNewChunkWithTarget(testChunkID, pipeline.CompleteStage);

            pipeline.Update();

            //Chunk gets stuck at the Data stage, as it has to wait for dependencies
            Assert.AreEqual(pipeline.DataStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.DataStage, pipeline.GetMinStage(testChunkID));

            //Second update allows data to be generated for neighbours
            pipeline.Update();

            //Chunk passes the whole way through
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetMinStage(testChunkID));
        }

        [Test]
        public void SetTargetHigherThanCurrentNoWIP() 
        {
            pipeline = new ChunkPipelineManager<AbstractChunkData, VoxelData>(
               mockProvider,
               mockMesher,
               mockGetComponent,
               mockCreateNewChunkWithTarget,
               6,
               1,
               1
               );

            var testChunkID = new Vector3Int(0, 0, 0);

            mockCreateNewChunkWithTarget(testChunkID, pipeline.RenderedStage);

            pipeline.Update();

            //Chunk ends up in rendered stage
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMinStage(testChunkID));

            pipeline.Update();
            //Further updates do not take the chunk higher than its target stage
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMinStage(testChunkID));

            //Update the target
            pipeline.SetTarget(testChunkID, pipeline.CompleteStage);

            //New target should be CompleteStage, other values should be unmodified
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMinStage(testChunkID));

            pipeline.Update();

            //Chunk should have reached completion
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetMinStage(testChunkID));
        }

        [Test]
        public void SetTargetHigherThanCurrentWithWIP() 
        {
            pipeline = new ChunkPipelineManager<AbstractChunkData, VoxelData>(
               mockProvider,
               mockMesher,
               mockGetComponent,
               mockCreateNewChunkWithTarget,
               6,
               1,
               1
               );

            var testChunkID = new Vector3Int(0, 0, 0);

            mockCreateNewChunkWithTarget(testChunkID, pipeline.RenderedStage);

            pipeline.Update();

            //Chunk ends up in rendered stage
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMinStage(testChunkID));

            //Cause there to be work in progress
            pipeline.ReenterAtStage(testChunkID, pipeline.DataStage);
            //Update the target
            pipeline.SetTarget(testChunkID, pipeline.CompleteStage);

            //New target should be CompleteStage, min stage should be DataStage
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.DataStage, pipeline.GetMinStage(testChunkID));

            pipeline.Update();

            //Chunk should have reached completion, as the work-in-progress continues to the new target
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetMinStage(testChunkID));
        }

        [Test]
        public void SetTargetLowerThanCurrentAndMax() 
        {
            pipeline = new ChunkPipelineManager<AbstractChunkData, VoxelData>(
              mockProvider,
              mockMesher,
              mockGetComponent,
              mockCreateNewChunkWithTarget,
              6,
              1,
              1
              );

            var testChunkID = new Vector3Int(0, 0, 0);

            mockCreateNewChunkWithTarget(testChunkID, pipeline.RenderedStage);

            pipeline.Update();

            //Increase target
            pipeline.SetTarget(testChunkID, pipeline.CompleteStage);

            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMinStage(testChunkID));

            //Set target lower than the current target, and lower than the current max stage
            pipeline.SetTarget(testChunkID, pipeline.DataStage);

            //Render mesh should have been removed
            Assert.IsNull(mockComponentStorage[testChunkID].RenderMesh);

            //Max stage and target should have been decreased, min stage should have decreased to max
            Assert.AreEqual(pipeline.DataStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.DataStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.DataStage, pipeline.GetMinStage(testChunkID));

        }

        [Test]
        public void SetTargetLowerThanCurrentGreaterThanMax() 
        {
            pipeline = new ChunkPipelineManager<AbstractChunkData, VoxelData>(
              mockProvider,
              mockMesher,
              mockGetComponent,
              mockCreateNewChunkWithTarget,
              6,
              1,
              1
              );

            var testChunkID = new Vector3Int(0, 0, 0);

            mockCreateNewChunkWithTarget(testChunkID, pipeline.DataStage);

            pipeline.Update();

            //Increase target
            pipeline.SetTarget(testChunkID, pipeline.CompleteStage);

            Assert.AreEqual(pipeline.DataStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.DataStage, pipeline.GetMinStage(testChunkID));

            //Set target lower than the current target, but higher than the current max stage
            pipeline.SetTarget(testChunkID, pipeline.RenderedStage);

            //Check only target changed
            Assert.AreEqual(pipeline.DataStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.DataStage, pipeline.GetMinStage(testChunkID));

            pipeline.Update();

            //After update, the chunk should have reached the new target stage
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMinStage(testChunkID));

        }

        // A Test behaves as an ordinary method
        [Test]
        public void ChunkPipelineTestsSimplePasses()
        {
            // Use the Assert class to test conditions
        }

        // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
        // `yield return null;` to skip a frame.
        [UnityTest]
        public IEnumerator ChunkPipelineTestsWithEnumeratorPasses()
        {
            // Use the Assert class to test conditions.
            // Use yield to skip a frame.
            yield return null;
        }
    }
}
