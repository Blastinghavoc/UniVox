using UnityEngine;
using System.Collections;
using Unity.Burst;
using Unity.Collections;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Unity.Mathematics;
using System;
using static UniVox.Framework.VoxelTypeManager;
using Utils;

namespace UniVox.Framework.Jobified
{
    /// <summary>
    /// flattened and indexed collections of mesh definitions for use by meshing jobs
    /// </summary>
    public struct NativeMeshDatabase: IDisposable
    {
        /// <summary>
        /// All nodes used in all the different mesh types
        /// </summary>
        [ReadOnly] public NativeArray<Node> allMeshNodes;

        /// <summary>
        /// Start and end ranges of nodes for each of the 6 faces of all the
        /// mesh types. e.g, mesh type 0 uses the first 6 entries of this
        /// array to index the nodes used for its faces.
        /// </summary>
        [ReadOnly] public NativeArray<StartEnd> nodesUsedByFaces;

        /// <summary>
        /// Indexes of triangles used for each face, restarting at 0 for each face,
        /// grouped in ranges of mesh type.
        /// </summary>
        [ReadOnly] public NativeArray<int> allRelativeTriangles;

        /// <summary>
        /// Ranges into the relative triangles array for each face of a mesh type.
        /// Each mesh type has 6 contiguous entries in here.
        /// </summary>
        [ReadOnly] public NativeArray<StartEnd> relativeTrianglesByFaces;

        /// <summary>
        /// Whether or not each of the 6 faces of a mesh are solid,
        /// packed so each mesh type has 6 contiguous entries here.
        /// </summary>
        [ReadOnly] public NativeArray<bool> isFaceSolid;

        /// <summary>
        /// Defines the ranges into the "by faces" arrays for each mesh type.
        /// </summary>
        [ReadOnly] public NativeArray<StartEnd> meshTypeRanges;

        /// <summary>
        /// Array mapping voxel types (index) to mesh type, which is an index into the
        /// meshTypeRanges array.
        /// </summary>
        [ReadOnly] public NativeArray<int> voxelTypeToMeshTypeMap;        

        /// <summary>
        /// Array mapping voxel types (index) to material ID
        /// </summary>
        [ReadOnly] public NativeArray<ushort> voxelTypeToMaterialIDMap;

        /// <summary>
        /// Array mapping mesh id (index) to a bool indicating whether or not to 
        /// include backfaces.
        /// </summary>
        [ReadOnly] public NativeArray<bool> meshIdToIncludeBackfacesMap;

        public void Dispose()
        {
            allMeshNodes.SmartDispose();
            nodesUsedByFaces.SmartDispose();
            allRelativeTriangles.SmartDispose();
            relativeTrianglesByFaces.SmartDispose();
            isFaceSolid.SmartDispose();
            meshTypeRanges.SmartDispose();
            voxelTypeToMeshTypeMap.SmartDispose();
            voxelTypeToMaterialIDMap.SmartDispose();
            meshIdToIncludeBackfacesMap.SmartDispose();
        }
    }

