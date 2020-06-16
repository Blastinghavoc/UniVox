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
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Implementations.ChunkData;
using UniVox.Implementations.Common;

namespace Tests
{
    public class ChunkPipelineTests
    {
        class MockMesher : IChunkMesher<VoxelData>
        {
            public bool IsMeshDependentOnNeighbourChunks { get; set; } = false;

            Func<Vector3Int, MockChunkComponent> mockGetComponent;

            public MockMesher(Func<Vector3Int,MockChunkComponent> mockGetComponent) 
            {
                this.mockGetComponent = mockGetComponent;
            }

            public Mesh CreateMesh(IChunkData<VoxelData> chunk)
            {
                return new Mesh();
            }

            public AbstractPipelineJob<Mesh> CreateMeshJob(Vector3Int chunkID)
            {
                return new BasicFunctionJob<Mesh>(() => CreateMesh(mockGetComponent(chunkID).Data));
            }
        }

        class MockProvider : IChunkProvider<VoxelData>
        {
            public IChunkData<VoxelData> ProvideChunkData(Vector3Int chunkID)
            {
                return new ArrayChunkData(chunkID, new Vector3Int(1, 1, 1));
            }

            public AbstractPipelineJob<IChunkData<VoxelData>> ProvideChunkDataJob(Vector3Int chunkID)
            {
                return new BasicFunctionJob<IChunkData<VoxelData>>(() => ProvideChunkData(chunkID));
            }
        }

        class MockChunkComponent : IChunkComponent<VoxelData>
        {
            public Vector3Int ChunkID { get; set; }

            public IChunkData<VoxelData> Data { get; set; }

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
        Vector3Int mockPlayChunkID;

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

        float MockGetPriorityOfChunk(Vector3Int chunkID)
        {
            var absDisplacement = (mockPlayChunkID - chunkID).ElementWise(Mathf.Abs);
            return absDisplacement.x + absDisplacement.y + absDisplacement.z;
        }

        ChunkPipelineManager<VoxelData> pipeline;


        [SetUp]
        public void Reset()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            mockMesher = new MockMesher(mockGetComponent);
            mockProvider = new MockProvider();
            mockComponentStorage = new Dictionary<Vector3Int, MockChunkComponent>();
            mockPlayChunkID = Vector3Int.zero;
        }

        [Test] 
        public void CompletePassNoChunkDependencies() 
        {
            pipeline = new ChunkPipelineManager<VoxelData>(
                mockProvider,
                mockMesher,
                mockGetComponent,
                mockCreateNewChunkWithTarget,
                MockGetPriorityOfChunk,
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

            pipeline = new ChunkPipelineManager<VoxelData>(
               mockProvider,
               mockMesher,
               mockGetComponent,
               mockCreateNewChunkWithTarget,
               MockGetPriorityOfChunk,
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
            pipeline = new ChunkPipelineManager<VoxelData>(
               mockProvider,
               mockMesher,
               mockGetComponent,
               mockCreateNewChunkWithTarget,
               MockGetPriorityOfChunk,
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
            pipeline = new ChunkPipelineManager<VoxelData>(
               mockProvider,
               mockMesher,
               mockGetComponent,
               mockCreateNewChunkWithTarget,
               MockGetPriorityOfChunk,
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
            pipeline = new ChunkPipelineManager<VoxelData>(
              mockProvider,
              mockMesher,
              mockGetComponent,
              mockCreateNewChunkWithTarget,
              MockGetPriorityOfChunk,
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
            pipeline = new ChunkPipelineManager<VoxelData>(
              mockProvider,
              mockMesher,
              mockGetComponent,
              mockCreateNewChunkWithTarget,
              MockGetPriorityOfChunk,
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

        [Test]
        public void PriorityCorrectOrderTest() 
        {
            pipeline = new ChunkPipelineManager<VoxelData>(
              mockProvider,
              mockMesher,
              mockGetComponent,
              mockCreateNewChunkWithTarget,
              MockGetPriorityOfChunk,
              1,
              1,
              1
              );

            var first = new Vector3Int(0, 0, 0);            
            var second = new Vector3Int(0, 1, 0);            
            var third = new Vector3Int(10, 0, 0);            

            //Adding chunks in reverse order that I expect them to come out
            mockCreateNewChunkWithTarget(third, pipeline.CompleteStage);
            mockCreateNewChunkWithTarget(second, pipeline.CompleteStage);
            mockCreateNewChunkWithTarget(first, pipeline.CompleteStage);

            pipeline.Update();

            AssertChunkStages(first, pipeline.CompleteStage, pipeline.CompleteStage, pipeline.CompleteStage);
            AssertChunkStages(second, 0, 0, pipeline.CompleteStage);
            AssertChunkStages(third, 0, 0, pipeline.CompleteStage);

            pipeline.Update();

            AssertChunkStages(second, pipeline.CompleteStage, pipeline.CompleteStage, pipeline.CompleteStage);
            AssertChunkStages(third, 0, 0, pipeline.CompleteStage);

            pipeline.Update();

            AssertChunkStages(third, pipeline.CompleteStage, pipeline.CompleteStage, pipeline.CompleteStage);
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

        /// <summary>
        /// Asserts that the chunk ID has the correct min max and target stages
        /// in the pipeline
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="target"></param>
        private void AssertChunkStages(Vector3Int id,int min,int max,int target) 
        {
            Assert.AreEqual(min, pipeline.GetMinStage(id));
            Assert.AreEqual(max, pipeline.GetMaxStage(id));
            Assert.AreEqual(target, pipeline.GetTargetStage(id));
        }
    }
}
