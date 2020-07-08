using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

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

        [ReadOnly] public NeighbourData neighbourData;//neighbour voxel data

        [ReadOnly] public NativeMeshDatabase meshDatabase;

        [ReadOnly] public NativeVoxelTypeDatabase voxelTypeDatabase;

        //Outputs
        public NativeList<Vector3> vertices;
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
                           NeighbourData neighbourData,
                           NativeMeshDatabase meshDatabase,
                           NativeVoxelTypeDatabase voxelTypeDatabase,
                           Allocator allocator)
        {
            this.dimensions = dimensions;
            this.voxels = voxels;
            this.rotatedVoxels = rotatedVoxels;
            this.neighbourData = neighbourData;
            this.meshDatabase = meshDatabase;
            this.voxelTypeDatabase = voxelTypeDatabase;
            vertices = new NativeList<Vector3>(allocator);
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
            neighbourData.Dispose();

            vertices.Dispose();
            uvs.Dispose();
            normals.Dispose();
            allTriangleIndices.Dispose();

            materialRuns.Dispose();
            collisionSubmesh.Dispose();
        }
    }
}