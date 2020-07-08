﻿using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;
using UniVox.Framework.Jobified;
using Utils;
using static Utils.Helpers;

namespace UniVox.Implementations.Meshers
{
    // The MIT License (MIT)
    //
    // Copyright (c) 2012-2013 Mikola Lysenko, modified by Jacob Taylor 2020
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
    [BurstCompile]
    public struct GreedyMeshingJob : IMeshingJob
    {        
        public MeshJobData data { get; set; }

        //Private temporaries
        private MaterialRunTracker runTracker;
        private FaceDescriptor nullFace;//The empty face
        private int dxdy;

        public void Dispose()
        {
            data.Dispose();
        }

        public void Execute()
        {
            if (data.dimensions.x != data.dimensions.y || data.dimensions.y != data.dimensions.z)
            {
                throw new ArgumentException($"Greedy meshing does not support non-cubic chunk dimensions. " +
                    $"x,y,z of chunk dimensions must be identical to use greedy meshing.");
            }

            //initialise temporaries
            nullFace = default;
            dxdy = data.dimensions.x * data.dimensions.y;
            runTracker = new MaterialRunTracker();
            NativeList<Dolater> nonCollidable = new NativeList<Dolater>(Allocator.Temp);

            int size = data.dimensions.x;

            //Sweep over 3-axes
            for (int axis = 0; axis < 3; axis++)
            {
                //Secondary axis is used for width, tertiary axis is used for height
                int secondaryAxis;
                int tertiaryAxis;

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
                NativeArray<FaceDescriptor> maskPositive = new NativeArray<FaceDescriptor>((size + 1) * (size + 1), Allocator.Temp);
                NativeArray<FaceDescriptor> maskNegative = new NativeArray<FaceDescriptor>((size + 1) * (size + 1), Allocator.Temp);

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
                            FaceDescriptor positiveFace = (workingCoordinates[axis] >= 0) ? maskData(workingCoordinates, positiveAxisDirection) : nullFace;
                            //Face in the negative axis direction
                            FaceDescriptor negativeFace = (workingCoordinates[axis] < size - 1) ? maskData(workingCoordinates + axisVector, negativeAxisDirection) : nullFace;

                            if (IncludeFace(positiveFace, negativeFace))
                            {
                                maskPositive[maskIndex] = positiveFace;
                            }

                            if (IncludeFace(negativeFace, positiveFace))
                            {
                                maskNegative[maskIndex] = negativeFace;
                            }

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
                                    workingCoordinates[secondaryAxis] = i;
                                    workingCoordinates[tertiaryAxis] = j;
                                    int3 du = new int3();
                                    int3 dv = new int3();

                                    du[secondaryAxis] = width;
                                    dv[tertiaryAxis] = height;

                                    if (data.voxelTypeDatabase.voxelTypeToIsPassableMap[currentMaskValue.typeId])
                                    {
                                        nonCollidable.Add(new Dolater(currentMaskValue, workingCoordinates, du, dv, width, height, flip));
                                    }
                                    else
                                    {
                                        ProcessSection(currentMaskValue, workingCoordinates, du, dv, width, height, flip);
                                    }

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

            //Record length of collidable mesh section
            data.collisionSubmesh.Record(data.vertices.Length, data.allTriangleIndices.Length, data.materialRuns.Length);
            //End collidable run
            runTracker.EndRun(data.materialRuns, data.allTriangleIndices);

            //Process non collidable sections
            for (int j = 0; j < nonCollidable.Length; j++)
            {
                var item = nonCollidable[j];
                ProcessSection(item.currentMaskValue, item.workingCoordinates, item.du, item.dv, item.width, item.height, item.flip);
            }

            //End non collidable run
            runTracker.EndRun(data.materialRuns, data.allTriangleIndices);
        }

        public void Run()
        {
            IJobExtensions.Run(this);
        }

        public JobHandle Schedule(JobHandle dependsOn = default)
        {
            return IJobExtensions.Schedule(this, dependsOn);
        }

        /// <summary>
        /// Holds the parameters for a call to ProcessSection
        /// to execute it later on.
        /// </summary>
        private struct Dolater 
        {
            public FaceDescriptor currentMaskValue;
            public int3 workingCoordinates;
            public int3 du;
            public int3 dv;
            public int width;
            public int height;
            public bool flip;

            public Dolater(FaceDescriptor currentMaskValue, int3 workingCoordinates, int3 du, int3 dv, int width, int height, bool flip)
            {
                this.currentMaskValue = currentMaskValue;
                this.workingCoordinates = workingCoordinates;
                this.du = du;
                this.dv = dv;
                this.width = width;
                this.height = height;
                this.flip = flip;
            }
        }

        /// <summary>
        /// Compute the properties of the quad with given parameters,
        /// and add it to the mesh.
        /// </summary>
        /// <param name="currentMaskValue"></param>
        /// <param name="workingCoordinates"></param>
        /// <param name="du"></param>
        /// <param name="dv"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="flip"></param>
        private void ProcessSection(FaceDescriptor currentMaskValue,
            int3 workingCoordinates,
            int3 du,
            int3 dv,
            int width,
            int height,
            bool flip) 
        {
            // Add quad if mask value not null
            var meshID = data.meshDatabase.voxelTypeToMeshTypeMap[currentMaskValue.typeId];
            var meshRange = data.meshDatabase.meshTypeRanges[meshID];
            var usedNodesSlice = data.meshDatabase.nodesUsedByFaces[meshRange.start + (int)currentMaskValue.originalFaceDirection];
            var uvStart = data.voxelTypeDatabase.voxelTypeToZIndicesRangeMap[currentMaskValue.typeId].start;
            var uvZ = data.voxelTypeDatabase.zIndicesPerFace[uvStart + (int)currentMaskValue.originalFaceDirection];

            var materialId = data.meshDatabase.voxelTypeToMaterialIDMap[currentMaskValue.typeId];
            //Update material runs
            runTracker.Update(materialId, data.materialRuns, data.allTriangleIndices);            

            float3 bl = workingCoordinates;
            float3 tr = workingCoordinates + du + dv;
            float3 br = workingCoordinates + du;
            float3 tl = workingCoordinates + dv;

            Node nodebl = data.meshDatabase.allMeshNodes[usedNodesSlice.start];
            Node nodetr = data.meshDatabase.allMeshNodes[usedNodesSlice.start + 1];
            Node nodebr = data.meshDatabase.allMeshNodes[usedNodesSlice.start + 2];
            Node nodetl = data.meshDatabase.allMeshNodes[usedNodesSlice.start + 3];
            ///NOTE for negative faces, the UVs are already flipped in the face definition
            ///Therefore, flip just the vertices when necessary
            if (flip)
            {
                Swap(ref bl, ref br);
                Swap(ref tr, ref tl);
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

            AddQuad(nodebl, nodetr, nodebr, nodetl, uvZ);
        }

        private void AddQuad(Node v0, Node v1, Node v2, Node v3, float uvZ)
        {
            int index = data.vertices.Length;

            data.vertices.Add(v0.vertex);
            data.vertices.Add(v1.vertex);
            data.vertices.Add(v2.vertex);
            data.vertices.Add(v3.vertex);

            data.uvs.Add(new float3(v0.uv, uvZ));
            data.uvs.Add(new float3(v1.uv, uvZ));
            data.uvs.Add(new float3(v2.uv, uvZ));
            data.uvs.Add(new float3(v3.uv, uvZ));

            data.normals.Add(v0.normal);
            data.normals.Add(v1.normal);
            data.normals.Add(v2.normal);
            data.normals.Add(v3.normal);

            data.allTriangleIndices.Add(index);
            data.allTriangleIndices.Add(index + 1);
            data.allTriangleIndices.Add(index + 2);
            data.allTriangleIndices.Add(index);
            data.allTriangleIndices.Add(index + 3);
            data.allTriangleIndices.Add(index + 1);

        }

        private bool IncludeFace(FaceDescriptor thisFace, FaceDescriptor oppositeFace)
        {
            return thisFace.typeId != oppositeFace.typeId;
        }

        private FaceDescriptor maskData(int3 position, Direction direction)
        {
            var typeId = data.voxels[MultiIndexToFlat(position.x,position.y,position.z, data.dimensions.x,dxdy)];
            if (typeId == VoxelTypeManager.AIR_ID)
            {
                return nullFace;
            }

            FaceDescriptor faceDescriptor = new FaceDescriptor()
            {
                typeId = typeId,
                originalFaceDirection = direction,//TODO account for rotation
            };
            return faceDescriptor;
        }

        private struct FaceDescriptor : IEquatable<FaceDescriptor>
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