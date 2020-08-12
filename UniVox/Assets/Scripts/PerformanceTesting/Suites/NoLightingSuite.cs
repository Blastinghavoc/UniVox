namespace PerformanceTesting
{
    public class NoLightingSuite : ProceduralSuite 
    {
        protected override PassDetails EndPass(string groupName)
        {
            chunkManager.SetIncludeLighting(false);

            return base.EndPass(groupName);
        }
    }
}