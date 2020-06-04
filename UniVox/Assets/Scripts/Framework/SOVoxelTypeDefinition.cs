using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName ="UniVox/VoxelType")]
public class SOVoxelTypeDefinition : ScriptableObject
{
    public string DisplayName;
    public Vector2[] TextureAtlasUvs = new Vector2[6];
    public VoxelMeshDefinition meshDefinition = MeshDefinitions.Cube;
}
