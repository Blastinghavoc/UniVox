namespace UniVox.Framework.Serialisation
{
    [System.Serializable]
    public class ChunkSaveData : ISaveData
    {
        public VoxelTypeID[] voxels;
        public RotatedVoxelEntry[] rotatedEntries;
    }
}