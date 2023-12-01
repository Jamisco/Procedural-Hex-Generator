using Assets.Scripts.WorldMap;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Assets.Scripts.WorldMap.Biosphere.SurfaceBody;
using static Assets.Scripts.WorldMap.GridManager;
using static Assets.Scripts.WorldMap.HexTile;
using static UnityEngine.GraphicsBuffer;

public class Pathfinder : MonoBehaviour
{
    // Anthony and Felicity A* Pathfinding Algo
    // Public variables.
    public GridManager manager;
    public PlanetGenerator planet;
    public GridData gridData;
    public Vector2Int startPoint;
    public Vector2Int endPoint;

    // A* specific structures
    private Dictionary<Vector2Int, Node> allNodes = new Dictionary<Vector2Int, Node>(); // all nodes (hexes) in the grid
    private List<Vector2Int> openSet = new List<Vector2Int>(); // list of nodes to be evaluated
    private HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>(); // set of nodes already evaluated

    private class Node
    {
        public Vector2Int Position;
        public float GCost; // Cost from start to this node
        public float HCost; // Heuristic cost from this node to end
        public float FCost => GCost + HCost; // Total cost
        public Node Parent;
    }

    // Private variables.
    List<Vector2Int> hexPositions = new List<Vector2Int>(); // used to store hex positions
    HexTile.HexVisualData hexColorData = new HexTile.HexVisualData(); // stores hex color data
    List<Vector2Int> algoPath = new List<Vector2Int>();
    private int log_out_ctr = 0;

    // a boolean to check if the user has started the pathfinding algo
    private bool isPathfindingStarted = false;
    public void BeginPathfinding()
    {
        isPathfindingStarted = true;
    }

    // initialize planet and manager game objects, grid is populated with hexes
    void Start()
    {
        manager = GetComponent<GridManager>();
        // Get the planet and generate it.
        planet = GetComponent<PlanetGenerator>();
        planet.MainPlanet.PlanetSize = gridData.GridSize;
        planet.GenerateData();

        // Init the biomes and generate it.
        List<BiomeData> biomes = planet.GetAllBiomes();
        manager.InitializeGrid(gridData, Biome_data_to_hex(biomes));
        manager.GenerateGrid();

        // Populate the list with positions of all hexes.
        for (int x = 0; x < gridData.GridSize.x; x++)
        {
            for (int y = 0; y < gridData.GridSize.y; y++)
            {
                hexPositions.Add(new Vector2Int(x, y));
            }
        }
        
        _re_paint_path();
    }

    // Update is called once per frame, used to update the visual of the path
    void Update()
    {
        if (isPathfindingStarted)
        {
            // Log printing counter.
            log_out_ctr++;
            log_out_ctr = log_out_ctr % (30);

            // Hidden class to re-make the biome.
            _re_make_biome();

            if (log_out_ctr == 0)
            {
                RunAStar();
            }

            // Re-paint the algorithm path currently.
            _re_paint_path();
            manager.DrawChunkInstanced();
        }
       
    }
    

