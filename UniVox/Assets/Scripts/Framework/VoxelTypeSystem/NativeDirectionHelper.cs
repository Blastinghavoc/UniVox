using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utils;

namespace UniVox.Framework
{
    [BurstCompile]
    public struct NativeDirectionHelper 
    {
        [ReadOnly] public NativeArray<int3> DirectionVectors;
        [ReadOnly] public NativeArray<byte> DirectionOpposites;

        [ReadOnly] public NativeArray<byte> XRotationMatix;
        [ReadOnly] public NativeArray<byte> YRotationMatix;
        [ReadOnly] public NativeArray<byte> ZRotationMatix;

        private const float PI_2 = math.PI / 2;

        public byte GetDirectionAfterRotation(byte dir, VoxelRotation rot) 
        {
            for (int i = 0; i < rot.y; i++)
            {
                dir = YRotationMatix[dir];
            }

            for (int i = 0; i < rot.x; i++)
            {
                dir = XRotationMatix[dir];
            }

            for (int i = 0; i < rot.z; i++)
            {
                dir = ZRotationMatix[dir];
            }

            return dir;
        }

        public quaternion GetRotationQuat(VoxelRotation rotation) 
        {
            return quaternion.Euler(PI_2 * rotation.x, PI_2 * rotation.y, PI_2 * rotation.z, math.RotationOrder.YXZ);
        }
    }

    public static class DirectionHelperExtensions 
    {
        public static NativeDirectionHelper Create() 
        {
            NativeDirectionHelper native = new NativeDirectionHelper();

            native.DirectionVectors = new NativeArray<int3>(Directions.NumDirections, Allocator.Persistent);
            for (int i = 0; i < Directions.NumDirections; i++)
            {
                var vec = Directions.IntVectors[i];
                native.DirectionVectors[i] = vec.ToNative();
            }

            native.DirectionOpposites = new NativeArray<byte>(Directions.Oposite, Allocator.Persistent);

            var xArr = new byte[Directions.Array.Length];
            var yArr = new byte[Directions.Array.Length];
            var zArr = new byte[Directions.Array.Length];

            for (int i = 0; i < xArr.Length; i++)
            {
                xArr[i] = CardinalRotateX((byte)i);
            }

            for (int i = 0; i < yArr.Length; i++)
            {
                yArr[i] = CardinalRotateY((byte)i);
            }

            for (int i = 0; i < zArr.Length; i++)
            {
                zArr[i] = CardinalRotateZ((byte)i);
            }

            native.XRotationMatix = new NativeArray<byte>(xArr, Allocator.Persistent);
            native.YRotationMatix = new NativeArray<byte>(yArr, Allocator.Persistent);
            native.ZRotationMatix = new NativeArray<byte>(zArr, Allocator.Persistent);

            return native;
        }

        public static void Dispose(this NativeDirectionHelper native) 
        {
            native.XRotationMatix.SmartDispose();
            native.YRotationMatix.SmartDispose();
            native.ZRotationMatix.SmartDispose();
            native.DirectionOpposites.SmartDispose();
            native.DirectionVectors.SmartDispose();
        }

        /// <summary>
        /// Rotate 90 degrees clockwise about the Y axis
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        private static byte CardinalRotateY(byte original)
        {
            if (original == Directions.UP || original == Directions.DOWN)
            {
                return original;
            }

            switch (original)
            {
                case Directions.EAST:
                    return Directions.SOUTH;
                case Directions.SOUTH:
                    return Directions.WEST;
                case Directions.WEST:
                    return Directions.NORTH;
                case Directions.NORTH:
                    return Directions.EAST;
                default:
                    break;
            }
            throw new ArgumentException("Direction not valid");
        }

        /// <summary>
        /// Rotate 90 degrees clockwise about the X axis
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        private static byte CardinalRotateX(byte original)
        {
            if (original == Directions.EAST || original == Directions.WEST)
            {
                return original;
            }

            switch (original)
            {
                case Directions.NORTH:
                    return Directions.DOWN;
                case Directions.DOWN:
                    return Directions.SOUTH;
                case Directions.SOUTH:
                    return Directions.UP;
                case Directions.UP:
                    return Directions.NORTH;
                default:
                    break;
            }
            throw new ArgumentException("Direction not valid");
        }

        /// <summary>
        /// Rotate 90 degrees clockwise about the Y axis
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        private static byte CardinalRotateZ(byte original)
        {
            if (original == Directions.NORTH || original == Directions.SOUTH)
            {
                return original;
            }

            switch (original)
            {
                case Directions.EAST:
                    return Directions.UP;
                case Directions.DOWN:
                    return Directions.EAST;
                case Directions.WEST:
                    return Directions.DOWN;
                case Directions.UP:
                    return Directions.WEST;
                default:
                    break;
            }
            throw new ArgumentException("Direction not valid");
        }
    }
}