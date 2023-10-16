using System.Collections.Generic;
using UnityEngine;
using static Assets.Scripts.WorldMap.HexTile;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System;
using UnityEditor;
using Axial = Assets.Scripts.WorldMap.HexTile.Axial;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using System.Xml.Linq;
using Assets.Scripts.Miscellaneous;
using static Assets.Scripts.WorldMap.GridManager;
using static Assets.Scripts.Miscellaneous.ExtensionMethods;
using Unity.VisualScripting;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements.Experimental;
using System.Collections;

// Namespace.
namespace Assets.Scripts.WorldMap
{
    [System.Serializable]
    public class GridManager : MonoBehaviour
    {
        public HexSettings HexSettings;
        public GameObject hexParent;
        public HexChunk hexChunkPrefab;

        private List<HexChunk> hexChunks;
        Dictionary<Axial, HexTile> HexTiles;

        public Material MainMaterial;
        public Material InstanceMaterial;

        public PlanetGenerator planetGenerator;
        Planet mainPlanet;

        private Vector2Int MapSize;
        private Bounds MapBounds;
        public int ChunkSize;

        public enum BiomeVisual { Color, Material }

        public BiomeVisual biomeVisual;

        public Dictionary<int, HexData> HighlightedHexes;

        private void SetGridSettings()
        {
            HexChunk.MainMaterial = MainMaterial;
            HexChunk.InstanceMaterial = InstanceMaterial;

            HexChunk.BiomeVisual = biomeVisual;

            HexTile.Grid = this;
            HexTile.Planet = planetGenerator;

            HexTile.hexSettings = HexSettings;

            planetGenerator.MainPlanet.Initialize();

            mainPlanet = planetGenerator.MainPlanet;
            MapSize = mainPlanet.PlanetSize;
        }
        private void SetBounds()
        {
            Vector3 center
                = HexTile.GetPosition(mainPlanet.PlanetSize.x / 2, 0, mainPlanet.PlanetSize.y / 2);

            Vector3 start = GetPosition(Vector3Int.zero);
            Vector3 end = GetPosition(mainPlanet.PlanetSize.x, 0, mainPlanet.PlanetSize.y);

            MapBounds = new Bounds(center, end - start);
        }
        private void Awake()
        {
            HexTiles = new Dictionary<Axial, HexTile>();

            hexChunks = new List<HexChunk>();

            HighlightedHexes = new Dictionary<int, HexData>();

            SetGridSettings();

            GenerateGridChunks();
        }

        [Tooltip("If true when mouse is over hex, mouse will highlight, if false, mouse will highlight when clicked")]
        public bool HoverHighlight = false;
        private void Update()
        {
            UpdateHexProperties();

            if (HoverHighlight)
            {
                HighlightOnHover();
            }
            else
            {
                HighlightOnClick();

            }
        }

        #region Hex Generation Methods
        public void GenerateGridChunks()
        {
            // Time starts for 300 x 300

            //Computer Biome : .044-- 1.2 %
            //Create Chunks: .05-- 1.4 %
            //Create Hexes: 1.257-- 36.4 %

            //Splitting : .355 - 10.3 %

            //Fusing : 2.029 - 58.87 %

            //Draw Mesh: 2.406 - 69.82 %

            //Generation : 3.446

            // Time stats for 1000 x 1000

            // Compute Biome: .457
            // Create Chunks: .516
            // Create Hexes: 14.943

            // be advised this time is when we drew the chunks using the coroutine
            // What ultimately matters is the time it takes to draw the chunks, which can be circumvented by using a coroutine to draw them bit by bit, or else the game will freeze for a while depending on the size of the map
            
            // Generation Time: 14.976

            Stopwatch sw = new Stopwatch();
            sw.Start();

            hexSettings.ResetVariables();
            HexTiles.Clear();
            HighlightedHexes.Clear();

            DestroyChildren();

            SetGridSettings();

            planetGenerator.SetComputeSize();
            planetGenerator.ComputeBiomeNoise();

            LogTimer("Compute Biome: ", sw.ElapsedMilliseconds);

            CreateHexChunks();

            LogTimer("Create Chunks: ", sw.ElapsedMilliseconds);

            HexTiles = HexTile.CreatesHexes(MapSize, ref hexChunks);

            LogTimer("Create Hexes: ", sw.ElapsedMilliseconds);

            StartCoroutine(SpawnChunkEveryXSeconds(0));

            

            SetBounds();

            sw.Stop();

            LogTimer("Generation Time: ", sw.ElapsedMilliseconds);
        }
        public void InitializeChunks()
        {
            CreateHexChunks();

            Parallel.ForEach(HexTiles.Values, (hexTile) =>
            {
                hexChunks.First(h => h.IsInChunk(hexTile.X, hexTile.Y)).AddHex(hexTile);
            });

            //Debug.Log("Adding To Chunk Elapsed : " + (timer.ElapsedMilliseconds / 1000f).ToString("0.00") + " seconds");


        }

