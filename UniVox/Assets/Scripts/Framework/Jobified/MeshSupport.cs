using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UniVox.Framework.Common;
using UniVox.Framework.Lighting;
using Utils;
using static UniVox.Framework.VoxelTypeManager;

namespace UniVox.Framework.Jobified
{
    /// <summary>
    /// flattened and indexed collections of mesh definitions for use by meshing jobs
    /// </summary>
    public struct NativeMeshDatabase : IDisposable
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
        [ReadOnly] public NativeArray<StartEndRange> nodesUsedByFaces;

        /// <summary>
        /// Indexes of triangles used for each face, restarting at 0 for each face,
        /// grouped in ranges of mesh type.
        /// </summary>
        [ReadOnly] public NativeArray<int> allRelativeTriangles;

        /// <summary>
        /// Ranges into the relative triangles array for each face of a mesh type.
        /// Each mesh type has 6 contiguous entries in here.
        /// </summary>
        [ReadOnly] public NativeArray<StartEndRange> relativeTrianglesByFaces;

        /// <summary>
        /// Whether or not each of the 6 faces of a mesh are solid,
        /// packed so each mesh type has 6 contiguous entries here.
        /// </summary>
        [ReadOnly] public NativeArray<bool> isFaceSolid;

        /// <summary>
        /// Defines the ranges into the "by faces" arrays for each mesh type.
        /// </summary>
        [ReadOnly] public NativeArray<StartEndRange> meshTypeRanges;

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
            List<StartEndRange> nodesUsedByFacesList = new List<StartEndRange>();
            List<int> allRelativeTrianglesList = new List<int>();
            List<StartEndRange> relativeTrianglesByFacesList = new List<StartEndRange>();
            List<bool> isFaceSolidList = new List<bool>();

