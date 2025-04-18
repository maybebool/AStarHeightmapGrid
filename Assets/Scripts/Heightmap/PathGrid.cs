using System.Collections.Generic;
using UnityEngine;

namespace Heightmap {
    public class PathGrid {
        
        private PathNode[,] Grid { get; }
        private readonly Vector3 _origin;
        
        public PathGrid(int x, int y, Vector3 origin = default) {
            _origin = origin;
            Grid = new PathNode[x, y];
            for (int i = 0; i < y; i++) {
                for (int j = 0; j < x; j++) {
                    Grid[j, i] = new PathNode(new(j, i));
                }
            }
        }

        /// <summary>
        /// Retrieves all neighboring nodes of the specified node index within the grid.
        /// </summary>
        /// <param name="index">The index of the node to find neighbors for, represented as a 2D coordinate (Vector2Int).</param>
        /// <returns>An array of <see cref="PathNode"/> objects representing the neighboring nodes.</returns>
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
        
        public Vector3 GetWorldPositionFromNodeIndex(Vector2Int index, float yOffset, float cellSize) {
            return _origin + new Vector3((index.x + 0.5f) * cellSize, yOffset, (index.y + 0.5f) * cellSize);
        }
    }
}
