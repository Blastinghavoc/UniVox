using UnityEngine;
using UniVox.Framework;

namespace UniVox.Gameplay.Inventory
{
    [RequireComponent(typeof(InventoryRow))]
    public class HotbarController : MonoBehaviour
    {
        [SerializeField] private Color highlightColor = Color.yellow;
        private InventoryRow row;
        private KeyCode[] keys;
        private int selectedSlotIndex;
        private VoxelTypeManager typeManager;
        public bool InvertScroll;

        public SOVoxelTypeDefinition Selected
        {
            get
            {
                var item = row.Slots[selectedSlotIndex].Item;
                return item != null ? item.typeDefinition : null;
            }
        }

        private void Start()
        {
            typeManager = FindObjectOfType<VoxelTypeManager>();
            row = GetComponent<InventoryRow>();
            keys = new KeyCode[] {
                KeyCode.Alpha1,
                KeyCode.Alpha2,
                KeyCode.Alpha3,
                KeyCode.Alpha4,
                KeyCode.Alpha5,
                KeyCode.Alpha6,
                KeyCode.Alpha7,
                KeyCode.Alpha8,
                KeyCode.Alpha9,
            };
            SetSelectedSlot(0);
        }

        private void Update()
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (Input.GetKeyDown(keys[i]))
                {
                    SetSelectedSlot(i);
                    return;
                }
            }

            var scrollAmount = Input.GetAxis("Mouse ScrollWheel");
            if (InvertScroll)
            {
                scrollAmount = 0 - scrollAmount;
            }
            if (scrollAmount > 0)
            {
                SetSelectedSlot((selectedSlotIndex + 1) % row.NumSlots);
            }
            else if (scrollAmount < 0)
            {
                var nextSlot = selectedSlotIndex - 1;
                if (nextSlot < 0)
                {
                    nextSlot = row.NumSlots - 1;
                }
                SetSelectedSlot(nextSlot);
            }
        }

        public void SetCurrentItem(InventoryItem item) 
        {
            row.Slots[selectedSlotIndex].Item = item;
        }

        private void SetSelectedSlot(int newIndex)
        {
            row.Slots[selectedSlotIndex].SetHighlight(Color.white);
            selectedSlotIndex = newIndex;
            row.Slots[selectedSlotIndex].SetHighlight(highlightColor);
        }


    }
}