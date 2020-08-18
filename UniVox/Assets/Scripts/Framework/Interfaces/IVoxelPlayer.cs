using UnityEngine;

namespace UniVox.Framework
{
    public interface IVoxelPlayer
    {
        Vector3 Position { get; set; }
        Vector3 StartPosition { get; }
        Rigidbody Rigidbody { get; }

        void AllowMove(bool allow);
    }
}