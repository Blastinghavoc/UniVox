using UnityEngine;

namespace UniVox.UI
{
    public abstract class AbstractUIController : MonoBehaviour
    {
        public bool IsVisible { get; protected set; }

        public bool EnableCursorWhileVisible = false;

        public void ToggleVisibility()
        {
            SetVisibility(!IsVisible);
        }

        public abstract void SetVisibility(bool visible);
    }
}