using UnityEngine;
using UniVox.Framework;

namespace UniVox.Gameplay.Inventory
{
    public interface IInventorySystem
    {
        InventoryItem ItemOnCursor { get; set; }

        Sprite IconSpriteFor(VoxelTypeID id);
    }
}