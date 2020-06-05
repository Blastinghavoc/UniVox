using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using TypeData = VoxelTypeManager.VoxelTypeData;

public abstract class AbstractMesherComponent<ChunkDataType, VoxelDataType> : MonoBehaviour, IChunkMesher<ChunkDataType, VoxelDataType> 
    where ChunkDataType: IChunkData<VoxelDataType>
    where VoxelDataType:IVoxelData
{

    protected VoxelTypeManager voxelTypeManager;

    public virtual void Initialise(VoxelTypeManager voxelTypeManager) 
    {
        this.voxelTypeManager = voxelTypeManager;
    }

    public Mesh CreateMesh(ChunkDataType chunk) {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> uvs = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> indices = new List<int>();

        int currentIndex = 0;

        Vector3 positionOffset = Vector3.zero;

        for (int x = 0; x < chunk.Dimensions.x; x++)
        {
            positionOffset.y = 0;//Reset offset before each vertical loop
            for (int y = 0; y < chunk.Dimensions.y; y++)
            {
                positionOffset.z = 0;//Reset offset before each horizontal loop
                for (int z = 0; z < chunk.Dimensions.z; z++)
                {
                    var voxelTypeID = chunk[x, y, z].TypeID;

                    if (voxelTypeID == VoxelTypeManager.AIR_ID)
                    {
                        continue;
                    }

                    var typeData = voxelTypeManager.GetData(voxelTypeID);

                    AddMeshDataForVoxel(chunk, typeData, new Vector3Int(x,y,z),vertices,uvs,normals,indices,ref currentIndex,ref positionOffset);
                    

                    positionOffset.z++;
                }
                positionOffset.y++;
            }
            positionOffset.x++;
        }

        Mesh mesh = new Mesh();

        if (vertices.Count >= ushort.MaxValue)
        {
            //Cope with bigger meshes
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.vertices = vertices.ToArray();
        mesh.SetUVs(0,uvs.ToArray());
        mesh.normals = normals.ToArray();
        mesh.triangles = indices.ToArray();

        //Debug.Log($"Generated mesh with {vertices.Count} vertices and {indices.Count/3} triangles");

        return mesh;

    }

    protected virtual void AddMeshDataForVoxel(ChunkDataType chunk, TypeData voxelTypeData, Vector3Int position, List<Vector3> vertices, List<Vector3> uvs, List<Vector3> normals, List<int> indices, ref int currentIndex, ref Vector3 positionOffset) 
    {
        var meshDefinition = voxelTypeData.definition.meshDefinition;
        ref var faceZs = ref voxelTypeData.zIndicesPerFace;

        //Add single voxel's data
        for (int i = 0; i < meshDefinition.Faces.Length; i++)
        {
            if (IncludeFace(chunk,position,i))
            {
                AddFace(meshDefinition,ref faceZs, i,vertices,uvs,normals,indices,ref currentIndex,ref positionOffset);
            }
        }
    }

    protected virtual bool IncludeFace(ChunkDataType chunk, Vector3Int position,int direction) 
    {
        return true;
    }

    protected void AddFace(SOMeshDefinition meshDefinition, ref float[] zIndicesPerFace, int direction, List<Vector3> vertices, List<Vector3> uvs, List<Vector3> normals, List<int> indices, ref int currentIndex, ref Vector3 positionOffset) 
    {
        var face = meshDefinition.Faces[direction];

        foreach (var vertexID in face.UsedVertices)
        {
            vertices.Add(meshDefinition.AllVertices[vertexID] + positionOffset);
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
}
