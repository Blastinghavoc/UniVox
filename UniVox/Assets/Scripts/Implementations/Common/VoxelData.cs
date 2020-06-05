using UnityEngine;
using System.Collections;
using UniVox.Framework;

namespace UniVox.Implementations.Common
{
    public struct VoxelData : IVoxelData
    {
        public ushort TypeID { get; set; }

        public VoxelData(ushort typeID)
        {
            TypeID = typeID;
        }
    }
}