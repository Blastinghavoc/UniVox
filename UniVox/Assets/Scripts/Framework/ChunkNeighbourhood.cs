using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace UniVox.Framework
{
    public struct NativeChunkData 
    {
        public NativeArray<VoxelTypeID> voxelTypes;
        public NativeHashMap<int, VoxelRotation> rotationData;

    }

    //TODO remove if not doing job-based structure gen. Otherwise, reorder to be consistent with DiagonalDirections enum
    public struct NativeChunkNeighbourhood 
    {
        public NativeChunkData up;
        public NativeChunkData down;
        public NativeChunkData north;
        public NativeChunkData south;
        public NativeChunkData east;
        public NativeChunkData west;
        public NativeChunkData NE;
        public NativeChunkData NW;
        public NativeChunkData SE;
        public NativeChunkData SW;
        public NativeChunkData upN;
        public NativeChunkData upS;
        public NativeChunkData upE;
        public NativeChunkData upW;
        public NativeChunkData downN;
        public NativeChunkData downS;
        public NativeChunkData downE;
        public NativeChunkData downW;
        public NativeChunkData upNE;
        public NativeChunkData upNW;
        public NativeChunkData upSE;
        public NativeChunkData upSW;
        public NativeChunkData downNE;
        public NativeChunkData downNW;
        public NativeChunkData downSE;
        public NativeChunkData downSW;

    }

    public class ChunkNeighbourhood
    {
        private Dictionary<Vector3Int, IChunkData> data;

        public IChunkData center;

        public bool HasDiagonals { get; private set; }

        public VoxelTypeID this[int x, int y, int z] {
            get {
                var chunkData = extendedIndex(ref x, ref y, ref z);
                return chunkData[x, y, z];
            }
            set {
                var chunkData = extendedIndex(ref x, ref y, ref z);
                chunkData[x, y, z] = value;
            }
        }

        public void SetIfUnoccupied(int x, int y, int z,VoxelTypeID typeID) 
        {
            var chunkData = extendedIndex(ref x,ref y,ref z);
            if (chunkData[x,y,z] == VoxelTypeManager.AIR_ID)
            {
                chunkData[x, y, z] = typeID;
            }
        }

        public ChunkNeighbourhood(Vector3Int center, Func<Vector3Int, IChunkData> getData, bool includeDiagonals = false)
        {
            data = new Dictionary<Vector3Int, IChunkData>();

            Func<Vector3Int, IEnumerable<Vector3Int>> neighbourIdGenerator = Utils.Helpers.GetNeighboursDirectOnly;

            HasDiagonals = includeDiagonals;

            if (includeDiagonals)
            {
                neighbourIdGenerator = Utils.Helpers.GetNeighboursIncludingDiagonal;                
            }

            this.center = getData(center);

            foreach (var item in neighbourIdGenerator(center))
            {
                data.Add(item, getData(item));
            }
        }

        private IChunkData extendedIndex(ref int x,ref int y,ref int z) 
        {
            var ChunkId = center.ChunkID;
            var dimensions = center.Dimensions;
            if (x < 0)
            {
                ChunkId.x--;
                x += dimensions.x;
            }
            else if (x >= dimensions.x)
            {
                ChunkId.x++;
                x -= dimensions.x;
            }

            if (y < 0)
            {
                ChunkId.y--;
                y += dimensions.y;
            }
            else if (y >= dimensions.y)
            {
                ChunkId.y++;
                y -= dimensions.y;
            }

            if (z < 0)
            {
                ChunkId.z--;
                z += dimensions.z;
            }
            else if (z >= dimensions.z)
            {
                ChunkId.z++;
                z -= dimensions.z;
            }

            if (ChunkId!= center.ChunkID)
            {
                return data[ChunkId];
            }
            return center;
        }
    }
}