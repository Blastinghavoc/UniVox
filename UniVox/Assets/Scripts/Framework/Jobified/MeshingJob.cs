using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Utils;

namespace UniVox.Framework.Jobified
{
    ///Used for run-length encoding the material runs in the triangle indices
    ///for a meshing job
    public struct MaterialRun
    {
        public ushort materialID;
        public StartEnd range;
    }

    [BurstCompile]
    public struct SortIndicesByMaterial : IJob
    {
        [ReadOnly] public NativeArray<int> allTriangleIndices;
        public NativeArray<MaterialRun> materialRuns;
        public NativeList<MaterialRun> packedRuns;
        public NativeList<int> packedIndices;

        //Single element array
        [ReadOnly] public NativeArray<int> collisionMeshMaterialRunLength;

        private struct RunComparer : IComparer<MaterialRun>
        {
            public int Compare(MaterialRun x, MaterialRun y)
            {
                return x.materialID.CompareTo(y.materialID);
            }
        }

        public void Execute()
        {
            var comparer = new RunComparer();
            //Sort the runs by material ID
            //materialRuns.Sort(comparer);

            var collidableRunLength = collisionMeshMaterialRunLength[0];

            var collidableRuns = materialRuns.GetSubArray(0, collidableRunLength);
            collidableRuns.Sort(comparer);
            
            var nonCollidableRuns = materialRuns.GetSubArray(collidableRunLength, materialRuns.Length - collidableRunLength);
            nonCollidableRuns.Sort(comparer);


            //Resize packedIndices list to required capacity
            packedIndices.Capacity = allTriangleIndices.Length;            

            //Apply the ordering of the runs to the triangle indices
            MaterialRun currentPackedRun = new MaterialRun();
            currentPackedRun.materialID = materialRuns[0].materialID;
            currentPackedRun.range.start = 0;
            for (int i = 0; i < materialRuns.Length; i++)
            {
                var run = materialRuns[i];

                if (run.materialID != currentPackedRun.materialID)
                {
                    currentPackedRun.range.end = packedIndices.Length;
                    if (currentPackedRun.range.Length > 0)
                    {
                        packedRuns.Add(currentPackedRun);
                    }
                    currentPackedRun.materialID = run.materialID;
                    currentPackedRun.range.start = packedIndices.Length;
                }

                //Copy ranges into the packedIndices array in order by material
                var allSlice = allTriangleIndices.GetSubArray(run.range.start, run.range.Length);
                packedIndices.AddRange(allSlice);
            }

            currentPackedRun.range.end = packedIndices.Length;
            if (currentPackedRun.range.Length > 0)
            {
                packedRuns.Add(currentPackedRun);
            }
        }
    }

    [BurstCompile]
    public struct MeshingJob : IJob
    {
        [ReadOnly] public bool cullfaces;
        [ReadOnly] public int3 dimensions;
        [ReadOnly] private const int numDirections = Directions.NumDirections;

        [ReadOnly] public NativeArray<VoxelTypeID> voxels;
        public NativeArray<RotatedVoxelEntry> rotatedVoxels;

        [ReadOnly] public NeighbourData neighbourData;

        [ReadOnly] public NativeMeshDatabase meshDatabase;

        [ReadOnly] public NativeVoxelTypeDatabase voxelTypeDatabase;

        [ReadOnly] public NativeDirectionHelper directionHelper;

        //Outputs
        public NativeList<Vector3> vertices;
        public NativeList<Vector3> uvs;
        public NativeList<Vector3> normals;
        public NativeList<int> allTriangleIndices;

        public NativeList<MaterialRun> materialRuns;

        //Single element lists to be passed as deffered to later jobs
        public NativeList<int> collisionMeshLengthVertices;
        public NativeList<int> collisionMeshLengthTriangleIndices;
        public NativeList<int> collisionMeshMaterialRunLength;

        //Private temporaries
        private MaterialRun currentRun;

        private struct DoLater 
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

