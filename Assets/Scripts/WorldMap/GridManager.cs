using Assets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Assets.Scripts.WorldMap.Planet;
using Assets.Scripts.WorldMap;
using static Assets.Scripts.WorldMap.HexTile;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System;
using UnityEditor;
using static Assets.Scripts.Miscellaneous.HexFunctions;
using Axial = Assets.Scripts.WorldMap.HexTile.Axial;
using System.Linq;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using static Assets.Scripts.WorldMap.HexChunk;
using System.Drawing;

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
        public int ChunkSize;

        public enum BiomeVisual { Color, Material }

        public BiomeVisual biomeVisual;

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

        Bounds mapBounds;

        private void SetBounds()
        {
            Vector3 center 
                = HexTile.GetPosition(mainPlanet.PlanetSize.x / 2, 0, mainPlanet.PlanetSize.y / 2);

            Vector3 start = GetPosition(Vector3Int.zero);
            Vector3 end = GetPosition(mainPlanet.PlanetSize.x, 0, mainPlanet.PlanetSize.y);

            mapBounds = new Bounds(center, end - start);
        }
        private void Awake()
        {  
            HexTiles = new Dictionary<Axial, HexTile>();

            hexChunks = new List<HexChunk>();

            SetGridSettings();

            GenerateGridChunks();

            SetBounds();
        }

        private void Update()
        {
            UpdateHexProperties();
           // CheckMouseHover();
            //UpdateMeshInstanced();
        }

        Stopwatch timer = new Stopwatch();
        string formattedTime = "";
        TimeSpan elapsedTime;

        #region Hex Generation Methods
        public void GenerateGridChunks()
        {
            timer.Start();
            HexTiles.Clear();

            SetGridSettings();

            planetGenerator.SetComputeSize();
            planetGenerator.ComputeBiomeNoise();

            HexTiles = HexTile.CreatesHexes(MapSize);

            //Debug.Log("Creation Took : " + (timer.ElapsedMilliseconds / 1000f).ToString("0.00") + " seconds");

            HexTile.CreateSlopes(HexTiles);

            //Debug.Log("Slopes Elapsed : " + (timer.ElapsedMilliseconds / 1000f).ToString("0.00") + " seconds");

            InitializeChunks();

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
        public HexTile GetHexTile(Axial coordinates)
        {
            HexTile hex = null;
            HexTiles.TryGetValue(coordinates, out hex);

            return hex;
        }

        public HexTile HoveredHex { get; private set; }

        private void CheckMouseHover()
        {
            if(!MouseOverMap())
            {
                return;
            }

            Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            Vector2Int mousePos = HexTile.GetGridCoordinate(pos);

            Debug.Log("Grid Position: " + mousePos.ToString());

            Axial axeCoord = Axial.ToAxial(mousePos);

            HoveredHex = GetHexTile(axeCoord);

            if(HoveredHex != null)
            {
                Debug.Log("Hovered Hex is at: " + HoveredHex.Position.ToString());
            }
            
        }

        private bool MouseOverMap()
        {
            Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            pos.y = 0;

            if(pos.x >= mapBounds.min.x && pos.x <= mapBounds.max.x)
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
