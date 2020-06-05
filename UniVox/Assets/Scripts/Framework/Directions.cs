using UnityEngine;
using System.Collections;

namespace UniVox.Framework
{
    public static class Directions
    {
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

        public static readonly Vector3Int[] IntVectors = new Vector3Int[] {
        Vector3Int.up,
        Vector3Int.down,
        new Vector3Int(0,0,1),
        new Vector3Int(0,0,-1),
        Vector3Int.right,
        Vector3Int.left,
    };

        public static readonly byte[] Oposite = new byte[]
        {
        DOWN,
        UP,
        SOUTH,
        NORTH,
        WEST,
        EAST
        };
    }
}