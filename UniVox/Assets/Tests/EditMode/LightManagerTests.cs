using NSubstitute;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.ComponentModel.Design;
using Unity.Mathematics;
using UnityEngine;
using UniVox.Framework;
using UniVox.Framework.Common;
using UniVox.Framework.Lighting;

namespace Tests
{
    public class LightManagerTests
    {
        IVoxelTypeManager voxelTypeManager;
        LightManager lightManager;
        VoxelTypeID lampId;

        //TODO add mock chunk data for light values

        [SetUp]
        public void Reset()
        {
            lampId = (VoxelTypeID)1337;
            voxelTypeManager = Substitute.For<VoxelTypeManager>();
            voxelTypeManager.GetLightEmission(Arg.Any<VoxelTypeID>())
                .Returns((args)=> {
                    var typeId = (VoxelTypeID) args[0];
                    if (typeId.Equals(lampId))
                    {
                        return LightValue.IntensityRange - 1;
                    }
                    return 0;
                });

            lightManager = new LightManager();
            lightManager.Initialise(voxelTypeManager);
        }

        [Test]
        public void PropagateDynamicOnLightSourcePlaced() 
        {

        }
    }
}
