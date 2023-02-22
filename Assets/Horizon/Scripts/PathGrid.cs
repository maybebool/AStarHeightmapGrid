// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
//
// public class PathGrid<T> where T : PathNode
// {
//     private T[,] grid;
//     public T[,] Grid { get { return grid; } }
//
//     public PathGrid(int width, int height)
//     {
//         grid = new T[width, height];
//         for (int i = 0; i < width; i++)
//         {
//             for (int j = 0; j < height; j++)
//             {
//                 grid[i, j] = (T)new PathNode(new Vector2Int(i, j), i, j);
//             }
//         }
//     }
//
//     public T[] GetAllNeighbours(Vector2Int index)
//     {
//         List<T> neighbours = new List<T>();
//         for (int i = -1; i < 2; i++)
//         {
//             for (int j = -1; j < 2; j++)
//             {
//                 int x = index.x + i;
//                 int y = index.y + j;
//
//                 if (x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1))
//                 {
//                     // Fucking remove the "Filter out middle" as it was flawed logic.
//                     neighbours.Add(grid[x, y]);
//                     //Debug.Log("Neighbour found: " + x + " / " + y);                        
//                 }
//             }
//         }
//         return neighbours.ToArray();
//     }
//
//     // New Function
//     public T GetRandomNode()
//     {
//         int x = Random.Range(0, grid.GetLength(0));
//         int y = Random.Range(0, grid.GetLength(1));
//         return grid[x, y];
//     }
//
//     // New Function
//     public T GetNode(int x, int y)
//     {
//         return grid[x, y];
//     }
// }
