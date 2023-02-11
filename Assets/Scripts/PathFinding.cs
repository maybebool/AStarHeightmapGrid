using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class PathFinding : MonoBehaviour
{
    [SerializeField] private int samplesPerDimension;
    [SerializeField] private float flyCostMultiplier = 1.25f;

    private List<PathNode> openNodes = new();
    private List<PathNode> closedNodes = new();
    [SerializeField] private TerrainInfo terrainInfo;

    private PathGrid _pathGrid;

    private void OnValidate()
    {
        samplesPerDimension =  Mathf.Clamp(Mathf.ClosestPowerOfTwo(samplesPerDimension), 2, int.MaxValue);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _pathGrid = new PathGrid(samplesPerDimension, samplesPerDimension);
            var start = _pathGrid.GetRandomNode();
            var end = _pathGrid.GetRandomNode();
            while (start == end)
            {
                end = _pathGrid.GetRandomNode();
            }
            
            terrainInfo.SetAllColor(Color.black);
            
            

            var path = FindPath(start, end);
            foreach (var node in path)
            {
                Debug.Log($"Node: {node.Index}, FCost: {node.FCost}");
                terrainInfo.SetColor(node.Index, Color.white);
            }
            
            terrainInfo.SetColor(start.Index, Color.blue);
            terrainInfo.SetColor(end.Index, Color.magenta);

        }
    }

    public List<PathNode> FindPath(PathNode start, PathNode end)
    {
        if (_pathGrid == null || terrainInfo == null)
        {
            Debug.Log("No grid or terrain");
            return null;
        }
        openNodes.Clear();
        closedNodes.Clear();

        var terrainHeights = terrainInfo.SampleHeights(samplesPerDimension, false);
        start.GCost = 0;
        start.HCost = Vector2.Distance(start.Index, end.Index);
        start.FlyCost = 0;
        openNodes.Add(start);
        while (openNodes.Count > 0)
        {
            PathNode currentNode = GetLowestCostNode();
            if (currentNode == end) // TODO: Return final path 
            {
                return GetFinalPath(currentNode);
            }

            openNodes.Remove(currentNode);
            closedNodes.Add(currentNode);
            var neighbours = _pathGrid.GetAllNeighbours(currentNode.Index);
            foreach (var neighbour in neighbours)
            {
                if (neighbour.isWall)
                {
                    closedNodes.Add(neighbour);
                    continue;
                }

                if (!closedNodes.Contains(neighbour))
                {
                    var isDiagonal = currentNode.Index.x != neighbour.Index.x &&
                                     currentNode.Index.y != neighbour.Index.y;

                    var gCost = currentNode.GCost + (isDiagonal ? 1.4f : 1);
                    var flyCost = (terrainHeights[neighbour.Index.x, neighbour.Index.y] -
                                   terrainHeights[currentNode.Index.x, currentNode.Index.y]) * flyCostMultiplier; // divide by 50 to make values less 

                    if (gCost + flyCost < neighbour.GCost)
                    {
                        neighbour.GCost = gCost;
                        neighbour.FlyCost = flyCost;
                        neighbour.HCost = Vector2.Distance(neighbour.Index, end.Index);
                        neighbour.SourceNode = currentNode;
                    }
                    if (!openNodes.Contains(neighbour))
                    {
                        openNodes.Add(neighbour);
                    }
                }

                
            }

        }

        Debug.Log("No path found. Make sure and end are accessible");
        return null;
    }

    private List<PathNode> GetFinalPath(PathNode endNode)
    {
        List<PathNode> finalPath = new();
        var pathNode = endNode;
        finalPath.Add(pathNode);
        while (pathNode.SourceNode != null)
        {
            pathNode = pathNode.SourceNode;
            finalPath.Add(pathNode);
        }

        finalPath.Reverse();
        return finalPath;
    }

    private PathNode GetLowestCostNode()
    {
        var lowestCostNode = openNodes[0];
        var lowestCost = lowestCostNode.FCost;
        foreach (var node in openNodes)
        {
            var nodeCost = node.FCost;
            if (node.FCost < lowestCost)
            {
                lowestCost = nodeCost;
                lowestCostNode = node;
            }
        }

        return lowestCostNode;
    }
}
