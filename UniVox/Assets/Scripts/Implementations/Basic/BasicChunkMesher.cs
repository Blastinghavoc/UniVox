using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BasicChunkMesher : IChunkMesher<BasicChunkData, BasicVoxelData>
{
    public Mesh CreateMesh(BasicChunkData chunk)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> indices = new List<int>();

        var voxelDefinition = new CubeMeshDefinition();

        for (int z = 0; z < chunk.Dimensions.z; z++)
        {
            for (int y = 0; y < chunk.Dimensions.y; y++)
            {
                for (int x = 0; x < chunk.Dimensions.x; x++)
                {
                    var voxelTypeID = chunk[x, y, z].TypeID;

                    //TODO voxel type manager to lookup voxel type and get corresponding voxel definition
                    if (voxelTypeID == 0)
                    {
                        continue;
                    }

                    foreach (var vertex in voxelDefinition.Vertices)
                    {
                        //TODO adjust for position
                        vertices.Add(vertex);
                        uvs.Add(Vector2.zero);//TODO proper texture coords
                    }

                    foreach (var indexList in voxelDefinition.FaceIndices)
                    {
                        indices.AddRange(indexList);
                    }
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = indices.ToArray();

        mesh.RecalculateNormals();

        return mesh;
    }
}
