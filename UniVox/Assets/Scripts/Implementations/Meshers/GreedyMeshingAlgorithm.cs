﻿using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework;
using UniVox.Framework.Jobified;
using Utils;
using static Utils.Helpers;

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

        private static FaceDescriptor nullFace = default;//The empty face

        public static Mesh ReduceMesh(IChunkData chunk,Vector3Int chunkDimensions,NativeMeshDatabase meshDatabase,NativeVoxelTypeDatabase voxelTypeDatabase,NativeDirectionHelper directionHelper)
        {

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> uvs = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
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
                //Secondary axis is used for width, tertiary axis is used for height
                int secondaryAxis;// = (axis + 1) % 3;
                int tertiaryAxis;// = (axis + 2) % 3;

                switch (axis)
                {
                    case 0://x axis primary
                        secondaryAxis = 2;
                        tertiaryAxis = 1;
                        break;
                    case 1://y axis primary
                        secondaryAxis = 0;
                        tertiaryAxis = 2;
                        break;
                    case 2://z axis primary
                        secondaryAxis = 0;
                        tertiaryAxis = 1;
                        break;
                    default:
                        throw new Exception("axis not valid");
                }


                int3 workingCoordinates = new int3();
                int3 axisVector = new int3();

                //Masks in the positive and negative directions
                FaceDescriptor[] maskPositive = new FaceDescriptor[(size + 1) * (size + 1)];
                FaceDescriptor[] maskNegative = new FaceDescriptor[(size + 1) * (size + 1)];            

                axisVector[axis] = 1;

                //Compute the direction we are currently meshing
                //Direction currentMeshDirection;
                Direction positiveAxisDirection; 
                Direction negativeAxisDirection; 
                if (axis == 0)
                {
                    //currentMeshDirection = (reverseAxis) ? Direction.west : Direction.east;
                    positiveAxisDirection = Direction.east;
                    negativeAxisDirection = Direction.west;
                }
                else if (axis == 1)
                {
                    //currentMeshDirection = (reverseAxis) ? Direction.down : Direction.up;
                    positiveAxisDirection = Direction.up;
                    negativeAxisDirection = Direction.down;
                }
                else
                {
                    //currentMeshDirection = (reverseAxis) ? Direction.south : Direction.north;
                    positiveAxisDirection = Direction.north;
                    negativeAxisDirection = Direction.south;
                }


                //Sweep through planes in the direction of the current axis
                for (workingCoordinates[axis] = -1; workingCoordinates[axis] < size;)
                {
                    int maskIndex = 0;
                    // Compute the mask for this plane
                    for (workingCoordinates[tertiaryAxis] = 0; workingCoordinates[tertiaryAxis] < size; ++workingCoordinates[tertiaryAxis])
                    {
                        for (workingCoordinates[secondaryAxis] = 0; workingCoordinates[secondaryAxis] < size; ++workingCoordinates[secondaryAxis], ++maskIndex)
                        {

                            //Face in the positive axis direction
                            FaceDescriptor positiveFace = (workingCoordinates[axis] >= 0) ? maskData(chunk, workingCoordinates,positiveAxisDirection) : nullFace;
                            //Face in the negative axis direction
                            FaceDescriptor negativeFace = (workingCoordinates[axis] < size - 1) ? maskData(chunk, workingCoordinates + axisVector,negativeAxisDirection) : nullFace;

                            if (IncludeFace(positiveFace,negativeFace))
                            {
                                maskPositive[maskIndex] = positiveFace;
                            }

                            if (IncludeFace(negativeFace,positiveFace))
                            {
                                maskNegative[maskIndex] = negativeFace;
                            }

                            //if (positiveFace.Equals(negativeFace)) 
                            //{ 
                            //    //Cull face(s) in this direction, as the voxels are of the same type
                            //    maskPositive[maskIndex] = 0; 
                            //}
                            //else if (positiveFace > 0)
                            //{
                            //    maskPositive[maskIndex] = positiveFace;
                            //}
                            //else
                            //{
                            //    ///If the current position is air, we may still need a face in the other direction. 
                            //    ///Record the negated adjacent id to indicate this
                            //    maskPositive[maskIndex] = -negativeFace;
                            //}
                        }
                    }

                    // Step forward one slice along the axis
                    ++workingCoordinates[axis];

                    //Run meshing twice, once in the positive direction, then again in the negative direction
                    for (int maskSelector = 0; maskSelector <= 1; maskSelector++)
                    {
                        bool isPositive = (maskSelector == 0) ? true : false;
                        var currentMask = (isPositive) ? maskPositive : maskNegative;

                        ///The X and Y axes need to flip quads for negative faces, 
                        ///the z axis needs to flip quads for positive faces
                        bool flip = (axis != 2) ? !isPositive : isPositive;

                        // Generate mesh for current mask using lexicographic ordering
                        maskIndex = 0;
                        for (int j = 0; j < size; ++j)
                        {
                            for (int i = 0; i < size;)
                            {
                                var currentMaskValue = currentMask[maskIndex];  

                                //TODO check for non-quad faces, set width and height to 1 in that case

                                // Compute width as maximal width such that the mask value does not change
                                int width;
                                for (width = 1; currentMaskValue.Equals(currentMask[maskIndex + width]) && i + width < size; ++width) { }

                                // Compute height
                                int height;
                                bool heightDone = false;
                                // Increase height up to no more than the size
                                for (height = 1; j + height < size; ++height)
                                {
                                    //Check that the mask value does not change along the row
                                    for (int k = 0; k < width; ++k)
                                    {
                                        if (!currentMaskValue.Equals(currentMask[maskIndex + k + height * size]))
                                        {
                                            //If the mask value has changed, we can go no further
                                            heightDone = true;
                                            break;
                                        }
                                    }

                                    if (heightDone) break;
                                }


                                if (!currentMaskValue.Equals(nullFace))
                                {
                                    // Add quad if mask value not null
                                    var meshID = meshDatabase.voxelTypeToMeshTypeMap[currentMaskValue.typeId];
                                    var meshRange = meshDatabase.meshTypeRanges[meshID];
                                    var usedNodesSlice = meshDatabase.nodesUsedByFaces[meshRange.start + (int)currentMaskValue.originalFaceDirection];
                                    var uvStart = voxelTypeDatabase.voxelTypeToZIndicesRangeMap[currentMaskValue.typeId].start;
                                    var uvZ = voxelTypeDatabase.zIndicesPerFace[uvStart + (int)currentMaskValue.originalFaceDirection];

                                    workingCoordinates[secondaryAxis] = i;
                                    workingCoordinates[tertiaryAxis] = j;
                                    int3 du = new int3();
                                    int3 dv = new int3();

                                    du[secondaryAxis] = width;
                                    dv[tertiaryAxis] = height;

                                    float3 bl = workingCoordinates;
                                    float3 tr = workingCoordinates + du + dv;
                                    float3 br = workingCoordinates + du;
                                    float3 tl = workingCoordinates + dv;

                                    Node nodebl = meshDatabase.allMeshNodes[usedNodesSlice.start];
                                    Node nodetr = meshDatabase.allMeshNodes[usedNodesSlice.start +1];
                                    Node nodebr = meshDatabase.allMeshNodes[usedNodesSlice.start +2];
                                    Node nodetl = meshDatabase.allMeshNodes[usedNodesSlice.start +3];
                                    ///NOTE for negative faces, the UVs are already flipped in the face definition
                                    ///Therefore, flip just the vertices when necessary
                                    if (flip)
                                    {
                                        Swap(ref bl,ref br);
                                        Swap(ref tr,ref tl);
                                    }

                                    nodebl.vertex = bl;
                                    nodetr.vertex = tr;
                                    nodebr.vertex = br;
                                    nodetl.vertex = tl;

                                    int2 uvScale = new int2(width, height);

                                    //Scale the UVs
                                    //nodebl.uv *= uvScale;//Don't need to scale the bottom left, as its UV should be 0,0
                                    nodetr.uv *= uvScale;
                                    nodebr.uv *= uvScale;
                                    nodetl.uv *= uvScale;
                                                                           
                                    AddFace(nodebl, nodetr, nodebr, nodetl, vertices, elements, uvs, normals, uvZ);         
                                }

                                /// Zero-out mask for this section
                                /// This is necessary to prevent the same area being meshed multiple times,
                                /// and also clears the mask for the next pass
                                for (int l = 0; l < height; ++l)
                                {
                                    for (int k = 0; k < width; ++k)
                                    {
                                        currentMask[maskIndex + k + l * size] = nullFace;
                                    }
                                }
                                // Increment counters and continue
                                i += width; maskIndex += width;                                
                            }
                        }
                    }
                }
            }
            


            Mesh mesh = new Mesh();
            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.SetUVs(0, uvs.ToArray());
            mesh.normals = normals.ToArray();
            mesh.triangles = elements.ToArray();

            //mesh.RecalculateBounds();
            //mesh.RecalculateNormals();

            return mesh;

        }

        private static void AddFace(Node v0, Node v1, Node v2, Node v3, List<Vector3> vertices, List<int> elements,List<Vector3> uvs,List<Vector3> normals,float uvZ)
        {
            int index = vertices.Count;

            vertices.Add(v0.vertex);
            vertices.Add(v1.vertex);
            vertices.Add(v2.vertex);
            vertices.Add(v3.vertex);

            uvs.Add(new float3(v0.uv, uvZ));
            uvs.Add(new float3(v1.uv, uvZ));
            uvs.Add(new float3(v2.uv, uvZ));
            uvs.Add(new float3(v3.uv, uvZ));

            normals.Add(v0.normal);
            normals.Add(v1.normal);
            normals.Add(v2.normal);
            normals.Add(v3.normal);

            elements.Add(index);
            elements.Add(index + 1);
            elements.Add(index + 2);
            elements.Add(index);
            elements.Add(index + 3);
            elements.Add(index + 1);

        }

        private static bool IncludeFace(FaceDescriptor thisFace, FaceDescriptor oppositeFace) 
        {
            return thisFace.typeId != oppositeFace.typeId;        
        }

        private static FaceDescriptor maskData(IChunkData chunk, int3 position,Direction direction)
        {
            var typeId = chunk[position.x, position.y, position.z];
            if (typeId == VoxelTypeManager.AIR_ID)
            {
                return nullFace;
            }

            FaceDescriptor faceDescriptor = new FaceDescriptor() { 
                typeId = typeId,
                originalFaceDirection = direction,//TODO account for rotation
            };
            return faceDescriptor;
        }

        private struct FaceDescriptor: IEquatable<FaceDescriptor> 
        {
            public VoxelTypeID typeId;
            public Direction originalFaceDirection;
            public VoxelRotation rotation;

            public bool Equals(FaceDescriptor other)
            {
                return typeId == other.typeId && 
                    originalFaceDirection == other.originalFaceDirection &&
                    rotation.Equals(other.rotation);
            }
        }

    }
}