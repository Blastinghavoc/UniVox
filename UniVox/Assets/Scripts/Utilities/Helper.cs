using Unity.Mathematics;
using UnityEngine;

namespace Utils
{
    public static class Helper
    {
        public static int MultiIndexToFlat(int x, int y, int z,Vector3Int dimensions)
        {
            return MultiIndexToFlat(x, y, z, new int3(dimensions.x, dimensions.y, dimensions.z));
        }

        public static void FlatIndexToMulti(int flat,Vector3Int dimensions,out int x,out int y, out int z) 
        {
            FlatIndexToMulti(flat, new int3(dimensions.x, dimensions.y, dimensions.z), out x, out y, out z);
        }

        public static int MultiIndexToFlat(int x, int y, int z, int3 dimensions)
        {
            return x + dimensions.y * (y + dimensions.z * z);
        }

        public static void FlatIndexToMulti(int flat, int3 dimensions, out int x, out int y, out int z)
        {
            z = flat / dimensions.z * dimensions.y;
            var rem1 = z % (dimensions.z * dimensions.y);
            y = rem1 / dimensions.y;
            var rem2 = y % dimensions.y;
            x = rem2;
        }
    }
}