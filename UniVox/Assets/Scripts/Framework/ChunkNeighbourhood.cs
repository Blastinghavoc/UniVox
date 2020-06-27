using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniVox.Framework
{
    public class ChunkNeighbourhood
    {
        private Dictionary<Vector3Int, IChunkData> data;

        public ChunkNeighbourhood(Vector3Int center, Func<Vector3Int, IChunkData> getData,bool includeDiagonals = false) 
        {
            data = new Dictionary<Vector3Int, IChunkData>();

            Func<Vector3Int,IEnumerable<Vector3Int>> neighbourIdGenerator = Utils.Helpers.GetNeighboursDirectOnly;

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