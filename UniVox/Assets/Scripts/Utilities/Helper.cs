﻿using System;
using Unity.Mathematics;
using UnityEngine;

namespace Utils
{
    public static class Helper
    {
        #region 3D index flattening
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
            return x + dimensions.x * (y + dimensions.y * z);
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
    }
}