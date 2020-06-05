using UnityEngine;
using System.Collections;

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

        public ChunkStatus status { get; set; }

        public void Initialise(Vector3Int id, Vector3 position) 
        {
            status = ChunkStatus.ReadyForData;
            ChunkID = id;
            this.name = $"Chunk ({id.x},{id.y},{id.z})";
            transform.position = position;
        }

        public void SetMesh(Mesh mesh)
        {
            meshFilter.mesh = mesh;
            meshCollider.sharedMesh = mesh;
        }
    }

    public enum ChunkStatus 
    {
        ReadyForData,
        ScheduledForData,//Pending data
        ReadyForMesh,
        ScheduledForMesh,//Got data, pending mesh
        Complete,
        Deactivated//Chunk has been deactivated and should not be processed for data or meshing
    }
}