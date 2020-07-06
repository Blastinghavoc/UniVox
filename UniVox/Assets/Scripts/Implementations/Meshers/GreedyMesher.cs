using UnityEngine;
using UniVox.Framework;
using UniVox.Framework.ChunkPipeline.VirtualJobs;

namespace UniVox.Implementations.Meshers
{
    public class GreedyMesher : AbstractMesherComponent 
    {
        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager, FrameworkEventManager eventManager)
        {
            base.Initialise(voxelTypeManager, chunkManager, eventManager);
            IsMeshDependentOnNeighbourChunks = true;
        }

        public override AbstractPipelineJob<MeshDescriptor> CreateMeshJob(Vector3Int chunkID)
        {
            var chunkData = chunkManager.GetReadOnlyChunkData(chunkID);
            //TODO WIP testing
            return new BasicFunctionJob<MeshDescriptor>(() => {
                var mesh = GreedyMeshingAlgorithm.ReduceMesh(chunkData, chunkManager.ChunkDimensions);
                MeshDescriptor descriptor = new MeshDescriptor()
                {
                    mesh = mesh,
                    collidableLengthIndices = mesh.GetIndices(0).Length,
                    collidableLengthVertices = mesh.vertexCount,
                    materialsBySubmesh = new Material[] { voxelTypeManager.GetMaterial(0) }                  
                };
                return descriptor;
            }); 
        }
    }
}