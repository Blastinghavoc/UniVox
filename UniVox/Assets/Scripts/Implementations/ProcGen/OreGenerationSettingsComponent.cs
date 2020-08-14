using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UniVox.Framework;
using Utils;

namespace UniVox.Implementations.ProcGen
{
    [System.Serializable]
    public class OreGenerationSettingsComponent : MonoBehaviour, IDisposable
    {
        [System.Serializable]
        private class OreSettingsPair
        {
            public SOVoxelTypeDefinition voxelType = null;
            public OreSettings settings = default;
        }

        [SerializeField] private List<OreSettingsPair> ores = new List<OreSettingsPair>();

        public NativeArray<NativeOreSettingsPair> Native { get; private set; }

        public void Initialise(VoxelTypeManager typeManager, Allocator allocator = Allocator.Persistent)
        {
            NativeArray<NativeOreSettingsPair> oreSettings = new NativeArray<NativeOreSettingsPair>(ores.Count, allocator);
            for (int i = 0; i < ores.Count; i++)
            {
                var original = ores[i];
                var settings = original.settings;
                settings.Initialise();
                oreSettings[i] = new NativeOreSettingsPair()
                {
                    voxelType = typeManager.GetId(original.voxelType),
                    settings = settings
                };
            }
            Native = oreSettings;
        }

        public void Dispose()
        {
            Native.SmartDispose();
        }
    }
}