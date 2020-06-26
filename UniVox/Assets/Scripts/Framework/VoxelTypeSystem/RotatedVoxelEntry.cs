using Unity.Burst;

namespace UniVox.Framework
{
    [BurstCompile]
    public struct RotatedVoxelEntry 
    {
        public int flatIndex;
        public VoxelRotation rotation;
    }

}