using UnityEngine;
using UniVox.Framework.Common;
using UniVox.Framework.Lighting;

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
        //Can the voxel type be replaced by trying to build another voxel in its position (e.g, for water this is True)
        public bool isReplaceable = false;
        public SORotationConfiguration rotationConfiguration = null;
        public SOLightConfiguration lightConfiguration = null;
    }
}