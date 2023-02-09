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


    public PathNode[] GetAllNeighbours(Vector2Int index)
    {
        var nodes = new List<PathNode>(9);
        for (int i = -1; i < 2; i++)
        {
            for (int j = -1; j < 2; j++)
            {
                var xPos = index.x + j;
                var yPos = index.y + i;

                if (xPos >= 0 && yPos >= 0 && xPos < _grid.GetLength(0) && yPos < _grid.GetLength(1))
                {
                    nodes.Add(_grid[xPos,yPos]);
                }
            }
        }

        return nodes.ToArray();
    }

    public PathNode GetRandomNode()
    {
        return _grid[Random.Range(0, _grid.GetLength(0)), Random.Range(0, _grid.GetLength(1))];
    }
    
}
