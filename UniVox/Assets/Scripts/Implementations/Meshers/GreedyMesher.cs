using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UniVox.Framework;
using UniVox.Framework.ChunkPipeline.VirtualJobs;
using UniVox.Framework.Jobified;
using Utils;

namespace UniVox.Implementations.Meshers
{
    public class GreedyMesher : AbstractMesherComponent 
    {
        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager, FrameworkEventManager eventManager)
        {
            base.Initialise(voxelTypeManager, chunkManager, eventManager);
            IsMeshDependentOnNeighbourChunks = true;
        }

        protected override IMeshingJob createMeshingJob(MeshJobData data)
        {
            var job = new GreedyMeshingJob();
            job.data = data;
            job.rotatedVoxelsMap = new NativeHashMap<int, VoxelRotation>(job.data.rotatedVoxels.Length, Allocator.Persistent);
            job.directionRotator = directionRotator;
            return job;
        }        
    }
}