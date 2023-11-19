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

        // Init go left algo at start point.
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
            run_algo();
        }

        // Re-paint the algorithm path currently.
        _re_paint_path();
        manager.DrawChunkInstanced();
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


    // Make a sample go-left algo. (x+1, y)
    void go_left_algo()
    {
        Vector2Int prev_point = algo_path.Last();
        algo_path.Add(new Vector2Int(prev_point.x + 1, prev_point.y));
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