            List<StartEndRange> meshTypeRangesList = new List<StartEndRange>();
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
                    StartEndRange meshTypeRange = new StartEndRange();
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
            nativeMeshDatabase.nodesUsedByFaces = new NativeArray<StartEndRange>(nodesUsedByFacesList.ToArray(), Allocator.Persistent);
            nativeMeshDatabase.allRelativeTriangles = new NativeArray<int>(allRelativeTrianglesList.ToArray(), Allocator.Persistent);
            nativeMeshDatabase.relativeTrianglesByFaces = new NativeArray<StartEndRange>(relativeTrianglesByFacesList.ToArray(), Allocator.Persistent);
            nativeMeshDatabase.isFaceSolid = new NativeArray<bool>(isFaceSolidList.ToArray(), Allocator.Persistent);
            nativeMeshDatabase.meshTypeRanges = new NativeArray<StartEndRange>(meshTypeRangesList.ToArray(), Allocator.Persistent);
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
            List<StartEndRange> nodesUsedByFacesList,
            List<int> allRelativeTrianglesList,
            List<StartEndRange> relativeTrianglesByFacesList,
            List<bool> isFaceSolidList
            )
        {
            for (int i = 0; i < def.Faces.Length; i++)
            {
                var faceDef = def.Faces[i];
                isFaceSolidList.Add(faceDef.isSolid);

                StartEndRange nodesUsedIndexers = new StartEndRange();
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

                StartEndRange trianglesIndexers = new StartEndRange();
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
        [ReadOnly] public NativeArray<StartEndRange> voxelTypeToZIndicesRangeMap;
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
            List<StartEndRange> voxelTypeToZIndicesRangeMapList = new List<StartEndRange>();
            List<bool> voxelTypeToIsPassableMapList = new List<bool>();

            //AIR
            voxelTypeToZIndicesRangeMapList.Add(new StartEndRange());
            voxelTypeToIsPassableMapList.Add(true);

            for (int i = 1; i < typeData.Count; i++)
            {
                StartEndRange range = new StartEndRange();
                range.start = zIndicesPerFaceList.Count;

                var zIndices = typeData[i].zIndicesPerFace;
                zIndicesPerFaceList.AddRange(zIndices);

                range.end = zIndicesPerFaceList.Count;
                voxelTypeToZIndicesRangeMapList.Add(range);
                voxelTypeToIsPassableMapList.Add(typeData[i].definition.isPassable);
            }

            NativeVoxelTypeDatabase database = new NativeVoxelTypeDatabase();
            database.zIndicesPerFace = new NativeArray<float>(zIndicesPerFaceList.ToArray(), Allocator.Persistent);
            database.voxelTypeToZIndicesRangeMap = new NativeArray<StartEndRange>(voxelTypeToZIndicesRangeMapList.ToArray(), Allocator.Persistent);
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
    public struct NeighbourData : IDisposable
    {
        [ReadOnly] public NativeArray<VoxelTypeID> up;
        [ReadOnly] public NativeArray<VoxelTypeID> down;
        [ReadOnly] public NativeArray<VoxelTypeID> north;
        [ReadOnly] public NativeArray<VoxelTypeID> south;
        [ReadOnly] public NativeArray<VoxelTypeID> east;
        [ReadOnly] public NativeArray<VoxelTypeID> west;

        [ReadOnly] public NativeArray<LightValue> upLight;
        [ReadOnly] public NativeArray<LightValue> downLight;
        [ReadOnly] public NativeArray<LightValue> northLight;
        [ReadOnly] public NativeArray<LightValue> southLight;
        [ReadOnly] public NativeArray<LightValue> eastLight;
        [ReadOnly] public NativeArray<LightValue> westLight;        

        public NativeArray<VoxelTypeID> GetVoxels(Direction dir) 
        {
            switch (dir)
            {
                case Direction.up:
                    return up;
                case Direction.down:
                    return down;
                case Direction.north:
                    return north;
                case Direction.south:
                    return south;
                case Direction.east:
                    return east;
                case Direction.west:
                    return west;
                default:
                    throw new Exception($"direction {dir} was not recognised");
            }
        }

        public NativeArray<LightValue> GetLightValues(Direction dir) 
        {
            switch (dir)
            {
                case Direction.up:
                    return upLight;
                case Direction.down:
                    return downLight;
                case Direction.north:
                    return northLight;
                case Direction.south:
                    return southLight;
                case Direction.east:
                    return eastLight;
                case Direction.west:
                    return westLight;
                default:
                    throw new Exception($"direction {dir} was not recognised");
            }
        }

        public void Dispose()
        {
            up.SmartDispose();
            down.SmartDispose();
            north.SmartDispose();
            south.SmartDispose();
            east.SmartDispose();
            west.SmartDispose();

            upLight.SmartDispose();
            downLight.SmartDispose();
            northLight.SmartDispose();
            southLight.SmartDispose();
            eastLight.SmartDispose();
            westLight.SmartDispose();
        }

        public void Add(Direction direction, NativeArray<VoxelTypeID> data)
        {
            switch (direction)
            {
                case Direction.up:
                    up = data;
                    break;
                case Direction.down:
                    down = data;
                    break;
                case Direction.north:
                    north = data;
                    break;
                case Direction.south:
                    south = data;
                    break;
                case Direction.east:
                    east = data;
                    break;
                case Direction.west:
                    west = data;
                    break;
                default:
                    throw new ArgumentException($"direction {direction} was not recognised");
            }
        }

        public void Add(Direction direction, NativeArray<LightValue> lightData)
        {
            switch (direction)
            {
                case Direction.up:
                    upLight = lightData;
                    break;
                case Direction.down:
                    downLight = lightData;
                    break;
                case Direction.north:
                    northLight = lightData;
                    break;
                case Direction.south:
                    southLight = lightData;
                    break;
                case Direction.east:
                    eastLight = lightData;
                    break;
                case Direction.west:
                    westLight = lightData;
                    break;
                default:
                    throw new ArgumentException($"direction {direction} was not recognised");
            }
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
        public void AdjustLocalPos(ref int3 pos, out bool isInChunk, out Direction directionOfNeighbour,int3 dimensions)
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
            if (direction == Direction.east || direction == Direction.west)
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
}