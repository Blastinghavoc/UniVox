using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework.Common;
using Utils;
using static UniVox.Framework.Jobified.JobUtils;

namespace UniVox.Framework.Jobified
{

    [BurstCompile]
    public struct MeshingJob : IMeshingJob
    {
        [ReadOnly] public bool cullfaces;
        [ReadOnly] private const int numDirections = DirectionExtensions.numDirections;
        [ReadOnly] public NativeDirectionRotator directionHelper;

        public MeshJobData data { get; set; }

        public NativeList<DoLater> nonCollidableQueue;

        //Private temporaries
        private MaterialRunTracker runTracker;

        public struct DoLater
        {
            public int3 position;
            public ushort typeID;
            public bool rotated;
            public VoxelRotation rotation;
        }

        private struct RotatedVoxelComparer : IComparer<RotatedVoxelEntry>
        {
            public int Compare(RotatedVoxelEntry x, RotatedVoxelEntry y)
            {
                return x.flatIndex.CompareTo(y.flatIndex);
            }
        }

        public void Dispose()
        {
            //Dispose the data
            data.Dispose();
            nonCollidableQueue.Dispose();
        }

        public void Execute()
        {
            //Sort the rotated voxels so that they are in the order of iteration
            if (data.rotatedVoxels.Length > 0)
            {
                data.rotatedVoxels.Sort(new RotatedVoxelComparer());
            }
            int currentRotatedIndex = 0;

            runTracker = new MaterialRunTracker();

            int i = 0;//current index into voxelData
            for (int z = 0; z < data.dimensions.z; z++)
            {
                for (int y = 0; y < data.dimensions.y; y++)
                {
                    for (int x = 0; x < data.dimensions.x; x++)
                    {
                        var voxelTypeID = data.voxels[i];

                        if (voxelTypeID != VoxelTypeID.AIR_ID)
                        {
                            bool rotated = false;
                            VoxelRotation rotation = default;
                            if (data.rotatedVoxels.Length > 0 && data.rotatedVoxels[currentRotatedIndex].flatIndex == i)
                            {
                                //This voxel is rotated
                                rotated = true;
                                rotation = data.rotatedVoxels[currentRotatedIndex].rotation;
                                AdvanceRotatedIndex(ref currentRotatedIndex);
                            }

                            if (data.voxelTypeDatabase.voxelTypeToIsPassableMap[voxelTypeID])
                            {
                                //Save non-collidable voxels for later, so that they appear contiguously in the mesh arrays
                                nonCollidableQueue.Add(new DoLater()
                                {
                                    position = new int3(x, y, z),
                                    typeID = voxelTypeID,
                                    rotated = rotated,
                                    rotation = rotation
                                });
                            }
                            else
                            {
                                AddMeshDataForVoxel(voxelTypeID, new int3(x, y, z), rotated, rotation);
                            }
                        }

                        i++;
                    }
                }
            }
            //Record length of collidable mesh section
            data.collisionSubmesh.Record(data.vertices.Length, data.allTriangleIndices.Length, data.materialRuns.Length);

            runTracker.EndRun(data.materialRuns, data.allTriangleIndices);

            //Process non collidable section
            for (int j = 0; j < nonCollidableQueue.Length; j++)
            {
                var item = nonCollidableQueue[j];
                AddMeshDataForVoxel(item.typeID, item.position, item.rotated, item.rotation);
            }

            runTracker.EndRun(data.materialRuns, data.allTriangleIndices);
        }

        private void AdvanceRotatedIndex(ref int currentRotatedIndex)
        {
            var nextIndex = currentRotatedIndex + 1;
            if (nextIndex < data.rotatedVoxels.Length)
            {
                currentRotatedIndex = nextIndex;
            }
        }

