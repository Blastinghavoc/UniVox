﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UniVox.Framework;
using UniVox.Framework.ChunkPipeline;
using Utils.FSM;
using Utils.Pooling;

public abstract class AbstractChunkManager<ChunkDataType, VoxelDataType> : MonoBehaviour, IChunkManager 
    where ChunkDataType : IChunkData<VoxelDataType>
    where VoxelDataType : IVoxelData
{
    #region Shown in inspector

    #region Runtime Constants
    [SerializeField] private Vector3Int chunkDimensions = new Vector3Int(32, 32, 32);
    public Vector3Int ChunkDimensions { get => chunkDimensions; }

    [SerializeField] private int voxelSize = 1;
    protected int VoxelSize { get => voxelSize; }
    #endregion

    [SerializeField] protected VoxelTypeManager VoxelTypeManager;

    [SerializeField] protected PrefabPool chunkPool = null;

    [SerializeField] protected Rigidbody Player;

    [SerializeField] protected Vector3Int collidableChunksRadii;
    [SerializeField] protected Vector3Int renderedChunksRadii;
    protected Vector3Int dataChunksRadii;

    /// <summary>
    /// Controls how many chunks can be generated and meshed per update
    /// </summary>
    [Range(1, 100)]
    [SerializeField] protected ushort MaxGeneratedPerUpdate = 1;

    /// <summary>
    /// Controls how many chunks can be generated and meshed per update
    /// </summary>
    [Range(1, 100)]
    [SerializeField] protected ushort MaxMeshedPerUpdate = 1;
    #endregion


    public AbstractProviderComponent<ChunkDataType, VoxelDataType> chunkProvider { get; protected set; }
    public AbstractMesherComponent<ChunkDataType, VoxelDataType> chunkMesher { get; protected set; }

    protected Dictionary<Vector3Int, AbstractChunkComponent<ChunkDataType, VoxelDataType>> loadedChunks = new Dictionary<Vector3Int, AbstractChunkComponent<ChunkDataType, VoxelDataType>>();

    //Current chunkID occupied by the Player transform
    protected Vector3Int playerChunkID = Vector3Int.zero;

    private ChunkPipelineManager<ChunkDataType, VoxelDataType> chunkPipeline;
   
    protected virtual void Start()
    {
        Assert.IsTrue(renderedChunksRadii.All((a, b) => a >= b, collidableChunksRadii),
            "The rendering radii must be at least as large as the collidable radii");

        //Chunks can exist as just data one chunk further away than the rendered chunks
        dataChunksRadii = renderedChunksRadii + new Vector3Int(1, 1, 1);

        //Enforce positioning of ChunkManager at the world origin
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.Euler(0, 0, 0);

        Assert.IsNotNull(VoxelTypeManager,"Chunk Manager must have a reference to a Voxel Type Manager");
        VoxelTypeManager.Initialise();

        //Create VoxelWorldInterface
        var worldInterface = gameObject.AddComponent<VoxelWorldInterface>();
        worldInterface.Intialise(this,VoxelTypeManager);

        chunkProvider = GetComponent<AbstractProviderComponent<ChunkDataType, VoxelDataType>>();
        chunkMesher = GetComponent<AbstractMesherComponent<ChunkDataType, VoxelDataType>>();
        Assert.IsNotNull(chunkProvider,"Chunk Manager must have a chunk provider component");
        Assert.IsNotNull(chunkMesher, "Chunk Manager must have a chunk mesher component");

        chunkProvider.Initialise(VoxelTypeManager,this);
        chunkMesher.Initialise(VoxelTypeManager,this);

        chunkPipeline = new ChunkPipelineManager<ChunkDataType, VoxelDataType>(this,
            GetChunkComponent,
            SetTargetStageOfChunk, 
            MaxGeneratedPerUpdate,
            MaxMeshedPerUpdate, 
            MaxMeshedPerUpdate);

        //Immediately request generation of the chunk the player is in
        SetTargetStageOfChunk(WorldToChunkPosition(Player.position), chunkPipeline.CompleteStage);
    }

    protected virtual void Update()
    {
        UpdatePlayerArea();

        chunkPipeline.Update();

        if (!loadedChunks.TryGetValue(playerChunkID, out var chunkComponent) ||
            !chunkPipeline.GetMaxStage(playerChunkID).Equals(chunkPipeline.CompleteStage))
        {
            //Freeze player if the chunk isn't ready for them (doesn't exist or doesn't have collision mesh)
            Player.constraints |= RigidbodyConstraints.FreezePosition;
        }
        else 
        {
            //Remove constraints
            Player.constraints &= ~RigidbodyConstraints.FreezePosition;
        }
    }

    /// <summary>
    /// Load chunks around the player, unload those that are too far.
    /// </summary>
    protected void UpdatePlayerArea()
    {
        playerChunkID = WorldToChunkPosition(Player.position);


        List<Vector3Int> deactivate = new List<Vector3Int>();

        foreach (var chunkID in loadedChunks.Keys)
        {
            if (InsideChunkRadius(chunkID, collidableChunksRadii))
            {
                SetTargetStageOfChunk(chunkID, chunkPipeline.CompleteStage);//Request that this chunk should be complete
            }
            else if (InsideChunkRadius(chunkID, renderedChunksRadii))
            {
                SetTargetStageOfChunk(chunkID, chunkPipeline.RenderedStage);//Request that this chunk should be rendered
            }
            else if (InsideChunkRadius(chunkID, dataChunksRadii))
            {
                SetTargetStageOfChunk(chunkID, chunkPipeline.DataStage);//Request that this chunk should be just data
            }
            else 
            {
                deactivate.Add(chunkID);
            }
        }

        deactivate.ForEach(_ => Deactivate(_));
    }

    private AbstractChunkComponent<ChunkDataType,VoxelDataType> GetChunkComponent(Vector3Int chunkID) 
    {
        if (loadedChunks.TryGetValue(chunkID, out var chunkComponent))
        {
            return chunkComponent;
        }
        throw new Exception($"Tried to get a chunk component that for chunk ID {chunkID} that is not loaded");
    }

    protected void Deactivate(Vector3Int chunkID)
    {
        if (loadedChunks.TryGetValue(chunkID, out var chunkComponent))
        {
            chunkPipeline.RemoveChunk(chunkID);

            //Return modified data to the provider
            if (chunkComponent.Data != null && chunkComponent.Data.ModifiedSinceGeneration)
            {
                chunkProvider.AddModifiedChunkData(chunkComponent.ChunkID, chunkComponent.Data);
            }

            loadedChunks.Remove(chunkID);

            //Return chunk gameobject to pool
            chunkPool.ReturnToPool(chunkComponent.gameObject);

        }
        else
        {
            throw new ArgumentException($"Cannot deactivate chunk {chunkID} as it is already inactive or nonexistent");
        }

    }

    /// <summary>
    /// Sets the target stage of a chunk, creating it if it did not exist
    /// </summary>
    /// <param name="chunkID"></param>
    /// <param name="targetStage"></param>
    protected void SetTargetStageOfChunk(Vector3Int chunkID,int targetStage)
    {
        
        if (!loadedChunks.TryGetValue(chunkID, out var ChunkComponent))
        {
            //Get a new Chunk GameObject to house the generated Chunk data.
            var ChunkObject = chunkPool.Next(transform);
            ChunkComponent = ChunkObject.GetComponent<AbstractChunkComponent<ChunkDataType, VoxelDataType>>();
            ChunkComponent.Initialise(chunkID, ChunkToWorldPosition(chunkID));
            //Add to set of loaded chunks
            loadedChunks[chunkID] = ChunkComponent;

            chunkPipeline.AddChunk(chunkID, targetStage);
            return;

        }        

        chunkPipeline.SetTarget(chunkID, targetStage);        

    }

    /// <summary>
    /// Cause the chunk with the given ID to be re-enterred into the pipeline at an
    /// earlier stage.
    /// </summary>
    /// <param name="chunkID"></param>
    /// <param name="stage"></param>
    protected void RedoChunkFromStage(Vector3Int chunkID, int stage) 
    {
        chunkPipeline.ReenterAtStage(chunkID, stage);
    }

    /// <summary>
    /// Manhattan distance query returning true of the chunk ID is 
    /// within the given radii of the player chunk in each axis
    /// </summary>
    /// <param name="chunkID"></param>
    /// <returns></returns>
    protected bool InsideChunkRadius(Vector3Int chunkID,Vector3Int Radii)
    {
        var displacement = playerChunkID - chunkID;
        var absDisplacement = displacement.ElementWise(Mathf.Abs);

        //Inside if all elements of the absolute displacement are less than or equal to the chunk radius
        return absDisplacement.All((a, b) => a <= b, Radii);
    }

    #region Get/Set voxels

    /// <summary>
    /// What to do when a voxel value is set
    /// </summary>
    /// <param name="previousTypeID"></param>
    /// <param name="newTypeID"></param>
    /// <param name="chunkComponent"></param>
    /// <param name="localVoxelIndex"></param>
    protected void OnVoxelSet(ushort previousTypeID,ushort newTypeID, AbstractChunkComponent<ChunkDataType, VoxelDataType> chunkComponent, Vector3Int localVoxelIndex) 
    {
        if (previousTypeID == newTypeID)
        {
            return;//Nothing has changed
        }

        var chunkID = chunkComponent.ChunkID;

        //The neighbouring chunk(s) may need remeshing
        if (chunkMesher.IsMeshDependentOnNeighbourChunks)
        {
            foreach (var dir in GetBordersVoxelIsOn(localVoxelIndex))
            {
                var neighbourChunkID = chunkID + dir;
                if (loadedChunks.TryGetValue(neighbourChunkID, out var neighbourComponent))
                {
                    
                    if (chunkPipeline.GetTargetStage(neighbourChunkID) >= chunkPipeline.RenderedStage)
                    {
                        //The neighbour chunk will need remeshing
                        RedoChunkFromStage(neighbourChunkID, chunkPipeline.DataStage);
                    }
                }
            }
        }

        if (chunkPipeline.GetTargetStage(chunkID) >= chunkPipeline.RenderedStage)
        {
            //The chunk that changed will need remeshing if its target stage has a mesh
            RedoChunkFromStage(chunkID, chunkPipeline.DataStage);
        }
    }

    /// <summary>
    /// Returns a list of directions such that the given voxel index 
    /// shares a face with another chunk in that direction.
    /// List is empty for indices that are not on the edge of the chunk
    /// </summary>
    /// <param name="localVoxelIndex"></param>
    /// <returns></returns>
    private List<Vector3Int> GetBordersVoxelIsOn(Vector3Int localVoxelIndex) 
    {
        List<Vector3Int> borderDirections = new List<Vector3Int>();

        //If voxel is an interior voxel, it is not on any borders
        if (localVoxelIndex.All((a,b)=>a > 0 && a < b-1,ChunkDimensions))
        {
            return borderDirections;
        }
        
        for (int i = 0; i < Directions.NumDirections; i++)
        {
            var dir = Directions.IntVectors[i];
            var adjacentIndex = localVoxelIndex + dir;
            if (adjacentIndex.Any((a,b)=> a < 0 || a >= b ,ChunkDimensions))
            {
                /* If any of the adjacent index components are outside the chunk dimensions,
                 * then the voxel is on the border in the current direction
                */
                borderDirections.Add(dir);
            }
        }

        return borderDirections;
    }

    public bool TrySetVoxel(Vector3 worldPos, ushort voxelTypeID,bool overrideExisting)
    {
        Vector3Int localVoxelIndex;
        var chunkID = WorldToChunkPosition(worldPos,out localVoxelIndex);

        if (loadedChunks.TryGetValue(chunkID,out var chunkComponent))
        {
            if (chunkPipeline.GetMaxStage(chunkID) < chunkPipeline.CompleteStage)
            {
                //Disallow edits to incomplete chunks (those without collision meshes)
                return false;
            }

            var newVox = default(VoxelDataType);
            newVox.TypeID = voxelTypeID;

            ushort prevID = chunkComponent.Data[localVoxelIndex].TypeID;

            if (!overrideExisting)
            {
                //Disallow setting voxel if one already exists
                if (prevID != VoxelTypeManager.AIR_ID)
                {
                    return false;
                }
            }

            chunkComponent.Data[localVoxelIndex] = newVox;

            OnVoxelSet(prevID, voxelTypeID, chunkComponent, localVoxelIndex);            

            return true;
        }
        return false;
    }

    public bool TryGetVoxel(Vector3 worldPos,out ushort voxelTypeID)
    {
        Vector3Int localVoxelIndex;
        var chunkID = WorldToChunkPosition(worldPos, out localVoxelIndex);

        return TryGetVoxel(chunkID, localVoxelIndex, out voxelTypeID);
    }

    public bool TryGetVoxel(Vector3Int chunkID,Vector3Int localVoxelIndex, out ushort voxelTypeID)
    {
        voxelTypeID = VoxelTypeManager.AIR_ID;

        if (loadedChunks.TryGetValue(chunkID, out var chunkComponent))
        {
            if (!chunkPipeline.ChunkDataReadable(chunkID))
            {
                //Data is not valid to be read
                return false;
            }

            voxelTypeID = chunkComponent.Data[localVoxelIndex].TypeID;

            return true;
        }
        return false;
    }

    #endregion

    #region position conversion methods
    public Vector3Int WorldToChunkPosition(Vector3 pos)
    {
        //ChunkManager must be located at world origin
        Assert.AreEqual(Vector3.zero, transform.position);
        Assert.AreEqual(Quaternion.Euler(0, 0, 0), transform.rotation);

        Vector3Int floor = new Vector3Int();
        floor.x = Mathf.FloorToInt(pos.x);
        floor.y = Mathf.FloorToInt(pos.y);
        floor.z = Mathf.FloorToInt(pos.z);

        //Result is elementwise integer division by the Chunk dimensions
        var result = floor.ElementWise((a, b) => Mathf.FloorToInt(a / (float)b), ChunkDimensions);

        return result;
    }

    protected Vector3Int WorldToChunkPosition(Vector3 pos, out Vector3Int localVoxelIndex)
    {
        //ChunkManager must be located at world origin
        Assert.AreEqual(Vector3.zero, transform.position);
        Assert.AreEqual(Quaternion.Euler(0, 0, 0), transform.rotation);

        Vector3Int floor = new Vector3Int();
        floor.x = Mathf.FloorToInt(pos.x);
        floor.y = Mathf.FloorToInt(pos.y);
        floor.z = Mathf.FloorToInt(pos.z);

        //Result is elementwise integer division by the Chunk dimensions
        var result = floor.ElementWise((a, b) => Mathf.FloorToInt(a / (float)b), ChunkDimensions);

        localVoxelIndex = LocalVoxelIndexOfPosition(floor);

        return result;
    }

    /// <summary>
    /// Returns the local voxel index of some position, but not the chunk ID
    /// of the chunk containing that position. Intended to be used when you 
    /// already know the chunkID containing the position.
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public Vector3Int LocalVoxelIndexOfPosition(Vector3Int position) 
    {
        var remainder = position.ElementWise((a, b) => a % b, ChunkDimensions);
        //Local voxel index is the remainder, with negatives adjusted
        return remainder.ElementWise((a, b) => a < 0 ? b + a : a, ChunkDimensions);
    }

    public Vector3 ChunkToWorldPosition(Vector3Int chunkID)
    {
        //ChunkManager must be located at world origin
        Assert.AreEqual(Vector3.zero, transform.position);
        Assert.AreEqual(Quaternion.Euler(0, 0, 0), transform.rotation);

        Vector3 result = chunkID * ChunkDimensions;
        return result;
    }

    public Vector3 SnapToVoxelCenter(Vector3 pos)
    {
        var result = pos.ElementWise(_ => Mathf.Floor(_) + VoxelSize/2.0f);
        return result;
    }

    

    #endregion
}
