// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
//
// public class Pathfinding : MonoBehaviour
// {
//     [SerializeField] int samplesPerDimension = 4;
//     [SerializeField] TerrainInfo terrainInfo;
//     [SerializeField] TerrainInfoUI terrainInfoUI;
//
//     PathGrid<PathNode> grid;
//
//     List<PathNode> closedNodes = new List<PathNode>();
//     List<PathNode> openNodes = new List<PathNode>();
//
//     private void Update()
//     {
//         if (Input.GetKeyDown(KeyCode.Space))
//         {
//             InitializePathGrid();
//             terrainInfo.SetAllColor(Color.white);
//
//
//             PathNode start = grid.GetRandomNode();
//             PathNode end = grid.GetRandomNode();
//             while (end.IsWall)
//             {
//                 end = grid.GetRandomNode();
//             }
//             System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
//             stopwatch.Start();
//             List<PathNode> path = FindPath(start, end);
//             stopwatch.Stop();
//             Debug.Log("Seconds to find path: " + (double)stopwatch.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency);
//             if (path != null)
//             {
//                 foreach (var node in path)
//                 {
//                     terrainInfo.SetColor(node.Index, Color.magenta);
//                 }
//             }
//             terrainInfo.SetColor(start.Index, Color.green);
//             terrainInfo.SetColor(end.Index, Color.yellow);
//             terrainInfoUI.ShowPatchInfo(grid.Grid);
//         }
//
//     }
//
//     private List<PathNode> FindPath(PathNode start, PathNode end)
//     {
//         //Debug.Log($"Find path from {start.Index} to  {end.Index}");
//         start.GCost = 0;
//         start.HCost = DistanceBetweenNodes(start, end);
//         openNodes.Add(start);
//
//         while (openNodes.Count > 0)
//         {
//             PathNode currentNode = GetNodeWithLowestCost();
//             if (currentNode == end) return GetFinalPath(end);
//
//             openNodes.Remove(currentNode);
//             closedNodes.Add(currentNode);
//
//             var neighbours = grid.GetAllNeighbours(currentNode.Index);
//             foreach (var neighbour in neighbours)
//             {
//                 if (neighbour.IsWall)
//                 {
//                     closedNodes.Add(neighbour);
//                     continue;
//                 }
//                 if (!closedNodes.Contains(neighbour))
//                 {
//                     bool isDiagonal = neighbour.Index.x != currentNode.Index.x && neighbour.Index.y != currentNode.Index.y;
//                     //if (isDiagonal) continue; // disable diagonal walking . .remove to enable
//                     float expectedCost = currentNode.GCost + (isDiagonal ? 1.4f : 1f);
//                     float flyCost = (terrainInfo.cubes[neighbour.Index.x, neighbour.Index.y].localScale.y - terrainInfo.cubes[currentNode.Index.x, currentNode.Index.y].localScale.y)/50f;
//                     //Debug.Log($"Neighbour Height {terrainInfo.cubes[neighbour.Index.x, neighbour.Index.y].lossyScale.y}, this node height {terrainInfo.cubes[currentNode.Index.x, currentNode.Index.y].lossyScale.y}, cost {flyCost}");
//                     expectedCost += flyCost;
//                     if (expectedCost < neighbour.GCost)
//                     {
//                         neighbour.SourceNode = currentNode;
//                         neighbour.GCost = expectedCost;
//                         neighbour.FlyCost = flyCost; // also included in Gcost
//                         neighbour.HCost = DistanceBetweenNodes(neighbour, end);
//                     }
//
//                     if (!openNodes.Contains(neighbour))
//                         openNodes.Add(neighbour);
//                 }
//             }
//
//         }
//         // No path available
//         Debug.Log("Found no Path from" + start.Index + " to " + end.Index);
//         return null;
//     }
//
//
//     private List<PathNode> GetFinalPath(PathNode node)
//     {
//         PathNode currentNode = node;
//         List<PathNode> path = new List<PathNode>();
//         path.Add(currentNode);
//         while (currentNode.SourceNode != null)
//         {
//             path.Add(currentNode.SourceNode);
//             currentNode = currentNode.SourceNode;
//         }
//         path.Reverse();
//         return path;
//     }
//
//     private PathNode GetNodeWithLowestCost()
//     {
//         PathNode lowestCostNode = openNodes[0];
//         foreach (PathNode node in openNodes)
//         {
//             //Debug.Log("LookAtNode is:" + node.Index + " with costs " + node.GCost + "  " + node.HCost + "  " + node.FCost);
//             if (node.FCost < lowestCostNode.FCost)
//                 lowestCostNode = node;
//         }
//
//         return lowestCostNode;
//     }
//
//     private float DistanceBetweenNodes(PathNode start, PathNode end)
//     {
//         return Vector2.Distance(new Vector2(start.X, start.Y), new Vector2(end.X, end.Y));
//     }
//
//     private void InitializePathGrid()
//     {
//         grid = new PathGrid<PathNode>(samplesPerDimension, samplesPerDimension);
//         closedNodes.Clear();
//         openNodes.Clear();
//     }
// }