    public static class NativeMeshDatabaseExtensions
    {
        /// <summary>
        /// Generate the database from a list of type data
        /// </summary>
        public static NativeMeshDatabase FromTypeData(List<VoxelTypeData> typeData)
        {
            List<Node> allMeshNodesList = new List<Node>();
            List<StartEnd> nodesUsedByFacesList = new List<StartEnd>();
            List<int> allRelativeTrianglesList = new List<int>();
            List<StartEnd> relativeTrianglesByFacesList = new List<StartEnd>();
            List<bool> isFaceSolidList = new List<bool>();

            List<StartEnd> meshTypeRangesList = new List<StartEnd>();
            List<int> voxelTypeToMeshTypeMapList = new List<int>();
            List<ushort> voxelTypeToMaterialIDMapList = new List<ushort>();

            List<bool> meshIdToIncludeBackfacesMapList = new List<bool>();

            Dictionary<SOMeshDefinition, int> uniqueMeshIDs = new Dictionary<SOMeshDefinition, int>();

            //AIR
            voxelTypeToMeshTypeMapList.Add(0);
            voxelTypeToMaterialIDMapList.Add(0);            

            for (ushort voxelId = 1; voxelId < typeData.Count; voxelId++)
            {
                var item = typeData[voxelId];


                var SODef = item.definition.meshDefinition;
                if (!uniqueMeshIDs.TryGetValue(SODef, out var meshID))
                {
                    //New Unique mesh defintion
                    meshID = meshTypeRangesList.Count;
                    uniqueMeshIDs.Add(SODef, meshID);
                    StartEnd meshTypeRange = new StartEnd();
                    meshTypeRange.start = nodesUsedByFacesList.Count;                    

                    Flatten(SODef, allMeshNodesList, nodesUsedByFacesList, allRelativeTrianglesList, relativeTrianglesByFacesList, isFaceSolidList);

                    meshTypeRange.end = nodesUsedByFacesList.Count;
                    meshTypeRangesList.Add(meshTypeRange);
                    meshIdToIncludeBackfacesMapList.Add(SODef.includeBackfaces);
                }
                voxelTypeToMeshTypeMapList.Add(meshID);
                voxelTypeToMaterialIDMapList.Add(item.materialID);
            }

            NativeMeshDatabase nativeMeshDatabase = new NativeMeshDatabase();
            nativeMeshDatabase.allMeshNodes = new NativeArray<Node>(allMeshNodesList.ToArray(), Allocator.Persistent);
            nativeMeshDatabase.nodesUsedByFaces = new NativeArray<StartEnd>(nodesUsedByFacesList.ToArray(), Allocator.Persistent);
            nativeMeshDatabase.allRelativeTriangles = new NativeArray<int>(allRelativeTrianglesList.ToArray(), Allocator.Persistent);
            nativeMeshDatabase.relativeTrianglesByFaces = new NativeArray<StartEnd>(relativeTrianglesByFacesList.ToArray(), Allocator.Persistent);
            nativeMeshDatabase.isFaceSolid = new NativeArray<bool>(isFaceSolidList.ToArray(), Allocator.Persistent);
            nativeMeshDatabase.meshTypeRanges = new NativeArray<StartEnd>(meshTypeRangesList.ToArray(), Allocator.Persistent);
            nativeMeshDatabase.voxelTypeToMeshTypeMap = new NativeArray<int>(voxelTypeToMeshTypeMapList.ToArray(), Allocator.Persistent);
            nativeMeshDatabase.voxelTypeToMaterialIDMap = new NativeArray<ushort>(voxelTypeToMaterialIDMapList.ToArray(), Allocator.Persistent);
            nativeMeshDatabase.meshIdToIncludeBackfacesMap = meshIdToIncludeBackfacesMapList.ToArray().ToNative(Allocator.Persistent);

            return nativeMeshDatabase;
        }

        /// <summary>
        /// Flatten a mesh definition into the working lists
        /// </summary>
        /// <param name="def"></param>
        /// <param name="allMeshNodesList"></param>
        /// <param name="nodesUsedByFacesList"></param>
        /// <param name="allRelativeTrianglesList"></param>
        /// <param name="relativeTrianglesByFacesList"></param>
        /// <param name="isFaceSolidList"></param>
        private static void Flatten(SOMeshDefinition def,
            List<Node> allMeshNodesList,
            List<StartEnd> nodesUsedByFacesList,
            List<int> allRelativeTrianglesList,
            List<StartEnd> relativeTrianglesByFacesList,
            List<bool> isFaceSolidList
            )
        {
            for (int i = 0; i < def.Faces.Length; i++)
            {
                var faceDef = def.Faces[i];
                isFaceSolidList.Add(faceDef.isSolid);

                StartEnd nodesUsedIndexers = new StartEnd();
                nodesUsedIndexers.start = allMeshNodesList.Count;

                //Add all used nodes
                for (int j = 0; j < faceDef.UsedVertices.Length; j++)
                {
                    Node node = new Node()
                    {
                        vertex = def.AllVertices[faceDef.UsedVertices[j]],
                        uv = def.AllUvs[faceDef.UsedUvs[j]],
                        normal = def.AllNormals[faceDef.UsedNormals[j]]
                    };

                    allMeshNodesList.Add(node);
                }

                nodesUsedIndexers.end = allMeshNodesList.Count;
                nodesUsedByFacesList.Add(nodesUsedIndexers);

                StartEnd trianglesIndexers = new StartEnd();
                trianglesIndexers.start = allRelativeTrianglesList.Count;
                //Add all relative triangles
                for (int j = 0; j < faceDef.Triangles.Length; j++)
                {
                    allRelativeTrianglesList.Add(faceDef.Triangles[j]);
                }
                trianglesIndexers.end = allRelativeTrianglesList.Count;

                relativeTrianglesByFacesList.Add(trianglesIndexers);
            }
        }        
    }

