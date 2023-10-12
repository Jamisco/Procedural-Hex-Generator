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

        public HexData HighlightedHex;

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

        Stopwatch timer = new Stopwatch();
        string formattedTime = "";
        TimeSpan elapsedTime;

        #region Hex Generation Methods
        public void GenerateGridChunks()
        {
            hexSettings.ResetVariables();
            timer.Start();
            HexTiles.Clear();

            DestroyChildren();

            SetGridSettings();

            planetGenerator.SetComputeSize();
            planetGenerator.ComputeBiomeNoise();

            HexTiles = HexTile.CreatesHexes(MapSize);

            //Debug.Log("Creation Took : " + (timer.ElapsedMilliseconds / 1000f).ToString("0.00") + " seconds");

            HexTile.CreateSlopes(HexTiles);

            //Debug.Log("Slopes Elapsed : " + (timer.ElapsedMilliseconds / 1000f).ToString("0.00") + " seconds");

            InitializeChunks();

            SetBounds();

            timer.Stop();

            elapsedTime = timer.Elapsed;

            formattedTime = $"{elapsedTime.Minutes}m : {elapsedTime.Seconds} s : {elapsedTime.Milliseconds} ms";

            Debug.Log("Generation Took : " + formattedTime);

            timer.Reset();
        }
        public void InitializeChunks()
        {
            CreateHexChunks();

            Parallel.ForEach(HexTiles.Values, (hexTile) =>
            {
                hexChunks.First(h => h.IsInChunk(hexTile.X, hexTile.Y)).AddHex(hexTile);
            });

            //Debug.Log("Adding To Chunk Elapsed : " + (timer.ElapsedMilliseconds / 1000f).ToString("0.00") + " seconds");

            foreach (HexChunk chunk in hexChunks)
            {
                chunk.DrawMesh();
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

            if (ChunkSize > MapSize.x || ChunkSize > MapSize.y)
            {
                // Since all chunks will be squares, we use the smaller of the two map sizes
                ChunkSize = Mathf.Min(MapSize.x, MapSize.y);
            }

            ChunkSize -= 1;

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

        void HighlightOnClick()
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                HexData newData = GetHexDataAtMousePosition();

                if (newData.IsNullOrEmpty())
                {
                    return;
                }

                //Debug.Log("Hex at: " + newData.hex.GridCoordinates);

                //unhighlight the old hex
                HighlightedHex.UnHighlight();
                HighlightedHex = newData;
                //highlight the new one
                HighlightedHex.Highlight();
            }

            if(Mouse.current.rightButton.wasPressedThisFrame)
            {
                HighlightedHex.UnHighlight();
            }
        }

        void HighlightOnHover()
        {
            HexData newData = GetHexDataAtMousePosition();

            if (newData.IsNullOrEmpty())
            {
                return;
            }

            //Debug.Log("Hex at: " + newData.hex.GridCoordinates);

            if (newData != HighlightedHex)
            {
                //unhighlight the old hex
                HighlightedHex.UnHighlight();
                HighlightedHex = newData;
                //highlight the new one
                HighlightedHex.Highlight();
            }
        }

        public HexData GetHexDataAtMousePosition()
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

            public void UnHighlight()
            {
                if (chunk != null && hex != null)
                {
                    chunk.UnHighlightHex();
                    ResetData();
                }
            }

            public void ResetData()
            {
                if (chunk != null && hex != null)
                {
                    chunk.UnHighlightHex();
                }
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