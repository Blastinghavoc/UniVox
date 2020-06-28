using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace UniVox.Framework.Jobified
{
    public struct NativeChunkData 
    {
        public NativeArray<VoxelTypeID> voxelTypes;
        public NativeHashMap<int, VoxelRotation> rotationData;

    }

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

        public ChunkNeighbourhood(Vector3Int center, Func<Vector3Int, IChunkData> getData, bool includeDiagonals = false)
        {
            data = new Dictionary<Vector3Int, IChunkData>();

            Func<Vector3Int, IEnumerable<Vector3Int>> neighbourIdGenerator = Utils.Helpers.GetNeighboursDirectOnly;

            if (includeDiagonals)
            {
                neighbourIdGenerator = Utils.Helpers.GetNeighboursIncludingDiagonal;
            }

            data.Add(center, getData(center));

            foreach (var item in neighbourIdGenerator(center))
            {
                data.Add(item, getData(item));
            }
        }
    }
}