using UnityEngine;
using System.Collections;

public static class Directions {
    public const byte UP = 0;//+Y
    public const byte DOWN = 1;//-Y
    public const byte NORTH = 2;//+Z
    public const byte SOUTH = 3;//-Z
    public const byte EAST = 4;//+X
    public const byte WEST = 5;//-X

    public const byte NumDirections = 6;

    public static readonly Vector3[] Vectors = new Vector3[]{
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back,
            Vector3.right,
            Vector3.left,
        };
}

