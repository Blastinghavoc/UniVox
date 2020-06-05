using UnityEngine;
using System.Collections;
using UniVox.Framework;

namespace UniVox.Deprecated
{
    public static class MeshDefinitions
    {
        public static VoxelMeshDefinition Cube = new VoxelMeshDefinition()
        {
            AllVertices = new Vector3[8] {
            new Vector3(0, 0, 0),//0
            new Vector3(0, 0, 1),//1
            new Vector3(1, 0, 0),//2
            new Vector3(1, 0, 1),//3
            new Vector3(0, 1, 0),//4
            new Vector3(0, 1, 1),//5
            new Vector3(1, 1, 0),//6
            new Vector3(1, 1, 1),//7
        },

            AllUvs = new Vector2[] {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1),
        },

            AllNormals = Directions.Vectors,//A Cube's normals are the cardinal direction vectors

            Faces = new VoxelFaceDefinition[] {
            new VoxelFaceDefinition(){ //UP
               UsedVertices = new int[]{6,4,5,7},
               UsedUvs = new int[]{ 0,1,2,3},
               UsedNormals = new int[]{ Directions.UP, Directions.UP, Directions.UP, Directions.UP },

               Triangles = new int[]{0,1,2,0,2,3},
            },
            new VoxelFaceDefinition(){//DOWN
                UsedVertices = new int[]{ 0,2,3,1},
                UsedUvs = new int[]{ 0,1,2,3},
                UsedNormals = new int[]{ Directions.DOWN, Directions.DOWN, Directions.DOWN, Directions.DOWN },

                Triangles = new int[]{0,1,2,0,2,3},
            },
            new VoxelFaceDefinition(){//NORTH
                UsedVertices = new int[]{ 1,3,7,5},
                UsedUvs = new int[]{ 0,1,2,3},
                UsedNormals = new int[]{ Directions.NORTH, Directions.NORTH, Directions.NORTH, Directions.NORTH },

                Triangles = new int[]{0,1,2,0,2,3},
            },
            new VoxelFaceDefinition(){//SOUTH
                UsedVertices = new int[]{ 2,0,4,6},
                UsedUvs = new int[]{ 0,1,2,3},
                UsedNormals = new int[]{ Directions.SOUTH, Directions.SOUTH, Directions.SOUTH, Directions.SOUTH },

                Triangles = new int[]{0,1,2,0,2,3},
            },
            new VoxelFaceDefinition(){//EAST
                UsedVertices = new int[]{ 3,2,6,7},
                UsedUvs = new int[]{ 0,1,2,3},
                UsedNormals = new int[]{ Directions.EAST, Directions.EAST, Directions.EAST, Directions.EAST },

                Triangles = new int[]{0,1,2,0,2,3},
            },
            new VoxelFaceDefinition(){//WEST
                UsedVertices = new int[]{ 0,1,5,4},
                UsedUvs = new int[]{ 0,1,2,3},
                UsedNormals = new int[]{ Directions.WEST, Directions.WEST, Directions.WEST, Directions.WEST },

                Triangles = new int[]{0,1,2,0,2,3},
            },
        }
        };

    }
}