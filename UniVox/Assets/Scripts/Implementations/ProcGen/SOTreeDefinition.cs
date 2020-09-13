using UnityEngine;
using UniVox.Framework;
using static UniVox.Implementations.ProcGen.SOTreeDefinition;

namespace UniVox.Implementations.ProcGen
{
    [CreateAssetMenu(menuName = "UniVox/TreeDefinition")]
    public class SOTreeDefinition : ScriptableObject
    {
        [Range(1, 15)]
        public int maxHeight;
        [Range(1, 15)]
        public int minHeight;

        //Minimum distance above the ground at which leaves can be placed
        [Range(0, 8)]
        public int minLeafClearance;

        public SOVoxelTypeDefinition leafType;
        public SOVoxelTypeDefinition logType;

        public TreeStyle Style;
        public enum TreeStyle
        {
            oak,
            spruce,
            cactus
        }

        public NativeTreeDefinition ToNative(VoxelTypeManager typeManager)
        {
            NativeTreeDefinition def = new NativeTreeDefinition();
            def.maxHeight = maxHeight;
            def.minHeight = minHeight;
            def.minLeafClearance = minLeafClearance;
            def.leafID = (leafType == null) ? (VoxelTypeID)VoxelTypeID.AIR_ID : typeManager.GetId(leafType);
            def.logID = (logType == null) ? (VoxelTypeID)VoxelTypeID.AIR_ID : typeManager.GetId(logType);
            def.style = Style;
            return def;
        }
    }

    public struct NativeTreeDefinition
    {
        public int maxHeight;
        public int minHeight;
        public int minLeafClearance;

        public VoxelTypeID leafID;
        public VoxelTypeID logID;

        public TreeStyle style;
    }
}