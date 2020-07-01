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
using UniVox.Framework.Jobified;
using UniVox.Implementations.ChunkData;

namespace Tests
{
    public class ChunkPipelineTests
    {
        class MockMesher : IChunkMesher
        {
            public bool IsMeshDependentOnNeighbourChunks { get; set; } = false;

            Func<Vector3Int, MockChunkComponent> mockGetComponent;

            public MockMesher(Func<Vector3Int,MockChunkComponent> mockGetComponent) 
            {
                this.mockGetComponent = mockGetComponent;
            }

            public Mesh CreateMesh(IChunkData chunk)
            {
                return new Mesh();
            }

            public AbstractPipelineJob<MeshDescriptor> CreateMeshJob(Vector3Int chunkID)
            {
                return new BasicFunctionJob<MeshDescriptor>(() => 
                new MeshDescriptor() {                
                    mesh= CreateMesh(mockGetComponent(chunkID).Data)
                }
                
                );
            }

            public AbstractPipelineJob<Mesh> ApplyCollisionMeshJob(Vector3Int chunkID)
            {
                return new BasicFunctionJob<Mesh>(() =>
                CreateMesh(mockGetComponent(chunkID).Data)
                ); 
            }

            public void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager, FrameworkEventManager eventManager)
            {
                throw new NotImplementedException();
            }
        }

        class MockProvider : IChunkProvider
        {
            public AbstractPipelineJob<ChunkNeighbourhood> GenerateStructuresForNeighbourhood(Vector3Int centerChunkID, ChunkNeighbourhood neighbourhood)
            {
                return new BasicFunctionJob<ChunkNeighbourhood>(() => null);
            }

            public AbstractPipelineJob<IChunkData> GenerateTerrainData(Vector3Int chunkID)
            {
                return new BasicFunctionJob<IChunkData>(() => ProvideChunkData(chunkID));
            }

            public void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager, FrameworkEventManager eventManager)
            {
                throw new NotImplementedException();
            }

            public IChunkData ProvideChunkData(Vector3Int chunkID)
            {
                return new ArrayChunkData(chunkID, new Vector3Int(1, 1, 1));
            }

            public void StoreModifiedChunkData(Vector3Int chunkID, IChunkData data)
            {
                throw new NotImplementedException();
            }

