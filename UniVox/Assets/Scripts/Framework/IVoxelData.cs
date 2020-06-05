using UnityEngine;
using System.Collections;

namespace UniVox.Framework
{

    /// <summary>
    /// Voxel interface, requiring only that there is a voxel type ID
    /// </summary>
    public interface IVoxelData
    {
        ushort TypeID { get; set; }
    }
}