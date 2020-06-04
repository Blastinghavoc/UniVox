using UnityEngine;
using System.Collections;
using System;

public static class VectorExtensions
{
    /// <summary>
    /// Returns true if all elements of the vector statisy the condition
    /// </summary>
    /// <param name=""></param>
    /// <param name="condition"></param>
    /// <returns></returns>
    public static bool All(this Vector3Int v, Func<int, bool> condition) 
    {
        return condition(v.x) && condition(v.y) && condition(v.z);
    }

    public static bool LessThan(this Vector3Int v1, Vector3Int v2) 
    {
        return 
            v1.x < v2.x && 
            v1.y < v2.y && 
            v1.z < v2.z;
    }

    public static bool GreaterThan(this Vector3Int v1, Vector3Int v2)
    {
        return
            v1.x > v2.x &&
            v1.y > v2.y &&
            v1.z > v2.z;
    }
}
