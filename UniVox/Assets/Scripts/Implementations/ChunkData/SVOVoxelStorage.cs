using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UniVox.Framework;

namespace UniVox.Implementations.ChunkData
{
    public class SVOVoxelStorage: IVoxelStorageImplementation
    {
        public interface INode
        {
            INode GetChild(int index);
            INode MakeChild(int index,bool isLeaf);
            void RemoveChild(int index);

            bool IsEmpty { get; }
        }

        public class LeafNode : INode
        {
            public VoxelTypeID[] children = new VoxelTypeID[8];

            public bool IsEmpty => !children.Any((id) => id != VoxelTypeManager.AIR_ID);

            public INode GetChild(int index)
            {
                return null;
            }

            public INode MakeChild(int index,bool isLeaf)
            {
                throw new NotImplementedException();
            }

            public void RemoveChild(int index)
            {
                children[index] = default;
            }
        }

        /// <summary>
        /// Child layout: +z+z+z+z -z-z-z-z
        ///               +y+y-y-y +y+y-y-y
        ///               +x-x+x-x +x-x+x-x
        /// Where + stands for "near" and - stands for "far". I.e, the first 4 elements
        /// have z coordinates closer to 0 in local coordinates.
        /// </summary>
        public class Node : INode
        {
            public INode[] children = new INode[8];

            public bool IsEmpty => !children.Any((child) => child != null);

            public INode GetChild(int index)
            {
                return children[index];
            }