    public struct NativeVoxelTypeDatabase 
    {
        [ReadOnly] public NativeArray<float> zIndicesPerFace;
        [ReadOnly] public NativeArray<StartEnd> voxelTypeToZIndicesRangeMap;
        /// <summary>
        /// Whether or not each voxel type is passable (should be included in collision mesh)
        /// </summary>
        [ReadOnly] public NativeArray<bool> voxelTypeToIsPassableMap;
    }

    public static class NativeVoxelTypeDatabaseExtensions 
    {
        public static NativeVoxelTypeDatabase FromTypeData(List<VoxelTypeData> typeData) 
        {
            List<float> zIndicesPerFaceList = new List<float>();
            List<StartEnd> voxelTypeToZIndicesRangeMapList = new List<StartEnd>();
            List<bool> voxelTypeToIsPassableMapList = new List<bool>();

            //AIR
            voxelTypeToZIndicesRangeMapList.Add(new StartEnd());
            voxelTypeToIsPassableMapList.Add(true);

            for (int i = 1; i < typeData.Count; i++)
            {
                StartEnd range = new StartEnd();
                range.start = zIndicesPerFaceList.Count;

                var zIndices = typeData[i].zIndicesPerFace;
                zIndicesPerFaceList.AddRange(zIndices);

                range.end = zIndicesPerFaceList.Count;
                voxelTypeToZIndicesRangeMapList.Add(range);
                voxelTypeToIsPassableMapList.Add(typeData[i].definition.isPassable);
            }

            NativeVoxelTypeDatabase database = new NativeVoxelTypeDatabase();
            database.zIndicesPerFace = new NativeArray<float>(zIndicesPerFaceList.ToArray(), Allocator.Persistent);
            database.voxelTypeToZIndicesRangeMap = new NativeArray<StartEnd>(voxelTypeToZIndicesRangeMapList.ToArray(), Allocator.Persistent);
            database.voxelTypeToIsPassableMap = new NativeArray<bool>(voxelTypeToIsPassableMapList.ToArray(), Allocator.Persistent);
            return database;
        }

        public static void Dispose(ref this NativeVoxelTypeDatabase database) 
        {
            database.voxelTypeToZIndicesRangeMap.SmartDispose();
            database.zIndicesPerFace.SmartDispose();
            database.voxelTypeToIsPassableMap.SmartDispose();
        }
    }

    [BurstCompile]
    public struct NeighbourData
    {
        [ReadOnly] public NativeArray<VoxelTypeID> up;
        [ReadOnly] public NativeArray<VoxelTypeID> down;
        [ReadOnly] public NativeArray<VoxelTypeID> north;
        [ReadOnly] public NativeArray<VoxelTypeID> south;
        [ReadOnly] public NativeArray<VoxelTypeID> east;
        [ReadOnly] public NativeArray<VoxelTypeID> west;

        public NativeArray<VoxelTypeID> this[int i] 
        {
            get {
                switch (i)
                {
                    case Directions.UP:
                        return up;
                    case Directions.DOWN:
                        return down;
                    case Directions.NORTH:
                        return north;
                    case Directions.SOUTH:
                        return south;
                    case Directions.EAST:
                        return east;
                    case Directions.WEST:
                        return west;
                    default:                        
                        throw new Exception($"direction {i} was not recognised");
                }
            }
        }
    }

    public static class NeighbourDataExtensions 
    {
        public static void Dispose(ref this NeighbourData neighbourData)
        {
            neighbourData.up.SmartDispose();
            neighbourData.down.SmartDispose();
            neighbourData.north.SmartDispose();
            neighbourData.south.SmartDispose();
            neighbourData.east.SmartDispose();
            neighbourData.west.SmartDispose();
        }

        public static void Add(ref this NeighbourData container, int direction, NativeArray<VoxelTypeID> data)
        {
            switch (direction)
            {
                case Directions.UP:
                    container.up = data;
                    break;
                case Directions.DOWN:
                    container.down = data;
                    break;
                case Directions.NORTH:
                    container.north = data;
                    break;
                case Directions.SOUTH:
                    container.south = data;
                    break;
                case Directions.EAST:
                    container.east = data;
                    break;
                case Directions.WEST:
                    container.west = data;
                    break;
                default:
                    throw new ArgumentException($"direction {direction} was not recognised");
            }
        }
    }

    /// <summary>
    /// A unique mesh node
    /// </summary>
    public struct Node 
    {
        public float3 vertex;
        public float2 uv;
        public float3 normal;
    }

    [BurstCompile]
    public struct StartEnd 
    {
        public int start;
        public int end;//Exclusive

        public int Length { get => end - start; }

        public StartEnd(int start, int end) 
        {
            this.start = start;
            this.end = end;
        }
    }
}