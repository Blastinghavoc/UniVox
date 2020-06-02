using UnityEngine;
using System.Collections;

[System.Serializable]
public class VoxelMeshDefinition
{
    public Vector3[] Vertices { get; protected set; }
    public int[][] FaceIndices { get; protected set; }
}
