using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework;

namespace UniVox.Implementations.ChunkData
{
    public class OctreeVoxelStorage : IVoxelStorageImplementation, IEquatable<OctreeVoxelStorage>
    {
        private interface INode : IEquatable<INode>
        {
            INode GetChild(int index);
            INode MakeChild(int index, bool isLeaf);
            void RemoveChild(int index);

            void SetChild(int index, INode child);

            bool IsLeaf { get; }

            bool IsEmpty { get; }
        }

        /// <summary>
        /// Leaf nodes at the maximum possible depth,
        /// these contain 8 voxel ids
        /// </summary>
        private class TerminalLeafNode : INode
        {
            public VoxelTypeID[] children = new VoxelTypeID[8];

            public bool IsEmpty => !children.Any((id) => id != VoxelTypeID.AIR_ID);
            public bool IsLeaf => true;

            public bool Equals(INode other)
            {
                if (other == null)
                {
                    return false;
                }
                if (other is TerminalLeafNode term)
                {
                    for (int i = 0; i < children.Length; i++)
                    {
                        if (!children[i].Equals(term.children[i]))
                        {
                            return false;
                        }
                    }
                    return true;
                }
                return false;
            }

            public INode GetChild(int index)
            {
                return null;
            }

            public INode MakeChild(int index, bool isLeaf)
            {
                throw new NotImplementedException();
            }

            public void RemoveChild(int index)
            {
                children[index] = default;
            }

            public void SetChild(int index, INode child)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Node that represents a section of space that's all one voxel type
        /// </summary>
        private class MetaLeafNode : INode
        {
            public bool IsEmpty => false;
            public bool IsLeaf => true;
            public VoxelTypeID voxel;

            public INode GetChild(int index)
            {
                throw new NotImplementedException();
            }

            public INode MakeChild(int index, bool isLeaf)
            {
                throw new NotImplementedException();
            }

            public void RemoveChild(int index)
            {
                throw new NotImplementedException();
            }

            public void SetChild(int index, INode child)
            {
                throw new NotImplementedException();
            }

            public INode Split(bool isTerminal)
            {
                INode node;
                if (isTerminal)
                {
                    var terminal = new TerminalLeafNode();
                    for (int i = 0; i < 8; i++)
                    {
                        terminal.children[i] = voxel;
                    }
                    node = terminal;
                }
                else
                {
                    node = new Node();
                    for (int i = 0; i < 8; i++)
                    {
                        node.SetChild(i, new MetaLeafNode() { voxel = voxel });
                    }
                }
                return node;
            }

            public bool Equals(INode other)
            {
                if (other == null)
                {
                    return false;
                }
                if (other is MetaLeafNode meta)
                {
                    return meta.voxel.Equals(voxel);
                }
                return false;
            }
        }

        /// <summary>
        /// Child layout: +z+z+z+z -z-z-z-z
        ///               +y+y-y-y +y+y-y-y
        ///               +x-x+x-x +x-x+x-x
        /// Where + stands for "near" and - stands for "far". I.e, the first 4 elements
        /// have z coordinates closer to 0 in local coordinates.
        /// </summary>
        private class Node : INode
        {
            public INode[] children = new INode[8];
            public bool IsLeaf => false;
            public bool IsEmpty => !children.Any((child) => child != null);

            public bool Equals(INode other)
            {
                if (other == null)
                {
                    return false;
                }
                if (other is Node node)
                {
                    for (int i = 0; i < children.Length; i++)
                    {
                        if (children[i] != null)
                        {
                            if (!children[i].Equals(node.children[i]))
                            {
                                return false;
                            }
                        }
                        else if (node.children[i] != null)
                        {
                            return false;
                        }
                    }
                    return true;
                }
                return false;
            }

            public INode GetChild(int index)
            {
                return children[index];
            }

            public INode MakeChild(int index, bool isLeaf)
            {
                INode child;
                if (isLeaf)
                {
                    child = new TerminalLeafNode();
                }
                else
                {
                    child = new Node();
                }
                children[index] = child;
                return child;
            }

            public void RemoveChild(int index)
            {
                children[index] = null;
            }

            public void SetChild(int index, INode child)
            {
                children[index] = child;
            }
        }