        private IEnumerator SpawnChunkEveryXSeconds(float time)
        {
            for(int i = 0; i < hexChunks.Count; i++)
            {
                hexChunks[i].IniaiteDrawProtocol();    
                yield return new WaitForSeconds(time);
            }
        }

        private void CreateHexChunks()
        {
            hexChunks.Clear();

            // 6 for the base hex, 6 for each slope on each side of the hex
            int maxHexVertCount = 42;

            // the max vert count of combined mesh. Unity side limit
            int maxVertCount = 65535;

            int maxHexCount = maxVertCount / maxHexVertCount;

            ChunkSize = (int)Mathf.Sqrt(maxHexCount);

            // if the map is small enough such that it can fit in out chunk size, we use the map size instead
            if (ChunkSize * ChunkSize > MapSize.x * MapSize.y)
            {
                // Since all chunks will be squares, we use the smaller of the two map sizes
                ChunkSize = Mathf.Max(MapSize.x, MapSize.y);
            }

            //ChunkSize -= 1;

            int chunkCountX = Mathf.CeilToInt((float)MapSize.x / ChunkSize);
            int chunkCountZ = Mathf.CeilToInt((float)MapSize.y / ChunkSize);

            HexChunk chunk;

            for (int z = 0; z < chunkCountZ; z++)
            {
                for (int x = 0; x < chunkCountX; x++)
                {
                    bool inX = (x + 1) * ChunkSize <= MapSize.x;
                    bool inZ = (z + 1) * ChunkSize <= MapSize.y;

                    Vector3Int start = new Vector3Int();
                    Vector3Int size = new Vector3Int();

                    start.x = x * ChunkSize;
                    start.y = z * ChunkSize;

                    if (inX)
                    {
                        size.x = ChunkSize;
                    }
                    else
                    {
                        size.x = MapSize.x - start.x;
                    }

                    if (inZ)
                    {
                        size.y = ChunkSize;
                    }
                    else
                    {
                        size.y = MapSize.y - start.y;
                    }

                    BoundsInt bounds = new BoundsInt(start, size);

                    chunk = Instantiate(hexChunkPrefab, hexParent.transform);
                    chunk.Initialize(bounds);
                    hexChunks.Add(chunk);
                }
            }

            //Debug.Log("Chunk Size: " + ChunkSize);
            //Debug.Log("Chunk count: " + hexChunks.Count);
        }
        public bool UpdateMap = false;
        public void UpdateHexProperties()
        {
            if (!UpdateMap)
            {
                SetChildrenStatus(true);
                return;
            }

            SetChildrenStatus(false);

            planetGenerator.SetComputeSize();
            planetGenerator.ComputeBiomeNoise();

            // 100 x100 10 fps
            foreach (HexChunk chunk in hexChunks)
            {
                chunk.RenderMesh();
            }
        }
        private void DestroyChildren()
        {
            int childCount = hexParent.transform.childCount;

            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform child = hexParent.transform.GetChild(i);
                DestroyImmediate(child.gameObject);
                // If you want to use DestroyImmediate instead, replace the line above with:
                // DestroyImmediate(child.gameObject);
            }
        }

        bool chunkOn = true;
        private void SetChildrenStatus(bool status)
        {
            if (chunkOn == status)
            {
                return;
            }

            int childCount = hexParent.transform.childCount;

            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform child = hexParent.transform.GetChild(i);
                child.gameObject.SetActive(status);
            }

