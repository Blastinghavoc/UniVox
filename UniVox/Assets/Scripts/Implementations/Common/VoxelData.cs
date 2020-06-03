using UnityEngine;
using System.Collections;

public struct VoxelData : IVoxelData
{
    public ushort TypeID { get; set; }
    public VoxelData(ushort typeID)
    {
        TypeID = typeID;
    }
}
