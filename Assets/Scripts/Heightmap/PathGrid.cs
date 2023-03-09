using System.Collections.Generic;
using UnityEngine;


namespace Heightmap {
    public class PathGrid
    {
        public PathNode[,] Grid { get; }

        // fix problem here
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

        public PathNode GetRandomNode() {
            return Grid[Random.Range(0, Grid.GetLength(0)), Random.Range(0, Grid.GetLength(1))];
        }
    
    }
}
