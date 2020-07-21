using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework.Lighting;
using UniVox.Framework.Common;

namespace UniVox.Framework.Jobified
{
    /// <summary>
    /// The minimum data required by a meshing job,
    /// and expected to be present for later jobs.
    /// </summary>
    [BurstCompile]
    public struct MeshJobData : IDisposable
    {
        [ReadOnly] public int3 dimensions;//Dimensions of chunk

        [ReadOnly] public NativeArray<VoxelTypeID> voxels;//Voxel data
        public NativeArray<RotatedVoxelEntry> rotatedVoxels;

        [ReadOnly] public NativeArray<LightValue> lights;//per-voxel light data

        [ReadOnly] public NeighbourData neighbourData;//neighbour voxel data

        [ReadOnly] public NativeMeshDatabase meshDatabase;

        [ReadOnly] public NativeVoxelTypeDatabase voxelTypeDatabase;

        //Outputs
        public NativeList<Vector3> vertices;
        public NativeList<Color> vertexColours;
        public NativeList<Vector3> uvs;
        public NativeList<Vector3> normals;
        public NativeList<int> allTriangleIndices;

        //Material runs to be written to by a run tracker
        public NativeList<MaterialRun> materialRuns;
        //Collision submesh details
        public CollisionSubmeshDescriptor collisionSubmesh;

        public MeshJobData(int3 dimensions,
                           NativeArray<VoxelTypeID> voxels,
                           NativeArray<RotatedVoxelEntry> rotatedVoxels,
                           NativeArray<LightValue> lights,
                           NeighbourData neighbourData,
                           NativeMeshDatabase meshDatabase,
                           NativeVoxelTypeDatabase voxelTypeDatabase,
                           Allocator allocator)
        {
            this.dimensions = dimensions;
            this.voxels = voxels;
            this.rotatedVoxels = rotatedVoxels;
            this.lights = lights;
            this.neighbourData = neighbourData;
            this.meshDatabase = meshDatabase;
            this.voxelTypeDatabase = voxelTypeDatabase;
            vertices = new NativeList<Vector3>(allocator);
            vertexColours = new NativeList<Color>(allocator);
            uvs = new NativeList<Vector3>(allocator);
            normals = new NativeList<Vector3>(allocator);
            allTriangleIndices = new NativeList<int>(allocator);
            materialRuns = new NativeList<MaterialRun>(allocator);
            collisionSubmesh = new CollisionSubmeshDescriptor(allocator);
        }

        public void Dispose()
        {
            voxels.Dispose();
            rotatedVoxels.Dispose();
            lights.Dispose();
            neighbourData.Dispose();

            vertices.Dispose();
            vertexColours.Dispose();
            uvs.Dispose();
            normals.Dispose();
            allTriangleIndices.Dispose();

            materialRuns.Dispose();
            collisionSubmesh.Dispose();
        }

        /// <summary>
        /// Returns true if the position is in this chunk,
        /// false if its in one of the neighbours
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>inChunk</returns>
        private bool TryGetIndexOf(int3 pos,out int index)
        {
            if (pos.x >= 0 && pos.x < dimensions.x &&
                pos.y >= 0 && pos.y < dimensions.y &&
                pos.z >= 0 && pos.z < dimensions.z)
            {//In this chunk
                index = Utils.Helpers.MultiIndexToFlat(pos.x, pos.y, pos.z, dimensions);
                return true;
            }
            index = 0;
            return false;
        }

