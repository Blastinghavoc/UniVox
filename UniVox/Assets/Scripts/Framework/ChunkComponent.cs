using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using UniVox.Framework.ChunkPipeline;

namespace UniVox.Framework
{

    /// <summary>
    /// The Component managing the operation of a Chunk GameObject
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class ChunkComponent : MonoBehaviour, 
        IChunkComponent
    {
        public IChunkData Data { get; set; }
        public Vector3Int ChunkID { get; private set; }

        public MeshFilter meshFilter;
        public MeshCollider meshCollider;
        public MeshRenderer meshRenderer;

        public MeshDescriptor meshDescriptor { get; protected set; }

        public void Initialise(Vector3Int id, Vector3 position)
        {
            ChunkID = id;
            this.name = $"Chunk ({id.x},{id.y},{id.z})";
            transform.position = position;

            SetRenderMesh(null);
            SetCollisionMesh(null);
        }

        public Mesh GetRenderMesh() 
        {
            return meshFilter.mesh;
        }

        public void SetRenderMesh(MeshDescriptor meshDesc)
        {
            if (meshDesc == null)
            {
                meshFilter.mesh = null;
                return;
            }
            meshFilter.mesh = meshDesc.mesh;
            meshDescriptor = meshDesc;
            if (meshDesc.materialsBySubmesh!=null)
            {
                meshRenderer.materials = meshDesc.materialsBySubmesh;
            }
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

        //TODO remove DEBUG
        public ChunkStageData stageData;
        public void SetPipelineStagesDebug(ChunkStageData chunkStageData)
        {
            stageData = new ChunkStageData()
            {
                maxStage = chunkStageData.maxStage,
                minStage = chunkStageData.minStage,
                targetStage = chunkStageData.targetStage
            };
        }
    }

    public class MeshDescriptor 
    {
        public Mesh mesh;
        public Material[] materialsBySubmesh;
        public int collidableLengthVertices;
        public int collidableLengthIndices;
    }
}