        private void AddMeshDataForVoxel(ushort id, int3 position, bool rotated, VoxelRotation rotation)
        {
            var meshID = data.meshDatabase.voxelTypeToMeshTypeMap[id];
            var faceZRange = data.voxelTypeDatabase.voxelTypeToZIndicesRangeMap[id];
            var materialID = data.meshDatabase.voxelTypeToMaterialIDMap[id];

            var meshRange = data.meshDatabase.meshTypeRanges[meshID];

            //Update the material run tracker
            runTracker.Update(materialID, data.materialRuns, data.allTriangleIndices);


            if (rotated)
            {
                //Add single rotated voxels data
                for (byte dir = 0; dir < numDirections; dir++)
                {
                    var rotatedDirection = (byte)directionHelper.GetDirectionAfterRotation((Direction)dir, rotation);
                    var faceIsSolid = data.meshDatabase.isFaceSolid[meshRange.start + rotatedDirection];
                    if (IncludeFace(id, position, rotatedDirection, faceIsSolid))
                    {
                        AddFaceRotated(meshID, data.voxelTypeDatabase.zIndicesPerFace[faceZRange.start + dir], dir, position, rotation);
                    }
                }
            }
            else
            {
                //Add single voxel's data
                for (int dir = 0; dir < numDirections; dir++)
                {
                    var faceIsSolid = data.meshDatabase.isFaceSolid[meshRange.start + dir];
                    if (IncludeFace(id, position, dir, faceIsSolid))
                    {
                        AddFace(meshID, data.voxelTypeDatabase.zIndicesPerFace[faceZRange.start + dir], dir, position);
                    }
                }
            }
        }

        private void AddFaceRotated(int meshID, float uvZ, int direction, int3 position, VoxelRotation rotation)
        {
            //Lighting
            Color vertexColourValue = new Color();
            if (data.includeLighting)
            {
                var directionVector = directionHelper.DirectionVectors[direction];
                var lightForFace = GetLightValue(position + directionVector, data.lights, data.dimensions, data.neighbourData);
                vertexColourValue = lightForFace.ToVertexColour();
            }

            var meshRange = data.meshDatabase.meshTypeRanges[meshID];

            var usedNodesSlice = data.meshDatabase.nodesUsedByFaces[meshRange.start + direction];

            var currentIndex = data.vertices.Length;

            var rotationQuat = directionHelper.GetRotationQuat(rotation);

            //NOTE vertex definitions start from 0,0,0 in the bottom left of the cube, so an offset is needed for rotations
            float3 rotationOffset = new float3(.5f, .5f, .5f);

            //Add all the nodes used by this face
            for (int i = usedNodesSlice.start; i < usedNodesSlice.end; i++)
            {
                var node = data.meshDatabase.allMeshNodes[i];
                var rotatedVert = math.mul(rotationQuat, node.vertex - rotationOffset) + rotationOffset;
                var adjustedVert = rotatedVert + position;
                data.vertices.Add(adjustedVert);

                if (data.includeLighting)
                {
                    data.vertexColours.Add(vertexColourValue);
                }

                data.uvs.Add(new float3(node.uv, uvZ));
                var adjustedNorm = math.mul(rotationQuat, node.normal);
                data.normals.Add(adjustedNorm);
            }

            //Add the triangleIndices used by this face

            var relativeTrianglesSlice = data.meshDatabase.relativeTrianglesByFaces[meshRange.start + direction];


            for (int i = relativeTrianglesSlice.start; i < relativeTrianglesSlice.end; i++)
            {
                data.allTriangleIndices.Add(data.meshDatabase.allRelativeTriangles[i] + currentIndex);
            }
        }

