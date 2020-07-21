using System;
using UnityEngine;

namespace UniVox.Framework
{
    public interface IVoxelTypeManager:IDisposable
    {
        VoxelTypeManager.VoxelTypeData GetData(ushort voxelTypeID);
        SOVoxelTypeDefinition GetDefinition(VoxelTypeID id);
        VoxelTypeID GetId(SOVoxelTypeDefinition def);
        (int, int) GetLightProperties(VoxelTypeID voxelType);
        Material GetMaterial(ushort materialID);
        void Initialise();
    }
}