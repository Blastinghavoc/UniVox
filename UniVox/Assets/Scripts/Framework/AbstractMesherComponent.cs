﻿using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Framework.Jobified;

namespace UniVox.Framework
{
    public abstract class AbstractMesherComponent : MonoBehaviour, IChunkMesher, IDisposable
    {

        public bool CullFaces { get; protected set; } = false;

        public bool Parrallel = true;

        protected VoxelTypeManager voxelTypeManager;
        protected IChunkManager chunkManager;

        /// <summary>
        /// Data to support mesh jobs
        /// </summary>
        protected NativeDirectionRotator directionRotator;
        private bool disposed = false;

        protected FrameworkEventManager eventManager;

        public virtual void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager, FrameworkEventManager eventManager)
        {
            this.voxelTypeManager = voxelTypeManager;
            this.chunkManager = chunkManager;

            directionRotator = DirectionRotatorExtensions.Create();
            this.eventManager = eventManager;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                directionRotator.Dispose();
                disposed = true;
            }
        }

        public AbstractPipelineJob<MeshDescriptor> CreateMeshJob(Vector3Int chunkID)
        {
            Profiler.BeginSample("CreateMeshJob");

            var chunkDimensions = chunkManager.ChunkDimensions;

            var chunkData = chunkManager.GetReadOnlyChunkData(chunkID);

            //Copy chunk data to native array
            Profiler.BeginSample("VoxelsToNative");
            NativeArray<VoxelTypeID> voxels = chunkData.ToNative();
            Profiler.EndSample();


            NeighbourData neighbourData = new NeighbourData();
            //Cache neighbour data          
            neighbourData = JobUtils.CacheNeighbourData(chunkID, chunkManager);


            var meshingJob = createMeshingJob(new MeshJobData(chunkDimensions.ToNative(),
                chunkManager.IncludeLighting,
                voxels,
                chunkData.NativeRotations(),
                chunkData.LightToNative(),
                neighbourData,
                voxelTypeManager.nativeMeshDatabase,
                voxelTypeManager.nativeVoxelTypeDatabase,
                Allocator.Persistent
                ));


            var indexingWrapper = new JobWrapper<SortIndicesByMaterialJob>();
            //AsDeferredJobArray takes the length etc at the time of execution, rather than now.
            indexingWrapper.job.allTriangleIndices = meshingJob.data.allTriangleIndices.AsDeferredJobArray();
            indexingWrapper.job.materialRuns = meshingJob.data.materialRuns.AsDeferredJobArray();
            indexingWrapper.job.packedRuns = new NativeList<MaterialRun>(Allocator.Persistent);
            indexingWrapper.job.packedIndices = new NativeList<int>(Allocator.Persistent);
            indexingWrapper.job.collisionMeshMaterialRunLength = meshingJob.data.collisionSubmesh.collisionMeshMaterialRunLength;


            Func<MeshDescriptor> cleanup = () =>
            {
                Profiler.BeginSample("MeshJobCleanup");
                Mesh mesh = new Mesh();

                if (meshingJob.data.vertices.Length >= ushort.MaxValue)
                {
                    //Cope with bigger meshes
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                mesh.vertices = meshingJob.data.vertices.ToArray();
                if (meshingJob.data.includeLighting)
                {
                    mesh.colors = meshingJob.data.vertexColours.ToArray();
                }
                mesh.SetUVs(0, meshingJob.data.uvs.ToArray());
                mesh.normals = meshingJob.data.normals.ToArray();

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
                meshDescriptor.collidableLengthVertices = meshingJob.data.collisionSubmesh.collisionMeshLengthVertices[0];
                meshDescriptor.collidableLengthIndices = meshingJob.data.collisionSubmesh.collisionMeshLengthTriangleIndices[0];

                //Disposal
                meshingJob.Dispose();

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
                    meshingJob.Run();
                    indexingWrapper.Run();
                    return cleanup();
                });
            }

            var meshingHandle = meshingJob.Schedule();
            var handle = indexingWrapper.Schedule(meshingHandle);

            return new PipelineUnityJob<MeshDescriptor>(handle, cleanup);

        }

        protected virtual IMeshingJob createMeshingJob(MeshJobData data)
        {
            var job = new MeshingJob();
            job.data = data;
            job.cullfaces = CullFaces;
            job.directionHelper = directionRotator;
            //TODO use allocator temp job if job can be guaranteed to take less than 4 frames
            job.nonCollidableQueue = new NativeList<MeshingJob.DoLater>(Allocator.Persistent);
            return job;
        }

        public AbstractPipelineJob<Mesh> ApplyCollisionMeshJob(Vector3Int chunkID)
        {
            return new BasicFunctionJob<Mesh>(() =>
            {
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