        private INode root;
        private Vector3Int rootDimensions;
        private readonly Vector3Int dimensionsOfTerminalNodes = new Vector3Int(2, 2, 2);
        public bool IsEmpty { get => root.IsEmpty; }
        int dxdy;

        //Empty constructor requiring use of the Initialise functions to do anything useful
        public OctreeVoxelStorage()
        {

        }

        public OctreeVoxelStorage(Vector3Int dimensions)
        {
            InitialiseEmpty(dimensions);
        }

        public OctreeVoxelStorage(Vector3Int dimensions, VoxelTypeID[] initialData)
        {
            InitialiseWithData(dimensions, initialData);
        }

        public void InitialiseEmpty(Vector3Int dimensions)
        {
            Assert.IsTrue(dimensions.All((_) => _ > 0), "Dimensions must be positive and non-zero");

            if (!dimensions.All((_) => _ == dimensions.x))
            {
                throw new ArgumentException("SVO does not currently support dimensions that are not equal on all axes");
            }

            if ((dimensions.x & (dimensions.x - 1)) != 0)
            {
                throw new ArgumentException("SVO does not currently support dimensions that are not a power of 2");
            }

            rootDimensions = dimensions;
            uint capacity = (uint)dimensions.x * (uint)dimensions.y * (uint)dimensions.z;

            if (capacity > 8)
            {
                root = new Node();
            }
            else
            {
                root = new TerminalLeafNode();
            }
            dxdy = rootDimensions.x * rootDimensions.y;
        }

        public void InitialiseWithData(Vector3Int dimensions, VoxelTypeID[] initialData)
        {
            InitialiseEmpty(dimensions);
            FromArray(initialData);
        }

        /// <summary>
        /// More efficient (than calling set for each element of the array) way to 
        /// initialise an SVO from an array of data.
        /// </summary>
        /// <param name="array"></param>
        private void FromArray(VoxelTypeID[] array)
        {
            Profiler.BeginSample("SVO FromArray");
            FromArrayRecursive(root, Vector3Int.zero, rootDimensions, array);
            Profiler.EndSample();
        }

