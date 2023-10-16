using Assets.Scripts.Miscellaneous;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using static Assets.Scripts.WorldMap.Biosphere.SurfaceBody;
using static Assets.Scripts.WorldMap.GridManager;
using static Assets.Scripts.WorldMap.HexTile;
using static Assets.Scripts.Miscellaneous.ExtensionMethods;
using static Unity.Collections.AllocatorManager;

namespace Assets.Scripts.WorldMap
{
    // Due to the nature of this class, it will have to work hand in hand with the Gridmanager is order to properly share data. It is recommended that you refrain from doing specific functions that would limit the gridmanager from being able to controlt them. For example, a chunk should not be the one to highlight itself, but rather the gridmanager should be the one to do it. A chunk should not store the data, data about itself that isnt nessacary for it to display its meshes. We should force the Gridmanager to store that.

    // Max hex count per chunk is about 1600. See Gridmanager.CreateHexChunks() for details
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class HexChunk : MonoBehaviour
    {
        // since the hexes positions are set regardless of the position of the chunk, we simply spawn the chunk at 0,0
        public static Matrix4x4 SpawnPosition = Matrix4x4.Translate(Vector3.zero);

        public static Material MainMaterial;
        public static Material InstanceMaterial;

        public static BiomeVisual BiomeVisual;

        public List<HexTile> hexes;

        public ConcurrentDictionary<Vector2Int, HexTile> hexDictionary = new ConcurrentDictionary<Vector2Int, HexTile>();

        public Dictionary<BiomeProperties, List<HexTile>> biomeTiles = new Dictionary<BiomeProperties, List<HexTile>>();

        public ConcurrentDictionary<BiomeProperties, ConcurrentBag<HexTile>> bt = new ConcurrentDictionary<BiomeProperties, ConcurrentBag<HexTile>>();

        public Dictionary<BiomeProperties, FusedMesh> biomeFusedMeshes = new Dictionary<BiomeProperties, FusedMesh>();

        private List<Material> materials = new List<Material>();
        private List<MaterialPropertyBlock> blocks = new List<MaterialPropertyBlock>();

        // the highlight layer has to be above the base layer.
        // how high above do u want it to be
        private static readonly Vector3 HighlightLayerOffset = new Vector3(0, .001f, 0);


        // you must be aware that technically speaking, all these chunks are at position (0,0). It is their meshes/hexes that are place appriopriately
        public Vector2Int StartPosition;
        public BoundsInt ChunkBounds;
        private Bounds boundsCheck;

        RenderParams renderParams;
        RenderParams instanceParam;

        MeshFilter meshFilter;
        GameObject HighlightLayer;
        FusedMesh HighlightedHexes;

        private void Awake()
        {
            renderParams = new RenderParams(MainMaterial);
            instanceParam = new RenderParams(InstanceMaterial);

            HighlightLayer = gameObject.GetGameObject("HighlightLayer");

            HighlightLayer.transform.position += HighlightLayerOffset;

            meshFilter = HighlightLayer.GetComponent<MeshFilter>();
            meshFilter.mesh.MarkDynamic();
        }



        public void Initialize(BoundsInt aBounds)
        {
            ChunkBounds = aBounds;
            ChunkBounds.ClampToBounds(aBounds);

            hexes = new List<HexTile>(aBounds.size.x * aBounds.size.y);

            HighlightedHexes = new FusedMesh();

            boundsCheck = new Bounds(ChunkBounds.center, ChunkBounds.size);
        }

        public bool IsInChunk(HexTile hex)
        {
            if (ChunkBounds.Contains((Vector3Int)hex.GridCoordinates))
            {
                return true;
            }

            return false;
        }

        public bool IsInChunk(int x, int y)
        {
            // its wrong for some reason, idk why but you must use bounds

            if (boundsCheck.Contains(new Vector3Int(x, y, 0)))
            {
                return true;
            }

            return false;
        }
        public void AddHex(HexTile hex)
        {
            // the reason we use a concurrent bag is because it is thread safe
            // thus you can add to it from multiple from threads
            
            hexDictionary.TryAdd(hex.GridCoordinates, hex);

            if (bt.TryGetValue(hex.HexBiomeProperties,
                                    out ConcurrentBag<HexTile> hexes))
            {
                hexes.Add(hex);
            }
            else
            {
                hexes = new ConcurrentBag<HexTile>();
                hexes.Add(hex);
                bt.TryAdd(hex.HexBiomeProperties, hexes);
            }
        }
        private void SplitDictionary()
        {
            biomeTiles = bt.ToDictionary(x => x.Key, x => x.Value.ToList());
            bt.Clear();
        }

        public void IniaiteDrawProtocol()
        {
            hexes = hexDictionary.Values.ToList();
            SplitDictionary();
            FuseMeshes();
        }

        private void FuseMeshes()
        {
            List<Mesh> meshes = new List<Mesh>();
            List<int> hashes = new List<int>();
            List<Vector3> offsets = new List<Vector3>();

            // the below loop accounts for 90% of the time taken for this method 
            // this entire method alone accounts for 60% of the time taken to draw the entire map
            foreach (KeyValuePair<BiomeProperties, List<HexTile>> biomes in biomeTiles)
            {
                ExtractData(biomes.Value);

                biomeFusedMeshes.Add(biomes.Key,
                    new FusedMesh(meshes, hashes, offsets));
            }

            DrawMesh();

            void ExtractData(List<HexTile> hexes)
            {
                meshes.Clear();
                hashes.Clear();
                offsets.Clear();

                foreach (HexTile hex in hexes)
                {
                    meshes.Add(hex.DrawMesh());
                    hashes.Add(hex.GetHashCode());
                    offsets.Add(hex.Position);
                }
            }
        }

        private void DrawMesh()
        {
            // The downside of this is that every time you change the mesh of any fused mesh you have to recombine ALL the other meshes
            Mesh mainMeshes = FusedMesh.CombineToSubmesh(
                biomeFusedMeshes.Values.ToList());

            SetMaterialProperties();

            SetMaterialPropertyBlocks();

            GetComponent<MeshFilter>().mesh = mainMeshes;
            GetComponent<MeshCollider>().sharedMesh = mainMeshes;
        }

        /// <summary>
        /// Since we combined all of the individual meshes into one, there exist only one collider. THus we need to find the hex that was clicked on base on the position. 
        /// We do this by getting all the possible grid positions within the vicinity of the mouse click and then we measure between the positions of said grid positions and the mouse click position. The grid position with the smallest distance is the one that was clicked on.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public HexTile GetClosestHex(Vector3 position)
        {
            // average time to find hex is 0.002 seconds
            HexTile hex;
            List<Vector2Int> possibleGridCoords = new List<Vector2Int>();

            Vector2Int gridPos = GetGridCoordinate(position);
            possibleGridCoords.Add(gridPos);

            Vector2Int gridPos2 = new Vector2Int(gridPos.x + 1, gridPos.y);
            Vector2Int gridPos3 = new Vector2Int(gridPos.x - 1, gridPos.y);
            Vector2Int gridPos4 = new Vector2Int(gridPos.x, gridPos.y + 1);
            Vector2Int gridPos5 = new Vector2Int(gridPos.x, gridPos.y - 1);

            possibleGridCoords.Add(gridPos2);
            possibleGridCoords.Add(gridPos3);
            possibleGridCoords.Add(gridPos4);
            possibleGridCoords.Add(gridPos5);

            hex = GetClosestHex(possibleGridCoords, position);
            //Debug.Log("Hex: " + hex.GridCoordinates.ToString() + " Hit");
            return hex;

            HexTile GetClosestHex(List<Vector2Int> coords, Vector3 pos)
            {
                HexTile closestHex = null;

                float shortestDistance = float.MaxValue;

                HexTile posHex = null;

                foreach (Vector2Int item in coords)
                {
                    posHex = null;

                    hexDictionary.TryGetValue(item, out posHex);

                    if (posHex == null)
                    {
                        continue;
                    }

                    if (posHex != null)
                    {
                        float currDistance = Vector3.Distance(posHex.Position, pos);

                        if (currDistance < shortestDistance)
                        {
                            shortestDistance = currDistance;
                            closestHex = posHex;
                        }
                    }
                }

                return closestHex;
            }
        }

        private void RemoveHexFromLists(HexTile hex)
        {
            hexDictionary.TryRemove(hex.GridCoordinates, out HexTile hexTile);

            biomeTiles.Remove(hex.HexBiomeProperties);
        }

        public void RemoveHex(HexTile hex)
        {
            RemoveHexFromLists(hex);

            biomeFusedMeshes[hex.HexBiomeProperties].RemoveMesh(hex.GetHashCode());

            DrawMesh();

        }

        public void HighlightHex(HexTile hex)
        {
            HighlightedHexes.AddMesh(hexSettings.GetOuterHighlighter(),
                hex.GetHashCode(), hex.Position);

            meshFilter.mesh = HighlightedHexes.Mesh;
        }

        public void UnHighlightHex(HexTile hex)
        {
            HighlightedHexes.RemoveMesh(hex.GetHashCode());

            meshFilter.mesh = HighlightedHexes.Mesh;

        }
        private void SetMaterialProperties()
        {
            blocks.Clear();
            materials.Clear();

            if (BiomeVisual == BiomeVisual.Color)
            {
                SetMaterialColor();
            }
            else
            {
                SetMaterialTexture();
            }
        }
        private void SetMaterialColor()
        {
            for (int i = 0; i < biomeFusedMeshes.Count; i++)
            {
                Material newMat = new Material(MainMaterial);
                Color color = biomeFusedMeshes.Keys.ElementAt(i).BiomeColor;

                MaterialPropertyBlock block = new MaterialPropertyBlock();

                block.SetFloat("_UseColor", 1);
                block.SetColor("_Color", color);

                blocks.Add(block);

                materials.Add(newMat);
            }
        }
        private void SetMaterialTexture()
        {
            for (int i = 0; i < biomeFusedMeshes.Count; i++)
            {
                Material newMat = new Material(MainMaterial);

                Texture2D texture = biomeFusedMeshes.Keys.ElementAt(i).BiomeTexture;

                newMat.SetTexture("_MainTex", texture);

                materials.Add(newMat);
            }
        }
        public void SetMaterialPropertyBlocks()
        {
            Renderer renderer = GetComponent<Renderer>();

            int count = renderer.materials.Length;

            if (blocks.Count != count)
            {
                renderer.materials = materials.ToArray();
            }

            // for the time being, this will only adjust the color of the material
            for (int i = 0; i < blocks.Count; i++)
            {
                renderer.SetPropertyBlock(blocks[i], i);
            }
        }

        #region The Below is for Gpu Mesh generation, Dont touch unless you know what you are doing

        // the limit for graphic instances is 1000
        private static int maxLimit = 500;
        List<List<MyInstanceData>> data2 = new List<List<MyInstanceData>>();
        List<List<Vector4>> color2 = new List<List<Vector4>>();
        MyInstanceData[] data;
        public struct MyInstanceData
        {
            public Matrix4x4 objectToWorld; // We must specify object-to-world transformation for each instance
            public uint renderingLayerMask; // In addition we also like to specify rendering layer mask per instence.

            public int hexIndex;
        };

        private void SetData()
        {
            // Data
            data = new MyInstanceData[hexes.Count];
            data2.Clear();

            Vector3 transformOffset = transform.position;

            Parallel.For(0, hexes.Count, x =>
                {
                    MyInstanceData d = new MyInstanceData();
                    d.objectToWorld = Matrix4x4.Translate(hexes[x].Position + transformOffset);
                    d.renderingLayerMask = 0;

                    d.hexIndex = x;
                    data[x] = d;
                });

            while (data.Any())
            {
                data2.Add(data.Take(maxLimit).ToList());

                data = data.Skip(maxLimit).ToArray();
            }
        }

        private void SetColor()
        {
            color2.Clear();
            Vector4[] aColor;

            foreach (List<MyInstanceData> item in data2)
            {
                aColor = new Vector4[item.Count];

                Parallel.For(0, item.Count, x =>
                {
                    aColor[x] =
                    hexes[item[x].hexIndex].HexBiomeProperties.BiomeColor;
                });

                color2.Add(aColor.ToList());
            }
        }

        Mesh instanceMesh;
        MaterialPropertyBlock instanceBlock;
        public void RenderMesh()
        {
            if (data2.Count == 0)
            {
                SetData();
                instanceMesh = hexes[0].DrawMesh();
            }

            SetColor();

            int i = 0;

            foreach (List<MyInstanceData> item in data2)
            {
                instanceBlock = new MaterialPropertyBlock();

                Vector4[] v = color2.ElementAt(i).ToArray();

                instanceBlock.SetVectorArray("_MeshColors", v);
                instanceParam.matProps = instanceBlock;

                Graphics.RenderMeshInstanced(instanceParam, instanceMesh, 0, item);

                i++;
            }
        }

        #endregion

    }


