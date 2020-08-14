using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UniVox.Framework.Common;
using static UniVox.Framework.Jobified.JobUtils;
using static Utils.Helpers;

namespace UniVox.Framework.Lighting
{
    [BurstCompile]
    public struct BorderResolutionJob : IJob, IDisposable
    {
        public LightJobData data;

        public NativeArray<int3> sunlightFromBorder;
        public NativeArray<int3> dynamicFromBorder;

        //Direction relative to this chunk to the border that was updated
        public Direction toDirection;

        //Outputs
        public NativeQueue<int3> dynamicPropagationQueue { get; set; }
        public NativeQueue<int3> sunlightPropagationQueue { get; set; }

        public void Dispose()
        {
            sunlightFromBorder.Dispose();
            dynamicFromBorder.Dispose();
        }

        public void Execute()
        {
            int dx = data.dimensions.x;
            int dxdy = data.dimensions.x * data.dimensions.y;

            var offset = data.directionVectors[(int)toDirection];

            for (int i = 0; i < sunlightFromBorder.Length; i++)
            {
                var localPos = sunlightFromBorder[i];

                var localFlat = MultiIndexToFlat(localPos, dx, dxdy);
                var localLv = data.lights[localFlat];

                var neighPos = localPos + offset;
                var neighLv = GetLightValue(neighPos, data.lights, data.dimensions, data.neighbourData);

                var voxel = data.voxels[localFlat];
                var absorption = data.voxelTypeToAbsorptionMap[voxel];

                //Sunlight
                var sunlight = neighLv.Sun;
                if (absorption < LightValue.MaxIntensity && sunlight > 1)
                {
                    if ((offset.y == 1) && absorption == 1 && sunlight == LightValue.MaxIntensity)
                    {
                        //Do nothing, sunlight preserved going downwards
                    }
                    else
                    {
                        sunlight -= absorption;
                    }

                    if (sunlight > localLv.Sun)
                    {
                        localLv.Sun = sunlight;
                        sunlightPropagationQueue.Enqueue(localPos);
                        data.lights[localFlat] = localLv;
                    }
                }

            }

            for (int i = 0; i < dynamicFromBorder.Length; i++)
            {
                var localPos = dynamicFromBorder[i];

                var localFlat = MultiIndexToFlat(localPos, dx, dxdy);
                var localLv = data.lights[localFlat];

                var neighPos = localPos + offset;
                var neighLv = GetLightValue(neighPos, data.lights, data.dimensions, data.neighbourData);

                var voxel = data.voxels[localFlat];
                var absorption = data.voxelTypeToAbsorptionMap[voxel];

                //Dynamic
                var next = neighLv.Dynamic - absorption;
                if (next > localLv.Dynamic)
                {
                    localLv.Dynamic = next;
                    dynamicPropagationQueue.Enqueue(localPos);
                    data.lights[localFlat] = localLv;
                }
            }
        }
    }
}