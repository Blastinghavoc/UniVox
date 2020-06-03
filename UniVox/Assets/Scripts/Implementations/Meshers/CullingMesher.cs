using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CullingMesher : AbstractMesherComponent<AbstractChunkData, VoxelData>
{
    protected override void AddMeshDataForVoxel(AbstractChunkData chunk, ushort voxelTypeID, Vector3Int position, List<Vector3> vertices, List<Vector2> uvs, List<Vector3> normals, List<int> indices, ref int currentIndex, ref Vector3 positionOffset)
    {
        var voxelDefinition = MeshDefinitions.Cube;

        //TODO voxel type manager to lookup voxel type and get corresponding voxel definition
        if (voxelTypeID != 0)
        {
            //Add single voxel's data
            for (int i = 0; i < voxelDefinition.Faces.Length; i++)
            {
                if (chunk.TryGetVoxelAtLocalCoordinates(position + Directions.IntVectors[i], out VoxelData adjacent))
                {
                    if (adjacent.TypeID != 0)
                    {
                        //TODO better handling of TypeID (i.e look up whether that type is solid)
                        //Skip face adjacent to solid block
                        continue;
                    }
                }

                var face = voxelDefinition.Faces[i];

                foreach (var vertexID in face.UsedVertices)
                {
                    vertices.Add(voxelDefinition.AllVertices[vertexID] + positionOffset);
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
    }
}
