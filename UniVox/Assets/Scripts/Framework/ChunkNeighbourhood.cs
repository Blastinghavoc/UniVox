using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UniVox.Framework.Lighting;

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

        private Func<Vector3Int, IChunkData> getData;

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

        public VoxelTypeID GetVoxel(int x, int y, int z) 
        {
            var chunkData = extendedIndex(ref x, ref y, ref z);
            return chunkData[x, y, z];
        }

        public void SetVoxel(int x, int y, int z,VoxelTypeID value)
        {
            var chunkData = extendedIndex(ref x, ref y, ref z);
            chunkData[x, y, z] = value;
        }

        public LightValue GetLightValue(int x, int y, int z)
        {
            var chunkData = extendedIndex(ref x, ref y, ref z);
            return chunkData.lightChunk[x,y,z];
        }

        public void SetLightValue(int x, int y, int z, LightValue value)
        {
            var chunkData = extendedIndex(ref x, ref y, ref z);
            chunkData.lightChunk[x,y,z] = value;
        }

        public void SetIfUnoccupied(int x, int y, int z,VoxelTypeID typeID) 
        {
            var chunkData = extendedIndex(ref x,ref y,ref z);
            if (chunkData[x,y,z] == VoxelTypeID.AIR_ID)
            {
                chunkData[x, y, z] = typeID;
            }
        }

        public ChunkNeighbourhood(IChunkData center, Func<Vector3Int, IChunkData> getData) 
        {
            data = new Dictionary<Vector3Int, IChunkData>();
            this.center = center;
            this.getData = getData;

        }

        public ChunkNeighbourhood(Vector3Int center, Func<Vector3Int, IChunkData> getData)
        {
            Profiler.BeginSample("CreateChunkNeighbourhood");
            data = new Dictionary<Vector3Int, IChunkData>();

            this.center = getData(center);
            this.getData = getData;

            //Func<Vector3Int, IEnumerable<Vector3Int>> neighbourIdGenerator = Utils.Helpers.GetNeighboursDirectOnly;

            //if (includeDiagonals)
            //{
            //    neighbourIdGenerator = Utils.Helpers.GetNeighboursIncludingDiagonal;                
            //}

            //this.center = getData(center);

            //foreach (var item in neighbourIdGenerator(center))
            //{
            //    data.Add(item, getData(item));
            //}
            Profiler.EndSample();
        }

        public List<Vector3Int> GetAllUsedNeighbourIds() 
        {
            return data.Keys.ToList();
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
                if (data.TryGetValue(ChunkId,out var chunkData))
                {
                    return chunkData;
                }
                else
                {
                    chunkData = getData(ChunkId);
                    data[ChunkId] = chunkData;
                    return chunkData;
                }
            }
            return center;
        }
    }
}