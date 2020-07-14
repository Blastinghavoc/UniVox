using UnityEngine;

namespace UniVox.Framework
{
    public interface IVoxelPlayer
    {
        Vector3 Position { get; set; }

        void AllowMove(bool allow);
    }
}