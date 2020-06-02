using UnityEngine;
using System.Collections;

public class CubeMeshDefinition : VoxelMeshDefinition
{
    public CubeMeshDefinition()
    {
        Vertices = new Vector3[8] {
            new Vector3(0,0,0),//0
            new Vector3(0,0,1),//1
            new Vector3(1,0,0),//2
            new Vector3(1,0,1),//3
            new Vector3(0,1,0),//4
            new Vector3(0,1,1),//5
            new Vector3(1,1,0),//6
            new Vector3(1,1,1),//7
        };
        FaceIndices = new int[][] {
            new int[]{4,5,7,4,7,6},       //UP
            new int[]{0,3,1,0,2,3},       //DOWN
            new int[]{3,2,6,3,6,7},       //NORTH
            new int[]{0,1,5,0,5,4},       //SOUTH
            new int[]{1,3,7,1,7,5},       //EAST
            new int[]{2,0,4,2,4,6},       //WEST
        };


    }
}
