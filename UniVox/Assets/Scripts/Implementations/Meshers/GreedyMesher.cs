﻿using Unity.Collections;
using UniVox.Framework;
using UniVox.Framework.Jobified;

namespace UniVox.Implementations.Meshers
{
    public class GreedyMesher : AbstractMesherComponent
    {
        public override void Initialise(VoxelTypeManager voxelTypeManager, IChunkManager chunkManager, FrameworkEventManager eventManager)
        {
            base.Initialise(voxelTypeManager, chunkManager, eventManager);
            CullFaces = true;
        }

        protected override IMeshingJob createMeshingJob(MeshJobData data)
        {
            var job = new GreedyMeshingJob();
            job.data = data;
            job.rotatedVoxelsMap = new NativeHashMap<int, VoxelRotation>(job.data.rotatedVoxels.Length, Allocator.Persistent);
            job.directionRotator = directionRotator;
            //TODO use allocator temp job if job can be guaranteed to take less than 4 frames
            job.nonCollidableQueue = new NativeList<GreedyMeshingJob.DoLater>(Allocator.Persistent);
            var size = data.dimensions.x;
            job.maskPositive = new NativeArray<GreedyMeshingJob.FaceDescriptor>((size + 1) * (size + 1), Allocator.Persistent);
            job.maskNegative = new NativeArray<GreedyMeshingJob.FaceDescriptor>((size + 1) * (size + 1), Allocator.Persistent);

            return job;
        }
    }
}