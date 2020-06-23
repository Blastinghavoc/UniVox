using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

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
        public NativeList<int> allTriangleIndices;
        public NativeList<MaterialRun> materialRuns;
        public NativeList<MaterialRun> packedRuns;
        public NativeArray<int> packedIndices;

        private struct RunComparer : IComparer<MaterialRun>
        {
            public int Compare(MaterialRun x, MaterialRun y)
            {
                return x.materialID.CompareTo(y);
            }
        }

        public void Execute()
        {
            var comparer = new RunComparer();
            //Sort the runs by material ID
            materialRuns.Sort(comparer);
            //Apply the ordering of the runs to the triangle indices
            for (int i = 0; i < materialRuns.Length; i++)
            {
                var run = materialRuns[i];
                for (int i = 0; i < length; i++)
                {
                    //WIP
                }
            }
        }
    }

    [BurstCompile]
    public struct MeshingJob<V> : IJob
        where V : struct, IVoxelData
    {
        [ReadOnly] public bool cullfaces;
        [ReadOnly] public int3 dimensions;
        [ReadOnly] private const int numDirections = Directions.NumDirections;

        [ReadOnly] public NativeArray<V> voxels;

        [ReadOnly] public NeighbourData<V> neighbourData;

        [ReadOnly] public NativeMeshDatabase meshDatabase;

        [ReadOnly] public NativeVoxelTypeDatabase voxelTypeDatabase;

        [ReadOnly] public NativeArray<int3> DirectionVectors;
        [ReadOnly] public NativeArray<byte> DirectionOpposites;

        //Outputs
        public NativeList<Vector3> vertices;
        public NativeList<Vector3> uvs;
        public NativeList<Vector3> normals;
        public NativeList<int> allTriangleIndices;

        public NativeList<MaterialRun> materialRuns;

        private MaterialRun currentRun;

        public void Execute()
        {
            int currentIndex = 0;//Current index for indices list

            currentRun = new MaterialRun();

            int i = 0;//current index into voxelData
            for (int z = 0; z < dimensions.z; z++)
            {
                for (int y = 0; y < dimensions.y; y++)
                {
                    for (int x = 0; x < dimensions.x; x++)
                    {
                        var voxelTypeID = voxels[i].TypeID;

                        if (voxelTypeID != VoxelTypeManager.AIR_ID)
                        {
                            AddMeshDataForVoxel(voxelTypeID, new int3(x, y, z), ref currentIndex);
                        }

                        i++;
                    }
                }
            }
            currentRun.range.end = allTriangleIndices.Length;
            materialRuns.Add(currentRun);
        }

        private void AddMeshDataForVoxel(ushort id, int3 position, ref int currentIndex)
        {
            var meshID = meshDatabase.voxelTypeToMeshTypeMap[id];
            var faceZRange = voxelTypeDatabase.voxelTypeToZIndicesRangeMap[id];
            var materialID = meshDatabase.voxelTypeToMaterialIDMap[id];

            if (materialID != currentRun.materialID)
            {
                currentRun.range.end = allTriangleIndices.Length;
                materialRuns.Add(currentRun);
                currentRun.materialID = materialID;
                currentRun.range.start = allTriangleIndices.Length;
            }

            //Add single voxel's data
            for (int i = 0; i < numDirections; i++)
            {
                if (IncludeFace(position, i))
                {
                    AddFace(meshID, voxelTypeDatabase.zIndicesPerFace[faceZRange.start + i], i, position, ref currentIndex);
                }
            }
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

        private bool IncludeFace(int3 position, int directionIndex)
        {
            if (!cullfaces)
            {
                return true;
            }
            var directionVector = DirectionVectors[directionIndex];
            var adjacentVoxelIndex = position + directionVector;

            if (TryGetVoxelAt(adjacentVoxelIndex, out var adjacentID))
            {//If adjacent voxel is in the chunk

                return IncludeFaceOfAdjacentWithID(adjacentID, directionIndex);
            }
            else
            {
                //If adjacent voxel is in the neighbouring chunk

                var localIndexOfAdjacentVoxelInNeighbour = DirectionToIndicesInSlice(directionVector, LocalVoxelIndexOfPosition(adjacentVoxelIndex));

                var neighbourChunkData = neighbourData[directionIndex];

                var neighbourDimensions = DirectionToIndicesInSlice(directionVector, dimensions);

                var flattenedIndex = Utils.Helpers.MultiIndexToFlat(localIndexOfAdjacentVoxelInNeighbour.x, localIndexOfAdjacentVoxelInNeighbour.y, neighbourDimensions);

                return IncludeFaceOfAdjacentWithID(neighbourChunkData[flattenedIndex].TypeID, directionIndex);
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
            var faceIsSolid = meshDatabase.isFaceSolid[meshRange.start + DirectionOpposites[direction]];

            //Exclude this face if adjacent face is solid
            return !faceIsSolid;
        }

        private bool TryGetVoxelAt(int3 pos, out ushort voxelId)
        {
            if (pos.x >= 0 && pos.x < dimensions.x &&
                pos.y >= 0 && pos.y < dimensions.y &&
                pos.z >= 0 && pos.z < dimensions.z)
            {
                voxelId = voxels[Utils.Helpers.MultiIndexToFlat(pos.x, pos.y, pos.z, dimensions)].TypeID;
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