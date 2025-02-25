using System.Collections.Generic;
using UnityEngine;


namespace Heightmap {
    public class PathGrid {
        
        private PathNode[,] Grid { get; }
        
        public PathGrid(int x, int y) {
            Grid = new PathNode[x, y];
            for (int i = 0; i < y; i++) {
                for (int j = 0; j < x; j++) {
                    Grid[j, i] = new PathNode(new(j, i));
                }
            }
        }


        public PathNode[] GetAllNeighbours(Vector2Int index) {
            var nodes = new List<PathNode>(9);
            for (int i = -1; i < 2; i++) {
                for (int j = -1; j < 2; j++) {
                    var xPos = index.x + j;
                    var yPos = index.y + i;

                    if (xPos >= 0 && yPos >= 0 && xPos < Grid.GetLength(0) && yPos < Grid.GetLength(1)) {
                        nodes.Add(Grid[xPos,yPos]);
                    }
                }
            }
            return nodes.ToArray();
        }
        
        public Vector2Int GetNodeIndexFromWorldPosition(Vector3 worldPosition) {
            int x = Mathf.FloorToInt(worldPosition.x);
            int y = Mathf.FloorToInt(worldPosition.z); // Using Z for the second coordinate
            return new Vector2Int(x, y);
        }

        // Returns the node at the given grid index.
        // Returns null if the index is out of bounds.
        public PathNode GetNodeAt(Vector2Int index) {
            if (index.x >= 0 && index.x < Grid.GetLength(0) && 
                index.y >= 0 && index.y < Grid.GetLength(1)) {
                return Grid[index.x, index.y];
            }
            return null;
        }

        public PathNode GetRandomNode() {
            return Grid[Random.Range(0, Grid.GetLength(0)), Random.Range(0, Grid.GetLength(1))];
        }
    }
}
