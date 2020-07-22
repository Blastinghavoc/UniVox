﻿using Unity.Collections;
using UnityEngine;
using UniVox.Framework.Common;
using UniVox.Framework.Lighting;

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

        public NativeArray<VoxelTypeID> BorderToNative(Direction dir)
        {
            return realData.BorderToNative(dir);
        }

        public NativeArray<LightValue> BorderToNativeLight(Direction dir)
        {
            return realData.BorderToNativeLight(dir);
        }

        public LightValue GetLight(int x, int y, int z)
        {
            return realData.GetLight(x, y, z);
        }

        public LightValue GetLight(Vector3Int pos)
        {
            return realData.GetLight(pos);
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

        public NativeArray<VoxelTypeID> ToNative(Allocator allocator = Allocator.Persistent)
        {
            return realData.ToNative(allocator);
        }

        public bool TryGetVoxelID(Vector3Int coords, out VoxelTypeID vox)
        {
            return realData.TryGetVoxelID(coords, out vox);
        }

        public bool TryGetVoxelID(int x, int y, int z, out VoxelTypeID vox)
        {
            return realData.TryGetVoxelID(x, y, z, out vox);
        }
    }
}