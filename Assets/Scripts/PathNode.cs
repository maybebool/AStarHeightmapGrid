using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathNode
{
    public Vector2Int Index;
    // /// <summary>
    // /// X Pos in world space (Unity.Units)
    // /// </summary>
    // public float X;
    // /// <summary>
    // /// Y Pos in world space (Unity.Units)
    // /// </summary>
    // public float Y;

    public float GCost { get; set; }  // Distance from current node to start node (Path length)
    public float HCost { get; set; }  // Heuristic (estimated Distance from current to end) 
    public float FCost { get => GCost + HCost + FlyCost; }  // Total cost
    public float FlyCost { get; set; } // cost to fly up/down 
    
    public bool isWall { get; set; }

    public PathNode SourceNode;
    public TerrainInfo terrainInfo;

    public PathNode(Vector2Int index) {
        Index = index;
        // X = x;
        // Y = y;
        GCost = float.MaxValue;
    }
}
