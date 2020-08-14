using System;
using Unity.Mathematics;
using UnityEngine;

public static class VectorExtensions
{
    #region Vector3Int
    /// <summary>
    /// Returns true if all elements of the vector satisfy the condition
    /// </summary>
    /// <param name=""></param>
    /// <param name="condition"></param>
    /// <returns></returns>
    public static bool All(this Vector3Int v, Func<int, bool> condition)
    {
        return condition(v.x) && condition(v.y) && condition(v.z);
    }

    /// <summary>
    /// Returns true if all elements of the vector satisfy the condition,
    /// with the elements of the second vector as arguments.
    /// </summary>
    /// <param name=""></param>
    /// <param name="condition"></param>
    /// <returns></returns>
    public static bool All(this Vector3Int v1, Func<int, int, bool> condition, Vector3Int v2)
    {
        return condition(v1.x, v2.x) && condition(v1.y, v2.y) && condition(v1.z, v2.z);
    }

    /// <summary>
    /// Returns true iif any of the elements of the vector satisfy the condition
    /// </summary>
    /// <param name="v"></param>
    /// <param name="condition"></param>
    /// <returns></returns>
    public static bool Any(this Vector3Int v, Func<int, bool> condition)
    {
        return condition(v.x) || condition(v.y) || condition(v.z);
    }

    /// <summary>
    /// Returns true iff any of the elements of the vector satisfy the condition,
    /// with the elements of the second vector as arguments.
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="condition"></param>
    /// <param name="v2"></param>
    /// <returns></returns>
    public static bool Any(this Vector3Int v1, Func<int, int, bool> condition, Vector3Int v2)
    {
        return condition(v1.x, v2.x) || condition(v1.y, v2.y) || condition(v1.z, v2.z);
    }

    /// <summary>
    /// Returns the result of applying the operation to each element of the 
    /// input vector.
    /// </summary>
    /// <param name=""></param>
    /// <param name="operation"></param>
    /// <returns></returns>
    public static Vector3Int ElementWise(this Vector3Int v, Func<int, int> operation)
    {
        return new Vector3Int(operation(v.x), operation(v.y), operation(v.z));
    }

    /// <summary>
    /// Returns the result of applying the element-wise operation to the vector,
    /// with v2 as the second input to the operation.
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="operation"></param>
    /// <returns></returns>
    public static Vector3Int ElementWise(this Vector3Int v1, Func<int, int, int> operation, Vector3Int v2)
    {
        return new Vector3Int(operation(v1.x, v2.x), operation(v1.y, v2.y), operation(v1.z, v2.z));
    }

    public static int Dot(this Vector3Int v1, Vector3Int v2)
    {
        return (v1.x * v2.x) + (v1.y * v2.y) + (v1.z * v2.z);
    }

    /// <summary>
    /// Used to unpack a Vector3Int
    /// </summary>
    /// <param name="v"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    public static void Deconstruct(this Vector3Int v, out int x, out int y, out int z)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public static int ManhattanMagnitude(this Vector3Int v)
    {
        return Math.Abs(v.x) + Math.Abs(v.y) + Math.Abs(v.z);
    }

    #endregion

    #region Vector3

    /// <summary>
    /// Returns the result of applying the operation to each element of the 
    /// input vector.
    /// </summary>
    /// <param name=""></param>
    /// <param name="operation"></param>
    /// <returns></returns>
    public static Vector3 ElementWise(this Vector3 v, Func<float, float> operation)
    {
        return new Vector3(operation(v.x), operation(v.y), operation(v.z));
    }

    /// <summary>
    /// Returns the result of applying the element-wise operation to the vector,
    /// with v2 as the second input to the operation.
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="operation"></param>
    /// <returns></returns>
    public static Vector3 ElementWise(this Vector3 v1, Func<float, float, float> operation, Vector3 v2)
    {
        return new Vector3(operation(v1.x, v2.x), operation(v1.y, v2.y), operation(v1.z, v2.z));
    }

    /// <summary>
    /// Cast to int vector
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    public static Vector3Int ToInt(this Vector3 v)
    {
        return new Vector3Int((int)v.x, (int)v.y, (int)v.z);
    }
    #endregion


    #region Jobified

    public static int3 ToNative(this Vector3Int v)
    {
        return new int3(v.x, v.y, v.z);
    }

    public static Vector3Int ToBasic(this int3 v)
    {
        return new Vector3Int(v.x, v.y, v.z);
    }

    public static float3 ToNative(this Vector3 v)
    {
        return new float3(v.x, v.y, v.z);
    }

    public static Vector3 ToBasic(this float3 v)
    {
        return new Vector3(v.x, v.y, v.z);
    }

    #endregion
}
