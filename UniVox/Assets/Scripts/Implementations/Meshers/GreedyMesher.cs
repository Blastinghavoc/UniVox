using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UniVox.Framework;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Framework.Jobified;
using Utils;

namespace UniVox.Implementations.Meshers
{
    public class GreedyMesher : AbstractMesherComponent 
    {
        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager, FrameworkEventManager eventManager)
        {
            base.Initialise(voxelTypeManager, chunkManager, eventManager);
            IsMeshDependentOnNeighbourChunks = true;
        }

        public override AbstractPipelineJob<MeshDescriptor> CreateMeshJob(Vector3Int chunkID)
        {
            Profiler.BeginSample("CreateMeshJobGreedy");

            var chunkData = chunkManager.GetReadOnlyChunkData(chunkID);

            //Copy chunk data to native array
            Profiler.BeginSample("VoxelsToNative");
            NativeArray<VoxelTypeID> voxels = chunkData.ToNative();
            Profiler.EndSample();


            NeighbourData neighbourData = new NeighbourData();
            //Cache neighbour data if necessary

            Profiler.BeginSample("CacheNeighbourData");
            if (IsMeshDependentOnNeighbourChunks)
            {
                for (int i = 0; i < Directions.NumDirections; i++)
                {
                    var neighbourID = chunkData.ChunkID + Directions.IntVectors[i];
                    try
                    {
                        var neighbour = chunkManager.GetReadOnlyChunkData(neighbourID);
                        neighbourData.Add(i, neighbour.BorderToNative(Directions.Oposite[i]));
                    }
                    catch (Exception e)
                    {
                        var (managerHad, pipelinehad) = chunkManager.ContainsChunkID(chunkID);
                        throw new Exception($"Failed to get neighbour data for chunk {chunkID}." +
                            $"Manager had this chunk = {managerHad}, pipeline had it = {pipelinehad}." +
                            $"Cause: {e.Message}", e);
                    }
                }
            }
            else
            {
                //Initialise neighbour data with small blank arrays if not needed
                for (int i = 0; i < Directions.NumDirections; i++)
                {
                    neighbourData.Add(i, new NativeArray<VoxelTypeID>(1, Allocator.Persistent));
                }
            }
            Profiler.EndSample();

            var meshingWrapper = new JobWrapper<GreedyMeshingJob>();
            meshingWrapper.job = new GreedyMeshingJob();
            var chunkDimensions = chunkManager.ChunkDimensions;
            meshingWrapper.job.dimensions = chunkDimensions.ToNative();
            meshingWrapper.job.voxels = voxels;
            meshingWrapper.job.rotatedVoxels = chunkData.NativeRotations();

            meshingWrapper.job.neighbourData = neighbourData;

            meshingWrapper.job.meshDatabase = voxelTypeManager.nativeMeshDatabase;
            meshingWrapper.job.voxelTypeDatabase = voxelTypeManager.nativeVoxelTypeDatabase;

            meshingWrapper.job.vertices = new NativeList<Vector3>(Allocator.Persistent);
            meshingWrapper.job.uvs = new NativeList<Vector3>(Allocator.Persistent);
            meshingWrapper.job.normals = new NativeList<Vector3>(Allocator.Persistent);
            meshingWrapper.job.elements = new NativeList<int>(Allocator.Persistent);
            meshingWrapper.job.materialRuns = new NativeList<MaterialRun>(Allocator.Persistent);

            meshingWrapper.job.collisionMeshLengthVertices = new NativeList<int>(Allocator.Persistent);
            meshingWrapper.job.collisionMeshLengthTriangleIndices = new NativeList<int>(Allocator.Persistent);
            meshingWrapper.job.collisionMeshMaterialRunLength = new NativeList<int>(Allocator.Persistent);

            var indexingWrapper = new JobWrapper<SortIndicesByMaterialJob>();
            //AsDeferredJobArray takes the length etc at the time of execution, rather than now.
            indexingWrapper.job.allTriangleIndices = meshingWrapper.job.elements.AsDeferredJobArray();
            indexingWrapper.job.materialRuns = meshingWrapper.job.materialRuns.AsDeferredJobArray();
            indexingWrapper.job.packedRuns = new NativeList<MaterialRun>(Allocator.Persistent);
            indexingWrapper.job.packedIndices = new NativeList<int>(Allocator.Persistent);
            indexingWrapper.job.collisionMeshMaterialRunLength = meshingWrapper.job.collisionMeshMaterialRunLength.AsDeferredJobArray();


            Func<MeshDescriptor> cleanup = () =>
            {
                Profiler.BeginSample("MeshJobCleanup");
                Mesh mesh = new Mesh();

                if (meshingWrapper.job.vertices.Length >= ushort.MaxValue)
                {
                    //Cope with bigger meshes
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                mesh.vertices = meshingWrapper.job.vertices.ToArray();
                mesh.SetUVs(0, meshingWrapper.job.uvs.ToArray());
                mesh.normals = meshingWrapper.job.normals.ToArray();

                mesh.subMeshCount = indexingWrapper.job.packedRuns.Length;

                MeshDescriptor meshDescriptor = new MeshDescriptor();
                meshDescriptor.materialsBySubmesh = new Material[mesh.subMeshCount];

                for (int i = 0; i < indexingWrapper.job.packedRuns.Length; i++)
                {
                    var run = indexingWrapper.job.packedRuns[i];
                    var slice = new NativeSlice<int>(indexingWrapper.job.packedIndices, run.range.start, run.range.Length);
                    mesh.SetTriangles(slice.ToArray(), i);
                    meshDescriptor.materialsBySubmesh[i] = voxelTypeManager.GetMaterial(run.materialID);
                }

                meshDescriptor.mesh = mesh;
                meshDescriptor.collidableLengthVertices = meshingWrapper.job.collisionMeshLengthVertices[0];
                meshDescriptor.collidableLengthIndices = meshingWrapper.job.collisionMeshLengthTriangleIndices[0];


                //Disposal
                meshingWrapper.job.vertices.Dispose();
                meshingWrapper.job.uvs.Dispose();
                meshingWrapper.job.normals.Dispose();
                meshingWrapper.job.elements.Dispose();
                meshingWrapper.job.materialRuns.Dispose();

                meshingWrapper.job.voxels.Dispose();
                meshingWrapper.job.rotatedVoxels.Dispose();
                meshingWrapper.job.neighbourData.Dispose();

                meshingWrapper.job.collisionMeshLengthVertices.Dispose();
                meshingWrapper.job.collisionMeshLengthTriangleIndices.Dispose();
                meshingWrapper.job.collisionMeshMaterialRunLength.Dispose();


                //Dispose of packed containers
                indexingWrapper.job.packedIndices.Dispose();
                indexingWrapper.job.packedRuns.Dispose();

                Profiler.EndSample();

                return meshDescriptor;
            };

            Profiler.EndSample();


            //Single threaded version
            if (!Parrallel)
            {
                return new BasicFunctionJob<MeshDescriptor>(() =>
                {
                    meshingWrapper.Run();
                    indexingWrapper.Run();
                    return cleanup();
                });
            }

            var meshingHandle = meshingWrapper.Schedule();
            var handle = indexingWrapper.Schedule(meshingHandle);

            return new PipelineUnityJob<MeshDescriptor>(handle, cleanup);
        }
    }
}