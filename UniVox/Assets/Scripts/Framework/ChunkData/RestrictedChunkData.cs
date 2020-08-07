using System;
using Unity.Collections;
using UnityEngine;
using UniVox.Framework.Common;
using UniVox.Framework.Lighting;
using UniVox.Framework.Serialisation;

namespace UniVox.Framework
{
    public class RestrictedChunkData : IChunkData
    {
        private IChunkData realData;

        private bool allowModifyLight;
        private bool allowModifyVoxels;

        public RestrictedChunkData(IChunkData realData,bool allowModifyVoxels = false, bool allowModifyLight = false)
        {
            this.realData = realData;
            this.allowModifyVoxels = allowModifyVoxels;
            this.allowModifyLight = allowModifyLight;
        }

        public VoxelTypeID this[Vector3Int index] { get => realData[index]; 
            set {
                if (!allowModifyVoxels)
                {
                    throw new System.NotImplementedException(); 
                }
                else
                {
                    realData[index] = value;
                }
            } 
        }
        public VoxelTypeID this[int i, int j, int k] { get => realData[i, j, k]; 
            set
            {
                if (!allowModifyVoxels)
                {
                    throw new System.NotImplementedException();
                }
                else
                {
                    realData[i,j,k] = value;
                }
            }
        }

        public Vector3Int ChunkID { get => realData.ChunkID; set => throw new System.NotImplementedException(); }
        public Vector3Int Dimensions { get => realData.Dimensions; set => throw new System.NotImplementedException(); }
        public bool ModifiedSinceGeneration { get => realData.ModifiedSinceGeneration; set => throw new System.NotImplementedException(); }
        public bool FullyGenerated { get => realData.FullyGenerated; set => throw new System.NotImplementedException(); }

        public NativeArray<VoxelTypeID> BorderToNative(Direction dir, Allocator allocator = Allocator.Persistent)
        {
            return realData.BorderToNative(dir, allocator);
        }

        public NativeArray<LightValue> BorderToNativeLight(Direction dir, Allocator allocator = Allocator.Persistent)
        {
            return realData.BorderToNativeLight(dir, allocator);
        }

        public LightValue GetLight(int x, int y, int z)
        {
            return realData.GetLight(x, y, z);
        }

        public LightValue GetLight(Vector3Int pos)
        {
            return realData.GetLight(pos);
        }

        public ISaveData GetSaveData()
        {
            return realData.GetSaveData();
        }

        public NativeArray<LightValue> LightToNative(Allocator allocator = Allocator.Persistent)
        {
            return realData.LightToNative(allocator);
        }

        public NativeArray<RotatedVoxelEntry> NativeRotations(Allocator allocator = Allocator.Persistent)
        {
            return realData.NativeRotations(allocator);
        }

        public void SetLight(int x, int y, int z, LightValue lightValue)
        {
            if (!allowModifyLight)
            {
                throw new System.NotImplementedException();
            }
            else
            {
                realData.SetLight(x, y, z, lightValue);
            }
        }

        public void SetLight(Vector3Int pos, LightValue lightValue)
        {
            if (!allowModifyLight)
            {
                throw new System.NotImplementedException();
            }
            else
            {
                realData.SetLight(pos, lightValue);
            }
        }

        public void SetLightMap(LightValue[] lights)
        {
            if (allowModifyLight)
            {
                realData.SetLightMap(lights);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public void SetRotation(Vector3Int coords, VoxelRotation rotation)
        {
            if (!allowModifyVoxels)
            {
                throw new System.NotImplementedException();
            }
            else
            {
                realData.SetRotation(coords, rotation);
            }
        }

        public void SetRotationsFromArray(RotatedVoxelEntry[] entries)
        {
            if (!allowModifyVoxels)
            {
                throw new System.NotImplementedException();
            }
            else
            {
                realData.SetRotationsFromArray(entries);
            }
        }

        public NativeArray<VoxelTypeID> ToNative(Allocator allocator = Allocator.Persistent)
        {
            return realData.ToNative(allocator);
        }

        public bool TryGetVoxel(Vector3Int coords, out VoxelTypeID vox)
        {
            return realData.TryGetVoxel(coords, out vox);
        }

        public bool TryGetVoxel(int x, int y, int z, out VoxelTypeID vox)
        {
            return realData.TryGetVoxel(x, y, z, out vox);
        }
    }
}