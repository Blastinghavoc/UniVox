using System;
using Unity.Mathematics;
using UnityEngine;

namespace Utils
{
    public static class Helper
    {
        #region 3D index flattening
        public static int MultiIndexToFlat(int x, int y, int z,Vector3Int dimensions)
        {
            return MultiIndexToFlat(x, y, z, dimensions.x,dimensions.x*dimensions.y);
        }

        public static void FlatIndexToMulti(int flat,Vector3Int dimensions,out int x,out int y, out int z) 
        {
            FlatIndexToMulti(flat, new int3(dimensions.x, dimensions.y, dimensions.z), out x, out y, out z);
        }

        public static int MultiIndexToFlat(int x, int y, int z, int3 dimensions)
        {
            return MultiIndexToFlat(x, y, z, dimensions.x, dimensions.x * dimensions.y);
        }

        /// <summary>
        /// Optimized version of multiIndexToFlat assuming the dimensions have been precalculated
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="dx"></param>
        /// <param name="dxdy"></param>
        /// <returns></returns>
        public static int MultiIndexToFlat(int x, int y, int z, int dx, int dxdy) 
        {
            return x + dx*y + dxdy * z;
        }

        public static void FlatIndexToMulti(int flat, int3 dimensions, out int x, out int y, out int z)
        {
            z = flat / (dimensions.x * dimensions.y);
            flat = flat % (dimensions.x * dimensions.y);
            y = flat / dimensions.x;
            x = flat % dimensions.x;
        }
        #endregion

        #region 2D index flattening
        public static int MultiIndexToFlat(int x, int y, Vector2Int dimensions)
        {
            return MultiIndexToFlat(x, y, new int2(dimensions.x, dimensions.y));
        }

        public static void FlatIndexToMulti(int flat, Vector2Int dimensions, out int x, out int y)
        {
            FlatIndexToMulti(flat, new int2(dimensions.x, dimensions.y), out x, out y);
        }

        public static int MultiIndexToFlat(int x,int y,int2 dimensions) 
        {
            return x + dimensions.x * y;
        }

        public static void FlatIndexToMulti(int flat,int2 dimensions,out int x, out int y) 
        {
            y = flat / dimensions.x;
            x = flat % dimensions.x;
        }
        #endregion

        public static T[,,] Expand<T>(this T[] flat, Vector3Int dimensions) 
        {
            var result = new T[dimensions.x, dimensions.y, dimensions.z];
            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        result[x, y, z] = flat[i];

                        i++;
                    }
                }
            }
            return result;
        }
    }
}