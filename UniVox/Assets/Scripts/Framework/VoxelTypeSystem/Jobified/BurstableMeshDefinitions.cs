using UnityEngine;
using System.Collections;
using Unity.Burst;
using Unity.Collections;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace UniVox.Framework.Jobified
{
    /// <summary>
    /// Defines a mesh for a voxel type.
    /// E.g cube
    /// </summary>
    public struct BurstableMeshDefinition
    {
        public NativeArray<Vector3> vertices;
        public NativeArray<Vector2> uvs;
        public NativeArray<Vector3> normals;

        public NativeArray<bool> isFaceSolid;

        //Each entry is a triple index into the vertices, uvs and normals arrays
        public NativeArray<TripleIndex> triangleIndices;

        //Start-end indices of triangles for each of the 6 faces
        public NativeArray<StartEnd> faceTriangles;

    }

    public struct StartEnd 
    {
        public int start;
        public int end;//Inclusive
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
                vertices = new NativeArray<Vector3>(def.AllVertices, Allocator.Persistent),
                uvs = new NativeArray<Vector2>(def.AllUvs, Allocator.Persistent),
                normals = new NativeArray<Vector3>(def.AllNormals,Allocator.Persistent),            
                isFaceSolid = new NativeArray<bool>(def.Faces.Length,Allocator.Persistent),
                           
            };

            List<TripleIndex> triangleIndicesTmp = new List<TripleIndex>();
            List<StartEnd> faceTrianglesTmp = new List<StartEnd>();

            for (int i = 0; i < def.Faces.Length; i++)
            {
                StartEnd currentFace = new StartEnd();
                currentFace.start = triangleIndicesTmp.Count;

                var faceDef = def.Faces[i];
                burstable.isFaceSolid[i] = faceDef.isSolid;

                foreach (var tri in faceDef.Triangles)
                {
                    TripleIndex index = new TripleIndex()
                    {
                        vertex = faceDef.UsedVertices[tri],
                        uv = faceDef.UsedUvs[tri],
                        normal = faceDef.UsedNormals[tri]
                    };
                    triangleIndicesTmp.Add(index);
                }

                currentFace.end = triangleIndicesTmp.Count - 1;
                faceTrianglesTmp.Add(currentFace);
            }

            burstable.triangleIndices = new NativeArray<TripleIndex>(triangleIndicesTmp.ToArray(), Allocator.Persistent);
            burstable.faceTriangles = new NativeArray<StartEnd>(faceTrianglesTmp.ToArray(), Allocator.Persistent);

            return burstable;
        }
    }
}