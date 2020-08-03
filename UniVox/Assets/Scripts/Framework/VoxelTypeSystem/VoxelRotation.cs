using System;
using Unity.Burst;

namespace UniVox.Framework
{
    /// <summary>
    /// Struct encoding Voxel Rotations in 4 bits, allowing for
    /// 4 angles of rotation about the Y and X planes, or 16 total rotations.
    /// </summary>
    [BurstCompile]
    [Serializable]
    public struct VoxelRotation: IEquatable<VoxelRotation> 
    {
        //first 6 bits encode values 00xxyyzz
        private byte val;

        //Rotation around X axis
        public int x { get => (val>>4)&0x3; set => val = (byte)((val & 0xF) | value << 4); }

        //Rotation around Y axis
        public int y { get => (val>>2)&0x3; set => val = (byte)((val & 0x33) | value << 2); }

        //Rotation around Z axis
        public int z { get => val & 0x3; set => val = (byte)((val & 0x3C) | value); }

        public bool isBlank { get => (val & 0x3F) == 0; }

        public int this[int axis] 
        {
            get {
                switch (axis)
                {
                    case 0:
                        return x;
                    case 1:
                        return y;
                    case 2:
                        return z;
                    default:
                        throw new IndexOutOfRangeException($"Voxel rotation has no element {axis}");
                }
            }

            set 
            {
                switch (axis)
                {
                    case 0:
                        x = value;
                        break;
                    case 1:
                        y = value;
                        break;
                    case 2:
                        z = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException($"Voxel rotation has no element {axis}");
                }
            }
        }

        public bool Equals(VoxelRotation other)
        {
            return (val & 0x3F) == (other.val & 0x3F);
        }

        public override string ToString()
        {
            return $"({x},{y},{z})";
        }

        public VoxelRotation Inverse() 
        {
            VoxelRotation rot = this;
            rot.x = (4 - x) % 4;
            rot.y = (4 - y) % 4;
            rot.z = (4 - z) % 4;
            return rot;
        }
    }
    

}