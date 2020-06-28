using System;
using Unity.Mathematics;

namespace UniVox.Implementations.ProcGen
{
    [Serializable]
    public struct WorldSettings
    {
        public float HeightmapScale;
        public float MoistureMapScale;
        public float MaxHeightmapHeight;
        public float HeightmapExponentPositive;
        public float HeightmapExponentNegative;
        public int HeightmapYOffset;
        [NonSerialized] public float MinY;
        public bool MakeCaves;
        public float CaveThreshold;
        public float CaveScale;
        [NonSerialized] public int3 ChunkDimensions;
        [NonSerialized] public float maxPossibleHmValue;
        [NonSerialized] public float minPossibleHmValue;

        public void Initialise(float miny, int3 chunkDimensions)
        {
            MinY = miny;
            ChunkDimensions = chunkDimensions;

            ///Translate scale variables into the form needed by noise operations,
            /// i.e, invert them
            HeightmapScale = 1 / HeightmapScale;
            MoistureMapScale = 1 / MoistureMapScale;
            CaveScale = 1 / CaveScale;

            maxPossibleHmValue = MaxHeightmapHeight + HeightmapYOffset;
            minPossibleHmValue = -1 * MaxHeightmapHeight + HeightmapYOffset;
        }
    }
}