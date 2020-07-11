using UnityEngine;
using UniVox.Framework.Common;

namespace UniVox.Framework
{
    [CreateAssetMenu(menuName = "UniVox/VoxelType")]
    public class SOVoxelTypeDefinition : ScriptableObject
    {
        public string DisplayName;
        public Texture2D[] FaceTextures = new Texture2D[DirectionExtensions.numDirections];
        public SOMeshDefinition meshDefinition;
        public Material material;
        //Can the voxel type be moved through (examples where true would include liquids and decorative plants)
        public bool isPassable;
        public SORotationConfiguration rotationConfiguration = null;
    }
}