    public struct SimplifiedHex
    {
        public List<HexTile> hexRows;

        public List<Vector3> Vertices;
        public List<int> Triangles;
        List<Vector2> UV;

        public Vector3 Position;

        public Mesh mesh;
        public SimplifiedHex(List<HexTile> rows)
        {
            Vertices = new List<Vector3>();
            Triangles = new List<int>();
            UV = new List<Vector2>();

            hexRows = rows;

            mesh = new Mesh();

            Position = rows[0].Position;

            Sort();

            Position = hexRows[0].Position;

            Simplify();
        }

        private void Sort()
        {
            // this list should already be sorted, this will be just in case
            hexRows.Sort((x, y) => x.GridCoordinates.x.CompareTo(y.GridCoordinates.x));

        }

        private static Vector2[] HexUV
        {
            get
            {
                return new Vector2[]
                {
                    new Vector2(0.5f, 1),
                    new Vector2(1, 0.75f),
                    new Vector2(1, 0.25f),
                    new Vector2(0.5f, 0),
                    new Vector2(0, 0.25f),
                    new Vector2(0, 0.75f)
                };
            }
        }

        private void Simplify()
        {
            HexTile hex;

            Vector3 leftTop = Vector3.zero;
            Vector3 leftBot = Vector3.zero;
            Vector3 rightTop = Vector3.zero;
            Vector3 rightBot = Vector3.zero;

            int[] topIndex = { 5, 0, 1 };
            int[] botIndex = { 4, 2, 3 };

            for (int i = 0; i < hexRows.Count; i++)
            {
                // from here we will test it based on materials etc, for now just simplify
                hex = hexRows[i];

                // normally we would set the edges incrementally, becuase the hex might have different materials im between

                if (i == 0)
                {
                    leftTop = hex.GetWorldVertexPosition(5);
                    leftBot = hex.GetWorldVertexPosition(4);
                }

                if (i == hexRows.Count - 1)
                {
                    rightTop = hex.GetWorldVertexPosition(1);
                    rightBot = hex.GetWorldVertexPosition(2);
                }

                // for uv mapping, these top and bottom edges have uv which are independent of the row they are placed, so we can just add them here
                // add top and bottom part of hex
                foreach (int num in topIndex)
                {
                    Vertices.Add(hex.GetWorldVertexPosition(num));
                    UV.Add(HexUV[num]);
                    Triangles.Add(Vertices.Count - 1);
                }

                foreach (int num in botIndex)
                {
                    Vertices.Add(hex.GetWorldVertexPosition(num));
                    UV.Add(HexUV[num]);
                    Triangles.Add(Vertices.Count - 1);
                }
            }

            // add the 2 mid main triangles
            Vertices.Add(leftBot); // -4
            UV.Add(GetUV(4, 1));

            Vertices.Add(leftTop); // - 3
            UV.Add(GetUV(5, 1));

            Vertices.Add(rightBot); // - 2
            UV.Add(GetUV(2, hexRows.Count));

            Vertices.Add(rightTop); // -1
            UV.Add(GetUV(1, hexRows.Count));

            Triangles.Add(Vertices.Count - 4);
            Triangles.Add(Vertices.Count - 3);
            Triangles.Add(Vertices.Count - 1);

            Triangles.Add(Vertices.Count - 4);
            Triangles.Add(Vertices.Count - 1);
            Triangles.Add(Vertices.Count - 2);

            mesh.vertices = Vertices.ToArray();
            mesh.triangles = Triangles.ToArray();
            mesh.uv = UV.ToArray();

        }

        private Vector2 GetUV(int hexSide, int rowCount)
        {
            // the reason we multiply the x by the row count is because since the mesh in a collection of multiple rows, we want our texture mapping to repeat for each hex
            Vector2 uv = HexUV[hexSide];

            uv.x = uv.x * rowCount;

            return uv;
        }
    }

}