        private void AddFace(int meshID, float uvZ, int direction, int3 position)
        {
            //Lighting
            Color vertexColourValue = new Color();
            if (data.includeLighting)
            {
                var directionVector = directionHelper.DirectionVectors[direction];
                var lightForFace = GetLightValue(position + directionVector, data.lights, data.dimensions, data.neighbourData);
                vertexColourValue = lightForFace.ToVertexColour();
            }

            var meshRange = data.meshDatabase.meshTypeRanges[meshID];

            var usedNodesSlice = data.meshDatabase.nodesUsedByFaces[meshRange.start + direction];

            var currentIndex = data.vertices.Length;

            //Add all the nodes used by this face
            for (int i = usedNodesSlice.start; i < usedNodesSlice.end; i++)
            {
                var node = data.meshDatabase.allMeshNodes[i];
                data.vertices.Add(node.vertex + position);

                if (data.includeLighting)
                {
                    data.vertexColours.Add(vertexColourValue);
                }

                data.uvs.Add(new float3(node.uv, uvZ));
                data.normals.Add(node.normal);
            }

            //Add the triangleIndices used by this face

            var relativeTrianglesSlice = data.meshDatabase.relativeTrianglesByFaces[meshRange.start + direction];

            for (int i = relativeTrianglesSlice.start; i < relativeTrianglesSlice.end; i++)
            {
                data.allTriangleIndices.Add(data.meshDatabase.allRelativeTriangles[i] + currentIndex);
            }

            if (data.meshDatabase.meshIdToIncludeBackfacesMap[meshID])
            {
                //Generate backface by adding the triangle indices again in reverse
                for (int i = relativeTrianglesSlice.end - 1; i >= relativeTrianglesSlice.start; i--)
                {
                    data.allTriangleIndices.Add(data.meshDatabase.allRelativeTriangles[i] + currentIndex);
                }
            }
        }

        private bool IncludeFace(ushort voxelID, int3 position, int directionIndex, bool faceIsSolid)
        {
            if (!cullfaces)
            {
                return true;
            }
            var directionVector = directionHelper.DirectionVectors[directionIndex];
            var adjacentVoxelIndex = position + directionVector;

            var adjacentId = GetVoxel(adjacentVoxelIndex, data.voxels, data.dimensions, data.neighbourData);
            return (voxelID != adjacentId) && IncludeFaceOfAdjacentWithID(adjacentId, directionIndex);
        }

        private bool IncludeFaceOfAdjacentWithID(ushort voxelTypeID, int direction)
        {
            if (voxelTypeID == VoxelTypeID.AIR_ID)
            {
                //Include the face if the adjacent voxel is air
                return true;
            }
            var meshID = data.meshDatabase.voxelTypeToMeshTypeMap[voxelTypeID];
            var meshRange = data.meshDatabase.meshTypeRanges[meshID];
            var faceIsSolid = data.meshDatabase.isFaceSolid[meshRange.start + (byte)directionHelper.DirectionOpposites[direction]];

            //Exclude this face if adjacent face is solid
            return !faceIsSolid;
        }

        public void Run()
        {
            IJobExtensions.Run(this);
        }
        public JobHandle Schedule(JobHandle dependsOn = default)
        {
            return IJobExtensions.Schedule(this, dependsOn);
        }
    }

    /// <summary>
    /// Struct describing where in each of the arrays (vertices, triangles,materials) the 
    /// collidable part of the mesh ends.
    /// </summary>
    public struct CollisionSubmeshDescriptor : IDisposable
    {
        //Single element arrays to be passed to later jobs
        public NativeArray<int> collisionMeshLengthVertices;
        public NativeArray<int> collisionMeshLengthTriangleIndices;
        public NativeArray<int> collisionMeshMaterialRunLength;

        public CollisionSubmeshDescriptor(Allocator allocator = Allocator.Persistent)
        {
            collisionMeshLengthVertices = new NativeArray<int>(1, allocator);
            collisionMeshLengthTriangleIndices = new NativeArray<int>(1, allocator);
            collisionMeshMaterialRunLength = new NativeArray<int>(1, allocator);
        }

        public void Record(int verticesLength, int trianglesLength, int materialsLength)
        {
            collisionMeshLengthVertices[0] = verticesLength;
            collisionMeshLengthTriangleIndices[0] = trianglesLength;
            collisionMeshMaterialRunLength[0] = materialsLength;
        }

        public void Dispose()
        {
            collisionMeshLengthVertices.SmartDispose();
            collisionMeshLengthTriangleIndices.SmartDispose();
            collisionMeshMaterialRunLength.SmartDispose();
        }
    }
}