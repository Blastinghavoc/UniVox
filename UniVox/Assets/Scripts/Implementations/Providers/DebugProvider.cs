using UnityEngine;
using System.Collections;
using UniVox.Framework;
using UniVox.Implementations.ChunkData;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Framework.Jobified;
using UniVox.Framework.Serialisation;

namespace UniVox.Implementations.Providers
{
    public class DebugProvider : AbstractProviderComponent
    {
        public enum WorldType 
        { 
            flat,
            flatWithHoles,
            singleBlock
        }
        public WorldType worldType = WorldType.flat;
        public SOVoxelTypeDefinition dirtType;
        private ushort dirtID;
        public SOVoxelTypeDefinition grassType;
        private ushort grassID;
        [SerializeField] private ChunkDataFactory chunkDataFactory = null;

        public override void Initialise(VoxelTypeManager voxelTypeManager,IChunkManager chunkManager, FrameworkEventManager eventManager)
        {
            base.Initialise(voxelTypeManager,chunkManager,eventManager);
            dirtID = voxelTypeManager.GetId(dirtType);
            grassID = voxelTypeManager.GetId(grassType);
        }

        public override AbstractPipelineJob<IChunkData> GenerateTerrainData(Vector3Int chunkID)
        {
            switch (worldType)
            {
                case WorldType.flat:
                    return new BasicFunctionJob<IChunkData>(() => FlatWorld(chunkID, chunkManager.ChunkDimensions));
                case WorldType.flatWithHoles:
                    return new BasicFunctionJob<IChunkData>(() => FlatWorldWithHoles(chunkID, chunkManager.ChunkDimensions));
                case WorldType.singleBlock:
                    return new BasicFunctionJob<IChunkData>(() => SingleBlock(chunkID, chunkManager.ChunkDimensions));
                default:
                    throw new System.Exception("Invalid world type");
            }            
        }

        private IChunkData FlatWorld(Vector3Int chunkID, Vector3Int chunkDimensions) 
        {
            var ChunkData = chunkDataFactory.Create(chunkID, chunkDimensions);

            int groundHeight = 0;
            int chunkYCuttoff = (groundHeight + chunkDimensions.y)/chunkDimensions.y;

            var chunkPosition = chunkManager.ChunkToWorldPosition(chunkID);

            if (chunkID.y < chunkYCuttoff)//Chunks above the cuttof are just pure air
            {
                for (int z = 0; z < chunkDimensions.z; z++)
                {
                    for (int y = 0; y < chunkDimensions.y; y++)
                    {
                        for (int x = 0; x < chunkDimensions.x; x++)
                        {
                            var height = y + chunkPosition.y;
                            if (height == groundHeight)
                            {
                                ChunkData[x, y, z] = new VoxelTypeID(grassID);
                            }
                            else if (height < groundHeight)
                            {
                                ChunkData[x, y, z] = new VoxelTypeID(dirtID);
                            }
                        }
                    }
                }
            }

            return ChunkData;
        }

        private IChunkData FlatWorldWithHoles(Vector3Int chunkID, Vector3Int chunkDimensions)
        {
            var ChunkData = chunkDataFactory.Create(chunkID, chunkDimensions);

            int groundHeight = 0;
            int chunkYCuttoff = (groundHeight + chunkDimensions.y) / chunkDimensions.y;

            var chunkPosition = chunkManager.ChunkToWorldPosition(chunkID);

            bool isHole = chunkID.x % 3 == 0;

            if (chunkID.y < chunkYCuttoff && !isHole)//Chunks above the cuttof are just pure air
            {
                for (int z = 0; z < chunkDimensions.z; z++)
                {
                    for (int y = 0; y < chunkDimensions.y; y++)
                    {
                        for (int x = 0; x < chunkDimensions.x; x++)
                        {
                            var height = y + chunkPosition.y;
                            if (height == groundHeight)
                            {
                                ChunkData[x, y, z] = new VoxelTypeID(grassID);
                            }
                            else if (height < groundHeight)
                            {
                                ChunkData[x, y, z] = new VoxelTypeID(dirtID);
                            }
                        }
                    }
                }
            }

            return ChunkData;
        }

        private IChunkData SingleBlock(Vector3Int chunkID, Vector3Int chunkDimensions) 
        {
            var ChunkData = chunkDataFactory.Create(chunkID, chunkDimensions);

            ChunkData[0, 0, 0] = new VoxelTypeID(grassID);

            return ChunkData;
        }

        private IChunkData HalfHeight(Vector3Int chunkID, Vector3Int chunkDimensions)
        {
            var ChunkData = chunkDataFactory.Create(chunkID, chunkDimensions);
            var maxY = chunkDimensions.y / 2;

            for (int z = 0; z < chunkDimensions.z; z++)
            {
                for (int y = 0; y < maxY; y++)
                {
                    for (int x = 0; x < chunkDimensions.x; x++)
                    {
                        if (y == maxY - 1)
                        {
                            ChunkData[x, y, z] = new VoxelTypeID(grassID);
                        }
                        else
                        {
                            ChunkData[x, y, z] = new VoxelTypeID(dirtID);
                        }
                    }
                }
            }
            return ChunkData;
        }

        private IChunkData HalfLattice(Vector3Int chunkID, Vector3Int chunkDimensions)
        {
            bool b = true;
            var ChunkData = chunkDataFactory.Create(chunkID, chunkDimensions);
            for (int z = 0; z < chunkDimensions.z; z++)
            {
                b = !b;
                for (int y = 0; y < chunkDimensions.y / 2; y++)
                {
                    b = !b;
                    for (int x = 0; x < chunkDimensions.x; x++)
                    {
                        b = !b;
                        if (b)
                        {
                            continue;
                        }
                        ChunkData[x, y, z] = new VoxelTypeID(dirtID);
                    }
                }
            }
            return ChunkData;
        }

        public override AbstractPipelineJob<ChunkNeighbourhood> GenerateStructuresForNeighbourhood(Vector3Int centerChunkID, ChunkNeighbourhood neighbourhood)
        {
            return new BasicFunctionJob<ChunkNeighbourhood>(()=>neighbourhood);
        }

        public override int[] GetHeightMapForColumn(Vector2Int columnId)
        {
            //Ground height assumed to be 0.
            return new int[chunkManager.ChunkDimensions.x * chunkManager.ChunkDimensions.z];            
        }

        protected override IChunkData InitialiseChunkDataFromSaved(ChunkSaveData chunkSaveData, Vector3Int chunkId)
        {
            var data = new FlatArrayChunkData(chunkId, chunkManager.ChunkDimensions, chunkSaveData.voxels);
            data.SetRotationsFromArray(chunkSaveData.rotatedEntries);
            return data;
        }
    }
}