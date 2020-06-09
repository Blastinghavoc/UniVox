using UnityEngine;
using System.Collections;
using System;

namespace UniVox.Framework
{

    /// <summary>
    /// The Component managing the operation of a Chunk GameObject
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public abstract class AbstractChunkComponent<ChunkDataType, VoxelDataType> : MonoBehaviour
        where ChunkDataType : IChunkData<VoxelDataType>
        where VoxelDataType : IVoxelData
    {
        public ChunkDataType Data;
        public Vector3Int ChunkID { get; private set; }

        public MeshFilter meshFilter;
        public MeshCollider meshCollider;

        //DEBUG
        public bool inMeshRadius = false;

        public void Initialise(Vector3Int id, Vector3 position) 
        {
            ChunkID = id;
            this.name = $"Chunk ({id.x},{id.y},{id.z})";
            transform.position = position;

            SetRenderMesh(null);
            SetCollisionMesh(null);
        }

        public void SetRenderMesh(Mesh mesh) 
        {
            meshFilter.mesh = mesh;
        }

        public void SetCollisionMesh(Mesh mesh) 
        {
            meshCollider.sharedMesh = mesh;
        }
        public void RemoveRenderMesh() 
        {
            meshFilter.mesh = null;
        }

        public void RemoveCollisionMesh() 
        {
            meshCollider.sharedMesh = null;
        }

    }
}