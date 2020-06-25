using Unity.Burst;

namespace UniVox.Framework
{
    /// <summary>
    /// Struct encoding Voxel Rotations in 4 bits, allowing for
    /// 4 angles of rotation about the Y and X planes, or 16 total rotations.
    /// </summary>
    [BurstCompile]
    public struct VoxelRotation 
    {
        //first 4 bits encode values 0000xxyy
        private byte val;

        public VoxelRotation(int y, int x) 
        {
            val = 0;
            this.y = y;
            this.x = x;
        }

        //Rotation around Y axis
        public int y { get => val & 0x3; set => val = (byte)((val & 0xc) | value); }  

        //Rotation around X axis
        public int x { get => (val>>2)&0x3; set => val = (byte)((val & 0x3) | value << 2); }

    }

    

}