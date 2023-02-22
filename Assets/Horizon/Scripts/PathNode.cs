// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
//
// public class PathNode
// {
//     public Vector2Int Index { get; private set; }
//
//     public float X { get; private set; }
//     public float Y { get; private set; }
//
//     public float GCost { get; set; }
//     public float HCost { get; set; }
//     public float FCost { get => GCost + HCost; }
//     public float FlyCost { get; set; }
//
//     public bool IsWall { get; set; } = false;
//     // Node we came from
//     public PathNode SourceNode { get; set; }
//
//     public PathNode(Vector2Int index, float x, float y)
//     {
//         Index = index;
//         X = x;
//         Y = y;
//         GCost = float.MaxValue;
//     }
// }