            chunkOn = status;
        }

        #endregion

        /// <summary>
        /// This is slower because it will loop through all the hexes
        /// </summary>
        /// <param name="coordinates"></param>
        /// <returns></returns>
        public HexTile GetHexTile(Vector2Int coordinates)
        {
            HexTile hex = null;
            hex = HexTiles.Values.First(h => h.GridCoordinates == coordinates);

            return hex;
        }

        /// <summary>
        /// This is the main function to get a hex tiles. Uses a dictionary, so it is super fast
        /// </summary>
        /// <param name="coordinates"></param>
        /// <returns></returns>
        public HexTile GetHexTile(Axial coordinates)
        {
            HexTile hex = null;
            HexTiles.TryGetValue(coordinates, out hex);

            return hex;
        }
        private void HighlightOnClick()
        {
            HexData newData;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                newData = GetHexDataAtMousePosition();

                if (newData.IsNullOrEmpty())
                {
                    return;
                }

                HighlightHex(newData);
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                newData = GetHexDataAtMousePosition();

                if (newData.IsNullOrEmpty())
                {
                    return;
                }

                newData.Remove();
            }
        }
        private void HighlightOnHover()
        {
            HexData newData = GetHexDataAtMousePosition();

            if (newData.IsNullOrEmpty())
            {
                return;
            }

            //Debug.Log("Hex at: " + newData.hex.GridCoordinates);

            HighlightHex(newData);
        }

        public void HighlightHex(HexData hex)
        {
            if (hex.IsNullOrEmpty())
            {
                return;
            }

            if (!HighlightedHexes.ContainsKey(hex.Hash))
            {
                HighlightedHexes.Add(hex.Hash, hex);
                hex.Highlight();
            }
        }

        public void UnHighlightHex(HexData hex)
        {
            if (hex.IsNullOrEmpty())
            {
                return;
            }

            if (HighlightedHexes.ContainsKey(hex.Hash))
            {
                HighlightedHexes.Remove(hex.Hash);
                hex.UnHighlight();
            }
        }


        private HexData GetHexDataAtMousePosition()
        {
            HexData data = new HexData();

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 1000))
            {
                HexChunk chunk = hit.collider.GetComponentInParent<HexChunk>();

                if (chunk == null)
                {
                    return data;
                }

                // we subtract the position of the chunk because the hexes are positioned relative to the chunk, so if a chunk is at 0,10 and the hex is at 0,0, the hex is actually at 0,10,0 in world position

                HexTile foundHex = chunk.GetClosestHex(hit.point - transform.position);

                data.chunk = chunk;
                data.hex = foundHex;

                return data;
            }

            return data;
        }

        /// <summary>
        /// We use this struct to store the data a hex. This way we dont have to find the chunk. Used for highlighting
        /// </summary>
        public struct HexData
        {
            public HexChunk chunk;
            public HexTile hex;
            public int Hash { get { return GetHashCode(); } }
            public HexData(HexChunk chunk, HexTile aHex)
            {
                this.chunk = chunk;
                this.hex = aHex;
            }

            public void Highlight()
            {
                if (chunk != null && hex != null)
                {
                    chunk.HighlightHex(hex);
                }
            }

            public void Remove()
            {
                if (chunk != null && hex != null)
                {
                    chunk.RemoveHex(hex);
                    ResetData();
                }
            }
            public void UnHighlight()
            {
                if (chunk != null && hex != null)
                {
                    chunk.UnHighlightHex(hex);
                    ResetData();
                }
            }

            public void ResetData()
            {
                chunk = null;
                hex = null;
            }
            public static bool operator ==(HexData hex1, HexData hex2)
            {
                return hex1.Equals(hex2);
            }

            public static bool operator !=(HexData hex1, HexData hex2)
            {
                return !hex1.Equals(hex2);
            }

            bool Equals(HexData other)
            {
                if (chunk == other.chunk)
                {
                    if (hex == other.hex)
                    {
                        return true;
                    }
                }

                return false;
            }

            public override bool Equals(object obj)
            {
                if (obj != null)
                {
                    HexData hex;

                    try
                    {
                        hex = (HexData)obj;
                    }
                    catch (Exception)
                    {
                        return false;
                    }

                    return Equals(hex);
                }

                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(hex, chunk);
            }

            public bool IsNullOrEmpty()
            {
                if (chunk == null && hex == null)
                {
                    return true;
                }

                return false;
            }
        }


#if UNITY_EDITOR
        [CustomEditor(typeof(GridManager))]
        public class ClassButtonEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                GridManager exampleScript = (GridManager)target;

                if (GUILayout.Button("Generate Grid"))
                {
                    exampleScript.GenerateGridChunks();
                }
            }
        }
#endif

    }
}