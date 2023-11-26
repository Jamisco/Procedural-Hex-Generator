using Assets.Scripts.WorldMap;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Assets.Scripts.WorldMap.Biosphere.SurfaceBody;
using static Assets.Scripts.WorldMap.GridManager;

public class pathfinder : MonoBehaviour
{
    // Public variables.
    public GridManager manager;
    public PlanetGenerator planet;
    public GridData gridData;
    public Color hex_color;
    public Vector2Int start_point;
    public Vector2Int end_point;

    // A* specific structures
    private Dictionary<Vector2Int, Node> allNodes = new Dictionary<Vector2Int, Node>();
    private List<Vector2Int> openSet = new List<Vector2Int>();
    private HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

    private class Node
    {
        public Vector2Int Position;
        public float GCost; // Cost from start to this node
        public float HCost; // Heuristic cost from this node to end
        public float FCost => GCost + HCost; // Total cost
        public Node Parent;
    }

    // Private variables.
    List<Vector2Int> hex_pos = new List<Vector2Int>();
    HexTile.HexVisualData hexColorData = new HexTile.HexVisualData();
    // Will be changed by the algorithm.
    // Should be a list of (x, y) positions. (each adjacent pos should be one after another)
    List<Vector2Int> algo_path = new List<Vector2Int>();
    int log_out_ctr = 0;


    // Start is called before the first frame update
    void Start()
    {
        manager = GetComponent<GridManager>();

        // Get the planet and generate it.
        planet = GetComponent<PlanetGenerator>();
        planet.MainPlanet.PlanetSize = gridData.MapSize;
        planet.GenerateData();

        // Init the biomes and generate it.
        List<BiomeData> biomes = planet.GetAllBiomes();
        manager.InitializeGrid(gridData, Biome_data_to_hex(biomes));
        manager.GenerateGrid();

        // Populate the list with positions of all hexes.
        for (int x = 0; x < gridData.MapSize.x; x++)
        {
            for (int y = 0; y < gridData.MapSize.y; y++)
            {
                hex_pos.Add(new Vector2Int(x, y));
            }
        }

       
        algo_path.Add(start_point);
    }

    // Update is called once per frame
    void Update()
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

    private void RunAStar()
    {
        InitializeNodes();

        openSet.Add(start_point);
        allNodes[start_point].GCost = 0;
        allNodes[start_point].HCost = CalculateHeuristic(start_point, end_point);

        while (openSet.Count > 0)
        {
            Vector2Int current = GetLowestFCostNode(openSet);
            if (current == end_point)
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
                    allNodes[neighbor].HCost = CalculateHeuristic(neighbor, end_point);

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }
    }

    private void InitializeNodes()
    {
        allNodes.Clear();
        foreach (var hex in hex_pos)
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
            Vector2Int neighborPos = new Vector2Int(hex.x + direction.x, hex.y + direction.y);

            // Check if the neighbor is within the grid bounds
            if (neighborPos.x >= 0 && neighborPos.x < gridData.MapSize.x &&
                neighborPos.y >= 0 && neighborPos.y < gridData.MapSize.y)
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
            case Biomes.TemperateGrassland:
            case Biomes.TemerateSeasonalForest:
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
        algo_path.Clear();
        Node currentNode = allNodes[current];
        while (currentNode != null)
        {
            algo_path.Add(currentNode.Position);
            currentNode = currentNode.Parent;
        }
        algo_path.Reverse();
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
        manager.SetVisualData(hex_pos.ToArray(), Biome_data_to_hex(biomes).ToArray());
    }

    // Class to visualize the path.
    void _re_paint_path()
    {
        // Set the color to what the user wants the path to have.
        hexColorData.SetColor(hex_color);
        // Color the path.
        SetVisualData(algo_path.ToArray(), hexColorData);
    }

    // Overloaded function that calls the path vector to be colored the same color.
    public void SetVisualData(Vector2Int[] path, HexTile.HexVisualData color)
    {
        foreach (Vector2Int hx in path)
        {
            manager.SetVisualData(hx, color);
        }
    }

    void run_algo()
    {
        // If end not found.
        if (algo_path.Last() != end_point)
        {
            go_left_algo();
            if (log_out_ctr == 0)
                Debug.Log("Added Hex:" + algo_path.Last().ToString());
        }
        else
        {
            if (log_out_ctr == 0)
                Debug.Log("End Found");
        }
    }

}