using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public abstract class AbstractMesherComponent<ChunkDataType, VoxelDataType> : MonoBehaviour, IChunkMesher<ChunkDataType, VoxelDataType> 
    where ChunkDataType: IChunkData<VoxelDataType>
    where VoxelDataType:IVoxelData
{
    public Mesh CreateMesh(ChunkDataType chunk) {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();
        List<int> indices = new List<int>();

        int currentIndex = 0;

        Vector3 positionOffset = Vector3.zero;

        for (int z = 0; z < chunk.Dimensions.z; z++)
        {
            positionOffset.y = 0;//Reset offset before each vertical loop
            for (int y = 0; y < chunk.Dimensions.y; y++)
            {
                positionOffset.x = 0;//Reset offset before each horizontal loop
                for (int x = 0; x < chunk.Dimensions.x; x++)
                {
                    var voxelTypeID = chunk[x, y, z].TypeID;

                    AddMeshDataForVoxel(chunk,voxelTypeID,new Vector3Int(x,y,z),vertices,uvs,normals,indices,ref currentIndex,ref positionOffset);

                    positionOffset.x++;
                }
                positionOffset.y++;
            }
            positionOffset.z++;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.normals = normals.ToArray();
        mesh.triangles = indices.ToArray();

        Debug.Log($"Generated mesh with {vertices.Count} vertices and {indices.Count/3} triangles");

        return mesh;

    }

    protected abstract void AddMeshDataForVoxel(ChunkDataType chunk,ushort voxelTypeID,Vector3Int position, List<Vector3> vertices, List<Vector2> uvs, List<Vector3> normals, List<int> indices, ref int currentIndex, ref Vector3 positionOffset);

}
