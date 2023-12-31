using Assets.Scripts.Miscellaneous;
using Assets.Scripts.WorldMap;
using Assets.Scripts.WorldMap.Biosphere;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using static Assets.Scripts.WorldMap.Biosphere.SurfaceBody;
using static Assets.Scripts.WorldMap.GridManager;
using static Assets.Scripts.WorldMap.HexTile;
using static Assets.Scripts.WorldMap.HexTile.HexVisualData;
using static UnityEngine.GraphicsBuffer;

[RequireComponent(typeof(GridManager))]
[RequireComponent(typeof(BoxCollider))]
public class GridSpawner : MonoBehaviour
{
    public GridManager manager;
    public PlanetGenerator planet;
    public GridData gridData;

    Vector2Int mapSize;

    public bool useColor = true;


    public Vector2Int startPoint;
    public Vector2Int endPoint;

    private void Awake()
    {

    }
    void Start()
    {
        Begin();
    }



    private void Update()
    {
        OnMouseClick();
    }


    public void GetData()
    {
        Biomes startData = planet.GetBiomeData(startPoint).Biome;

        int weight = SurfaceBody.GetWeight(startData);

    }

    void Begin()
    {
        manager = GetComponent<GridManager>();
        planet = GetComponent<PlanetGenerator>();

        List<HexVisualData> visualData;

        mapSize = gridData.GridSize;

        planet.MainPlanet.PlanetSize = gridData.GridSize;
        planet.GenerateData();

        visualData = ConvertToHexVisual(planet.GetAllBiomes());

        manager.InitializeGrid(gridData, visualData);

        // manager.GetBoxCollider(ref boxCollider);

        manager.GenerateGrid();
        //SetHexVisualData();

    }

    void SetHexVisualData()
    {
        planet.GenerateData();

        List<BiomeData> datas = planet.GetAllBiomes();

        for (int x = 0; x < mapSize.x; x++)
        {
            for (int y = 0; y < mapSize.y; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);

                BiomeData data = planet.GetBiomeData(pos);

                HexVisualData hData = ConvertToHexVisual(data);

                manager.SetVisualData(pos, hData);
            }
        }
    }

    private List<HexVisualData> ConvertToHexVisual(List<BiomeData> datas)
    {
        List<HexVisualData> hexVisuals = new List<HexVisualData>();

        foreach (BiomeData data in datas)
        {
            hexVisuals.Add(ConvertToHexVisual(data));
        }

        return hexVisuals;
    }

    private HexVisualData ConvertToHexVisual(BiomeData data)
    {
        HexVisualData hData = new HexVisualData(data.BiomeColor,
                              data.WeatherTexture, null, 0f);

        if (useColor)
        {
            hData.SetVisualOption(HexVisualOption.Color);
        }
        else
        {
            hData.SetVisualOption(HexVisualOption.BaseTextures);
        }

        return hData;
    }


    Dictionary<int, HexData> HighlightedHexes = new Dictionary<int, HexData>();
    Dictionary<int, HexData> ActivatedBorderHexes = new Dictionary<int, HexData>();
    private void OnMouseClick()
    {
        HexData newData;
        Vector3 mousePos;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            mousePos = GetMousePosition();

            if (!manager.PositionInGrid(mousePos))
            {
                return;
            }

            newData =
                manager.GetHexDataAtPosition(mousePos);

            if (newData.IsNullOrEmpty())
            {
                return;
            }

            newData.Highlight();

            BiomeData da = planet.GetBiomeData(newData.GridCoordinates);

            Debug.Log("Clicked On Hex: " + newData.GridCoordinates.ToString() + " Biome: " + da.Biome.ToString());
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            mousePos = GetMousePosition();

            if(!manager.PositionInGrid(mousePos))
            {
                return;
            }

            newData =
               manager.GetHexDataAtPosition(mousePos);

            if (!newData.IsNullOrEmpty())
            {
                newData.UnHighlight();
            }
        }
    }

    HexData previousData = new HexData();
    private void HighlightOnHover()
    {
        Vector3 mousePos = GetMousePosition();

        if (!manager.PositionInGrid(mousePos))
        {
            return;
        }

        // hextile.GetGridCoordinate() is not accurate
        HexData newData = manager.GetHexDataAtPosition(mousePos);

        if (newData.IsNullOrEmpty() || newData == previousData)
        {
            return;
        }

        previousData.DeactivateBorder();

        newData.ActivateBorder();
        previousData = newData;

        //Debug.Log("Hovering Over Hex: " + newData.GridCoordinates.ToString());
    }

    private void DisableUnseenChunks()
    {
        Bounds bounds = Camera.main.OrthographicBounds3D();

        manager.SetChunkStatusIfInBoundsOtherwise(bounds, true);
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
            // hex.Highlight();


            hex.SetColor(Color.black);
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
            // hex.UnHighlight();
            //hex.UnHighlight();

            Color ogColor = Color.red;

            hex.SetColor(ogColor);
        }
    }

    private Vector3 GetMousePosition()
    {
        Vector3 mousePos = Mouse.current.position.ReadValue();

        mousePos = Camera.main.ScreenToWorldPoint(mousePos);

        return mousePos;
    }


#if UNITY_EDITOR
    [CustomEditor(typeof(GridSpawner))]
    public class ClassButtonEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GridSpawner exampleScript = (GridSpawner)target;

            if (GUILayout.Button("Generate Grid"))
            {
                exampleScript.Begin();
            }
        }
    }
#endif
}
