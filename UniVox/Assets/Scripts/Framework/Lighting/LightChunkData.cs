using UnityEngine;
using static Utils.Helpers;

namespace UniVox.Framework.Lighting
{
    public class LightChunkData 
    { 
        private LightValue[] lightValues;
        private int dx;
        private int dxdy;

        public LightValue this[int x, int y, int z] 
        {
            get { return lightValues[MultiIndexToFlat(x, y, z, dx, dxdy)]; }
            set { lightValues[MultiIndexToFlat(x, y, z, dx, dxdy)] = value; }
        }

        public LightChunkData(Vector3Int dimensions) 
        {
            dx = dimensions.x;
            dxdy = dimensions.x * dimensions.y;
            lightValues = new LightValue[dimensions.x * dimensions.y * dimensions.z];
        }
    }
}