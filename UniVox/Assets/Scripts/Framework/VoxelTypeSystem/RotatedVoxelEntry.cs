using Unity.Burst;

namespace UniVox.Framework
{
    [BurstCompile]
    [System.Serializable]
    public struct RotatedVoxelEntry
    {
        public int flatIndex;
        public VoxelRotation rotation;
    }

}