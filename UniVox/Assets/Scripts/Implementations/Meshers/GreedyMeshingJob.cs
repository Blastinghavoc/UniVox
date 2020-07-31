using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;
using UniVox.Framework.Common;
using UniVox.Framework.Jobified;
using UniVox.Framework.Lighting;
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

        public NativeHashMap<int, VoxelRotation> rotatedVoxelsMap;
        public NativeDirectionRotator directionRotator;

        //Private locals
        private MaterialRunTracker runTracker;
        private FaceDescriptor nullFace;//The empty face
        private int dxdy;

        public void Dispose()
        {
            data.Dispose();
            rotatedVoxelsMap.Dispose();
        }

        public void Execute()
        {
            if (data.dimensions.x != data.dimensions.y || data.dimensions.y != data.dimensions.z)
            {
                throw new ArgumentException($"Greedy meshing does not support non-cubic chunk dimensions. " +
                    $"x,y,z of chunk dimensions must be identical to use greedy meshing.");
            }

            //initialise locals
            nullFace = default;
            dxdy = data.dimensions.x * data.dimensions.y;
            runTracker = new MaterialRunTracker();
            NativeList<Dolater> nonCollidable = new NativeList<Dolater>(Allocator.Temp);

            //Initialise rotated voxels map from list
            for (int i = 0; i < data.rotatedVoxels.Length; i++)
            {
                var item = data.rotatedVoxels[i];
                rotatedVoxelsMap.Add(item.flatIndex, item.rotation);
            }

            int size = data.dimensions.x;

            //Sweep over 3-axes
            for (byte axis = 0; axis < 3; axis++)
            {
                //Secondary axis is used for width, tertiary axis is used for height
                byte secondaryAxis;
                byte tertiaryAxis;

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
                    positiveAxisDirection = Direction.east;
                    negativeAxisDirection = Direction.west;
                }
                else if (axis == 1)
                {
                    positiveAxisDirection = Direction.up;
                    negativeAxisDirection = Direction.down;
                }
                else
                {
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
                            bool positiveFaceInThisChunk = (workingCoordinates[axis] >= 0);
                            FaceDescriptor positiveFace = positiveFaceInThisChunk ? maskData(workingCoordinates, positiveAxisDirection, workingCoordinates + axisVector)
                                : GetFaceInNeighbour(workingCoordinates, negativeAxisDirection, positiveAxisDirection, axis, workingCoordinates + axisVector);
                            
                            //Face in the negative axis direction
                            bool negativeFaceInThisChunk = (workingCoordinates[axis] < size - 1);
                            FaceDescriptor negativeFace = negativeFaceInThisChunk ? maskData(workingCoordinates + axisVector, negativeAxisDirection, workingCoordinates)
                                : GetFaceInNeighbour(workingCoordinates + axisVector, positiveAxisDirection, negativeAxisDirection, axis,workingCoordinates);

                            if (positiveFaceInThisChunk && IncludeFace(positiveFace, negativeFace))
                            {
                                maskPositive[maskIndex] = positiveFace;
                            }

                            if (negativeFaceInThisChunk && IncludeFace(negativeFace, positiveFace))
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
                        //Normals must be flipped for negative faces
                        bool flipNormals = !isPositive;

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

                                    if (data.voxelTypeDatabase.voxelTypeToIsPassableMap[currentMaskValue.typeId])
                                    {
                                        nonCollidable.Add(new Dolater(currentMaskValue, workingCoordinates,
                                            axis, secondaryAxis, tertiaryAxis, width, height, flip, flipNormals));
                                    }
                                    else
                                    {
                                        ProcessSection(currentMaskValue, workingCoordinates,
                                            axis, secondaryAxis, tertiaryAxis, width, height, flip, flipNormals);
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
                ProcessSection(item.currentMaskValue, item.workingCoordinates,
                    item.primaryAxis, item.secondaryAxis, item.tertiaryAxis, item.width, item.height, item.flip, item.flipNormals);
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
            public byte primaryAxis;
            public byte secondaryAxis;
            public byte tertiaryAxis;
            public int width;
            public int height;
            public bool flip;
            public bool flipNormals;

            public Dolater(FaceDescriptor currentMaskValue,
                int3 workingCoordinates,
                byte primaryAxis,
                byte secondaryAxis,
                byte tertiaryAxis,
                int width,
                int height,
                bool flip,
                bool flipNormals)
            {
                this.currentMaskValue = currentMaskValue;
                this.workingCoordinates = workingCoordinates;
                this.primaryAxis = primaryAxis;
                this.secondaryAxis = secondaryAxis;
                this.tertiaryAxis = tertiaryAxis;
                this.width = width;
                this.height = height;
                this.flip = flip;
                this.flipNormals = flipNormals;
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
            byte primaryAxis,
            byte secondaryAxis,
            byte tertiaryAxis,
            int width,
            int height,
            bool flip,
            bool flipNormals)
        {
            // Add quad if mask value not null
            var meshID = data.meshDatabase.voxelTypeToMeshTypeMap[currentMaskValue.typeId];
            var meshRange = data.meshDatabase.meshTypeRanges[meshID];
            var usedNodesSlice = data.meshDatabase.nodesUsedByFaces[meshRange.start + (int)currentMaskValue.faceDirection];
            var uvStart = data.voxelTypeDatabase.voxelTypeToZIndicesRangeMap[currentMaskValue.typeId].start;
            var uvZ = data.voxelTypeDatabase.zIndicesPerFace[uvStart + (int)currentMaskValue.faceDirection];


            var materialId = data.meshDatabase.voxelTypeToMaterialIDMap[currentMaskValue.typeId];
            //Update material runs
            runTracker.Update(materialId, data.materialRuns, data.allTriangleIndices);

            //int3 du = new int3();
            //int3 dv = new int3();

            //du[secondaryAxis] = width;
            //dv[tertiaryAxis] = height;

            //float3 bl = workingCoordinates;
            //float3 tr = workingCoordinates + du + dv;
            //float3 br = workingCoordinates + du;
            //float3 tl = workingCoordinates + dv;

            Node nodebl = data.meshDatabase.allMeshNodes[usedNodesSlice.start];
            Node nodetr = data.meshDatabase.allMeshNodes[usedNodesSlice.start + 1];
            Node nodebr = data.meshDatabase.allMeshNodes[usedNodesSlice.start + 2];
            Node nodetl = data.meshDatabase.allMeshNodes[usedNodesSlice.start + 3];

            //Apply rotation
            if (!currentMaskValue.rotation.isBlank)
            {
                var rotationQuat = directionRotator.GetRotationQuat(currentMaskValue.rotation);
                nodebl = adjustForRotation(nodebl, rotationQuat);
                nodetr = adjustForRotation(nodetr, rotationQuat);
                nodebr = adjustForRotation(nodebr, rotationQuat);
                nodetl = adjustForRotation(nodetl, rotationQuat);
            }


            int3 scale = new int3();
            scale[secondaryAxis] = width;
            scale[tertiaryAxis] = height;
            //Apply scaling
            nodebl.vertex *= scale;
            nodetr.vertex *= scale;
            nodebr.vertex *= scale;
            nodetl.vertex *= scale;
            //Apply translation
            nodebl.vertex += workingCoordinates;
            nodetr.vertex += workingCoordinates;
            nodebr.vertex += workingCoordinates;
            nodetl.vertex += workingCoordinates;


            int3 normal = new int3();
            normal[primaryAxis] = 1;

            ///NOTE for negative faces, the UVs are already flipped in the face definition
            ///Therefore, flip just the vertices and normals when necessary
            //if (flip)
            //{
            //    Swap(ref nodebl, ref nodebr);
            //    Swap(ref nodetr, ref nodetl);
            //}
            if (flipNormals)
            {
                normal *= -1;
            }

            nodebl.normal = normal;
            nodetr.normal = normal;
            nodebr.normal = normal;
            nodetl.normal = normal;

            //nodebl.vertex = bl;
            //nodetr.vertex = tr;
            //nodebr.vertex = br;
            //nodetl.vertex = tl;

            var secondaryDif = math.lengthsq(nodetl.vertex - nodebl.vertex);
            var tertiaryDif = math.lengthsq(nodebr.vertex - nodebl.vertex);
            int2 uvScale = new int2(width, height);
            ///Calculate the correct uv scale based on which axis is longer 
            ///(if they are equal it doesn't matter).
            ///This is needed because it is difficult to track the correct orientation
            ///of width and height after rotation
            if (!currentMaskValue.rotation.isBlank)
            {
                if (width > height)
                {
                    if (secondaryDif > tertiaryDif)
                    {
                        uvScale = new int2(height, width);
                    }
                }
                else
                {
                    if (secondaryDif < tertiaryDif)
                    {
                        uvScale = new int2(height, width);
                    }
                }
            }

            //Scale the UVs
            //nodebl.uv *= uvScale;//Don't need to scale the bottom left, as its UV should be 0,0
            nodetr.uv *= uvScale;
            nodebr.uv *= uvScale;
            nodetl.uv *= uvScale;

            bool makeBackface = data.meshDatabase.meshIdToIncludeBackfacesMap[meshID];

            var lightForFace = currentMaskValue.lightValue.ToVertexColour();

            AddQuad(nodebl, nodetr, nodebr, nodetl, uvZ,lightForFace, makeBackface);
        }

        private Node adjustForRotation(Node node, quaternion quat)
        {
            float3 rotationOffset = new float3(.5f, .5f, .5f);
            node.vertex = math.mul(quat, node.vertex - rotationOffset) + rotationOffset;
            return node;
        }

        private void AddQuad(Node v0, Node v1, Node v2, Node v3, float uvZ,Color color, bool makeBackface = false)
        {
            int index = data.vertices.Length;

            data.vertices.Add(v0.vertex);
            data.vertices.Add(v1.vertex);
            data.vertices.Add(v2.vertex);
            data.vertices.Add(v3.vertex);

            data.vertexColours.Add(color);
            data.vertexColours.Add(color);
            data.vertexColours.Add(color);
            data.vertexColours.Add(color);

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

            if (makeBackface)
            {
                data.allTriangleIndices.Add(index + 1);//6
                data.allTriangleIndices.Add(index + 3);//5
                data.allTriangleIndices.Add(index);//4
                data.allTriangleIndices.Add(index + 2);//3
                data.allTriangleIndices.Add(index + 1);//2
                data.allTriangleIndices.Add(index);//1
            }

        }

        private FaceDescriptor GetFaceInNeighbour(int3 position, Direction neighbourDirection, Direction faceDirection, int primaryAxis,int3 lightPosition)
        {
            var localIndexOfAdjacentVoxelInNeighbour = data.neighbourData.IndicesInNeighbour(primaryAxis, position);

            var neighbourChunkData = data.neighbourData.GetVoxels(neighbourDirection);

            var neighbourDimensions = data.neighbourData.IndicesInNeighbour(primaryAxis, data.dimensions);

            var flattenedIndex = MultiIndexToFlat(localIndexOfAdjacentVoxelInNeighbour.x, localIndexOfAdjacentVoxelInNeighbour.y, neighbourDimensions);

            var id = neighbourChunkData[flattenedIndex];

            var lv = JobUtils.GetLightValue(lightPosition, data.lights, data.dimensions, data.neighbourData);

            //NOTE currently the rotation data is not fetched for neighbours, so this can't be incorporated.
            return makeFaceDescriptor(id, faceDirection, lv);
        }

        private bool IncludeFace(FaceDescriptor thisFace, FaceDescriptor oppositeFace)
        {
            if (thisFace.typeId == oppositeFace.typeId)
            {
                return false;//Dont include faces between voxels of the same type
            }

            if (oppositeFace.typeId == VoxelTypeID.AIR_ID)
            {
                //Include the face if the opposite voxel is air
                return true;
            }

            var meshID = data.meshDatabase.voxelTypeToMeshTypeMap[oppositeFace.typeId];
            var meshRange = data.meshDatabase.meshTypeRanges[meshID];
            var faceIsSolid = data.meshDatabase.isFaceSolid[meshRange.start + (int)oppositeFace.faceDirection];

            //Exclude this face if opposite face is solid
            return !faceIsSolid;

        }

        private FaceDescriptor maskData(int3 position, Direction direction,int3 lightPosition)
        {
            var flatIndex = MultiIndexToFlat(position.x, position.y, position.z, data.dimensions.x, dxdy);
            var typeId = data.voxels[flatIndex];
            var lv = JobUtils.GetLightValue(lightPosition,data.lights,data.dimensions,data.neighbourData);
            if (rotatedVoxelsMap.TryGetValue(flatIndex, out var rotation))
            {
                return makeFaceDescriptor(typeId, direction, lv, rotation);
            }
            return makeFaceDescriptor(typeId, direction, lv);
        }

        private FaceDescriptor makeFaceDescriptor(VoxelTypeID typeId, Direction originalDirection,LightValue lightValue, VoxelRotation rotation = default)
        {
            if (typeId == VoxelTypeID.AIR_ID)
            {
                return nullFace;
            }

            var faceDirection = originalDirection;

            if (!rotation.isBlank)
            {
                ///Face direction needs to be the direction index of the face currently pointing in the original
                ///direction. Therefore it is the direction such that applying the rotation gives the original direction.
                faceDirection = directionRotator.GetDirectionBeforeRotation(originalDirection, rotation);
            }

            FaceDescriptor faceDescriptor = new FaceDescriptor()
            {
                typeId = typeId,
                faceDirection = faceDirection,
                rotation = rotation,
                lightValue = lightValue
            };
            return faceDescriptor;
        }

        private struct FaceDescriptor : IEquatable<FaceDescriptor>
        {
            public VoxelTypeID typeId;
            //The direction of the face, accounting for any rotation
            public Direction faceDirection;
            public VoxelRotation rotation;
            public LightValue lightValue;

            public bool Equals(FaceDescriptor other)
            {
                return typeId == other.typeId &&
                    faceDirection == other.faceDirection &&
                    rotation.Equals(other.rotation) &&
                    lightValue.Equals(other.lightValue);
            }
        }
    }
}