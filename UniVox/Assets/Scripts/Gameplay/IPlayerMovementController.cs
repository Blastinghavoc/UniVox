using UnityEngine;


namespace UniVox.Gameplay
{
    public interface IPlayerMovementController
    {
        Vector3 Velocity { get; }
        bool Grounded { get; }
        bool Jumping { get; }
        bool Running { get; }

        void SetCursorLock(bool value);
    }
}