    // init the nodes, adds the starting point to the open set, then evaluates the lowest cost nodes until end point or open set is empty
    private void RunAStar()
    {
        InitializeNodes();
        algoPath.Add(startPoint);
        openSet.Add(startPoint);
        allNodes[startPoint].GCost = 0;
        allNodes[startPoint].HCost = CalculateHeuristic(startPoint, endPoint);

        while (openSet.Count > 0)
        {
            Vector2Int current = GetLowestFCostNode(openSet);
            if (current == endPoint)
            {
                ReconstructPath(current);
                break;
            }

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (var neighbor in GetNeighbors(current))
            {
                if (closedSet.Contains(neighbor)) continue;

                float tentativeGCost = allNodes[current].GCost + GetTraversalCost(current, neighbor);
                if (tentativeGCost < allNodes[neighbor].GCost)
                {
                    allNodes[neighbor].Parent = allNodes[current];
                    allNodes[neighbor].GCost = tentativeGCost;
                    allNodes[neighbor].HCost = CalculateHeuristic(neighbor, endPoint);

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }
    }

    private void InitializeNodes()
    {
        allNodes.Clear();
        foreach (var hex in hexPositions)
        {
            allNodes.Add(hex, new Node { Position = hex, GCost = float.MaxValue });
        }
    }

    private float CalculateHeuristic(Vector2Int from, Vector2Int to)
    {
        return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
    }

    private List<Vector2Int> GetNeighbors(Vector2Int from)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        // These are the six directions for a pointy-topped hex grid
        Vector2Int[] directions = new Vector2Int[]
        {
        new Vector2Int(+1,  0), new Vector2Int(+1, -1), new Vector2Int( 0, -1),
        new Vector2Int(-1,  0), new Vector2Int(-1, +1), new Vector2Int( 0, +1)
        };

        foreach (Vector2Int direction in directions)
        {
            Vector2Int neighborPos = new(from.x + direction.x, from.y + direction.y);

            // Check if the neighbor is within the grid bounds
            if (neighborPos.x >= 0 && neighborPos.x < gridData.GridSize.x &&
                neighborPos.y >= 0 && neighborPos.y < gridData.GridSize.y)
            {
                neighbors.Add(neighborPos);
            }
        }

        return neighbors;
    }

    private float GetTraversalCost(Vector2Int from, Vector2Int to)
    {
        // retrieves biome data on the given vector position (the location we want to check what terrain hex it is)
        BiomeData toBiomeData = planet.GetBiomeData(to);

        // Check if the destination biome is of a type that should be in the closed set
        if (toBiomeData.Biome == Biomes.Ocean || toBiomeData.Biome == Biomes.Sea || toBiomeData.Biome == Biomes.Lake)
        {
            // Adding to the closed set to make sure the pathfinder never considers this tile
            closedSet.Add(to);
            return float.MaxValue; // Impassable terrain
        }

        switch (toBiomeData.Biome)
        {
            case Biomes.TemperateGrassland:
            case Biomes.TropicalRainforest:
            case Biomes.TemperateRainforest:
            case Biomes.BorealForest:
            case Biomes.Woodland:
                return 1.0f; // Normal traversal cost

            case Biomes.SubtropicalDesert:
            case Biomes.Tundra:
            case Biomes.Polar:
            case Biomes.PolarDesert:
                return 2.0f; // Higher traversal cost due to difficult terrain

            //... include other cases for different biomes as necessary

            default:
                return 1.0f; // Default traversal cost if biome is not specified
        }
    }

    private Vector2Int GetLowestFCostNode(List<Vector2Int> openSet)
    {
        Vector2Int lowest = openSet[0];
        for (int i = 1; i < openSet.Count; i++)
        {
            if (allNodes[openSet[i]].FCost < allNodes[lowest].FCost)
                lowest = openSet[i];
        }
        return lowest;
    }

    private void ReconstructPath(Vector2Int current)
    {
        algoPath.Clear();
        Node currentNode = allNodes[current];
        while (currentNode != null)
        {
            algoPath.Add(currentNode.Position);
            currentNode = currentNode.Parent;
        }
        algoPath.Reverse();
    }

    List<HexTile.HexVisualData> Biome_data_to_hex(List<BiomeData> all_biomes)
    {
        List<HexTile.HexVisualData> rt_hex = new List<HexTile.HexVisualData>();
        foreach (BiomeData biome in all_biomes)
        {
            HexTile.HexVisualData vis_dt = new HexTile.HexVisualData(biome.SeasonTexture, HexTile.HexVisualData.HexVisualOption.BaseTextures);
            vis_dt.SetColor(biome.BiomeColor);
            rt_hex.Add(vis_dt);
        }
        return rt_hex;
    }

    void _re_make_biome()
    {
        // Re-update the data. (to view changes)
        planet.GenerateData();
        List<BiomeData> biomes = planet.GetAllBiomes();
        manager.SetVisualData(hexPositions.ToArray(), Biome_data_to_hex(biomes).ToArray());
    }

    // Class to visualize the path.
    void _re_paint_path()
    {

        // The hexdata struct contains the value of the hex, and the chunk it is assigned to.
        // This is because each time you modify the visual data of a hex, the ENTIRE chunk must be redraw, since we group all hexes of thesame visual data into one draw call
        HexData startData = manager.GetHexData(startPoint);
        HexData endData = manager.GetHexData(endPoint);

        // Set the color of the start point to black.

        // you can then modify the visual data of the hex using methods provided by the HexData struct.
        startData.Highlight();
        endData.Highlight();

        // be advised that when changing the color or the material, you MUST set the hex visual option to "BaseTextures" or "Color" respectively so that the correct are visible.
        // so if you change the color, you must set the visual option to "Color" and if you change the texture, you must set the visual option to "BaseTextures", if you do not do this, changing the color will have no effect if the visual option is set to "BaseTextures"

        // you can change the visualdata by using the SetVisualData method of the GridManager class.
        // something like this

        //HexVisualData hexData = manager.GetVisualData(startPoint);

        //hexData.SetVisualOption(HexVisualData.HexVisualOption.Color);

        //manager.SetVisualData(startPoint, hexData);

    }

    void run_algo()
    {
        // If end not found.
        if (algoPath.Last() != endPoint)
        {
            RunAStar();
            if (log_out_ctr == 0)
                Debug.Log("Added Hex:" + algoPath.Last().ToString());
        }
        else
        {
            if (log_out_ctr == 0)
                Debug.Log("End Found");
        }
    }


#if UNITY_EDITOR
    [CustomEditor(typeof(Pathfinder))]
    public class PathfindingButtonEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            Pathfinder exampleScript = (Pathfinder)target;

            if (GUILayout.Button("Begin Pathfinding"))
            {
                Debug.Log("Button Works");
                exampleScript.BeginPathfinding();
            }
        }
    }
#endif

    // this method to set the start point from a UI input field
    public void SetStartPointFromInput(string input)
    {
        startPoint = ParseVector2Int(input);
    }

    //  this method to set the end point from a UI input field
    public void SetEndPointFromInput(string input)
    {
        endPoint = ParseVector2Int(input);
    }

    private Vector2Int ParseVector2Int(string input)
    {
        string[] parts = input.Split(',');
        if (parts.Length == 2)
        {
            return new Vector2Int(int.Parse(parts[0].Trim()), int.Parse(parts[1].Trim()));
        }
        return Vector2Int.zero; 
    }
}

