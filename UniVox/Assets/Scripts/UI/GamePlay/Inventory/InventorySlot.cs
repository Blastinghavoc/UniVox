using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniVox.Gameplay.Inventory;

namespace UniVox.UI
{
    [RequireComponent(typeof(Image))]
    public class InventorySlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler,IPointerExitHandler
    {
        [SerializeField] private Image image;
        private IInventorySystem inventorySystem;

        [SerializeField] private InventoryIcon icon;

        /// <summary>
        /// Whether or not this slot supplies an infinite amount of items
        /// </summary>
        public bool InfiniteMode = false;

        private InventoryItem _item;
        public InventoryItem Item
        {
            get => _item;
            set
            {
                _item = value;
                UpdateIconImage();
            }
        }

        private void Awake()
        {
            image = GetComponent<Image>();
            icon = GetComponentInChildren<InventoryIcon>();
            inventorySystem = GetComponentInParent<IInventorySystem>();
            Assert.IsNotNull(image);
            Assert.IsNotNull(icon);
            Assert.IsNotNull(inventorySystem);
            Item = null;
        }

        public void SetHighlight(Color color)
        {            
            image.color = color;            
        }

        private void UpdateIconImage()
        {
            if (Item != null)
            {
                icon.image.sprite = inventorySystem.IconSpriteFor(Item.ID);
                icon.image.color = Color.white;
            }
            else
            {
                icon.image.color = Color.clear;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (InfiniteMode)
            {
                //Set the cursor item
                inventorySystem.ItemOnCursor = Item;
            }
            else
            {
                //Swap the item on cursor with the item in this slot
                var tmp = Item;
                Item = inventorySystem.ItemOnCursor;
                inventorySystem.ItemOnCursor = tmp;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Item != null)
            {
                inventorySystem.Tooltip = Item.typeDefinition.DisplayName;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            inventorySystem.Tooltip = string.Empty;
        }
    }
}