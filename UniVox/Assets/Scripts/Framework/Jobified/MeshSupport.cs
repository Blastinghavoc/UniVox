using UnityEngine;
using System.Collections;
using Unity.Burst;
using Unity.Collections;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Unity.Mathematics;
using System;
using static UniVox.Framework.VoxelTypeManager;

namespace UniVox.Framework.Jobified
{
    /// <summary>
    /// flattened and indexed collections of mesh definitions for use by meshing jobs
    /// </summary>
    public struct NativeMeshDatabase 
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

        public static void Dispose(ref this NativeMeshDatabase database) 
        {
            database.allMeshNodes.SmartDispose();
            database.nodesUsedByFaces.SmartDispose();
            database.allRelativeTriangles.SmartDispose();
            database.relativeTrianglesByFaces.SmartDispose();
            database.isFaceSolid.SmartDispose();
            database.meshTypeRanges.SmartDispose();
            database.voxelTypeToMeshTypeMap.SmartDispose();
            database.voxelTypeToMaterialIDMap.SmartDispose();
        }
    }

    public struct NativeVoxelTypeDatabase 
    {
        [ReadOnly] public NativeArray<float> zIndicesPerFace;
        [ReadOnly] public NativeArray<StartEnd> voxelTypeToZIndicesRangeMap;
    }

    public static class NativeVoxelTypeDatabaseExtensions 
    {
        public static NativeVoxelTypeDatabase FromTypeData(List<VoxelTypeData> typeData) 
        {
            List<float> zIndicesPerFaceList = new List<float>();
            List<StartEnd> voxelTypeToZIndicesRangeMapList = new List<StartEnd>();

            //AIR
            voxelTypeToZIndicesRangeMapList.Add(new StartEnd());

            for (int i = 1; i < typeData.Count; i++)
            {
                StartEnd range = new StartEnd();
                range.start = zIndicesPerFaceList.Count;

                var zIndices = typeData[i].zIndicesPerFace;
                zIndicesPerFaceList.AddRange(zIndices);

                range.end = zIndicesPerFaceList.Count;
                voxelTypeToZIndicesRangeMapList.Add(range);
            }

            NativeVoxelTypeDatabase database = new NativeVoxelTypeDatabase();
            database.zIndicesPerFace = new NativeArray<float>(zIndicesPerFaceList.ToArray(), Allocator.Persistent);
            database.voxelTypeToZIndicesRangeMap = new NativeArray<StartEnd>(voxelTypeToZIndicesRangeMapList.ToArray(), Allocator.Persistent);
            return database;
        }

        public static void Dispose(ref this NativeVoxelTypeDatabase database) 
        {
            database.voxelTypeToZIndicesRangeMap.SmartDispose();
            database.zIndicesPerFace.SmartDispose();
        }
    }

    [BurstCompile]
    public struct NeighbourData<V> where V:struct,IVoxelData 
    {
        [ReadOnly] public NativeArray<V> up;
        [ReadOnly] public NativeArray<V> down;
        [ReadOnly] public NativeArray<V> north;
        [ReadOnly] public NativeArray<V> south;
        [ReadOnly] public NativeArray<V> east;
        [ReadOnly] public NativeArray<V> west;

        public NativeArray<V> this[int i] 
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
                        //NOTE can't throw an exception inside burst code
                        Debug.LogError($"direction {i} was not recognised");
                        return up;
                }
            }
        }
    }

    public static class NeighbourDataExtensions 
    {
        public static void Dispose<V>(ref this NeighbourData<V> neighbourData)
            where V : struct, IVoxelData
        {
            neighbourData.up.SmartDispose();
            neighbourData.down.SmartDispose();
            neighbourData.north.SmartDispose();
            neighbourData.south.SmartDispose();
            neighbourData.east.SmartDispose();
            neighbourData.west.SmartDispose();
        }

        public static void Add<V>(ref this NeighbourData<V> container, int direction, NativeArray<V> data)
            where V : struct, IVoxelData
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
    /// Defines a mesh for a voxel type.
    /// E.g cube
    /// </summary>
    //public struct BurstableMeshDefinition: IDisposable
    //{
    //    //All nodes used by the mesh for all the faces, packed in order of face
    //    public NativeArray<Node> nodes;

    //    //Start-end indices of nodes used for each of the 6 faces
    //    public NativeArray<StartEnd> nodesUsedByFace;

    //    //Indexes of triangles used for each face (starting at 0 for each face), flatpacked
    //    public NativeArray<int> allRelativeTriangles;

    //    public NativeArray<StartEnd> relativeTrianglesByFace;

    //    public NativeArray<bool> isFaceSolid;

    //    public void Dispose()
    //    {
    //        if (nodes.IsCreated)
    //        {
    //            nodes.Dispose();
    //        }
    //        if (nodesUsedByFace.IsCreated)
    //        {
    //            nodesUsedByFace.Dispose();
    //        }
    //        if (allRelativeTriangles.IsCreated)
    //        {
    //            allRelativeTriangles.Dispose();
    //        }
    //        if (relativeTrianglesByFace.IsCreated)
    //        {
    //            relativeTrianglesByFace.Dispose();
    //        }
    //        if (isFaceSolid.IsCreated)
    //        {
    //            isFaceSolid.Dispose();
    //        }
    //    }
    //}

    /// <summary>
    /// A unique mesh node
    /// </summary>
    public struct Node 
    {
        public float3 vertex;
        public float2 uv;
        public float3 normal;
    }

    public struct StartEnd 
    {
        public int start;
        public int end;//Exclusive

        public int Length { get => end - start; }
    }

    //public struct TripleIndex 
    //{
    //    public int vertex;
    //    public int uv;
    //    public int normal;
    //}

    //public static class BurstableMeshExtensions 
    //{
    //    public static BurstableMeshDefinition ToBurst(this SOMeshDefinition def) {
    //        BurstableMeshDefinition burstable = new BurstableMeshDefinition() {     
    //            isFaceSolid = new NativeArray<bool>(def.Faces.Length,Allocator.Persistent),                           
    //        };

    //        List<Node> nodes = new List<Node>();
    //        List<StartEnd> nodesUsedByFace = new List<StartEnd>();
    //        List<int> allRelativeTriangles = new List<int>();
    //        List<StartEnd> relativeTrianglesByFace = new List<StartEnd>();

    //        for (int i = 0; i < def.Faces.Length; i++)
    //        {
    //            var faceDef = def.Faces[i];
    //            burstable.isFaceSolid[i] = faceDef.isSolid;

    //            StartEnd nodesUsedIndexers = new StartEnd();
    //            nodesUsedIndexers.start = nodes.Count;

    //            //Add all used nodes
    //            for (int j = 0; j < faceDef.UsedVertices.Length; j++)
    //            {
    //                Node node = new Node()
    //                {
    //                    vertex = faceDef.UsedVertices[j],
    //                    uv = faceDef.UsedUvs[j],
    //                    normal = faceDef.UsedNormals[j]
    //                };

    //                nodes.Add(node);
    //            }

    //            nodesUsedIndexers.end = nodes.Count;
    //            nodesUsedByFace.Add(nodesUsedIndexers);

    //            StartEnd trianglesIndexers = new StartEnd();
    //            trianglesIndexers.start = allRelativeTriangles.Count;
    //            //Add all relative triangles
    //            for (int j = 0; j < faceDef.Triangles.Length; j++)
    //            {
    //                allRelativeTriangles.Add(faceDef.Triangles[j]);
    //            }
    //            trianglesIndexers.end = allRelativeTriangles.Count;

    //            relativeTrianglesByFace.Add(trianglesIndexers);
    //        }

    //        burstable.nodes = new NativeArray<Node>(nodes.ToArray(), Allocator.Persistent);
    //        burstable.nodesUsedByFace = new NativeArray<StartEnd>(nodesUsedByFace.ToArray(), Allocator.Persistent);
    //        burstable.allRelativeTriangles = new NativeArray<int>(allRelativeTriangles.ToArray(),Allocator.Persistent);
    //        burstable.relativeTrianglesByFace = new NativeArray<StartEnd>(relativeTrianglesByFace.ToArray(), Allocator.Persistent);

    //        return burstable;
    //    }
    //}
}