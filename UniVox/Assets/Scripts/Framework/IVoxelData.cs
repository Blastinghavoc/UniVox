using UnityEngine;
using System.Collections;

/// <summary>
/// Voxel interface, requiring only that there is a voxel type ID
/// </summary>
public interface IVoxelData 
{
    ushort TypeID { get; set; }
}
