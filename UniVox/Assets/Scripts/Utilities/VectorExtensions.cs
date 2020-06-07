﻿using UnityEngine;
using System.Collections;
using System;

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
    public static bool All(this Vector3Int v1, Func<int,int, bool> condition,Vector3Int v2)
    {
        return condition(v1.x,v2.x) && condition(v1.y,v2.y) && condition(v1.z,v2.z);
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
    public static bool Any(this Vector3Int v1, Func<int,int,bool> condition,Vector3Int v2)
    {
        return condition(v1.x,v2.x) || condition(v1.y,v2.y) || condition(v1.z,v2.z);
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
        return new Vector3Int(operation(v.x),operation(v.y),operation(v.z));
    }

    /// <summary>
    /// Returns the result of applying the element-wise operation to the vector,
    /// with v2 as the second input to the operation.
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="operation"></param>
    /// <returns></returns>
    public static Vector3Int ElementWise(this Vector3Int v1,Func<int,int,int> operation, Vector3Int v2)
    {
        return new Vector3Int(operation(v1.x,v2.x), operation(v1.y,v2.y), operation(v1.z,v2.z));
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
    #endregion

}