        /// <summary>
        /// Adjusts the given position to be relative to the
        /// chunk that it's in. First return indicates whether
        /// the pos is in the center chunk, if false the second
        /// return gives the direction of the neighbour chunk.
        /// It is assumed and required that the pos is in either
        /// the center chunk or one of the 6 neighbours.
        /// </summary>
        /// <param name="pos"></param>
        private void AdjustLocalPos(ref int3 pos,out bool isInChunk,out Direction directionOfNeighbour) 
        {
            isInChunk = true;
            directionOfNeighbour = new Direction();

            if (pos.x < 0)
            {
                directionOfNeighbour = Direction.west;
                pos.x += dimensions.x;
                isInChunk = false;                
                return;
            }
            else if (pos.x >= dimensions.x)
            {
                directionOfNeighbour = Direction.east;
                pos.x -= dimensions.x;
                isInChunk = false;
                return;
            }

            if (pos.y < 0)
            {
                directionOfNeighbour = Direction.down;
                pos.y += dimensions.y;
                isInChunk = false;
                return;
            }
            else if (pos.y >= dimensions.y)
            {
                directionOfNeighbour = Direction.up;
                pos.y -= dimensions.y;
                isInChunk = false;
                return;
            }

            if (pos.z < 0)
            {
                directionOfNeighbour = Direction.south;
                pos.z += dimensions.z;
                isInChunk = false;
                return;
            }
            else if (pos.z >= dimensions.z)
            {
                directionOfNeighbour = Direction.north;
                pos.z -= dimensions.z;
                isInChunk = false;
                return;
            }            
            return;
        }

        /// <summary>
        /// Project fullCoords to 2D in the relevant primary axis
        /// </summary>
        public int2 IndicesInNeighbour(int primaryAxis, int3 fullCoords)
        {
            switch (primaryAxis)
            {
                case 0:
                    return new int2(fullCoords.y, fullCoords.z);
                case 1:
                    return new int2(fullCoords.x, fullCoords.z);
                case 2:
                    return new int2(fullCoords.x, fullCoords.y);
                default:
                    throw new Exception("Invalid axis given");
            }
        }

        /// <summary>
        /// As above, but for a direction
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="fullCoords"></param>
        /// <returns></returns>
        public int2 IndicesInNeighbour(Direction direction, int3 fullCoords)
        {
            if (direction == Direction.east || direction == Direction.west )
            {
                return new int2(fullCoords.y, fullCoords.z);
            }
            if (direction == Direction.up || direction == Direction.down)
            {
                return new int2(fullCoords.x, fullCoords.z);
            }
            //if (direction == Direction.north || direction == Direction.south)
            return new int2(fullCoords.x, fullCoords.y);
        }

        public VoxelTypeID GetVoxel(int3 pos) 
        {
            AdjustLocalPos(ref pos,out var InChunk,out var DirectionOfNeighbour);
            if (InChunk)
            {
                return voxels[Utils.Helpers.MultiIndexToFlat(pos.x, pos.y, pos.z, dimensions)];
            }
            else
            {
                var localIndexInNeighbour = IndicesInNeighbour(DirectionOfNeighbour, pos);
                var neighbourDimensions = IndicesInNeighbour(DirectionOfNeighbour, dimensions);

                var flattenedIndex = Utils.Helpers.MultiIndexToFlat(localIndexInNeighbour.x, localIndexInNeighbour.y, neighbourDimensions);

                var neighbourVoxelData = neighbourData.GetVoxels(DirectionOfNeighbour);
                return neighbourVoxelData[flattenedIndex];
            }
        }

        public LightValue GetLightValue(int3 pos) 
        {
            AdjustLocalPos(ref pos, out var InChunk, out var DirectionOfNeighbour);
            if (InChunk)
            {
                return lights[Utils.Helpers.MultiIndexToFlat(pos.x, pos.y, pos.z, dimensions)];
            }
            else
            {
                var localIndexInNeighbour = IndicesInNeighbour(DirectionOfNeighbour, pos);
                var neighbourDimensions = IndicesInNeighbour(DirectionOfNeighbour, dimensions);

                var flattenedIndex = Utils.Helpers.MultiIndexToFlat(localIndexInNeighbour.x, localIndexInNeighbour.y, neighbourDimensions);

                var neighbourLightData = neighbourData.GetLightValues(DirectionOfNeighbour);
                return neighbourLightData[flattenedIndex];
            }            
        }
    }
}