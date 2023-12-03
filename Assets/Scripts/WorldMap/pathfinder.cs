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
        RunAStar();  // Start the pathfinding process
        _re_paint_path();  // Highlight the path after pathfinding is done
    }

    // initialize planet and manager game objects, grid is populated with hexes
    void Start()
    {
        manager = GetComponent<GridManager>();
        // Get the planet and generate it.
        manager.SetGridData(gridData);
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
    }

    // Update is called once per frame, used to update the visual of the path
    void Update()
    {
        if (isPathfindingStarted)
        {
           // Re-paint the algorithm path currently.
            _re_paint_path();
            manager.DrawChunkInstanced();
        }
       
    }
    

    // init the nodes, adds the starting point to the open set, then evaluates the lowest cost nodes until end point or open set is empty
    private void RunAStar()
{
    // Initialize all nodes with maximum float value for GCost
    InitializeNodes();

    // Add the start point to the open set
    algoPath.Add(startPoint);
    openSet.Add(startPoint);
    
    // The cost from start to start is zero
    allNodes[startPoint].GCost = 0;
    
    // Calculate the heuristic cost from start to end
    allNodes[startPoint].HCost = CalculateHeuristic(startPoint, endPoint);

    // While there are nodes to evaluate
    while (openSet.Count > 0)
    {
        // Find the node with the lowest F cost
        Vector2Int current = GetLowestFCostNode(openSet);
        
        // If the current node is the end point, reconstruct the path
        if (current == endPoint)
        {
            ReconstructPath(current);
            return; // Exit the function as the path has been found
        }

        // Move the current node from open to closed set
        openSet.Remove(current);
        closedSet.Add(current);

        // Evaluate all neighbors
        foreach (var neighbor in GetNeighbors(current))
        {
            // Skip if the neighbor is in the closed set
            if (closedSet.Contains(neighbor)) continue;

            // Calculate the G cost from start to the neighbor through current
            float tentativeGCost = allNodes[current].GCost + GetTraversalCost(current, neighbor);
            
            // If the new path to neighbor is shorter, or neighbor is not in open set
            if (tentativeGCost < allNodes[neighbor].GCost || !openSet.Contains(neighbor))
            {
                // Update the neighbor node's costs and parent
                allNodes[neighbor].Parent = allNodes[current];
                allNodes[neighbor].GCost = tentativeGCost;
                allNodes[neighbor].HCost = CalculateHeuristic(neighbor, endPoint);

                // Add the neighbor to the open set if it's not there
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
        // Clear any existing path
        algoPath.Clear();
    
        // Start from the end node
        Node currentNode = allNodes[current];
    
        // Trace back from end node to start node
        while (currentNode != null)
        {
            algoPath.Add(currentNode.Position);
            currentNode = currentNode.Parent; // Move to the parent node
        }
    
        // Reverse the path to get it from start to end
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
    if (!isPathfindingStarted) return; // Only highlight if pathfinding has started

    // Highlight start point
    HexVisualData startHexData = manager.GetVisualData(startPoint);
    startHexData.SetVisualOption(HexVisualData.HexVisualOption.Color);
    startHexData.SetColor(Color.magenta); // Start point color
    manager.SetVisualData(startPoint, startHexData);

    // Highlight end point
    HexVisualData endHexData = manager.GetVisualData(endPoint);
    endHexData.SetVisualOption(HexVisualData.HexVisualOption.Color);
    endHexData.SetColor(Color.magenta); // End point color
    manager.SetVisualData(endPoint, endHexData);

    // Highlight the path
    foreach (Vector2Int pathNode in algoPath)
    {
        if(pathNode != startPoint && pathNode != endPoint) // Avoid recoloring the start/end points
        {
            HexVisualData pathNodeData = manager.GetVisualData(pathNode);
            pathNodeData.SetVisualOption(HexVisualData.HexVisualOption.Color);
            pathNodeData.SetColor(Color.cyan); // Path color
            manager.SetVisualData(pathNode, pathNodeData);
        }
    }

    manager.DrawChunkInstanced(); // This line will trigger the grid manager to update the visuals on the screen
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

