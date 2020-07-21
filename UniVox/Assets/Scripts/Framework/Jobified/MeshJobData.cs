using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework.Lighting;

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

        public LightValue GetLightValue(int3 pos) 
        {
            if (pos.x >= 0 && pos.x < dimensions.x &&
                pos.y >= 0 && pos.y < dimensions.y &&
                pos.z >= 0 && pos.z < dimensions.z)
            {//Inside chunk
                return lights[ Utils.Helpers.MultiIndexToFlat(pos.x, pos.y, pos.z, dimensions)];
            }
            else
            {
                return new LightValue();//TODO replace with lv in neighbour
            }
        }
    }
}