        public void Execute()
        {
            //Sort the rotated voxels so that they are in the order of iteration
            if (rotatedVoxels.Length > 0)
            {
                rotatedVoxels.Sort(new RotatedVoxelComparer());
            }
            int currentRotatedIndex = 0;

            int currentIndex = 0;//Current index for indices list

            var nonCollidable = new NativeList<DoLater>(Allocator.Temp);

            currentRun = new MaterialRun();

            int i = 0;//current index into voxelData
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        var voxelTypeID = voxels[i];

                        if (voxelTypeID != VoxelTypeManager.AIR_ID)
                        {
                            bool rotated = false;
                            VoxelRotation rotation = default;
                            if (rotatedVoxels.Length > 0 && rotatedVoxels[currentRotatedIndex].flatIndex == i)
                            {
                                //This voxel is rotated
                                rotated = true;
                                rotation = rotatedVoxels[currentRotatedIndex].rotation;
                                AdvanceRotatedIndex(ref currentRotatedIndex);
                            }

                            if (voxelTypeDatabase.voxelTypeToIsPassableMap[voxelTypeID])
                            {
                                //Save non-collidable voxels for later, so that they appear contiguously in the mesh arrays
                                nonCollidable.Add(new DoLater() { position = new int3(x, y, z),
                                    typeID = voxelTypeID,
                                    rotated = rotated,
                                    rotation = rotation
                                });
                            }
                            else
                            {
                                AddMeshDataForVoxel(voxelTypeID, new int3(x, y, z), ref currentIndex,rotated,rotation);
                            }
                        }

                        i++;
                    }
                }
            }
            //Record length of collidable mesh section
            collisionMeshLengthVertices.Add(vertices.Length);
            collisionMeshLengthTriangleIndices.Add(allTriangleIndices.Length);
            currentRun.range.end = allTriangleIndices.Length;
            materialRuns.Add(currentRun);
            collisionMeshMaterialRunLength.Add(materialRuns.Length);

            currentRun.range.start = allTriangleIndices.Length;

            for (int j = 0; j < nonCollidable.Length; j++)
            {
                var item = nonCollidable[j];
                AddMeshDataForVoxel(item.typeID, item.position, ref currentIndex,item.rotated,item.rotation);
            }

            currentRun.range.end = allTriangleIndices.Length;
            materialRuns.Add(currentRun);
        }

        private void AdvanceRotatedIndex(ref int currentRotatedIndex) 
        {
            var nextIndex = currentRotatedIndex + 1;
            if (nextIndex < rotatedVoxels.Length)
            {
                currentRotatedIndex = nextIndex;
            }
        }

        private void AddMeshDataForVoxel(ushort id, int3 position, ref int currentIndex,bool rotated,VoxelRotation rotation)
        {
            var meshID = meshDatabase.voxelTypeToMeshTypeMap[id];
            var faceZRange = voxelTypeDatabase.voxelTypeToZIndicesRangeMap[id];
            var materialID = meshDatabase.voxelTypeToMaterialIDMap[id];

            var meshRange = meshDatabase.meshTypeRanges[meshID];
            

            if (materialID != currentRun.materialID)
            {
                currentRun.range.end = allTriangleIndices.Length;
                materialRuns.Add(currentRun);
                currentRun.materialID = materialID;
                currentRun.range.start = allTriangleIndices.Length;
            }

            
            if (rotated)
            {
                //Add single rotated voxels data
                for (byte dir = 0; dir < numDirections; dir++)
                {
                    var rotatedDirection = directionHelper.GetDirectionAfterRotation(dir, rotation);
                    var faceIsSolid = meshDatabase.isFaceSolid[meshRange.start + rotatedDirection];
                    if (IncludeFace(id, position, rotatedDirection, faceIsSolid))
                    {
                        AddFaceRotated(meshID, voxelTypeDatabase.zIndicesPerFace[faceZRange.start + dir], dir, position, ref currentIndex,rotation);
                    }
                }
            }
            else
            {
                //Add single voxel's data
                for (int dir = 0; dir < numDirections; dir++)
                {
                    var faceIsSolid = meshDatabase.isFaceSolid[meshRange.start + dir];
                    if (IncludeFace(id,position, dir,faceIsSolid))
                    {
                        AddFace(meshID, voxelTypeDatabase.zIndicesPerFace[faceZRange.start + dir], dir, position, ref currentIndex);
                    }
                }
            }
        }

        private void AddFaceRotated(int meshID, float uvZ, int direction, int3 position, ref int currentIndex, VoxelRotation rotation) 
        {
            var meshRange = meshDatabase.meshTypeRanges[meshID];

            var usedNodesSlice = meshDatabase.nodesUsedByFaces[meshRange.start + direction];

            var rotationQuat = directionHelper.GetRotationQuat(rotation);

            //NOTE vertex definitions start from 0,0,0 in the bottom left of the cube, so an offset is needed for rotations
            float3 rotationOffset = new float3(.5f, .5f, .5f);

            //Add all the nodes used by this face
            for (int i = usedNodesSlice.start; i < usedNodesSlice.end; i++)
            {
                var node = meshDatabase.allMeshNodes[i];
                var rotatedVert = math.mul(rotationQuat, node.vertex-rotationOffset)+rotationOffset;
                var adjustedVert = rotatedVert + position;
                vertices.Add(adjustedVert);
                uvs.Add(new float3(node.uv, uvZ));
                var adjustedNorm = math.mul(rotationQuat, node.normal);
                normals.Add(adjustedNorm);
            }

            //Add the triangleIndices used by this face

            var relativeTrianglesSlice = meshDatabase.relativeTrianglesByFaces[meshRange.start + direction];

            for (int i = relativeTrianglesSlice.start; i < relativeTrianglesSlice.end; i++)
            {
                allTriangleIndices.Add(meshDatabase.allRelativeTriangles[i] + currentIndex);
            }

            //Update indexing
            currentIndex += usedNodesSlice.end - usedNodesSlice.start;
        }

        private void AddFace(int meshID, float uvZ, int direction, int3 position, ref int currentIndex)
        {
            var meshRange = meshDatabase.meshTypeRanges[meshID];

            var usedNodesSlice = meshDatabase.nodesUsedByFaces[meshRange.start + direction];

            //Add all the nodes used by this face
            for (int i = usedNodesSlice.start; i < usedNodesSlice.end; i++)
            {
                var node = meshDatabase.allMeshNodes[i];
                vertices.Add(node.vertex + position);
                uvs.Add(new float3(node.uv, uvZ));
                normals.Add(node.normal);
            }

            //Add the triangleIndices used by this face

            var relativeTrianglesSlice = meshDatabase.relativeTrianglesByFaces[meshRange.start + direction];

            for (int i = relativeTrianglesSlice.start; i < relativeTrianglesSlice.end; i++)
            {
                allTriangleIndices.Add(meshDatabase.allRelativeTriangles[i] + currentIndex);
            }

            //Update indexing
            currentIndex += usedNodesSlice.end - usedNodesSlice.start;
        }

        private bool IncludeFace(ushort voxelID,int3 position, int directionIndex,bool faceIsSolid)
        {
            if (!cullfaces)
            {
                return true;
            }
            var directionVector = directionHelper.DirectionVectors[directionIndex];
            var adjacentVoxelIndex = position + directionVector;

            if (TryGetVoxelAt(adjacentVoxelIndex, out var adjacentID))
            {//If adjacent voxel is in the chunk

                if (adjacentID == voxelID)
                {
                    return false;
                }

                return IncludeFaceOfAdjacentWithID(adjacentID, directionIndex);
            }
            else
            {
                //If adjacent voxel is in the neighbouring chunk

                var localIndexOfAdjacentVoxelInNeighbour = DirectionToIndicesInSlice(directionVector, LocalVoxelIndexOfPosition(adjacentVoxelIndex));

                var neighbourChunkData = neighbourData[directionIndex];

                var neighbourDimensions = DirectionToIndicesInSlice(directionVector, dimensions);

                var flattenedIndex = Utils.Helpers.MultiIndexToFlat(localIndexOfAdjacentVoxelInNeighbour.x, localIndexOfAdjacentVoxelInNeighbour.y, neighbourDimensions);

                adjacentID = neighbourChunkData[flattenedIndex];

                if (adjacentID == voxelID)
                {
                    return false;
                }

                return IncludeFaceOfAdjacentWithID(adjacentID, directionIndex);
            }
        }

        private bool IncludeFaceOfAdjacentWithID(ushort voxelTypeID, int direction)
        {
            if (voxelTypeID == VoxelTypeManager.AIR_ID)
            {
                //Include the face if the adjacent voxel is air
                return true;
            }
            var meshID = meshDatabase.voxelTypeToMeshTypeMap[voxelTypeID];
            var meshRange = meshDatabase.meshTypeRanges[meshID];
            var faceIsSolid = meshDatabase.isFaceSolid[meshRange.start + directionHelper.DirectionOpposites[direction]];

            //Exclude this face if adjacent face is solid
            return !faceIsSolid;
        }

        private bool TryGetVoxelAt(int3 pos, out ushort voxelId)
        {
            if (pos.x >= 0 && pos.x < dimensions.x &&
                pos.y >= 0 && pos.y < dimensions.y &&
                pos.z >= 0 && pos.z < dimensions.z)
            {
                voxelId = voxels[Utils.Helpers.MultiIndexToFlat(pos.x, pos.y, pos.z, dimensions)];
                return true;
            }
            voxelId = VoxelTypeManager.AIR_ID;
            return false;
        }

        /// <summary>
        /// Verbose version of ChunkManager's method with the same name.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        private int3 LocalVoxelIndexOfPosition(int3 position)
        {
            var remainder = position % dimensions;

            if (remainder.x < 0)
            {
                remainder.x += dimensions.x;
            }

            if (remainder.y < 0)
            {
                remainder.y += dimensions.y;
            }

            if (remainder.z < 0)
            {
                remainder.z += dimensions.z;
            }

            return remainder;
        }

        /// <summary>
        /// Takes a direction vector, assumed to have just one non-zero element,
        /// and returns the values of fullCoords for the dimensions that are
        /// zero in the direction. I.e, projects fullCoords to 2D in the relevant direction
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        private int2 DirectionToIndicesInSlice(int3 direction, int3 fullCoords)
        {
            if (direction.x != 0)
            {
                return new int2(fullCoords.y, fullCoords.z);
            }
            if (direction.y != 0)
            {
                return new int2(fullCoords.x, fullCoords.z);
            }
            //if (direction.z != 0)
            return new int2(fullCoords.x, fullCoords.y);

        }
    }
}