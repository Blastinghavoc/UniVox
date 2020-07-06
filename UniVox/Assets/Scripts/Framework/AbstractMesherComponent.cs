using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Framework.Jobified;
using Utils;
using TypeData = UniVox.Framework.VoxelTypeManager.VoxelTypeData;

namespace UniVox.Framework
{
    public abstract class AbstractMesherComponent : MonoBehaviour, IChunkMesher, IDisposable
    {

        public bool IsMeshDependentOnNeighbourChunks { get; protected set; } = false;

        //TODO remove, testing only
        public bool Parrallel = true;

        protected VoxelTypeManager voxelTypeManager;
        protected IChunkManager chunkManager;

        /// <summary>
        /// Data to support mesh jobs
        /// </summary>
        protected NativeDirectionHelper directionHelper;
        private bool disposed = false;

        protected FrameworkEventManager eventManager;

        public virtual void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager, FrameworkEventManager eventManager)
        {
            this.voxelTypeManager = voxelTypeManager;
            this.chunkManager = chunkManager;

            directionHelper = DirectionHelperExtensions.Create();
            this.eventManager = eventManager;
        }

        public void Dispose() 
        {
            if (!disposed)
            {
                directionHelper.Dispose();
                disposed = true;
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public virtual AbstractPipelineJob<MeshDescriptor> CreateMeshJob(Vector3Int chunkID)
        {
            Profiler.BeginSample("CreateMeshJob");

            var meshingWrapper = new JobWrapper<MeshingJob>();
            meshingWrapper.job = new MeshingJob();
            meshingWrapper.job.cullfaces = IsMeshDependentOnNeighbourChunks;
            var chunkDimensions = chunkManager.ChunkDimensions;
            meshingWrapper.job.dimensions = new int3(chunkDimensions.x, chunkDimensions.y, chunkDimensions.z);

            var chunkData = chunkManager.GetReadOnlyChunkData(chunkID);

            //Copy chunk data to native array
            Profiler.BeginSample("VoxelsToNative");
            NativeArray<VoxelTypeID> voxels = chunkData.ToNative();
            Profiler.EndSample();

            meshingWrapper.job.voxels = voxels;
            meshingWrapper.job.rotatedVoxels = chunkData.NativeRotations();

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
                            $"Cause: {e.Message}",e);
                    }
                }
            }
            else
            {
                //Initialise neighbour data with small blank arrays if not needed
                for (int i = 0; i < Directions.NumDirections; i++)
                {
                    neighbourData.Add(i, new NativeArray<VoxelTypeID>(1,Allocator.Persistent));
                }
            }
            Profiler.EndSample();

            meshingWrapper.job.neighbourData = neighbourData;

            meshingWrapper.job.meshDatabase = voxelTypeManager.nativeMeshDatabase;
            meshingWrapper.job.voxelTypeDatabase = voxelTypeManager.nativeVoxelTypeDatabase;
            meshingWrapper.job.directionHelper = directionHelper;

            meshingWrapper.job.vertices = new NativeList<Vector3>(Allocator.Persistent);
            meshingWrapper.job.uvs = new NativeList<Vector3>(Allocator.Persistent);
            meshingWrapper.job.normals = new NativeList<Vector3>(Allocator.Persistent);
            meshingWrapper.job.allTriangleIndices = new NativeList<int>(Allocator.Persistent);
            meshingWrapper.job.materialRuns = new NativeList<MaterialRun>(Allocator.Persistent);

            meshingWrapper.job.collisionMeshLengthVertices = new NativeList<int>(Allocator.Persistent);
            meshingWrapper.job.collisionMeshLengthTriangleIndices = new NativeList<int>(Allocator.Persistent);
            meshingWrapper.job.collisionMeshMaterialRunLength = new NativeList<int>(Allocator.Persistent);

            var indexingWrapper = new JobWrapper<SortIndicesByMaterial>();
            //AsDeferredJobArray takes the length etc at the time of execution, rather than now.
            indexingWrapper.job.allTriangleIndices = meshingWrapper.job.allTriangleIndices.AsDeferredJobArray();
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

                //mesh.SetTriangles()

                //mesh.triangles = meshingWrapper.job.allTriangleIndices.ToArray();

                meshingWrapper.job.vertices.Dispose();
                meshingWrapper.job.uvs.Dispose();
                meshingWrapper.job.normals.Dispose();
                meshingWrapper.job.allTriangleIndices.Dispose();
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

        public AbstractPipelineJob<Mesh> ApplyCollisionMeshJob(Vector3Int chunkID)
        {
            return new BasicFunctionJob<Mesh>(() => {
                Profiler.BeginSample("ApplyingCollisionMesh");

                var desc = chunkManager.GetMeshDescriptor(chunkID);
                Mesh collisionMesh;
                if (desc.collidableLengthVertices < desc.mesh.vertices.Length)
                {
                    collisionMesh = new Mesh();
                    var verts = new Vector3[desc.collidableLengthVertices];
                    Array.Copy(desc.mesh.vertices, verts, desc.collidableLengthVertices);
                    var indices = new int[desc.collidableLengthIndices];
                    Array.Copy(desc.mesh.triangles, indices, desc.collidableLengthIndices);
                    collisionMesh.vertices = verts;
                    collisionMesh.triangles = indices;                    
                }
                else
                {
                    collisionMesh = desc.mesh;
                }

                Profiler.EndSample();
                return collisionMesh;
            }
            );
        }
    }

}