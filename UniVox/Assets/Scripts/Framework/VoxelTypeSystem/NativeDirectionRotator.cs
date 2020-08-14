using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UniVox.Framework.Common;
using Utils;

namespace UniVox.Framework
{
    [BurstCompile]
    public struct NativeDirectionRotator : IDisposable
    {
        [ReadOnly] public NativeArray<int3> DirectionVectors;
        [ReadOnly] public NativeArray<Direction> DirectionOpposites;

        [ReadOnly] public NativeArray<Direction> XRotationMatix;
        [ReadOnly] public NativeArray<Direction> YRotationMatix;
        [ReadOnly] public NativeArray<Direction> ZRotationMatix;

        private const float PI_2 = math.PI / 2;

        public Direction GetDirectionAfterRotation(Direction dir, VoxelRotation rot)
        {
            for (int i = 0; i < rot.y; i++)
            {
                dir = YRotationMatix[(byte)dir];
            }

            for (int i = 0; i < rot.x; i++)
            {
                dir = XRotationMatix[(byte)dir];
            }

            for (int i = 0; i < rot.z; i++)
            {
                dir = ZRotationMatix[(byte)dir];
            }

            return dir;
        }

        public Direction GetDirectionBeforeRotation(Direction dir, VoxelRotation rot)
        {
            rot = rot.Inverse();//Opposite rotation

            //Opposite order of rotations
            for (int i = 0; i < rot.z; i++)
            {
                dir = ZRotationMatix[(byte)dir];
            }

            for (int i = 0; i < rot.x; i++)
            {
                dir = XRotationMatix[(byte)dir];
            }

            for (int i = 0; i < rot.y; i++)
            {
                dir = YRotationMatix[(byte)dir];
            }

            return dir;
        }

        public quaternion GetRotationQuat(VoxelRotation rotation)
        {
            return quaternion.Euler(PI_2 * rotation.x, PI_2 * rotation.y, PI_2 * rotation.z, math.RotationOrder.YXZ);
        }

        public void Dispose()
        {
            XRotationMatix.SmartDispose();
            YRotationMatix.SmartDispose();
            ZRotationMatix.SmartDispose();
            DirectionOpposites.SmartDispose();
            DirectionVectors.SmartDispose();
        }
    }

    public static class DirectionRotatorExtensions
    {
        public static NativeDirectionRotator Create()
        {
            NativeDirectionRotator native = new NativeDirectionRotator();

            native.DirectionVectors = new NativeArray<int3>(DirectionExtensions.numDirections, Allocator.Persistent);
            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                var vec = DirectionExtensions.Vectors[i];
                native.DirectionVectors[i] = vec.ToNative();
            }

            native.DirectionOpposites = DirectionExtensions.Opposite.ToNative(Allocator.Persistent);

            var xArr = new Direction[DirectionExtensions.numDirections];
            var yArr = new Direction[DirectionExtensions.numDirections];
            var zArr = new Direction[DirectionExtensions.numDirections];

            for (int i = 0; i < xArr.Length; i++)
            {
                xArr[i] = CardinalRotateX((Direction)i);
            }

            for (int i = 0; i < yArr.Length; i++)
            {
                yArr[i] = CardinalRotateY((Direction)i);
            }

            for (int i = 0; i < zArr.Length; i++)
            {
                zArr[i] = CardinalRotateZ((Direction)i);
            }

            native.XRotationMatix = xArr.ToNative(Allocator.Persistent);
            native.YRotationMatix = yArr.ToNative(Allocator.Persistent);
            native.ZRotationMatix = zArr.ToNative(Allocator.Persistent);

            return native;
        }

        /// <summary>
        /// Rotate 90 degrees clockwise about the Y axis
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        private static Direction CardinalRotateY(Direction original)
        {
            if (original == Direction.up || original == Direction.down)
            {
                return original;
            }

            switch (original)
            {
                case Direction.east:
                    return Direction.south;
                case Direction.south:
                    return Direction.west;
                case Direction.west:
                    return Direction.north;
                case Direction.north:
                    return Direction.east;
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
        private static Direction CardinalRotateX(Direction original)
        {
            if (original == Direction.east || original == Direction.west)
            {
                return original;
            }

            switch (original)
            {
                case Direction.north:
                    return Direction.down;
                case Direction.down:
                    return Direction.south;
                case Direction.south:
                    return Direction.up;
                case Direction.up:
                    return Direction.north;
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
        private static Direction CardinalRotateZ(Direction original)
        {
            if (original == Direction.north || original == Direction.south)
            {
                return original;
            }

            switch (original)
            {
                case Direction.east:
                    return Direction.up;
                case Direction.down:
                    return Direction.east;
                case Direction.west:
                    return Direction.down;
                case Direction.up:
                    return Direction.west;
                default:
                    break;
            }
            throw new ArgumentException("Direction not valid");
        }
    }
}