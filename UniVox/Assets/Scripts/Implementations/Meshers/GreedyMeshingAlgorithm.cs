using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework;

namespace UniVox.Implementations.Meshers
{
    public static class GreedyMeshingAlgorithm 
    {
        // The MIT License (MIT)
        //
        // Copyright (c) 2012-2013 Mikola Lysenko
        //
        // Permission is hereby granted, free of charge, to any person obtaining a copy
        // of this software and associated documentation files (the "Software"), to deal
        // in the Software without restriction, including without limitation the rights
        // to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        // copies of the Software, and to permit persons to whom the Software is
        // furnished to do so, subject to the following conditions:
        //
        // The above copyright notice and this permission notice shall be included in
        // all copies or substantial portions of the Software.
        //
        // THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        // IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        // FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        // AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        // LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        // OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
        // THE SOFTWARE.


        public static Mesh ReduceMesh(IChunkData chunk,Vector3Int chunkDimensions)
        {

            List<Vector3> vertices = new List<Vector3>();
            List<int> elements = new List<int>();

            if (chunkDimensions.x != chunkDimensions.y || chunkDimensions.y != chunkDimensions.z)
            {
                throw new ArgumentException($"Greedy meshing does not support non-cubic chunk dimensions. " +
                    $"x,y,z of chunk dimensions must be identical to use greedy meshing.");
            }

            int size = chunkDimensions.x;

            //Sweep over 3-axes
            for (int axis = 0; axis < 3; axis++)
            {

                int secondaryAxis = (axis + 1) % 3;
                int tertiaryAxis = (axis + 2) % 3;

                int3 workingCoordinates = new int3();
                int3 axisVector = new int3();

                //int[] mask = new int[size * size * size];
                int[] mask = new int[(size + 1) * (size + 1)];            

                axisVector[axis] = 1;

                //Sweep through planes in the direction of the current axis
                for (workingCoordinates[axis] = -1; workingCoordinates[axis] < size;)
                {
                    int maskIndex = 0;
                    // Compute the mask for this plane
                    for (workingCoordinates[tertiaryAxis] = 0; workingCoordinates[tertiaryAxis] < size; ++workingCoordinates[tertiaryAxis])
                    {
                        for (workingCoordinates[secondaryAxis] = 0; workingCoordinates[secondaryAxis] < size; ++workingCoordinates[secondaryAxis], ++maskIndex)
                        {

                            //If current position value is valid, record that value, otherwise assumed to be 0
                            int currentPositionValue = (workingCoordinates[axis] >= 0) ? data(chunk, workingCoordinates[0], workingCoordinates[1], workingCoordinates[2]) : 0;
                            //If the adjacent position (in the axis direction) is valid, record that value, otherwise 0
                            int adjacentPositionValue = (workingCoordinates[axis] < size - 1) ? data(chunk, workingCoordinates[0] + axisVector[0], workingCoordinates[1] + axisVector[1], workingCoordinates[2] + axisVector[2]) : 0;

                            
                            //This was part of the original condition: currentPositionValue != -1 && adjacentPositionValue != -1
                            if (currentPositionValue == adjacentPositionValue) 
                            { 
                                //Cull face(s) in this direction, as the voxels are of the same type
                                mask[maskIndex] = 0; 
                            }
                            else if (currentPositionValue > 0)
                            {
                                mask[maskIndex] = currentPositionValue;
                            }
                            else
                            {
                                ///If the current position is air, we may still need a face in the other direction. 
                                ///Record the negated adjacent id to indicate this
                                mask[maskIndex] = -adjacentPositionValue;
                            }
                        }
                    }

                    // Step forward one slice along the axis
                    ++workingCoordinates[axis];

                    // Generate mesh for mask using lexicographic ordering
                    maskIndex = 0;
                    for (int j = 0; j < size; ++j)
                    {
                        for (int i = 0; i < size;)
                        {

                            var currentMaskValue = mask[maskIndex];

                            Assert.IsTrue(currentMaskValue > -2,$"Was {currentMaskValue}");

                            //if (currentMaskValue > -2)
                            //{
                                // Compute width as maximal width such that the current mask value does not change
                                int width;
                                for (width = 1; currentMaskValue == mask[maskIndex + width] && i + width < size; ++width) { }

                                // Compute height
                                int height;
                                bool done = false;
                                // Increase height up to no more than the size
                                for (height = 1; j + height < size; ++height)
                                {
                                    //Check that the mask value does not change along the row
                                    for (int k = 0; k < width; ++k)
                                    {
                                        if (currentMaskValue != mask[maskIndex + k + height * size])
                                        {
                                            //If the mask value has changed, we can go no further
                                            done = true;
                                            break;
                                        }
                                    }

                                    if (done) break;
                                }

                                // Add quad

                                workingCoordinates[secondaryAxis] = i;
                                workingCoordinates[tertiaryAxis] = j;
                                int3 du = new int3();
                                int3 dv = new int3();

                                du[secondaryAxis] = width;
                                dv[tertiaryAxis] = height;

                                bool flip = false;
                                if (currentMaskValue < 0)
                                {
                                    //Face needs to be flipped
                                    flip = true;
                                    currentMaskValue = -currentMaskValue;//Restore the original voxel id
                                }


                                float3 v1 = workingCoordinates;
                                float3 v2 = workingCoordinates + du;
                                float3 v3 = workingCoordinates + du + dv;
                                float3 v4 = workingCoordinates + dv;

                                bool floor = false;

                                if (v1.y == 0 && v2.y == 0 && v3.y == 0 && v4.y == 0) 
                                { 
                                    floor = true;
                                }

                                if (currentMaskValue > 0 && !floor)
                                {
                                    AddFace(v1, v2, v3, v4, vertices, elements);
                                }

                                //TODO remove DEBUG checking if the floor check is necessary. It does seem to be
                                if (currentMaskValue > 0 && floor)
                                {
                                    //Debug.Log("Yes Floor");
                                }

                                //Note flip implies currentMaskValue != 0
                                if (flip)
                                {
                                    AddFace(v4, v3, v2, v1, vertices, elements);
                                }

                                // Zero-out mask for this section
                                //TODO check if this is necessary
                                for (int l = 0; l < height; ++l)
                                {
                                    for (int k = 0; k < width; ++k)
                                    {
                                        mask[maskIndex + k + l * size] = 0;
                                    }
                                }
                                // Increment counters and continue
                                i += width; maskIndex += width;
                            //}

                            //else
                            //{
                            //    Debug.Log("Yes");
                            //    ++i;
                            //    ++maskIndex;
                            //}
                        }
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = elements.ToArray();

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;

        }


        public static void AddFace(float3 v1, float3 v2, float3 v3, float3 v4, List<Vector3> vertices, List<int> elements)
        {
            int index = vertices.Count;

            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);
            vertices.Add(v4);

            elements.Add(index);
            elements.Add(index + 1);
            elements.Add(index + 2);
            elements.Add(index + 2);
            elements.Add(index + 3);
            elements.Add(index);

        }


        private static int data(IChunkData chunk, int x, int y, int z)
        {
            int value = (int)chunk[x, y, z];
            if (value > 1) { value = 1; }
            return value;
        }

    }
}