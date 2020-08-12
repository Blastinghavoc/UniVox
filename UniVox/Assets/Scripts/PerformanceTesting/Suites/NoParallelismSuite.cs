namespace PerformanceTesting
{
    public class NoParallelismSuite : ProceduralSuite 
    {
        protected override PassDetails EndPass(string groupName)
        {
            provider.Parrallel = false;
            mesher.Parrallel = false;

            return base.EndPass(groupName);
        }
    }
}