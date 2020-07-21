using System.Collections.Generic;
using UnityEngine;

namespace UniVox.Framework.Lighting
{
    public class LightManagerComponent : MonoBehaviour, ILightManager
    {
        public string GlobalLightName;
        [Range(0, 1)]
        public float GlobalLightValue;

        private ILightManager lightManager;

        public void Initialise(IVoxelTypeManager voxelTypeManager)
        {
            lightManager = new LightManager();
            lightManager.Initialise(voxelTypeManager);
        }

        public void OnChunkGenerated(IChunkData chunkData, IChunkData aboveChunkData)
        {
            lightManager.OnChunkGenerated(chunkData, aboveChunkData);
        }

        public List<Vector3Int> UpdateLightOnVoxelSet(ChunkNeighbourhood neighbourhood, Vector3Int localCoords, VoxelTypeID voxelType, VoxelTypeID previousType)
        {
            return lightManager.UpdateLightOnVoxelSet(neighbourhood, localCoords, voxelType, previousType);
        }

        // Start is called before the first frame update
        void Start()
        {
            //Shader.SetGlobalFloat(GlobalLightName, GlobalLightValue);
        }

        // Update is called once per frame
        void Update()
        {
            Shader.SetGlobalFloat(GlobalLightName, GlobalLightValue);
        }
    }
}