            public bool TryGetStoredDataForChunk(Vector3Int chunkID, out IChunkData storedData)
            {
                throw new NotImplementedException();
            }
        }

        class MockChunkComponent : IChunkComponent
        {
            public Vector3Int ChunkID { get; set; }

            public IChunkData Data { get; set; }

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

            public void SetRenderMesh(MeshDescriptor meshDesc)
            {
                RenderMesh = meshDesc.mesh;                
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

        void mockAddNewChunkWithTarget(Vector3Int id, int target) 
        {
            mockComponentStorage.Add(id, new MockChunkComponent() { ChunkID = id});
            pipeline.Add(id, target);
        }

        void mockRemoveChunk(Vector3Int id) 
        {
            pipeline.RemoveChunk(id);
            mockComponentStorage.Remove(id);
        }

        float MockGetPriorityOfChunk(Vector3Int chunkID)
        {
            var absDisplacement = (mockPlayChunkID - chunkID).ElementWise(Mathf.Abs);
            return absDisplacement.x + absDisplacement.y + absDisplacement.z;
        }

        ChunkPipelineManager pipeline;


        [SetUp]
        public void Reset()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            mockMesher = new MockMesher(mockGetComponent);
            mockMesher.IsMeshDependentOnNeighbourChunks = true;//by default mesher is dependent on neighbour, as only the naive mesher is not
            mockProvider = new MockProvider();
            mockComponentStorage = new Dictionary<Vector3Int, MockChunkComponent>();
            mockPlayChunkID = Vector3Int.zero;
        }

        private void MakePipeline(int numData, int numMesh, int numCollision, bool makeStructures = false, int numStructures = 200) 
        {
            pipeline = new ChunkPipelineManager(
                mockProvider,
                mockMesher,
                mockGetComponent,
                MockGetPriorityOfChunk,
                numData,
                numMesh,
                numCollision,
                makeStructures,
                numStructures
                );
        }        

        private void AddOrUpdateTarget(Vector3Int chunkId, int targetStage) 
        {
            if (mockComponentStorage.ContainsKey(chunkId))
            {
                pipeline.SetTarget(chunkId, targetStage);
            }
            else
            {
                mockAddNewChunkWithTarget(chunkId, targetStage);
            }
        }

        private void AddAllDependenciesNecessaryForChunkToGetToStage(Vector3Int chunkId, int targetStage) 
        {
            int terrainRadius = 0;
            int allDataRadius = 0;
            bool includeDiagonals = pipeline.StructureGen;
            if (targetStage > pipeline.TerrainDataStage)
            {
                terrainRadius++;
            }
            if (targetStage > pipeline.FullyGenerated)
            {
                if (pipeline.StructureGen)
                {
                    terrainRadius++;   
                }
                allDataRadius++;
            }

            Func<Vector3Int, int,bool> radiusTest = (offset,radius)=> offset.All((v) => Math.Abs(v) <= radius);

            if (!includeDiagonals)
            {
                radiusTest = (offset, radius) =>
                {
                    var abs = offset.ElementWise((_) => Math.Abs(_));
                    var manhattan = abs.x + abs.y + abs.z;
                    return manhattan <= terrainRadius;
                };
            }

            for (int z = -terrainRadius; z <= terrainRadius; z++)
            {
                for (int y = -terrainRadius; y <= terrainRadius; y++)
                {
                    for (int x = -terrainRadius; x <= terrainRadius; x++)
                    {
                        var offset = new Vector3Int(x, y, z);                       

                        if (offset.All((v)=>v==0))
                        {
                            continue;//skip center chunk
                        }

                        var id = chunkId + offset;
                        if (radiusTest(offset,allDataRadius))
                        {
                            AddOrUpdateTarget(id, pipeline.FullyGenerated);
                            Debug.Log($"Set {id} target to AllData");
                        }
                        else if (radiusTest(offset,terrainRadius))
                        {
                            AddOrUpdateTarget(id, pipeline.TerrainDataStage);
                            Debug.Log($"Set {id} target to TerrainData");
                        }
                    }
                }
            }
        }

        [Test] 
        public void CompletePassNoChunkDependenciesNoStructures() 
        {
            mockMesher.IsMeshDependentOnNeighbourChunks = false;
            MakePipeline(6, 1, 1);

            var testChunkID = new Vector3Int(0, 0, 0);

            mockAddNewChunkWithTarget(testChunkID, pipeline.CompleteStage);

            pipeline.Update();

            //Chunk passes the whole way through
            Assert.AreEqual(pipeline.CompleteStage,pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage,pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage,pipeline.GetMinStage(testChunkID));
        }

        [Test]
        public void CompletePassNoStructures() 
        {
            MakePipeline(20, 1, 1);

            var testChunkID = new Vector3Int(0, 0, 0);

            mockAddNewChunkWithTarget(testChunkID, pipeline.CompleteStage);

            pipeline.Update();

            //Chunk gets stuck at the Data stage, as it has to wait for dependencies
            AssertChunkStages(testChunkID, pipeline.FullyGenerated, pipeline.FullyGenerated, pipeline.CompleteStage);

            //Add all neighbours necessary for chunk to get to complete stage
            AddAllDependenciesNecessaryForChunkToGetToStage(testChunkID, pipeline.CompleteStage);

            //Second update allows data to be generated for neighbours
            pipeline.Update();

            //Chunk passes the whole way through
            AssertChunkStages(testChunkID, pipeline.CompleteStage, pipeline.CompleteStage, pipeline.CompleteStage);
        }

        [Test]
        public void CompletePass()
        {
            MakePipeline(1000, 1, 1,true);

            var testChunkID = new Vector3Int(0, 0, 0);

            mockAddNewChunkWithTarget(testChunkID, pipeline.CompleteStage);

            pipeline.Update();

            //Chunk gets stuck at the terrain data stage, as it has to wait for dependencies
            AssertChunkStages(testChunkID, pipeline.TerrainDataStage, pipeline.TerrainDataStage, pipeline.CompleteStage);

            //Add all neighbours necessary for chunk to get to complete stage
            AddAllDependenciesNecessaryForChunkToGetToStage(testChunkID, pipeline.CompleteStage);

            //Second update allows data to be generated for neighbours
            pipeline.Update();

            //Chunk passes the whole way through
            AssertChunkStages(testChunkID, pipeline.CompleteStage, pipeline.CompleteStage, pipeline.CompleteStage);
        }

        [Test]
        public void SetTargetHigherThanCurrentNoWIP() 
        {
            MakePipeline(6, 1, 1);

            var testChunkID = new Vector3Int(0, 0, 0);

            mockAddNewChunkWithTarget(testChunkID, pipeline.RenderedStage);

            pipeline.Update();

            //Chunk ends up in rendered stage
            AssertChunkStages(testChunkID, pipeline.RenderedStage);

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
            pipeline = new ChunkPipelineManager(
               mockProvider,
                mockMesher,
                mockGetComponent,
                MockGetPriorityOfChunk,
                6,
                1,
                1
               );

            var testChunkID = new Vector3Int(0, 0, 0);

            mockAddNewChunkWithTarget(testChunkID, pipeline.RenderedStage);

            pipeline.Update();

            //Chunk ends up in rendered stage
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMinStage(testChunkID));

            //Cause there to be work in progress
            pipeline.ReenterAtStage(testChunkID, pipeline.FullyGenerated);
            //Update the target
            pipeline.SetTarget(testChunkID, pipeline.CompleteStage);

            //New target should be CompleteStage, min stage should be DataStage
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.FullyGenerated, pipeline.GetMinStage(testChunkID));

            pipeline.Update();

            //Chunk should have reached completion, as the work-in-progress continues to the new target
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetMinStage(testChunkID));
        }

        [Test]
        public void SetTargetLowerThanCurrentAndMax() 
        {
            pipeline = new ChunkPipelineManager(
              mockProvider,
                mockMesher,
                mockGetComponent,
                MockGetPriorityOfChunk,
                6,
                1,
                1
              );

            var testChunkID = new Vector3Int(0, 0, 0);

            mockAddNewChunkWithTarget(testChunkID, pipeline.RenderedStage);

            pipeline.Update();

            //Increase target
            pipeline.SetTarget(testChunkID, pipeline.CompleteStage);

            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMinStage(testChunkID));

            //Set target lower than the current target, and lower than the current max stage
            pipeline.SetTarget(testChunkID, pipeline.FullyGenerated);

            //Render mesh should have been removed
            Assert.IsNull(mockComponentStorage[testChunkID].RenderMesh);

            //Max stage and target should have been decreased, min stage should have decreased to max
            Assert.AreEqual(pipeline.FullyGenerated, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.FullyGenerated, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.FullyGenerated, pipeline.GetMinStage(testChunkID));

        }

        [Test]
        public void SetTargetLowerThanCurrentGreaterThanMax() 
        {
            pipeline = new ChunkPipelineManager(
              mockProvider,
                mockMesher,
                mockGetComponent,
                MockGetPriorityOfChunk,
                6,
                1,
                1
              );

            var testChunkID = new Vector3Int(0, 0, 0);

            mockAddNewChunkWithTarget(testChunkID, pipeline.FullyGenerated);

            pipeline.Update();

            //Increase target
            pipeline.SetTarget(testChunkID, pipeline.CompleteStage);

            Assert.AreEqual(pipeline.FullyGenerated, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.CompleteStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.FullyGenerated, pipeline.GetMinStage(testChunkID));

            //Set target lower than the current target, but higher than the current max stage
            pipeline.SetTarget(testChunkID, pipeline.RenderedStage);

            //Check only target changed
            Assert.AreEqual(pipeline.FullyGenerated, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.FullyGenerated, pipeline.GetMinStage(testChunkID));

            pipeline.Update();

            //After update, the chunk should have reached the new target stage
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMaxStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetTargetStage(testChunkID));
            Assert.AreEqual(pipeline.RenderedStage, pipeline.GetMinStage(testChunkID));

        }

        [Test]
        public void PriorityCorrectOrderTest() 
        {
            pipeline = new ChunkPipelineManager(
              mockProvider,
                mockMesher,
                mockGetComponent,
                MockGetPriorityOfChunk,
                1,
                1,
                1
              );

            var first = new Vector3Int(0, 0, 0);            
            var second = new Vector3Int(0, 1, 0);            
            var third = new Vector3Int(10, 0, 0);            

            //Adding chunks in reverse order that I expect them to come out
            mockAddNewChunkWithTarget(third, pipeline.CompleteStage);
            mockAddNewChunkWithTarget(second, pipeline.CompleteStage);
            mockAddNewChunkWithTarget(first, pipeline.CompleteStage);

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

       [Test]
        public void ChunkRemovedWhileScheduledForData() 
        {
            pipeline = new ChunkPipelineManager(
              mockProvider,
                mockMesher,
                mockGetComponent,
                MockGetPriorityOfChunk,
                1,
                1,
                1
              );

            var testId = new Vector3Int(10, 0, 0);
            mockAddNewChunkWithTarget(new Vector3Int(0, 0, 0), pipeline.CompleteStage);
            mockAddNewChunkWithTarget(testId, pipeline.CompleteStage);

            pipeline.Update();

            //the testId should not have progressed due to the max per update limit
            AssertChunkStages(testId, 0, 0, pipeline.CompleteStage);

            //Remove the chunk
            mockRemoveChunk(testId);

            //Exception should be thrown on trying to get the chunk id now
            Assert.That(() => pipeline.GetMaxStage(testId),
                  Throws.TypeOf<ArgumentOutOfRangeException>(),
                  "Should throw exception when trying to get max stage of a chunk that was removed from the pipeline");

            //Add the chunk back
            mockAddNewChunkWithTarget(testId, pipeline.CompleteStage);

            pipeline.Update();

            //The chunk should have passed through the pipeline

            AssertChunkStages(testId, pipeline.CompleteStage, pipeline.CompleteStage, pipeline.CompleteStage);

        }

        [Test]
        public void ChunkRemovedWhileScheduledForMeshWithDependencies()
        {
            mockMesher.IsMeshDependentOnNeighbourChunks = true;
            pipeline = new ChunkPipelineManager(
              mockProvider,
                mockMesher,
                mockGetComponent,
                MockGetPriorityOfChunk,
                100,
                1,
                1
              );

            var zeroId = new Vector3Int(0, 0, 0);
            var testId = new Vector3Int(1, 0, 0);
            mockAddNewChunkWithTarget(zeroId, pipeline.CompleteStage);

            pipeline.Update();

            //cid 0 should get to waiting for neighbours
            AssertChunkStages(zeroId, pipeline.FullyGenerated, pipeline.FullyGenerated, pipeline.CompleteStage);

            //The test id should have been added to the start stage as it is a neighbour of cid 0
            AssertChunkStages(testId, 0, 0, pipeline.FullyGenerated);

            //Update the target of some of the other neighbours
            pipeline.SetTarget(new Vector3Int(0, 1, 0), pipeline.CompleteStage);
            pipeline.SetTarget(new Vector3Int(0, 0, 1), pipeline.CompleteStage);
            pipeline.SetTarget(new Vector3Int(0, 0, -1), pipeline.CompleteStage);
            pipeline.SetTarget(new Vector3Int(0, -1, 0), pipeline.CompleteStage);

            pipeline.Update();

            //zeroID should be complete
            AssertChunkStages(zeroId, pipeline.CompleteStage, pipeline.CompleteStage, pipeline.CompleteStage);

            //Update the target of the test id
            pipeline.SetTarget(testId, pipeline.CompleteStage);

            pipeline.Update();
            pipeline.Update();

            //the test id should be scheduled for mesh
            AssertChunkStages(testId, pipeline.FullyGenerated + 1, pipeline.FullyGenerated + 1, pipeline.CompleteStage);

            //Remove the test id
            mockRemoveChunk(testId);

            //Exception should be thrown on trying to get the chunk id now
            Assert.That(() => pipeline.GetMaxStage(testId),
                  Throws.TypeOf<ArgumentOutOfRangeException>(),
                  "Should throw exception when trying to get max stage of a chunk that was removed from the pipeline");

            //Add it back with a lower priority
            mockAddNewChunkWithTarget(testId, pipeline.FullyGenerated);

            pipeline.Update();

            //test id should be back in the data stage
            AssertChunkStages(testId, pipeline.FullyGenerated, pipeline.FullyGenerated, pipeline.FullyGenerated);

        }

        [Test]
        public void OneOfNeighboursRemovedWhileScheduledForMesh()
        {
            mockMesher.IsMeshDependentOnNeighbourChunks = true;
            pipeline = new ChunkPipelineManager(
              mockProvider,
                mockMesher,
                mockGetComponent,
                MockGetPriorityOfChunk,
                100,
                1,
                1
              );

            var zeroId = new Vector3Int(0, 0, 0);
            var testId = new Vector3Int(1, 0, 0);
            mockAddNewChunkWithTarget(zeroId, pipeline.CompleteStage);

            pipeline.Update();

            //cid 0 should get to waiting for neighbours
            AssertChunkStages(zeroId, pipeline.FullyGenerated, pipeline.FullyGenerated, pipeline.CompleteStage);

            //The test id should have been added to the start stage as it is a neighbour of cid 0
            AssertChunkStages(testId, 0, 0, pipeline.FullyGenerated);

            //Update the target of some of the other neighbours
            pipeline.SetTarget(new Vector3Int(0, 1, 0), pipeline.CompleteStage);
            pipeline.SetTarget(new Vector3Int(0, 0, 1), pipeline.CompleteStage);


            pipeline.Update();

            //zeroID should be complete
            AssertChunkStages(zeroId, pipeline.CompleteStage, pipeline.CompleteStage, pipeline.CompleteStage);

            //Update the target of the test id
            pipeline.SetTarget(testId, pipeline.CompleteStage);

            pipeline.Update();
            pipeline.Update();

            //the test id should be scheduled for mesh
            AssertChunkStages(testId, pipeline.FullyGenerated +1, pipeline.FullyGenerated + 1, pipeline.CompleteStage);

            //Remove one of the test id's neighbours
            mockRemoveChunk(zeroId);

            pipeline.Update();

            //test id should be back in the waiting for neighbour data stage
            AssertChunkStages(testId, pipeline.FullyGenerated, pipeline.FullyGenerated, pipeline.CompleteStage);

        }

        [Test]
        public void NeighbourAndSelfRemovedWhileScheduledForMesh()
        {
            mockMesher.IsMeshDependentOnNeighbourChunks = true;
            int maxData = 100;
            pipeline = new ChunkPipelineManager(
              mockProvider,
                mockMesher,
                mockGetComponent,
                MockGetPriorityOfChunk,
                maxData,
                1,
                1
              );

            var zeroId = new Vector3Int(0, 1000, 0);
            var testId = new Vector3Int(1, 1000, 0);
            mockAddNewChunkWithTarget(zeroId, pipeline.CompleteStage);

            pipeline.Update();

            //zeroId should get to waiting for neighbours
            AssertChunkStages(zeroId, pipeline.FullyGenerated, pipeline.FullyGenerated, pipeline.CompleteStage);

            //The test id should have been added to the start stage as it is a neighbour of cid 0
            AssertChunkStages(testId, 0, 0, pipeline.FullyGenerated);

            //Update the target of some of the other neighbours
            pipeline.SetTarget(new Vector3Int(0, 1001, 0), pipeline.CompleteStage);
            pipeline.SetTarget(new Vector3Int(0, 1000, 1), pipeline.CompleteStage);


            pipeline.Update();

            //zeroID should be complete
            AssertChunkStages(zeroId, pipeline.CompleteStage, pipeline.CompleteStage, pipeline.CompleteStage);

            //Update the target of the test id
            pipeline.SetTarget(testId, pipeline.CompleteStage);

            pipeline.Update();
            pipeline.Update();

            //the test id should be scheduled for mesh
            AssertChunkStages(testId, pipeline.FullyGenerated + 1, pipeline.FullyGenerated + 1, pipeline.CompleteStage);

            //Remove one of the test id's neighbours
            mockRemoveChunk(zeroId);
            //Remove the test id itself
            mockRemoveChunk(testId);

            //Add loads of other chunks (with higher priority) to prevent the target getting back to the data stage
            for (int i = 0; i <= maxData; i++)
            {
                mockAddNewChunkWithTarget(new Vector3Int(0, 0, i), pipeline.FullyGenerated);
            }

            //Add the test id back
            mockAddNewChunkWithTarget(testId,pipeline.CompleteStage);

            //Min and max should now be 0 as the id has been removed and re-added
            AssertChunkStages(testId, 0, 0, pipeline.CompleteStage);

            pipeline.Update();

            //test id should be in the start stage, with its min and max correct
            AssertChunkStages(testId, 0, 0, pipeline.CompleteStage);

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
            Assert.AreEqual(min, pipeline.GetMinStage(id),"Min stage not as expected");
            Assert.AreEqual(max, pipeline.GetMaxStage(id),"Max stage not as expected");
            Assert.AreEqual(target, pipeline.GetTargetStage(id),"Target stage not as expected");
        }

        /// <summary>
        /// Overload for asserting that min max and target should be the same
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="target"></param>
        private void AssertChunkStages(Vector3Int id, int all)
        {
            Assert.AreEqual(all, pipeline.GetMinStage(id));
            Assert.AreEqual(all, pipeline.GetMaxStage(id));
            Assert.AreEqual(all, pipeline.GetTargetStage(id));
        }
    }
}
