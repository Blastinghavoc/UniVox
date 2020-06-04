using UnityEngine;
using System.Collections;

[System.Serializable]
public class VoxelMeshDefinition
{
    //All positional vertices in the mesh
    public Vector3[] AllVertices;
    //All UVs in the mesh
    public Vector2[] AllUvs;
    //All Normals in the mesh
    public Vector3[] AllNormals;
    //Definitions for each face
    public VoxelFaceDefinition[] Faces = new VoxelFaceDefinition[Directions.NumDirections];

}

[System.Serializable]
public class VoxelFaceDefinition 
{
    /// <summary>
    /// These arrays are indices into the corresponding AllX arrays of the 
    /// owning VoxelMeshDefinition
    /// </summary>
    public int[] UsedVertices;
    public int[] UsedUvs;
    public int[] UsedNormals;

    /// <summary>
    /// Triangle indices defined in terms of the UsedVertices.
    /// E.g, 0,1,2 is a triangle using vertices UsedVertices[0], UsedVertices[1] and UsedVertices[2],
    /// </summary>
    public int[] Triangles;
}
