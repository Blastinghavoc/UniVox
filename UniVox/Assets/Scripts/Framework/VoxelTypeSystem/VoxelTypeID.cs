﻿using System;
using Unity.Burst;

namespace UniVox.Framework
{
    [BurstCompile]
    [Serializable]
    public struct VoxelTypeID : IEquatable<VoxelTypeID>
    {
        public const ushort AIR_ID = 0;

        private ushort value;

        public VoxelTypeID(ushort typeID)
        {
            value = typeID;
        }

        public static implicit operator ushort(VoxelTypeID id) => id.value;
        public static explicit operator VoxelTypeID(ushort val) => new VoxelTypeID(val);

        public override string ToString()
        {
            return value.ToString();
        }

        public bool Equals(VoxelTypeID other)
        {
            return value.Equals(other.value);
        }
    }

}