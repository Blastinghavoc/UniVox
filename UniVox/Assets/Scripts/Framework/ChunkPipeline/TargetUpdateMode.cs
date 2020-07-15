namespace UniVox.Framework.ChunkPipeline
{
    public enum TargetUpdateMode 
    { 
        any,//Target may increase or decrease
        upgradeOnly,//Target may only go up
        downgradeOnly//Target may only go down
    }

    public static class TargetUpdateModeExtensions 
    {
        public static bool allowsUpgrade(this TargetUpdateMode mode) 
        {
            return mode == TargetUpdateMode.any || mode == TargetUpdateMode.upgradeOnly;
        }

        public static bool allowsDowngrade(this TargetUpdateMode mode)
        {
            return mode == TargetUpdateMode.any || mode == TargetUpdateMode.downgradeOnly;
        }
    }
}