        /// <summary>
        /// Recursive implementation for FromArray
        /// </summary>
        /// <param name="node"></param>
        /// <param name="offset"></param>
        /// <param name="nodeDimensions"></param>
        /// <param name="array"></param>
        /// <param name="dxdy"></param>
        /// <returns>IsEmpty,IsSolid</returns>
        private (bool, bool) FromArrayRecursive(INode node, Vector3Int offset, Vector3Int nodeDimensions, VoxelTypeID[] array)
        {
            bool empty = true;
            bool solid = true;
            var childDimensions = nodeDimensions / 2;
            if (!(node is TerminalLeafNode leaf))
            {
                bool childIsLeaf = childDimensions.x == dimensionsOfTerminalNodes.x;
                for (int i = 0; i < 8; i++)
                {
                    var child = node.MakeChild(i, childIsLeaf);
                    var localCoords = getLocalCoords(i);
                    var childOffset = offset + (localCoords * childDimensions);
                    var (childEmpty, childSolid) = FromArrayRecursive(child, childOffset, childDimensions, array);
                    if (childEmpty)
                    {
                        node.RemoveChild(i);
                        solid = false;
                    }
                    else
                    {
                        empty = false;
                        if (childSolid)
                        {
                            if (childSolid && !(child is MetaLeafNode))
                            {
                                //Swap previous child for a meta node
                                var flat = Utils.Helpers.MultiIndexToFlat(childOffset.x, childOffset.y, childOffset.z, rootDimensions.x, dxdy);
                                var voxel = array[flat];
                                node.SetChild(i, new MetaLeafNode() { voxel = voxel });
                            }
                        }
                        else
                        {
                            solid = false;
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    var leafOffset = getLocalCoords(i) + offset;
                    var flat = Utils.Helpers.MultiIndexToFlat(leafOffset.x, leafOffset.y, leafOffset.z, rootDimensions.x, dxdy);
                    var voxel = array[flat];

                    if (voxel != VoxelTypeID.AIR_ID)
                    {
                        leaf.children[i] = voxel;
                        empty = false;
                    }
                }
                solid = leaf.children.All(_ => _ == leaf.children[0]);
            }
            return (empty, solid);
        }

        /// <summary>
        /// More efficient conversion to array than naively indexing.
        /// This takes advantage of the known ordering of children to iterate over the tree
        /// in a sensible order, without having to start a search from the root for
        /// every single node.
        /// 
        /// The array returned is a flattened 3d array, in the same format used by the rest
        /// of the framework.
        /// </summary>
        /// <returns></returns>
        public VoxelTypeID[] ToArray()
        {
            Profiler.BeginSample("SVO ToArray");

            VoxelTypeID[] array = new VoxelTypeID[rootDimensions.x * rootDimensions.y * rootDimensions.z];
            Stack<INode> nodeStack = new Stack<INode>();
            nodeStack.Push(root);
            Stack<Vector3Int> offsetStack = new Stack<Vector3Int>();
            offsetStack.Push(new Vector3Int(0, 0, 0));
            Stack<Vector3Int> dimensionsStack = new Stack<Vector3Int>();
            dimensionsStack.Push(rootDimensions);

            int dxdy = rootDimensions.x * rootDimensions.y;

            while (nodeStack.Count > 0)
            {
                INode current = nodeStack.Pop();
                var offset = offsetStack.Pop();
                var currentDimensions = dimensionsStack.Pop();
                var childDimensions = currentDimensions / 2;

                if (!current.IsLeaf)
                {
                    //Not a leaf

                    for (int i = 0; i < 8; i++)
                    {

                        var child = current.GetChild(i);
                        if (child != null)
                        {
                            var childOffset = getLocalCoords(i) * childDimensions;

                            nodeStack.Push(child);
                            offsetStack.Push(offset + childOffset);
                            dimensionsStack.Push(childDimensions);
                        }
                    }
                }
                else
                {
                    if (current is TerminalLeafNode terminal)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            var childOffset = getLocalCoords(i) + offset;
                            var flat = Utils.Helpers.MultiIndexToFlat(childOffset.x, childOffset.y, childOffset.z, rootDimensions.x, dxdy);
                            array[flat] = terminal.children[i];
                        }
                    }
                    else if (current is MetaLeafNode meta)
                    {
                        for (int z = 0; z < currentDimensions.z; z++)
                        {
                            for (int y = 0; y < currentDimensions.y; y++)
                            {
                                for (int x = 0; x < currentDimensions.x; x++)
                                {
                                    var flat = Utils.Helpers.MultiIndexToFlat(offset.x + x, offset.y + y, offset.z + z, rootDimensions.x, dxdy);
                                    array[flat] = meta.voxel;
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Unkown leaf type");
                    }
                }

            }

            Profiler.EndSample();
            return array;
        }

        /// <summary>
        /// Set the voxel type at the given local coordinates, creating or removing nodes
        /// as necessary.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public void Set(int x, int y, int z, VoxelTypeID id)
        {
            PlaceVoxel_IsEmptyIsSolid(root, new Vector3Int(x, y, z), rootDimensions, id);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="nodeLocal"></param>
        /// <param name="nodeDimensions"></param>
        /// <param name="voxel"></param>
        ///<returns>IsEmpty,IsSolid</returns>
        private (bool, bool) PlaceVoxel_IsEmptyIsSolid(INode node, Vector3Int nodeLocal, Vector3Int nodeDimensions, VoxelTypeID voxel)
        {
            bool empty = true;
            bool solid = true;
            var childDimensions = nodeDimensions / 2;
            var (index, childNodeLocal) = getIndexAndAdjustedNodeLocalCoords(nodeLocal, childDimensions);

            if (!node.IsLeaf)
            {
                var child = node.GetChild(index);
                bool childIsTerminal = childDimensions.x == dimensionsOfTerminalNodes.x;
                if (child == null)
                {
                    child = node.MakeChild(index, childIsTerminal);
                }

                if (child is MetaLeafNode meta && !voxel.Equals(meta.voxel))
                {
                    //Must split the meta node
                    child = meta.Split(childIsTerminal);
                    node.SetChild(index, child);
                }

                var (childEmpty, childSolid) = PlaceVoxel_IsEmptyIsSolid(child, childNodeLocal, childDimensions, voxel);
                if (childEmpty)
                {
                    node.RemoveChild(index);
                }
                else
                {
                    if (childSolid && !(child is MetaLeafNode))
                    {
                        //Swap previous child for a meta node
                        node.SetChild(index, new MetaLeafNode() { voxel = voxel });
                    }
                }

                //Check children for emptiness and solidity
                for (int i = 0; i < 8; i++)
                {
                    var tmpChild = node.GetChild(i);
                    if (tmpChild == null)
                    {
                        continue;
                    }
                    else
                    {
                        empty = false;
                    }
                    if (tmpChild is MetaLeafNode tmpMeta)
                    {
                        if (!tmpMeta.voxel.Equals(voxel))
                        {
                            solid = false;
                        }
                    }
                    else
                    {
                        solid = false;
                        continue;
                    }
                }
            }
            else
            {
                if (node is TerminalLeafNode leaf)
                {
                    leaf.children[index] = voxel;
                    empty = leaf.IsEmpty;
                    solid = leaf.children.All(_ => _ == leaf.children[0]);
                }
                else if (node is MetaLeafNode meta)
                {
                    empty = false;
                    solid = true;
                }
                else
                {
                    throw new Exception("Unkown leaf type");
                }
            }
            return (empty, solid);
        }

        /// <summary>
        /// Get the voxel type at the given local coordinates
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public VoxelTypeID Get(int x, int y, int z)
        {
            Profiler.BeginSample("SVO Get");

            var voxel = getRecursive(root, new Vector3Int(x, y, z), rootDimensions);
            Profiler.EndSample();
            return voxel;
        }

        private VoxelTypeID getRecursive(INode node, Vector3Int nodeLocal, Vector3Int nodeDimensions)
        {
            Vector3Int childDimensions = nodeDimensions / 2;
            var (index, childNodeLocal) = getIndexAndAdjustedNodeLocalCoords(nodeLocal, childDimensions);
            if (node == null)
            {
                return (VoxelTypeID)VoxelTypeID.AIR_ID;
            }
            if (node.IsLeaf)
            {
                if (node is MetaLeafNode meta)
                {
                    return meta.voxel;
                }
                else if (node is TerminalLeafNode terminal)
                {
                    return terminal.children[index];
                }
                else
                {
                    throw new Exception("Unkown leaf type");
                }
            }
            else
            {
                return getRecursive(node.GetChild(index), childNodeLocal, childDimensions);
            }
        }

        /// <summary>
        /// Gets the child index corresponding to the local coordinates given,
        /// and adjusts the local coordinates to be local to the child.
        /// </summary>
        /// <param name="nodeLocal"></param>
        /// <param name="childDimensions"></param>
        /// <returns></returns>
        public int getIndexAndAdjustNodeLocalCoords(ref Vector3Int nodeLocal, Vector3Int childDimensions)
        {
            int index = 0;
            if (nodeLocal.z >= childDimensions.z)
            {
                index += 4;
                nodeLocal.z -= childDimensions.z;
            }
            if (nodeLocal.y >= childDimensions.y)
            {
                index += 2;
                nodeLocal.y -= childDimensions.y;
            }
            if (nodeLocal.x >= childDimensions.x)
            {
                index += 1;
                nodeLocal.x -= childDimensions.x;
            }
            return index;
        }

        public (int, Vector3Int) getIndexAndAdjustedNodeLocalCoords(Vector3Int nodeLocal, Vector3Int childDimensions)
        {
            int index = 0;
            if (nodeLocal.z >= childDimensions.z)
            {
                index += 4;
                nodeLocal.z -= childDimensions.z;
            }
            if (nodeLocal.y >= childDimensions.y)
            {
                index += 2;
                nodeLocal.y -= childDimensions.y;
            }
            if (nodeLocal.x >= childDimensions.x)
            {
                index += 1;
                nodeLocal.x -= childDimensions.x;
            }
            return (index, nodeLocal);
        }

        public Vector3Int getLocalCoords(int index)
        {
            return new Vector3Int(index % 2, (index / 2) % 2, (index / 4) % 2);
        }

        public bool Equals(OctreeVoxelStorage other)
        {
            if (root == null)
            {
                return other.root == null;
            }
            else
            {
                return root.Equals(other.root);
            }
        }
    }
}