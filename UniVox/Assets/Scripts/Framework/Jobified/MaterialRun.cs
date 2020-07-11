using UniVox.Framework.Common;

namespace UniVox.Framework.Jobified
{
    ///Used for run-length encoding the material runs in the triangle indices
    ///for a meshing job
    public struct MaterialRun
    {
        public ushort materialID;
        public StartEndRange range;
    }
}