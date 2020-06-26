using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

namespace UniVox.Framework
{
    [CreateAssetMenu(menuName = "UniVox/VoxelRotationConfig")]
    public class SORotationConfiguration : ScriptableObject
    {
        public bool3 AllowedRotationAxes;
        [Range(1,3)]
        public int MaximumSimultaneousRotations = 1;

        /// <summary>
        /// Assumed that if AllowedRotationsAxes.X == true, but allowedRotationsX is empty,
        /// then all rotations are allowed.
        /// </summary>
        public List<AllowedRotationValue> allowedRotationsX;
        public List<AllowedRotationValue> allowedRotationsY;
        public List<AllowedRotationValue> allowedRotationsZ;

        public bool RotationValid(VoxelRotation rotation) 
        {
            if (!AllowedRotationAxes.x && rotation.x != 0)
            {
                return false;
            }
            if (!AllowedRotationAxes.y && rotation.y != 0)
            {
                return false;
            }
            if (!AllowedRotationAxes.z && rotation.z != 0)
            {
                return false;
            }

            int numRotationAxes = 0;
            if (rotation.x != 0)
            {
                numRotationAxes++;
            }

            if (rotation.y != 0)
            {
                numRotationAxes++;
            }

            if (rotation.z != 0)
            {
                numRotationAxes++;
            }

            if (numRotationAxes> MaximumSimultaneousRotations)
            {
                return false;
            }

            if (allowedRotationsX.Count > 0 && !allowedRotationsX.Contains((AllowedRotationValue)rotation.x))
            {
                return false;
            }

            if (allowedRotationsY.Count > 0 && !allowedRotationsY.Contains((AllowedRotationValue)rotation.y))
            {
                return false;
            }

            if (allowedRotationsZ.Count > 0 && !allowedRotationsZ.Contains((AllowedRotationValue)rotation.z))
            {
                return false;
            }

            return true;
        }


        [System.Serializable]
        public struct AllowedRotationValue
        {
            [Range(0,3)]
            public byte r;

            public static implicit operator AllowedRotationValue(byte b) => new AllowedRotationValue() { r = b};
            public static explicit operator AllowedRotationValue(int i) => new AllowedRotationValue() { r = (byte)i};
        }
    }
}