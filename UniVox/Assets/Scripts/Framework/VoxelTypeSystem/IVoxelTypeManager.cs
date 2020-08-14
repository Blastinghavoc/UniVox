using System;
using UnityEngine;

namespace UniVox.Framework
{
    public interface IVoxelTypeManager : IDisposable
    {
        ushort LastVoxelID { get; }

        VoxelTypeData GetData(ushort voxelTypeID);
        SOVoxelTypeDefinition GetDefinition(VoxelTypeID id);
        VoxelTypeID GetId(SOVoxelTypeDefinition def);
        (int, int) GetLightProperties(VoxelTypeID voxelType);
        Material GetMaterial(ushort materialID);
        void Initialise();
    }
}