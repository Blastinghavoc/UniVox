using UnityEngine;
using System.Collections;
using Unity.Burst;

namespace UniVox.Framework
{
    [BurstCompile]
    public struct VoxelTypeID
    {
        private ushort value;

        public VoxelTypeID(ushort typeID)
        {
            value = typeID;
        }

        public static implicit operator ushort(VoxelTypeID id) => id.value;
        public static explicit operator VoxelTypeID(ushort val) => new VoxelTypeID(val);
    }

}