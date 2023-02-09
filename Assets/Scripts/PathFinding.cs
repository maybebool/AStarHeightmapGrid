using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathFinding : MonoBehaviour
{

    private List<PathNode> openNodes = new();
    private List<PathNode> closedNodes = new();
    private TerrainInfo _terrainInfo;

    private PathGrid _pathGrid;
    
    public List<PathNode> FindPath(PathNode start, PathNode end)
    {
        if (_pathGrid == null || _terrainInfo == null)
        {
            Debug.Log("No grid or terrain");
            return null;
        }
        start.GCost = 0;
        start.HCost = Vector2.Distance(start.Index, end.Index);
        start.FlyCost = 0;
        openNodes.Add(start);
        while (openNodes.Count > 0)
        {
            PathNode currentNode = GetLowestCostNode();
            if (currentNode == end) // TODO: Return final path 
            {
                
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
                    var flyCost = _terrainInfo.
                }
            }

        }

        return null;
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
