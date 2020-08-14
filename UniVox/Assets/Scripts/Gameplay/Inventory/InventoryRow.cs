using UnityEngine;
using UniVox.UI;

namespace UniVox.Gameplay.Inventory
{
    public class InventoryRow : MonoBehaviour
    {
        public InventorySlot[] Slots { get; private set; }

        public int NumSlots { get => Slots.Length; }

        void Awake()
        {
            Slots = GetComponentsInChildren<InventorySlot>(true);
        }
    }
}