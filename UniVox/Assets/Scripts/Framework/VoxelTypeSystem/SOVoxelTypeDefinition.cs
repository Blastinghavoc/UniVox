using UnityEngine;
using System.Collections;
using Unity.Mathematics;

namespace UniVox.Framework
{
    [CreateAssetMenu(menuName = "UniVox/VoxelType")]
    public class SOVoxelTypeDefinition : ScriptableObject
    {
        public string DisplayName;
        public Texture2D[] FaceTextures = new Texture2D[Directions.NumDirections];
        public SOMeshDefinition meshDefinition;
    }
}