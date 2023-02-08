using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathGrid
{
    private PathNode[,] _grid;
    public PathNode[,] Grid { get => _grid; }

    public PathGrid(int x, int y)
    {
        _grid = new PathNode[x, y];
        for (int i = 0; i < y; i++)
        {
            for (int j = 0; j < x; j++)
            {
                _grid[j, i] = new PathNode(new(j, i));
            }
        }
    }
}
