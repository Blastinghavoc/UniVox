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

        private ChunkStatus _status;
        public ChunkStatus Status { get => _status;
            set { _status = value;  UpdateDataValidity(); }
        }
        public ChunkStatus TargetStatus { get; set; }//The status that the chunk has been requested to be in

        public bool DataValid { get; private set; }

        public void Initialise(Vector3Int id, Vector3 position) 
        {
            Status = ChunkStatus.ReadyForData;
            //Chunks have no target status when initialised
            TargetStatus = ChunkStatus.ReadyForData;
            ChunkID = id;
            this.name = $"Chunk ({id.x},{id.y},{id.z})";
            transform.position = position;

            SetMesh(null);
        }

        public void SetMesh(Mesh mesh)
        {
            meshFilter.mesh = mesh;
            meshCollider.sharedMesh = mesh;
        }

        /// <summary>
        /// Sets the DataValid flag according to the current status
        /// </summary>
        private void UpdateDataValidity() 
        {
            switch (Status)
            {
                case ChunkStatus.ReadyForMesh:
                    DataValid = true;
                    break;
                case ChunkStatus.WaitingForNeighbourData:
                    DataValid = true;
                    break;
                case ChunkStatus.ScheduledForMesh:
                    DataValid = true;
                    break;
                case ChunkStatus.Complete:
                    DataValid = true;
                    break;
                default:
                    DataValid = false;
                    break;
            }
        }
    }

    /// <summary>
    /// NOTE the order of these is important, as these represent the
    /// stages of a Chunk's lifecycle.
    /// </summary>
    public enum ChunkStatus 
    {
        Deactivated,//The chunk has been deactivated and is not valid for any operations
        ReadyForData,
        ScheduledForData,//Pending data, a transient state
        WaitingForNeighbourData,//Has own data, but cannot mesh until neighbours have data, as mesh may be dependent on this data
        ReadyForMesh,//Has Data
        ScheduledForMesh,//Got data, pending mesh, transient state
        Complete,

    }

}