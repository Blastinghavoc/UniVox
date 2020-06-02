using UnityEngine;
using System.Collections;

public struct BasicVoxelData : IVoxelData
{
    public ushort TypeID { get; set; }
    public BasicVoxelData(ushort typeID)
    {
        TypeID = typeID;
    }
}
