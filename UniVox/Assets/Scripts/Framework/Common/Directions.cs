using UnityEngine;

namespace UniVox.Framework.Common
{

    public enum Direction : byte
    {
        up,
        down,
        north,
        south,
        east,
        west,
    }

    public static class DirectionExtensions
    {
        public const int numDirections = 6;

        public static readonly Vector3Int[] Vectors = new Vector3Int[] {
        Vector3Int.up,
        Vector3Int.down,
        new Vector3Int(0,0,1),
        new Vector3Int(0,0,-1),
        Vector3Int.right,
        Vector3Int.left,
        };

        public static readonly Direction[] Opposite = new Direction[]
        {
            Direction.down,
            Direction.up,
            Direction.south,
            Direction.north,
            Direction.west,
            Direction.east,
        };
    }

    /// <summary>
    /// Directions including diagonals
    /// </summary>
    public enum DiagonalDirection : int
    {
        //Basic directions
        up,
        down,
        north,
        south,
        east,
        west,

        //Middle diagonals
        NE,
        NW,
        SE,
        SW,

        //Up Cardinal
        up_north,
        up_south,
        up_east,
        up_west,

        //Up diagonals
        up_NE,
        up_NW,
        up_SE,
        up_SW,


        //Down Cardinal
        down_north,
        down_south,
        down_east,
        down_west,

        //Down diagonals
        down_NE,
        down_NW,
        down_SE,
        down_SW,

    }

    public static class DiagonalDirectionExtensions
    {
        public const int numDirections = 26;

        private static readonly Vector3Int vectorNorth = new Vector3Int(0, 0, 1);
        private static readonly Vector3Int vectorSouth = new Vector3Int(0, 0, -1);
        private static readonly Vector3Int vectorEast = Vector3Int.right;
        private static readonly Vector3Int vectorWest = Vector3Int.left;

        public static readonly Vector3Int[] Vectors = new Vector3Int[] {
            //Basic directions
            Vector3Int.up,//up
            Vector3Int.down,//down
            vectorNorth,//north
            vectorSouth,//south
            vectorEast,//east
            vectorWest,//west

            //Middle diagonals
            vectorNorth + vectorEast,//NE
            vectorNorth + vectorWest,//NW
            vectorSouth + vectorEast,//SE
            vectorSouth + vectorWest,//SW

            //up cardinal
            Vector3Int.up + vectorNorth,
            Vector3Int.up + vectorSouth,
            Vector3Int.up + vectorEast,
            Vector3Int.up + vectorWest,

            //up diagonals
            Vector3Int.up + vectorNorth + vectorEast,
            Vector3Int.up + vectorNorth + vectorWest,
            Vector3Int.up + vectorSouth + vectorEast,
            Vector3Int.up + vectorSouth + vectorWest,

            //down cardinal
            Vector3Int.down + vectorNorth,
            Vector3Int.down + vectorSouth,
            Vector3Int.down + vectorEast,
            Vector3Int.down + vectorWest,

            //down diagonals
            Vector3Int.down + vectorNorth + vectorEast,
            Vector3Int.down + vectorNorth + vectorWest,
            Vector3Int.down + vectorSouth + vectorEast,
            Vector3Int.down + vectorSouth + vectorWest,
        };

        public static readonly DiagonalDirection[] Opposite = new DiagonalDirection[]
            {
                //Reverse of basic directions
                DiagonalDirection.down,
                DiagonalDirection.up,
                DiagonalDirection.south,
                DiagonalDirection.north,
                DiagonalDirection.west,
                DiagonalDirection.east,

                //Reverse of middle diagonals
                DiagonalDirection.SW,
                DiagonalDirection.SE,
                DiagonalDirection.NW,
                DiagonalDirection.NE,

                //Reverse of up cardinal
                DiagonalDirection.down_south,
                DiagonalDirection.down_north,
                DiagonalDirection.down_west,
                DiagonalDirection.down_east,

                //Reverse of up diagonals
                DiagonalDirection.down_SW,
                DiagonalDirection.down_SE,
                DiagonalDirection.down_NW,
                DiagonalDirection.down_NE,

                //Reverse of down cardinal
                DiagonalDirection.up_south,
                DiagonalDirection.up_north,
                DiagonalDirection.up_west,
                DiagonalDirection.up_east,

                //Reverse of down diagonals
                DiagonalDirection.up_SW,
                DiagonalDirection.up_SE,
                DiagonalDirection.up_NW,
                DiagonalDirection.up_NE,
            };
    }
}