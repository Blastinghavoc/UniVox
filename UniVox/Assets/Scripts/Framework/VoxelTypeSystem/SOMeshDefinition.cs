using UnityEngine;
using UniVox.Framework.Common;

namespace UniVox.Framework
{
    [CreateAssetMenu(menuName = "UniVox/VoxelMesh")]
    public class SOMeshDefinition : ScriptableObject
    {
        public bool includeBackfaces = false;
        //All positional vertices in the mesh
        public Vector3[] AllVertices;
        //All UVs in the mesh
        public Vector2[] AllUvs;
        //All Normals in the mesh
        public Vector3[] AllNormals;
        //Definitions for each face
        public VoxelFaceDefinition[] Faces = new VoxelFaceDefinition[DirectionExtensions.numDirections];
    }
}