using System;
using Unity.Collections;

namespace UniVox.Implementations.ProcGen
{
    [Serializable]
    public struct OceanGenConfig : IDisposable
    {
        public NativeArray<int> oceanIDs;
        public float sealevel;
        public ushort waterID;

        public void Dispose()
        {
            oceanIDs.Dispose();
        }
    }
}
