using UnityEngine;
using System.Collections;

namespace UniVox.Framework
{
    [CreateAssetMenu(menuName = "UniVox/VoxelType")]
    public class SOVoxelTypeDefinition : ScriptableObject
    {
        public string DisplayName;
        public Texture2D[] FaceTextures = new Texture2D[Directions.NumDirections];
        public SOMeshDefinition meshDefinition;
        public Material material;
        //Can the voxel type be moved through (examples where true would include liquids and decorative plants)
        public bool isPassable;
        public SORotationConfiguration rotationConfiguration = null;
    }
}