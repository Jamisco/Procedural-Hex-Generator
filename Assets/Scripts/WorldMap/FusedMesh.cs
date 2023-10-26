using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Assets.Scripts.WorldMap
{
    public class FusedMesh
    {
        private List<int> MeshHashes;
        // vertex and triangle size
        private List<(int vertexCount, int triangleCount)> MeshSizes;

        private List<Vector3> Vertices;
        private List<int> Triangles;
        private List<Vector2> UVs;
        private List<Color> Colors;

        public int VertexCount { get { return Vertices.Count; } }
        public int TriangleCount { get { return Triangles.Count; } }
        
        public Mesh Mesh;


        private void Init()
        {
            MeshHashes = new List<int>();
            MeshSizes = new List<(int, int)>();
            
            Vertices = new List<Vector3>();
            Triangles = new List<int>();
            Colors = new List<Color>();
            UVs = new List<Vector2>();

            Mesh = new Mesh();
            Mesh.MarkDynamic();
        }
        public FusedMesh()
        {
            Init();
        }

        public FusedMesh(List<Mesh> meshes, List<int> hashes, List<Vector3> offsets)
        {
            Init();

            if(! (meshes.Count == hashes.Count && meshes.Count == offsets.Count))
            {
                throw new Exception("List must be thesame size");
            }

            for (int i = 0; i < meshes.Count; i++)
            {
                AddMesh_NoUpdate(meshes[i], hashes[i], offsets[i]);
            }

            UpdateMesh();
        }
    
        private void AddMesh_NoUpdate(Mesh mesh, int hash, Vector3 offset)
        {
            int index = MeshHashes.IndexOf(hash);

            if (index != -1)
            {
                RemoveMesh(hash, index);
            }

            AddToList(hash, mesh.vertexCount, mesh.triangles.Length);

            AddMeshAtEnd(mesh, offset);
        }

        /// <summary>
        /// Returns true or false if mesh was successfully removed
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool RemoveMesh_NoUpdate(int hash, int position = -1)
        {
            // this allows us to skip having to refind the index again
            int index = position == -1 ? MeshHashes.IndexOf(hash) : position;

            if (index != -1)
            {
                var size = MeshSizes[index];

                int triIndex = 0;
                int vertIndex = 0;
                
                for (int i = 0; i < index; i++)
                {
                    triIndex += MeshSizes[i].triangleCount;
                    vertIndex += MeshSizes[i].vertexCount;
                }

                try
                {
                    Exception e = new Exception("Error when removing mesh");
                    
                    // error might occur if some of the below list are empty.
                    // this might be because they were never filled to begin with
                    Vertices.TryRemoveElementsInRange(vertIndex, size.vertexCount, out e);
                    Triangles.TryRemoveElementsInRange(triIndex, size.triangleCount, out e);
                    Colors.TryRemoveElementsInRange(vertIndex, size.vertexCount, out e);
                    UVs.TryRemoveElementsInRange(vertIndex, size.vertexCount, out e);

                }
                catch (Exception)
                {
                   
                }
                
                RemoveFromList(index);

                RecalculateTriangles(-size.vertexCount, triIndex);

                return true;
            }

            return false;
        }




        public void AddMesh(Mesh mesh, int hash, Vector3 offset)
        {
            AddMesh_NoUpdate(mesh, hash, offset);

            UpdateMesh();
        }
        public void RemoveMesh(int hash, int position = -1)
        {
            bool removed = RemoveMesh_NoUpdate(hash, position);

            if(removed)
            {
                UpdateMesh();
            }

        }

        private void AddToList(int hash, int vertexCount, int triangleCount)
        {
            MeshHashes.Add(hash);
            MeshSizes.Add((vertexCount, triangleCount));
        }
        private void RemoveFromList(int index)
        {
            MeshHashes.RemoveAt(index);
            MeshSizes.RemoveAt(index);
        }
        private void AddMeshAtEnd(Mesh aMesh, Vector3 offset)
        {
            List<Vector3> hexVertices = new List<Vector3>();
            List<int> hexTris = new List<int>();

            foreach (Vector3 v in aMesh.vertices)
            {
                hexVertices.Add(v + offset);
            }

            foreach (int tri in aMesh.triangles)
            {
                hexTris.Add(tri + Vertices.Count);
            }

            Vertices.AddRange(hexVertices);
            Triangles.AddRange(hexTris);
            Colors.AddRange(aMesh.colors);
            UVs.AddRange(aMesh.uv);
        }

        private void RecalculateTriangles(int offset, int startIndex = 0)
        {
            for (int i = startIndex; i < Triangles.Count; i++)
            {
                Triangles[i] += offset;
            }
        }

        public Mesh GetMesh()
        {
            Mesh mesh = new Mesh();

            mesh.vertices = Vertices.ToArray();

            mesh.triangles = Triangles.ToArray();
            mesh.colors = Colors.ToArray();
            mesh.uv = UVs.ToArray();

            return mesh;
        }

        public void SetMesh(ref Mesh mesh)
        {
            mesh.Clear();

            mesh.vertices = Vertices.ToArray();

            mesh.triangles = Triangles.ToArray();
            mesh.colors = Colors.ToArray();
            mesh.uv = UVs.ToArray();
        }
        private void UpdateMesh()
        {
            //It is important to call Clear before assigning new vertices or triangles. Unity always checks the supplied triangle indices whether they don't reference out of bounds vertices. Calling Clear then assigning vertices then triangles makes sure you never have out of bounds data.

            Mesh.Clear();
           
            Mesh.vertices = Vertices.ToArray();
            Mesh.triangles = Triangles.ToArray();
            Mesh.colors = Colors.ToArray();
            Mesh.uv = UVs.ToArray();
        }

        public static implicit operator Mesh(FusedMesh f)
        {
            return f.Mesh;
        }
        public static Mesh CombineToSubmesh(List<FusedMesh> subMesh)
        {
            Mesh newMesh = new Mesh();
            
            CombineInstance[] tempArray = new CombineInstance[subMesh.Count];

            for (int i = 0; i < subMesh.Count; i++)
            {
                CombineInstance subInstance = new CombineInstance();

                subInstance.mesh = subMesh[i];

                tempArray[i] = subInstance;
            }
        
            newMesh.CombineMeshes(tempArray, false, false);

            return newMesh;
        }
        public Mesh CombineToSubmesh(Mesh subMesh)
        {
            Mesh newMesh = new Mesh();
            
            newMesh = Mesh;
            
            CombineInstance subInstance = new CombineInstance();

            subInstance.mesh = subMesh;

            CombineInstance[] tempArray = new CombineInstance[0];

            newMesh.CombineMeshes(tempArray);

            return newMesh;
        }

        public static void CloneMesh(Mesh parent, ref Mesh clone)
        {
            clone.vertices = parent.vertices;
            clone.triangles = parent.triangles;
            clone.colors = parent.colors;
            clone.uv = parent.uv;
        }
    }
}
