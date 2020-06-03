using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NaiveMesher : AbstractMesherComponent<AbstractChunkData, VoxelData>
{
    public override Mesh CreateMesh(AbstractChunkData chunk)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();
        List<int> indices = new List<int>();

        var voxelDefinition = MeshDefinitions.Cube;

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

                    //TODO voxel type manager to lookup voxel type and get corresponding voxel definition
                    if (voxelTypeID != 0)
                    {
                        //Add single voxel's data
                        for (int i = 0; i < voxelDefinition.Faces.Length; i++)
                        {
                            var face = voxelDefinition.Faces[i];

                            foreach (var vertexID in face.UsedVertices)
                            {
                                vertices.Add(voxelDefinition.AllVertices[vertexID]+positionOffset);
                            }
                            foreach (var UvID in face.UsedUvs) 
                            {
                                uvs.Add(voxelDefinition.AllUvs[UvID]);
                            }
                            foreach (var NormalID in face.UsedNormals)
                            {
                                normals.Add(voxelDefinition.AllNormals[NormalID]);
                            }
                            foreach (var TriangleIndex in face.Triangles)
                            {
                                indices.Add(currentIndex + TriangleIndex);
                            }

                            //Update indexing
                            currentIndex += face.UsedVertices.Length;
                        }
                    }

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

        //mesh.RecalculateNormals();

        return mesh;
    }

}
