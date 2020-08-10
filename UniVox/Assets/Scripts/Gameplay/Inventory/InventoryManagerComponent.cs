using Boo.Lang;
using System;
using UnityEngine;
using UniVox.Framework;
using UniVox.Framework.Common;
using UniVox.UI;

namespace UniVox.Gameplay.Inventory
{
    public class InventoryManagerComponent : AbstractUIController, IInventorySystem
    {
        private VoxelTypeManager typeManager;

        public bool CreativeMode;

        [SerializeField] private GameObject InventoryPanel = null;
        [SerializeField] private Transform Content = null;
        [SerializeField] private GameObject InventoryRowPrefab = null;

        private InventoryItem _itemOnCursor;
        public InventoryItem ItemOnCursor { get => _itemOnCursor; 
            set { 
                _itemOnCursor = value;
                UpdateCursorIcon();
            } 
        }

        public Sprite[] IconSprites { get; private set; }

        [SerializeField] private InventoryIcon CursorItemIcon = null;

        private List<InventoryRow> inventoryRows;

        private void Start()
        {
            typeManager = FindObjectOfType<VoxelTypeManager>();
            ComputeIconSprites();
            ItemOnCursor = null;

            inventoryRows = new List<InventoryRow>();

            if (CreativeMode)
            {
                InitialiseCreativeMode();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                ItemOnCursor = null;
            }
            CursorIconFollowCursor();
        }

        private void InitialiseCreativeMode() 
        {
            if (typeManager.LastVoxelID == 0)
            {
                return;
            }            

            int totalCapacity = 0;
            while (totalCapacity < typeManager.LastVoxelID)
            {
                var row = Instantiate(InventoryRowPrefab, Content).GetComponent<InventoryRow>();
                inventoryRows.Add(row);
                totalCapacity += row.NumSlots;
            }

            var currentRow = inventoryRows[0];
            var currentRowIndex = 0;
            var localIndex = 0;
            for (int i = 1; i <= typeManager.LastVoxelID; i++,localIndex++)
            {
                if (localIndex >= currentRow.NumSlots)
                {
                    localIndex = 0;
                    currentRowIndex++;
                    currentRow = inventoryRows[currentRowIndex];
                }

                currentRow.Slots[localIndex].Item = new InventoryItem() { 
                    ID = (VoxelTypeID)i,
                    typeDefinition = typeManager.GetDefinition((VoxelTypeID)i),
                    Count = 1 };
                currentRow.Slots[localIndex].InfiniteMode = true;
            }

            //Last row cleanup
            while (localIndex < currentRow.NumSlots)
            {
                currentRow.Slots[localIndex].gameObject.SetActive(false);
                localIndex++;
            }
        }

        private void ComputeIconSprites() 
        {
            IconSprites = new Sprite[typeManager.LastVoxelID + 1];
            IconSprites[0] = null;

            for (int i = 1; i <= typeManager.LastVoxelID; i++)
            {
                var voxelDef = typeManager.GetDefinition((VoxelTypeID)i);
                var tex = voxelDef.FaceTextures[(int)Direction.north];
                IconSprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }

        private void CursorIconFollowCursor() 
        {
            CursorItemIcon.gameObject.transform.position = Input.mousePosition;
        }

        private void UpdateCursorIcon() 
        {
            if (ItemOnCursor == null || !IsVisible)
            {
                CursorItemIcon.gameObject.SetActive(false);
            }
            else
            {
                CursorItemIcon.gameObject.SetActive(true);
                CursorItemIcon.image.sprite = IconSpriteFor(ItemOnCursor.ID);
            }
        }

        public Sprite IconSpriteFor(VoxelTypeID id) 
        {
            return IconSprites[id];
        }

        public override void SetVisibility(bool visible)
        {
            IsVisible = visible;

            InventoryPanel.SetActive(visible);
            enabled = visible;//Prevent update being called too
            UpdateCursorIcon();
        }
    }
}