            public INode MakeChild(int index,bool isLeaf)
            {
                INode child;
                if (isLeaf)
                {
                    child = new LeafNode();
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
        }

        private INode root;
        private Vector3Int dimensions;
        private readonly Vector3Int dimensionsOfLeafNodes = new Vector3Int(2,2,2);
        public bool IsEmpty { get => root.IsEmpty; }

        //Empty constructor requiring use of the Initialise functions to do anything useful
        public SVOVoxelStorage() 
        { 
        
        }

        public SVOVoxelStorage(Vector3Int dimensions)
        {
            InitialiseEmpty(dimensions);
        }

        public SVOVoxelStorage(Vector3Int dimensions,VoxelTypeID[] initialData)
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

            this.dimensions = dimensions;
            uint capacity = (uint)dimensions.x * (uint)dimensions.y * (uint)dimensions.z;

            if (capacity > 8)
            {
                root = new Node();
            }
            else
            {
                root = new LeafNode();
            }
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
            int dxdy = dimensions.x * dimensions.y;
            FromArrayProcessChildIsEmpty(root, Vector3Int.zero, dimensions, array, dxdy);
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
        /// <returns></returns>
        private bool FromArrayProcessChildIsEmpty(INode node, Vector3Int offset, Vector3Int nodeDimensions,VoxelTypeID[] array,int dxdy) 
        {
            bool empty = true;
            var childDimensions = nodeDimensions / 2;
            if (!(node is LeafNode leaf))
            {
                bool childIsLeaf = childDimensions.x == dimensionsOfLeafNodes.x;
                for (int i = 0; i < 8; i++)
                {
                    var child = node.MakeChild(i, childIsLeaf);
                    var childOffset = getLocalCoords(i) * childDimensions;
                    if (FromArrayProcessChildIsEmpty(child, offset + childOffset, childDimensions,array,dxdy))
                    {
                        node.RemoveChild(i);
                    }
                    else
                    {
                        empty = false;
                    }
                }
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    var leafOffset = getLocalCoords(i) + offset;
                    var flat = Utils.Helpers.MultiIndexToFlat(leafOffset.x, leafOffset.y, leafOffset.z, dimensions.x, dxdy);
                    var voxel = array[flat];
                    if (voxel != VoxelTypeManager.AIR_ID)
                    {
                        leaf.children[i] = voxel;
                        empty = false;
                    }
                }
            }
            return empty;
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

            VoxelTypeID[] array = new VoxelTypeID[dimensions.x * dimensions.y * dimensions.z];
            Stack<INode> nodeStack = new Stack<INode>();
            nodeStack.Push(root);
            Stack<Vector3Int> offsetStack = new Stack<Vector3Int>();
            offsetStack.Push(new Vector3Int(0, 0, 0));
            Stack<Vector3Int> dimensionsStack = new Stack<Vector3Int>();
            dimensionsStack.Push(dimensions);

            int dxdy = dimensions.x * dimensions.y;

            while (nodeStack.Count > 0)
            {
                INode current = nodeStack.Pop();
                var offset = offsetStack.Pop();
                var currentDimensions = dimensionsStack.Pop();
                var childDimensions = currentDimensions / 2;

                if (!(current is LeafNode leaf))
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
                    //Leaf
                    for (int i = 0; i < 8; i++)
                    {
                        var childOffset = getLocalCoords(i) + offset;
                        var flat = Utils.Helpers.MultiIndexToFlat(childOffset.x, childOffset.y, childOffset.z,dimensions.x,dxdy);
                        array[flat] = leaf.children[i];
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
            if (id == VoxelTypeManager.AIR_ID)
            {
                Remove(x, y, z);
            }
            else
            {
                Place(x, y, z, id);
            }

        }

        private void Remove(int x, int y, int z)
        {
            Profiler.BeginSample("SVO Remove");

            INode current = root;
            //Assuming dimensions are all powers of 2, so this is lossless.
            Vector3Int childDimensions = dimensions / 2;
            Vector3Int nodeLocal = new Vector3Int(x, y, z);
            Stack<INode> path = new Stack<INode>();
            Stack<int> indices = new Stack<int>();
            int index;
            LeafNode leaf;
            while ((leaf = current as LeafNode) == null)//While the current node is not a leaf node
            {
                index = getIndexAndAdjustNodeLocalCoords(ref nodeLocal, childDimensions);

                var child = current.GetChild(index);
                if (child == null)
                {
                    Profiler.EndSample();
                    return;//location doesn't exist anyway, nothing to remove.
                }
                path.Push(current);
                indices.Push(index);
                current = child;

                childDimensions = childDimensions / 2;
            }

            index = getIndexAndAdjustNodeLocalCoords(ref nodeLocal, childDimensions);            
            leaf.RemoveChild(index);

            //Prune any empty nodes

            while (current.IsEmpty)
            {
                if (path.Count > 0)
                {
                    var parent = path.Pop();
                    index = indices.Pop();
                    parent.RemoveChild(index);
                    current = parent;
                }
                else
                {
                    Profiler.EndSample();
                    return;
                }
            }

            Profiler.EndSample();
        }

        /// <summary>
        /// Place the given id at the given local coordinates, assuming the id is
        /// not air.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <param name="id"></param>
        private void Place(int x, int y, int z, VoxelTypeID id)
        {
            Profiler.BeginSample("SVO Place");

            INode current = root;
            //Assuming dimensions are all powers of 2, so this is lossless.
            Vector3Int childDimensions = dimensions / 2;
            Vector3Int nodeLocal = new Vector3Int(x, y, z);
            int index;
            LeafNode leaf;
            while ((leaf = current as LeafNode) == null)//While the current node is not a leaf node
            {
                index = getIndexAndAdjustNodeLocalCoords(ref nodeLocal, childDimensions);

                var child = current.GetChild(index);
                if (child == null)
                {
                    bool childIsLeaf = childDimensions.x == dimensionsOfLeafNodes.x;
                    child = current.MakeChild(index,childIsLeaf);
                }
                current = child;

                childDimensions = childDimensions / 2;
            }

            index = getIndexAndAdjustNodeLocalCoords(ref nodeLocal, childDimensions);
            leaf.children[index] = id;

            Profiler.EndSample();
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

            INode current = root;
            //Assuming dimensions are all powers of 2, so this is lossless.
            Vector3Int childDimensions = dimensions / 2;
            Vector3Int nodeLocal = new Vector3Int(x, y, z);
            int index;
            LeafNode leaf;
            while ((leaf = current as LeafNode) == null)//While the current node is not a leaf node
            {
                index = getIndexAndAdjustNodeLocalCoords(ref nodeLocal, childDimensions);

                current = current.GetChild(index);

                if (current == null)
                {
                    Profiler.EndSample();
                    return new VoxelTypeID();
                }

                childDimensions = childDimensions / 2;
            }

            index = getIndexAndAdjustNodeLocalCoords(ref nodeLocal, childDimensions);

            Profiler.EndSample();
            return leaf.children[index];
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

        public Vector3Int getLocalCoords(int index) 
        {
            return new Vector3Int(index % 2, (index / 2) % 2, (index / 4) % 2);
        }        
    }
}