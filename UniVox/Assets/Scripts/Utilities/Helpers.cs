using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework.Common;

namespace Utils
{
    /// <summary>
    /// Plain utility functions
    /// </summary>
    public static class Helpers
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
        [BurstCompile]
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

        [BurstCompile]
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

        public static T[] Flatten<T>(this T[,,] threeD) 
        {
            var dimensions = new Vector3Int(threeD.GetLength(0), threeD.GetLength(1), threeD.GetLength(2));
            var result = new T[threeD.Length];
            int i = 0;
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        result[i] = threeD[x, y, z];

                        i++;
                    }
                }
            }
            return result;
        }


        public static IEnumerable<Vector3Int> GetNeighboursDirectOnly(Vector3Int chunkID)
        {
            foreach (var dir in DirectionExtensions.Vectors)
            {
                var neighbourID = chunkID + dir;
                yield return neighbourID;
            }
        }

        public static IEnumerable<Vector3Int> GetNeighboursIncludingDiagonal(Vector3Int chunkID)
        {
            foreach (var dir in DiagonalDirectionExtensions.Vectors)
            {
                var neighbourID = chunkID + dir;
                yield return neighbourID;
            }
        }

        /// <summary>
        /// X-Z manhattan distance "circle" (a diagonally oriented square)
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        public static IEnumerable<Vector3Int> ManhattanCircle(Vector3Int center, int radius)
        {
            for (int x = 0; x <= radius; x++)
            {
                for (int z = 0; z <= radius - x; z++)
                {
                    //by symmetry, we have the points in all 4 2d quadrants
                    yield return new Vector3Int(x + center.x, center.y, z + center.z);

                    if (z != -z)
                    {
                        yield return new Vector3Int(x + center.x, center.y, -z + center.z);
                    }
                    if (x != -x)
                    {
                        yield return new Vector3Int(-x + center.x, center.y, z + center.z);
                        if (z != -z)
                        {
                            yield return new Vector3Int(-x + center.x, center.y, -z + center.z);
                        }
                    }

                }
            }
        }


        public static IEnumerable<Vector3Int> CuboidalArea(Vector3Int center, Vector3Int endRadii) 
        {
            for (int x = 0; x <= endRadii.x; x++)
            {
                for (int y = 0; y <= endRadii.y; y++)
                {
                    for (int z = 0; z <= endRadii.z; z++)
                    {
                        foreach (var point in AllPointsOfSymmetry3D(center, x, y, z))
                        {
                            yield return point;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// As above, but excludes all points that are not
        /// at least within the start radii
        /// </summary>
        /// <param name="center"></param>
        /// <param name="endRadii"></param>
        /// <param name="startRadii"></param>
        /// <returns></returns>
        public static IEnumerable<Vector3Int> CuboidalArea(Vector3Int center, Vector3Int endRadii, Vector3Int startRadii)
        {

            for (int x = 0; x <= endRadii.x; x++)
            {
                //Do all values of Y that are inside the exclusion radii
                for (int y = 0; y < startRadii.y; y++)
                {
                    if (x < startRadii.x)
                    {
                        //Neither x or y are valid, so z must be
                        for (int z = startRadii.z; z <= endRadii.z; z++)
                        {
                            foreach (var point in AllPointsOfSymmetry3D(center, x, y, z))
                            {
                                yield return point;
                            }
                        }
                    }
                    else
                    {
                        //X is valid, Y is not, Z is free to range over all values
                        for (int z = 0; z <= endRadii.z; z++)
                        {
                            foreach (var point in AllPointsOfSymmetry3D(center, x, y, z))
                            {
                                yield return point;
                            }
                        }
                    }
                }

                //Do all values of Y that are outside the exclusion radii
                for (int y = startRadii.y; y <= endRadii.y; y++)
                {
                    //Y is valid, X and Z are free to range over all values    

                    for (int z = 0; z <= endRadii.z; z++)
                    {
                        foreach (var point in AllPointsOfSymmetry3D(center,x,y,z))
                        {
                            yield return point;
                        }
                    }
                }
            }
        }

        private static IEnumerable<Vector3Int> AllPointsOfSymmetry3D(Vector3Int center,int x, int y, int z) 
        {
            //By symmetry we have the points in all 8 3d octants
            yield return new Vector3Int(x + center.x, y + center.y, z + center.z);

            bool xSymm = x != -x;
            bool ySymm = y != -y;
            bool zSymm = z != -z;

            if (zSymm)
            {
                yield return new Vector3Int(x + center.x, y + center.y, -z + center.z);
            }
            if (xSymm)
            {
                yield return new Vector3Int(-x + center.x, y + center.y, z + center.z);
                if (zSymm)
                {
                    yield return new Vector3Int(-x + center.x, y + center.y, -z + center.z);
                }
            }

            if (ySymm)
            {
                yield return new Vector3Int(x + center.x, -y + center.y, z + center.z);
                if (zSymm)
                {
                    yield return new Vector3Int(x + center.x, -y + center.y, -z + center.z);
                }
                if (xSymm)
                {
                    yield return new Vector3Int(-x + center.x, -y + center.y, z + center.z);
                    if (zSymm)
                    {
                        yield return new Vector3Int(-x + center.x, -y + center.y, -z + center.z);
                    }
                }
            }
        }

        public static void Swap<T>(ref T v1, ref T v2) 
        {
            var tmp = v1;
            v1 = v2;
            v2 = tmp;
        }

    }
}