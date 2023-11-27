using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class pathfinder : MonoBehaviour
{
    public int[,] map = null;
    public int[,] start = null;
    public int[,] end = null;
    
    public int[,] dir =
    {
        {-1,0}, // Up
        {1,0}, // Down
        {0, -1}, // Left
        {0, 1 } // Right
    };

    List<int> path = new List<int>();

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void aStar() {

        path = FindPath(start, end);
        // if path is found
        if (path != null)
        {
            // write out in console the path it took
            for(int i = 0; i < path.Count; i++)
            {
                Console.Write(" " + path[i]);
            }
        }
        
    }
    List<int> FindPath(int[,] start, int[,] end) {

        return path;
    }
}
