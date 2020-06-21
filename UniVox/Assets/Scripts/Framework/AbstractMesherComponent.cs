using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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
        public bool Burst = true;

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

        public AbstractPipelineJob<Mesh> CreateMeshJob(Vector3Int chunkID)
        {
            if (!Burst)
            {
                return new BasicFunctionJob<Mesh>(() => CreateMesh(chunkManager.GetReadOnlyChunkData(chunkID)));
            }

            Profiler.BeginSample("CreateMeshJob");

            var jobWrapper = new JobWrapper<MeshingJob<V>>();
            jobWrapper.job = new MeshingJob<V>();
            jobWrapper.job.cullfaces = IsMeshDependentOnNeighbourChunks;
            var chunkDimensions = chunkManager.ChunkDimensions;
            jobWrapper.job.dimensions = new int3(chunkDimensions.x, chunkDimensions.y, chunkDimensions.z);

            var chunkData = chunkManager.GetReadOnlyChunkData(chunkID);

            //Copy chunk data to native array
            Profiler.BeginSample("VoxelsToNative");
            NativeArray<V> voxels = chunkData.ToNative();
            Profiler.EndSample();

            jobWrapper.job.voxels = voxels;

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

            jobWrapper.job.neighbourData = neighbourData;

            jobWrapper.job.meshDatabase = voxelTypeManager.nativeMeshDatabase;
            jobWrapper.job.voxelTypeDatabase = voxelTypeManager.nativeVoxelTypeDatabase;
            jobWrapper.job.DirectionVectors = directionVectors;
            jobWrapper.job.DirectionOpposites = directionOpposites;

            jobWrapper.job.vertices = new NativeList<Vector3>(Allocator.Persistent);
            jobWrapper.job.uvs = new NativeList<Vector3>(Allocator.Persistent);
            jobWrapper.job.normals = new NativeList<Vector3>(Allocator.Persistent);
            jobWrapper.job.triangleIndices = new NativeList<int>(Allocator.Persistent);

            Func<Mesh> cleanup = () =>
            {
                Profiler.BeginSample("MeshJobCleanup");
                Mesh mesh = new Mesh();

                if (jobWrapper.job.vertices.Length >= ushort.MaxValue)
                {
                    //Cope with bigger meshes
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                mesh.vertices = jobWrapper.job.vertices.ToArray();
                mesh.SetUVs(0, jobWrapper.job.uvs.ToArray());
                mesh.normals = jobWrapper.job.normals.ToArray();
                mesh.triangles = jobWrapper.job.triangleIndices.ToArray();

                jobWrapper.job.vertices.Dispose();
                jobWrapper.job.uvs.Dispose();
                jobWrapper.job.normals.Dispose();
                jobWrapper.job.triangleIndices.Dispose();

                jobWrapper.job.voxels.Dispose();
                jobWrapper.job.neighbourData.Dispose();

                Profiler.EndSample();

                return mesh;
            };

            Profiler.EndSample();


            //Single threaded version
            //return new BasicFunctionJob<Mesh>(() => {
            //    jobWrapper.job.Run();
            //    return cleanup();
            //});

            return new PipelineUnityJob<Mesh, MeshingJob<V>>(jobWrapper, cleanup);

        }

    }

    [BurstCompile]
    public struct MeshingJob<V> : IJob
        where V:struct, IVoxelData
    {
        [ReadOnly] public bool cullfaces;
        [ReadOnly] public int3 dimensions;
        [ReadOnly] private const int numDirections = Directions.NumDirections;

        [ReadOnly] public NativeArray<V> voxels;

        [ReadOnly] public NeighbourData<V> neighbourData;

        [ReadOnly] public NativeMeshDatabase meshDatabase;

        [ReadOnly] public NativeVoxelTypeDatabase voxelTypeDatabase;

        [ReadOnly] public NativeArray<int3> DirectionVectors;
        [ReadOnly] public NativeArray<byte> DirectionOpposites;

        public NativeList<Vector3> vertices;
        public NativeList<Vector3> uvs;
        public NativeList<Vector3> normals;
        public NativeList<int> triangleIndices;

        public void Execute()
        {
            int currentIndex = 0;//Current index for indices list

            int i = 0;//current index into voxelData
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        var voxelTypeID = voxels[i].TypeID;

                        if (voxelTypeID != VoxelTypeManager.AIR_ID)
                        {            
                            AddMeshDataForVoxel(voxelTypeID,new int3(x,y,z),ref currentIndex);
                        }

                        i++;
                    }
                }
            }
        }

        private void AddMeshDataForVoxel(ushort id, int3 position, ref int currentIndex)
        {
            var meshID = meshDatabase.voxelTypeToMeshTypeMap[id];
            var faceZRange = voxelTypeDatabase.voxelTypeToZIndicesRangeMap[id];

            //Add single voxel's data
            for (int i = 0; i < numDirections; i++)
            {
                if (IncludeFace(position, i))
                {
                    AddFace(meshID, voxelTypeDatabase.zIndicesPerFace[faceZRange.start+i], i, position,ref currentIndex);
                }
            }
        }

        private void AddFace(int meshID, float uvZ, int direction,int3 position,ref int currentIndex)
        {
            var meshRange = meshDatabase.meshTypeRanges[meshID];

            var usedNodesSlice = meshDatabase.nodesUsedByFaces[meshRange.start+direction];

            //Add all the nodes used by this face
            for (int i = usedNodesSlice.start; i < usedNodesSlice.end; i++)
            {
                var node = meshDatabase.allMeshNodes[i];
                vertices.Add(node.vertex + position);
                uvs.Add(new float3(node.uv, uvZ));
                normals.Add(node.normal);
            }

            //Add the triangleIndices used by this face

            var relativeTrianglesSlice = meshDatabase.relativeTrianglesByFaces[meshRange.start + direction];

            for (int i = relativeTrianglesSlice.start; i < relativeTrianglesSlice.end; i++)
            {
                triangleIndices.Add(meshDatabase.allRelativeTriangles[i] + currentIndex);
            }

            //Update indexing
            currentIndex += usedNodesSlice.end - usedNodesSlice.start;
        }

        private bool IncludeFace(int3 position, int directionIndex)
        {
            if (!cullfaces)
            {
                return true;
            }          
            var directionVector = DirectionVectors[directionIndex];
            var adjacentVoxelIndex = position + directionVector;

            if (TryGetVoxelAt(adjacentVoxelIndex, out var adjacentID))
            {//If adjacent voxel is in the chunk

                return IncludeFaceOfAdjacentWithID(adjacentID, directionIndex);
            }
            else
            {
                //If adjacent voxel is in the neighbouring chunk

                var localIndexOfAdjacentVoxelInNeighbour = DirectionToIndicesInSlice(directionVector, LocalVoxelIndexOfPosition(adjacentVoxelIndex));

                var neighbourChunkData = neighbourData[directionIndex];

                var neighbourDimensions = DirectionToIndicesInSlice(directionVector, dimensions);

                var flattenedIndex = Utils.Helper.MultiIndexToFlat(localIndexOfAdjacentVoxelInNeighbour.x, localIndexOfAdjacentVoxelInNeighbour.y, neighbourDimensions);

                return IncludeFaceOfAdjacentWithID(neighbourChunkData[flattenedIndex].TypeID, directionIndex);
            }
        }

        private bool IncludeFaceOfAdjacentWithID(ushort voxelTypeID, int direction)
        {
            if (voxelTypeID == VoxelTypeManager.AIR_ID)
            {
                //Include the face if the adjacent voxel is air
                return true;
            }
            var meshID = meshDatabase.voxelTypeToMeshTypeMap[voxelTypeID];
            var meshRange = meshDatabase.meshTypeRanges[meshID];
            var faceIsSolid = meshDatabase.isFaceSolid[meshRange.start + DirectionOpposites[direction]];

            //Exclude this face if adjacent face is solid
            return !faceIsSolid;
        }

        private bool TryGetVoxelAt(int3 pos, out ushort voxelId) 
        {
            if (pos.x >= 0 && pos.x < dimensions.x &&
                pos.y >= 0 && pos.y < dimensions.y &&
                pos.z >= 0 && pos.z < dimensions.z)
            {
                voxelId = voxels[Utils.Helper.MultiIndexToFlat(pos.x, pos.y, pos.z, dimensions)].TypeID;
                return true;
            }
            voxelId = VoxelTypeManager.AIR_ID;
            return false;
        }

        /// <summary>
        /// Verbose version of ChunkManager's method with the same name.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        private int3 LocalVoxelIndexOfPosition(int3 position)
        {
            var remainder = position % dimensions;

            if (remainder.x < 0)
            {
                remainder.x += dimensions.x;
            }

            if (remainder.y < 0)
            {
                remainder.y += dimensions.y;
            }

            if (remainder.z < 0)
            {
                remainder.z += dimensions.z;
            }

            return remainder;
        }

        /// <summary>
        /// Takes a direction vector, assumed to have just one non-zero element,
        /// and returns the values of fullCoords for the dimensions that are
        /// zero in the direction. I.e, projects fullCoords to 2D in the relevant direction
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        private int2 DirectionToIndicesInSlice(int3 direction,int3 fullCoords) 
        {
            if (direction.x != 0)
            {
                return new int2(fullCoords.y, fullCoords.z);
            }
            if (direction.y != 0)
            {
                return new int2(fullCoords.x, fullCoords.z);
            }
            //if (direction.z != 0)
            return new int2(fullCoords.x, fullCoords.y);

        }
    }
}