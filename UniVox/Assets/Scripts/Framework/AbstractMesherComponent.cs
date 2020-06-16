using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using TypeData = UniVox.Framework.VoxelTypeManager.VoxelTypeData;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Framework
{
    public abstract class AbstractMesherComponent<V> : MonoBehaviour, IChunkMesher<V>
        where V : IVoxelData
    {

        public bool IsMeshDependentOnNeighbourChunks { get; protected set; } = false;

        protected VoxelTypeManager voxelTypeManager;
        protected AbstractChunkManager<V> chunkManager;

        public virtual void Initialise(VoxelTypeManager voxelTypeManager, AbstractChunkManager<V> chunkManager)
        {
            this.voxelTypeManager = voxelTypeManager;
            this.chunkManager = chunkManager;
        }

        public Mesh CreateMesh(IChunkData<V> chunk)
        {
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

            return mesh;

        }

        protected virtual void AddMeshDataForVoxel(IChunkData<V> chunk, TypeData voxelTypeData, Vector3Int position, List<Vector3> vertices, List<Vector3> uvs, List<Vector3> normals, List<int> indices, ref int currentIndex, List<ReadOnlyChunkData<V>> neighbourData)
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

        public AbstractPipelineJob<Mesh> CreateMeshJob(Vector3Int chunkID)
        {
            return new BasicFunctionJob<Mesh>(() => CreateMesh(chunkManager.GetReadOnlyChunkData(chunkID)));
        }
    }
}