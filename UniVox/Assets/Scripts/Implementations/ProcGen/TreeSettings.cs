using System;

namespace UniVox.Implementations.ProcGen
{
    [Serializable]
    public struct TreeSettings 
    {
        public float TreemapScale;
        public float TreeThreshold;

        public void Initialise() 
        {
            ///Translate scale variables into the form needed by noise operations,
            /// i.e, invert them
            TreemapScale = 1 / TreemapScale;
        }
    }
}