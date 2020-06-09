﻿using UnityEngine;
using System.Collections;

namespace UniVox.Framework
{
    public interface IChunkMesher<ChunkDataType, V> where ChunkDataType : IChunkData<V> where V : IVoxelData
    {
        bool IsMeshDependentOnNeighbourChunks { get; }
        Mesh CreateMesh(ChunkDataType chunk);
    }
}