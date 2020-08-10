using UnityEngine;
using UnityEngine.UI;

namespace UniVox.Gameplay.Inventory
{
    public class Tooltip : MonoBehaviour
    {
        [SerializeField] private Text text = null;

        public string Value { get => text.text; set => text.text = value; }

        private void Update()
        {
            transform.position = Input.mousePosition;
        }
    }
}