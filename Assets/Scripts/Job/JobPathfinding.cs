using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;


namespace Job {
    public class Pathfinding : MonoBehaviour {

        private const int MOVE_STRAIGHT_COST = 10;
        private const int MOVE_DIAGONAL_COST = 14;
        
        private void FindPath(int2 startPosition, int2 endPosition) {
            var gridSize = new int2(4, 4);
            var pathNodeArray = new NativeArray<PathNode>(gridSize.x * gridSize.y, Allocator.Temp);

            for (int x = 0; x < gridSize.x; x++) {
                for (int y = 0; y < gridSize.y; y++) {
                    var pathNode = new PathNode();
                    pathNode.X = x;
                    pathNode.Y = y;
                    pathNode.Index = CalculateIndex(x, y, gridSize.x);

                    pathNode.GCost = int.MaxValue;
                    pathNode.HCost = CalculateDistanceCost(new int2(x, y), endPosition);
                    pathNode.CalculateFCost();
                    pathNode.IsWalkable = true;
                    pathNode.CameFromNodeIndex = -1;

                    pathNodeArray[pathNode.Index] = pathNode;

                }
            }
            pathNodeArray.Dispose();
        }

        private int CalculateDistanceCost(int2 aPosition, int2 bPosition) {
            var xDistance = math.abs(aPosition.x - bPosition.x);
            var yDistance = math.abs(aPosition.y - bPosition.y);
            var remaining = math.abs(xDistance - yDistance);
            return MOVE_DIAGONAL_COST * math.min(xDistance, yDistance) + MOVE_STRAIGHT_COST * remaining;

        }


        private int CalculateIndex(int x, int y, int gridWidth) {
            return x + y * gridWidth;
        }
        
        private struct PathNode {
            public int X;
            public int Y;
            public int Index;

            public int GCost;
            public int HCost;
            public int FCost;

            public bool IsWalkable;

            public int CameFromNodeIndex;

            public void CalculateFCost() {
                FCost = GCost + HCost;
            }

        }
    }
}
