using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Framework.Jobified;
using TypeData = UniVox.Framework.VoxelTypeManager.VoxelTypeData;

namespace UniVox.Framework
{
    public abstract class AbstractMesherComponent<V> : MonoBehaviour, IChunkMesher<V>, IDisposable
        where V : struct, IVoxelData
    {

        public bool IsMeshDependentOnNeighbourChunks { get; protected set; } = false;

        //TODO remove, testing only
        public bool Parrallel = true;

        protected VoxelTypeManager voxelTypeManager;
        protected AbstractChunkManager<V> chunkManager;

        /// <summary>
        /// Data to support mesh jobs
        /// </summary>
        protected NativeArray<int3> directionVectors;
        protected NativeArray<byte> directionOpposites;
        private bool disposed = false;

        public virtual void Initialise(VoxelTypeManager voxelTypeManager, AbstractChunkManager<V> chunkManager)
        {
            this.voxelTypeManager = voxelTypeManager;
            this.chunkManager = chunkManager;

            directionVectors = new NativeArray<int3>(Directions.NumDirections, Allocator.Persistent);
            for (int i = 0; i < Directions.NumDirections; i++)
            {
                var vec = Directions.IntVectors[i];
                directionVectors[i] = vec.ToBurstable();
            }

            directionOpposites = new NativeArray<byte>(Directions.Oposite, Allocator.Persistent);
        }

        public void Dispose() 
        {
            if (!disposed)
            {
                directionVectors.SmartDispose();
                directionOpposites.SmartDispose();
                disposed = true;
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        #region Deprecated

        public Mesh CreateMesh(IChunkData<V> chunk)
        {
            Profiler.BeginSample("CreateMesh");
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> uvs = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> indices = new List<int>();

            List< ReadOnlyChunkData < V >> neighbourData = new List<ReadOnlyChunkData<V>>();
            //Cache neighbour data if necessary
            if (IsMeshDependentOnNeighbourChunks)
            {
                for (int i = 0; i < Directions.NumDirections; i++)
                {
                    var neighbourID = chunk.ChunkID + Directions.IntVectors[i];
                    neighbourData.Add(chunkManager.GetReadOnlyChunkData(neighbourID));
                }
            }

            int currentIndex = 0;

            for (int x = 0; x < chunk.Dimensions.x; x++)
            {
                for (int y = 0; y < chunk.Dimensions.y; y++)
                {
                    for (int z = 0; z < chunk.Dimensions.z; z++)
                    {
                        var voxelTypeID = chunk[x, y, z].TypeID;

                        if (voxelTypeID == VoxelTypeManager.AIR_ID)
                        {
                            continue;
                        }

                        var typeData = voxelTypeManager.GetData(voxelTypeID);

                        AddMeshDataForVoxel(chunk, typeData, new Vector3Int(x, y, z), vertices, uvs, normals, indices, ref currentIndex,neighbourData);

                    }
                }
            }

            Mesh mesh = new Mesh();

            if (vertices.Count >= ushort.MaxValue)
            {
                //Cope with bigger meshes
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = vertices.ToArray();
            mesh.SetUVs(0, uvs.ToArray());
            mesh.normals = normals.ToArray();
            mesh.triangles = indices.ToArray();

            //Debug.Log($"Generated mesh with {vertices.Count} vertices and {indices.Count/3} triangles");

            Profiler.EndSample();

            return mesh;

        }

        protected void AddMeshDataForVoxel(IChunkData<V> chunk, TypeData voxelTypeData, Vector3Int position, List<Vector3> vertices, List<Vector3> uvs, List<Vector3> normals, List<int> indices, ref int currentIndex, List<ReadOnlyChunkData<V>> neighbourData)
        {
            var meshDefinition = voxelTypeData.definition.meshDefinition;
            ref var faceZs = ref voxelTypeData.zIndicesPerFace;

            //Add single voxel's data
            for (int i = 0; i < meshDefinition.Faces.Length; i++)
            {
                if (IncludeFace(chunk, position, i,neighbourData))
                {
                    AddFace(meshDefinition, ref faceZs, i, vertices, uvs, normals, indices, ref currentIndex, position);
                }
            }
        }

        protected virtual bool IncludeFace(IChunkData<V> chunk, Vector3Int position, int direction, List<ReadOnlyChunkData<V>> neighbourData)
        {
            return true;
        }

        protected void AddFace(SOMeshDefinition meshDefinition, ref float[] zIndicesPerFace, int direction, List<Vector3> vertices, List<Vector3> uvs, List<Vector3> normals, List<int> indices, ref int currentIndex, Vector3Int position)
        {
            var face = meshDefinition.Faces[direction];

            foreach (var vertexID in face.UsedVertices)
            {
                vertices.Add(meshDefinition.AllVertices[vertexID] + position);
            }

            foreach (var UvID in face.UsedUvs)
            {
                var tmp = meshDefinition.AllUvs[UvID];
                uvs.Add(new Vector3(tmp.x, tmp.y, zIndicesPerFace[direction]));
            }

            foreach (var NormalID in face.UsedNormals)
            {
                normals.Add(meshDefinition.AllNormals[NormalID]);
            }

            foreach (var TriangleIndex in face.Triangles)
            {
                indices.Add(currentIndex + TriangleIndex);
            }

            //Update indexing
            currentIndex += face.UsedVertices.Length;
        }

        #endregion

        public AbstractPipelineJob<MeshDescriptor> CreateMeshJob(Vector3Int chunkID)
        {
            Profiler.BeginSample("CreateMeshJob");

            var meshingWrapper = new JobWrapper<MeshingJob<V>>();
            meshingWrapper.job = new MeshingJob<V>();
            meshingWrapper.job.cullfaces = IsMeshDependentOnNeighbourChunks;
            var chunkDimensions = chunkManager.ChunkDimensions;
            meshingWrapper.job.dimensions = new int3(chunkDimensions.x, chunkDimensions.y, chunkDimensions.z);

            var chunkData = chunkManager.GetReadOnlyChunkData(chunkID);

            //Copy chunk data to native array
            Profiler.BeginSample("VoxelsToNative");
            NativeArray<V> voxels = chunkData.ToNative();
            Profiler.EndSample();

            meshingWrapper.job.voxels = voxels;

            NeighbourData<V> neighbourData = new NeighbourData<V>();
            //Cache neighbour data if necessary

            Profiler.BeginSample("CacheNeighbourData");
            if (IsMeshDependentOnNeighbourChunks)
            {
                for (int i = 0; i < Directions.NumDirections; i++)
                {
                    var neighbourID = chunkData.ChunkID + Directions.IntVectors[i];
                    neighbourData.Add(i, chunkManager.GetReadOnlyChunkData(neighbourID).BorderToNative(Directions.Oposite[i]));
                }
            }
            else
            {
                //Initialise neighbour data with small blank arrays if not needed
                for (int i = 0; i < Directions.NumDirections; i++)
                {
                    neighbourData.Add(i, new NativeArray<V>(1,Allocator.Persistent));
                }
            }
            Profiler.EndSample();

            meshingWrapper.job.neighbourData = neighbourData;

            meshingWrapper.job.meshDatabase = voxelTypeManager.nativeMeshDatabase;
            meshingWrapper.job.voxelTypeDatabase = voxelTypeManager.nativeVoxelTypeDatabase;
            meshingWrapper.job.DirectionVectors = directionVectors;
            meshingWrapper.job.DirectionOpposites = directionOpposites;

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