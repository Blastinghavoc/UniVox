using UnityEngine;
using System.Collections;
using Unity.Burst;
using Unity.Collections;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Unity.Mathematics;
using System;

namespace UniVox.Framework.Jobified
{
    /// <summary>
    /// Defines a mesh for a voxel type.
    /// E.g cube
    /// </summary>
    public struct BurstableMeshDefinition: IDisposable
    {
        //All nodes used by the mesh for all the faces, packed in order of face
        public NativeArray<Node> nodes;

        //Start-end indices of nodes used for each of the 6 faces
        public NativeArray<StartEnd> nodesUsedByFace;

        //Indexes of triangles used for each face (starting at 0 for each face), flatpacked
        public NativeArray<int> allRelativeTriangles;

        public NativeArray<StartEnd> relativeTrianglesByFace;

        public NativeArray<bool> isFaceSolid;

        public void Dispose()
        {
            if (nodes.IsCreated)
            {
                nodes.Dispose();
            }
            if (nodesUsedByFace.IsCreated)
            {
                nodesUsedByFace.Dispose();
            }
            if (allRelativeTriangles.IsCreated)
            {
                allRelativeTriangles.Dispose();
            }
            if (relativeTrianglesByFace.IsCreated)
            {
                relativeTrianglesByFace.Dispose();
            }
            if (isFaceSolid.IsCreated)
            {
                isFaceSolid.Dispose();
            }
        }
    }

    /// <summary>
    /// A unique mesh node
    /// </summary>
    public struct Node 
    {
        public float3 vertex;
        public float2 uv;
        public float3 normal;
    }

    public struct StartEnd 
    {
        public int start;
        public int end;//Exclusive

        public int Length { get => end - start; }
    }

    public struct TripleIndex 
    {
        public int vertex;
        public int uv;
        public int normal;
    }

    public static class BurstableMeshExtensions 
    {
        public static BurstableMeshDefinition ToBurst(this SOMeshDefinition def) {
            BurstableMeshDefinition burstable = new BurstableMeshDefinition() {     
                isFaceSolid = new NativeArray<bool>(def.Faces.Length,Allocator.Persistent),                           
            };

            List<Node> nodes = new List<Node>();
            List<StartEnd> nodesUsedByFace = new List<StartEnd>();
            List<int> allRelativeTriangles = new List<int>();
            List<StartEnd> relativeTrianglesByFace = new List<StartEnd>();

            for (int i = 0; i < def.Faces.Length; i++)
            {
                var faceDef = def.Faces[i];
                burstable.isFaceSolid[i] = faceDef.isSolid;

                StartEnd nodesUsedIndexers = new StartEnd();
                nodesUsedIndexers.start = nodes.Count;

                //Add all used nodes
                for (int j = 0; j < faceDef.UsedVertices.Length; j++)
                {
                    Node node = new Node()
                    {
                        vertex = faceDef.UsedVertices[j],
                        uv = faceDef.UsedUvs[j],
                        normal = faceDef.UsedNormals[j]
                    };

                    nodes.Add(node);
                }

                nodesUsedIndexers.end = nodes.Count;
                nodesUsedByFace.Add(nodesUsedIndexers);

                StartEnd trianglesIndexers = new StartEnd();
                trianglesIndexers.start = allRelativeTriangles.Count;
                //Add all relative triangles
                for (int j = 0; j < faceDef.Triangles.Length; j++)
                {
                    allRelativeTriangles.Add(faceDef.Triangles[j]);
                }
                trianglesIndexers.end = allRelativeTriangles.Count;

                relativeTrianglesByFace.Add(trianglesIndexers);
            }

            burstable.nodes = new NativeArray<Node>(nodes.ToArray(), Allocator.Persistent);
            burstable.nodesUsedByFace = new NativeArray<StartEnd>(nodesUsedByFace.ToArray(), Allocator.Persistent);
            burstable.allRelativeTriangles = new NativeArray<int>(allRelativeTriangles.ToArray(),Allocator.Persistent);
            burstable.relativeTrianglesByFace = new NativeArray<StartEnd>(relativeTrianglesByFace.ToArray(), Allocator.Persistent);

            return burstable;
        }
    }
}